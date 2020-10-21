using iLand.Simulation;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
{
    /** @class ResourceUnit
        ResourceUnit is the spatial unit that encapsulates a forest stand and links to several environmental components
        (Climate, Soil, Water, ...).
        A resource unit has a size of (currently) 100x100m. Many processes in iLand operate on the level of a ResourceUnit.
        Each resource unit has the same Climate and other properties (e.g. available nitrogen).
        Proceses on this level are, inter alia, NPP Production (see Production3PG), water calculations (WaterCycle), the modeling
        of dead trees (Snag) and soil processes (Soil).
        */
    public class ResourceUnit
    {
        private float mTotalWeightedLeafArea; // sum of lightResponse * LeafArea for all trees
        private float mAggregatedLR; // sum of lightresponse*LA of the current unit
        private int mNextTreeID;
        private int mHeightCells; // count of (Heightgrid) pixels thare are inside the RU
        private int mHeightCellsWithTrees;  // count of pixels that are stocked with trees

        public float AverageAging { get; private set; } // leaf area weighted average aging
        public RectangleF BoundingBox { get; set; }
        public Climate Climate { get; set; } // link to the climate on this resource unit
        public Point TopLeftLightOffset { get; set; } // coordinates on the LIF grid of the upper left corner of the RU
        public float EffectiveAreaPerWla { get; private set; } ///<
        public bool HasDeadTrees { get; private set; } // if true, the resource unit has dead trees and needs maybe some cleanup
        public int ID { get; set; }
        public int Index { get; private set; }
        public float LriModifier { get; private set; }
        public float PhotosyntheticallyActiveArea { get; private set; } // TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public Snag Snags { get; private set; } // access the snag object
        public Soil Soil { get; private set; } // access the soil model
        public List<ResourceUnitSpecies> Species { get; private set; }
        public SpeciesSet SpeciesSet { get; private set; } // get SpeciesSet this RU links to.
        public SaplingCell[] SaplingCells { get; private set; } // access the array of sapling-cells
        public StandStatistics Statistics { get; private set; }
        public float StockedArea { get; private set; } // get the stocked area in m2
        public float StockableArea { get; set; } // total stockable area in m2
        public float TotalLeafArea { get; private set; } // total leaf area of resource unit (m2)
        public Dictionary<string, Trees> TreesBySpeciesID { get; private set; } // reference to the tree list.
        public ResourceUnitVariables Variables { get; private set; } // access to variables that are specific to resourceUnit (e.g. nitrogenAvailable)
        public WaterCycle WaterCycle { get; private set; } // water model of the unit

        public float HeightArea() { return mHeightCells * Constant.HeightPixelArea; } // get the resource unit area in m2
        public float InterceptedArea(float leafArea, float lightResponse) { return EffectiveAreaPerWla * leafArea * lightResponse; }
        public float LeafAreaIndex() { return StockableArea != 0.0F ? TotalLeafArea / StockableArea : 0.0F; } // Total Leaf Area Index

        public ResourceUnitSpecies ResourceUnitSpecies(int speciesIndex) { return this.Species[speciesIndex]; } // get RU-Species-container with index 'species_index' from the RU
        public void SnagNewYear() { if (this.Snags != null) this.Snags.NewYear(); } // clean transfer pools
        public void TreeDied() { HasDeadTrees = true; } // sets the flag that indicates that the resource unit contains dead trees

        public ResourceUnit(int index)
        {
            this.mAggregatedLR = 0.0F;
            this.mTotalWeightedLeafArea = 0.0F;
            this.mNextTreeID = 0;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;

            this.Climate = null;
            this.EffectiveAreaPerWla = 0.0F;
            this.ID = 0;
            this.Index = index;
            this.LriModifier = 0.0F;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.Species = new List<ResourceUnitSpecies>();
            this.SaplingCells = null;
            this.Snags = null;
            this.Soil = null;
            this.SpeciesSet = null;
            this.Statistics = new StandStatistics();
            this.StockableArea = 0.0F;
            this.StockedArea = 0.0F;
            this.TotalLeafArea = 0.0F;
            this.TreesBySpeciesID = new Dictionary<string, Trees>();
            this.Variables = new ResourceUnitVariables();
            this.WaterCycle = new WaterCycle();
        }

        public void Setup(Model model)
        {
            this.WaterCycle.Setup(model, this);

            this.Snags = null;
            this.Soil = null;
            if (model.ModelSettings.CarbonCycleEnabled)
            {
                this.Soil = new Soil(model, this);
                this.Snags = new Snag();
                // class size of snag classes
                // swdDBHClass12: class break between classes 1 and 2 for standing snags(dbh, cm)
                // swdDBHClass23: class break between classes 2 and 3 for standing snags(dbh, cm)
                this.Snags.SetupThresholds(model.Project.Model.Settings.Soil.SwdDbhClass12,
                                           model.Project.Model.Settings.Soil.SwdDdhClass23);
                this.Snags.Setup(model, this); // must call SetupThresholds() first
            }

            if (model.ModelSettings.RegenerationEnabled)
            {
                this.SaplingCells = new SaplingCell[Constant.LightCellsPerHectare];
                for (int cellIndex = 0; cellIndex < this.SaplingCells.Length; ++cellIndex)
                {
                    // TODO: SoA
                    this.SaplingCells[cellIndex] = new SaplingCell();
                }
            }

            // if dynamic coupling of soil nitrogen is enabled, a starting value for available N is calculated
            // TODO: but starting values are in the environment file?
            //if (this.Soil != null && model.ModelSettings.UseDynamicAvailableNitrogen && model.ModelSettings.CarbonCycleEnabled)
            //{
            //    this.Soil.ClimateDecompositionFactor = 1.0; // TODO: why is this set to 1.0 without restoring the original value?
            //    this.Soil.CalculateYear(); // BUGBUG: doesn't just calculate nitrogen, runs a year of decomposition on pools?
            //}

            this.AverageAging = 0.0F;
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
        public void AddTreeAging(float leafArea, float agingFactor) { AverageAging += leafArea * agingFactor; } // aggregate the tree aging values (weighted by leaf area)

        /// AddWeightedLeafArea() is called by each tree to aggregate the total weighted leaf area on a unit
        public void AddWeightedLeafArea(float leafArea, float lri) 
        { 
            mTotalWeightedLeafArea += leafArea * lri; 
            TotalLeafArea += leafArea; 
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

        public ResourceUnitSpecies GetSpecies(Species species)
        {
            return this.Species[species.Index];
        }

        public int AddTree(Model model, string speciesID)
        {
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out Trees treesOfSpecies) == false)
            {
                int speciesIndex = -1;
                foreach (ResourceUnitSpecies species in this.Species)
                {
                    if (String.Equals(speciesID, species.Species.ID))
                    {
                        speciesIndex = species.Species.Index;
                        break;
                    }
                }
                if (speciesIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(speciesID));
                }

                treesOfSpecies = new Trees(model, this)
                {
                    Species = this.ResourceUnitSpecies(speciesIndex).Species
                };
                this.TreesBySpeciesID.Add(speciesID, treesOfSpecies);
            }
            
            int treeIndex = treesOfSpecies.Count;
            treesOfSpecies.Add();
            treesOfSpecies.ID[treeIndex] = this.mNextTreeID++;
            return treeIndex;
        }

        /// remove dead trees from tree list
        /// reduce size of vector if lots of space is free
        /// tests showed that this way of cleanup is very fast,
        /// because no memory allocations are performed (simple memmove())
        /// when trees are moved.
        public void RemoveDeadTrees()
        {
            if (this.HasDeadTrees == false)
            {
                return;
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                int lastLiveTreeIndex;
                for (lastLiveTreeIndex = treesOfSpecies.Count - 1; lastLiveTreeIndex >= 0 && treesOfSpecies.IsDead(lastLiveTreeIndex); --lastLiveTreeIndex)
                {
                }

                int overwriteIndex = 0;
                while (overwriteIndex < lastLiveTreeIndex)
                {
                    if (treesOfSpecies.IsDead(overwriteIndex))
                    {
                        treesOfSpecies.Copy(lastLiveTreeIndex, overwriteIndex); // copy data!
                        --lastLiveTreeIndex; //
                        while (lastLiveTreeIndex >= overwriteIndex && treesOfSpecies.IsDead(lastLiveTreeIndex))
                        {
                            --lastLiveTreeIndex;
                        }
                    }
                    ++overwriteIndex;
                }
                ++lastLiveTreeIndex; // last points now to the first dead tree

                // free resources
                if (lastLiveTreeIndex != treesOfSpecies.Count)
                {
                    treesOfSpecies.RemoveRange(lastLiveTreeIndex, treesOfSpecies.Count - lastLiveTreeIndex); // BUGBUG: assumes dead trees are at end of list
                    if (treesOfSpecies.Count == 0)
                    {
                        this.TreesBySpeciesID.Remove(treesOfSpecies.Species.ID);
                    }
                    else if (treesOfSpecies.Capacity > 100)
                    {
                        if (((double)treesOfSpecies.Count / (double)treesOfSpecies.Capacity) < 0.2)
                        {
                            //int target_size = mTrees.Count*2;
                            //Debug.WriteLine("reduce size from "+mTrees.Capacity + "to" + target_size;
                            //mTrees.reserve(qMax(target_size, 100));
                            //if (GlobalSettings.Instance.LogDebug())
                            //{
                            //    Debug.WriteLine("reduce tree storage of RU " + Index + " from " + Trees.Capacity + " to " + Trees.Count);
                            //}
                            treesOfSpecies.Capacity = treesOfSpecies.Count;
                        }
                    }
                }
            }

            this.HasDeadTrees = false;
        }

        public void NewYear()
        {
            this.mAggregatedLR = 0.0F;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;
            this.mTotalWeightedLeafArea = 0.0F;

            this.PhotosyntheticallyActiveArea = 0.0F;
            this.TotalLeafArea = 0.0F;

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
                PhotosyntheticallyActiveArea = 0.0F;
                StockedArea = 0.0F;
                return;
            }

            // the pixel counters are filled during the height-grid-calculations
            StockedArea = Constant.HeightSize * Constant.HeightSize * mHeightCellsWithTrees; // m2 (1 height grid pixel = 10x10m)
            if (LeafAreaIndex() < 3.0)
            {
                // estimate stocked area based on crown projections
                double totalCrownArea = 0.0;
                foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
                {
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        if (treesOfSpecies.IsDead(treeIndex) == false)
                        {
                            totalCrownArea += treesOfSpecies.Stamp[treeIndex].Reader.CrownArea;
                        }
                    }
                }
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("crown area: lai " + LeafAreaIndex() + " stocked area (pixels) " + StockedArea + " area (crown) " + totalCrownArea);
                //}
                if (LeafAreaIndex() < 1.0)
                {
                    this.StockedArea = MathF.Min((float)totalCrownArea, this.StockedArea);
                }
                else
                {
                    // for LAI between 1 and 3:
                    // interpolate between sum of crown area of trees (at LAI=1) and the pixel-based value (at LAI=3 and above)
                    // only results in a change if crown area is less than stocked area
                    // BUGBUG: assumes trees are homogeneously distributed across resource unit and that crowns don't overlap
                    float px_frac = (LeafAreaIndex() - 1.0F) / 2.0F; // 0 at LAI=1, 1 at LAI=3
                    this.StockedArea = this.StockedArea * px_frac + MathF.Min((float)totalCrownArea, StockedArea) * (1.0F - px_frac);
                }
                if (this.StockedArea == 0.0)
                {
                    return;
                }
            }

            // calculate the leaf area index (LAI)
            float leafAreaIndex = this.TotalLeafArea / this.StockedArea;
            // calculate the intercepted radiation fraction using the law of Beer Lambert
            float k = model.ModelSettings.LightExtinctionCoefficient;
            float lightInterceptionfraction = 1.0F - MathF.Exp(-k * leafAreaIndex);
            PhotosyntheticallyActiveArea = StockedArea * lightInterceptionfraction; // m2

            // calculate the total weighted leaf area on this RU:
            this.LriModifier = (float)(this.PhotosyntheticallyActiveArea / mTotalWeightedLeafArea); // p_WLA
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
                EffectiveAreaPerWla = 0.0F;
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
            AverageAging = 0.0F;
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
            for (int species = 0; species < this.Species.Count; species++)
            {
                this.Species[species].StatisticsDead.Calculate(); // calculate the dead trees
                this.Species[species].StatisticsMgmt.Calculate(); // stats of removed trees
                this.Species[species].UpdateGwl(); // get sum of dead trees (died + removed)
                this.Species[species].Statistics.Calculate(); // calculate the living (and add removed volume to gwl)
                this.Statistics.Add(Species[species].Statistics);
            }
            this.Statistics.Calculate(); // aggreagte on stand level

            // update carbon flows
            if (this.Soil != null && model.ModelSettings.CarbonCycleEnabled)
            {
                double area_factor = this.StockableArea / Constant.RUArea; //conversion factor
                this.Variables.Npp = this.Statistics.Npp * Constant.BiomassCFraction;
                this.Variables.Npp += this.Statistics.NppSaplings * Constant.BiomassCFraction;

                double to_atm = this.Snags.FluxToAtmosphere.C / area_factor; // from snags, kgC/ha
                to_atm += this.Soil.FluxToAtmosphere.C * Constant.RUArea / 10.0; // soil: t/ha . t/m2 . kg/ha
                this.Variables.CarbonToAtmosphere = to_atm;

                double to_dist = this.Snags.FluxToDisturbance.C / area_factor;
                to_dist += this.Soil.FluxToDisturbance.C * Constant.RUArea / 10.0;
                double to_harvest = this.Snags.FluxToExtern.C / area_factor;

                this.Variables.Nep = this.Variables.Npp - to_atm - to_dist - to_harvest; // kgC/ha

                // incremental values....
                this.Variables.TotalNpp += this.Variables.Npp;
                this.Variables.TotalCarbonToAtmosphere += this.Variables.CarbonToAtmosphere;
                this.Variables.TotalNep += this.Variables.Nep;
            }
        }

        public void AddTreeAgingForAllTrees(Model model)
        {
            this.AverageAging = 0.0F;
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.AddTreeAging(treesOfSpecies.LeafArea[treeIndex], treesOfSpecies.Species.Aging(model, treesOfSpecies.Height[treeIndex], treesOfSpecies.Age[treeIndex]));
                }
            }
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "yearEnd()" above!!!
        public void CreateStandStatistics(Model model)
        {
            // clear statistics (ru-level and ru-species level)
            this.Statistics.Clear();
            for (int species = 0; species < this.Species.Count; species++)
            {
                this.Species[species].Statistics.Clear();
                this.Species[species].StatisticsDead.Clear();
                this.Species[species].StatisticsMgmt.Clear();
                this.Species[species].SaplingStats.ClearStatistics();
            }

            // add all trees to the statistics objects of the species
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.GetSpecies(treesOfSpecies.Species).Statistics.Add(treesOfSpecies, treeIndex, null, skipDead: true);
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
                Species[i].SaplingStats.Calculate(Species[i].Species, this, model);
                Species[i].Statistics.Add(Species[i].SaplingStats);
                Species[i].Statistics.Calculate();
                Statistics.Add(Species[i].Statistics);
            }
            Statistics.Calculate();
            this.AverageAging = this.Statistics.LeafAreaIndex > 0.0 ? AverageAging / ((float)Statistics.LeafAreaIndex * StockableArea) : 0.0F;
            if (this.AverageAging < 0.0F || this.AverageAging > 1.0F)
            {
                throw new OverflowException("Average aging invalid: (RU, LAI): " + Index + Statistics.LeafAreaIndex);
            }
        }

        /** recreate statistics. This is necessary after events that changed the structure
            of the stand *after* the growth of trees (where stand statistics are updated).
            An example is after disturbances.  */
        public void RecreateStandStatistics(bool recalculateSpecies)
        {
            // when called after disturbances (recalculate_stats=false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int species = 0; species < Species.Count; species++)
            {
                if (recalculateSpecies)
                {
                    this.Species[species].Statistics.Clear();
                }
                else
                {
                    this.Species[species].Statistics.ClearOnlyTrees();
                }
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.ResourceUnitSpecies(treesOfSpecies.Species.Index).Statistics.Add(treesOfSpecies, treeIndex, null);
                }
            }

            if (recalculateSpecies)
            {
                for (int species = 0; species < Species.Count; species++)
                {
                    this.Species[species].Statistics.Calculate();
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
            Soil.ClimateDecompositionFactor = this.Snags.ClimateFactor; // the climate factor is only calculated once
            Soil.SetSoilInput(Snags.LabileFlux, Snags.RefractoryFlux);
            Soil.CalculateYear(); // update the ICBM/2N model

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
