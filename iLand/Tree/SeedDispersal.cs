using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** @class SeedDispersal
        The class encapsulates the dispersal of seeds of one species over the whole landscape.
        The dispersal algortihm operate on grids with a 20m resolution.

        See http://iland-model.org/dispersal
        */
    public class SeedDispersal
    {
        private Grid<float> mExternalSeedBaseMap; // intermediate data while setting up external seeds
        // TODO: can this be made species specific?
        private readonly Dictionary<string, List<float>> mExtSeedData; // holds definition of species and percentages for external seed input
        private int mExternalSeedSectorX = 0; // size of the sectors used to specify external seed input
        private int mExternalSeedSectorY = 0;
        private bool mProbMode; // if 'true', seed dispersal uses probabilities to distribute (old version)
        private float mTreeMigAlphas1, mTreeMigAlphas2, mTreeMigKappas; // seed dispersal parameters (treemig)
        private float mTreeMigFecundityPerCell; // maximum seeds per source cell
        private float mTreeMigOccupancy; // seeds required per destination regeneration pixel
        private float mNonSeedYearFraction; // fraction of the seed production in non-seed-years
        private float mLddMaximumSeedlingDensity, mLddMinimumSeedlingDensity; // value of the kernel function that is the threhold for full coverage and LDD, respectively
        private int mIndexFactor; // multiplier between light-pixel-size and seed-pixel-size
        private readonly Grid<float> mSourceMap; // (large) seedmap used to denote the sources
        private readonly Grid<float> mKernelSeedYear; // species specific "seed kernel" (small) for seed years
        private readonly Grid<float> mKernelNonSeedYear; // species specific "seed kernel" (small) for non-seed-years
        private readonly Grid<float> mKernelSerotiny; // seed kernel for extra seed rain
        private readonly Grid<float> mSeedMapSerotiny; // seed map that keeps track of serotiny events
        private readonly List<float> mLddDistance; // long distance dispersal distances (e.g. the "rings")
        private readonly List<float> mLddSeedsByRing;  // long distance dispersal # of cells that should be affected in each "ring"
        private int mLddRings; // # of rings (with equal probability) for LDD
        private float mLddSeedlingsPerCell; // each LDD pixel has this probability
        private bool mHasPendingSerotiny; // true if active (unprocessed) pixels are on the extra-serotiny map
        private bool mDumpSeedMaps; // if true, seedmaps are stored as images
        private bool mHasExternalSeedInput; // if true, external seeds are modelled for the species
        private int mExternalSeedDirection; // direction of external seeds
        private int mExternalSeedBuffer; // how many 20m pixels away from the simulation area should the seeding start?
        private float mExternalSeedBackgroundInput; // background propability for this species; if set, then a certain seed availability is provided for the full area
        // external seeds
        private readonly Grid<float> mExternalSeedMap; // for more complex external seed input, this map holds that information

        public Grid<float> SeedMap { get; private init; } // (large) seedmap. Is filled by individual trees and then processed
        public TreeSpecies Species { get; private init; }

        public SeedDispersal(TreeSpecies species)
        {
            this.mExternalSeedBaseMap = new Grid<float>();
            this.mExtSeedData = new Dictionary<string, List<float>>();
            this.mExternalSeedMap = new Grid<float>();
            this.mIndexFactor = 10;
            this.mKernelSeedYear = new Grid<float>(); // species specific "seed kernel" (small) for seed years
            this.mKernelNonSeedYear = new Grid<float>(); // species specific "seed kernel" (small) for non-seed-years
            this.mKernelSerotiny = new Grid<float>(); // seed kernel for extra seed rain
            this.mLddDistance = new List<float>(); // long distance dispersal distances (e.g. the "rings")
            this.mLddSeedsByRing = new List<float>();  // long distance dispersal # of cells that should be affected in each "ring"
            this.mSeedMapSerotiny = new Grid<float>(); // seed map that keeps track of serotiny events
            this.mSourceMap = new Grid<float>(); // (large) seedmap used to denote the sources

            this.SeedMap = new Grid<float>(); // (large) seedmap. Is filled by individual trees and then processed
            this.Species = species;
        }

        /// setMatureTree is called by individual (mature) trees. This actually fills the initial state of the seed map.
        public void SetMatureTree(Point lightCellPosition, float leafArea)
        {
            if (mProbMode)
            {
                this.SeedMap[lightCellPosition.X, lightCellPosition.Y, mIndexFactor] = 1.0F;
            }
            else
            {
                this.mSourceMap[lightCellPosition.X, lightCellPosition.Y, mIndexFactor] += leafArea;
            }
        }

        /** setup of the seedmaps.
          This sets the size of the seed map and creates the seed kernel (species specific)
          */
        public void Setup(Model model)
        {
            this.mProbMode = false;

            // setup of seed map
            this.SeedMap.Clear();
            this.SeedMap.Setup(model.Landscape.HeightGrid.PhysicalExtent, Constant.SeedmapSize);
            this.SeedMap.Fill(0.0F);
            if (mProbMode == false)
            {
                mSourceMap.Setup(SeedMap);
                mSourceMap.Fill(0.0F);
            }
            mExternalSeedMap.Clear();
            mIndexFactor = Constant.SeedmapSize / Constant.LightSize; // ratio seed grid / lip-grid:

            if ((model.Project.World.Geometry.Buffer % Constant.SeedmapSize) != 0.0)
            {
                throw new NotSupportedException("SeedDispersal:setup(): The buffer (model.world.buffer) must be a integer multiple of the seed pixel size (currently 20m, e.g. 20,40,60,...)).");
            }

            // settings
            this.mTreeMigOccupancy = 1.0F; // is currently constant
            // copy values for the species parameters
            this.Species.GetTreeMigKernel(out this.mTreeMigAlphas1, out this.mTreeMigAlphas2, out this.mTreeMigKappas);
            this.mTreeMigFecundityPerCell = this.Species.FecundityM2 * Constant.SeedmapSize * Constant.SeedmapSize * mTreeMigOccupancy; // scale to production for the whole cell
            this.mNonSeedYearFraction = this.Species.NonSeedYearFraction;
            this.mLddMaximumSeedlingDensity = model.Project.Model.SeedDispersal.LongDistanceDispersal.MinimumSeedlingDensity;
            this.mLddMinimumSeedlingDensity = model.Project.Model.SeedDispersal.LongDistanceDispersal.MaximumSeedlingDensity;
            this.mLddRings = model.Project.Model.SeedDispersal.LongDistanceDispersal.Rings;
            this.mLddSeedlingsPerCell = model.Project.Model.SeedDispersal.LongDistanceDispersal.SeedlingsPerCell;
            this.mLddSeedlingsPerCell = MathF.Max(this.mLddSeedlingsPerCell, this.mLddMaximumSeedlingDensity);

            // long distance dispersal
            float ldd_area = this.SetupLongDistanceDispersal();
            this.CreateKernel(mKernelSeedYear, mTreeMigFecundityPerCell, 1.0F - ldd_area);

            // the kernel for non seed years looks similar, but is simply linearly scaled down
            // using the species parameter NonSeedYearFraction.
            // the central pixel still gets the value of 1 (i.e. 100% probability)
            this.CreateKernel(mKernelNonSeedYear, mTreeMigFecundityPerCell * mNonSeedYearFraction, 1.0F - ldd_area);

            if (this.Species.FecunditySerotiny > 0.0)
            {
                // an extra seed map is used for storing information related to post-fire seed rain
                this.mSeedMapSerotiny.Clear();
                this.mSeedMapSerotiny.Setup(model.Landscape.HeightGrid.PhysicalExtent, Constant.SeedmapSize);
                this.mSeedMapSerotiny.Fill(0.0F);

                // set up the special seed kernel for post fire seed rain
                this.CreateKernel(this.mKernelSerotiny, this.mTreeMigFecundityPerCell * this.Species.FecunditySerotiny, 1.0F);
                Debug.WriteLine("created extra seed map and serotiny seed kernel for species " + Species.Name + " with fecundity factor " + Species.FecunditySerotiny);
            }
            this.mHasPendingSerotiny = false;

            // debug info
            this.mDumpSeedMaps = String.IsNullOrWhiteSpace(model.Project.Model.SeedDispersal.DumpSeedMapsPath) == false;
            if (this.mDumpSeedMaps)
            {
                string path = model.Project.GetFilePath(ProjectDirectory.Home, model.Project.Model.SeedDispersal.DumpSeedMapsPath);
                File.WriteAllText(String.Format("{0}/seedkernelYes_{1}.csv", path, Species.ID), mKernelSeedYear.ToString());
                File.WriteAllText(String.Format("{0}/seedkernelNo_{1}.csv", path, Species.ID), mKernelNonSeedYear.ToString());
                if (this.mKernelSerotiny.IsNotSetup() == false)
                {
                    File.WriteAllText(String.Format("{0}/seedkernelSerotiny_{1}.csv", path, Species.ID), mKernelSerotiny.ToString());
                }
            }

            // external seeds
            this.mHasExternalSeedInput = false;
            this.mExternalSeedBuffer = 0;
            this.mExternalSeedDirection = 0;
            this.mExternalSeedBackgroundInput = 0.0F;

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
                        String.IsNullOrWhiteSpace(seedDispersal.ExternalSeedSpecies))
                    {
                        throw new NotSupportedException("An external seed species, source, buffer, and background input must be specified when external seed is enabled but the seed belt is disabled.");
                    }

                    // external seeds specified fixedly per cardinal direction
                    // current species in list??
                    this.mHasExternalSeedInput = seedDispersal.ExternalSeedSpecies.Contains(this.Species.ID);
                    string direction = seedDispersal.ExternalSeedDirection.ToLowerInvariant();
                    // encode cardinal positions as bits: e.g: "e,w" . 6
                    this.mExternalSeedDirection += direction.Contains("n") ? 1 : 0;
                    this.mExternalSeedDirection += direction.Contains("e") ? 2 : 0;
                    this.mExternalSeedDirection += direction.Contains("s") ? 4 : 0;
                    this.mExternalSeedDirection += direction.Contains("w") ? 8 : 0;
                    List<string> buffer_list = Regex.Matches(seedDispersal.ExternalSeedBuffer, "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    int index = buffer_list.IndexOf(this.Species.ID);
                    if (index >= 0)
                    {
                        this.mExternalSeedBuffer = Int32.Parse(buffer_list[index + 1]);
                        Debug.WriteLine("enabled special buffer for species " + Species.ID + ": distance of " + mExternalSeedBuffer + " pixels = " + mExternalSeedBuffer * 20.0 + " m");
                    }

                    // background seed rain (i.e. for the full landscape), use regexp
                    List<string> background_input_list = Regex.Matches(seedDispersal.ExternalSeedBackgroundInput, "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    index = background_input_list.IndexOf(this.Species.ID);
                    if (index >= 0)
                    {
                        this.mExternalSeedBackgroundInput = Single.Parse(background_input_list[index + 1]);
                        Debug.WriteLine("enabled background seed input (for full area) for species " + Species.ID + ": p=" + mExternalSeedBackgroundInput);
                    }

                    if (this.mHasExternalSeedInput)
                    {
                        Debug.WriteLine("External seed input enabled for " + Species.ID);
                    }
                }
            }

            // setup of seed kernel
            //    int max_radius = 15; // pixels
            //
            //    mSeedKernel.clear();
            //    mSeedKernel.setup(mSeedMap.cellsize(), 2*max_radius + 1 , 2*max_radius + 1);
            //    mKernelOffset = max_radius;
            //    // filling of the kernel.... for simplicity: a linear kernel
            //    Point center = Point(mKernelOffset, mKernelOffset);
            //    float max_dist = max_radius * seedmap_size;
            //    for (float *p=mSeedKernel.begin(); p!=mSeedKernel.end();++p) {
            //        float d = mSeedKernel.distance(center, mSeedKernel.indexOf(p));
            //        *p = Math.Max( 1.0 - d / max_dist, 0.0);
            //    }

            // randomize seed map.... set 1/3 to "filled"
            //for (int i=0;i<mSeedMap.count(); i++)
            //    mSeedMap[mSeedMap.randomPosition()) = 1.0;


            //    QImage img = gridToImage(mSeedMap, true, -1., 1.0);
            //    img.save("seedmap.png");

            //    img = gridToImage(mSeedMap, true, -1., 1.0);
            //    img.save("seedmap_e.png");
        }

        public void SetupExternalSeeds(Model model)
        {
            this.mExternalSeedBaseMap.Clear();
            if (model.Project.Model.SeedDispersal.ExternalSeedBelt.Enabled == false)
            {
                return;
            }

            //using DebugTimer t = model.DebugTimers.Create("SeedDispertal.SetupExternalSeeds()");
            int seedbeltWidth = model.Project.Model.SeedDispersal.ExternalSeedBelt.WidthInM;
            // setup of sectors
            // setup of base map
            float seedmap_size = 20.0F;
            this.mExternalSeedBaseMap = new Grid<float>();
            this.mExternalSeedBaseMap.Setup(model.Landscape.HeightGrid.PhysicalExtent, seedmap_size);
            this.mExternalSeedBaseMap.Fill(0.0F);
            if (this.mExternalSeedBaseMap.Count * 4 != model.Landscape.HeightGrid.Count)
            {
                throw new NotSupportedException("Width and height of the project area need to be a multiple of 20m when external seeds are enabled.");
            }
            // make a copy of the 10m height grid in lower resolution and mark pixels that are forested and outside of
            // the project area.
            for (int indexY = 0; indexY < mExternalSeedBaseMap.SizeY; ++indexY)
            {
                for (int indexX = 0; indexX < mExternalSeedBaseMap.SizeX; ++indexX)
                {
                    bool cellIsInWorld = model.Landscape.HeightGrid[2 * indexX, 2 * indexY].IsOnLandscape();
                    this.mExternalSeedBaseMap[indexX, indexY] = cellIsInWorld ? -1.0F : 1.0F;
                }
            }
            //string path = model.GlobalSettings.Path(model.GlobalSettings.Settings.GetString("Model.SeedDispersal.dumpSeedMapsPath"));

            // now scan the pixels of the belt: paint all pixels that are close to the project area
            // we do this 4 times (for all cardinal direcitons)
            for (int indexY = 0; indexY < this.mExternalSeedBaseMap.SizeY; ++indexY)
            {
                for (int indexX = 0; indexX < this.mExternalSeedBaseMap.SizeX; ++indexX)
                {
                    if (this.mExternalSeedBaseMap[indexX, indexY] != 1.0)
                    {
                        continue;
                    }

                    int lookForward = Math.Min(indexX + seedbeltWidth, this.mExternalSeedBaseMap.SizeX - 1);
                    if (this.mExternalSeedBaseMap[lookForward, indexY] == -1.0F)
                    {
                        // fill pixels
                        for (; indexX < lookForward; ++indexX)
                        {
                            float v = this.mExternalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                this.mExternalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // right to left
            for (int indexY = 0; indexY < this.mExternalSeedBaseMap.SizeY; ++indexY)
            {
                for (int indexX = this.mExternalSeedBaseMap.SizeX; indexX >= 0; --indexX)
                {
                    if (this.mExternalSeedBaseMap[indexX, indexY] != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Max(indexX - seedbeltWidth, 0);
                    if (this.mExternalSeedBaseMap[look_forward, indexY] == -1.0F)
                    {
                        // fill pixels
                        for (; indexX > look_forward; --indexX)
                        {
                            float v = this.mExternalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                this.mExternalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // up and down ***
            // from top to bottom
            for (int indexX = 0; indexX < this.mExternalSeedBaseMap.SizeX; ++indexX)
            {
                for (int indexY = 0; indexY < this.mExternalSeedBaseMap.SizeY; ++indexY)
                {
                    if (this.mExternalSeedBaseMap[indexX, indexY] != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Min(indexY + seedbeltWidth, this.mExternalSeedBaseMap.SizeY - 1);
                    if (this.mExternalSeedBaseMap[indexX, look_forward] == -1.0)
                    {
                        // fill pixels
                        for (; indexY < look_forward; ++indexY)
                        {
                            float v = mExternalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }
            // bottom to top ***
            for (int indexY = 0; indexY < mExternalSeedBaseMap.SizeY; ++indexY)
            {
                for (int indexX = mExternalSeedBaseMap.SizeX; indexX >= 0; --indexX)
                {
                    if (mExternalSeedBaseMap[indexX, indexY] != 1.0)
                        continue;
                    int look_forward = Math.Max(indexY - seedbeltWidth, 0);
                    if (mExternalSeedBaseMap[indexX, look_forward] == -1.0)
                    {
                        // fill pixels
                        for (; indexY > look_forward; --indexY)
                        {
                            float v = mExternalSeedBaseMap[indexX, indexY];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[indexX, indexY] = 2.0F;
                            }
                        }
                    }
                }
            }

            mExtSeedData.Clear();
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
                if (x < 0 || x >= sectorsX || y < 0 || y >= sectorsY)
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
                List<string> speciesIDs = species.SpeciesIDs.Split(" ").ToList();
                for (int speciesIndex = 0; speciesIndex < speciesIDs.Count; ++speciesIndex)
                {
                    List<float> space = mExtSeedData[speciesIDs[speciesIndex]];
                    if (space.Count == 0)
                    {
                        space.Capacity = sectorsX * sectorsY; // are initialized to 0s
                    }
                    float fraction = Single.Parse(speciesIDs[++speciesIndex]);
                    space.Add(fraction);
                }
            }
            this.mExternalSeedSectorX = sectorsX;
            this.mExternalSeedSectorY = sectorsY;
            // Debug.WriteLine("setting up of external seed maps finished");
        }

        public void SeedProductionSerotiny(Point positionIndex)
        {
            if (this.mSeedMapSerotiny.IsNotSetup())
            {
                throw new NotSupportedException("Tried to set a seed source for a non-serotinous species.");
            }
            this.mSeedMapSerotiny[positionIndex.X, positionIndex.Y, mIndexFactor] = 1.0F;
            this.mHasPendingSerotiny = true;
        }

        public void CreateKernel(Grid<float> kernel, float maxSeed, float scaleArea)
        {
            float maxDistance = this.TreeMigDistanceforProbability(mLddMaximumSeedlingDensity / this.Species.FecundityM2);
            float seedCellSize = this.SeedMap.CellSize;
            // e.g.: cell_size: regeneration grid (e.g. 400qm), px-size: light-grid (4qm)
            float occupation = seedCellSize * seedCellSize / (Constant.LightSize * Constant.LightSize * mTreeMigOccupancy);

            kernel.Clear();
            int maxCellDistance = (int)(maxDistance / seedCellSize);
            kernel.Setup(2 * maxCellDistance + 1, 2 * maxCellDistance + 1, SeedMap.CellSize);
            int kernelOffset = maxCellDistance;

            // filling of the kernel.... use the treemig density function
            float dist_center_cell = MathF.Sqrt(seedCellSize * seedCellSize / MathF.PI);
            Point kernelCenter = new Point(kernelOffset, kernelOffset);
            for (int kernelIndex = 0; kernelIndex < kernel.Count; ++kernelIndex)
            {
                float value = kernel.GetCenterToCenterCellDistance(kernelCenter, kernel.GetCellPosition(kernelIndex));
                if (value == 0.0)
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
            if (sum == 0.0 || occupation == 0.0)
            {
                // TODO: shouldn't kernel probabilities sum to 1.0?
                throw new NotSupportedException("Sum of probabilities is zero.");
            }

            // the sum of all kernel cells has to equal 1 (- long distance dispersal)
            kernel.Multiply(scaleArea / sum);

            if (mProbMode)
            {
                // probabilities are derived in multiplying by seed number, and dividing by occupancy criterion
                float fecundityFactor = maxSeed / occupation;
                kernel.Multiply(fecundityFactor);
                // all cells that get more seeds than the occupancy criterion are considered to have no seed limitation for regeneration
                for (int kernelIndex = 0; kernelIndex < kernel.Count; ++kernelIndex)
                {
                    // TODO: why isn't this a call to kernel.limit(0, 1)?
                    kernel[kernelIndex] = Math.Min(kernel[kernelIndex], 1.0F);
                }
            }
            // set the parent cell to 1
            //kernel[kernel_offset, kernel_offset)=1.0F;

            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("kernel setup. Species: " + Species.ID + " kernel-size: " + kernel.CellsX + " x " + kernel.CellsY + " pixels, sum (after scaling): " + kernel.Sum());
            //}
        }

        private float SetupLongDistanceDispersal()
        {
            this.mLddSeedsByRing.Clear();
            this.mLddDistance.Clear();
            if (this.mLddMinimumSeedlingDensity >= this.mLddMaximumSeedlingDensity)
            {
                throw new NotSupportedException("Minimum seed intensity is greater than or equal to maximum seed intensity?");
            }

            float minimumRadius = this.TreeMigDistanceforProbability(this.mLddMaximumSeedlingDensity / this.Species.FecundityM2);
            float maximumRadius = this.TreeMigDistanceforProbability(this.mLddMinimumSeedlingDensity / this.Species.FecundityM2);
            this.mLddDistance.Add(minimumRadius);
            float ldd_sum = 0.0F;
            for (int ring = 0; ring < this.mLddRings; ++ring)
            {
                float innerRadius = this.mLddDistance[^1];
                this.mLddDistance.Add(this.mLddDistance[^1] + (maximumRadius - minimumRadius) / this.mLddRings);
                float outerRadius = this.mLddDistance[^1];
                // calculate the value of the kernel for the middle of the ring
                float kernelInner = this.TreeMig(innerRadius); // kernel value at the inner border of the ring
                float kernelOuter = this.TreeMig(outerRadius); // kernel value at the outer border of the ring
                float meanEstimate = kernelInner * 0.4F + kernelOuter * 0.6F; // this is the average p -- 0.4/0.6 better estimate the nonlinear behavior (fits very well for medium to large kernels, e.g. piab)
                // calculate the area of the ring
                float ringArea = MathF.PI * (outerRadius * outerRadius - innerRadius * innerRadius); // m²
                // the number of px considers the fecundity
                float n_px = meanEstimate * ringArea * this.Species.FecundityM2 / this.mLddSeedlingsPerCell; // mean * m² * germinating seeds/m²  / seedlings = dimensionless?
                ldd_sum += meanEstimate * ringArea; // this fraction of the full kernel (=1) is distributed in this ring

                this.mLddSeedsByRing.Add(n_px);
            }
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("Setup LDD for " + Species.Name + ", using probability: " + mLDDSeedlings + ": Distances: " + mLDDDistance + ", seed pixels: " + mLDDDensity + "covered prob: " + ldd_sum);
            //}

            return ldd_sum;
        }

        /* R-Code:
        treemig=function(as1,as2,ks,d) # two-part exponential function, cf. Lischke & Loeffler (2006), Annex
                {
                p1=(1-ks)*Math.Exp(-d/as1)/as1
                if(as2>0){p2=ks*Math.Exp(-d/as2)/as2}else{p2=0}
                p1+p2
                }
        */

        /// the used kernel function
        /// see also Appendix B of iland paper II (note the different variable names)
        /// the function returns the seed density at a point with distance 'distance'.
        private float TreeMig(float distance)
        {
            float p1 = (1.0F - mTreeMigKappas) * MathF.Exp(-distance / mTreeMigAlphas1) / mTreeMigAlphas1;
            float p2 = 0.0F;
            if (mTreeMigAlphas2 > 0.0F)
            {
                p2 = mTreeMigKappas * MathF.Exp(-distance / mTreeMigAlphas2) / mTreeMigAlphas2;
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
            while (this.TreeMig(dist) > probability && dist < 10000.0F)
            {
                dist += 10.0F;
            }
            return dist;
        }

        public void SetupExternalSeedsForSpecies(Model model, TreeSpecies species)
        {
            if (this.mExtSeedData.ContainsKey(species.ID) == false)
            {
                return; // nothing to do
            }

            // Debug.WriteLine("setting up external seed map for " + species.ID);
            int cellsX = this.mExternalSeedMap.SizeX / this.mExternalSeedSectorX; // number of cells per sector
            int cellsY = this.mExternalSeedMap.SizeY / this.mExternalSeedSectorY; // number of cells per sector
            List<float> pcts = this.mExtSeedData[species.ID];
            this.mExternalSeedMap.Setup(this.SeedMap);
            this.mExternalSeedMap.Fill(0.0F);
            for (int sectorX = 0; sectorX < this.mExternalSeedSectorX; ++sectorX)
            {
                for (int sectorY = 0; sectorY < this.mExternalSeedSectorY; ++sectorY)
                {
                    int xMin = sectorX * cellsX;
                    int xMax = (sectorX + 1) * cellsX;
                    int yMin = sectorY * cellsY;
                    int yMax = (sectorY + 1) * cellsY;
                    // now loop over the whole sector
                    int index = sectorY * this.mExternalSeedSectorX + sectorX;
                    float p = pcts[index];
                    for (int indexY = yMin; indexY < yMax; ++indexY)
                    {
                        for (int indexX = xMin; indexX < xMax; ++indexX)
                        {
                            // check
                            if (this.mExternalSeedBaseMap[indexX, indexY] == 2.0F)
                            {
                                if (model.RandomGenerator.GetRandomFloat() < p)
                                {
                                    mExternalSeedMap[indexX, indexY] = 1.0F; // flag
                                }
                            }
                        }
                    }
                }
            }

            if (!mProbMode)
            {
                // scale external seed values to have pixels with LAI=3
                for (int p = 0; p < mExternalSeedMap.Count; ++p)
                {
                    mExternalSeedMap[p] *= 3.0F * mExternalSeedMap.CellSize * mExternalSeedMap.CellSize;
                }
            }
        }

        /// debug function: loads a image of arbirtrary size...
        public void LoadFromImage(string fileName)
        {
            this.SeedMap.Clear();
            Grid.LoadGridFromImage(fileName, this.SeedMap);
            for (int cellIndex = 0; cellIndex != this.SeedMap.Count; ++cellIndex)
            {
                this.SeedMap[cellIndex] = this.SeedMap[cellIndex] > 0.8F ? 1.0F : 0.0F;
            }
        }

        public void Clear(Model model)
        {
            Grid<float> seedMap = this.SeedMap;
            if (this.mProbMode == false)
            {
                seedMap = this.mSourceMap;
                this.SeedMap.Fill(0.0F);
            }
            if (this.mExternalSeedMap.IsNotSetup() == false)
            {
                // we have a preprocessed initial value for the external seed map (see setupExternalSeeds() et al)
                seedMap.CopyFrom(mExternalSeedMap);
                return;
            }

            // clear the map
            float background_value = this.mExternalSeedBackgroundInput; // there is potentitally a background probability <>0 for all pixels.
            seedMap.Fill(background_value);
            if (this.mHasExternalSeedInput)
            {
                // if external seed input is enabled, the buffer area of the seed maps is
                // "turned on", i.e. set to 1.
                int bufferSize = (int)(model.Project.World.Geometry.Buffer / seedMap.CellSize);
                // if a special buffer is defined, reduce the size of the input
                if (this.mExternalSeedBuffer > 0)
                {
                    bufferSize -= this.mExternalSeedBuffer;
                }
                if (bufferSize <= 0.0)
                {
                    throw new NotSupportedException("Invalid buffer size.");
                }

                for (int indexY = 0; indexY < seedMap.SizeY; ++indexY)
                {
                    for (int indeX = 0; indeX < seedMap.SizeX; ++indeX)
                    {
                        if (indexY < bufferSize || indexY >= seedMap.SizeY - bufferSize || indeX < bufferSize || indeX >= seedMap.SizeX - bufferSize)
                        {
                            if (mExternalSeedDirection == 0)
                            {
                                // seeds from all directions
                                seedMap[indeX, indexY] = 1.0F;
                            }
                            else
                            {
                                // seeds only from specific directions
                                float value = 0.0F;
                                if (Maths.IsBitSet(mExternalSeedDirection, 1) && indeX >= seedMap.SizeX - bufferSize)
                                {
                                    value = 1; // north
                                }
                                if (Maths.IsBitSet(mExternalSeedDirection, 2) && indexY < bufferSize)
                                {
                                    value = 1; // east
                                }
                                if (Maths.IsBitSet(mExternalSeedDirection, 3) && indeX < bufferSize)
                                {
                                    value = 1; // south
                                }
                                if (Maths.IsBitSet(mExternalSeedDirection, 4) && indexY >= seedMap.SizeY - bufferSize)
                                {
                                    value = 1; // west
                                }
                                seedMap[indeX, indexY] = value;
                            }
                        }
                    }
                }
            }
        }

        public void DisperseSeeds(Model model)
        {
            if (this.mDumpSeedMaps)
            {
                throw new NotSupportedException("Saving of seedmaps only supported in the iLand GUI.");
            }
            if (this.mProbMode)
            {
                //using DebugTimer t = model.DebugTimers.Create("SeedDispersal.Execute()");

                // (1) detect edges
                if (this.DetectEdges(this.SeedMap))
                {
                    // (2) distribute seed probabilites from edges
                    this.DistributeSeedProbability(model, this.SeedMap, this.Species.IsSeedYear ? mKernelSeedYear : mKernelNonSeedYear);
                }

                // special case serotiny
                if (this.mHasPendingSerotiny)
                {
                    //Debug.WriteLine("calculating extra seed rain (serotiny)....");
                    if (this.DetectEdges(this.mSeedMapSerotiny))
                    {
                        this.DistributeSeedProbability(model, this.mSeedMapSerotiny, this.mKernelSerotiny);
                    }
                    // copy back data
                    for (int seedIndex = 0; seedIndex < this.SeedMap.Count; ++seedIndex)
                    {
                        this.SeedMap[seedIndex] = MathF.Max(this.SeedMap[seedIndex], this.mSeedMapSerotiny[seedIndex]);
                    }

                    float total = this.mSeedMapSerotiny.Sum();
                    this.mSeedMapSerotiny.Fill(0.0F); // clear
                    this.mHasPendingSerotiny = false;
                    Debug.WriteLine("serotiny event: extra seed input " + total + " (total sum of seed probability over all pixels of the serotiny seed map) of species " + Species.Name);
                }
            }
            else
            {
                //using DebugTimer t = model.DebugTimers.Create("SeedDispersal.DistributeSeeds()");
                // fill seed map from source map
                this.DistributeSeeds(model, this.mSourceMap);
            }
        }

        /** scans the seed image and detects "edges".
            edges are then subsequently marked (set to -1). This is pass 1 of the seed distribution process.
            */
        private bool DetectEdges(Grid<float> seedMap)
        {
            // fill mini-gaps
            int n_gaps_filled = 0;
            for (int y = 1; y < seedMap.SizeY - 1; ++y)
            {
                int p = seedMap.IndexOf(1, y);
                int p_above = p - seedMap.SizeX; // one line above
                int p_below = p + seedMap.SizeX; // one line below
                for (int x = 1; x < seedMap.SizeX - 1; ++x, ++p, ++p_below, ++p_above)
                {
                    if (seedMap[p] < 0.999F)
                    {
                        if ((seedMap[p_above - 1] == 1.0F ? 1 : 0) + (seedMap[p_above] == 1.0F ? 1 : 0) + (seedMap[p_above + 1] == 1.0F ? 1 : 0) +
                            (seedMap[p - 1] == 1.0F ? 1 : 0) + (seedMap[p + 1] == 1.0F ? 1 : 0) +
                            (seedMap[p_below - 1] == 1.0F ? 1 : 0) + (seedMap[p_below] == 1.0F ? 1 : 0) + (seedMap[p_below + 1] == 1.0F ? 1 : 0) > 3)
                        {
                            seedMap[p] = 0.999F; // if more than 3 neighbors are active pixels, the value is high
                            ++n_gaps_filled;
                        }
                    }
                }
            }

            // now detect the edges
            bool edgeFound = false;
            int edgeCount = 0;
            for (int y = 1; y < seedMap.SizeY - 1; ++y)
            {
                int p = seedMap.IndexOf(1, y);
                int p_above = p - seedMap.SizeX; // one line above
                int p_below = p + seedMap.SizeX; // one line below
                for (int x = 1; x < seedMap.SizeX - 1; ++x, ++p, ++p_below, ++p_above)
                {
                    if (seedMap[p] == 1.0F)
                    {
                        edgeFound = true;
                        if ((seedMap[p_above - 1] < 0.999F && seedMap[p_above - 1] >= 0.0F) ||
                             (seedMap[p_above] < 0.999F && seedMap[p_above] >= 0.0F) ||
                             (seedMap[p_above + 1] < 0.999F && seedMap[p_above + 1] >= 0.0F) ||
                             (seedMap[p - 1] < 0.999F && seedMap[p - 1] >= 0.0F) ||
                             (seedMap[p + 1] < 0.999F && seedMap[p + 1] >= 0.0F) ||
                             (seedMap[p_below - 1] < 0.999F && seedMap[p_below - 1] >= 0.0F) ||
                             (seedMap[p_below] < 0.999F && seedMap[p_below] >= 0.0F) ||
                             (seedMap[p_below + 1] < 0.999F && seedMap[p_below + 1] >= 0.0F))
                        {
                            seedMap[p] = -1.0F; // if any surrounding pixel is >=0 & <0.999: . mark as edge
                            ++edgeCount;
                        }
                    }

                }
            }
            if (this.mDumpSeedMaps)
            {
                Debug.WriteLine("species: " + Species.ID + " # of gaps filled: " + n_gaps_filled + " # of edge-pixels: " + edgeCount);
            }
            // TODO: what if edgeFound == true but edgeCount == 0?
            return edgeFound;
        }

        /** do the seed probability distribution.
            This is phase 2. Apply the seed kernel for each "edge" point identified in phase 1.
            */
        private void DistributeSeedProbability(Model model, Grid<float> seedmap, Grid<float> kernel)
        {
            int kernelCenter = kernel.SizeX / 2; // offset is the index of the center pixel
            for (int seedIndex = 0; seedIndex < seedmap.Count; ++seedIndex)
            {
                if (seedmap[seedIndex] == -1.0F)
                {
                    // edge pixel found. Now apply the kernel....
                    Point pt = seedmap.GetCellPosition(seedIndex);
                    for (int y = -kernelCenter; y <= kernelCenter; ++y)
                    {
                        for (int x = -kernelCenter; x <= kernelCenter; ++x)
                        {
                            float kernel_value = kernel[x + kernelCenter, y + kernelCenter];
                            if (kernel_value > 0.0F && seedmap.Contains(pt.X + x, pt.Y + y))
                            {
                                float val = seedmap[pt.X + x, pt.Y + y];
                                if (val != -1.0F)
                                {
                                    seedmap[pt.X + x, pt.Y + y] = MathF.Min(1.0F - (1.0F - val) * (1.0F - kernel_value), 1.0F);
                                }
                            }
                        }
                    }
                    // long distance dispersal
                    if (this.mLddSeedsByRing.Count != 0)
                    {
                        float yearScaling = this.Species.IsSeedYear ? 1.0F : mNonSeedYearFraction;
                        for (int distanceIndex = 0; distanceIndex < this.mLddSeedsByRing.Count; ++distanceIndex)
                        {
                            float lddProbability = this.mLddSeedlingsPerCell; // pixels will have this probability
                            int nCells = (int)MathF.Round(this.mLddSeedsByRing[distanceIndex] * yearScaling); // number of pixels to activate
                            for (int cell = 0; cell < nCells; ++cell)
                            {
                                // distance and direction:
                                float radius = model.RandomGenerator.GetRandomFloat(mLddDistance[distanceIndex], mLddDistance[distanceIndex + 1]) / seedmap.CellSize; // choose a random distance (in pixels)
                                float phi = model.RandomGenerator.GetRandomFloat() * 2.0F * MathF.PI; // choose a random direction
                                Point ldd = new Point((int)(pt.X + radius * MathF.Cos(phi)), (int)(pt.Y + radius * MathF.Sin(phi)));
                                if (seedmap.Contains(ldd))
                                {
                                    float val = seedmap[ldd];
                                    // use the same adding of probabilities
                                    if (val != -1.0F)
                                    {
                                        seedmap[ldd] = MathF.Min(1.0F - (1.0F - val) * (1.0F - lddProbability), 1.0F);
                                    }
                                }
                            }
                        }
                    }
                    seedmap[seedIndex] = 1.0F; // mark as processed
                } // if (seedmap[p]==1)
            } // for()
        }

        private void DistributeSeeds(Model model, Grid<float> sourceMap)
        {
            Grid<float> kernel = mKernelSeedYear;

            // *** estimate seed production (based on leaf area) ***
            // calculate number of seeds; the source map holds now m2 leaf area on 20x20m pixels
            // after this step, each source cell has a value between 0 (no source) and 1 (fully covered cell)
            float fecundity = this.Species.FecundityM2;
            if (this.Species.IsSeedYear == false)
            {
                fecundity *= mNonSeedYearFraction;
            }
            for (int sourceIndex = 0; sourceIndex < sourceMap.Count; ++sourceIndex)
            {
                if (sourceMap[sourceIndex] != 0.0F)
                {
                    // if LAI  >3, then full potential is assumed, below LAI=3 a linear ramp is used
                    sourceMap[sourceIndex] = Math.Min(sourceMap[sourceIndex] / (sourceMap.CellSize * sourceMap.CellSize) / 3.0F, 3.0F);
                }
            }

            // sink mode
            //    // now look for each pixel in the targetmap and sum up seeds*kernel
            //    int idx=0;
            //    int offset = kernel.sizeX() / 2; // offset is the index of the center pixel
            //    //Grid<ResourceUnit*> &ru_map = GlobalSettings.instance().model().RUgrid();
            //    DebugTimer tsink("seed_sink"); {
            //    for (float *t=mSeedMap.begin(); t!=mSeedMap.end(); ++t, ++idx) {
            //        // skip out-of-project areas
            //        //if (!ru_map.constValueAtIndex(mSeedMap.index5(idx)))
            //        //    continue;
            //        // apply the kernel
            //        Point sm=mSeedMap.indexOf(t)-Point(offset, offset);
            //        for (int iy=0;iy<kernel.sizeY();++iy) {
            //            for (int ix=0;ix<kernel.sizeX();++ix) {
            //                if (sourcemap.isIndexValid(sm.x()+ix, sm.y()+iy))
            //                    *t+=sourcemap(sm.x()+ix, sm.y()+iy) * kernel(ix, iy);
            //            }
            //        }
            //    }
            //    } // debugtimer
            //    mSeedMap.initialize(0.0F); // just for debugging...
            int kernelCenter = kernel.SizeX / 2; // offset is the index of the center pixel
            // source mode

            // *** seed distribution (kernel + long distance dispersal) ***
            if (model.Project.World.Geometry.IsTorus == false)
            {
                // ** standard case (no torus) **
                for (int sourceIndex = 0; sourceIndex < sourceMap.Count; ++sourceIndex)
                {
                    if (sourceMap[sourceIndex] > 0.0F)
                    {
                        Point kernelOrigin = sourceMap.GetCellPosition(sourceIndex).Subtract(kernelCenter);
                        int kernelX = kernelOrigin.X;
                        int kernelY = kernelOrigin.Y;
                        for (int indexY = 0; indexY < kernel.SizeY; ++indexY)
                        {
                            for (int indexX = 0; indexX < kernel.SizeX; ++indexX)
                            {
                                if (this.SeedMap.Contains(kernelX + indexX, kernelY + indexY))
                                {
                                    this.SeedMap[kernelX + indexX, kernelY + indexY] += sourceMap[sourceIndex] * kernel[indexX, indexY];
                                }
                            }
                        }
                        // long distance dispersal
                        if (this.mLddSeedsByRing.Count > 0)
                        {
                            Point sourceCellIndex = sourceMap.GetCellPosition(sourceIndex);
                            for (int ringIndex = 0; ringIndex < this.mLddSeedsByRing.Count; ++ringIndex)
                            {
                                float ldd_val = this.mLddSeedlingsPerCell / fecundity; // pixels will have this probability [note: fecundity will be multiplied below]
                                int nSeeds;
                                if (this.mLddSeedsByRing[ringIndex] < 1)
                                {
                                    nSeeds = model.RandomGenerator.GetRandomFloat() < this.mLddSeedsByRing[ringIndex] ? 1 : 0;
                                }
                                else
                                {
                                    nSeeds = (int)MathF.Round(this.mLddSeedsByRing[ringIndex]); // number of pixels to activate
                                }
                                for (int seedIndex = 0; seedIndex < nSeeds; ++seedIndex)
                                {
                                    // distance and direction:
                                    float radiusInCells = model.RandomGenerator.GetRandomFloat(this.mLddDistance[ringIndex], this.mLddDistance[ringIndex + 1]) / this.SeedMap.CellSize; // choose a random distance (in pixels)
                                    float phi = 2.0F * MathF.PI * model.RandomGenerator.GetRandomFloat(); // choose a random direction
                                    Point seedCellPosition = new Point(sourceCellIndex.X + (int)(radiusInCells * MathF.Cos(phi)), sourceCellIndex.Y + (int)(radiusInCells * MathF.Sin(phi)));
                                    if (this.SeedMap.Contains(seedCellPosition))
                                    {
                                        this.SeedMap[seedCellPosition] += ldd_val;
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
                int seedmapOffset = sourceMap.GetCellIndex(new PointF(0.0F, 0.0F)).X; // the seed maps have x extra rows/columns
                int seedCellsPerRU = (int)(Constant.RUSize / sourceMap.CellSize);
                for (int sourceIndex = 0; sourceIndex < sourceMap.Count; ++sourceIndex)
                {
                    if (sourceMap[sourceIndex] > 0.0F)
                    {
                        Point sourceCellPosition = sourceMap.GetCellPosition(sourceIndex);
                        // get the origin of the resource unit *on* the seedmap in *seedmap-coords*:
                        Point ruOffset = new Point(((sourceCellPosition.X - seedmapOffset) / seedCellsPerRU) * seedCellsPerRU + seedmapOffset,
                                                   ((sourceCellPosition.Y - seedmapOffset) / seedCellsPerRU) * seedCellsPerRU + seedmapOffset);  // coords RU origin
                        Point offsetInRU = new Point((sourceCellPosition.X - seedmapOffset) % seedCellsPerRU, (sourceCellPosition.Y - seedmapOffset) % seedCellsPerRU);  // offset of current point within the RU

                        //Point sm=sourcemap.indexOf(src)-Point(offset, offset);
                        for (int indexY = 0; indexY < kernel.SizeY; ++indexY)
                        {
                            for (int indexX = 0; indexX < kernel.SizeX; ++indexX)
                            {
                                Point torusIndex = ruOffset.Add(new Point(Maths.Modulo((offsetInRU.X - kernelCenter + indexX), seedCellsPerRU), Maths.Modulo((offsetInRU.Y - kernelCenter + indexY), seedCellsPerRU)));
                                if (this.SeedMap.Contains(torusIndex))
                                {
                                    this.SeedMap[torusIndex] += sourceMap[sourceIndex] * kernel[indexX, indexY];
                                }
                            }
                        }
                        // long distance dispersal
                        if (mLddSeedsByRing.Count != 0)
                        {
                            for (int densityIndex = 0; densityIndex < mLddSeedsByRing.Count; ++densityIndex)
                            {
                                float ldd_val = this.mLddSeedlingsPerCell / fecundity; // pixels will have this probability [note: fecundity will be multiplied below]
                                int nSeeds;
                                if (mLddSeedsByRing[densityIndex] < 1)
                                {
                                    nSeeds = model.RandomGenerator.GetRandomFloat() < mLddSeedsByRing[densityIndex] ? 1 : 0;
                                }
                                else
                                {
                                    nSeeds = (int)Math.Round(mLddSeedsByRing[densityIndex]); // number of pixels to activate
                                }
                                for (int seed = 0; seed < nSeeds; ++seed)
                                {
                                    // distance and direction:
                                    float radius = model.RandomGenerator.GetRandomFloat(mLddDistance[densityIndex], mLddDistance[densityIndex + 1]) / SeedMap.CellSize; // choose a random distance (in pixels)
                                    float phi = model.RandomGenerator.GetRandomFloat() * 2.0F * MathF.PI; // choose a random direction
                                    Point ldd = new Point((int)(radius * MathF.Cos(phi)), (int)(radius * MathF.Sin(phi))); // destination (offset)
                                    Point torusIndex = ruOffset.Add(new Point(Maths.Modulo((offsetInRU.X + ldd.X), seedCellsPerRU), Maths.Modulo((offsetInRU.Y + ldd.Y), seedCellsPerRU)));

                                    if (this.SeedMap.Contains(torusIndex))
                                    {
                                        this.SeedMap[torusIndex] += ldd_val;
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
            // We assume that the availability of 10 potential seedlings/m2 is enough for unconstrained establishment;
            const float nUnlimited = 100.0F;
            for (int seedIndex = 0; seedIndex < this.SeedMap.Count; ++seedIndex)
            {
                if (this.SeedMap[seedIndex] > 0.0F)
                {
                    this.SeedMap[seedIndex] = MathF.Min(this.SeedMap[seedIndex] * fecundity / nUnlimited, 1.0F);
                }
            }
        }
    }
}
