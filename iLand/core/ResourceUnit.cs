using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    /** @class ResourceUnit
        ResourceUnit is the spatial unit that encapsulates a forest stand and links to several environmental components
        (Climate, Soil, Water, ...).
        @ingroup core
        A resource unit has a size of (currently) 100x100m. Many processes in iLand operate on the level of a ResourceUnit.
        Each resource unit has the same Climate and other properties (e.g. available nitrogen).
        Proceses on this level are, inter alia, NPP Production (see Production3PG), water calculations (WaterCycle), the modeling
        of dead trees (Snag) and soil processes (Soil).
        */
    internal class ResourceUnit
    {
        private int mIndex; ///< internal index
        private int mID; ///< ID provided by external stand grid
        private bool mHasDeadTrees; ///< flag that indicates if currently dead trees are in the tree list
        private Climate mClimate; ///< pointer to the climate object of this RU
        private SpeciesSet mSpeciesSet; ///< pointer to the species set for this RU
        private WaterCycle mWater; ///< link to the Soil water calculation engine
        private Snag mSnag; ///< ptr to snag storage / dynamics
        private Soil mSoil; ///< ptr to CN dynamics soil submodel
        private List<ResourceUnitSpecies> mRUSpecies; ///< data for this ressource unit per species
        private List<Tree> mTrees; ///< storage container for tree individuals
        private SaplingCell[] mSaplings; ///< pointer to the array of Sapling-cells for the resource unit
        private RectangleF mBoundingBox; ///< bounding box (metric) of the RU
        private Point mCornerOffset; ///< coordinates on the LIF grid of the upper left corner of the RU
        private double mAggregatedLA; ///< sum of leafArea
        private double mAggregatedWLA; ///< sum of lightResponse * LeafArea for all trees
        private double mAggregatedLR; ///< sum of lightresponse*LA of the current unit
        private double mEffectiveArea; ///< total "effective" area per resource unit, i.e. area of RU - non-stocked - beerLambert-loss
        public double mEffectiveArea_perWLA; ///<
        private double mLRI_modification;
        public double mAverageAging; ///< leaf-area weighted average aging f this species on this RU.
        private float[] mSaplingHeightMap; ///< pointer to array that holds max-height for each 2x2m pixel. Note: this information is not persistent

        private int mPixelCount; ///< count of (Heightgrid) pixels thare are inside the RU
        private int mStockedPixelCount;  ///< count of pixels that are stocked with trees
        private double mStockedArea; ///< size of stocked area
        private double mStockableArea; ///< area of stockable area (defined by project setup)
        public StandStatistics mStatistics; ///< aggregate values on stand value
        public ResourceUnitVariables mUnitVariables;

        public void setClimate(Climate climate) { mClimate = climate; }
        public void setID(int id) { mID = id; }

        // access to elements
        public Climate climate() { return mClimate; } ///< link to the climate on this resource unit
        public SpeciesSet speciesSet() { return mSpeciesSet; } ///< get SpeciesSet this RU links to.
        public WaterCycle waterCycle() { return mWater; } ///< water model of the unit
        public Snag snag() { return mSnag; } ///< access the snag object
        public Soil soil() { return mSoil; } ///< access the soil model
        public SaplingCell[] saplingCellArray() { return mSaplings; } ///< access the array of sapling-cells

        public ResourceUnitSpecies resourceUnitSpecies(int species_index) { return mRUSpecies[species_index]; } ///< get RU-Species-container with index 'species_index' from the RU
        public List<ResourceUnitSpecies> ruSpecies() { return mRUSpecies; }
        public List<Tree> trees() { return mTrees; } ///< reference to the tree list.
        public List<Tree> constTrees() { return mTrees; } ///< reference to the (const) tree list.
        public Tree tree(int index) { return mTrees[index]; } ///< get pointer to a tree
        public ResourceUnitVariables resouceUnitVariables() { return mUnitVariables; } ///< access to variables that are specific to resourceUnit (e.g. nitrogenAvailable)
        public StandStatistics statistics() { return mStatistics; }

        // properties
        public int index() { return mIndex; }
        public int id() { return mID; }
        public RectangleF boundingBox() { return mBoundingBox; }
        public Point cornerPointOffset() { return mCornerOffset; } ///< coordinates on the LIF grid of the upper left corner of the RU
        public double area() { return mPixelCount * 100; } ///< get the resource unit area in m2
        public double stockedArea() { return mStockedArea; } ///< get the stocked area in m2
        public double stockableArea() { return mStockableArea; } ///< total stockable area in m2
        public double productiveArea() { return mEffectiveArea; } ///< TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public double leafAreaIndex() { return stockableArea() != 0.0 ? mAggregatedLA / stockableArea() : 0.0; } ///< Total Leaf Area Index
        public double leafArea() { return mAggregatedLA; } ///< total leaf area of resource unit (m2)
        public double interceptedArea(double LA, double LightResponse) { return mEffectiveArea_perWLA * LA * LightResponse; }
        public double LRImodifier() { return mLRI_modification; }
        public double averageAging() { return mAverageAging; } ///< leaf area weighted average aging

        // actions
        public void treeDied() { mHasDeadTrees = true; } ///< sets the flag that indicates that the resource unit contains dead trees
        public bool hasDiedTrees() { return mHasDeadTrees; } ///< if true, the resource unit has dead trees and needs maybe some cleanup
        /// addWLA() is called by each tree to aggregate the total weighted leaf area on a unit
        public void addWLA(float LA, float LRI) { mAggregatedWLA += LA * LRI; mAggregatedLA += LA; }
        public void addLR(float LA, float LightResponse) { mAggregatedLR += LA * LightResponse; }
        public void addTreeAging(double leaf_area, double aging_factor) { mAverageAging += leaf_area * aging_factor; } ///< aggregate the tree aging values (weighted by leaf area)
        // stocked area calculation
        public void countStockedPixel(bool pixelIsStocked) { mPixelCount++; if (pixelIsStocked) mStockedPixelCount++; }
        public void setStockableArea(double area) { mStockableArea = area; } ///< set stockable area (m2)

        // snag / snag dynamics
        // snag dynamics, soil carbon and nitrogen cycle
        public void snagNewYear() { if (snag() != null) snag().newYear(); } ///< clean transfer pools

        public ResourceUnit(int index)
        {
            mRUSpecies = new List<ResourceUnitSpecies>();
            mSpeciesSet = null;
            mClimate = null;
            mPixelCount = 0;
            mStockedArea = 0;
            mStockedPixelCount = 0;
            mStockableArea = 0;
            mAggregatedWLA = 0.0;
            mAggregatedLA = 0.0;
            mAggregatedLR = 0.0;
            mEffectiveArea = 0.0;
            mLRI_modification = 0.0;
            mIndex = index;
            mSaplingHeightMap = null;
            mEffectiveArea_perWLA = 0.0;
            mWater = new WaterCycle();
            mSnag = null;
            mSoil = null;
            mSaplings = null;
            mID = 0;
        }

        public void setup()
        {
            mWater.setup(this);

            mSnag = null;
            mSoil = null;
            if (GlobalSettings.instance().model().settings().carbonCycleEnabled)
            {
                mSoil = new Soil(this);
                mSnag = new Snag();
                mSnag.setup(this);
                XmlHelper xml = GlobalSettings.instance().settings();

                // setup contents of the soil of the RU; use values for C and N (kg/ha)
                mSoil.setInitialState(new CNPool(xml.valueDouble("model.site.youngLabileC", -1),
                                              xml.valueDouble("model.site.youngLabileN", -1),
                                              xml.valueDouble("model.site.youngLabileDecompRate", -1)),
                                      new CNPool(xml.valueDouble("model.site.youngRefractoryC", -1),
                                              xml.valueDouble("model.site.youngRefractoryN", -1),
                                              xml.valueDouble("model.site.youngRefractoryDecompRate", -1)),
                                       new CNPair(xml.valueDouble("model.site.somC", -1), xml.valueDouble("model.site.somN", -1)));
            }

            mSaplings = null;
            if (GlobalSettings.instance().model().settings().regenerationEnabled)
            {
                mSaplings = new SaplingCell[Constant.cPxPerHectare];
            }

            // setup variables
            mUnitVariables.nitrogenAvailable = GlobalSettings.instance().settings().valueDouble("model.site.availableNitrogen", 40);

            // if dynamic coupling of soil nitrogen is enabled, a starting value for available N is calculated
            if (mSoil != null && GlobalSettings.instance().model().settings().useDynamicAvailableNitrogen && GlobalSettings.instance().model().settings().carbonCycleEnabled)
            {
                mSoil.setClimateFactor(1.0);
                mSoil.calculateYear();
                mUnitVariables.nitrogenAvailable = soil().availableNitrogen();
            }
            mHasDeadTrees = false;
            mAverageAging = 0.0;

        }

        public void setBoundingBox(RectangleF bb)
        {
            mBoundingBox = bb;
            mCornerOffset = GlobalSettings.instance().model().grid().indexAt(bb.TopLeft());
        }

        /// return the sapling cell at given LIF-coordinates
        public SaplingCell saplingCell(Point lifCoords)
        {
            // LIF-Coordinates are global, we here need (RU-)local coordinates
            int ix = lifCoords.X % Constant.cPxPerRU;
            int iy = lifCoords.Y % Constant.cPxPerRU;
            int i = iy * Constant.cPxPerRU + ix;
            Debug.Assert(i >= 0 && i < Constant.cPxPerHectare);
            return mSaplings[i];
        }

        /// set species and setup the species-per-RU-data
        public void setSpeciesSet(SpeciesSet set)
        {
            mSpeciesSet = set;
            mRUSpecies.Clear();

            //mRUSpecies.Capacity = set.count(); // ensure that the vector space is not relocated
            for (int i = 0; i < set.count(); i++)
            {
                Species s = mSpeciesSet.species(i);
                if (s == null)
                {
                    throw new NotSupportedException("setSpeciesSet: invalid index!");
                }

                ResourceUnitSpecies rus = new ResourceUnitSpecies();
                mRUSpecies.Add(rus);
                rus.setup(s, this);
                /* be careful: setup() is called with a pointer somewhere to the content of the mRUSpecies container.
                   If the container memory is relocated (List), the pointer gets invalid!!!
                   Therefore, a resize() is called before the loop (no resize()-operations during the loop)! */
                //mRUSpecies[i].setup(s,this); // setup this element
            }
        }

        public ResourceUnitSpecies resourceUnitSpecies(Species species)
        {
            return mRUSpecies[species.index()];
        }

        public ResourceUnitSpecies constResourceUnitSpecies(Species species)
        {
            return mRUSpecies[species.index()];
        }

        public Tree newTree()
        {
            // start simple: just append to the vector...
            if (mTrees.Count == 0)
            {
                mTrees.Capacity = 100; // reserve a junk of memory for trees
            }

            Tree tree = new Tree();
            mTrees.Add(tree);
            return tree;
        }

        public int newTreeIndex()
        {
            newTree();
            return mTrees.Count - 1; // return index of the last tree
        }

        /// remove dead trees from tree list
        /// reduce size of vector if lots of space is free
        /// tests showed that this way of cleanup is very fast,
        /// because no memory allocations are performed (simple memmove())
        /// when trees are moved.
        public void cleanTreeList()
        {
            if (!mHasDeadTrees)
            {
                return;
            }

            int last;
            for (last = mTrees.Count - 1; last >= 0 && mTrees[last].isDead(); --last)
            {
                --last;
            }

            int current = 0;
            while (current < last)
            {
                if (mTrees[current].isDead())
                {
                    mTrees[current] = mTrees[last]; // copy data!
                    --last; //
                    while (last >= current && mTrees[last].isDead())
                    {
                        --last;
                    }
                }
                ++current;
            }
            ++last; // last points now to the first dead tree

            // free ressources
            if (last != mTrees.Count)
            {
                mTrees.RemoveRange(last, mTrees.Count - last);
                if (mTrees.Capacity > 100)
                {
                    if (mTrees.Count / (double)mTrees.Capacity < 0.2)
                    {
                        //int target_size = mTrees.Count*2;
                        //Debug.WriteLine("reduce size from "+mTrees.Capacity + "to" + target_size;
                        //mTrees.reserve(qMax(target_size, 100));
                        if (GlobalSettings.instance().logLevelDebug())
                        {
                            Debug.WriteLine("reduce tree storage of RU " + index() + " from " + mTrees.Capacity + " to " + mTrees.Count);
                        }
                        mTrees.Capacity = mTrees.Count;
                    }
                }
            }
            mHasDeadTrees = false; // reset flag
        }

        public void newYear()
        {
            mAggregatedWLA = 0.0;
            mAggregatedLA = 0.0;
            mAggregatedLR = 0.0;
            mEffectiveArea = 0.0;
            mPixelCount = mStockedPixelCount = 0;
            snagNewYear();
            if (mSoil != null)
            {
                mSoil.newYear();
            }
            // clear statistics global and per species...
            mStatistics.clear();
            for (int i = 0; i < mRUSpecies.Count; ++i)
            {
                mRUSpecies[i].statisticsDead().clear();
                mRUSpecies[i].statisticsMgmt().clear();
            }
        }

        /** production() is the "stand-level" part of the biomass production (3PG).
            - The amount of radiation intercepted by the stand is calculated
            - the water cycle is calculated
            - statistics for each species are cleared
            - The 3PG production for each species and ressource unit is called (calculates species-responses and NPP production)
            see also: http://iland.boku.ac.at/individual+tree+light+availability */
        public void production()
        {

            if (mAggregatedWLA == 0.0 || mPixelCount == 0)
            {
                // clear statistics of resourceunitspecies
                for (int i = 0; i < mRUSpecies.Count; ++i)
                {
                    mRUSpecies[i].statistics().clear();
                }
                mEffectiveArea = 0.0;
                mStockedArea = 0.0;
                return;
            }

            // the pixel counters are filled during the height-grid-calculations
            mStockedArea = Constant.cHeightPerRU * Constant.cHeightPerRU * mStockedPixelCount; // m2 (1 height grid pixel = 10x10m)
            if (leafAreaIndex() < 3.0)
            {
                // estimate stocked area based on crown projections
                double crown_area = 0.0;
                for (int i = 0; i < mTrees.Count; ++i)
                {
                    crown_area += mTrees[i].isDead() ? 0.0 : mTrees[i].stamp().reader().crownArea();
                }
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("crown area: lai " + leafAreaIndex() + " stocked area (pixels) " + mStockedArea + " area (crown) " + crown_area);
                }
                if (leafAreaIndex() < 1.0)
                {
                    mStockedArea = Math.Min(crown_area, mStockedArea);
                }
                else
                {
                    // for LAI between 1 and 3:
                    // interpolate between sum of crown area of trees (at LAI=1) and the pixel-based value (at LAI=3 and above)
                    double px_frac = (leafAreaIndex() - 1.0) / 2.0; // 0 at LAI=1, 1 at LAI=3
                    mStockedArea = mStockedArea * px_frac + Math.Min(crown_area, mStockedArea) * (1.0 - px_frac);
                }
                if (mStockedArea == 0.0)
                {
                    return;
                }
            }

            // calculate the leaf area index (LAI)
            double LAI = mAggregatedLA / mStockedArea;
            // calculate the intercepted radiation fraction using the law of Beer Lambert
            double k = GlobalSettings.instance().model().settings().lightExtinctionCoefficient;
            double interception_fraction = 1.0 - Math.Exp(-k * LAI);
            mEffectiveArea = mStockedArea * interception_fraction; // m2

            // calculate the total weighted leaf area on this RU:
            mLRI_modification = interception_fraction * mStockedArea / mAggregatedWLA; // p_WLA
            if (mLRI_modification == 0.0)
            {
                Debug.WriteLine("lri modification==0!");
            }
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine(String.Format("production: LAI: {0} (intercepted fraction: {1}, stocked area: {3}). LRI-Multiplier: {2}",
                                              LAI, interception_fraction, mLRI_modification, mStockedArea));
            }

            // calculate LAI fractions
            double ru_lai = leafAreaIndex();
            if (ru_lai < 1.0)
            {
                ru_lai = 1.0;
            }
            // note: LAIFactors are only 1 if sum of LAI is > 1.0 (see WaterCycle)
            for (int i = 0; i < mRUSpecies.Count; ++i)
            {
                double lai_factor = mRUSpecies[i].statistics().leafAreaIndex() / ru_lai;

                //DBGMODE(
                if (lai_factor > 1.0)
                {
                    ResourceUnitSpecies rus = mRUSpecies[i];
                    Debug.WriteLine("LAI factor > 1: species ru-index: " + rus.species().name() + rus.ru().index());
                }
                //);
                mRUSpecies[i].setLAIfactor(lai_factor);
            }

            // soil water model - this determines soil water contents needed for response calculations
            {
                mWater.run();
            }

            // invoke species specific calculation (3PG)
            for (int i = 0; i < mRUSpecies.Count; ++i)
            {
                //DBGMODE(
                if (mRUSpecies[i].LAIfactor() > 1.0)
                {
                    ResourceUnitSpecies rus = mRUSpecies[i];
                    Debug.WriteLine("LAI factor > 1: species ru-index value: " + rus.species().name() + rus.ru().index() + rus.LAIfactor());
                }
                //);
                mRUSpecies[i].calculate(); // CALCULATE 3PG

                // debug output related to production
                if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dStandGPP) && mRUSpecies[i].LAIfactor() > 0.0)
                {
                    List<object> output = GlobalSettings.instance().debugList(index(), DebugOutputs.dStandGPP);
                    output.AddRange(new object[] { mRUSpecies[i].species().id(),  index(),  id(),
                                                   mRUSpecies[i].LAIfactor(),  mRUSpecies[i].prod3PG().GPPperArea(), 
                                                   productiveArea() * mRUSpecies[i].LAIfactor() * mRUSpecies[i].prod3PG().GPPperArea(), averageAging(),  
                                                   mRUSpecies[i].prod3PG().fEnvYear() });
                }
            }
        }

        public void calculateInterceptedArea()
        {
            if (mAggregatedLR == 0)
            {
                mEffectiveArea_perWLA = 0.0;
                return;
            }
            Debug.Assert(mAggregatedLR > 0.0);
            mEffectiveArea_perWLA = mEffectiveArea / mAggregatedLR;
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("RU: aggregated lightresponse: " + mAggregatedLR + " eff.area./wla: " + mEffectiveArea_perWLA);
            }
        }

        // function is called immediately before the growth of individuals
        public void beforeGrow()
        {
            mAverageAging = 0.0;
        }

        // function is called after finishing the indivdual growth / mortality.
        public void afterGrow()
        {
            mAverageAging = leafArea() > 0.0 ? mAverageAging / leafArea() : 0; // calculate aging value (calls to addAverageAging() by individual trees)
            if (mAverageAging > 0.0 && mAverageAging < 0.00001)
            {
                Debug.WriteLine("ru " + mIndex + " aging <0.00001");
            }
            if (mAverageAging < 0.0 || mAverageAging > 1.0)
            {
                Debug.WriteLine("Average aging invalid: (RU, LAI): " + index() + mStatistics.leafAreaIndex());
            }
        }

        public void yearEnd()
        {
            // calculate statistics for all tree species of the ressource unit
            int c = mRUSpecies.Count;
            for (int i = 0; i < c; i++)
            {
                mRUSpecies[i].statisticsDead().calculate(); // calculate the dead trees
                mRUSpecies[i].statisticsMgmt().calculate(); // stats of removed trees
                mRUSpecies[i].updateGWL(); // get sum of dead trees (died + removed)
                mRUSpecies[i].statistics().calculate(); // calculate the living (and add removed volume to gwl)
                mStatistics.add(mRUSpecies[i].statistics());
            }
            mStatistics.calculate(); // aggreagte on stand level

            // update carbon flows
            if (soil() != null && GlobalSettings.instance().model().settings().carbonCycleEnabled)
            {
                double area_factor = stockableArea() / Constant.cRUArea; //conversion factor
                mUnitVariables.carbonUptake = statistics().npp() * Constant.biomassCFraction;
                mUnitVariables.carbonUptake += statistics().nppSaplings() * Constant.biomassCFraction;

                double to_atm = snag().fluxToAtmosphere().C / area_factor; // from snags, kgC/ha
                to_atm += soil().fluxToAtmosphere().C * Constant.cRUArea / 10.0; // soil: t/ha . t/m2 . kg/ha
                mUnitVariables.carbonToAtm = to_atm;

                double to_dist = snag().fluxToDisturbance().C / area_factor;
                to_dist += soil().fluxToDisturbance().C * Constant.cRUArea / 10.0;
                double to_harvest = snag().fluxToExtern().C / area_factor;

                mUnitVariables.NEP = mUnitVariables.carbonUptake - to_atm - to_dist - to_harvest; // kgC/ha

                // incremental values....
                mUnitVariables.cumCarbonUptake += mUnitVariables.carbonUptake;
                mUnitVariables.cumCarbonToAtm += mUnitVariables.carbonToAtm;
                mUnitVariables.cumNEP += mUnitVariables.NEP;
            }
        }

        public void addTreeAgingForAllTrees()
        {
            mAverageAging = 0.0;
            foreach (Tree t in mTrees)
            {
                addTreeAging(t.leafArea(), t.species().aging(t.height(), t.age()));
            }
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "yearEnd()" above!!!
        public void createStandStatistics()
        {
            // clear statistics (ru-level and ru-species level)
            mStatistics.clear();
            for (int i = 0; i < mRUSpecies.Count; i++)
            {
                mRUSpecies[i].statistics().clear();
                mRUSpecies[i].statisticsDead().clear();
                mRUSpecies[i].statisticsMgmt().clear();
                mRUSpecies[i].saplingStat().clearStatistics();
            }

            // add all trees to the statistics objects of the species
            foreach (Tree t in mTrees)
            {
                if (!t.isDead())
                {
                    resourceUnitSpecies(t.species()).statistics().add(t, null);
                }
            }
            // summarise sapling stats
            GlobalSettings.instance().model().saplings().calculateInitialStatistics(this);

            // summarize statistics for the whole resource unit
            for (int i = 0; i < mRUSpecies.Count; i++)
            {
                mRUSpecies[i].saplingStat().calculate(mRUSpecies[i].species(), this);
                mRUSpecies[i].statistics().add(mRUSpecies[i].saplingStat());
                mRUSpecies[i].statistics().calculate();
                mStatistics.add(mRUSpecies[i].statistics());
            }
            mStatistics.calculate();
            mAverageAging = mStatistics.leafAreaIndex() > 0.0 ? mAverageAging / (mStatistics.leafAreaIndex() * stockableArea()) : 0.0;
            if (mAverageAging < 0.0 || mAverageAging > 1.0)
            {
                Debug.WriteLine("Average aging invalid: (RU, LAI): " + index() + mStatistics.leafAreaIndex());
            }
        }

        /** recreate statistics. This is necessary after events that changed the structure
            of the stand *after* the growth of trees (where stand statistics are updated).
            An example is after disturbances.  */
        public void recreateStandStatistics(bool recalculate_stats)
        {
            // when called after disturbances (recalculate_stats=false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int i = 0; i < mRUSpecies.Count; i++)
            {
                if (recalculate_stats)
                {
                    mRUSpecies[i].statistics().clear();
                }
                else
                {
                    mRUSpecies[i].statistics().clearOnlyTrees();
                }
            }
            foreach (Tree t in mTrees)
            {
                resourceUnitSpecies(t.species()).statistics().add(t, null);
            }

            if (recalculate_stats)
            {
                for (int i = 0; i < mRUSpecies.Count; i++)
                {
                    mRUSpecies[i].statistics().calculate();
                }
            }
        }

        public void calculateCarbonCycle()
        {
            if (snag() == null) // TODO: what about other pools?
            {
                return;
            }

            // (1) calculate the snag dynamics
            // because all carbon/nitrogen-flows from trees to the soil are routed through the snag-layer,
            // all soil inputs (litter + deadwood) are collected in the Snag-object.
            snag().calculateYear();
            soil().setClimateFactor(snag().climateFactor()); // the climate factor is only calculated once
            soil().setSoilInput(snag().labileFlux(), snag().refractoryFlux());
            soil().calculateYear(); // update the ICBM/2N model
                                    // use available nitrogen?
            if (GlobalSettings.instance().model().settings().useDynamicAvailableNitrogen)
            {
                mUnitVariables.nitrogenAvailable = soil().availableNitrogen();
            }

            // debug output
            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dCarbonCycle) && !snag().isEmpty())
            {
                List<object> output = GlobalSettings.instance().debugList(index(), DebugOutputs.dCarbonCycle);
                output.Add(new object[] { index(), id(), // resource unit index and id
                                          snag().debugList(), // snag debug outs
                                          soil().debugList() }); // ICBM/2N debug outs
            }
        }
    }
}
