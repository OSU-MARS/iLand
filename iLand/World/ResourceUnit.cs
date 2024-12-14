using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    /** ResourceUnit is the spatial unit that encapsulates a forest stand and links to several environmental components
        (weather, soil, water, ...).
        A resource unit has a size of (currently) 100x100m. Many processes in iLand operate on the level of a resource unit.
        Proceses on this level are, inter alia, NPP Production (see Production3-PG), water calculations (WaterCycle), the modeling
        of dead trees (Snags) and soil processes (Soil).
        */
    public class ResourceUnit
    {
        private int heightCellsWithTreesTallerThanRegenerationLayer;  // count of pixels that are stocked with trees
        private int saplingsRecruited;

        public float AreaInLandscapeInM2 { get; set; } // total stockable area in m² at height grid (10 m) resolution
        public float AreaWithTreesInM2 { get; private set; } // the stocked area in m² at height grid (10 m) resolution
        public int HeightCellsOnLandscape { get; set; } // count of on landscape height grid cells within the RU
        public UInt32 ID { get; set; }
        public RectangleF ProjectExtent { get; set; } ///< bounding box (metric) of the RU
        public int ResourceUnitGridIndex { get; private init; }

        public ResourceUnitCarbonFluxes? CarbonCycle { get; private set; }
        public Microclimate? Microclimate { get; private set; } ///< pointer to the microclimate-array
        public Point MinimumLightIndexXY { get; set; } // coordinates on the LIF grid of the upper left corner of the RU
        public ResourceUnitSnags? Snags { get; private set; }
        public ResourceUnitSoil? Soil { get; private set; }
        // TODO: should be ResourceUnitSaplings : Grid<SaplingCell> rather than an array
        public SaplingCell[]? SaplingCells { get; private set; }
        public ResourceUnitSvdState SvdState { get; private set; }
        public ResourceUnitTrees Trees { get; private init; }
        public ResourceUnitWaterCycle WaterCycle { get; private init; }
        public Weather Weather { get; private init; }

        public ResourceUnit(Project projectFile, Weather weather, TreeSpeciesSet speciesSet, int ruGridIndex)
        {
            this.heightCellsWithTreesTallerThanRegenerationLayer = 0;
            this.saplingsRecruited = 0;
            this.SvdState = new();

            this.AreaInLandscapeInM2 = 0.0F;
            this.AreaWithTreesInM2 = 0.0F;
            this.CarbonCycle = null;
            this.HeightCellsOnLandscape = 0;
            this.ID = 0;
            this.Microclimate = null;
            this.ResourceUnitGridIndex = ruGridIndex;
            this.SaplingCells = null;
            this.Snags = null;
            this.Soil = null;
            this.Trees = new(this, speciesSet);
            this.WaterCycle = new(projectFile, this, weather.TimeSeries.GetMaximumTimestepsPerYear());
            this.Weather = weather;
        }

        // C++/core/saplings.cpp: Saplings::addSprout()
        public void AddSprout(Model model, TreeListSpatial trees, int treeIndex, bool treeIsRemoved)
        {
            if (trees.Species.SaplingGrowth.SproutGrowth == 0.0F)
            {
                return;
            }

            SaplingCell? saplingCell = model.Landscape.GetSaplingCell(trees.LightCellIndexXY[treeIndex], true, out ResourceUnit _);
            if (saplingCell == null)
            {
                return; // function has no effect if sapling cell isn't populated
            }

            if (treeIsRemoved)
            {
                // if the host tree is removed (disturbance, harvest), resprout on its center sapling cell
                Debug.Assert(trees.DbhInCm[treeIndex] < Constant.Grid.LightCellSizeInM, "Resprout on tree with " + trees.DbhInCm[treeIndex] + " cm DBH, which exceeds the size of a sapling cell.");
                trees.ResourceUnit.ClearSaplings(saplingCell, removeBiomass: false, resprout: false); // but tree could be removed without removing surrounding saplings?
                Sapling? sapling = saplingCell.AddSaplingIfSlotFree(Constant.Sapling.MinimumHeightInM, 0, trees.Species.Index);
                if (sapling != null)
                {
                    sapling.IsSprout = true;
                }
                saplingCell.CheckState();
            }
            else
            {
                // sprouts spread from a living tree with a low probability in adjacent cells
                if ((trees.Species.SaplingGrowth.AdultSproutProbability > 0.0F) && (trees.AgeInYears[treeIndex] > trees.Species.MinimumAgeInYearsForSeedProduction))
                {
                    this.EstablishSproutsInAdjacentCells(model, trees.Species, trees.LightCellIndexXY[treeIndex]);
                }
            }
        }

        public float GetAreaWithinLandscapeInM2() 
        {
            // get the on-landscape part of resource unit's area in m²
            return Constant.Grid.HeightCellAreaInM2 * this.HeightCellsOnLandscape; 
        }

        // C++/core/sapplings.cpp: Saplings::clearSaplings()
        // TODO: update naming as does both clearing and sprouting
        private void ClearSaplings(SaplingCell saplingCell, bool removeBiomass, bool resprout)
        {
            for (int index = 0; index < saplingCell.Saplings.Length; ++index)
            {
                if (saplingCell.Saplings[index].IsOccupied())
                {
                    if (!removeBiomass)
                    {
                        ResourceUnitTreeSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(this);
                        ruSpecies.SaplingStats.AddDeadCohort(saplingCell.Saplings[index].HeightInM / ruSpecies.Species.SaplingGrowth.HeightDiameterRatio * 100.0F);
                    }

                    saplingCell.Saplings[index].Clear();
                    if (resprout)
                    {
                        ResourceUnitTreeSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(this);
                        Sapling? st = saplingCell.AddSaplingIfSlotFree(0.05F, 0, ruSpecies.Species.Index);
                        if (st != null)
                        {
                            st.IsSprout = true;
                        }
                    }
                }
            }
            saplingCell.CheckState();
        }

        // stocked area calculation
        public void CountHeightCellsContainingTreesTallerThanTheRegenerationLayer(Landscape landscape) 
        {
            // this.heightCell counts are zeroed in OnStartYear()
            GridWindowEnumerator<float> ruVegetationHeightEnumerator = new(landscape.VegetationHeightGrid, this.ProjectExtent);
            while (ruVegetationHeightEnumerator.MoveNext())
            {
                float maximumVegetationHeightInM = ruVegetationHeightEnumerator.Current;
                if (maximumVegetationHeightInM > Constant.RegenerationLayerHeight)
                {
                    ++this.heightCellsWithTreesTallerThanRegenerationLayer;
                }
            }

            // sometimes useful for debugging: check for resource units with trees but without hieight cells taller than the regeneration layer
            // Debug.Assert((this.heightCellsWithTrees <= this.HeightCellsOnLandscape) && ((this.heightCellsWithTrees > 0) || (this.Trees.TreesBySpeciesID.Count == 0) || ((this.Trees.TreesBySpeciesID.Count == 1) && (this.Trees.TreesBySpeciesID.Values[0].Count == 1) && (this.Trees.TreesBySpeciesID.Values[0].HeightInM[0] > 0.0F))));
        }

        // establishment of saplings from seeds
        // see https://iland-model.org/seed+kernel+and+seed+distribution and https://iland-model.org/establishment
        // TODO: move to ResourceUnitSaplings
        // C++/core/saplings.cpp: Saplings::establishment()
        public void EstablishSaplings(Model model)
        {
            Debug.Assert(this.SaplingCells != null, "EstablishSaplings() called on resource unit where regeneration isn't enabled.");

            for (int species = 0; species < this.Trees.SpeciesAvailableOnResourceUnit.Count; ++species)
            {
                this.Trees.SpeciesAvailableOnResourceUnit[species].SaplingStats.ZeroStatistics();
            }

            RandomGenerator random = model.RandomGenerator.Value!;
            if (this.Trees.TreeSpeciesSet.RandomSpeciesOrder.Count < 1)
            {
                this.Trees.TreeSpeciesSet.CreateRandomSpeciesOrder(random);
            }

            float[] lightCorrection = new float[Constant.Grid.LightCellsPerHectare];
            Array.Fill(lightCorrection, -1.0F);

            Point ruOrigin = this.MinimumLightIndexXY; // offset on LIF/saplings grid
            Point seedmapOrigin = new(ruOrigin.X / Constant.Grid.LightCellsPerSeedmapCellWidth, ruOrigin.Y / Constant.Grid.LightCellsPerSeedmapCellWidth); // seed-map has 20m resolution, LIF 2m . factor 10
            this.Trees.TreeSpeciesSet.GetRandomSpeciesSampleIndices(random, out int sampleBegin, out int sampleEnd);
            for (int sampleIndex = sampleBegin; sampleIndex != sampleEnd; ++sampleIndex)
            {
                // start from a random species (and cycle through the available species)
                int speciesIndex = this.Trees.TreeSpeciesSet.RandomSpeciesOrder[sampleIndex];
                ResourceUnitTreeSpecies ruSpecies = this.Trees.SpeciesAvailableOnResourceUnit[speciesIndex];
                Debug.Assert(ruSpecies.Species.SeedDispersal != null, nameof(EstablishSaplings) + "() called on tree species not configured for seed dispersal.");
                Grid<float> seedMap = ruSpecies.Species.SeedDispersal.SeedMap;

                // check if there are seeds of the given species on the resource unit
                float seeds = 0.0F;
                for (int seedIndexY = 0; seedIndexY < Tree.SaplingCell.SaplingsPerCell; ++seedIndexY)
                {
                    int seedIndex = seedMap.IndexXYToIndex(seedmapOrigin.X, seedmapOrigin.Y);
                    for (int seedIndexX = 0; seedIndexX < Tree.SaplingCell.SaplingsPerCell; ++seedIndex, ++seedIndexX)
                    {
                        seeds += seedMap[seedIndex];
                    }
                }
                // if there are no seeds: no need to do more
                if (seeds == 0.0F)
                {
                    continue;
                }

                // calculate the abiotic environment (TACA) (this could also trigger the execution of the water cycle)
                float frostAndWaterModifier = ruSpecies.SaplingEstablishment.CalculateFrostAndWaterModifier(model, this.Weather, ruSpecies);
                if (frostAndWaterModifier == 0.0F)
                {
                    // ruSpecies.Establishment.WriteDebugOutputs();
                    continue;
                }

                // loop over all 2m cells on this resource unit
                Landscape landscape = model.Landscape;
                Grid<float> lightGrid = model.Landscape.LightGrid;
                for (int lightIndexY = 0; lightIndexY < Constant.Grid.LightCellsPerRUWidth; ++lightIndexY)
                {
                    int lightIndex = lightGrid.IndexXYToIndex(ruOrigin.X, ruOrigin.Y + lightIndexY); // index on 2m cell
                    for (int lightIndexX = 0; lightIndexX < Constant.Grid.LightCellsPerRUWidth; ++lightIndexX, ++lightIndex)
                    {
                        SaplingCell saplingCell = this.SaplingCells[lightIndexY * Constant.Grid.LightCellsPerRUWidth + lightIndexX]; // pointer to a row
                        if (saplingCell.HasFreeSlots())
                        {
                            // is a sapling of the current species already on the pixel?
                            // * test for sapling height already in cell state
                            // * test for grass-cover already in cell state
                            Sapling? sapling = null;
                            Sapling[] slots = saplingCell.Saplings;
                            for (int cellIndex = 0; cellIndex < slots.Length; ++cellIndex)
                            {
                                if (sapling == null && !slots[cellIndex].IsOccupied())
                                {
                                    sapling = slots[cellIndex];
                                }
                                if (slots[cellIndex].SpeciesIndex == speciesIndex)
                                {
                                    sapling = null;
                                    break;
                                }
                            }

                            if (sapling != null)
                            {
                                float seedMapValue = seedMap[landscape.LightIndexToSeedIndex(lightIndex)];
                                if (seedMapValue == 0.0F)
                                {
                                    continue;
                                }

                                float lightValue = lightGrid[lightIndex];
                                float lriCorrection = lightCorrection[lightIndexY * Constant.Grid.LightCellsPerRUWidth + lightIndexX];
                                // calculate the LIFcorrected only once per pixel; the relative height is 0 (light level on the forest floor)
                                if (lriCorrection < 0.0F)
                                {
                                    // TODO: lightCorrection[] is never updated -> no caching?
                                    lriCorrection = ruSpecies.Species.SpeciesSet.GetLriCorrection(lightValue, 0.0F);
                                }

                                // check for the combination of seed availability and light on the forest floor
                                float pGermination = seedMapValue * lriCorrection * frostAndWaterModifier;
                                if (random.GetRandomProbability() < pGermination)
                                {
                                    // add a sapling at the given position (age is incremented later)
                                    sapling.SetSapling(Constant.Sapling.MinimumHeightInM, 0, speciesIndex);
                                    saplingCell.CheckState();
                                    ++ruSpecies.SaplingStats.NewCohorts;
                                }
                            }
                        }
                    }
                }
                // create debug output related to establishment
                // rus.Establishment.WriteDebugOutputs();
            }
        }

        // TODO: why does this variant of LAI calculation use stockable area instead of stocked area?
        public float GetLeafAreaIndex()
        {
            return this.AreaInLandscapeInM2 != 0.0F ? this.Trees.TotalLeafArea / this.AreaInLandscapeInM2 : 0.0F;
        }

        // TODO: move to ResourceUnitSaplings
        // C++/core/saplings.cpp: Saplings::saplingGrowth()
        public void GrowSaplings(Model model)
        {
            Debug.Assert(this.SaplingCells != null, nameof(this.GrowSaplings) + "() called on resource unit where regeneration isn't enabled.");

            Landscape landscape = model.Landscape;
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;

            Point ruOrigin = this.MinimumLightIndexXY;
            for (int lightIndexY = 0; lightIndexY < Constant.Grid.LightCellsPerRUWidth; ++lightIndexY)
            {
                int lightIndex = lightGrid.IndexXYToIndex(ruOrigin.X, ruOrigin.Y + lightIndexY);
                for (int lightIndexX = 0; lightIndexX < Constant.Grid.LightCellsPerRUWidth; ++lightIndexX, ++lightIndex)
                {
                    SaplingCell saplingCell = this.SaplingCells[lightIndexY * Constant.Grid.LightCellsPerRUWidth + lightIndexX]; // ptr to row
                    if (saplingCell.State != SaplingCellState.NotOnLandscape)
                    {
                        bool checkCellState = false;
                        int nSaplings = saplingCell.GetOccupiedSlotCount();
                        for (int index = 0; index < saplingCell.Saplings.Length; ++index)
                        {
                            if (saplingCell.Saplings[index].IsOccupied())
                            {
                                // growth of this sapling tree
                                float maximumVegetationHeightInM = vegetationHeightGrid[landscape.LightIndexToHeightIndex(lightIndex)];
                                float lightValue = lightGrid[lightIndex];

                                checkCellState |= this.GrowSaplings(model, saplingCell, saplingCell.Saplings[index], lightIndex, maximumVegetationHeightInM, lightValue, nSaplings);
                            }
                        }
                        if (checkCellState)
                        {
                            saplingCell.CheckState();
                        }
                    }
                }
            }

            // store statistics on saplings/regeneration
            for (int species = 0; species < this.Trees.SpeciesAvailableOnResourceUnit.Count; ++species)
            {
                ResourceUnitTreeSpecies ruSpecies = this.Trees.SpeciesAvailableOnResourceUnit[species];
                ruSpecies.SaplingStats.AfterSaplingGrowth(model, this, ruSpecies.Species);
                ruSpecies.StatisticsLive.Add(ruSpecies.SaplingStats);
                // TODO: how to include saplings in stand statistics?
            }


            // debug output related to saplings
            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.SaplingGrowth) && this.shouldCreateDebugOutput())
            //{
            //    // establishment details
            //    for (int speciesIndex = 0; speciesIndex != this.Trees.SpeciesAvailableOnResourceUnit.Count; ++speciesIndex)
            //    {
            //        ResourceUnitTreeSpecies species = this.Species[speciesIndex];
            //        if (species.SaplingStats.LivingCohorts == 0)
            //        {
            //            continue;
            //        }

            //        List<object> output = GlobalSettings.Instance.DebugList(this.Index, DebugOutputs.SaplingGrowth);
            //        output.AddRange(new object[] { species.Species.ID, this.Index, this.ID,
            //                                       species.SaplingStats.LivingCohorts, species.SaplingStats.AverageHeight, species.SaplingStats.AverageAge,
            //                                       species.SaplingStats.AverageDeltaHPot, species.SaplingStats.AverageDeltaHRealized,
            //                                       species.SaplingStats.NewCohorts, species.SaplingStats.NewCohortsVegetative, species.SaplingStats.DeadSaplings,
            //                                       species.SaplingStats.RecruitedSaplings, species.Species.SaplingGrowthParameters.ReferenceRatio,
            //                                       species.SaplingStats.CarbonNitrogenLiving, species.SaplingStats.CarbonNitrogenGain });
            //    }
            //}
        }

        // TODO: move to ResourceUnitSaplings
        /// <remarks>
        /// C++: Saplings::growSapling()
        /// </remarks>
        private bool GrowSaplings(Model model, SaplingCell saplingCell, Sapling sapling, int lightCellIndex, float dominantHeight, float lif_value, int cohorts_on_px)
        {
            ResourceUnitTreeSpecies ruSpecies = sapling.GetResourceUnitSpecies(this);
            TreeSpecies species = ruSpecies.Species;

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            float h_pot = species.SaplingGrowth.HeightGrowthPotential.Evaluate(sapling.HeightInM);
            float delta_h_pot = h_pot - sapling.HeightInM;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            if (dominantHeight <= 0.0F)
            {
                throw new NotSupportedException(String.Format("Dominant height grid has value 0 at light cell index {0}.", lightCellIndex));
            }

            float relativeHeight = sapling.HeightInM / dominantHeight;
            float lriCorrection = species.SpeciesSet.GetLriCorrection(lif_value, relativeHeight); // correction based on height
            float lightResponse = species.GetLightResponse(lriCorrection); // species specific light response (LUI, light utilization index)

            ruSpecies.CalculateBiomassGrowthForYear(model, fromSaplingEstablishmentOrGrowth: true); // calculate the 3-PG module (this is done only once per RU); true: call comes from regeneration
            float siteEnvironmentHeightMultiplier = ruSpecies.TreeGrowth.SiteEnvironmentSaplingHeightGrowthMultiplier;
            float heightGrowthFactor = siteEnvironmentHeightMultiplier * lightResponse; // relative growth

            Debug.Assert((h_pot >= 0.0F) && (delta_h_pot >= 0.0F) && (lriCorrection >= 0.0F) && (lriCorrection <= 1.0F) && (heightGrowthFactor >= 0.0F) && (heightGrowthFactor <= 1.0F), "Sapling growth out of range.");

            // sprouts grow faster. Sprouts therefore are less prone to stress (threshold), and can grow higher than the growth potential.
            if (sapling.IsSprout)
            {
                heightGrowthFactor *= species.SaplingGrowth.SproutGrowth;
            }

            // check browsing
            float browsingPressure = model.Project.World.Browsing.BrowsingPressure;
            if (browsingPressure > 0.0 && sapling.HeightInM <= 2.0F)
            {
                float pBrowsing = ruSpecies.Species.SaplingGrowth.BrowsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) . odds_mod = odds * browsingPressure . p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                float pBrowsed = pBrowsing * browsingPressure / (1.0F - pBrowsing + pBrowsing * browsingPressure);
                if (model.RandomGenerator.Value!.GetRandomProbability() < pBrowsed)
                {
                    heightGrowthFactor = 0.0F;
                }
            }
            // check browsing due to "management" or BITE
            if (sapling.IsBrowsed)
            {
                heightGrowthFactor = 0.0F;
                sapling.IsBrowsed = false;
            }

            // intrinsic mortality of saplings: use the same approach as adult trees (probability based on the maximum age species parameter)
            bool saplingDies = false;
            if (model.RandomGenerator.Value!.GetRandomProbability() < species.DeathProbabilityFixed)
            {
                saplingDies = true;
            }
            // check mortality of saplings
            if (heightGrowthFactor < species.SaplingGrowth.StressThreshold)
            {
                ++sapling.StressYears;
                if (sapling.StressYears > species.SaplingGrowth.MaxStressYears)
                {
                    saplingDies = true;
                }
            }
            else
            {
                sapling.StressYears = 0; // reset stress counter
            }

            if (saplingDies)
            {
                // sapling dies...
                ruSpecies.SaplingStats.AddDeadCohort(sapling.HeightInM / species.SaplingGrowth.HeightDiameterRatio * 100.0F);
                sapling.Clear();
                return true; // need cleanup
            }
            // Debug.WriteLineIf(delta_h_pot * heightGrowthFactor < 0.0F || (!sapling.IsSprout && delta_h_pot * heightGrowthFactor > 2.0), "Sapling::growSapling", "implausible height growth.");

            // grow
            sapling.HeightInM += (delta_h_pot * heightGrowthFactor);
            sapling.Age++; // increase age of sapling by 1

            // recruitment?
            SaplingStatistics saplingStats = ruSpecies.SaplingStats;
            float total_carbon_added = 0.0F;
            if (sapling.HeightInM > Constant.RegenerationLayerHeight)
            {
                ++ruSpecies.SaplingStats.RecruitedCohorts;

                float centralDbh = 100.0F * sapling.HeightInM / species.SaplingGrowth.HeightDiameterRatio;
                // the number of trees to create (result is in trees per pixel)
                float saplingsToEstablishAsTreesAsFloat = species.SaplingGrowth.RepresentedStemNumberFromDiameter(centralDbh);

                // if number of saplings to establish as treees is not an integer, choose randomly if we should add a tree.
                // For example, if n_trees = 2.3, add 2 trees with 70% probability, and add 3 trees with 30% probability.
                RandomGenerator random = model.RandomGenerator.Value!;
                int saplingsToEstablishAsTrees = (int)saplingsToEstablishAsTreesAsFloat;
                if ((saplingsToEstablishAsTrees == 0) ||
                    (random.GetRandomProbability() < (saplingsToEstablishAsTreesAsFloat - saplingsToEstablishAsTrees)))
                {
                    ++saplingsToEstablishAsTrees;
                }

                // add a new tree
                TreeListForAddition saplingsToRecruit = new(saplingsToEstablishAsTrees);
                float heightOrDiameterVariation = model.Project.Model.SeedDispersal.RecruitmentDimensionVariation;
                UInt32 ingrowthBaseTreeID = 100000U * this.ID + (UInt32)this.saplingsRecruited;
                Point lightCellIndexXY = model.Landscape.LightGrid.GetCellXYIndex(lightCellIndex);
                Debug.Assert(this.ProjectExtent.Contains(model.Landscape.LightGrid.GetCellProjectCentroid(lightCellIndexXY)));
                for (int saplingIndex = 0; saplingIndex < saplingsToEstablishAsTrees; ++saplingIndex)
                {
                    // add variation: add +/-N% to DBH and *independently* to height.
                    float dbhInCm = centralDbh * random.GetRandomFloat(1.0F - heightOrDiameterVariation, 1.0F + heightOrDiameterVariation);
                    saplingsToRecruit.DbhInCm[saplingIndex] = dbhInCm;
                    float heightInM = sapling.HeightInM * random.GetRandomFloat(1.0F - heightOrDiameterVariation, 1.0F + heightOrDiameterVariation);
                    saplingsToRecruit.HeightInM[saplingIndex] = heightInM;

                    saplingsToRecruit.AgeInYears[saplingIndex] = 0;
                    saplingsToRecruit.LightCellIndexXY[saplingIndex] = lightCellIndexXY;
                    saplingsToRecruit.SpeciesID[saplingIndex] = species.WorldFloraID;
                    saplingsToRecruit.StandID[saplingIndex] = Constant.DefaultStandID; // TODO
                    saplingsToRecruit.TreeID[saplingIndex] = ingrowthBaseTreeID + (UInt32)saplingIndex;
                }
                saplingsToRecruit.Count = saplingsToEstablishAsTrees;
                this.saplingsRecruited += saplingsToEstablishAsTrees;

                float lightStampBeerLambertK = model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
                TreeListSpatial treesOfSpecies = this.Trees.AddTrees(saplingsToRecruit.AsSpan(), lightStampBeerLambertK);
                for (int treeIndex = treesOfSpecies.Count - saplingsToEstablishAsTrees; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    ruSpecies.StatisticsLive.Add(treesOfSpecies, treeIndex); // capture newly recruited tree into tree statistics

                    // account for the carbon that is *added* by the new trees
                    float dbhInCm = treesOfSpecies.DbhInCm[treeIndex];
                    total_carbon_added += Constant.DryBiomassCarbonFraction * (treesOfSpecies.StemMassInKg[treeIndex] + treesOfSpecies.NppReserveInKg[treeIndex] + + species.GetBiomassBranch(dbhInCm) + treesOfSpecies.FoliageMassInKg[treeIndex] + treesOfSpecies.CoarseRootMassInKg[treeIndex] + treesOfSpecies.FineRootMassInKg[treeIndex]);
                }

                float woody_bm = species.GetBiomassStem(centralDbh) + species.GetBiomassStem(centralDbh) + species.GetBiomassCoarseRoot(centralDbh);
                float foliage = species.GetBiomassFoliage(centralDbh);
                float fineroot = foliage * species.FinerootFoliageRatio;
                // the 'gap' between sapling and trees is: sum(C recruited trees) - C sapling
                total_carbon_added -= total_carbon_added - Constant.DryBiomassCarbonFraction * (woody_bm + foliage + fineroot);

                // clear all regeneration from this pixel (including this tree)
                sapling.Clear(); // clear this tree (no carbon flow to the ground)
                for (int cellIndex = 0; cellIndex < saplingCell.Saplings.Length; ++cellIndex)
                {
                    if (saplingCell.Saplings[cellIndex].IsOccupied())
                    {
                        // add carbon to the ground
                        ResourceUnitTreeSpecies sruSpecies = saplingCell.Saplings[cellIndex].GetResourceUnitSpecies(this);
                        sruSpecies.SaplingStats.AddDeadCohort(saplingCell.Saplings[cellIndex].HeightInM / sruSpecies.Species.SaplingGrowth.HeightDiameterRatio * 100.0F);
                        saplingCell.Saplings[cellIndex].Clear();
                    }
                }

                saplingStats.CarbonOfRecruitedTrees += total_carbon_added;
                return true; // need cleanup
            }

            // book keeping (only for survivors) for the sapling of the resource unit / species
            float n_repr = species.SaplingGrowth.RepresentedStemNumberFromHeight(sapling.HeightInM) / cohorts_on_px;
            if (sapling.HeightInM > 1.3F)
            {
                saplingStats.LivingSaplings += n_repr;
                ++saplingStats.CohortsWithDbh;
            }
            else
            {
                saplingStats.LivingSaplingsSmall += n_repr;
            }
            saplingStats.LivingCohorts++;
            saplingStats.AverageHeight += sapling.HeightInM;
            saplingStats.AverageAgeInYears += sapling.Age;
            saplingStats.AverageDeltaHPotential += delta_h_pot;
            saplingStats.AverageDeltaHRealized += delta_h_pot * heightGrowthFactor;

            // seed dispersal: the saplings produce seed if they are are old enough
            // important e.g. for Pinus mugo
            if ((species.SeedDispersal != null) && (sapling.Age > species.MinimumAgeInYearsForSeedProduction))
            {
                float dbh = 100.0F * sapling.HeightInM / species.SaplingGrowth.HeightDiameterRatio;
                float foliage = species.GetBiomassFoliage(dbh);
                float leaf_area = foliage * n_repr;
                species.SeedDispersal.SetSaplingTree(model.Landscape.LightGrid.GetCellXYIndex(lightCellIndex), leaf_area);
            }

            // sprouting from regeneration: this requires a minimum height (and lateral sprouting being enabled)
            if ((species.SaplingGrowth.AdultSproutProbability > 0.0) && (sapling.Age > species.MinimumAgeInYearsForSeedProduction))
            {
                this.EstablishSproutsInAdjacentCells(model, species, model.Landscape.LightGrid.GetCellXYIndex(lightCellIndex));
            }

            // update stem height (local height packed into upper byte of HeightGridValue.mCount)
            //if (sapling.HeightInM > dominantHeight.stemHeight())
            //{
            //    dominantHeight.setStemHeight(sapling.HeightInM);
            //}

            return false;
        }

        // return the sapling cell at given LIF-coordinates
        // TODO: move to ResourceUnitSaplings
        public SaplingCell GetSaplingCell(Point lightCellPosition) // C++: ResourceUnit::saplingCell()
        {
            Debug.Assert(this.SaplingCells != null, "GetSaplingCell() called on resource unit where regeneration isn't enabled.");

            // LIF-Coordinates are global, we here need (RU-)local coordinates
            int indexX = (lightCellPosition.X - this.MinimumLightIndexXY.X) % Constant.Grid.LightCellsPerRUWidth;
            int indexY = (lightCellPosition.Y - this.MinimumLightIndexXY.Y) % Constant.Grid.LightCellsPerRUWidth;
            int index = indexY * Constant.Grid.LightCellsPerRUWidth + indexX;
            Debug.Assert(index >= 0 && index < Constant.Grid.LightCellsPerHectare);
            return this.SaplingCells[index];
        }

        // TODO: remove when this.Saplings is ResourceUnitSaplings : Grid<SaplingCell>
        public PointF GetSaplingCellPosition(Sapling sapling)
        {
            if (this.SaplingCells == null)
            {
                throw new InvalidOperationException(nameof(this.GetSaplingCellPosition) + "() called on resource unit " + this.ID + ", which does not have saplings enabled.");
            }

            for (int cellIndex = 0; cellIndex < this.SaplingCells.Length; ++cellIndex)
            {
                SaplingCell saplingCell = this.SaplingCells[cellIndex];
                for (int saplingIndex = 0; saplingIndex < saplingCell.Saplings.Length; ++saplingIndex)
                {
                    if (Object.ReferenceEquals(sapling, saplingCell.Saplings[saplingIndex]))
                    {
                        int saplingCellIndexX = cellIndex % Constant.Grid.LightCellsPerRUWidth;
                        int saplingCellIndexY = cellIndex / Constant.Grid.LightCellsPerRUWidth;
                        return new PointF(this.ProjectExtent.Left + Constant.Grid.LightCellSizeInM * saplingCellIndexX, this.ProjectExtent.Bottom + Constant.Grid.LightCellSizeInM * saplingCellIndexY);
                    }
                }
            }

            throw new ArgumentOutOfRangeException(nameof(sapling), "Sapling not found in resource unit.");
        }

        /// return the area (m2) which is covered by saplings (cells >0 saplings)
        /// if  `below130cm` is false, then only pixels with saplings >1.3m are counted; otherwise
        /// pixel are counted when saplings < 1.3m are present or when grass cover is present
        public float GetSaplingCoveredArea(bool onlyBelow130cm) // C++: ResourceUnit::saplingCoveredArea()
        {
            Debug.Assert(this.SaplingCells != null);

            int occupiedCells = 0;
            if (onlyBelow130cm)
            {
                for (int i = 0; i < this.SaplingCells.Length; ++i)
                {
                    // either grass *OR* hmax<1.3m
                    if (this.SaplingCells[i].State == SaplingCellState.Grass)
                    {
                        ++occupiedCells;
                    }
                    else
                    {
                        float maxSaplingHeightInM = this.SaplingCells[i].MaxHeight();
                        Debug.Assert(maxSaplingHeightInM > Constant.Sapling.MinimumHeightInM);
                        if (maxSaplingHeightInM <= 1.3F)
                        {
                            ++occupiedCells;
                        }
                    }
                }
            }
            else
            {
                // only px that have saplings > 1.3m
                for (int i = 0; i < this.SaplingCells.Length; ++i)
                {
                    if (this.SaplingCells[i].MaxHeight() > 1.3F)
                    {
                        ++occupiedCells;
                    }
                }
            }

            return Constant.Grid.LightCellSizeInM * Constant.Grid.LightCellSizeInM * occupiedCells;
        }

        public void OnStartYear(Landscape landscape)
        {
            // run microclimate
            // Done before trees zero their statistics for the start of the year.
            this.Microclimate?.CalculateVegetationLaiShadeToleranceAndMeans(landscape);

            this.heightCellsWithTreesTallerThanRegenerationLayer = 0;
            this.Trees.OnStartYear();

            // clean transfer pools
            this.Snags?.OnStartYear();
            this.Soil?.OnStartYear();
        }

        /** production() is the "stand-level" part of the biomass production (3-PG).
            - The amount of radiation intercepted by the stand is calculated
            - the water cycle is calculated
            - statistics for each species are cleared
            - The 3-PG production for each species and ressource unit is called (calculates species-responses and NPP production)
            see also: https://iland-model.org/individual+tree+light+availability */
        public void CalculateWaterAndBiomassGrowthForYear(Model model) // C++: ResourceUnit::production()
        {
            if ((this.Trees.TotalLightWeightedLeafArea == 0.0F) || (this.HeightCellsOnLandscape == 0))
            {
                // clear statistics of resource unit species
                for (int species = 0; species < this.Trees.SpeciesAvailableOnResourceUnit.Count; ++species)
                {
                    this.Trees.SpeciesAvailableOnResourceUnit[species].StatisticsLive.Zero();
                }

                this.AreaWithTreesInM2 = 0.0F;
                this.Trees.PhotosyntheticallyActiveArea = 0.0F; // TODO: is this redundant?
            }
            else
            {
                // height pixels are counted during the height-grid-calculations
                this.AreaWithTreesInM2 = Constant.Grid.HeightCellAreaInM2 * this.heightCellsWithTreesTallerThanRegenerationLayer; // m² (1 height grid pixel = 10x10m)
                float laiBasedOnRUAreaWithinLandscape = this.GetLeafAreaIndex();
                if (laiBasedOnRUAreaWithinLandscape < 3.0F)
                {
                    // estimate stocked area based on crown projections
                    float totalCrownArea = 0.0F;
                    for (int speciesIndex = 0; speciesIndex < this.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                    {
                        TreeListSpatial treesOfSpecies = this.Trees.TreesBySpeciesID.Values[speciesIndex];
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            if (treesOfSpecies.IsDead(treeIndex) == false)
                            {
                                LightStamp reader = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                                totalCrownArea += reader.CrownAreaInM2;
                            }
                        }
                    }
                    //if (GlobalSettings.Instance.LogDebug())
                    //{
                    //    Debug.WriteLine("crown area: lai " + LeafAreaIndex() + " stocked area (pixels) " + StockedArea + " area (crown) " + totalCrownArea);
                    //}
                    if (laiBasedOnRUAreaWithinLandscape < 1.0)
                    {
                        this.AreaWithTreesInM2 = MathF.Min(totalCrownArea, this.AreaWithTreesInM2);
                    }
                    else
                    {
                        // for LAI between 1 and 3
                        // interpolate between sum of crown area of trees (at LAI=1) and the pixel-based value (at LAI=3 and above)
                        // TODO: assumes trees are homogeneously distributed across resource unit and that crowns don't overlap
                        float linearInterpolationPoint = (laiBasedOnRUAreaWithinLandscape - 1.0F) / 2.0F; // 0 at LAI=1, 1 at LAI=3
                        this.AreaWithTreesInM2 = this.AreaWithTreesInM2 * linearInterpolationPoint + MathF.Min(totalCrownArea, this.AreaWithTreesInM2) * (1.0F - linearInterpolationPoint);
                    }
                }

                Debug.Assert((this.AreaWithTreesInM2 >= 0.0F) && (this.AreaWithTreesInM2 <= Constant.Grid.ResourceUnitAreaInM2));

                // calculate the leaf area index (LAI)
                float ruLeafAreaIndex = this.Trees.TotalLeafArea / this.AreaWithTreesInM2;
                // calculate the intercepted radiation fraction using the law of Beer Lambert
                float ruK = model.Project.Model.Ecosystem.ResourceUnitLightExtinctionCoefficient;
                float lightInterceptionFraction = 1.0F - MathF.Exp(-ruK * ruLeafAreaIndex);
                this.Trees.PhotosyntheticallyActiveArea = this.AreaWithTreesInM2 * lightInterceptionFraction; // m2

                // calculate the total weighted leaf area on this RU:
                this.Trees.AverageLightRelativeIntensity = this.Trees.PhotosyntheticallyActiveArea / this.Trees.TotalLightWeightedLeafArea; // p_WLA
                Debug.Assert((this.Trees.AverageLightRelativeIntensity >= 0.0F) && (this.Trees.AverageLightRelativeIntensity < 8.0F), "Average light relative intensity of " + this.Trees.AverageLightRelativeIntensity + " is negative or greater than the expected upper bound for LRIs."); // sanity upper bound, denser stands produce higher intensities
            }

            // soil water model - this determines soil water contents needed for response calculations
            this.WaterCycle.RunYear(model.Project);
            model.Modules.CalculateWater(this);

            // invoke species specific calculation (3-PG)
            for (int speciesIndex = 0; speciesIndex < this.Trees.SpeciesAvailableOnResourceUnit.Count; ++speciesIndex)
            {
                this.Trees.SpeciesAvailableOnResourceUnit[speciesIndex].CalculateBiomassGrowthForYear(model, fromSaplingEstablishmentOrGrowth: false); // CALCULATE 3-PG

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

        public void OnEndYear(Model model) // C++: ResourceUnit::yearEnd()
        {
            // calculate statistics for all tree species of the resource unit
            this.Trees.OnEndYear();

            // update carbon flows
            if (this.CarbonCycle != null)
            {
                Debug.Assert((this.Snags != null) && (this.Soil != null));

                this.CarbonCycle.NppInKgCPerHa = this.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.TreeNppPerHa * Constant.DryBiomassCarbonFraction;
                this.CarbonCycle.NppInKgCPerHa += this.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingNppPerHa * Constant.DryBiomassCarbonFraction;

                float areaFactor = this.AreaInLandscapeInM2 / Constant.Grid.ResourceUnitAreaInM2; // conversion factor
                float carbonToAtmosphereInKgPerHa = this.Snags.FluxToAtmosphere.C / areaFactor; // from snags, kgC/ha
                carbonToAtmosphereInKgPerHa += 0.1F * this.Soil.FluxToAtmosphere.C * Constant.Grid.ResourceUnitAreaInM2; // soil: ton/ha * 0.0001 ha/m² * 1000 kg/ton = 0.1 kg/m²
                this.CarbonCycle.CarbonToAtmosphereInKgPerHa = carbonToAtmosphereInKgPerHa;

                float disturbanceReleaseInKgPerHa = this.Snags.FluxToDisturbance.C / areaFactor;
                disturbanceReleaseInKgPerHa += 0.1F * this.Soil.FluxToDisturbance.C * Constant.Grid.ResourceUnitAreaInM2;
                float to_harvest = this.Snags.FluxToExtern.C / areaFactor;

                this.CarbonCycle.NepInKgCPerHa = this.CarbonCycle.NppInKgCPerHa - carbonToAtmosphereInKgPerHa - disturbanceReleaseInKgPerHa - to_harvest; // kgC/ha

                // incremental values....
                this.CarbonCycle.CumulativeCarbonToAtmosphereInKgPerHa += this.CarbonCycle.CarbonToAtmosphereInKgPerHa;
                this.CarbonCycle.CumulativeNepInKgCPerHa += this.CarbonCycle.NepInKgCPerHa;
                this.CarbonCycle.CumulativeNppInKgCPerHa += this.CarbonCycle.NppInKgCPerHa;
            }

            // SVD States: update state
            ///< (if enabled) update the state of the RU (C++: ResourceUnit::updateSVDState())
            if (model.SvdStates != null)
            {
                // create vectors on the heap only when really needed
                int nspecies = this.Trees.TreeSpeciesSet.Count;
                this.SvdState.LocalComposition.Capacity = nspecies;
                this.SvdState.MidDistanceComposition.Capacity = nspecies;

                int stateId = model.SvdStates.EvaluateState(model, this);
                if (this.SvdState.StateID == stateId)
                {
                    ++this.SvdState.YearsInState;
                }
                else
                {
                    this.SvdState.YearsInPreviousState = SvdState.YearsInState;
                    this.SvdState.PreviousStateID = SvdState.StateID;
                    this.SvdState.StateID = stateId;
                    this.SvdState.YearsInState = 1;
                }
            }
        }

        public void CalculateCarbonCycle(Model model) // C++: ResourceUnit::calculateCarbonCycle()
        {
            // (1) calculate the snag dynamics
            // because all carbon/nitrogen-flows from trees to the soil are routed through the snag-layer,
            // all soil inputs (litter + deadwood) are collected in the Snag-object.
            this.Snags?.RunYear(model);
            
            if (this.Soil != null)
            {
                Debug.Assert(this.Snags != null);

                this.Soil.ClimateDecompositionFactor = this.Snags.WeatherFactor; // the climate factor is only calculated once
                this.Soil.SetSoilInput(this.Snags.LabileFlux, this.Snags.RefractoryFlux, this.Snags.LabileFluxAbovegroundCarbon, this.Snags.RefractoryFluxAbovegroundCarbon);
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

        public void SetupEnvironment(Project projectFile, ResourceUnitEnvironment environment) // C++: ResourceUnit::setup()
        {
            this.WaterCycle.Setup(projectFile, environment);

            if (projectFile.Model.Settings.CarbonCycleEnabled)
            {
                this.CarbonCycle = new();
                this.Soil = new(this, environment);
                this.Snags = new(projectFile, this, environment);
            }

            if (projectFile.Model.Microclimate.Enabled)
            {
                this.Microclimate = new(this);
            }

            if (projectFile.Model.Settings.RegenerationEnabled)
            {
                this.SaplingCells = new SaplingCell[Constant.Grid.LightCellsPerHectare];
                for (int cellIndex = 0; cellIndex < this.SaplingCells.Length; ++cellIndex)
                {
                    // TODO: SoA instead of AoS storage
                    this.SaplingCells[cellIndex] = new();
                }
            }

            // if dynamic coupling of soil nitrogen is enabled, a starting value for available N is calculated
            // TODO: but starting values are in the environment file?
            //if (this.Soil != null && model.ModelSettings.UseDynamicAvailableNitrogen && model.ModelSettings.CarbonCycleEnabled)
            //{
            //    this.Soil.ClimateDecompositionFactor = 1.0; // TODO: why is this set to 1.0 without restoring the original value?
            //    this.Soil.CalculateYear();
            //}
        }

        public void SetupTreesAndSaplings(Landscape landscape)
        {
            this.CountHeightCellsContainingTreesTallerThanTheRegenerationLayer(landscape);
            this.Trees.SetupStatistics();

            if (this.SaplingCells == null)
            {
                return;
            }

            for (int lightCellIndex = 0; lightCellIndex < Constant.Grid.LightCellsPerHectare; ++lightCellIndex)
            {
                SaplingCell saplingCell = this.SaplingCells[lightCellIndex];
                if (saplingCell.State != SaplingCellState.NotOnLandscape)
                {
                    int cohortsInCell = saplingCell.GetOccupiedSlotCount();
                    for (int saplingCellIndex = 0; saplingCellIndex < saplingCell.Saplings.Length; ++saplingCellIndex)
                    {
                        if (saplingCell.Saplings[saplingCellIndex].IsOccupied())
                        {
                            Sapling sapling = saplingCell.Saplings[saplingCellIndex];
                            ResourceUnitTreeSpecies ruSpecies = sapling.GetResourceUnitSpecies(this);
                            ++ruSpecies.SaplingStats.LivingCohorts;
                            float nRepresented = ruSpecies.Species.SaplingGrowth.RepresentedStemNumberFromHeight(sapling.HeightInM) / cohortsInCell;
                            if (sapling.HeightInM > 1.3F)
                            {
                                ruSpecies.SaplingStats.LivingSaplings += nRepresented;
                            }
                            else
                            {
                                ruSpecies.SaplingStats.LivingSaplingsSmall += nRepresented;
                            }

                            ruSpecies.SaplingStats.AverageHeight += sapling.HeightInM;
                            ruSpecies.SaplingStats.AverageAgeInYears += sapling.Age;
                        }
                    }
                }
            }
        }

        public void UpdateSaplingCellGrassCover(Model model) // C++: Saplings::simplifiedGrassCover()
        {
            float threshold = model.Landscape.GrassCover.CellLifThreshold;
            Grid<float> lif_grid = model.Landscape.LightGrid;

            Point imap = this.MinimumLightIndexXY;
            if (this.SaplingCells == null)
            {
                throw new InvalidOperationException(nameof(this.UpdateSaplingCellGrassCover) + "() called on resource unit " + this.ID + " which does not have saplings enabled.");
            }

            for (int lightCellIndexY = 0, saplingCellIndex = 0; lightCellIndexY < Constant.Grid.LightCellsPerRUWidth; ++lightCellIndexY)
            {
                int lightCellIndex = lif_grid.IndexXYToIndex(imap.X, imap.Y + lightCellIndexY);
                for (int lightCellIndexX = 0; lightCellIndexX < Constant.Grid.LightCellsPerRUWidth; ++lightCellIndexX, ++lightCellIndex, ++saplingCellIndex) 
                {
                    SaplingCell saplingCell = this.SaplingCells[saplingCellIndex];
                    if ((saplingCell.State == SaplingCellState.Empty) || (saplingCell.State == SaplingCellState.Grass))
                    {
                        saplingCell.State = lif_grid[lightCellIndex] > threshold ? SaplingCellState.Grass : SaplingCellState.Empty;
                    }
                }
            }
        }

        // TODO: remove unused?
        private float GetMaximumSaplingHeight() // C++: Saplings::topHeight()
        {
            if (this.SaplingCells == null)
            {
                throw new InvalidOperationException(nameof(this.UpdateSaplingCellGrassCover) + "() called on resource unit " + this.ID + " which does not have saplings enabled.");
            }

            //int n_cells = ru->stockableArea() / (cPxSize*cPxSize);
            float h_max = Single.MinValue;
            for (int saplingCellIndex = 0; saplingCellIndex < this.SaplingCells.Length; ++saplingCellIndex) 
            {
                SaplingCell saplingCell = this.SaplingCells[saplingCellIndex];
                if (saplingCell.State != SaplingCellState.NotOnLandscape) 
                {
                    float tallestSaplingHeightInM = saplingCell.MaxHeight();
                    if (tallestSaplingHeightInM > h_max)
                    {
                        h_max = tallestSaplingHeightInM;
                    }
                }
            }
            return h_max;
        }

        // TODO: it appears these methods have never worked; confirm unused and delete
        //private void clearAllSaplings()
        //{
        //    if (this.SaplingCells == null)
        //    {
        //        return; // no saplings to clear
        //    }

        //    for (int saplingCellIndex = 0; saplingCellIndex < this.SaplingCells.Length; ++saplingCellIndex)
        //    {
        //        SaplingCell s = this.SaplingCells[saplingCellIndex];
        //        this.ClearSaplings(s, removeBiomass: true, resprout: false);
        //    }
        //}

        //private int addSaplings(Model model, RectangleF rectangle, string speciesSetName, float heightInM, int age)
        //{
        //    GridWindowEnumerator<float> runner = new(model.Landscape.LightGrid, rectangle);
        //    TreeSpeciesSet speciesSet = model.Landscape.SpeciesSetsByTableName[speciesSetName];

        //    int species_index = static_cast < short signed int> (speciesSet->index());
        //    int nadded = 0;
        //    while (runner.MoveNext())
        //    {
        //        SaplingCell? s = model.Landscape.GetSaplingCell(runner.GetCurrentXYIndex(), onlyValid: true, out ResourceUnit _);
        //        if (s != null)
        //        {
        //            if (s.AddSaplingIfSlotFree(heightInM, age, species_index) != null)
        //            {
        //                ++nadded;
        //            }
        //        }
        //    }
        //    return nadded;
        //}

        /// calculate the top tree height (as 90th percentile of the top heights on the 10m pixels), 'rIrregular' is set to true when 50% of the area < 50% of topheight
        public float GetCanopyHeight90(Model model, out bool rIrregular) // C++: ResourceUnit::topHeight()
        {
            GridWindowEnumerator<float> runner = new(model.Landscape.VegetationHeightGrid, this.ProjectExtent);
            List<float> px_heights = new(Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth);
            while (runner.MoveNext())
            {
                // stand map TODO: isValid()
                px_heights.Add(runner.Current);
            }
            
            SummaryStatistics hstat = new(px_heights);
            float h_top = hstat.GetPercentile(90);
            float h_median = hstat.GetMedian();
            // irregular: 50% of the area < 50% topheight: median=50% of the area
            if (h_median < h_top * 0.5F)
            {
                rIrregular = true;
            }
            else
            {
                rIrregular = false;
            }

            return h_top;
        }

        private void EstablishSproutsInAdjacentCells(Model model, TreeSpecies species, Point lightGridIndexXY) // C++: Saplings::vegetativeSprouting()
        {
            Debug.Assert(this.SaplingCells != null);

            // sprouts spread from a living tree with a low probability in adjacent cells
            ReadOnlySpan<int> offsets_x = [ 1, 1, 0, -1, -1, -1, 0, 1 ];
            ReadOnlySpan<int> offsets_y = [ 0, 1, 1, 1, 0, -1, -1, -1 ];

            bool isTorus = model.Project.World.Geometry.IsTorus;
            float p_resprout = species.SaplingGrowth.AdultSproutProbability;
            if (p_resprout > 0.0F && model.RandomGenerator.Value!.GetRandomProbability() < p_resprout)
            {
                // search for a neighbor cell to place sprouts in, starting with a random neigbor 
                int neighborIndex = model.RandomGenerator.Value!.GetRandomInteger(0, offsets_x.Length);
                for (int neighborsTested = 0; neighborsTested < offsets_x.Length; ++neighborsTested)
                {
                    Point neighborIndexXY = new(lightGridIndexXY.X + offsets_x[neighborIndex], lightGridIndexXY.Y + offsets_y[neighborIndex]);
                    SaplingCell? saplingCell = model.Landscape.GetSaplingCell(neighborIndexXY, onlyValid: true, out ResourceUnit sproutResourceUnit);
                    if ((saplingCell == null) || (isTorus && (Object.ReferenceEquals(sproutResourceUnit, this) == false))) 
                    {
                        // in torus mode saplings aren't grown in an adjacent resource unit
                        neighborIndex = (neighborIndex + 1) % 8; // move on...
                        continue;
                    }

                    if (saplingCell.TryGetSaplingOfSpecies(species.Index) == null)
                    {
                        // the species is not yet on the cell, so let us spread there....
                        Sapling? sapling = saplingCell.AddSaplingIfSlotFree(0.05F, 0, species.Index);
                        if (sapling != null)
                        {
                            sapling.IsSprout = true;
                            ++this.Trees.GetResourceUnitSpecies(species).SaplingStats.NewCohortsVegetative;
                            saplingCell.CheckState();
                        }
                        break;  // stop searching when one sprout was added
                    }

                    neighborIndex = (neighborIndex + 1) % 8; // move on...
                }
            }
        }

        /// notify a disturbance/management related activity happened on the resource unit
        /// this information is used for SVD states / context for state changes
        /// source: process caused activity (module), info: addition information, e.g. % killed trees
        public void OnDisturbance(Model model, ResourceUnitDisturbance source, float info) // C++: ResourceUnit::notifyDisturbance()
        {
            // events are stored with newest events first. Oldest event is removed
            // when maximum number of events reached
            this.SvdState.DisturbanceEvents.Insert(0, new ResourceUnitSvdState.SvdDisturbanceEvent(model.SimulationState.CurrentCalendarYear, source, info));
            if (this.SvdState.DisturbanceEvents.Count > 2)
            {
                this.SvdState.DisturbanceEvents.RemoveAt(this.SvdState.DisturbanceEvents.Count - 1);
            }
        }

        /// potential sources of disturbance used for SVD state tracking
        public enum ResourceUnitDisturbance // C++ ERUDisturbanceType
        { 
            None,
            Fire, // C++ dtFire
            BarkBeetle, // C++ dtBarkBeetle
            Wind, // C++ dtWind
            Bite, // C++ dtBite
            Abe, // C++ dtAbe
            Management // C++ dtManagement
        }

        public class ResourceUnitSvdState // C++ RUSVDState
        {
            public int StateID { get; set; } ///< the Id of the state the resource unit is in, C++ stateId
            public int PreviousStateID { get; set; } ///< the Id of the state the resource unit was previously in, C++ previousStateId
            public Int16 YearsInState { get; set; } ///< the number of years the RU is in state 'stateId', ++time
            public Int16 YearsInPreviousState { get; set; } ///< the number of years that the RU was in the previous state, C++ previousTime
            public List<float> LocalComposition { get; private init; } ///< save for each species the relative share in the moore-neighborhood, C++ localComposition
            public List<float> MidDistanceComposition { get; private init; } ///< save for each species the relative share in a regional neighborhood (200-300m), C++ midDistanceComposition
            // TODO: two element queue
            public List<SvdDisturbanceEvent> DisturbanceEvents { get; private init; } // C++ disturbanceEvents

            public ResourceUnitSvdState()
            {
                this.LocalComposition = [];
                this.MidDistanceComposition = [];
                this.DisturbanceEvents = [];
            }
            
            /// structure to store individual disturbance events
            public struct SvdDisturbanceEvent // C++ SVDDisturbanceEvent
            {
                public int Year { get; set; } // C++ year
                public ResourceUnitDisturbance Source { get; set; } // C++ source
                public float Info { get; set; } // C++ info

                public SvdDisturbanceEvent(int year, ResourceUnitDisturbance source, float info)
                {
                    this.Year = year;
                    this.Source = source;
                    this.Info = info;
                }
            }
            
            public void Clear() // C++ SVDDisturbanceEvent::clear()
            {
                this.StateID = 0;
                this.PreviousStateID = 0;
                this.YearsInState = 0;
                this.YearsInPreviousState = 0; 
            }
        }
    }
}
