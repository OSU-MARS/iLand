using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
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
    public class ResourceUnit
    {
        private double mTotalWeightedLeafArea; ///< sum of lightResponse * LeafArea for all trees
        private double mAggregatedLR; ///< sum of lightresponse*LA of the current unit
        private int mNextTreeID;
        private int mHeightCells; ///< count of (Heightgrid) pixels thare are inside the RU
        private int mHeightCellsWithTrees;  ///< count of pixels that are stocked with trees

        public double AverageAging { get; private set; } ///< leaf area weighted average aging
        public RectangleF BoundingBox { get; private set; }
        public Climate Climate { get; set; } ///< link to the climate on this resource unit
        public Point CornerPointOffset { get; private set; } ///< coordinates on the LIF grid of the upper left corner of the RU
        public double EffectiveAreaPerWla { get; private set; } ///<
        public bool HasDeadTrees { get; private set; } ///< if true, the resource unit has dead trees and needs maybe some cleanup
        public int ID { get; set; }
        public int Index { get; private set; }
        public double LriModifier { get; private set; }
        public double PhotosyntheticallyActiveArea { get; private set; } ///< TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public Snag Snags { get; private set; } ///< access the snag object
        public Soil Soil { get; private set; } ///< access the soil model
        public List<ResourceUnitSpecies> Species { get; private set; }
        public SpeciesSet SpeciesSet { get; private set; } ///< get SpeciesSet this RU links to.
        public SaplingCell[] SaplingCells { get; private set; } ///< access the array of sapling-cells
        public StandStatistics Statistics { get; private set; }
        public double StockedArea { get; private set; } ///< get the stocked area in m2
        public double StockableArea { get; set; } ///< total stockable area in m2
        public double TotalLeafArea { get; private set; } ///< total leaf area of resource unit (m2)
        public List<Tree> Trees { get; private set; } ///< reference to the tree list.
        public ResourceUnitVariables Variables { get; private set; } ///< access to variables that are specific to resourceUnit (e.g. nitrogenAvailable)
        public WaterCycle WaterCycle { get; private set; } ///< water model of the unit

        public double HeightArea() { return mHeightCells * Constant.HeightPixelArea; } ///< get the resource unit area in m2
        public double InterceptedArea(double leafArea, double lightResponse) { return EffectiveAreaPerWla * leafArea * lightResponse; }
        public double LeafAreaIndex() { return StockableArea != 0.0 ? TotalLeafArea / StockableArea : 0.0; } ///< Total Leaf Area Index

        public ResourceUnitSpecies ResourceUnitSpecies(int species_index) { return Species[species_index]; } ///< get RU-Species-container with index 'species_index' from the RU
        public void SnagNewYear() { if (Snags != null) Snags.NewYear(); } ///< clean transfer pools
        public Tree Tree(int index) { return Trees[index]; } ///< get pointer to a tree
        public void TreeDied() { HasDeadTrees = true; } ///< sets the flag that indicates that the resource unit contains dead trees

        public ResourceUnit(int index)
        {
            this.mAggregatedLR = 0.0;
            this.mTotalWeightedLeafArea = 0.0;
            this.mNextTreeID = 0;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;

            this.Climate = null;
            this.EffectiveAreaPerWla = 0.0;
            this.ID = 0;
            this.Index = index;
            this.LriModifier = 0.0;
            this.PhotosyntheticallyActiveArea = 0.0;
            this.Species = new List<ResourceUnitSpecies>();
            this.SaplingCells = null;
            this.Snags = null;
            this.Soil = null;
            this.SpeciesSet = null;
            this.Statistics = new StandStatistics();
            this.StockableArea = 0.0;
            this.StockedArea = 0.0;
            this.TotalLeafArea = 0.0;
            this.Trees = new List<Tree>();
            this.Variables = new ResourceUnitVariables();
            this.WaterCycle = new WaterCycle();
        }

        public void Setup(Model model)
        {
            this.WaterCycle.Setup(this, model);

            this.Snags = null;
            this.Soil = null;
            if (model.ModelSettings.CarbonCycleEnabled)
            {
                this.Soil = new Soil(model.GlobalSettings, this);
                this.Snags = new Snag();
                // class size of snag classes
                this.Snags.SetupThresholds(model.GlobalSettings.Settings.GetDouble("model.settings.soil.swdDBHClass12"),
                                           model.GlobalSettings.Settings.GetDouble("model.settings.soil.swdDBHClass23"));
                this.Snags.Setup(this, model.GlobalSettings); // must call SetupThresholds() first

                XmlHelper xml = model.GlobalSettings.Settings;

                // setup contents of the soil of the RU; use values for C and N (kg/ha)
                this.Soil.SetInitialState(new CNPool(xml.GetDouble("model.site.youngLabileC", -1),
                                                     xml.GetDouble("model.site.youngLabileN", -1),
                                                     xml.GetDouble("model.site.youngLabileDecompRate", -1)),
                                          new CNPool(xml.GetDouble("model.site.youngRefractoryC", -1),
                                                     xml.GetDouble("model.site.youngRefractoryN", -1),
                                                     xml.GetDouble("model.site.youngRefractoryDecompRate", -1)),
                                          new CNPair(xml.GetDouble("model.site.somC", -1), 
                                                     xml.GetDouble("model.site.somN", -1)));
            }

            if (model.ModelSettings.RegenerationEnabled)
            {
                this.SaplingCells = new SaplingCell[Constant.LightCellsPerHectare];
                for (int cellIndex = 0; cellIndex < this.SaplingCells.Length; ++cellIndex)
                {
                    // BUGBUG: SoA
                    this.SaplingCells[cellIndex] = new SaplingCell();
                }
            }

            // setup variables
            this.Variables.NitrogenAvailable = model.GlobalSettings.Settings.GetDouble("model.site.availableNitrogen", 40);

            // if dynamic coupling of soil nitrogen is enabled, a starting value for available N is calculated
            if (this.Soil != null && model.ModelSettings.UseDynamicAvailableNitrogen && model.ModelSettings.CarbonCycleEnabled)
            {
                this.Soil.ClimateFactor = 1.0;
                this.Soil.CalculateYear();
                this.Variables.NitrogenAvailable = Soil.AvailableNitrogen;
            }

            this.AverageAging = 0.0;
            this.HasDeadTrees = false;
        }

        // stocked area calculation
        public void AddHeightCell(bool cellHasTrees) 
        {
            ++this.mHeightCells;
            if (cellHasTrees)
            {
                ++this.mHeightCellsWithTrees;
            }
        }

        public void AddLightResponse(float leafArea, float lightResponse) { mAggregatedLR += leafArea * lightResponse; }
        public void AddTreeAging(double leafArea, double agingFactor) { AverageAging += leafArea * agingFactor; } ///< aggregate the tree aging values (weighted by leaf area)

        /// AddWeightedLeafArea() is called by each tree to aggregate the total weighted leaf area on a unit
        public void AddWeightedLeafArea(float leafArea, float lri) 
        { 
            mTotalWeightedLeafArea += leafArea * lri; 
            TotalLeafArea += leafArea; 
        }

        public void SetBoundingBox(RectangleF bb, Model model)
        {
            BoundingBox = bb;
            CornerPointOffset = model.LightGrid.IndexAt(bb.TopLeft());
        }

        /// return the sapling cell at given LIF-coordinates
        public SaplingCell SaplingCell(Point lifCoords)
        {
            // LIF-Coordinates are global, we here need (RU-)local coordinates
            int ix = lifCoords.X % Constant.LightPerRUsize;
            int iy = lifCoords.Y % Constant.LightPerRUsize;
            int i = iy * Constant.LightPerRUsize + ix;
            Debug.Assert(i >= 0 && i < Constant.LightCellsPerHectare);
            return SaplingCells[i];
        }

        /// set species and setup the species-per-RU-data
        public void SetSpeciesSet(SpeciesSet set)
        {
            SpeciesSet = set;
            Species.Clear();

            //mRUSpecies.Capacity = set.count(); // ensure that the vector space is not relocated
            for (int i = 0; i < set.SpeciesCount(); i++)
            {
                Species s = SpeciesSet.Species(i);
                if (s == null)
                {
                    throw new NotSupportedException("setSpeciesSet: invalid index!");
                }

                ResourceUnitSpecies rus = new ResourceUnitSpecies(s, this);
                Species.Add(rus);
                /* be careful: setup() is called with a pointer somewhere to the content of the mRUSpecies container.
                   If the container memory is relocated (List), the pointer gets invalid!!!
                   Therefore, a resize() is called before the loop (no resize()-operations during the loop)! */
                //mRUSpecies[i].setup(s,this); // setup this element
            }
        }

        public ResourceUnitSpecies ResourceUnitSpecies(Species species)
        {
            return Species[species.Index];
        }

        public Tree AddNewTree(Model model)
        {
            Tree tree = new Tree()
            {
                ID = this.mNextTreeID++
            };
            tree.SetGrids(model.LightGrid, model.HeightGrid);
            this.Trees.Add(tree);
            return tree;
        }

        /// remove dead trees from tree list
        /// reduce size of vector if lots of space is free
        /// tests showed that this way of cleanup is very fast,
        /// because no memory allocations are performed (simple memmove())
        /// when trees are moved.
        public void CleanTreeList()
        {
            if (!HasDeadTrees)
            {
                return;
            }

            int last;
            for (last = Trees.Count - 1; last >= 0 && Trees[last].IsDead(); --last)
            {
            }

            int current = 0;
            while (current < last)
            {
                if (Trees[current].IsDead())
                {
                    Trees[current] = Trees[last]; // copy data!
                    --last; //
                    while (last >= current && Trees[last].IsDead())
                    {
                        --last;
                    }
                }
                ++current;
            }
            ++last; // last points now to the first dead tree

            // free ressources
            if (last != Trees.Count)
            {
                Trees.RemoveRange(last, Trees.Count - last); // BUGBUG: assumes dead trees are at end of list
                if (Trees.Capacity > 100)
                {
                    if (Trees.Count / (double)Trees.Capacity < 0.2)
                    {
                        //int target_size = mTrees.Count*2;
                        //Debug.WriteLine("reduce size from "+mTrees.Capacity + "to" + target_size;
                        //mTrees.reserve(qMax(target_size, 100));
                        //if (GlobalSettings.Instance.LogDebug())
                        //{
                        //    Debug.WriteLine("reduce tree storage of RU " + Index + " from " + Trees.Capacity + " to " + Trees.Count);
                        //}
                        Trees.Capacity = Trees.Count;
                    }
                }
            }
            HasDeadTrees = false; // reset flag
        }

        public void NewYear()
        {
            this.mAggregatedLR = 0.0;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;
            this.mTotalWeightedLeafArea = 0.0;

            this.PhotosyntheticallyActiveArea = 0.0;
            this.TotalLeafArea = 0.0;

            SnagNewYear();
            if (Soil != null)
            {
                Soil.NewYear();
            }
            // clear statistics global and per species...
            Statistics.Clear();
            for (int i = 0; i < Species.Count; ++i)
            {
                Species[i].StatisticsDead.Clear();
                Species[i].StatisticsMgmt.Clear();
            }
        }

        /** production() is the "stand-level" part of the biomass production (3PG).
            - The amount of radiation intercepted by the stand is calculated
            - the water cycle is calculated
            - statistics for each species are cleared
            - The 3PG production for each species and ressource unit is called (calculates species-responses and NPP production)
            see also: http://iland.boku.ac.at/individual+tree+light+availability */
        public void Production(Model model)
        {
            if (mTotalWeightedLeafArea == 0.0 || mHeightCells == 0)
            {
                // clear statistics of resourceunitspecies
                for (int i = 0; i < Species.Count; ++i)
                {
                    Species[i].Statistics.Clear();
                }
                PhotosyntheticallyActiveArea = 0.0;
                StockedArea = 0.0;
                return;
            }

            // the pixel counters are filled during the height-grid-calculations
            StockedArea = Constant.HeightSize * Constant.HeightSize * mHeightCellsWithTrees; // m2 (1 height grid pixel = 10x10m)
            if (LeafAreaIndex() < 3.0)
            {
                // estimate stocked area based on crown projections
                double totalCrownArea = 0.0;
                for (int treeIndex = 0; treeIndex < Trees.Count; ++treeIndex)
                {
                    totalCrownArea += Trees[treeIndex].IsDead() ? 0.0 : Trees[treeIndex].Stamp.Reader.CrownArea;
                }
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("crown area: lai " + LeafAreaIndex() + " stocked area (pixels) " + StockedArea + " area (crown) " + totalCrownArea);
                //}
                if (LeafAreaIndex() < 1.0)
                {
                    this.StockedArea = Math.Min(totalCrownArea, this.StockedArea);
                }
                else
                {
                    // for LAI between 1 and 3:
                    // interpolate between sum of crown area of trees (at LAI=1) and the pixel-based value (at LAI=3 and above)
                    // only results in a change if crown area is less than stocked area
                    // BUGBUG: assumes trees are homogeneously distributed across resource unit and that crowns don't overlap
                    double px_frac = (LeafAreaIndex() - 1.0) / 2.0; // 0 at LAI=1, 1 at LAI=3
                    this.StockedArea = this.StockedArea * px_frac + Math.Min(totalCrownArea, StockedArea) * (1.0 - px_frac);
                }
                if (this.StockedArea == 0.0)
                {
                    return;
                }
            }

            // calculate the leaf area index (LAI)
            double leafAreaIndex = this.TotalLeafArea / this.StockedArea;
            // calculate the intercepted radiation fraction using the law of Beer Lambert
            double k = model.ModelSettings.LightExtinctionCoefficient;
            double lightInterceptionfraction = 1.0 - Math.Exp(-k * leafAreaIndex);
            PhotosyntheticallyActiveArea = StockedArea * lightInterceptionfraction; // m2

            // calculate the total weighted leaf area on this RU:
            LriModifier = PhotosyntheticallyActiveArea / mTotalWeightedLeafArea; // p_WLA
            Debug.Assert(LriModifier >= 0.0 && LriModifier < 2.0); // sanity upper bound
            if (LriModifier == 0.0)
            {
                Debug.WriteLine("lri modification==0!");
            }
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine(String.Format("production: LAI: {0} (intercepted fraction: {1}, stocked area: {3}). LRI-Multiplier: {2}",
            //                                  LAI, interception_fraction, LriModifier, StockedArea));
            //}

            // calculate LAI fractions
            double allSpeciesLeafAreaIndex = this.LeafAreaIndex(); // TODO: should this be the same as two LAI calculations above?
            if (allSpeciesLeafAreaIndex < 1.0)
            {
                allSpeciesLeafAreaIndex = 1.0;
            }
            // note: LAIFactors are only 1 if sum of LAI is > 1.0 (see WaterCycle)
            for (int speciesIndex = 0; speciesIndex < Species.Count; ++speciesIndex)
            {
                double lai_factor = Species[speciesIndex].Statistics.LeafAreaIndex / allSpeciesLeafAreaIndex;

                //DBGMODE(
                if (lai_factor > 1.0)
                {
                    ResourceUnitSpecies rus = Species[speciesIndex];
                    Debug.WriteLine("LAI factor > 1: species ru-index: " + rus.Species.Name + rus.RU.Index);
                }
                //);
                Species[speciesIndex].SetLaiFactor(lai_factor);
            }

            // soil water model - this determines soil water contents needed for response calculations
            WaterCycle.Run(model);

            // invoke species specific calculation (3PG)
            for (int speciesIndex = 0; speciesIndex < Species.Count; ++speciesIndex)
            {
                //DBGMODE(
                if (Species[speciesIndex].LaiFraction > 1.0)
                {
                    ResourceUnitSpecies rus = Species[speciesIndex];
                    Debug.WriteLine("LAI factor > 1: species ru-index value: " + rus.Species.Name + rus.RU.Index + rus.LaiFraction);
                }
                //);
                Species[speciesIndex].Calculate(model); // CALCULATE 3PG

                // debug output related to production
                //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.StandGpp) && Species[speciesIndex].LaiFraction > 0.0)
                //{
                //    List<object> output = GlobalSettings.Instance.DebugList(Index, DebugOutputs.StandGpp);
                //    output.AddRange(new object[] { Species[speciesIndex].Species.ID,  Index,  ID,
                //                                   Species[speciesIndex].LaiFraction,  Species[speciesIndex].BiomassGrowth.GppPerArea, 
                //                                   ProductiveArea * Species[speciesIndex].LaiFraction * Species[speciesIndex].BiomassGrowth.GppPerArea, AverageAging,  
                //                                   Species[speciesIndex].BiomassGrowth.EnvironmentalFactor });
                //}
            }
        }

        public void CalculateInterceptedArea()
        {
            if (mAggregatedLR == 0.0)
            {
                EffectiveAreaPerWla = 0.0;
                return;
            }
            Debug.Assert(mAggregatedLR > 0.0);
            EffectiveAreaPerWla = PhotosyntheticallyActiveArea / mAggregatedLR;
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("RU: aggregated lightresponse: " + mAggregatedLR + " eff.area./wla: " + mEffectiveArea_perWLA);
            //}
        }

        // function is called immediately before the growth of individuals
        public void BeforeGrow()
        {
            AverageAging = 0.0;
        }

        // function is called after finishing the indivdual growth / mortality.
        public void AfterGrow()
        {
            AverageAging = TotalLeafArea > 0.0 ? AverageAging / TotalLeafArea : 0; // calculate aging value (calls to addAverageAging() by individual trees)
            if (AverageAging > 0.0 && AverageAging < 0.00001)
            {
                Debug.WriteLine("ru " + Index + " aging <0.00001");
            }
            if (AverageAging < 0.0 || AverageAging > 1.0)
            {
                Debug.WriteLine("Average aging invalid: (RU, LAI): " + Index + Statistics.LeafAreaIndex);
            }
        }

        public void YearEnd(Model model)
        {
            // calculate statistics for all tree species of the ressource unit
            int c = Species.Count;
            for (int i = 0; i < c; i++)
            {
                Species[i].StatisticsDead.Calculate(); // calculate the dead trees
                Species[i].StatisticsMgmt.Calculate(); // stats of removed trees
                Species[i].UpdateGwl(); // get sum of dead trees (died + removed)
                Species[i].Statistics.Calculate(); // calculate the living (and add removed volume to gwl)
                Statistics.Add(Species[i].Statistics);
            }
            Statistics.Calculate(); // aggreagte on stand level

            // update carbon flows
            if (Soil != null && model.ModelSettings.CarbonCycleEnabled)
            {
                double area_factor = StockableArea / Constant.RUArea; //conversion factor
                Variables.CarbonUptake = Statistics.Npp * Constant.BiomassCFraction;
                Variables.CarbonUptake += Statistics.NppSaplings * Constant.BiomassCFraction;

                double to_atm = Snags.FluxToAtmosphere.C / area_factor; // from snags, kgC/ha
                to_atm += Soil.FluxToAtmosphere.C * Constant.RUArea / 10.0; // soil: t/ha . t/m2 . kg/ha
                Variables.CarbonToAtm = to_atm;

                double to_dist = Snags.FluxToDisturbance.C / area_factor;
                to_dist += Soil.FluxToDisturbance.C * Constant.RUArea / 10.0;
                double to_harvest = Snags.FluxToExtern.C / area_factor;

                Variables.Nep = Variables.CarbonUptake - to_atm - to_dist - to_harvest; // kgC/ha

                // incremental values....
                Variables.CumCarbonUptake += Variables.CarbonUptake;
                Variables.CumCarbonToAtm += Variables.CarbonToAtm;
                Variables.CumNep += Variables.Nep;
            }
        }

        public void AddTreeAgingForAllTrees(Model model)
        {
            AverageAging = 0.0;
            foreach (Tree t in Trees)
            {
                AddTreeAging(t.LeafArea, t.Species.Aging(model, t.Height, t.Age));
            }
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "yearEnd()" above!!!
        public void CreateStandStatistics(Model model)
        {
            // clear statistics (ru-level and ru-species level)
            Statistics.Clear();
            for (int i = 0; i < Species.Count; i++)
            {
                Species[i].Statistics.Clear();
                Species[i].StatisticsDead.Clear();
                Species[i].StatisticsMgmt.Clear();
                Species[i].SaplingStats.ClearStatistics();
            }

            // add all trees to the statistics objects of the species
            foreach (Tree t in Trees)
            {
                if (!t.IsDead())
                {
                    ResourceUnitSpecies(t.Species).Statistics.Add(t, null);
                }
            }
            // summarise sapling stats
            if (model.Saplings != null)
            {
                model.Saplings.CalculateInitialStatistics(this);
            }

            // summarize statistics for the whole resource unit
            for (int i = 0; i < Species.Count; i++)
            {
                Species[i].SaplingStats.Calculate(Species[i].Species, this, model.GlobalSettings);
                Species[i].Statistics.Add(Species[i].SaplingStats);
                Species[i].Statistics.Calculate();
                Statistics.Add(Species[i].Statistics);
            }
            Statistics.Calculate();
            AverageAging = Statistics.LeafAreaIndex > 0.0 ? AverageAging / (Statistics.LeafAreaIndex * StockableArea) : 0.0;
            if (AverageAging < 0.0 || AverageAging > 1.0)
            {
                Debug.WriteLine("Average aging invalid: (RU, LAI): " + Index + Statistics.LeafAreaIndex);
            }
        }

        /** recreate statistics. This is necessary after events that changed the structure
            of the stand *after* the growth of trees (where stand statistics are updated).
            An example is after disturbances.  */
        public void RecreateStandStatistics(bool recalculate_stats)
        {
            // when called after disturbances (recalculate_stats=false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int i = 0; i < Species.Count; i++)
            {
                if (recalculate_stats)
                {
                    Species[i].Statistics.Clear();
                }
                else
                {
                    Species[i].Statistics.ClearOnlyTrees();
                }
            }
            foreach (Tree t in Trees)
            {
                ResourceUnitSpecies(t.Species).Statistics.Add(t, null);
            }

            if (recalculate_stats)
            {
                for (int i = 0; i < Species.Count; i++)
                {
                    Species[i].Statistics.Calculate();
                }
            }
        }

        public void CalculateCarbonCycle(Model model)
        {
            if (Snags == null) // TODO: what about other pools?
            {
                return;
            }

            // (1) calculate the snag dynamics
            // because all carbon/nitrogen-flows from trees to the soil are routed through the snag-layer,
            // all soil inputs (litter + deadwood) are collected in the Snag-object.
            Snags.CalculateYear(model);
            Soil.ClimateFactor = Snags.ClimateFactor; // the climate factor is only calculated once
            Soil.SetSoilInput(Snags.LabileFlux, Snags.RefractoryFlux);
            Soil.CalculateYear(); // update the ICBM/2N model
                                    // use available nitrogen?
            if (model.ModelSettings.UseDynamicAvailableNitrogen)
            {
                Variables.NitrogenAvailable = Soil.AvailableNitrogen;
            }

            // debug output
            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.CarbonCycle) && !Snags.IsEmpty())
            //{
            //    List<object> output = GlobalSettings.Instance.DebugList(Index, DebugOutputs.CarbonCycle);
            //    output.Add(new object[] { Index, ID, // resource unit index and id
            //                              Snags.DebugList(), // snag debug outs
            //                              Soil.DebugList() }); // ICBM/2N debug outs
            //}
        }
    }
}
