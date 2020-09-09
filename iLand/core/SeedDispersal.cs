using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.core
{
    /** @class SeedDispersal
        @ingroup core
        The class encapsulates the dispersal of seeds of one species over the whole landscape.
        The dispersal algortihm operate on grids with a 20m resolution.

        See http://iland.boku.ac.at/dispersal

        */
    internal class SeedDispersal
    {
        private static Grid<float> mExternalSeedBaseMap = null; ///< static intermediate data while setting up external seeds
        private static Dictionary<string, List<double>> mExtSeedData; ///< holds definition of species and percentages for external seed input
        private static int mExtSeedSizeX = 0; ///< size of the sectors used to specify external seed input
        private static int mExtSeedSizeY = 0;
        private static int _debug_ldd = 0;

        private bool mProbMode; ///< if 'true', seed dispersal uses probabilities to distribute (old version)
        private double mTM_as1, mTM_as2, mTM_ks; ///< seed dispersal paramaters (treemig)
        private double mTM_fecundity_cell; ///< maximum seeds per source cell
        private double mTM_occupancy; ///< seeds required per destination regeneration pixel
        private double mNonSeedYearFraction; ///< fraction of the seed production in non-seed-years
        private double mKernelThresholdArea, mKernelThresholdLDD; ///< value of the kernel function that is the threhold for full coverage and LDD, respectively
        private int mIndexFactor; ///< multiplier between light-pixel-size and seed-pixel-size
        private Grid<float> mSeedMap; ///< (large) seedmap. Is filled by individual trees and then processed
        private Grid<float> mSourceMap; ///< (large) seedmap used to denote the sources
        private Grid<float> mKernelSeedYear; ///< species specific "seed kernel" (small) for seed years
        private Grid<float> mKernelNonSeedYear; ///< species specific "seed kernel" (small) for non-seed-years
        private Grid<float> mKernelSerotiny; ///< seed kernel for extra seed rain
        private Grid<float> mSeedMapSerotiny; ///< seed map that keeps track of serotiny events
        private List<double> mLDDDistance; ///< long distance dispersal distances (e.g. the "rings")
        private List<double> mLDDDensity;  ///< long distance dispersal # of cells that should be affected in each "ring"
        private int mLDDRings; ///< # of rings (with equal probability) for LDD
        private float mLDDSeedlings; ///< each LDD pixel has this probability
        private bool mHasPendingSerotiny; ///< true if active (unprocessed) pixels are on the extra-serotiny map
        private bool mSetup;
        private Species mSpecies;
        private bool mDumpSeedMaps; ///< if true, seedmaps are stored as images
        private bool mHasExternalSeedInput; ///< if true, external seeds are modelled for the species
        private string mDumpNextYearFileName; ///< debug output - dump of the content of the grid to a file during the next execution
        private int mExternalSeedDirection; ///< direction of external seeds
        private int mExternalSeedBuffer; ///< how many 20m pixels away from the simulation area should the seeding start?
        private double mExternalSeedBackgroundInput; ///< background propability for this species; if set, then a certain seed availability is provided for the full area
        // external seeds
        private Grid<float> mExternalSeedMap; ///< for more complex external seed input, this map holds that information

        public Grid<float> seedMap() { return mSeedMap; } ///< access to the seedMap
        public Species species() { return mSpecies; }

        public SeedDispersal(Species species = null)
        {
            mIndexFactor = 10;
            mSetup = false;
            mSpecies = species;

            mSeedMap = new Grid<float>(); ///< (large) seedmap. Is filled by individual trees and then processed
            mSourceMap = new Grid<float>(); ///< (large) seedmap used to denote the sources
            mKernelSeedYear = new Grid<float>(); ///< species specific "seed kernel" (small) for seed years
            mKernelNonSeedYear = new Grid<float>(); ///< species specific "seed kernel" (small) for non-seed-years
            mKernelSerotiny = new Grid<float>(); ///< seed kernel for extra seed rain
            mSeedMapSerotiny = new Grid<float>(); ///< seed map that keeps track of serotiny events
            mLDDDistance = new List<double>(); ///< long distance dispersal distances (e.g. the "rings")
            mLDDDensity = new List<double>();  ///< long distance dispersal # of cells that should be affected in each "ring"
        }

        public void dumpMapNextYear(string file_name) { mDumpNextYearFileName = file_name; }

        /// setMatureTree is called by individual (mature) trees. This actually fills the initial state of the seed map.
        public void setMatureTree(Point lip_index, double leaf_area)
        {
            if (mProbMode)
            {
                mSeedMap[lip_index.X / mIndexFactor, lip_index.Y / mIndexFactor] = 1.0F;
            }
            else
            {
                mSourceMap[lip_index.X / mIndexFactor, lip_index.Y / mIndexFactor] += (float)leaf_area;
            }
        }

        /** setup of the seedmaps.
          This sets the size of the seed map and creates the seed kernel (species specific)
          */
        public void setup()
        {
            if (GlobalSettings.instance().model() != null || GlobalSettings.instance().model().heightGrid() != null || mSpecies != null)
            {
                return;
            }
            mProbMode = false;

            float seedmap_size = 20.0F;
            // setup of seed map
            mSeedMap.clear();
            mSeedMap.setup(GlobalSettings.instance().model().heightGrid().metricRect(), seedmap_size);
            mSeedMap.initialize(0.0F);
            if (!mProbMode)
            {
                mSourceMap.setup(mSeedMap);
                mSourceMap.initialize(0.0F);
            }
            mExternalSeedMap.clear();
            mIndexFactor = (int)(seedmap_size / Constant.cPxSize); // ratio seed grid / lip-grid:
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("Seed map setup. Species: " + mSpecies.id() + " kernel-size: " + mSeedMap.sizeX() + " x " + mSeedMap.sizeY() + " pixels.");
            }

            if (mSpecies == null)
            {
                throw new NotSupportedException("Setup of SeedDispersal: Species not defined.");
            }

            if ((GlobalSettings.instance().settings().valueDouble("model.world.buffer", 0) % seedmap_size) != 0.0)
            {
                throw new NotSupportedException("SeedDispersal:setup(): The buffer (model.world.buffer) must be a integer multiple of the seed pixel size (currently 20m, e.g. 20,40,60,...)).");
            }

            // settings
            mTM_occupancy = 1.0; // is currently constant
                                 // copy values for the species parameters:
            mSpecies.treeMigKernel(ref mTM_as1, ref mTM_as2, ref mTM_ks);
            mTM_fecundity_cell = mSpecies.fecundity_m2() * seedmap_size * seedmap_size * mTM_occupancy; // scale to production for the whole cell
            mNonSeedYearFraction = mSpecies.nonSeedYearFraction();
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.settings.seedDispersal"));
            mKernelThresholdArea = xml.valueDouble(".longDistanceDispersal.thresholdArea", 0.0001);
            mKernelThresholdLDD = xml.valueDouble(".longDistanceDispersal.thresholdLDD", 0.0001);
            mLDDSeedlings = (float)xml.valueDouble(".longDistanceDispersal.LDDSeedlings", 0.0001);
            mLDDRings = xml.valueInt(".longDistanceDispersal.rings", 4);

            mLDDSeedlings = MathF.Max(mLDDSeedlings, (float)mKernelThresholdArea);

            // long distance dispersal
            double ldd_area = setupLDD();

            createKernel(mKernelSeedYear, mTM_fecundity_cell, 1.0 - ldd_area);

            // the kernel for non seed years looks similar, but is simply linearly scaled down
            // using the species parameter NonSeedYearFraction.
            // the central pixel still gets the value of 1 (i.e. 100% probability)
            createKernel(mKernelNonSeedYear, mTM_fecundity_cell * mNonSeedYearFraction, 1.0 - ldd_area);

            if (mSpecies.fecunditySerotiny() > 0.0)
            {
                // an extra seed map is used for storing information related to post-fire seed rain
                mSeedMapSerotiny.clear();
                mSeedMapSerotiny.setup(GlobalSettings.instance().model().heightGrid().metricRect(), seedmap_size);
                mSeedMapSerotiny.initialize(0.0F);

                // set up the special seed kernel for post fire seed rain
                createKernel(mKernelSerotiny, mTM_fecundity_cell * mSpecies.fecunditySerotiny(), 1.0);
                Debug.WriteLine("created extra seed map and serotiny seed kernel for species " + mSpecies.name() + " with fecundity factor " + mSpecies.fecunditySerotiny());
            }
            mHasPendingSerotiny = false;

            // debug info
            mDumpSeedMaps = GlobalSettings.instance().settings().valueBool("model.settings.seedDispersal.dumpSeedMapsEnabled", false);
            if (mDumpSeedMaps)
            {
                string path = GlobalSettings.instance().path(GlobalSettings.instance().settings().value("model.settings.seedDispersal.dumpSeedMapsPath"));
                Helper.saveToTextFile(String.Format("{0}/seedkernelYes_{1}.csv", path, mSpecies.id()), Grid.gridToString(mKernelSeedYear));
                Helper.saveToTextFile(String.Format("{0}/seedkernelNo_{1}.csv", path, mSpecies.id()), Grid.gridToString(mKernelNonSeedYear));
                if (!mKernelSerotiny.isEmpty())
                {
                    Helper.saveToTextFile(String.Format("{0}/seedkernelSerotiny_{1}.csv", path, mSpecies.id()), Grid.gridToString(mKernelSerotiny));
                }
            }

            // external seeds
            mHasExternalSeedInput = false;
            mExternalSeedBuffer = 0;
            mExternalSeedDirection = 0;
            mExternalSeedBackgroundInput = 0.0;
            if (GlobalSettings.instance().settings().valueBool("model.settings.seedDispersal.externalSeedEnabled", false))
            {
                if (GlobalSettings.instance().settings().valueBool("model.settings.seedDispersal.seedBelt.enabled", false))
                {
                    // external seed input specified by sectors and around the project area (seedbelt)
                    setupExternalSeedsForSpecies(mSpecies);
                }
                else
                {
                    // external seeds specified fixedly per cardinal direction
                    // current species in list??
                    mHasExternalSeedInput = GlobalSettings.instance().settings().value("model.settings.seedDispersal.externalSeedSpecies").Contains(mSpecies.id());
                    string dir = GlobalSettings.instance().settings().value("model.settings.seedDispersal.externalSeedSource").ToLowerInvariant();
                    // encode cardinal positions as bits: e.g: "e,w" . 6
                    mExternalSeedDirection += dir.Contains("n") ? 1 : 0;
                    mExternalSeedDirection += dir.Contains("e") ? 2 : 0;
                    mExternalSeedDirection += dir.Contains("s") ? 4 : 0;
                    mExternalSeedDirection += dir.Contains("w") ? 8 : 0;
                    List<string> buffer_list = Regex.Matches(GlobalSettings.instance().settings().value("model.settings.seedDispersal.externalSeedBuffer"), "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    int index = buffer_list.IndexOf(mSpecies.id());
                    if (index >= 0)
                    {
                        mExternalSeedBuffer = Int32.Parse(buffer_list[index + 1]);
                        Debug.WriteLine("enabled special buffer for species " + mSpecies.id() + ": distance of " + mExternalSeedBuffer + " pixels = " + mExternalSeedBuffer * 20.0 + " m");
                    }

                    // background seed rain (i.e. for the full landscape), use regexp
                    List<string> background_input_list = Regex.Matches(GlobalSettings.instance().settings().value("model.settings.seedDispersal.externalSeedBackgroundInput"), "([^\\.\\w]+)").Select(match => match.Value).ToList();
                    index = background_input_list.IndexOf(mSpecies.id());
                    if (index >= 0)
                    {
                        mExternalSeedBackgroundInput = Double.Parse(background_input_list[index + 1]);
                        Debug.WriteLine("enabled background seed input (for full area) for species " + mSpecies.id() + ": p=" + mExternalSeedBackgroundInput);
                    }

                    if (mHasExternalSeedInput)
                    {
                        Debug.WriteLine("External seed input enabled for " + mSpecies.id());
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
            //    mSeedMap.valueAtIndex(mSeedMap.randomPosition()) = 1.0;


            //    QImage img = gridToImage(mSeedMap, true, -1., 1.0);
            //    img.save("seedmap.png");

            //    img = gridToImage(mSeedMap, true, -1., 1.0);
            //    img.save("seedmap_e.png");
        }

        public static void setupExternalSeeds()
        {
            mExternalSeedBaseMap = null;
            if (!GlobalSettings.instance().settings().valueBool("model.settings.seedDispersal.seedBelt.enabled", false))
            {
                return;
            }

            using DebugTimer t = new DebugTimer("setup of external seed maps.");
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.settings.seedDispersal.seedBelt"));
            int seedbelt_width = xml.valueInt(".width", 10);
            // setup of sectors
            // setup of base map
            float seedmap_size = 20.0F;
            mExternalSeedBaseMap = new Grid<float>();
            mExternalSeedBaseMap.setup(GlobalSettings.instance().model().heightGrid().metricRect(), seedmap_size);
            mExternalSeedBaseMap.initialize(0.0F);
            if (mExternalSeedBaseMap.count() * 4 != GlobalSettings.instance().model().heightGrid().count())
            {
                throw new NotSupportedException("error in setting up external seeds: the width and height of the project area need to be a multiple of 20m when external seeds are enabled.");
            }
            // make a copy of the 10m height grid in lower resolution and mark pixels that are forested and outside of
            // the project area.
            for (int y = 0; y < mExternalSeedBaseMap.sizeY(); y++)
            {
                for (int x = 0; x < mExternalSeedBaseMap.sizeX(); x++)
                {
                    bool val = GlobalSettings.instance().model().heightGrid().valueAtIndex(x * 2, y * 2).isForestOutside();
                    mExternalSeedBaseMap[x, y] = val ? 1.0F : 0.0F;
                    if (GlobalSettings.instance().model().heightGrid().valueAtIndex(x * 2, y * 2).isValid())
                    {
                        mExternalSeedBaseMap[x, y] = -1.0F;
                    }
                }
            }
            string path = GlobalSettings.instance().path(GlobalSettings.instance().settings().value("model.settings.seedDispersal.dumpSeedMapsPath"));

            // now scan the pixels of the belt: paint all pixels that are close to the project area
            // we do this 4 times (for all cardinal direcitons)
            for (int y = 0; y < mExternalSeedBaseMap.sizeY(); y++)
            {
                for (int x = 0; x < mExternalSeedBaseMap.sizeX(); x++)
                {
                    if (mExternalSeedBaseMap.valueAtIndex(x, y) != 1.0)
                    {
                        continue;
                    }

                    int look_forward = Math.Min(x + seedbelt_width, mExternalSeedBaseMap.sizeX() - 1);
                    if (mExternalSeedBaseMap.valueAtIndex(look_forward, y) == -1.0F)
                    {
                        // fill pixels
                        for (; x < look_forward; ++x)
                        {
                            float v = mExternalSeedBaseMap.valueAtIndex(x, y);
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }
            // right to left
            for (int y = 0; y < mExternalSeedBaseMap.sizeY(); y++)
            {
                for (int x = mExternalSeedBaseMap.sizeX(); x >= 0; --x)
                {
                    if (mExternalSeedBaseMap.valueAtIndex(x, y) != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Max(x - seedbelt_width, 0);
                    if (mExternalSeedBaseMap.valueAtIndex(look_forward, y) == -1.0F)
                    {
                        // fill pixels
                        for (; x > look_forward; --x)
                        {
                            float v = mExternalSeedBaseMap.valueAtIndex(x, y);
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
            for (int x = 0; x < mExternalSeedBaseMap.sizeX(); x++)
            {
                for (int y = 0; y < mExternalSeedBaseMap.sizeY(); y++)
                {
                    if (mExternalSeedBaseMap.valueAtIndex(x, y) != 1.0)
                    {
                        continue;
                    }
                    int look_forward = Math.Min(y + seedbelt_width, mExternalSeedBaseMap.sizeY() - 1);
                    if (mExternalSeedBaseMap.valueAtIndex(x, look_forward) == -1.0)
                    {
                        // fill pixels
                        for (; y < look_forward; ++y)
                        {
                            float v = mExternalSeedBaseMap.valueAtIndex(x, y);
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }
            // bottom to top ***
            for (int y = 0; y < mExternalSeedBaseMap.sizeY(); y++)
            {
                for (int x = mExternalSeedBaseMap.sizeX(); x >= 0; --x)
                {
                    if (mExternalSeedBaseMap.valueAtIndex(x, y) != 1.0)
                        continue;
                    int look_forward = Math.Max(y - seedbelt_width, 0);
                    if (mExternalSeedBaseMap.valueAtIndex(x, look_forward) == -1.0)
                    {
                        // fill pixels
                        for (; y > look_forward; --y)
                        {
                            float v = mExternalSeedBaseMap.valueAtIndex(x, y);
                            if (v == 1.0F)
                            {
                                mExternalSeedBaseMap[x, y] = 2.0F;
                            }
                        }
                    }
                }
            }

            mExtSeedData.Clear();
            int sectors_x = xml.valueInt("sizeX", 0);
            int sectors_y = xml.valueInt("sizeY", 0);
            if (sectors_x < 1 || sectors_y < 1)
            {
                throw new NotSupportedException(String.Format("setup of external seed dispersal: invalid number of sectors x={0} y=%3", sectors_x, sectors_y));
            }

            XmlNode elem = xml.node(".");
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

                    string text = xml.value("." + n.Name);
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

        public static void finalizeExternalSeeds()
        {
            mExternalSeedBaseMap = null;
        }

        public void seedProductionSerotiny(Point position_index)
        {
            if (mSeedMapSerotiny.isEmpty())
            {
                throw new NotSupportedException("Invalid use seedProductionSerotiny(): tried to set a seed source for a non-serotinous species!");
            }
            mSeedMapSerotiny[position_index.X / mIndexFactor, position_index.Y / mIndexFactor] = 1.0F;
            mHasPendingSerotiny = true;
        }

        public void createKernel(Grid<float> kernel, double max_seed, double scale_area)
        {
            double max_dist = treemig_distanceTo(mKernelThresholdArea / species().fecundity_m2());
            double cell_size = mSeedMap.cellsize();
            int max_radius = (int)(max_dist / cell_size);
            // e.g.: cell_size: regeneration grid (e.g. 400qm), px-size: light-grid (4qm)
            double occupation = cell_size * cell_size / (Constant.cPxSize * Constant.cPxSize * mTM_occupancy);

            kernel.clear();

            kernel.setup(mSeedMap.cellsize(), 2 * max_radius + 1, 2 * max_radius + 1);
            int kernel_offset = max_radius;

            // filling of the kernel.... use the treemig density function
            double dist_center_cell = Math.Sqrt(cell_size * cell_size / Math.PI);
            Point center = new Point(kernel_offset, kernel_offset);
            for (int p = 0; p < kernel.count(); ++p)
            {
                double d = kernel.distance(center, kernel.indexOf(p));
                if (d == 0.0)
                {
                    kernel[p] = (float)treemig_centercell(dist_center_cell); // r is the radius of a circle with the same area as a cell
                }
                else
                {
                    kernel[p] = d <= max_dist ? (float)((treemig(d + dist_center_cell) + treemig(d - dist_center_cell)) / 2.0F * cell_size * cell_size) : 0.0F;
                }
            }

            // normalize
            float sum = kernel.sum();
            if (sum == 0.0 || occupation == 0.0)
            {
                throw new NotSupportedException("create seed kernel: sum of probabilities = 0!");
            }

            // the sum of all kernel cells has to equal 1 (- long distance dispersal)
            kernel.multiply((float)scale_area / sum);

            if (mProbMode)
            {
                // probabilities are derived in multiplying by seed number, and dividing by occupancy criterion
                float fecundity_factor = (float)(max_seed / occupation);
                kernel.multiply(fecundity_factor);
                // all cells that get more seeds than the occupancy criterion are considered to have no seed limitation for regeneration
                for (int p = 0; p < kernel.count(); ++p)
                {
                    // BUGBUG: why isn't this a call to kernel.limit(0, 1)?
                    kernel[p] = Math.Min(kernel[p], 1.0F);
                }
            }
            // set the parent cell to 1
            //kernel.valueAtIndex(kernel_offset, kernel_offset)=1.0F;

            // some final statistics....
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("kernel setup. Species: " + mSpecies.id() + " kernel-size: " + kernel.sizeX() + " x " + kernel.sizeY() + " pixels, sum (after scaling): " + kernel.sum());
            }
        }

        private double setupLDD()
        {
            mLDDDensity.Clear();
            mLDDDistance.Clear();
            if (mKernelThresholdLDD >= mKernelThresholdArea)
            {
                // no long distance dispersal
                return 0.0;

            }
            double r_min = treemig_distanceTo(mKernelThresholdArea / species().fecundity_m2());
            double r_max = treemig_distanceTo(mKernelThresholdLDD / species().fecundity_m2());
            mLDDDistance.Add(r_min);
            double ldd_sum = 0.0;
            for (int i = 0; i < mLDDRings; ++i)
            {
                double r_in = mLDDDistance[^1];
                mLDDDistance.Add(mLDDDistance[^1] + (r_max - r_min) / (float)(mLDDRings));
                double r_out = mLDDDistance[^1];
                // calculate the value of the kernel for the middle of the ring
                double ring_in = treemig(r_in); // kernel value at the inner border of the ring
                double ring_out = treemig(r_out); // kernel value at the outer border of the ring
                double ring_val = ring_in * 0.4 + ring_out * 0.6; // this is the average p -- 0.4/0.6 better estimate the nonlinear behavior (fits very well for medium to large kernels, e.g. piab)
                                                                  // calculate the area of the ring
                double ring_area = (r_out * r_out - r_in * r_in) * Math.PI; // in square meters
                                                                            // the number of px considers the fecundity
                double n_px = ring_val * ring_area * species().fecundity_m2() / mLDDSeedlings;
                ldd_sum += ring_val * ring_area; // this fraction of the full kernel (=1) is distributed in theis ring

                mLDDDensity.Add(n_px);
            }
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("Setup LDD for " + species().name() + ", using probability: " + mLDDSeedlings + ": Distances: " + mLDDDistance + ", seed pixels: " + mLDDDensity + "covered prob: " + ldd_sum);
            }

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
        private double treemig(double distance)
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
            s = s / (2.0 * Math.Max(distance, 0.01) * Math.PI);

            return s;
        }

        private double treemig_centercell(double max_distance)
        {
            // use 100 steps and calculate dispersal kernel for consecutive rings
            double sum = 0.0;
            for (int i = 0; i < 100; i++)
            {
                double r_in = i * max_distance / 100.0;
                double r_out = (i + 1) * max_distance / 100.0;
                double ring_area = (r_out * r_out - r_in * r_in) * Math.PI;
                // the value of each ring is: treemig(r) * area of the ring
                sum += treemig((r_out + r_in) / 2.0) * ring_area;
            }
            return sum;
        }

        /// calculate the distance where the probability falls below 'value'
        private double treemig_distanceTo(double value)
        {
            double dist = 0.0;
            while (treemig(dist) > value && dist < 10000.0)
            {
                dist += 10;
            }
            return dist;
        }

        public void setupExternalSeedsForSpecies(Species species)
        {
            if (!mExtSeedData.ContainsKey(species.id()))
            {
                return; // nothing to do
            }
            Debug.WriteLine("setting up external seed map for " + species.id());
            List<double> pcts = mExtSeedData[species.id()];
            mExternalSeedMap.setup(mSeedMap);
            mExternalSeedMap.initialize(0.0F);
            for (int sector_x = 0; sector_x < mExtSeedSizeX; ++sector_x)
                for (int sector_y = 0; sector_y < mExtSeedSizeY; ++sector_y)
                {
                    int xmin, xmax, ymin, ymax;
                    int fx = mExternalSeedMap.sizeX() / mExtSeedSizeX; // number of cells per sector
                    xmin = sector_x * fx;
                    xmax = (sector_x + 1) * fx;
                    fx = mExternalSeedMap.sizeY() / mExtSeedSizeY; // number of cells per sector
                    ymin = sector_y * fx;
                    ymax = (sector_y + 1) * fx;
                    // now loop over the whole sector
                    int index = sector_y * mExtSeedSizeX + sector_x;
                    double p = pcts[index];
                    for (int y = ymin; y < ymax; ++y)
                        for (int x = xmin; x < xmax; ++x)
                        {
                            // check
                            if (mExternalSeedBaseMap.valueAtIndex(x, y) == 2.0F)
                            {
                                if (RandomGenerator.drandom() < p)
                                {
                                    mExternalSeedMap[x, y] = 1.0F; // flag
                                }
                            }
                        }

                }
            if (!mProbMode)
            {
                // scale external seed values to have pixels with LAI=3
                for (int p = 0; p < mExternalSeedMap.count(); ++p)
                {
                    mExternalSeedMap[p] *= 3.0F * mExternalSeedMap.cellsize() * mExternalSeedMap.cellsize();
                }
            }
        }

        /// debug function: loads a image of arbirtrary size...
        public void loadFromImage(string fileName)
        {
            mSeedMap.clear();
            Grid.loadGridFromImage(fileName, mSeedMap);
            for (int p = 0; p != mSeedMap.count(); ++p)
            {
                mSeedMap[p] = mSeedMap[p] > 0.8 ? 1.0F : 0.0F;
            }
        }

        public void clear()
        {
            Grid<float> seed_map = mSeedMap;
            if (!mProbMode)
            {
                seed_map = mSourceMap;
                mSeedMap.initialize(0.0F);
            }
            if (!mExternalSeedMap.isEmpty())
            {
                // we have a preprocessed initial value for the external seed map (see setupExternalSeeds() et al)
                seed_map.copy(mExternalSeedMap);
                return;
            }
            // clear the map
            float background_value = (float)mExternalSeedBackgroundInput; // there is potentitally a background probability <>0 for all pixels.
            seed_map.initialize(background_value);
            if (mHasExternalSeedInput)
            {
                // if external seed input is enabled, the buffer area of the seed maps is
                // "turned on", i.e. set to 1.
                int buf_size = GlobalSettings.instance().settings().valueInt("model.world.buffer", 0) / (int)(seed_map.cellsize());
                // if a special buffer is defined, reduce the size of the input
                if (mExternalSeedBuffer > 0)
                {
                    buf_size -= mExternalSeedBuffer;
                }
                if (buf_size > 0)
                {
                    int ix, iy;
                    for (iy = 0; iy < seed_map.sizeY(); ++iy)
                    {
                        for (ix = 0; ix < seed_map.sizeX(); ++ix)
                        {
                            if (iy < buf_size || iy >= seed_map.sizeY() - buf_size || ix < buf_size || ix >= seed_map.sizeX() - buf_size)
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
                                    if (Global.isBitSet(mExternalSeedDirection, 1) && ix >= seed_map.sizeX() - buf_size) value = 1; // north
                                    if (Global.isBitSet(mExternalSeedDirection, 2) && iy < buf_size) value = 1; // east
                                    if (Global.isBitSet(mExternalSeedDirection, 3) && ix < buf_size) value = 1; // south
                                    if (Global.isBitSet(mExternalSeedDirection, 4) && iy >= seed_map.sizeY() - buf_size) value = 1; // west
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

        public void execute()
        {
            if (mDumpSeedMaps)
            {
                Debug.WriteLine("saving of seedmaps only supported in the iLand GUI.");
            }
            if (mProbMode)
            {
                using DebugTimer t = new DebugTimer("seed dispersal", true);

                // (1) detect edges
                if (edgeDetection())
                {
                    // (2) distribute seed probabilites from edges
                    distribute();
                }

                // special case serotiny
                if (mHasPendingSerotiny)
                {
                    Debug.WriteLine("calculating extra seed rain (serotiny)....");
                    if (edgeDetection(mSeedMapSerotiny))
                    {
                        distribute(mSeedMapSerotiny);
                    }
                    // copy back data
                    for (int p = 0; p < mSeedMap.count(); ++p)
                    {
                        mSeedMap[p] = Math.Max(mSeedMap[p], mSeedMapSerotiny[p]);
                    }

                    float total = mSeedMapSerotiny.sum();
                    mSeedMapSerotiny.initialize(0.0F); // clear
                    mHasPendingSerotiny = false;
                    Debug.WriteLine("serotiny event: extra seed input " + total + " (total sum of seed probability over all pixels of the serotiny seed map) of species " + mSpecies.name());
                }
            }
            else
            {
                // distribute actual values
                DebugTimer t = new DebugTimer("seed dispersal", true);
                // fill seed map from source map
                distributeSeeds();

            }
        }

        /** scans the seed image and detects "edges".
            edges are then subsequently marked (set to -1). This is pass 1 of the seed distribution process.
            */
        public bool edgeDetection(Grid<float> seed_map = null)
        {
            Grid<float> seedmap = seed_map == null ? seed_map : mSeedMap; // switch to extra seed map if provided
            int dy = seedmap.sizeY();
            int dx = seedmap.sizeX();
            bool found = false;

            // fill mini-gaps
            int n_gaps_filled = 0;
            for (int y = 1; y < dy - 1; ++y)
            {
                int p = seedmap.index(1, y);
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
                int p = seedmap.index(1, y);
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
                Debug.WriteLine("species: " + mSpecies.id() + " # of gaps filled: " + n_gaps_filled + " # of edge-pixels: " + n_edges);
            }
            return found;
        }

        /** do the seed probability distribution.
            This is phase 2. Apply the seed kernel for each "edge" point identified in phase 1.
            */
        public void distribute(Grid<float> seed_map = null)
        {
            int x, y;
            Grid<float> seedmap = seed_map != null ? seed_map : mSeedMap; // switch to extra seed map if provided
            // choose the kernel depending whether there is a seed year for the current species or not
            Grid<float> kernel = species().isSeedYear() ? mKernelSeedYear : mKernelNonSeedYear;
            // extra case: serotiny
            if (seed_map != null)
            {
                kernel = mKernelSerotiny;
            }

            int offset = kernel.sizeX() / 2; // offset is the index of the center pixel
            for (int p = 0; p < seedmap.count(); ++p)
            {
                if (seedmap[p] == -1.0F)
                {
                    // edge pixel found. Now apply the kernel....
                    Point pt = seedmap.indexOf(p);
                    for (y = -offset; y <= offset; ++y)
                    {
                        for (x = -offset; x <= offset; ++x)
                        {
                            float kernel_value = kernel.valueAtIndex(x + offset, y + offset);
                            if (kernel_value > 0.0F && seedmap.isIndexValid(pt.X + x, pt.Y + y))
                            {
                                float val = seedmap.valueAtIndex(pt.X + x, pt.Y + y);
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
                        double m = species().isSeedYear() ? 1.0 : mNonSeedYearFraction;
                        for (int r = 0; r < mLDDDensity.Count; ++r)
                        {
                            float ldd_val = mLDDSeedlings; // pixels will have this probability
                            int n = (int)Math.Round(mLDDDensity[r] * m); // number of pixels to activate
                            for (int i = 0; i < n; ++i)
                            {
                                // distance and direction:
                                double radius = RandomGenerator.nrandom(mLDDDistance[r], mLDDDistance[r + 1]) / seedmap.cellsize(); // choose a random distance (in pixels)
                                double phi = RandomGenerator.drandom() * 2.0 * Math.PI; // choose a random direction
                                Point ldd = new Point((int)(pt.X + radius * Math.Cos(phi)), (int)(pt.Y + radius * Math.Sin(phi)));
                                if (seedmap.isIndexValid(ldd))
                                {
                                    float val = seedmap.valueAtIndex(ldd);
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

        public void distributeSeeds(Grid<float> seed_map = null)
        {
            Grid<float> sourcemap = seed_map != null ? seed_map : mSourceMap; // switch to extra seed map if provided
            Grid<float> kernel = mKernelSeedYear;

            // *** estimate seed production (based on leaf area) ***
            // calculate number of seeds; the source map holds now m2 leaf area on 20x20m pixels
            // after this step, each source cell has a value between 0 (no source) and 1 (fully covered cell)
            float fec = (float)species().fecundity_m2();
            if (!species().isSeedYear())
            {
                fec *= (float)mNonSeedYearFraction;
            }
            for (int p = 0; p < sourcemap.count(); ++p)
            {
                if (sourcemap[p] != 0.0F)
                {
                    // if LAI  >3, then full potential is assumed, below LAI=3 a linear ramp is used
                    sourcemap[p] = Math.Min(sourcemap[p] / (sourcemap.cellsize() * sourcemap.cellsize()) / 3.0F, 3.0F);
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

            int offset = kernel.sizeX() / 2; // offset is the index of the center pixel
                                             // source mode

            // *** seed distribution (Kernel + long distance dispersal) ***
            if (GlobalSettings.instance().model().settings().torusMode == false)
            {
                // ** standard case (no torus) **
                for (int src = 0; src < sourcemap.count(); ++src)
                {
                    if (sourcemap[src] > 0.0F)
                    {
                        Point sm = sourcemap.indexOf(src).Subtract(offset);
                        int sx = sm.X, sy = sm.Y;
                        for (int iy = 0; iy < kernel.sizeY(); ++iy)
                        {
                            for (int ix = 0; ix < kernel.sizeX(); ++ix)
                            {
                                if (mSeedMap.isIndexValid(sx + ix, sy + iy))
                                {
                                    mSeedMap[sx + ix, sy + iy] += sourcemap[src] * kernel[ix, iy];
                                }
                            }
                        }
                        // long distance dispersal
                        if (mLDDDensity.Count != 0)
                        {
                            Point pt = sourcemap.indexOf(src);

                            for (int r = 0; r < mLDDDensity.Count; ++r)
                            {
                                float ldd_val = mLDDSeedlings / fec; // pixels will have this probability [note: fecundity will be multiplied below]
                                int n;
                                if (mLDDDensity[r] < 1)
                                {
                                    n = RandomGenerator.drandom() < mLDDDensity[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)Math.Round(mLDDDensity[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    double radius = RandomGenerator.nrandom(mLDDDistance[r], mLDDDistance[r + 1]) / mSeedMap.cellsize(); // choose a random distance (in pixels)
                                    double phi = RandomGenerator.drandom() * 2.0 * Math.PI; // choose a random direction
                                    Point ldd = new Point(pt.X + (int)(radius * Math.Cos(phi)), pt.Y + (int)(radius * Math.Sin(phi)));
                                    if (mSeedMap.isIndexValid(ldd))
                                    {
                                        mSeedMap[ldd] += ldd_val;
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
                int seedmap_offset = sourcemap.indexAt(new PointF(0.0F, 0.0F)).X; // the seed maps have x extra rows/columns
                Point torus_pos;
                int seedpx_per_ru = (int)(Constant.cRUSize / sourcemap.cellsize());
                for (int src = 0; src < sourcemap.count(); ++src)
                {
                    if (sourcemap[src] > 0.0F)
                    {
                        Point sm = sourcemap.indexOf(src);
                        // get the origin of the resource unit *on* the seedmap in *seedmap-coords*:
                        Point offset_ru = new Point(((sm.X - seedmap_offset) / seedpx_per_ru) * seedpx_per_ru + seedmap_offset,
                                 ((sm.Y - seedmap_offset) / seedpx_per_ru) * seedpx_per_ru + seedmap_offset);  // coords RU origin

                        Point offset_in_ru = new Point((sm.X - seedmap_offset) % seedpx_per_ru, (sm.Y - seedmap_offset) % seedpx_per_ru);  // offset of current point within the RU

                        //Point sm=sourcemap.indexOf(src)-Point(offset, offset);
                        for (int iy = 0; iy < kernel.sizeY(); ++iy)
                        {
                            for (int ix = 0; ix < kernel.sizeX(); ++ix)
                            {
                                torus_pos = offset_ru.Add(new Point(Global.MOD((offset_in_ru.X - offset + ix), seedpx_per_ru), Global.MOD((offset_in_ru.Y - offset + iy), seedpx_per_ru)));

                                if (mSeedMap.isIndexValid(torus_pos))
                                {
                                    mSeedMap[torus_pos] += sourcemap[src] * kernel[ix, iy];
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
                                    n = RandomGenerator.drandom() < mLDDDensity[r] ? 1 : 0;
                                }
                                else
                                {
                                    n = (int)Math.Round(mLDDDensity[r]); // number of pixels to activate
                                }
                                for (int i = 0; i < n; ++i)
                                {
                                    // distance and direction:
                                    double radius = RandomGenerator.nrandom(mLDDDistance[r], mLDDDistance[r + 1]) / mSeedMap.cellsize(); // choose a random distance (in pixels)
                                    double phi = RandomGenerator.drandom() * 2.0 * Math.PI; // choose a random direction
                                    Point ldd = new Point((int)(radius * Math.Cos(phi)), (int)(radius * Math.Sin(phi))); // destination (offset)
                                    torus_pos = offset_ru.Add(new Point(Global.MOD((offset_in_ru.X + ldd.X), seedpx_per_ru), Global.MOD((offset_in_ru.Y + ldd.Y), seedpx_per_ru)));

                                    if (mSeedMap.isIndexValid(torus_pos))
                                    {
                                        mSeedMap[torus_pos] += ldd_val;
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
            for (int p = 0; p < mSeedMap.count(); ++p)
            {
                if (mSeedMap[p] > 0.0F)
                {
                    mSeedMap[p] = Math.Min(mSeedMap[p] * fec / n_unlimited, 1.0F);
                }
            }
        }
    }
}
