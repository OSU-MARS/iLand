using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.Core
{
    /** @class SeedDispersal
        @ingroup core
        The class encapsulates the dispersal of seeds of one species over the whole landscape.
        The dispersal algortihm operate on grids with a 20m resolution.

        See http://iland.boku.ac.at/dispersal
        */
    public class SeedDispersal
    {
        private int _debug_ldd = 0;
        private Grid<float> mExternalSeedBaseMap = null; ///< static intermediate data while setting up external seeds
        // TODO: can this be made species specific?
        private readonly Dictionary<string, List<double>> mExtSeedData; ///< holds definition of species and percentages for external seed input
        private int mExtSeedSizeX = 0; ///< size of the sectors used to specify external seed input
        private int mExtSeedSizeY = 0;
        private bool mProbMode; ///< if 'true', seed dispersal uses probabilities to distribute (old version)
        private double mTM_as1, mTM_as2, mTM_ks; ///< seed dispersal paramaters (treemig)
        private double mTM_fecundity_cell; ///< maximum seeds per source cell
        private double mTM_occupancy; ///< seeds required per destination regeneration pixel
        private double mNonSeedYearFraction; ///< fraction of the seed production in non-seed-years
        private double mKernelThresholdArea, mKernelThresholdLDD; ///< value of the kernel function that is the threhold for full coverage and LDD, respectively
        private int mIndexFactor; ///< multiplier between light-pixel-size and seed-pixel-size
        private readonly Grid<float> mSourceMap; ///< (large) seedmap used to denote the sources
        private readonly Grid<float> mKernelSeedYear; ///< species specific "seed kernel" (small) for seed years
        private readonly Grid<float> mKernelNonSeedYear; ///< species specific "seed kernel" (small) for non-seed-years
        private readonly Grid<float> mKernelSerotiny; ///< seed kernel for extra seed rain
        private readonly Grid<float> mSeedMapSerotiny; ///< seed map that keeps track of serotiny events
        private readonly List<double> mLDDDistance; ///< long distance dispersal distances (e.g. the "rings")
        private readonly List<double> mLDDDensity;  ///< long distance dispersal # of cells that should be affected in each "ring"
        private int mLDDRings; ///< # of rings (with equal probability) for LDD
        private float mLDDSeedlings; ///< each LDD pixel has this probability
        private bool mHasPendingSerotiny; ///< true if active (unprocessed) pixels are on the extra-serotiny map
        private bool mDumpSeedMaps; ///< if true, seedmaps are stored as images
        private bool mHasExternalSeedInput; ///< if true, external seeds are modelled for the species
        private int mExternalSeedDirection; ///< direction of external seeds
        private int mExternalSeedBuffer; ///< how many 20m pixels away from the simulation area should the seeding start?
        private double mExternalSeedBackgroundInput; ///< background propability for this species; if set, then a certain seed availability is provided for the full area
        // external seeds
        private readonly Grid<float> mExternalSeedMap; ///< for more complex external seed input, this map holds that information

        public string DumpNextYearFileName { get; set; }
        public Grid<float> SeedMap { get; private set; } ///< (large) seedmap. Is filled by individual trees and then processed
        public Species Species { get; private set; }

        public SeedDispersal(Species species = null)
        {
            this.mExternalSeedBaseMap = new Grid<float>();
            this.mExtSeedData = new Dictionary<string, List<double>>();
            this.mExternalSeedMap = new Grid<float>();
            this.mIndexFactor = 10;
            this.mKernelSeedYear = new Grid<float>(); ///< species specific "seed kernel" (small) for seed years
            this.mKernelNonSeedYear = new Grid<float>(); ///< species specific "seed kernel" (small) for non-seed-years
            this.mKernelSerotiny = new Grid<float>(); ///< seed kernel for extra seed rain
            this.mLDDDistance = new List<double>(); ///< long distance dispersal distances (e.g. the "rings")
            this.mLDDDensity = new List<double>();  ///< long distance dispersal # of cells that should be affected in each "ring"
            this.mSeedMapSerotiny = new Grid<float>(); ///< seed map that keeps track of serotiny events
            this.mSourceMap = new Grid<float>(); ///< (large) seedmap used to denote the sources

            this.SeedMap = new Grid<float>(); ///< (large) seedmap. Is filled by individual trees and then processed
            this.Species = species;
        }

        /// setMatureTree is called by individual (mature) trees. This actually fills the initial state of the seed map.
        public void SetMatureTree(Point lip_index, double leaf_area)
        {
            if (mProbMode)
            {
                SeedMap[lip_index.X / mIndexFactor, lip_index.Y / mIndexFactor] = 1.0F;
            }
            else
            {
                mSourceMap[lip_index.X / mIndexFactor, lip_index.Y / mIndexFactor] += (float)leaf_area;
            }
        }

        /** setup of the seedmaps.
          This sets the size of the seed map and creates the seed kernel (species specific)
          */
        public void Setup(Model model)
        {
            if (model == null || model.HeightGrid == null || this.Species == null)
            {
                return;
            }
            mProbMode = false;

            // setup of seed map
            SeedMap.Clear();
            SeedMap.Setup(model.HeightGrid.PhysicalExtent, Constant.SeedmapSize);
            SeedMap.Initialize(0.0F);
            if (!mProbMode)
            {
                mSourceMap.Setup(SeedMap);
                mSourceMap.Initialize(0.0F);
            }
            mExternalSeedMap.Clear();
            mIndexFactor = Constant.SeedmapSize / Constant.LightSize; // ratio seed grid / lip-grid:
            if (model.GlobalSettings.LogInfo())
            {
                Debug.WriteLine("Seed map setup. Species: " + Species.ID + " kernel-size: " + SeedMap.CellsX + " x " + SeedMap.CellsY + " pixels.");
            }

            if (Species == null)
            {
                throw new NotSupportedException("Setup of SeedDispersal: Species not defined.");
            }

            if ((model.GlobalSettings.Settings.GetDouble("model.world.buffer", 0) % Constant.SeedmapSize) != 0.0)
            {
                throw new NotSupportedException("SeedDispersal:setup(): The buffer (model.world.buffer) must be a integer multiple of the seed pixel size (currently 20m, e.g. 20,40,60,...)).");
            }

            // settings
            mTM_occupancy = 1.0; // is currently constant
                                 // copy values for the species parameters:
            Species.GetTreeMigKernel(ref mTM_as1, ref mTM_as2, ref mTM_ks);
            mTM_fecundity_cell = Species.FecundityM2 * Constant.SeedmapSize * Constant.SeedmapSize * mTM_occupancy; // scale to production for the whole cell
            mNonSeedYearFraction = Species.NonSeedYearFraction;
            XmlHelper xml = new XmlHelper(model.GlobalSettings.Settings.Node("model.settings.seedDispersal"));
            mKernelThresholdArea = xml.GetDouble(".longDistanceDispersal.thresholdArea", 0.0001);
            mKernelThresholdLDD = xml.GetDouble(".longDistanceDispersal.thresholdLDD", 0.0001);
            mLDDSeedlings = (float)xml.GetDouble(".longDistanceDispersal.LDDSeedlings", 0.0001);
            mLDDRings = xml.ValueInt(".longDistanceDispersal.rings", 4);

            mLDDSeedlings = MathF.Max(mLDDSeedlings, (float)mKernelThresholdArea);

            // long distance dispersal
            double ldd_area = SetupLdd();

            CreateKernel(mKernelSeedYear, mTM_fecundity_cell, 1.0 - ldd_area);

            // the kernel for non seed years looks similar, but is simply linearly scaled down
            // using the species parameter NonSeedYearFraction.
            // the central pixel still gets the value of 1 (i.e. 100% probability)
            CreateKernel(mKernelNonSeedYear, mTM_fecundity_cell * mNonSeedYearFraction, 1.0 - ldd_area);

            if (Species.FecunditySerotiny > 0.0)
            {
                // an extra seed map is used for storing information related to post-fire seed rain
                mSeedMapSerotiny.Clear();
                mSeedMapSerotiny.Setup(model.HeightGrid.PhysicalExtent, Constant.SeedmapSize);
                mSeedMapSerotiny.Initialize(0.0F);

                // set up the special seed kernel for post fire seed rain
                CreateKernel(mKernelSerotiny, mTM_fecundity_cell * Species.FecunditySerotiny, 1.0);
                Debug.WriteLine("created extra seed map and serotiny seed kernel for species " + Species.Name + " with fecundity factor " + Species.FecunditySerotiny);
            }
            mHasPendingSerotiny = false;

            // debug info
            mDumpSeedMaps = model.GlobalSettings.Settings.GetBool("model.settings.seedDispersal.dumpSeedMapsEnabled", false);
            if (mDumpSeedMaps)
            {
                string path = model.GlobalSettings.Path(model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.dumpSeedMapsPath"));
                Helper.SaveToTextFile(String.Format("{0}/seedkernelYes_{1}.csv", path, Species.ID), mKernelSeedYear.ToString());
                Helper.SaveToTextFile(String.Format("{0}/seedkernelNo_{1}.csv", path, Species.ID), mKernelNonSeedYear.ToString());
                if (!mKernelSerotiny.IsEmpty())
                {
                    Helper.SaveToTextFile(String.Format("{0}/seedkernelSerotiny_{1}.csv", path, Species.ID), mKernelSerotiny.ToString());
                }
            }

            // external seeds
            mHasExternalSeedInput = false;
            mExternalSeedBuffer = 0;
            mExternalSeedDirection = 0;
            mExternalSeedBackgroundInput = 0.0;
            if (model.GlobalSettings.Settings.GetBool("model.settings.seedDispersal.externalSeedEnabled", false))
            {
                if (model.GlobalSettings.Settings.GetBool("model.settings.seedDispersal.seedBelt.enabled", false))
                {
                    // external seed input specified by sectors and around the project area (seedbelt)
                    SetupExternalSeedsForSpecies(Species);
                }
                else
                {
                    // external seeds specified fixedly per cardinal direction
                    // current species in list??
                    mHasExternalSeedInput = model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.externalSeedSpecies").Contains(Species.ID);
                    string dir = model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.externalSeedSource").ToLowerInvariant();
                    // encode cardinal positions as bits: e.g: "e,w" . 6
                    mExternalSeedDirection += dir.Contains("n") ? 1 : 0;
                    mExternalSeedDirection += dir.Contains("e") ? 2 : 0;
                    mExternalSeedDirection += dir.Contains("s") ? 4 : 0;
                    mExternalSeedDirection += dir.Contains("w") ? 8 : 0;
                    List<string> buffer_list = Regex.Matches(model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.externalSeedBuffer"), "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    int index = buffer_list.IndexOf(Species.ID);
                    if (index >= 0)
                    {
                        mExternalSeedBuffer = Int32.Parse(buffer_list[index + 1]);
                        Debug.WriteLine("enabled special buffer for species " + Species.ID + ": distance of " + mExternalSeedBuffer + " pixels = " + mExternalSeedBuffer * 20.0 + " m");
                    }

                    // background seed rain (i.e. for the full landscape), use regexp
                    List<string> background_input_list = Regex.Matches(model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.externalSeedBackgroundInput"), "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    index = background_input_list.IndexOf(Species.ID);
                    if (index >= 0)
                    {
                        mExternalSeedBackgroundInput = Double.Parse(background_input_list[index + 1]);
                        Debug.WriteLine("enabled background seed input (for full area) for species " + Species.ID + ": p=" + mExternalSeedBackgroundInput);
                    }

                    if (mHasExternalSeedInput)
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
            //    double max_dist = max_radius * seedmap_size;
            //    for (float *p=mSeedKernel.begin(); p!=mSeedKernel.end();++p) {
            //        double d = mSeedKernel.distance(center, mSeedKernel.indexOf(p));
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
            mExternalSeedBaseMap = null;
            if (!model.GlobalSettings.Settings.GetBool("model.settings.seedDispersal.seedBelt.enabled", false))
            {
                return;
            }

            using DebugTimer t = new DebugTimer("SeedDispertal.SetupExternalSeeds()");
            XmlHelper xml = new XmlHelper(model.GlobalSettings.Settings.Node("model.settings.seedDispersal.seedBelt"));
            int seedbelt_width = xml.ValueInt(".width", 10);
            // setup of sectors
            // setup of base map
            float seedmap_size = 20.0F;
            mExternalSeedBaseMap = new Grid<float>();
            mExternalSeedBaseMap.Setup(model.HeightGrid.PhysicalExtent, seedmap_size);
            mExternalSeedBaseMap.Initialize(0.0F);
            if (mExternalSeedBaseMap.Count * 4 != model.HeightGrid.Count)
            {
                throw new NotSupportedException("error in setting up external seeds: the width and height of the project area need to be a multiple of 20m when external seeds are enabled.");
            }
            // make a copy of the 10m height grid in lower resolution and mark pixels that are forested and outside of
            // the project area.
            for (int y = 0; y < mExternalSeedBaseMap.CellsY; y++)
            {
                for (int x = 0; x < mExternalSeedBaseMap.CellsX; x++)
                {
                    bool val = model.HeightGrid[x * 2, y * 2].IsOutsideWorld();
                    mExternalSeedBaseMap[x, y] = val ? 1.0F : 0.0F;
                    if (model.HeightGrid[x * 2, y * 2].IsInWorld())
                    {
                        mExternalSeedBaseMap[x, y] = -1.0F;
                    }
                }
            }
            string path = model.GlobalSettings.Path(model.GlobalSettings.Settings.GetString("model.settings.seedDispersal.dumpSeedMapsPath"));

            // now scan the pixels of the belt: paint all pixels that are close to the project area
            // we do this 4 times (for all cardinal direcitons)
            for (int y = 0; y < mExternalSeedBaseMap.CellsY; y++)
            {
                for (int x = 0; x < mExternalSeedBaseMap.CellsX; x++)
                {
                    if (mExternalSeedBaseMap[x, y] != 1.0)
                    {
                        continue;
                    }

                    int look_forward = Math.Min(x + seedbelt_width, mExternalSeedBaseMap.CellsX - 1);
                    if (mExternalSeedBaseMap[look_forward, y] == -1.0F)
                    {
                        // fill pixels
                        for (; x < look_forward; ++x)
                        {
                            float v = mExternalSeedBaseMap[x, y];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }
            // right to left
            for (int y = 0; y < mExternalSeedBaseMap.CellsY; y++)
            {
                for (int x = mExternalSeedBaseMap.CellsX; x >= 0; --x)
                {
                    if (mExternalSeedBaseMap[x, y] != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Max(x - seedbelt_width, 0);
                    if (mExternalSeedBaseMap[look_forward, y] == -1.0F)
                    {
                        // fill pixels
                        for (; x > look_forward; --x)
                        {
                            float v = mExternalSeedBaseMap[x, y];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }
            // up and down ***
            // from top to bottom
            for (int x = 0; x < mExternalSeedBaseMap.CellsX; x++)
            {
                for (int y = 0; y < mExternalSeedBaseMap.CellsY; y++)
                {
                    if (mExternalSeedBaseMap[x, y] != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Min(y + seedbelt_width, mExternalSeedBaseMap.CellsY - 1);
                    if (mExternalSeedBaseMap[x, look_forward] == -1.0)
                    {
                        // fill pixels
                        for (; y < look_forward; ++y)
                        {
                            float v = mExternalSeedBaseMap[x, y];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }
            // bottom to top ***
            for (int y = 0; y < mExternalSeedBaseMap.CellsY; y++)
            {
                for (int x = mExternalSeedBaseMap.CellsX; x >= 0; --x)
                {
                    if (mExternalSeedBaseMap[x, y] != 1.0)
                        continue;
                    int look_forward = Math.Max(y - seedbelt_width, 0);
                    if (mExternalSeedBaseMap[x, look_forward] == -1.0)
                    {
                        // fill pixels
                        for (; y > look_forward; --y)
                        {
                            float v = mExternalSeedBaseMap[x, y];
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }

            mExtSeedData.Clear();
            int sectors_x = xml.ValueInt("sizeX", 0);
            int sectors_y = xml.ValueInt("sizeY", 0);
            if (sectors_x < 1 || sectors_y < 1)
            {
                throw new NotSupportedException(String.Format("setup of external seed dispersal: invalid number of sectors x={0} y={1]", sectors_x, sectors_y));
            }

            XmlNode elem = xml.Node(".");
            for (XmlNode n = elem.FirstChild; n != null; n = n.NextSibling)
            {
                if (n.Name.StartsWith("species"))
                {
                    List<string> coords = n.Name.Split("_").ToList();
                    if (coords.Count() != 3)
                    {
                        throw new NotSupportedException("external seed species definition is not valid: " + n.Name);
                    }
                    int x = Int32.Parse(coords[1]);
                    int y = Int32.Parse(coords[2]);
                    if (x < 0 || x >= sectors_x || y < 0 || y >= sectors_y)
                    {
                        throw new NotSupportedException(String.Format("invalid sector for specifiing external seed input (x y): {0} {1} ", x, y));
                    }
                    int index = y * sectors_x + x;

                    string text = xml.GetString("." + n.Name);
                    Debug.WriteLine("processing element " + n.Name + " x,y: " + x + y + text);
                    // we assume pairs of name and fraction
                    List<string> species_list = text.Split(" ").ToList();
                    for (int i = 0; i < species_list.Count; ++i)
                    {
                        List<double> space = mExtSeedData[species_list[i]];
                        if (space.Count == 0)
                        {
                            space.Capacity = sectors_x * sectors_y; // are initialized to 0s
                        }
                        double fraction = Double.Parse(species_list[++i]);
                        space.Add(fraction);
                    }
                }
            }
            mExtSeedSizeX = sectors_x;
            mExtSeedSizeY = sectors_y;
            Debug.WriteLine("setting up of external seed maps finished");
        }

        public void SeedProductionSerotiny(Point position_index)
        {
            if (mSeedMapSerotiny.IsEmpty())
            {
                throw new NotSupportedException("Invalid use seedProductionSerotiny(): tried to set a seed source for a non-serotinous species!");
            }
            mSeedMapSerotiny[position_index.X / mIndexFactor, position_index.Y / mIndexFactor] = 1.0F;
            mHasPendingSerotiny = true;
        }

        public void CreateKernel(Grid<float> kernel, double max_seed, double scale_area)
        {
            double max_dist = TreemigDistanceforProbability(mKernelThresholdArea / Species.FecundityM2);
            double cell_size = SeedMap.CellSize;
            int max_radius = (int)(max_dist / cell_size);
            // e.g.: cell_size: regeneration grid (e.g. 400qm), px-size: light-grid (4qm)
            double occupation = cell_size * cell_size / (Constant.LightSize * Constant.LightSize * mTM_occupancy);

            kernel.Clear();

            kernel.Setup(SeedMap.CellSize, 2 * max_radius + 1, 2 * max_radius + 1);
            int kernel_offset = max_radius;

            // filling of the kernel.... use the treemig density function
            double dist_center_cell = Math.Sqrt(cell_size * cell_size / Math.PI);
            Point center = new Point(kernel_offset, kernel_offset);
            for (int p = 0; p < kernel.Count; ++p)
            {
                double d = kernel.GetCenterToCenterCellDistance(center, kernel.IndexOf(p));
                if (d == 0.0)
                {
                    kernel[p] = (float)TreemigCenterCell(dist_center_cell); // r is the radius of a circle with the same area as a cell
                }
                else
                {
                    kernel[p] = d <= max_dist ? (float)((Treemig(d + dist_center_cell) + Treemig(d - dist_center_cell)) / 2.0F * cell_size * cell_size) : 0.0F;
                }
            }

            // normalize
            float sum = kernel.Sum();
            if (sum == 0.0 || occupation == 0.0)
            {
                throw new NotSupportedException("create seed kernel: sum of probabilities = 0!");
            }

            // the sum of all kernel cells has to equal 1 (- long distance dispersal)
            kernel.Multiply((float)scale_area / sum);

            if (mProbMode)
            {
                // probabilities are derived in multiplying by seed number, and dividing by occupancy criterion
                float fecundity_factor = (float)(max_seed / occupation);
                kernel.Multiply(fecundity_factor);
                // all cells that get more seeds than the occupancy criterion are considered to have no seed limitation for regeneration
                for (int p = 0; p < kernel.Count; ++p)
                {
                    // BUGBUG: why isn't this a call to kernel.limit(0, 1)?
                    kernel[p] = Math.Min(kernel[p], 1.0F);
                }
            }
            // set the parent cell to 1
            //kernel[kernel_offset, kernel_offset)=1.0F;

            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("kernel setup. Species: " + Species.ID + " kernel-size: " + kernel.CellsX + " x " + kernel.CellsY + " pixels, sum (after scaling): " + kernel.Sum());
            //}
        }

        private double SetupLdd()
        {
            mLDDDensity.Clear();
            mLDDDistance.Clear();
            if (mKernelThresholdLDD >= mKernelThresholdArea)
            {
                // no long distance dispersal
                return 0.0;

            }
            double r_min = TreemigDistanceforProbability(mKernelThresholdArea / Species.FecundityM2);
            double r_max = TreemigDistanceforProbability(mKernelThresholdLDD / Species.FecundityM2);
            mLDDDistance.Add(r_min);
            double ldd_sum = 0.0;
            for (int i = 0; i < mLDDRings; ++i)
            {
                double r_in = mLDDDistance[^1];
                mLDDDistance.Add(mLDDDistance[^1] + (r_max - r_min) / (float)(mLDDRings));
                double r_out = mLDDDistance[^1];
                // calculate the value of the kernel for the middle of the ring
                double ring_in = Treemig(r_in); // kernel value at the inner border of the ring
                double ring_out = Treemig(r_out); // kernel value at the outer border of the ring
                double ring_val = ring_in * 0.4 + ring_out * 0.6; // this is the average p -- 0.4/0.6 better estimate the nonlinear behavior (fits very well for medium to large kernels, e.g. piab)
                                                                  // calculate the area of the ring
                double ring_area = (r_out * r_out - r_in * r_in) * Math.PI; // in square meters
                                                                            // the number of px considers the fecundity
                double n_px = ring_val * ring_area * Species.FecundityM2 / mLDDSeedlings;
                ldd_sum += ring_val * ring_area; // this fraction of the full kernel (=1) is distributed in theis ring

                mLDDDensity.Add(n_px);
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
        private double Treemig(double distance)
        {
            double p1 = (1.0 - mTM_ks) * Math.Exp(-distance / mTM_as1) / mTM_as1;
            double p2 = 0.0;
            if (mTM_as2 > 0.0)
            {
                p2 = mTM_ks * Math.Exp(-distance / mTM_as2) / mTM_as2;
            }
            double s = p1 + p2;
            // 's' is the density for radius 'distance' - not for specific point with that distance.
            // (i.e. the integral over the one-dimensional treemig function is 1, but if applied for 2d cells, the
            // sum would be much larger as all seeds arriving at 'distance' would be arriving somewhere at the circle with radius 'distance')
            // convert that to a density at a point, by dividing with the circumference at the circle with radius 'distance'
            s /= (2.0 * Math.Max(distance, 0.01) * Math.PI);

            return s;
        }

        private double TreemigCenterCell(double max_distance)
        {
            // use 100 steps and calculate dispersal kernel for consecutive rings
            double sum = 0.0;
            for (int i = 0; i < 100; i++)
            {
                double r_in = i * max_distance / 100.0;
                double r_out = (i + 1) * max_distance / 100.0;
                double ring_area = (r_out * r_out - r_in * r_in) * Math.PI;
                // the value of each ring is: treemig(r) * area of the ring
                sum += Treemig((r_out + r_in) / 2.0) * ring_area;
            }
            return sum;
        }

        /// calculate the distance where the probability falls below 'value'
        private double TreemigDistanceforProbability(double probability)
        {
            double dist = 0.0;
            while (Treemig(dist) > probability && dist < 10000.0)
            {
                dist += 10;
            }
            return dist;
        }

        public void SetupExternalSeedsForSpecies(Species species)
        {
            if (!mExtSeedData.ContainsKey(species.ID))
            {
                return; // nothing to do
            }
            Debug.WriteLine("setting up external seed map for " + species.ID);
            List<double> pcts = mExtSeedData[species.ID];
            mExternalSeedMap.Setup(SeedMap);
            mExternalSeedMap.Initialize(0.0F);
            for (int sector_x = 0; sector_x < mExtSeedSizeX; ++sector_x)
                for (int sector_y = 0; sector_y < mExtSeedSizeY; ++sector_y)
                {
                    int xmin, xmax, ymin, ymax;
                    int fx = mExternalSeedMap.CellsX / mExtSeedSizeX; // number of cells per sector
                    xmin = sector_x * fx;
                    xmax = (sector_x + 1) * fx;
                    fx = mExternalSeedMap.CellsY / mExtSeedSizeY; // number of cells per sector
                    ymin = sector_y * fx;
                    ymax = (sector_y + 1) * fx;
                    // now loop over the whole sector
                    int index = sector_y * mExtSeedSizeX + sector_x;
                    double p = pcts[index];
                    for (int y = ymin; y < ymax; ++y)
                        for (int x = xmin; x < xmax; ++x)
                        {
                            // check
                            if (mExternalSeedBaseMap[x, y] == 2.0F)
                            {
                                if (RandomGenerator.Random() < p)
                                {
                                    mExternalSeedMap[x, y] = 1.0F; // flag
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
            SeedMap.Clear();
            Grid.LoadGridFromImage(fileName, SeedMap);
            for (int p = 0; p != SeedMap.Count; ++p)
            {
                SeedMap[p] = SeedMap[p] > 0.8 ? 1.0F : 0.0F;
            }
        }

        public void Clear(GlobalSettings globalSettings)
        {
            Grid<float> seed_map = SeedMap;
            if (!mProbMode)
            {
                seed_map = mSourceMap;
                SeedMap.Initialize(0.0F);
            }
            if (!mExternalSeedMap.IsEmpty())
            {
                // we have a preprocessed initial value for the external seed map (see setupExternalSeeds() et al)
                seed_map.CopyFrom(mExternalSeedMap);
                return;
            }
            // clear the map
            float background_value = (float)mExternalSeedBackgroundInput; // there is potentitally a background probability <>0 for all pixels.
            seed_map.Initialize(background_value);
            if (mHasExternalSeedInput)
            {
                // if external seed input is enabled, the buffer area of the seed maps is
                // "turned on", i.e. set to 1.
                int buf_size = globalSettings.Settings.ValueInt("model.world.buffer", 0) / (int)(seed_map.CellSize);
                // if a special buffer is defined, reduce the size of the input
                if (mExternalSeedBuffer > 0)
                {
                    buf_size -= mExternalSeedBuffer;
                }
                if (buf_size > 0)
                {
                    int ix, iy;
                    for (iy = 0; iy < seed_map.CellsY; ++iy)
                    {
                        for (ix = 0; ix < seed_map.CellsX; ++ix)
                        {
                            if (iy < buf_size || iy >= seed_map.CellsY - buf_size || ix < buf_size || ix >= seed_map.CellsX - buf_size)
                            {
                                if (mExternalSeedDirection == 0)
                                {
                                    // seeds from all directions
                                    seed_map[ix, iy] = 1.0F;
                                }
                                else
                                {
                                    // seeds only from specific directions
                                    float value = 0.0F;
                                    if (Global.IsBitSet(mExternalSeedDirection, 1) && ix >= seed_map.CellsX - buf_size) value = 1; // north
                                    if (Global.IsBitSet(mExternalSeedDirection, 2) && iy < buf_size) value = 1; // east
                                    if (Global.IsBitSet(mExternalSeedDirection, 3) && ix < buf_size) value = 1; // south
                                    if (Global.IsBitSet(mExternalSeedDirection, 4) && iy >= seed_map.CellsY - buf_size) value = 1; // west
                                    seed_map[ix, iy] = value;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("external seed input: Error: invalid buffer size???");
                }
            }
        }

        public void Execute(Model model)
        {
            if (mDumpSeedMaps)
            {
                Debug.WriteLine("saving of seedmaps only supported in the iLand GUI.");
            }
            if (mProbMode)
            {
                using DebugTimer t = new DebugTimer("SeedDispersal.Execute()");

                // (1) detect edges
                if (EdgeDetection())
                {
                    // (2) distribute seed probabilites from edges
                    Distribute();
                }

                // special case serotiny
                if (mHasPendingSerotiny)
                {
                    Debug.WriteLine("calculating extra seed rain (serotiny)....");
                    if (EdgeDetection(mSeedMapSerotiny))
                    {
                        Distribute(mSeedMapSerotiny);
                    }
                    // copy back data
                    for (int p = 0; p < SeedMap.Count; ++p)
                    {
                        SeedMap[p] = Math.Max(SeedMap[p], mSeedMapSerotiny[p]);
                    }

                    float total = mSeedMapSerotiny.Sum();
                    mSeedMapSerotiny.Initialize(0.0F); // clear
                    mHasPendingSerotiny = false;
                    Debug.WriteLine("serotiny event: extra seed input " + total + " (total sum of seed probability over all pixels of the serotiny seed map) of species " + Species.Name);
                }
            }
            else
            {
                // distribute actual values
                using DebugTimer t = new DebugTimer("SeedDispersal.DistributeSeeds()");
                // fill seed map from source map
                DistributeSeeds(model);
            }
        }

        /** scans the seed image and detects "edges".
            edges are then subsequently marked (set to -1). This is pass 1 of the seed distribution process.
            */
        public bool EdgeDetection(Grid<float> seed_map = null)
        {
            Grid<float> seedmap = seed_map == null ? seed_map : SeedMap; // switch to extra seed map if provided
            int dy = seedmap.CellsY;
            int dx = seedmap.CellsX;
            bool found = false;

            // fill mini-gaps
            int n_gaps_filled = 0;
            for (int y = 1; y < dy - 1; ++y)
            {
                int p = seedmap.IndexOf(1, y);
                int p_above = p - dx; // one line above
                int p_below = p + dx; // one line below
                for (int x = 1; x < dx - 1; ++x, ++p, ++p_below, ++p_above)
                {
                    if (seedmap[p] < 0.999F)
                    {
                        if ((seedmap[p_above - 1] == 1.0F ? 1 : 0) + (seedmap[p_above] == 1.0F ? 1 : 0) + (seedmap[p_above + 1] == 1.0F ? 1 : 0) +
                            (seedmap[p - 1] == 1.0F ? 1 : 0) + (seedmap[p + 1] == 1.0F ? 1 : 0) +
                            (seedmap[p_below - 1] == 1.0F ? 1 : 0) + (seedmap[p_below] == 1.0F ? 1 : 0) + (seedmap[p_below + 1] == 1.0F ? 1 : 0) > 3)
                        {
                            seedmap[p] = 0.999F; // if more than 3 neighbors are active pixels, the value is high
                            ++n_gaps_filled;
                        }
                    }
                }
            }

            // now detect the edges
            int n_edges = 0;
            for (int y = 1; y < dy - 1; ++y)
            {
                int p = seedmap.IndexOf(1, y);
                int p_above = p - dx; // one line above
                int p_below = p + dx; // one line below
                for (int x = 1; x < dx - 1; ++x, ++p, ++p_below, ++p_above)
                {
                    if (seedmap[p] == 1.0F)
                    {
                        found = true;
                        if ((seedmap[p_above - 1] < 0.999F && seedmap[p_above - 1] >= 0.0F) ||
                             (seedmap[p_above] < 0.999F && seedmap[p_above] >= 0.0F) ||
                             (seedmap[p_above + 1] < 0.999F && seedmap[p_above + 1] >= 0.0F) ||
                             (seedmap[p - 1] < 0.999F && seedmap[p - 1] >= 0.0F) ||
                             (seedmap[p + 1] < 0.999F && seedmap[p + 1] >= 0.0F) ||
                             (seedmap[p_below - 1] < 0.999F && seedmap[p_below - 1] >= 0.0F) ||
                             (seedmap[p_below] < 0.999F && seedmap[p_below] >= 0.0F) ||
                             (seedmap[p_below + 1] < 0.999F && seedmap[p_below + 1] >= 0.0F))
                        {
                            seedmap[p] = -1.0F; // if any surrounding pixel is >=0 & <0.999: . mark as edge
                            ++n_edges;
                        }
                    }

                }
            }
            if (mDumpSeedMaps)
            {
                Debug.WriteLine("species: " + Species.ID + " # of gaps filled: " + n_gaps_filled + " # of edge-pixels: " + n_edges);
            }
            return found;
        }

        /** do the seed probability distribution.
            This is phase 2. Apply the seed kernel for each "edge" point identified in phase 1.
            */
        public void Distribute(Grid<float> seed_map = null)
        {
            int x, y;
            Grid<float> seedmap = seed_map ?? SeedMap; // switch to extra seed map if provided
            // choose the kernel depending whether there is a seed year for the current species or not
            Grid<float> kernel = Species.IsSeedYear ? mKernelSeedYear : mKernelNonSeedYear;
            // extra case: serotiny
            if (seed_map != null)
            {
                kernel = mKernelSerotiny;
            }

            int offset = kernel.CellsX / 2; // offset is the index of the center pixel
            for (int p = 0; p < seedmap.Count; ++p)
            {
                if (seedmap[p] == -1.0F)
                {
                    // edge pixel found. Now apply the kernel....
                    Point pt = seedmap.IndexOf(p);
                    for (y = -offset; y <= offset; ++y)
                    {
                        for (x = -offset; x <= offset; ++x)
                        {
                            float kernel_value = kernel[x + offset, y + offset];
                            if (kernel_value > 0.0F && seedmap.Contains(pt.X + x, pt.Y + y))
                            {
                                float val = seedmap[pt.X + x, pt.Y + y];
                                if (val != -1.0F)
                                {
                                    seedmap[pt.X + x, pt.Y + y] = Math.Min(1.0F - (1.0F - val) * (1.0F - kernel_value), 1.0F);
                                }
                            }
                        }
                    }
                    // long distance dispersal
                    if (mLDDDensity.Count != 0)
                    {
                        double m = Species.IsSeedYear ? 1.0 : mNonSeedYearFraction;
                        for (int r = 0; r < mLDDDensity.Count; ++r)
                        {
                            float ldd_val = mLDDSeedlings; // pixels will have this probability
                            int n = (int)Math.Round(mLDDDensity[r] * m); // number of pixels to activate
                            for (int i = 0; i < n; ++i)
                            {
                                // distance and direction:
                                double radius = RandomGenerator.Random(mLDDDistance[r], mLDDDistance[r + 1]) / seedmap.CellSize; // choose a random distance (in pixels)
                                double phi = RandomGenerator.Random() * 2.0 * Math.PI; // choose a random direction
                                Point ldd = new Point((int)(pt.X + radius * Math.Cos(phi)), (int)(pt.Y + radius * Math.Sin(phi)));
                                if (seedmap.Contains(ldd))
                                {
                                    float val = seedmap[ldd];
                                    _debug_ldd++;
                                    // use the same adding of probabilities
                                    if (val != -1.0F)
                                    {
                                        seedmap[ldd] = Math.Min(1.0F - (1.0F - val) * (1.0F - ldd_val), 1.0F);
                                    }
                                }
                            }
                        }
                    }
                    seedmap[p] = 1.0F; // mark as processed
                } // if (seedmap[p]==1)
            } // for()
        }

        public void DistributeSeeds(Model model, Grid<float> seed_map = null)
        {
            Grid<float> sourcemap = seed_map ?? mSourceMap; // switch to extra seed map if provided
            Grid<float> kernel = mKernelSeedYear;

            // *** estimate seed production (based on leaf area) ***
            // calculate number of seeds; the source map holds now m2 leaf area on 20x20m pixels
            // after this step, each source cell has a value between 0 (no source) and 1 (fully covered cell)
            float fec = (float)Species.FecundityM2;
            if (!Species.IsSeedYear)
            {
                fec *= (float)mNonSeedYearFraction;
            }
            for (int p = 0; p < sourcemap.Count; ++p)
            {
                if (sourcemap[p] != 0.0F)
                {
                    // if LAI  >3, then full potential is assumed, below LAI=3 a linear ramp is used
                    sourcemap[p] = Math.Min(sourcemap[p] / (sourcemap.CellSize * sourcemap.CellSize) / 3.0F, 3.0F);
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

            int offset = kernel.CellsX / 2; // offset is the index of the center pixel
                                             // source mode

            // *** seed distribution (Kernel + long distance dispersal) ***
            if (model.ModelSettings.TorusMode == false)
            {
                // ** standard case (no torus) **
                for (int src = 0; src < sourcemap.Count; ++src)
                {
                    if (sourcemap[src] > 0.0F)
                    {
                        Point sm = sourcemap.IndexOf(src).Subtract(offset);
                        int sx = sm.X, sy = sm.Y;
                        for (int iy = 0; iy < kernel.CellsY; ++iy)
                        {
                            for (int ix = 0; ix < kernel.CellsX; ++ix)
                            {
                                if (SeedMap.Contains(sx + ix, sy + iy))
                                {
                                    SeedMap[sx + ix, sy + iy] += sourcemap[src] * kernel[ix, iy];
                                }
                            }
                        }
                        // long distance dispersal
                        if (mLDDDensity.Count != 0)
                        {
                            Point pt = sourcemap.IndexOf(src);

                            for (int r = 0; r < mLDDDensity.Count; ++r)
                            {
                                float ldd_val = mLDDSeedlings / fec; // pixels will have this probability [note: fecundity will be multiplied below]
                                int n;
                                if (mLDDDensity[r] < 1)
                                {
                                    n = RandomGenerator.Random() < mLDDDensity[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)Math.Round(mLDDDensity[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    double radius = RandomGenerator.Random(mLDDDistance[r], mLDDDistance[r + 1]) / SeedMap.CellSize; // choose a random distance (in pixels)
                                    double phi = RandomGenerator.Random() * 2.0 * Math.PI; // choose a random direction
                                    Point ldd = new Point(pt.X + (int)(radius * Math.Cos(phi)), pt.Y + (int)(radius * Math.Sin(phi)));
                                    if (SeedMap.Contains(ldd))
                                    {
                                        SeedMap[ldd] += ldd_val;
                                        _debug_ldd++;
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
                int seedmap_offset = sourcemap.IndexAt(new PointF(0.0F, 0.0F)).X; // the seed maps have x extra rows/columns
                Point torus_pos;
                int seedpx_per_ru = (int)(Constant.RUSize / sourcemap.CellSize);
                for (int src = 0; src < sourcemap.Count; ++src)
                {
                    if (sourcemap[src] > 0.0F)
                    {
                        Point sm = sourcemap.IndexOf(src);
                        // get the origin of the resource unit *on* the seedmap in *seedmap-coords*:
                        Point offset_ru = new Point(((sm.X - seedmap_offset) / seedpx_per_ru) * seedpx_per_ru + seedmap_offset,
                                 ((sm.Y - seedmap_offset) / seedpx_per_ru) * seedpx_per_ru + seedmap_offset);  // coords RU origin

                        Point offset_in_ru = new Point((sm.X - seedmap_offset) % seedpx_per_ru, (sm.Y - seedmap_offset) % seedpx_per_ru);  // offset of current point within the RU

                        //Point sm=sourcemap.indexOf(src)-Point(offset, offset);
                        for (int iy = 0; iy < kernel.CellsY; ++iy)
                        {
                            for (int ix = 0; ix < kernel.CellsX; ++ix)
                            {
                                torus_pos = offset_ru.Add(new Point(Global.Modulo((offset_in_ru.X - offset + ix), seedpx_per_ru), Global.Modulo((offset_in_ru.Y - offset + iy), seedpx_per_ru)));

                                if (SeedMap.Contains(torus_pos))
                                {
                                    SeedMap[torus_pos] += sourcemap[src] * kernel[ix, iy];
                                }
                            }
                        }
                        // long distance dispersal
                        if (mLDDDensity.Count != 0)
                        {
                            for (int r = 0; r < mLDDDensity.Count; ++r)
                            {
                                float ldd_val = mLDDSeedlings / fec; // pixels will have this probability [note: fecundity will be multiplied below]
                                int n;
                                if (mLDDDensity[r] < 1)
                                {
                                    n = RandomGenerator.Random() < mLDDDensity[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)Math.Round(mLDDDensity[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    double radius = RandomGenerator.Random(mLDDDistance[r], mLDDDistance[r + 1]) / SeedMap.CellSize; // choose a random distance (in pixels)
                                    double phi = RandomGenerator.Random() * 2.0 * Math.PI; // choose a random direction
                                    Point ldd = new Point((int)(radius * Math.Cos(phi)), (int)(radius * Math.Sin(phi))); // destination (offset)
                                    torus_pos = offset_ru.Add(new Point(Global.Modulo((offset_in_ru.X + ldd.X), seedpx_per_ru), Global.Modulo((offset_in_ru.Y + ldd.Y), seedpx_per_ru)));

                                    if (SeedMap.Contains(torus_pos))
                                    {
                                        SeedMap[torus_pos] += ldd_val;
                                        _debug_ldd++;
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
            const float n_unlimited = 100.0F;
            for (int p = 0; p < SeedMap.Count; ++p)
            {
                if (SeedMap[p] > 0.0F)
                {
                    SeedMap[p] = Math.Min(SeedMap[p] * fec / n_unlimited, 1.0F);
                }
            }
        }
    }
}
