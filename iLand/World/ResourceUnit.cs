using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Model = iLand.Simulation.Model;

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
        public ResourceUnitCarbonFluxes CarbonCycle { get; private set; }
        public Climate Climate { get; set; } // link to the climate on this resource unit
        public Point TopLeftLightPosition { get; set; } // coordinates on the LIF grid of the upper left corner of the RU
        public float EffectiveAreaPerWla { get; private set; } ///<
        public bool HasDeadTrees { get; private set; } // if true, the resource unit has dead trees and needs maybe some cleanup
        public int EnvironmentID { get; set; }
        public int GridIndex { get; private set; }
        public float LriModifier { get; private set; }
        public float PhotosyntheticallyActiveArea { get; private set; } // TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public Tree.Snags Snags { get; private set; } // access the snag object
        public Soil Soil { get; private set; } // access the soil model
        public SaplingCell[] SaplingCells { get; private set; } // access the array of sapling-cells
        public ResourceUnitSpeciesStatistics Statistics { get; private set; }
        public float StockedArea { get; private set; } // get the stocked area in m2
        public float StockableArea { get; set; } // total stockable area in m2
        public float TotalLeafArea { get; private set; } // total leaf area of resource unit (m2)
        public Dictionary<string, Trees> TreesBySpeciesID { get; private set; } // reference to the tree list.
        public List<ResourceUnitSpecies> TreeSpecies { get; private set; }
        public TreeSpeciesSet TreeSpeciesSet { get; private set; } // get SpeciesSet this RU links to.
        public WaterCycle WaterCycle { get; private set; } // water model of the unit

        public float GetHeightCellArea() { return this.mHeightCells * Constant.HeightPixelArea; } // get the resource unit area in m2 TODO: when would this ever not be 1 ha?
        public float GetInterceptedArea(float leafArea, float lightResponse) { return this.EffectiveAreaPerWla * leafArea * lightResponse; }
        public float GetLeafAreaIndex() { return this.StockableArea != 0.0F ? this.TotalLeafArea / this.StockableArea : 0.0F; } // Total Leaf Area Index

        public ResourceUnitSpecies GetResourceUnitSpecies(int speciesIndex) { return this.TreeSpecies[speciesIndex]; } // get RU-Species-container with index 'species_index' from the RU
        public void OnTreeDied() { this.HasDeadTrees = true; } // sets the flag that indicates that the resource unit contains dead trees

        public ResourceUnit(Project projectFile, int index)
        {
            this.mAggregatedLR = 0.0F;
            this.mTotalWeightedLeafArea = 0.0F;
            this.mNextTreeID = 0;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;

            this.Climate = null;
            this.EffectiveAreaPerWla = 0.0F;
            this.EnvironmentID = 0;
            this.GridIndex = index;
            this.LriModifier = 0.0F;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.TreeSpecies = new List<ResourceUnitSpecies>();
            this.SaplingCells = null;
            this.Snags = null;
            this.Soil = null;
            this.TreeSpeciesSet = null;
            this.Statistics = new ResourceUnitSpeciesStatistics();
            this.StockableArea = 0.0F;
            this.StockedArea = 0.0F;
            this.TotalLeafArea = 0.0F;
            this.TreesBySpeciesID = new Dictionary<string, Trees>();
            this.CarbonCycle = new ResourceUnitCarbonFluxes();
            this.WaterCycle = new WaterCycle(projectFile);
        }

        public void Setup(Project projectFile, EnvironmentReader environmentReader)
        {
            this.WaterCycle.Setup(projectFile, environmentReader, this);

            if (projectFile.Model.Settings.CarbonCycleEnabled)
            {
                this.Soil = new Soil(environmentReader, this);
                this.Snags = new Tree.Snags();
                // class size of snag classes
                // swdDBHClass12: class break between classes 1 and 2 for standing snags(dbh, cm)
                // swdDBHClass23: class break between classes 2 and 3 for standing snags(dbh, cm)
                this.Snags.SetupThresholds(projectFile.Model.Settings.Soil.SwdDbhClass12,
                                           projectFile.Model.Settings.Soil.SwdDdhClass23);
                this.Snags.Setup(projectFile, this); // must call SetupThresholds() first
            }

            if (projectFile.Model.Settings.RegenerationEnabled)
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
        public void CountHeightCell(bool cellHasTrees) 
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
            int ix = lifCoords.X % Constant.LightCellsPerRUsize;
            int iy = lifCoords.Y % Constant.LightCellsPerRUsize;
            int i = iy * Constant.LightCellsPerRUsize + ix;
            Debug.Assert(i >= 0 && i < Constant.LightCellsPerHectare);
            return SaplingCells[i];
        }

        /// set species and setup the species-per-RU-data
        public void SetSpeciesSet(TreeSpeciesSet speciesSet)
        {
            this.TreeSpeciesSet = speciesSet;
            this.TreeSpecies.Clear();

            //mRUSpecies.Capacity = set.count(); // ensure that the vector space is not relocated
            for (int index = 0; index < speciesSet.SpeciesCount(); index++)
            {
                // TODO: this is an unnecessarily complex way of enumerating over all species in the species set
                TreeSpecies species = this.TreeSpeciesSet.GetSpecies(index);
                if (species == null)
                {
                    throw new NotSupportedException("Species index " + index + " not found.");
                }

                ResourceUnitSpecies ruSpecies = new ResourceUnitSpecies(species, this);
                this.TreeSpecies.Add(ruSpecies);
                /* be careful: setup() is called with a pointer somewhere to the content of the mRUSpecies container.
                   If the container memory is relocated (List), the pointer gets invalid!!!
                   Therefore, a resize() is called before the loop (no resize()-operations during the loop)! */
                //mRUSpecies[i].setup(s,this); // setup this element
            }
        }

        public ResourceUnitSpecies GetResourceUnitSpecies(TreeSpecies species)
        {
            return this.TreeSpecies[species.Index];
        }

        public int AddTree(Landscape landscape, string speciesID)
        {
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out Trees treesOfSpecies) == false)
            {
                int speciesIndex = -1;
                foreach (ResourceUnitSpecies species in this.TreeSpecies)
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

                treesOfSpecies = new Trees(landscape, this)
                {
                    Species = this.GetResourceUnitSpecies(speciesIndex).Species
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

        public void OnStartYear()
        {
            this.mAggregatedLR = 0.0F;
            this.mHeightCells = 0;
            this.mHeightCellsWithTrees = 0;
            this.mTotalWeightedLeafArea = 0.0F;

            this.PhotosyntheticallyActiveArea = 0.0F;
            this.TotalLeafArea = 0.0F;

            if (this.Snags != null)
            {
                // clean transfer pools
                this.Snags.OnStartYear();
            }
            if (this.Soil != null)
            {
                this.Soil.OnStartYear();
            }
            // clear statistics global and per species...
            this.Statistics.Zero();
            foreach (ResourceUnitSpecies ruSpecies in this.TreeSpecies)
            {
                ruSpecies.StatisticsDead.Zero();
                ruSpecies.StatisticsManagement.Zero();
            }
        }

        /** production() is the "stand-level" part of the biomass production (3PG).
            - The amount of radiation intercepted by the stand is calculated
            - the water cycle is calculated
            - statistics for each species are cleared
            - The 3PG production for each species and ressource unit is called (calculates species-responses and NPP production)
            see also: http://iland.boku.ac.at/individual+tree+light+availability */
        public void CalculateWaterAndBiomassGrowthForYear(Model model)
        {
            if (this.mTotalWeightedLeafArea == 0.0 || this.mHeightCells == 0)
            {
                // clear statistics of resourceunitspecies
                for (int species = 0; species < this.TreeSpecies.Count; ++species)
                {
                    this.TreeSpecies[species].Statistics.Zero();
                }
                this.PhotosyntheticallyActiveArea = 0.0F;
                this.StockedArea = 0.0F;
                return;
            }

            // the pixel counters are filled during the height-grid-calculations
            this.StockedArea = Constant.HeightSize * Constant.HeightSize * this.mHeightCellsWithTrees; // m2 (1 height grid pixel = 10x10m)
            if (this.GetLeafAreaIndex() < 3.0)
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
                if (GetLeafAreaIndex() < 1.0)
                {
                    this.StockedArea = MathF.Min((float)totalCrownArea, this.StockedArea);
                }
                else
                {
                    // for LAI between 1 and 3:
                    // interpolate between sum of crown area of trees (at LAI=1) and the pixel-based value (at LAI=3 and above)
                    // only results in a change if crown area is less than stocked area
                    // BUGBUG: assumes trees are homogeneously distributed across resource unit and that crowns don't overlap
                    float px_frac = (this.GetLeafAreaIndex() - 1.0F) / 2.0F; // 0 at LAI=1, 1 at LAI=3
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
            float k = model.Project.Model.Settings.LightExtinctionCoefficient;
            float lightInterceptionfraction = 1.0F - MathF.Exp(-k * leafAreaIndex);
            this.PhotosyntheticallyActiveArea = this.StockedArea * lightInterceptionfraction; // m2

            // calculate the total weighted leaf area on this RU:
            this.LriModifier = (float)(this.PhotosyntheticallyActiveArea / mTotalWeightedLeafArea); // p_WLA
            Debug.Assert(this.LriModifier >= 0.0F && LriModifier < 2.0F); // sanity upper bound
            //if (this.LriModifier == 0.0F)
            //{
            //    Debug.WriteLine("lri modification==0!");
            //}
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine(String.Format("production: LAI: {0} (intercepted fraction: {1}, stocked area: {3}). LRI-Multiplier: {2}",
            //                                  LAI, interception_fraction, LriModifier, StockedArea));
            //}

            // calculate LAI fractions
            float allSpeciesLeafAreaIndex = this.GetLeafAreaIndex(); // TODO: should this be the same as two LAI calculations above?
            if (allSpeciesLeafAreaIndex < 1.0F)
            {
                allSpeciesLeafAreaIndex = 1.0F;
            }
            // note: LAIFactors are only 1 if sum of LAI is > 1.0 (see WaterCycle)
            for (int ruSpeciesIndex = 0; ruSpeciesIndex < this.TreeSpecies.Count; ++ruSpeciesIndex)
            {
                float speciesLeafAreaFraction = this.TreeSpecies[ruSpeciesIndex].Statistics.LeafAreaIndex / allSpeciesLeafAreaIndex;
                if (speciesLeafAreaFraction > 1.000001F) // allow numerical error
                {
                    ResourceUnitSpecies ruSpecies = TreeSpecies[ruSpeciesIndex];
                    throw new NotSupportedException(ruSpecies.Species.Name + " (index " + ruSpecies.RU.GridIndex + ") leaf area exceeds area of all species in resource unit.");
                }
                this.TreeSpecies[ruSpeciesIndex].SetRULaiFraction(speciesLeafAreaFraction);
            }

            // soil water model - this determines soil water contents needed for response calculations
            WaterCycleData hydrologicState = this.WaterCycle.RunYear(model.Project);
            model.Modules.CalculateWater(this, hydrologicState);

            // invoke species specific calculation (3PG)
            for (int speciesIndex = 0; speciesIndex < TreeSpecies.Count; ++speciesIndex)
            {
                this.TreeSpecies[speciesIndex].CalculateBiomassGrowthForYear(model.Project, fromEstablishment: false); // CALCULATE 3PG

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
                this.EffectiveAreaPerWla = 0.0F;
                return;
            }
            Debug.Assert(mAggregatedLR > 0.0);
            this.EffectiveAreaPerWla = this.PhotosyntheticallyActiveArea / mAggregatedLR;
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("RU: aggregated lightresponse: " + mAggregatedLR + " eff.area./wla: " + mEffectiveArea_perWLA);
            //}
        }

        // function is called immediately before the growth of individuals
        public void BeforeTreeGrowth()
        {
            this.AverageAging = 0.0F;
        }

        // function is called after finishing the individual growth / mortality.
        public void AfterTreeGrowth()
        {
            this.AverageAging = this.TotalLeafArea > 0.0F ? this.AverageAging / this.TotalLeafArea : 0.0F; // calculate aging value (calls to addAverageAging() by individual trees)
            if ((this.AverageAging > 0.0F) && (this.AverageAging < 0.00001F))
            {
                Debug.WriteLine("RU-index " + this.GridIndex + " average aging < 0.00001.");
            }
            if ((this.AverageAging < 0.0F) || (this.AverageAging > 1.0F))
            {
                throw new ArithmeticException("Average aging invalid: RU-index " + this.GridIndex + ", LAI " + this.Statistics.LeafAreaIndex);
            }
        }

        public void OnEndYear()
        {
            // calculate statistics for all tree species of the ressource unit
            for (int species = 0; species < this.TreeSpecies.Count; ++species)
            {
                this.TreeSpecies[species].StatisticsDead.CalculateFromAccumulatedValues(); // calculate the dead trees
                this.TreeSpecies[species].StatisticsManagement.CalculateFromAccumulatedValues(); // stats of removed trees
                this.TreeSpecies[species].UpdateGwl(); // get sum of dead trees (died + removed)
                this.TreeSpecies[species].Statistics.CalculateFromAccumulatedValues(); // calculate the living (and add removed volume to gwl)
                this.Statistics.Add(this.TreeSpecies[species].Statistics);
            }
            this.Statistics.CalculateFromAccumulatedValues(); // aggregate on stand level

            // update carbon flows
            if (this.Soil != null)
            {
                this.CarbonCycle.Npp = this.Statistics.Npp * Constant.BiomassCFraction;
                this.CarbonCycle.Npp += this.Statistics.NppSaplings * Constant.BiomassCFraction;

                double area_factor = this.StockableArea / Constant.RUArea; //conversion factor
                double to_atm = this.Snags.FluxToAtmosphere.C / area_factor; // from snags, kgC/ha
                to_atm += this.Soil.FluxToAtmosphere.C * Constant.RUArea / 10.0; // soil: t/ha * 0.0001 ha/m2 * 1000 kg/ton = 0.1 kg/m2
                this.CarbonCycle.CarbonToAtmosphere = to_atm;

                double to_dist = this.Snags.FluxToDisturbance.C / area_factor;
                to_dist += this.Soil.FluxToDisturbance.C * Constant.RUArea / 10.0;
                double to_harvest = this.Snags.FluxToExtern.C / area_factor;

                this.CarbonCycle.Nep = this.CarbonCycle.Npp - to_atm - to_dist - to_harvest; // kgC/ha

                // incremental values....
                this.CarbonCycle.TotalNpp += this.CarbonCycle.Npp;
                this.CarbonCycle.TotalCarbonToAtmosphere += this.CarbonCycle.CarbonToAtmosphere;
                this.CarbonCycle.TotalNep += this.CarbonCycle.Nep;
            }
        }

        public void AddTreeAgingForAllTrees()
        {
            this.AverageAging = 0.0F;
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.AddTreeAging(treesOfSpecies.LeafArea[treeIndex], treesOfSpecies.Species.GetAgingFactor(treesOfSpecies.Height[treeIndex], treesOfSpecies.Age[treeIndex]));
                }
            }
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "yearEnd()" above!!!
        public void CreateStandStatistics(Model model)
        {
            // clear statistics (ru-level and ru-species level)
            this.Statistics.Zero();
            for (int species = 0; species < this.TreeSpecies.Count; species++)
            {
                this.TreeSpecies[species].Statistics.Zero();
                this.TreeSpecies[species].StatisticsDead.Zero();
                this.TreeSpecies[species].StatisticsManagement.Zero();
                this.TreeSpecies[species].SaplingStats.ClearStatistics();
            }

            // add all trees to the statistics objects of the species
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.GetResourceUnitSpecies(treesOfSpecies.Species).Statistics.Add(treesOfSpecies, treeIndex, null, skipDead: true);
                }
            }

            // summarise sapling stats
            if (model.Landscape.Saplings != null)
            {
                model.Landscape.Saplings.CalculateInitialStatistics(this);
            }

            // summarize statistics for the whole resource unit
            for (int species = 0; species < TreeSpecies.Count; species++)
            {
                TreeSpecies[species].SaplingStats.Recalculate(model, this, TreeSpecies[species].Species);
                TreeSpecies[species].Statistics.Add(TreeSpecies[species].SaplingStats);
                TreeSpecies[species].Statistics.CalculateFromAccumulatedValues();
                Statistics.Add(TreeSpecies[species].Statistics);
            }
            Statistics.CalculateFromAccumulatedValues();
            this.AverageAging = this.Statistics.LeafAreaIndex > 0.0 ? AverageAging / ((float)Statistics.LeafAreaIndex * StockableArea) : 0.0F;
            if (this.AverageAging < 0.0F || this.AverageAging > 1.0F)
            {
                throw new OverflowException("Average aging invalid: (RU, LAI): " + GridIndex + Statistics.LeafAreaIndex);
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
            for (int species = 0; species < this.TreeSpecies.Count; species++)
            {
                if (recalculateSpecies)
                {
                    this.TreeSpecies[species].Statistics.Zero();
                }
                else
                {
                    this.TreeSpecies[species].Statistics.ZeroTreeStatistics();
                }
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.GetResourceUnitSpecies(treesOfSpecies.Species.Index).Statistics.Add(treesOfSpecies, treeIndex, null);
                }
            }

            if (recalculateSpecies)
            {
                for (int species = 0; species < TreeSpecies.Count; species++)
                {
                    this.TreeSpecies[species].Statistics.CalculateFromAccumulatedValues();
                }
            }
        }

        public void CalculateCarbonCycle()
        {
            // (1) calculate the snag dynamics
            // because all carbon/nitrogen-flows from trees to the soil are routed through the snag-layer,
            // all soil inputs (litter + deadwood) are collected in the Snag-object.
            if (this.Snags != null)
            {
                this.Snags.RunYear();
            }
            if (this.Soil != null)
            {
                this.Soil.ClimateDecompositionFactor = this.Snags.ClimateFactor; // the climate factor is only calculated once
                this.Soil.SetSoilInput(this.Snags.LabileFlux, this.Snags.RefractoryFlux);
                this.Soil.CalculateYear(); // update the ICBM/2N model
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
