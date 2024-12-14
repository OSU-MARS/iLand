// C++/core/{ seeddispersal.h, seeddispersal.cpp }
using iLand.Extensions;
using iLand.Tool;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExternalSeedBeltSector = iLand.Input.ProjectFile.ExternalSeedBeltSector;
using Model = iLand.Simulation.Model;
using Project = iLand.Input.ProjectFile.Project;
using ProjectDirectory = iLand.Input.ProjectFile.ProjectDirectory;

namespace iLand.Tree
{
    /** @class SeedDispersal
        The class encapsulates the dispersal of seeds of one species over the whole landscape.
        The dispersal algortihm operate on grids with a 20m resolution.

        See https://iland-model.org/dispersal
        */
    public partial class SeedDispersal // partial with Regex precompilation
    {
        private bool hasPendingSerotiny; // true if active (unprocessed) pixels are on the extra-serotiny map
        private readonly Grid<float> kernelMastYear; // species specific "seed kernel" (small) for seed years
        // TODO: created but unused in post-2016 C++
        //private readonly Grid<float> kernelNonMastYear; // species specific "seed kernel" (small) for non-seed-years
        private readonly Grid<float> kernelSerotiny; // seed kernel for extra seed rain
        private float nonMastYearFraction; // fraction of the seed production in non-seed-years
        private readonly Grid<float> seedMapSerotiny; ///< seed map that keeps track of serotiny events (only for serotinous species)
        private readonly Grid<float> saplingSeedMap; ///< seed map that collects seed distribution from sapling trees
        private bool saplingMapCreated; ///< flag that indicates if a map for saplings has been created
        private readonly Grid<float> sourceMap; // (large) seedmap used to denote the sources
        private float treeMigAlphas1; // TreeMig seed dispersal parameters
        private float treeMigAlphaS2;
        private float treeMigKappaS;
        // TODO: remove unused
        // private float treeMigFecundityPerCell; // maximum seeds per source cell
        private float treeMigOccupancy; // seeds required per destination regeneration pixel

        // external seeds
        private float externalSeedBackgroundInput; // background propability for this species; if set, then a certain seed availability is provided for the full area
        private Grid<float> externalSeedBaseMap; // intermediate data while setting up external seeds
        private int externalSeedBufferWidth; // how many 20 m pixels away from the model area should the seeding start?
        // TODO: can this be made species specific?
        private readonly SortedList<WorldFloraID, List<float>> externalSeedData; // holds definition of species and percentages for external seed input
        // TODO: convert to flags enum
        private byte externalSeedDirection; // direction of external seeds
        private int externalSeedSectorX = 0; // size of the sectors used to specify external seed input
        private int externalSeedSectorY = 0;
        private readonly Grid<float> externalSeedMap; // for more complex external seed input, this map holds that information
        private bool hasExternalSeedInput; // if true, external seeds are modelled for the species

        // long distance dispersal
        private readonly List<float> longDistanceDispersalSeedsByRing;  // long distance dispersal # of cells that should be affected in each "ring", C++ mLDDDensity
        private float longDistanceDispersalMaximumSeedlingDensity; // value of the kernel function that is the threshold for full coverage and LDD, respectively
        private float longDistanceDispersalMinimumSeedlingDensity;
        private int longDistanceDispersalRingCount; // # of rings (with equal probability) for LDD, C++ mLDDRings
        private float longDistanceDispersalSeedlingsPerCell; // each LDD pixel has this probability, C++ mLDDSeedlings
        private readonly List<float> longDispersalDistance; // long distance dispersal distances (e.g. the "rings"), C++ mLDDDistance

        private bool writeSeedMapsToImages; // if true, seedmaps are stored as images

        public Grid<float> SeedMap { get; private init; } // (large) seedmap. Is filled by individual trees and then processed
        public TreeSpecies Species { get; private init; }

        public SeedDispersal(TreeSpecies species)
        {
            this.externalSeedBaseMap = new();
            this.externalSeedData = [];
            this.externalSeedMap = new();
            this.kernelMastYear = new(); // species specific "seed kernel" (small) for seed years
            // this.kernelNonMastYear = new(); // species specific "seed kernel" (small) for non-seed-years
            this.kernelSerotiny = new(); // seed kernel for extra seed rain
            this.longDispersalDistance = []; // long distance dispersal distances (e.g. the "rings")
            this.longDistanceDispersalSeedsByRing = [];  // long distance dispersal # of cells that should be affected in each "ring"
            this.saplingMapCreated = false;
            this.saplingSeedMap = new();
            this.seedMapSerotiny = new(); // seed map that keeps track of serotiny events
            this.sourceMap = new(); // (large) seedmap used to denote the sources

            this.SeedMap = new(); // (large) seedmap. Is filled by individual trees and then processed
            this.Species = species;
        }

        ///< initial values at the beginning of the year for the grid
        public void Clear(Model model) // SeedDispersal::newYear()
        {
            Grid<float> seedMap = this.sourceMap;
            if (this.externalSeedMap.IsSetup())
            {
                // we have a preprocessed initial value for the external seed map (see setupExternalSeeds() et al)
                seedMap.CopyFrom(externalSeedMap);

                if (this.saplingSeedMap.IsSetup() == false)
                {
                    // add the data from the sapling map if available
                    Debug.Assert((seedMap.CellCount == this.saplingSeedMap.CellCount) && (seedMap.CellsX == this.saplingSeedMap.CellsX) && (seedMap.CellsY == this.saplingSeedMap.CellsY));
                    for (int seedCellIndex = 0; seedCellIndex < this.saplingSeedMap.CellCount; ++seedCellIndex)
                    {
                        seedMap[seedCellIndex] += this.saplingSeedMap[seedCellIndex];
                    }
                }
                return;
            }

            // clear the map
            // version >2016: background seeds are applied *after* distribution
            seedMap.Fill(0.0F);
            if (this.hasExternalSeedInput)
            {
                // if external seed input is enabled, the buffer area of the seed maps is
                // "turned on", i.e. set to 1.
                int bufferWidth = (int)(model.Project.World.Geometry.BufferWidthInM / seedMap.CellSizeInM);
                // if a special buffer is defined, reduce the size of the input
                if (this.externalSeedBufferWidth > 0)
                {
                    bufferWidth -= this.externalSeedBufferWidth;
                }
                if (bufferWidth <= 0.0F)
                {
                    throw new NotSupportedException("Remaining seed buffer width is negative.");
                }

                // scale external seed values to have pixels with LAI=3
                // TODO: rather than traversing all cells and filtering, traverse only relevant cells
                float fullFecundityLai = Constant.Sapling.FullFecundityLai * seedMap.CellSizeInM * seedMap.CellSizeInM;
                for (int indexY = 0; indexY < seedMap.CellsY; ++indexY)
                {
                    for (int indexX = 0; indexX < seedMap.CellsX; ++indexX)
                    {
                        if ((indexY < bufferWidth) || (indexY >= seedMap.CellsY - bufferWidth) || (indexX < bufferWidth) || (indexX >= seedMap.CellsX - bufferWidth))
                        {
                            if (this.externalSeedDirection == 0)
                            {
                                // seeds from all directions
                                seedMap[indexX, indexY] = fullFecundityLai;
                            }
                            else
                            {
                                // seeds only from specific directions
                                if (((this.externalSeedDirection & 0x1) == 0x1) && (indexX >= seedMap.CellsX - bufferWidth))
                                {
                                    seedMap[indexX, indexY] = fullFecundityLai; // north
                                }
                                else if (((this.externalSeedDirection & 0x2) == 0x2) && (indexY < bufferWidth))
                                {
                                    seedMap[indexX, indexY] = fullFecundityLai; // east
                                }
                                else if (((this.externalSeedDirection & 0x4) == 0x4) && (indexX < bufferWidth))
                                {
                                    seedMap[indexX, indexY] = fullFecundityLai; // south
                                }
                                else if (((this.externalSeedDirection & 0x8) == 0x8) && (indexY >= seedMap.CellsY - bufferWidth))
                                {
                                    seedMap[indexX, indexY] = fullFecundityLai; // west
                                }
                            }
                        }
                    }
                }
            }

            // add the data from the sapling map if available
            if (this.saplingSeedMap.IsSetup())
            {
                Debug.Assert((seedMap.CellCount == this.saplingSeedMap.CellCount) && (seedMap.CellsX == this.saplingSeedMap.CellsX) && (seedMap.CellsY == this.saplingSeedMap.CellsY));
                for (int seedCellIndex = 0; seedCellIndex < this.saplingSeedMap.CellCount; ++seedCellIndex)
                {
                    seedMap[seedCellIndex] += this.saplingSeedMap[seedCellIndex];
                }
            }
        }

        ///< clear
        public void ClearSaplingMap() // C++: SeedDispersal::clearSaplingMap()
        {
            if (this.saplingSeedMap.IsSetup())
            {
                this.saplingSeedMap.Fill(0.0F);
            }
        }

        ///< initializes / creates the kernel
        public void CreateKernel(Grid<float> kernel, float scaleArea) // C++: SeedDispersal::createKernel()
        {
            float maxDistance = this.TreeMigDistanceforProbability(longDistanceDispersalMaximumSeedlingDensity / this.Species.FecundityM2);
            float seedCellSize = this.SeedMap.CellSizeInM;
            // e.g.: cell_size: regeneration grid (e.g. 400qm), px-size: light-grid (4qm)
            float occupation = seedCellSize * seedCellSize / (Constant.Grid.LightCellSizeInM * Constant.Grid.LightCellSizeInM * treeMigOccupancy);

            int maxCellDistance = (int)(maxDistance / seedCellSize);
            kernel.Setup(2 * maxCellDistance + 1, 2 * maxCellDistance + 1, this.SeedMap.CellSizeInM);
            int kernelOffset = maxCellDistance;

            // filling of the kernel.... use the treemig density function
            float dist_center_cell = MathF.Sqrt(seedCellSize * seedCellSize / MathF.PI);
            Point kernelCenter = new(kernelOffset, kernelOffset);
            for (int kernelIndex = 0; kernelIndex < kernel.CellCount; ++kernelIndex)
            {
                float value = kernel.GetCenterToCenterDistance(kernelCenter, kernel.GetCellXYIndex(kernelIndex));
                if (value == 0.0F)
                {
                    kernel[kernelIndex] = this.TreeMigCenterCell(dist_center_cell); // r is the radius of a circle with the same area as a cell
                }
                else
                {
                    kernel[kernelIndex] = value <= maxDistance ? ((this.TreeMig(value + dist_center_cell) + this.TreeMig(value - dist_center_cell)) / 2.0F * seedCellSize * seedCellSize) : 0.0F;
                }
            }

            // normalize
            float sum = kernel.Sum();
            if ((sum == 0.0F) || (occupation == 0.0F))
            {
                // TODO: shouldn't kernel probabilities sum to 1.0?
                throw new NotSupportedException("Sum of probabilities is zero.");
            }

            // the sum of all kernel cells has to equal 1 (- long distance dispersal)
            kernel.Multiply(scaleArea / sum);

            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("kernel setup. Species: " + Species.ID + " kernel-size: " + kernel.CellsX + " x " + kernel.CellsY + " pixels, sum (after scaling): " + kernel.Sum());
            //}
        }

        ///< run the seed dispersal
        public void DisperseSeeds(Project project, RandomGenerator random) // C++: SeedDispersal::execute()
        {
            if (this.writeSeedMapsToImages)
            {
                throw new NotSupportedException("Saving of seedmaps is only supported in the iLand GUI.");
            }

            // *********************************************
            // current version (>=2016)
            // *********************************************

            // special case serotiny
            if (this.hasPendingSerotiny)
            {
                this.DistributeSeeds(project, serotiny: true, this.seedMapSerotiny, this.kernelSerotiny, this.Species.FecunditySerotiny, random);

                // copy back data
                Debug.Assert((this.SeedMap.CellCount == this.seedMapSerotiny.CellCount) && (this.SeedMap.CellsX == this.seedMapSerotiny.CellsX) && (this.SeedMap.CellsY == this.seedMapSerotiny.CellsY));
                for (int seedCellIndex = 0; seedCellIndex < this.SeedMap.CellCount; ++seedCellIndex)
                {
                    float cellValue = this.SeedMap[seedCellIndex];
                    float serotinyValue = this.seedMapSerotiny[seedCellIndex];
                    if (cellValue < serotinyValue)
                    {
                        this.SeedMap[seedCellIndex] = serotinyValue;
                    }
                }

                this.seedMapSerotiny.Fill(0.0F); // clear
                this.hasPendingSerotiny = false;
            }

            // distribute actual values
            // *** estimate seed production (based on leaf area) ***
            // calculate number of seeds; the source map holds now m2 leaf area on 20x20m pixels
            // after this step, each source cell has a value between 0 (no source) and 1 (fully covered cell)
            float seedFecundity = this.Species.FecundityM2;
            if (this.Species.IsMastYear == false)
            {
                seedFecundity *= this.nonMastYearFraction;
            }
            // fill seed map from source map
            this.DistributeSeeds(project, serotiny: false, this.sourceMap, this.kernelMastYear, seedFecundity, random);

            // there is potentially a background probability >0 for all pixels.
            if (this.externalSeedBackgroundInput > 0.0F)
            {
                // add a constant number of seeds on the map
                for (int seedCellIndex = 0; seedCellIndex < this.SeedMap.CellCount; ++seedCellIndex)
                {
                    // TODO: why is this limited to [0, 1]?
                    float cellValue = Maths.Limit(this.SeedMap[seedCellIndex] + this.externalSeedBackgroundInput, 0.0F, 1.0F);
                    this.SeedMap[seedCellIndex] = cellValue;
                }
            }
        }

        /// do the actual seed distribution processing
        /// main seed distribution function
        /// distributes seeds using distribution kernels and long distance dispersal from source cells
        /// see https://iland-model.org/seed+kernel+and+seed+distribution
        private void DistributeSeeds(Project project, bool serotiny, Grid<float> sourcemap, Grid<float> kernel, float fec, RandomGenerator random)
        {
            float laiToFecundityFraction = 1.0F / (Constant.Sapling.FullFecundityLai * sourcemap.CellSizeInM * sourcemap.CellSizeInM);
            for (int seedCellIndex = 0; seedCellIndex < sourcemap.CellCount; ++seedCellIndex)
            {
                // if LAI  >3, then full potential is assumed, below LAI=3 a linear ramp is used;
                // the value of *p is the sum(LAI) of seed producing trees on the cell
                float cellValue = laiToFecundityFraction * sourcemap[seedCellIndex];
                Debug.Assert(cellValue >= 0.0F);
                if (cellValue > 1.0F)
                {
                    cellValue = 1.0F;
                }
                sourcemap[seedCellIndex] = cellValue;
            }

            // source mode
            // *** seed distribution (Kernel + long distance dispersal) ***
            int offset = kernel.CellsX / 2; // offset is the index of the center pixel
            if (project.World.Geometry.IsTorus == false)
            {
                // ** standard case (no torus) **
                for (int seedCellIndex = 0; seedCellIndex < sourcemap.CellCount; ++seedCellIndex)
                {
                    float cellFecundity = sourcemap[seedCellIndex];
                    if (cellFecundity > 0.0F)
                    {
                        Point sm = sourcemap.GetCellXYIndex(seedCellIndex);
                        int sx = sm.X - offset;
                        int sy = sm.Y - offset;
                        for (int iy = 0; iy < kernel.CellsY; ++iy)
                        {
                            for (int ix = 0; ix < kernel.CellsX; ++ix)
                            {
                                if (this.SeedMap.IsIndexValid(sx + ix, sy + iy))
                                {
                                    this.SeedMap[sx + ix, sy + iy] += cellFecundity * kernel[ix, iy];
                                }
                            }
                        }

                        // long distance dispersal
                        if ((serotiny == false) && (this.longDistanceDispersalSeedsByRing.Count > 0))
                        {
                            for (int r = 0; r < this.longDistanceDispersalSeedsByRing.Count; ++r)
                            {
                                float ldd_val = this.longDistanceDispersalSeedlingsPerCell / fec; // pixels will have this probability [note: fecundity will be multiplied below]
                                int n;
                                if (this.longDistanceDispersalSeedsByRing[r] < 1)
                                {
                                    n = random.GetRandomProbability() < this.longDistanceDispersalSeedsByRing[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)MathF.Round(this.longDistanceDispersalSeedsByRing[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    float radius = random.GetRandomFloat(this.longDispersalDistance[r], this.longDispersalDistance[r + 1]) / this.SeedMap.CellSizeInM; // choose a random distance (in pixels)
                                    float phi = random.GetRandomProbability() * 2.0F * Single.Pi; // choose a random direction
                                    Point ldd = new(sm.X + (int)(radius * MathF.Cos(phi)),
                                                    sm.Y + (int)(radius * MathF.Sin(phi)));
                                    if (this.SeedMap.IsIndexValid(ldd))
                                    {
                                        this.SeedMap[ldd] += ldd_val;
                                    }
                                }
                            }
                        }

                    }
                }
            }
            else
            {
                // **** seed distribution in torus mode ***
                int seedmap_offset = sourcemap.GetCellXYIndex(0.0F, 0.0F).X; // the seed maps have x extra rows/columns
                Point torus_pos;
                int seedpx_per_ru = (int)(Constant.Grid.ResourceUnitSizeInM / sourcemap.CellSizeInM);
                for (int seedCellIndex = 0; seedCellIndex <= sourcemap.CellCount; ++seedCellIndex)
                {
                    float cellFecundity = sourcemap[seedCellIndex];
                    if (cellFecundity > 0.0F)
                    {
                        Point sm = sourcemap.GetCellXYIndex(seedCellIndex);
                        // get the origin of the resource unit *on* the seedmap in *seedmap-coords*:
                        Point offset_ru = new((sm.X - seedmap_offset) / seedpx_per_ru * seedpx_per_ru + seedmap_offset,
                                              (sm.Y - seedmap_offset) / seedpx_per_ru * seedpx_per_ru + seedmap_offset); // coords RU origin

                        Point offset_in_ru = new((sm.X - seedmap_offset) % seedpx_per_ru,
                                                 (sm.Y - seedmap_offset) % seedpx_per_ru); // offset of current point within the RU

                        //Point sm=sourcemap.indexOf(src)-Point(offset, offset);
                        for (int iy = 0; iy < kernel.CellsX; ++iy)
                        {
                            for (int ix = 0; ix < kernel.CellsY; ++ix)
                            {

                                torus_pos = new Point(offset_ru.X + Maths.Modulo(offset_in_ru.X - offset + ix, seedpx_per_ru),
                                                      offset_ru.Y + Maths.Modulo(offset_in_ru.Y - offset + iy, seedpx_per_ru));
                                if (this.SeedMap.IsIndexValid(torus_pos))
                                {
                                    this.SeedMap[torus_pos] += cellFecundity * kernel[ix, iy];
                                }
                            }
                        }
                        // long distance dispersal
                        if ((serotiny == false) && (this.longDistanceDispersalSeedsByRing.Count > 0))
                        {
                            for (int r = 0; r < this.longDistanceDispersalSeedsByRing.Count; ++r)
                            {
                                float ldd_val = this.longDistanceDispersalSeedlingsPerCell / fec; // pixels will have this probability [note: fecundity will be multiplied below]
                                int n;
                                if (this.longDistanceDispersalSeedsByRing[r] < 1)
                                {
                                    n = random.GetRandomProbability() < this.longDistanceDispersalSeedsByRing[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)MathF.Round(this.longDistanceDispersalSeedsByRing[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    float radius = random.GetRandomFloat(this.longDispersalDistance[r], this.longDispersalDistance[r + 1]) / this.SeedMap.CellSizeInM; // choose a random distance (in pixels)
                                    float phi = random.GetRandomProbability() * 2.0F * Single.Pi; // choose a random direction
                                    Point ldd = new((int)(radius * MathF.Cos(phi)),
                                                     (int)(radius * MathF.Sin(phi))); // destination (offset)
                                    torus_pos = new Point(offset_ru.X + Maths.Modulo(offset_in_ru.X + ldd.X, seedpx_per_ru),
                                                          offset_ru.Y + Maths.Modulo(offset_in_ru.Y + ldd.Y, seedpx_per_ru));
                                    if (this.SeedMap.IsIndexValid(torus_pos))
                                    {
                                        this.SeedMap[torus_pos] += ldd_val;
                                    }
                                }
                            }
                        }
                    }
                }
            } // torus

            // now the seed sources (0..1) are spatially distributed by the kernel (and LDD) without altering the magnitude;
            // now we include the fecundity (=seedling potential per m2 crown area), and convert to the establishment probability p_seed.
            // The number of (potential) seedlings per m2 on each cell is: cell * fecundity[m2]
            // We assume that the availability of 100 potential seedlings/m2 is enough for unconstrained establishment;
            const float n_unlimited = 100.0F;
            for (int seedCellIndex = 0; seedCellIndex < sourcemap.CellCount; ++seedCellIndex)
            {
                float cellFecundity = sourcemap[seedCellIndex];
                if (cellFecundity > 0.0F)
                {
                    float cellProbability = cellFecundity * fec / n_unlimited;
                    if (cellProbability > 1.0F)
                    {
                        cellProbability = 1.0F;
                    }
                    sourcemap[seedCellIndex] = cellProbability;
                }
            }
        }

        // TODO: remove unused
        /** scans the seed image and detects "edges".
            edges are then subsequently marked (set to -1). This is pass 1 of the seed distribution process.
            */
        //private static bool DetectEdges(Grid<float> seedMap)
        //{
        //    // fill mini-gaps
        //    int n_gaps_filled = 0;
        //    for (int y = 1; y < seedMap.CellsY - 1; ++y)
        //    {
        //        int p = seedMap.IndexXYToIndex(1, y);
        //        int p_above = p - seedMap.CellsX; // one line above
        //        int p_below = p + seedMap.CellsX; // one line below
        //        for (int x = 1; x < seedMap.CellsX - 1; ++x, ++p, ++p_below, ++p_above)
        //        {
        //            if (seedMap[p] < 0.999F)
        //            {
        //                if ((seedMap[p_above - 1] == 1.0F ? 1 : 0) + (seedMap[p_above] == 1.0F ? 1 : 0) + (seedMap[p_above + 1] == 1.0F ? 1 : 0) +
        //                    (seedMap[p - 1] == 1.0F ? 1 : 0) + (seedMap[p + 1] == 1.0F ? 1 : 0) +
        //                    (seedMap[p_below - 1] == 1.0F ? 1 : 0) + (seedMap[p_below] == 1.0F ? 1 : 0) + (seedMap[p_below + 1] == 1.0F ? 1 : 0) > 3)
        //                {
        //                    seedMap[p] = 0.999F; // if more than 3 neighbors are active pixels, the value is high
        //                    ++n_gaps_filled;
        //                }
        //            }
        //        }
        //    }

        //    // now detect the edges
        //    bool edgeFound = false;
        //    int edgeCount = 0;
        //    for (int y = 1; y < seedMap.CellsY - 1; ++y)
        //    {
        //        int p = seedMap.IndexXYToIndex(1, y);
        //        int p_above = p - seedMap.CellsX; // one line above
        //        int p_below = p + seedMap.CellsX; // one line below
        //        for (int x = 1; x < seedMap.CellsX - 1; ++x, ++p, ++p_below, ++p_above)
        //        {
        //            if (seedMap[p] == 1.0F)
        //            {
        //                edgeFound = true;
        //                if ((seedMap[p_above - 1] < 0.999F && seedMap[p_above - 1] >= 0.0F) ||
        //                     (seedMap[p_above] < 0.999F && seedMap[p_above] >= 0.0F) ||
        //                     (seedMap[p_above + 1] < 0.999F && seedMap[p_above + 1] >= 0.0F) ||
        //                     (seedMap[p - 1] < 0.999F && seedMap[p - 1] >= 0.0F) ||
        //                     (seedMap[p + 1] < 0.999F && seedMap[p + 1] >= 0.0F) ||
        //                     (seedMap[p_below - 1] < 0.999F && seedMap[p_below - 1] >= 0.0F) ||
        //                     (seedMap[p_below] < 0.999F && seedMap[p_below] >= 0.0F) ||
        //                     (seedMap[p_below + 1] < 0.999F && seedMap[p_below + 1] >= 0.0F))
        //                {
        //                    seedMap[p] = -1.0F; // if any surrounding pixel is >=0 & <0.999: . mark as edge
        //                    ++edgeCount;
        //                }
        //            }

        //        }
        //    }
        //    // if (this.mDumpSeedMaps)
        //    // {
        //    //     Debug.WriteLine("species: " + Species.ID + " # of gaps filled: " + n_gaps_filled + " # of edge-pixels: " + edgeCount);
        //    // }
        //    // TODO: what if edgeFound == true but edgeCount == 0?
        //    return edgeFound;
        //}

        /** do the seed probability distribution.
            This is phase 2. Apply the seed kernel for each "edge" point identified in phase 1.
            */
        //private void DistributeSeedProbability(Model model, Grid<float> seedmap, Grid<float> kernel)
        //{
        //    int kernelCenter = kernel.CellsX / 2; // offset is the index of the center pixel
        //    for (int seedIndex = 0; seedIndex < seedmap.CellCount; ++seedIndex)
        //    {
        //        if (seedmap[seedIndex] == -1.0F)
        //        {
        //            // edge pixel found. Now apply the kernel....
        //            Point pt = seedmap.GetCellXYIndex(seedIndex);
        //            for (int y = -kernelCenter; y <= kernelCenter; ++y)
        //            {
        //                for (int x = -kernelCenter; x <= kernelCenter; ++x)
        //                {
        //                    float kernel_value = kernel[x + kernelCenter, y + kernelCenter];
        //                    if (kernel_value > 0.0F && seedmap.Contains(pt.X + x, pt.Y + y))
        //                    {
        //                        float val = seedmap[pt.X + x, pt.Y + y];
        //                        if (val != -1.0F)
        //                        {
        //                            seedmap[pt.X + x, pt.Y + y] = MathF.Min(1.0F - (1.0F - val) * (1.0F - kernel_value), 1.0F);
        //                        }
        //                    }
        //                }
        //            }
        //            // long distance dispersal
        //            if (this.longDistanceDispersalSeedsByRing.Count != 0)
        //            {
        //                float yearScaling = this.Species.IsMastYear ? 1.0F : this.nonMastYearFraction;
        //                RandomGenerator random = model.RandomGenerator.Value!;
        //                for (int distanceIndex = 0; distanceIndex < this.longDistanceDispersalSeedsByRing.Count; ++distanceIndex)
        //                {
        //                    float lddProbability = this.longDistanceDispersalSeedlingsPerCell; // pixels will have this probability
        //                    int nCells = (int)MathF.Round(this.longDistanceDispersalSeedsByRing[distanceIndex] * yearScaling); // number of pixels to activate
        //                    for (int cell = 0; cell < nCells; ++cell)
        //                    {
        //                        // distance and direction:
        //                        float radius = random.GetRandomFloat(longDispersalDistance[distanceIndex], longDispersalDistance[distanceIndex + 1]) / seedmap.CellSizeInM; // choose a random distance (in pixels)
        //                        float phi = random.GetRandomProbability() * 2.0F * MathF.PI; // choose a random direction
        //                        Point ldd = new((int)(pt.X + radius * MathF.Cos(phi)), (int)(pt.Y + radius * MathF.Sin(phi)));
        //                        if (seedmap.Contains(ldd))
        //                        {
        //                            float val = seedmap[ldd];
        //                            // use the same adding of probabilities
        //                            if (val != -1.0F)
        //                            {
        //                                seedmap[ldd] = MathF.Min(1.0F - (1.0F - val) * (1.0F - lddProbability), 1.0F);
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            seedmap[seedIndex] = 1.0F; // mark as processed
        //        } // if (seedmap[p]==1)
        //    } // for()
        //}

        //private void DistributeSeeds(Model model, Grid<float> sourceMap)
        //{
        //    Grid<float> kernel = this.kernelMastYear;

        //    // *** estimate seed production (based on leaf area) ***
        //    // calculate number of seeds; the source map holds now m2 leaf area on 20x20m pixels
        //    // after this step, each source cell has a value between 0 (no source) and 1 (fully covered cell)
        //    float fecundity = this.Species.FecundityM2;
        //    if (this.Species.IsMastYear == false)
        //    {
        //        fecundity *= this.nonMastYearFraction;
        //    }
        //    for (int sourceIndex = 0; sourceIndex < sourceMap.CellCount; ++sourceIndex)
        //    {
        //        if (sourceMap[sourceIndex] != 0.0F)
        //        {
        //            // if LAI  >3, then full potential is assumed, below LAI=3 a linear ramp is used
        //            sourceMap[sourceIndex] = MathF.Min(sourceMap[sourceIndex] / (sourceMap.CellSizeInM * sourceMap.CellSizeInM) / 3.0F, 3.0F);
        //        }
        //    }

        //    // sink mode
        //    //    // now look for each pixel in the targetmap and sum up seeds*kernel
        //    //    int idx=0;
        //    //    int offset = kernel.sizeX() / 2; // offset is the index of the center pixel
        //    //    //Grid<ResourceUnit*> &ru_map = GlobalSettings.instance().model().RUgrid();
        //    //    for (float *t=mSeedMap.begin(); t!=mSeedMap.end(); ++t, ++idx) {
        //    //        // skip out-of-project areas
        //    //        //if (!ru_map.constValueAtIndex(mSeedMap.index5(idx)))
        //    //        //    continue;
        //    //        // apply the kernel
        //    //        Point sm=mSeedMap.indexOf(t)-Point(offset, offset);
        //    //        for (int iy=0;iy<kernel.sizeY();++iy) {
        //    //            for (int ix=0;ix<kernel.sizeX();++ix) {
        //    //                if (sourcemap.isIndexValid(sm.x()+ix, sm.y()+iy))
        //    //                    *t+=sourcemap(sm.x()+ix, sm.y()+iy) * kernel(ix, iy);
        //    //            }
        //    //        }
        //    //    }
        //    //    mSeedMap.initialize(0.0F); // just for debugging...
        //    int kernelCenter = kernel.CellsX / 2; // offset is the index of the center pixel
        //    // source mode

        //    // *** seed distribution (kernel + long distance dispersal) ***
        //    if (model.Project.World.Geometry.IsTorus == false)
        //    {
        //        // ** standard case (no torus) **
        //        for (int sourceIndex = 0; sourceIndex < sourceMap.CellCount; ++sourceIndex)
        //        {
        //            if (sourceMap[sourceIndex] > 0.0F)
        //            {
        //                Point kernelOrigin = sourceMap.GetCellXYIndex(sourceIndex).Subtract(kernelCenter);
        //                int kernelX = kernelOrigin.X;
        //                int kernelY = kernelOrigin.Y;
        //                for (int indexY = 0; indexY < kernel.CellsY; ++indexY)
        //                {
        //                    for (int indexX = 0; indexX < kernel.CellsX; ++indexX)
        //                    {
        //                        if (this.SeedMap.Contains(kernelX + indexX, kernelY + indexY))
        //                        {
        //                            this.SeedMap[kernelX + indexX, kernelY + indexY] += sourceMap[sourceIndex] * kernel[indexX, indexY];
        //                        }
        //                    }
        //                }
        //                // long distance dispersal
        //                if (this.longDistanceDispersalSeedsByRing.Count > 0)
        //                {
        //                    RandomGenerator random = model.RandomGenerator.Value!;
        //                    Point sourceCellIndex = sourceMap.GetCellXYIndex(sourceIndex);
        //                    for (int ringIndex = 0; ringIndex < this.longDistanceDispersalSeedsByRing.Count; ++ringIndex)
        //                    {
        //                        float ldd_val = this.longDistanceDispersalSeedlingsPerCell / fecundity; // pixels will have this probability [note: fecundity will be multiplied below]
        //                        int nSeeds;
        //                        if (this.longDistanceDispersalSeedsByRing[ringIndex] < 1)
        //                        {
        //                            nSeeds = random.GetRandomProbability() < this.longDistanceDispersalSeedsByRing[ringIndex] ? 1 : 0;
        //                        }
        //                        else
        //                        {
        //                            nSeeds = (int)MathF.Round(this.longDistanceDispersalSeedsByRing[ringIndex]); // number of pixels to activate
        //                        }
        //                        for (int seedIndex = 0; seedIndex < nSeeds; ++seedIndex)
        //                        {
        //                            // distance and direction:
        //                            float radiusInCells = random.GetRandomFloat(this.longDispersalDistance[ringIndex], this.longDispersalDistance[ringIndex + 1]) / this.SeedMap.CellSizeInM; // choose a random distance (in pixels)
        //                            float phi = 2.0F * MathF.PI * random.GetRandomProbability(); // choose a random direction
        //                            Point seedCellPosition = new(sourceCellIndex.X + (int)(radiusInCells * MathF.Cos(phi)), sourceCellIndex.Y + (int)(radiusInCells * MathF.Sin(phi)));
        //                            if (this.SeedMap.Contains(seedCellPosition))
        //                            {
        //                                this.SeedMap[seedCellPosition] += ldd_val;
        //                            }
        //                        }
        //                    }
        //                }

        //            }
        //        }
        //    }
        //    else
        //    {
        //        // **** seed distribution in torus mode ***
        //        RandomGenerator random = model.RandomGenerator.Value!;
        //        int seedmapOffset = sourceMap.GetCellXYIndex(new PointF(0.0F, 0.0F)).X; // the seed maps have x extra rows/columns
        //        int seedCellsPerRU = (int)(Constant.Grid.ResourceUnitSizeInM / sourceMap.CellSizeInM);
        //        for (int sourceIndex = 0; sourceIndex < sourceMap.CellCount; ++sourceIndex)
        //        {
        //            if (sourceMap[sourceIndex] > 0.0F)
        //            {
        //                Point sourceCellPosition = sourceMap.GetCellXYIndex(sourceIndex);
        //                // get the origin of the resource unit *on* the seedmap in *seedmap-coords*:
        //                Point ruOffset = new((sourceCellPosition.X - seedmapOffset) / seedCellsPerRU * seedCellsPerRU + seedmapOffset,
        //                                     (sourceCellPosition.Y - seedmapOffset) / seedCellsPerRU * seedCellsPerRU + seedmapOffset);  // coords RU origin
        //                Point offsetInRU = new((sourceCellPosition.X - seedmapOffset) % seedCellsPerRU, (sourceCellPosition.Y - seedmapOffset) % seedCellsPerRU);  // offset of current point within the RU

        //                //Point sm=sourcemap.indexOf(src)-Point(offset, offset);
        //                for (int indexY = 0; indexY < kernel.CellsY; ++indexY)
        //                {
        //                    for (int indexX = 0; indexX < kernel.CellsX; ++indexX)
        //                    {
        //                        Point torusIndex = ruOffset.Add(new Point(Maths.Modulo((offsetInRU.X - kernelCenter + indexX), seedCellsPerRU), Maths.Modulo((offsetInRU.Y - kernelCenter + indexY), seedCellsPerRU)));
        //                        if (this.SeedMap.Contains(torusIndex))
        //                        {
        //                            this.SeedMap[torusIndex] += sourceMap[sourceIndex] * kernel[indexX, indexY];
        //                        }
        //                    }
        //                }
        //                // long distance dispersal
        //                if (longDistanceDispersalSeedsByRing.Count != 0)
        //                {
        //                    for (int densityIndex = 0; densityIndex < longDistanceDispersalSeedsByRing.Count; ++densityIndex)
        //                    {
        //                        float ldd_val = this.longDistanceDispersalSeedlingsPerCell / fecundity; // pixels will have this probability [note: fecundity will be multiplied below]
        //                        int nSeeds;
        //                        if (longDistanceDispersalSeedsByRing[densityIndex] < 1)
        //                        {
        //                            nSeeds = random.GetRandomProbability() < longDistanceDispersalSeedsByRing[densityIndex] ? 1 : 0;
        //                        }
        //                        else
        //                        {
        //                            nSeeds = (int)MathF.Round(longDistanceDispersalSeedsByRing[densityIndex]); // number of pixels to activate
        //                        }
        //                        for (int seed = 0; seed < nSeeds; ++seed)
        //                        {
        //                            // distance and direction:
        //                            float radius = random.GetRandomFloat(longDispersalDistance[densityIndex], longDispersalDistance[densityIndex + 1]) / SeedMap.CellSizeInM; // choose a random distance (in pixels)
        //                            float phi = random.GetRandomProbability() * 2.0F * MathF.PI; // choose a random direction
        //                            Point ldd = new((int)(radius * MathF.Cos(phi)), (int)(radius * MathF.Sin(phi))); // destination (offset)
        //                            Point torusIndex = ruOffset.Add(new Point(Maths.Modulo((offsetInRU.X + ldd.X), seedCellsPerRU), Maths.Modulo((offsetInRU.Y + ldd.Y), seedCellsPerRU)));

        //                            if (this.SeedMap.Contains(torusIndex))
        //                            {
        //                                this.SeedMap[torusIndex] += ldd_val;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    } // torus

        //    // now the seed sources (0..1) are spatially distributed by the kernel (and LDD) without altering the magnitude;
        //    // now we include the fecundity (=seedling potential per m2 crown area), and convert to the establishment probability p_seed.
        //    // The number of (potential) seedlings per m2 on each cell is: cell * fecundity[m2]
        //    // We assume that the availability of 10 potential seedlings/m2 is enough for unconstrained establishment;
        //    const float nUnlimited = 100.0F;
        //    for (int seedIndex = 0; seedIndex < this.SeedMap.CellCount; ++seedIndex)
        //    {
        //        if (this.SeedMap[seedIndex] > 0.0F)
        //        {
        //            this.SeedMap[seedIndex] = MathF.Min(this.SeedMap[seedIndex] * fecundity / nUnlimited, 1.0F);
        //        }
        //    }
        //}

        [GeneratedRegex("([^\\.\\w]+)")]
        private static partial Regex GetTokensRegex();

        /// extra seed rain of serotinous species
        public void SeedProductionSerotiny(RandomGenerator random, TreeListSpatial treeList, int treeIndex)
        {
            if (this.seedMapSerotiny.IsSetup() == false)
            {
                throw new NotSupportedException("Tried to set a seed source for a non-serotinous species.");
            }

            // if the tree is not considered as serotinous (i.e. seeds need external trigger such as fire), then do nothing
            int treeAge = treeList.AgeInYears[treeIndex];
            if ((treeAge < treeList.Species.MinimumAgeInYearsForSeedProduction) || (treeList.Species.IsTreeSerotinousRandom(random, treeAge) == false))
            {
                return;
            }

            // todo:  (see setMatureTree): new way uses a "sourceMap" and writes not directly on seed map??
            Point positionIndex = treeList.LightCellIndexXY[treeIndex];
            this.seedMapSerotiny[positionIndex.X, positionIndex.Y, Constant.Grid.LightCellsPerSeedmapCellWidth] += treeList.LeafAreaInM2[treeIndex];
            this.hasPendingSerotiny = true;
        }

        // setMatureTree is called by individual (mature) trees. This actually fills the initial state of the seed map.
        public void SetMatureTree(Point lightCellPosition, float leafArea)
        {
            this.sourceMap[lightCellPosition.X, lightCellPosition.Y, Constant.Grid.LightCellsPerSeedmapCellWidth] += leafArea;
        }

        /// flags pixel at 'lip_index' that seeds should be produced. Called by saplings in the regeneration layer.
        public void SetSaplingTree(Point lip_index, float leaf_area) // C++ SeedDispersal::setSaplingTree();
        {
            // TODO: why does this use this.saplingMapCreated instead of this.saplingSeedMap.IsSetup()?
            if (this.saplingMapCreated == false)
            {
                // setup the data on first use
                lock (this.saplingSeedMap)
                {
                    if (this.saplingMapCreated == false)
                    {
                        // if another thread already created the map, skip
                        this.saplingSeedMap.Setup(this.SeedMap);
                        this.saplingSeedMap.Fill(0.0F);
                        this.saplingMapCreated = true;
                    }
                }
            }

            this.saplingSeedMap[lip_index.X, lip_index.Y, Constant.Grid.LightCellsPerSeedmapCellWidth] += leaf_area;
        }

        /** setup of the seedmaps.
          This sets the size of the seed map and creates the seed kernel (species specific)
          */
        public void Setup(Model model)
        {
            if ((model.Project.World.Geometry.BufferWidthInM % Constant.Grid.SeedmapCellSizeInM) != 0)
            {
                throw new NotSupportedException("The world buffer width (/project/model/world/geometry/bufferWidth) must be a integer multiple of the seed cell size (currently 20m, e.g. 20,40,60,...)).");
            }

            // setup of seed map
            this.SeedMap.Setup(model.Landscape.VegetationHeightGrid.ProjectExtent, Constant.Grid.SeedmapCellSizeInM);
            this.SeedMap.Fill(0.0F);

            this.sourceMap.Setup(SeedMap);
            this.sourceMap.Fill(0.0F);

            // settings
            this.treeMigOccupancy = 1.0F; // is currently constant
            // copy values for the species parameters
            this.Species.GetTreeMigKernel(out this.treeMigAlphas1, out this.treeMigAlphaS2, out this.treeMigKappaS);
            // this.treeMigFecundityPerCell = this.Species.FecundityM2 * Constant.Grid.SeedmapCellSizeInM * Constant.Grid.SeedmapCellSizeInM * treeMigOccupancy; // scale to production for the whole cell
            this.nonMastYearFraction = this.Species.NonMastYearFraction;
            this.longDistanceDispersalMaximumSeedlingDensity = model.Project.Model.SeedDispersal.LongDistanceDispersal.MaximumSeedlingDensity;
            this.longDistanceDispersalMinimumSeedlingDensity = model.Project.Model.SeedDispersal.LongDistanceDispersal.MinimumSeedlingDensity;
            this.longDistanceDispersalRingCount = model.Project.Model.SeedDispersal.LongDistanceDispersal.Rings;
            this.longDistanceDispersalSeedlingsPerCell = model.Project.Model.SeedDispersal.LongDistanceDispersal.SeedlingsPerCell;
            this.longDistanceDispersalSeedlingsPerCell = MathF.Max(this.longDistanceDispersalSeedlingsPerCell, this.longDistanceDispersalMaximumSeedlingDensity);

            // long distance dispersal
            float sumOfLongDistanceDispersalRingFractions = this.SetupLongDistanceDispersal();
            this.CreateKernel(kernelMastYear, 1.0F - sumOfLongDistanceDispersalRingFractions);

            // the kernel for non seed years looks similar, but is simply linearly scaled down
            // using the species parameter NonMastYearFraction.
            // the central pixel still gets the value of 1 (i.e. 100% probability)
            //this.CreateKernel(kernelNonMastYear, 1.0F - sumOfLongDistanceDispersalRingFractions);

            if (this.Species.FecunditySerotiny > 0.0F)
            {
                // an extra seed map is used for storing information related to post-fire seed rain
                this.seedMapSerotiny.Setup(model.Landscape.VegetationHeightGrid.ProjectExtent, Constant.Grid.SeedmapCellSizeInM);
                this.seedMapSerotiny.Fill(0.0F);

                // set up the special seed kernel for post fire seed rain
                this.CreateKernel(this.kernelSerotiny, 1.0F);
                // Debug.WriteLine("created extra seed map and serotiny seed kernel for species " + Species.Name + " with fecundity factor " + Species.FecunditySerotiny);
            }
            this.hasPendingSerotiny = false;

            // debug info
            this.writeSeedMapsToImages = String.IsNullOrWhiteSpace(model.Project.Model.SeedDispersal.DumpSeedMapsPath) == false;
            if (this.writeSeedMapsToImages)
            {
                string seedmapPath = model.Project.GetFilePath(ProjectDirectory.Home, model.Project.Model.SeedDispersal.DumpSeedMapsPath);
                string fourLetterSpeciesCode = this.Species.WorldFloraID.ToSpeciesAbbreviation();
                File.WriteAllText(Path.Combine(seedmapPath, fourLetterSpeciesCode + "KernelMastYear.csv"), this.kernelMastYear.ToString());
                //File.WriteAllText(Path.Combine(seedmapPath, fourLetterSpeciesCode + "KernelNonmastYear.csv"), this.kernelNonMastYear.ToString());
                if (this.kernelSerotiny.IsSetup())
                {
                    File.WriteAllText(Path.Combine(seedmapPath, fourLetterSpeciesCode + "KernelSerotiny.csv"), this.kernelSerotiny.ToString());
                }
            }

            // external seeds
            this.hasExternalSeedInput = false;
            this.externalSeedBufferWidth = 0;
            this.externalSeedDirection = 0;
            this.externalSeedBackgroundInput = 0.0F;

            Input.ProjectFile.SeedDispersal seedDispersal = model.Project.Model.SeedDispersal;
            if (seedDispersal.ExternalSeedEnabled)
            {
                if (seedDispersal.ExternalSeedBelt.Enabled)
                {
                    // external seed input specified by sectors and around the project area (seedbelt)
                    this.SetupExternalSeedsForSpecies(model, this.Species);
                }
                else
                {
                    if (String.IsNullOrWhiteSpace(seedDispersal.ExternalSeedBackgroundInput) ||
                        String.IsNullOrWhiteSpace(seedDispersal.ExternalSeedBuffer) ||
                        String.IsNullOrWhiteSpace(seedDispersal.ExternalSeedDirection) ||
                        (seedDispersal.ExternalSeedSpecies.Count == 0))
                    {
                        throw new NotSupportedException("An external seed species, source, buffer, and background input must be specified when external seed is enabled but the seed belt is disabled.");
                    }

                    // external seeds specified fixedly per cardinal direction
                    // current species in list??
                    this.hasExternalSeedInput = seedDispersal.ExternalSeedSpecies.Contains(this.Species.WorldFloraID);
                    string direction = seedDispersal.ExternalSeedDirection.ToLowerInvariant();
                    // encode cardinal positions as bits: e.g: "e,w" = 6
                    this.externalSeedDirection = 0x0;
                    if (direction.Contains('n', StringComparison.Ordinal))
                    {
                        this.externalSeedDirection |= 0x1;
                    }
                    if (direction.Contains('e', StringComparison.Ordinal))
                    {
                        this.externalSeedDirection |= 0x2;
                    }
                    if (direction.Contains('s', StringComparison.Ordinal))
                    {
                        this.externalSeedDirection |= 0x4;
                    }
                    if (direction.Contains('w', StringComparison.Ordinal))
                    {
                        this.externalSeedDirection |= 0x8;
                    }

                    List<string> seedBufferTokens = SeedDispersal.GetTokensRegex().Matches(seedDispersal.ExternalSeedBuffer).Select(match => match.Value).ToList();
                    string speciesAbbreviation = this.Species.WorldFloraID.ToSpeciesAbbreviation();
                    int index = seedBufferTokens.IndexOf(speciesAbbreviation);
                    if (index >= 0)
                    {
                        this.externalSeedBufferWidth = Int32.Parse(seedBufferTokens[index + 1], CultureInfo.InvariantCulture);
                        // Debug.WriteLine("enabled special buffer for species " + Species.ID + ": distance of " + mExternalSeedBuffer + " pixels = " + mExternalSeedBuffer * 20.0 + " m");
                    }

                    // background seed rain (i.e. for the full landscape), use regexp
                    List<string> backgroundInputList = SeedDispersal.GetTokensRegex().Matches(seedDispersal.ExternalSeedBackgroundInput).Select(match => match.Value).ToList();
                    index = backgroundInputList.IndexOf(speciesAbbreviation);
                    if (index >= 0)
                    {
                        this.externalSeedBackgroundInput = Single.Parse(backgroundInputList[index + 1], CultureInfo.InvariantCulture);
                        // Debug.WriteLine("enabled background seed input (for full area) for species " + Species.ID + ": p=" + mExternalSeedBackgroundInput);
                    }

                    // if (this.mHasExternalSeedInput)
                    // {
                    //     Debug.WriteLine("External seed input enabled for " + Species.ID);
                    // }
                }
            }

            if (model.Project.Model.SeedDispersal.ExternalSeedBelt.Enabled)
            {
                this.SetupExternalSeeds(model);
            }
        }

        private void SetupExternalSeeds(Model model)
        {
            // make a copy of the 10m height grid in lower resolution and mark pixels which are on resource units
            this.externalSeedBaseMap = new();
            this.externalSeedBaseMap.Setup(model.Landscape.VegetationHeightGrid.ProjectExtent, Constant.Grid.SeedmapCellSizeInM);
            this.externalSeedBaseMap.Fill(-1.0F); // default all cells to of project area
            if (4 * this.externalSeedBaseMap.CellCount != model.Landscape.VegetationHeightGrid.CellCount)
            {
                // not quite the same as checking 2 * Constant.HeightCellSizeInM != Constant.SeedmapCellSizeInM
                throw new NotSupportedException("Width and height of the project area need to be a multiple of " + Constant.Grid.SeedmapCellSizeInM + " m when external seeds are enabled.");
            }
            for (int resourceUnitIndex = 0; resourceUnitIndex < model.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnit resourceUnit = model.Landscape.ResourceUnits[resourceUnitIndex];
                Point resourceUnitSeedmapIndexXY = this.externalSeedBaseMap.GetCellXYIndex(resourceUnit.ProjectExtent.X, resourceUnit.ProjectExtent.Y);
                this.externalSeedBaseMap.Fill(resourceUnitSeedmapIndexXY.X, resourceUnitSeedmapIndexXY.Y, Constant.Grid.SeedmapCellsPerRUWidth, Constant.Grid.SeedmapCellsPerRUWidth, 1.0F);
            }

            // now scan the pixels of the belt: paint all pixels that are close to the project area
            // we do this 4 times (for all cardinal direcitons)
            int externalSeedbeltWidth = model.Project.Model.SeedDispersal.ExternalSeedBelt.WidthInM;
            for (int indexY = 0; indexY < this.externalSeedBaseMap.CellsY; ++indexY)
            {
                for (int indexX = 0; indexX < this.externalSeedBaseMap.CellsX; ++indexX)
                {
                    if (this.externalSeedBaseMap[indexX, indexY] != 1.0F)
                    {
                        continue;
                    }

                    int lookForward = Math.Min(indexX + externalSeedbeltWidth, this.externalSeedBaseMap.CellsX - 1);
                    if (this.externalSeedBaseMap[lookForward, indexY] == -1.0F)
                    {
                        // fill pixels
                        for (; indexX < lookForward; ++indexX)
                        {
                            float v = this.externalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                this.externalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // right to left
            for (int indexY = 0; indexY < this.externalSeedBaseMap.CellsY; ++indexY)
            {
                for (int indexX = this.externalSeedBaseMap.CellsX - 1; indexX >= 0; --indexX)
                {
                    if (this.externalSeedBaseMap[indexX, indexY] != 1.0F)
                    {
                        continue;
                    }
                    int lookForward = Math.Max(indexX - externalSeedbeltWidth, 0);
                    if (this.externalSeedBaseMap[lookForward, indexY] == -1.0F)
                    {
                        // fill pixels
                        for (; indexX > lookForward; --indexX)
                        {
                            float v = this.externalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                this.externalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // up and down ***
            // from top to bottom
            for (int indexX = 0; indexX < this.externalSeedBaseMap.CellsX; ++indexX)
            {
                for (int indexY = 0; indexY < this.externalSeedBaseMap.CellsY; ++indexY)
                {
                    if (this.externalSeedBaseMap[indexX, indexY] != 1.0F)
                    {
                        continue;
                    }
                    int lookForward = Math.Min(indexY + externalSeedbeltWidth, this.externalSeedBaseMap.CellsY - 1);
                    if (this.externalSeedBaseMap[indexX, lookForward] == -1.0F)
                    {
                        // fill pixels
                        for (; indexY < lookForward; ++indexY)
                        {
                            float v = externalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                externalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // bottom to top ***
            for (int indexY = 0; indexY < externalSeedBaseMap.CellsY; ++indexY)
            {
                for (int indexX = externalSeedBaseMap.CellsX - 1; indexX >= 0; --indexX)
                {
                    if (externalSeedBaseMap[indexX, indexY] != 1.0F)
                    {
                        continue;
                    }
                    int lookForward = Math.Max(indexY - externalSeedbeltWidth, 0);
                    if (externalSeedBaseMap[indexX, lookForward] == -1.0)
                    {
                        // fill pixels
                        for (; indexY > lookForward; --indexY)
                        {
                            float v = externalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                externalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }

            // setup of sectors
            this.externalSeedData.Clear();
            int sectorsX = model.Project.Model.SeedDispersal.ExternalSeedBelt.SectorsX;
            int sectorsY = model.Project.Model.SeedDispersal.ExternalSeedBelt.SectorsY;
            if (sectorsX < 1 || sectorsY < 1)
            {
                throw new NotSupportedException(String.Format("Invalid number of sectors x={0} y={1}.", sectorsX, sectorsY));
            }

            foreach (ExternalSeedBeltSector species in model.Project.Model.SeedDispersal.ExternalSeedBelt.Sectors)
            {
                int x = species.X;
                int y = species.Y;
                if ((x < 0) || (x >= sectorsX) || (y < 0) || (y >= sectorsY))
                {
                    throw new NotSupportedException("Invalid sector for specifying external seed input: x = " + x + ", y = " + y + ".");
                }
                //int index = y * sectors_x + x;
                if (String.IsNullOrWhiteSpace(species.SpeciesIDs))
                {
                    throw new NotSupportedException("List of seed belt species is empty.");
                }

                // Debug.WriteLine("processing species list at x = " + x + ", y = " + y + ", " + species.IDs);
                // we assume pairs of name and fraction
                List<string> speciesIDs = new(species.SpeciesIDs.Split(" "));
                for (int speciesIndex = 0; speciesIndex < speciesIDs.Count; ++speciesIndex)
                {
                    WorldFloraID speciesTsn = WorldFloraIDExtensions.Parse(speciesIDs[speciesIndex]);
                    List<float> space = this.externalSeedData[speciesTsn];
                    if (space.Count == 0)
                    {
                        space.Capacity = sectorsX * sectorsY; // are initialized to 0s
                    }
                    float fraction = Single.Parse(speciesIDs[++speciesIndex], CultureInfo.InvariantCulture);
                    space.Add(fraction);
                }
            }
            this.externalSeedSectorX = sectorsX;
            this.externalSeedSectorY = sectorsY;
            // Debug.WriteLine("setting up of external seed maps finished");
        }

        private void SetupExternalSeedsForSpecies(Model model, TreeSpecies species) // C++: SeedDispersal::setupExternalSeedsForSpecies()
        {
            if (this.externalSeedData.TryGetValue(species.WorldFloraID, out List<float>? pcts) == false)
            {
                return; // nothing to do
            }

            // Debug.WriteLine("setting up external seed map for " + species.ID);
            int cellsX = this.externalSeedMap.CellsX / this.externalSeedSectorX; // number of cells per sector
            int cellsY = this.externalSeedMap.CellsY / this.externalSeedSectorY; // number of cells per sector
            this.externalSeedMap.Setup(this.SeedMap);
            this.externalSeedMap.Fill(0.0F);
            RandomGenerator random = model.RandomGenerator.Value!;
            for (int sectorX = 0; sectorX < this.externalSeedSectorX; ++sectorX)
            {
                for (int sectorY = 0; sectorY < this.externalSeedSectorY; ++sectorY)
                {
                    int xMin = sectorX * cellsX;
                    int xMax = (sectorX + 1) * cellsX;
                    int yMin = sectorY * cellsY;
                    int yMax = (sectorY + 1) * cellsY;
                    // now loop over the whole sector
                    int index = sectorY * this.externalSeedSectorX + sectorX;
                    float p = pcts[index];
                    for (int indexY = yMin; indexY < yMax; ++indexY)
                    {
                        for (int indexX = xMin; indexX < xMax; ++indexX)
                        {
                            // check
                            if (this.externalSeedBaseMap[indexX, indexY] == 2.0F)
                            {
                                if (random.GetRandomProbability() < p)
                                {
                                    this.externalSeedMap[indexX, indexY] = 1.0F; // flag
                                }
                            }
                        }
                    }
                }
            }

            // scale external seed values to have pixels with LAI=3
            for (int p = 0; p < this.externalSeedMap.CellCount; ++p)
            {
                this.externalSeedMap[p] *= 3.0F * this.externalSeedMap.CellSizeInM * this.externalSeedMap.CellSizeInM;
            }
        }

        private float SetupLongDistanceDispersal()
        {
            this.longDistanceDispersalSeedsByRing.Clear();
            this.longDispersalDistance.Clear();
            if (this.longDistanceDispersalMinimumSeedlingDensity >= this.longDistanceDispersalMaximumSeedlingDensity)
            {
                throw new NotSupportedException("Minimum seed intensity is greater than or equal to maximum seed intensity?");
            }

            float minimumRadius = this.TreeMigDistanceforProbability(this.longDistanceDispersalMaximumSeedlingDensity / this.Species.FecundityM2);
            float maximumRadius = this.TreeMigDistanceforProbability(this.longDistanceDispersalMinimumSeedlingDensity / this.Species.FecundityM2);
            this.longDispersalDistance.Add(minimumRadius);
            float sumOfLongDistanceDispersalRingFractions = 0.0F;
            for (int ring = 0; ring < this.longDistanceDispersalRingCount; ++ring)
            {
                float innerRadius = this.longDispersalDistance[^1];
                this.longDispersalDistance.Add(this.longDispersalDistance[^1] + (maximumRadius - minimumRadius) / this.longDistanceDispersalRingCount);
                float outerRadius = this.longDispersalDistance[^1];
                // calculate the value of the kernel for the middle of the ring
                float kernelInner = this.TreeMig(innerRadius); // kernel value at the inner border of the ring
                float kernelOuter = this.TreeMig(outerRadius); // kernel value at the outer border of the ring
                float meanEstimate = kernelInner * 0.4F + kernelOuter * 0.6F; // this is the average p -- 0.4/0.6 better estimate the nonlinear behavior (fits very well for medium to large kernels, e.g. piab)
                // calculate the area of the ring
                float ringArea = MathF.PI * (outerRadius * outerRadius - innerRadius * innerRadius); // m²
                // the number of px considers the fecundity
                float n_px = meanEstimate * ringArea * this.Species.FecundityM2 / this.longDistanceDispersalSeedlingsPerCell; // mean * m² * germinating seeds/m²  / seedlings = dimensionless?
                sumOfLongDistanceDispersalRingFractions += meanEstimate * ringArea; // this fraction of the full kernel (=1) is distributed in this ring

                this.longDistanceDispersalSeedsByRing.Add(n_px);
            }
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("Setup LDD for " + Species.Name + ", using probability: " + this.longDistanceDispersalSeedlingsPerCell + ": Distances: " + this.longDispersalDistance + ", seed pixels: " + this.longDistanceDispersalSeedsByRing + "covered prob: " + ldd_sum);
            //}

            return sumOfLongDistanceDispersalRingFractions;
        }

        /* R-Code:
        treemig=function(as1,as2,ks,d) # two-part exponential function, cf. Lischke & Loeffler (2006), Annex
                {
                p1=(1-ks)*MathF.Exp(-d/as1)/as1
                if(as2>0){p2=ks*MathF.Exp(-d/as2)/as2}else{p2=0}
                p1+p2
                }
        */

        /// the used kernel function
        /// see also Appendix B of iland paper II (note the different variable names)
        /// mTM_as1: shape parameter for wind / ballistic dispersal
        /// mTM_as2: shape parameter for zoochorous dispersal
        /// mTM_ks: proportion zoochorous transport
        /// fun fact: integral 0..asX = 1-1/e = ~0.63. 63% of dispersal distances are < asX
        /// the function returns the seed density at a point with distance 'distance'.
        private float TreeMig(float distance)
        {
            float p1 = (1.0F - treeMigKappaS) * MathF.Exp(-distance / treeMigAlphas1) / treeMigAlphas1;
            float p2 = 0.0F;
            if (treeMigAlphaS2 > 0.0F)
            {
                p2 = treeMigKappaS * MathF.Exp(-distance / treeMigAlphaS2) / treeMigAlphaS2;
            }
            float s = p1 + p2;
            // 's' is the density for radius 'distance' - not for specific point with that distance.
            // (i.e. the integral over the one-dimensional TreeMig function is 1, but if applied for 2d cells, the
            // sum would be much larger as all seeds arriving at 'distance' would be arriving somewhere at the circle with radius 'distance')
            // convert that to a density at a point, by dividing with the circumference at the circle with radius 'distance'
            s /= 2.0F * MathF.Max(distance, 0.01F) * MathF.PI;

            return s;
        }

        private float TreeMigCenterCell(float max_distance)
        {
            // use 100 steps and calculate dispersal kernel for consecutive rings
            float sum = 0.0F;
            for (int step = 0; step < 100; step++)
            {
                float r_in = step * max_distance / 100.0F;
                float r_out = (step + 1) * max_distance / 100.0F;
                float ring_area = (r_out * r_out - r_in * r_in) * MathF.PI;
                // the value of each ring is: treemig(r) * area of the ring
                sum += this.TreeMig((r_out + r_in) / 2.0F) * ring_area;
            }
            return sum;
        }

        /// calculate the distance where the probability falls below 'value'
        private float TreeMigDistanceforProbability(float probability)
        {
            float dist = 0.0F;
            while ((this.TreeMig(dist) > probability) && (dist < 10000.0F))
            {
                dist += 10.0F;
            }
            return dist;
        }
    }
}
