using iLand.Simulation;
using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    /** The Saplings class the container for the establishment and sapling growth in iLand.
     */
    public class Saplings
    {
        public void Setup(Model model)
        {
            //mGrid.setup(GlobalSettings.instance().model().grid().metricRect(), GlobalSettings.instance().model().grid().cellsize());
            Grid<float> lightGrid = model.LightGrid;
            // mask out out-of-project areas
            Grid<HeightCell> heightGrid = model.HeightGrid;
            GridWindowEnumerator<float> lightRunner = new GridWindowEnumerator<float>(model.LightGrid, model.ResourceUnitGrid.PhysicalExtent);
            while (lightRunner.MoveNext())
            {
                SaplingCell saplingCell = this.GetCell(model, lightGrid.GetCellPosition(lightRunner.CurrentIndex), false, out ResourceUnit _); // false: retrieve also invalid cells
                if (saplingCell != null)
                {
                    if (!heightGrid[lightGrid.Index5(lightRunner.CurrentIndex)].IsInWorld())
                    {
                        saplingCell.State = SaplingCellState.Invalid;
                    }
                    else
                    {
                        saplingCell.State = SaplingCellState.Free;
                    }
                }
            }
        }

        public void CalculateInitialStatistics(ResourceUnit ru)
        {
            if (ru.SaplingCells == null)
            {
                return;
            }

            for (int lightCellIndex = 0; lightCellIndex < Constant.LightCellsPerHectare; ++lightCellIndex)
            {
                SaplingCell saplingCell = ru.SaplingCells[lightCellIndex];
                if (saplingCell.State != SaplingCellState.Invalid)
                {
                    int cohortsInCell = saplingCell.GetOccupiedSlotCount();
                    for (int saplingCellIndex = 0; saplingCellIndex < saplingCell.Saplings.Length; ++saplingCellIndex)
                    {
                        if (saplingCell.Saplings[saplingCellIndex].IsOccupied())
                        {
                            Sapling sapling = saplingCell.Saplings[saplingCellIndex];
                            ResourceUnitSpecies ruSpecies = sapling.GetResourceUnitSpecies(ru);
                            ++ruSpecies.SaplingStats.LivingCohorts;
                            float nRepresented = ruSpecies.Species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(sapling.Height) / cohortsInCell;
                            if (sapling.Height > 1.3F)
                            {
                                ruSpecies.SaplingStats.LivingSaplings += nRepresented;
                            }
                            else
                            {
                                ruSpecies.SaplingStats.LivingSaplingsSmall += nRepresented;
                            }

                            ruSpecies.SaplingStats.AverageHeight += sapling.Height;
                            ruSpecies.SaplingStats.AverageAge += sapling.Age;
                        }
                    }
                }
            }
        }

        public void EstablishSaplings(Model model, ResourceUnit ru)
        {
            for (int species = 0; species < ru.TreeSpecies.Count; ++species)
            {
                ru.TreeSpecies[species].SaplingStats.ClearStatistics();
            }

            double[] lightCorrection = new double[Constant.LightCellsPerHectare];
            Array.Fill(lightCorrection, -1.0);

            Point ruOrigin = ru.TopLeftLightPosition; // offset on LIF/saplings grid
            Point seedmapOrigin = new Point(ruOrigin.X / Constant.LightCellsPerSeedmapSize, ruOrigin.Y / Constant.LightCellsPerSeedmapSize); // seed-map has 20m resolution, LIF 2m . factor 10
            ru.TreeSpeciesSet.GetRandomSpeciesSampleIndices(model, out int sampleBegin, out int sampleEnd);
            for (int sampleIndex = sampleBegin; sampleIndex != sampleEnd; ++sampleIndex)
            {
                // start from a random species (and cycle through the available species)
                int speciesIndex = ru.TreeSpeciesSet.RandomSpeciesOrder[sampleIndex];
                ResourceUnitSpecies ruSpecies = ru.TreeSpecies[speciesIndex];
                ruSpecies.Establishment.Clear();
                Grid<float> seedMap = ruSpecies.Species.SeedDispersal.SeedMap;

                // check if there are seeds of the given species on the resource unit
                float seeds = 0.0F;
                for (int seedIndexY = 0; seedIndexY < 5; ++seedIndexY)
                {
                    int seedIndex = seedMap.IndexOf(seedmapOrigin.X, seedmapOrigin.Y);
                    for (int seedIndexX = 0; seedIndexX < 5; ++seedIndexX)
                    {
                        seeds += seedMap[seedIndex++];
                    }
                }
                // if there are no seeds: no need to do more
                if (seeds == 0.0F)
                {
                    continue;
                }

                // calculate the abiotic environment (TACA)
                ruSpecies.Establishment.CalculateAbioticEnvironment(model);
                if (ruSpecies.Establishment.AbioticEnvironment == 0.0)
                {
                    // rus.Establishment.WriteDebugOutputs();
                    continue;
                }

                // loop over all 2m cells on this resource unit
                Grid<float> lightGrid = model.LightGrid;
                for (int lightIndexY = 0; lightIndexY < Constant.LightCellsPerRUsize; ++lightIndexY)
                {
                    int lightIndex = lightGrid.IndexOf(ruOrigin.X, ruOrigin.Y + lightIndexY); // index on 2m cell
                    for (int lightIndexX = 0; lightIndexX < Constant.LightCellsPerRUsize; ++lightIndexX, ++lightIndex)
                    {
                        SaplingCell saplingCell = ru.SaplingCells[lightIndexY * Constant.LightCellsPerRUsize + lightIndexX]; // pointer to a row
                        if (saplingCell.State == SaplingCellState.Free)
                        {
                            // is a sapling of the current species already on the pixel?
                            // * test for sapling height already in cell state
                            // * test for grass-cover already in cell state
                            Sapling sapling = null;
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
                                float seedMapValue = seedMap[lightGrid.Index10(lightIndex)];
                                if (seedMapValue == 0.0F)
                                {
                                    continue;
                                }

                                float lightValue = lightGrid[lightIndex];
                                double lriCorrection = lightCorrection[lightIndexY * Constant.LightCellsPerRUsize + lightIndexX];
                                // calculate the LIFcorrected only once per pixel; the relative height is 0 (light level on the forest floor)
                                if (lriCorrection < 0.0)
                                {
                                    // TODO: lightCorrection[] is never updated -> no caching?
                                    lriCorrection = ruSpecies.Species.SpeciesSet.GetLriCorrection(model, lightValue, 0.0F);
                                }

                                // check for the combination of seed availability and light on the forest floor
                                double pGermination = seedMapValue * lriCorrection * ruSpecies.Establishment.AbioticEnvironment;
                                if (model.RandomGenerator.GetRandomDouble() < pGermination)
                                {
                                    // ok, lets add a sapling at the given position (age is incremented later)
                                    sapling.SetSapling(Constant.Sapling.MinimumHeight, 0, speciesIndex);
                                    saplingCell.CheckState();
                                    ++ruSpecies.SaplingStats.NewSaplings;
                                }
                            }
                                                    }
                    }
                }
                // create debug output related to establishment
                // rus.Establishment.WriteDebugOutputs();
            }
        }

        public void GrowSaplings(Model model, ResourceUnit ru)
        {
            Grid<HeightCell> heightGrid = model.HeightGrid;
            Grid<float> lightGrid = model.LightGrid;

            Point ruOrigin = ru.TopLeftLightPosition;
            for (int lightIndexY = 0; lightIndexY < Constant.LightCellsPerRUsize; ++lightIndexY)
            {
                int lightIndex = lightGrid.IndexOf(ruOrigin.X, ruOrigin.Y + lightIndexY);
                for (int lightIndexX = 0; lightIndexX < Constant.LightCellsPerRUsize; ++lightIndexX, ++lightIndex)
                {
                    SaplingCell saplingCell = ru.SaplingCells[lightIndexY * Constant.LightCellsPerRUsize + lightIndexX]; // ptr to row
                    if (saplingCell.State != SaplingCellState.Invalid)
                    {
                        bool checkCellState = false;
                        int nSaplings = saplingCell.GetOccupiedSlotCount();
                        for (int index = 0; index < saplingCell.Saplings.Length; ++index)
                        {
                            if (saplingCell.Saplings[index].IsOccupied())
                            {
                                // growth of this sapling tree
                                HeightCell heightCell = heightGrid[heightGrid.Index5(lightIndex)];
                                float lightValue = lightGrid[lightIndex];

                                checkCellState |= this.GrowSaplings(ru, model, saplingCell, saplingCell.Saplings[index], lightIndex, heightCell.Height, lightValue, nSaplings);
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
            for (int species = 0; species < ru.TreeSpecies.Count; ++species)
            {
                ResourceUnitSpecies ruSpecies = ru.TreeSpecies[species];
                ruSpecies.SaplingStats.Recalculate(model, ru, ruSpecies.Species);
                ruSpecies.Statistics.Add(ruSpecies.SaplingStats);
            }

            // debug output related to saplings
            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.SaplingGrowth))
            //{
            //    // establishment details
            //    for (int it = 0; it != ru.Species.Count; ++it)
            //    {
            //        ResourceUnitSpecies species = ru.Species[it];
            //        if (species.SaplingStats.LivingCohorts == 0)
            //        {
            //            continue;
            //        }

            //        List<object> output = GlobalSettings.Instance.DebugList(ru.Index, DebugOutputs.SaplingGrowth);
            //        output.AddRange(new object[] { species.Species.ID, ru.Index, ru.ID,
            //                                       species.SaplingStats.LivingCohorts, species.SaplingStats.AverageHeight, species.SaplingStats.AverageAge,
            //                                       species.SaplingStats.AverageDeltaHPot, species.SaplingStats.AverageDeltaHRealized,
            //                                       species.SaplingStats.NewSaplings, species.SaplingStats.DeadSaplings,
            //                                       species.SaplingStats.RecruitedSaplings, species.Species.SaplingGrowthParameters.ReferenceRatio });
            //    }
            //}
        }

        /// return the SaplingCell (i.e. container for the ind. saplings) for the given 2x2m coordinates
        /// if 'only_valid' is true, then null is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell GetCell(Model model, Point lightCellPosition, bool onlyValid, out ResourceUnit ru)
        {
            // in this case, getting the actual cell is quite cumbersome: first, retrieve the resource unit, then the
            // cell based on the offset of the given coordinates relative to the corner of the resource unit.
            ru = model.GetResourceUnit(model.LightGrid.GetCellCenterPosition(lightCellPosition));
            if (ru == null)
            {
                // TODO: can this be removed? GetCell() shouldn't be called outside of RU grid?
                return null;
            }

            Point ruLightCellPosition = lightCellPosition.Subtract(ru.TopLeftLightPosition);
            int lightIndex = ruLightCellPosition.Y * Constant.LightCellsPerRUsize + ruLightCellPosition.X;
            Debug.Assert(lightIndex >= 0 && lightIndex <= Constant.LightCellsPerHectare, "Invalid light cell index.");
            SaplingCell saplingCell = ru.SaplingCells[lightIndex];
            if ((saplingCell != null) && (!onlyValid || saplingCell.State != SaplingCellState.Invalid))
            {
                return saplingCell;
            }
            return null;
        }

        //public void ClearSaplings(Model model, RectangleF areaToClear, bool removeBiomass)
        //{
        //    GridWindowEnumerator<float> lightRunner = new GridWindowEnumerator<float>(model.LightGrid, areaToClear);
        //    while (lightRunner.MoveNext())
        //    {
        //        SaplingCell saplngCell = this.GetCell(model, lightRunner.GetCellPosition(), true, out ResourceUnit ru);
        //        if (saplngCell != null)
        //        {
        //            this.ClearSaplings(ru, saplngCell, removeBiomass);
        //        }
        //    }
        //}

        public void ClearSaplings(ResourceUnit ru, SaplingCell saplingCell, bool removeBiomass)
        {
            if (saplingCell != null)
            {
                for (int index = 0; index < saplingCell.Saplings.Length; ++index)
                {
                    if (saplingCell.Saplings[index].IsOccupied())
                    {
                        if (!removeBiomass)
                        {
                            ResourceUnitSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(ru);
                            if (ruSpecies == null && ruSpecies.Species != null)
                            {
                                Debug.WriteLine("clearSaplings(): invalid resource unit!!!");
                                return;
                            }
                            ruSpecies.SaplingStats.AddCarbonOfDeadSapling(saplingCell.Saplings[index].Height / ruSpecies.Species.SaplingGrowthParameters.HeightDiameterRatio * 100.0F);
                        }
                        saplingCell.Saplings[index].Clear();
                    }
                }
                saplingCell.CheckState();
            }
        }

        public void AddSprout(Model model, Trees trees, int treeIndex)
        {
            if (trees.Species.SaplingGrowthParameters.SproutGrowth == 0.0F)
            {
                return;
            }
            SaplingCell saplingCell = GetCell(model, trees.LightCellPosition[treeIndex], true, out ResourceUnit _);
            if (saplingCell == null)
            {
                return;
            }

            ClearSaplings(trees.RU, saplingCell, false);
            Sapling sapling = saplingCell.AddSaplingIfSlotFree(Constant.Sapling.MinimumHeight, 0, trees.Species.Index);
            if (sapling != null)
            {
                sapling.IsSprout = true;
            }

            // neighboring cells
            double crownArea = trees.GetCrownRadius(treeIndex) * trees.GetCrownRadius(treeIndex) * Math.PI; //m2
            // calculate how many cells on the ground are covered by the crown (this is a rather rough estimate)
            // n_cells: in addition to the original cell
            int lightCellsInCrown = (int)Math.Round(crownArea / (Constant.LightSize * Constant.LightSize) - 1.0);
            if (lightCellsInCrown > 0)
            {
                int[] offsetsX = new int[] { 1, 1, 0, -1, -1, -1, 0, 1 };
                int[] offsetsY = new int[] { 0, 1, 1, 1, 0, -1, -1, -1 };
                int neighbor = model.RandomGenerator.GetRandomInteger(0, 8);
                for(; lightCellsInCrown > 0; --lightCellsInCrown)
                {
                    saplingCell = this.GetCell(model, trees.LightCellPosition[treeIndex].Add(new Point(offsetsX[neighbor], offsetsY[neighbor])), true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        this.ClearSaplings(ru, saplingCell, false);
                        sapling = saplingCell.AddSaplingIfSlotFree(Constant.Sapling.MinimumHeight, 0, trees.Species.Index);
                        if (sapling != null)
                        {
                            sapling.IsSprout = true;
                        }
                    }

                    neighbor = (neighbor + 1) % 8;
                }
            }
        }

        private bool GrowSaplings(ResourceUnit ru, Model model, SaplingCell saplingCell, Sapling sapling, int lightIndex, float dominantHeight, float lif_value, int cohorts_on_px)
        {
            ResourceUnitSpecies ruSpecies = sapling.GetResourceUnitSpecies(ru);
            TreeSpecies species = ruSpecies.Species;

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            float h_pot = (float)species.SaplingGrowthParameters.HeightGrowthPotential.Evaluate(model, sapling.Height);
            float delta_h_pot = h_pot - sapling.Height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            if (dominantHeight <= 0.0F)
            {
                throw new NotSupportedException(String.Format("Dominant height grid has value 0 at light cell index {0}.", lightIndex));
            }

            float relativeHeight = sapling.Height / dominantHeight;
            float lriCorrection = species.SpeciesSet.GetLriCorrection(model, lif_value, relativeHeight); // correction based on height
            float lightResponse = species.GetLightResponse(model, lriCorrection); // species specific light response (LUI, light utilization index)

            ruSpecies.CalculateBiomassGrowthForYear(model, fromEstablishment: true); // calculate the 3pg module (this is done only once per RU); true: call comes from regeneration
            float siteEnvironmentHeightMultiplier = ruSpecies.BiomassGrowth.SiteEnvironmentSaplingHeightGrowthMultiplier;
            float heightGrowthFactor = siteEnvironmentHeightMultiplier * lightResponse; // relative growth

            if (h_pot < 0.0 || delta_h_pot < 0.0 || lriCorrection < 0.0 || lriCorrection > 1.0 || heightGrowthFactor < 0.0 || heightGrowthFactor > 1.0)
            {
                Debug.WriteLine("invalid values in Sapling::growSapling");
            }

            // sprouts grow faster. Sprouts therefore are less prone to stress (threshold), and can grow higher than the growth potential.
            if (sapling.IsSprout)
            {
                heightGrowthFactor *= species.SaplingGrowthParameters.SproutGrowth;
            }

            // check browsing
            if (model.ModelSettings.BrowsingPressure > 0.0 && sapling.Height <= 2.0F)
            {
                double p = ruSpecies.Species.SaplingGrowthParameters.BrowsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) . odds_mod = odds * browsingPressure . p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = p * model.ModelSettings.BrowsingPressure / (1.0 - p + p * model.ModelSettings.BrowsingPressure);
                if (model.RandomGenerator.GetRandomDouble() < p_browse)
                {
                    heightGrowthFactor = 0.0F;
                }
            }

            // check mortality of saplings
            if (heightGrowthFactor < species.SaplingGrowthParameters.StressThreshold)
            {
                sapling.StressYears++;
                if (sapling.StressYears > species.SaplingGrowthParameters.MaxStressYears)
                {
                    // sapling dies...
                    ruSpecies.SaplingStats.AddCarbonOfDeadSapling(sapling.Height / species.SaplingGrowthParameters.HeightDiameterRatio * 100.0F);
                    sapling.Clear();
                    return true; // need cleanup
                }
            }
            else
            {
                sapling.StressYears = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * heightGrowthFactor < 0.0F || (!sapling.IsSprout && delta_h_pot * heightGrowthFactor > 2.0), "Sapling::growSapling", "implausible height growth.");

            // grow
            sapling.Height += (float)(delta_h_pot * heightGrowthFactor);
            sapling.Age++; // increase age of sapling by 1

            // recruitment?
            if (sapling.Height > 4.0F)
            {
                ruSpecies.SaplingStats.RecruitedSaplings++;

                float dbh = sapling.Height / species.SaplingGrowthParameters.HeightDiameterRatio * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(dbh);
                int saplingsToEstablish = (int)(n_trees);

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 . add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (model.RandomGenerator.GetRandomDouble() < (n_trees - saplingsToEstablish) || saplingsToEstablish == 0)
                {
                    saplingsToEstablish++;
                }

                // add a new tree
                for (int saplingIndex = 0; saplingIndex < saplingsToEstablish; saplingIndex++)
                {
                    int treeIndex = ru.AddTree(model, species.ID);
                    Trees treesOfSpecies = ru.TreesBySpeciesID[species.ID];
                    treesOfSpecies.LightCellPosition[treeIndex] = model.LightGrid.GetCellPosition(lightIndex);
                    // add variation: add +/-N% to dbh and *independently* to height.
                    treesOfSpecies.Dbh[treeIndex] = (float)(dbh * model.RandomGenerator.GetRandomDouble(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation));
                    treesOfSpecies.SetHeight(treeIndex, (float)(sapling.Height * model.RandomGenerator.GetRandomDouble(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation)));
                    treesOfSpecies.Species = species;
                    treesOfSpecies.SetAge(treeIndex, sapling.Age, sapling.Height);
                    treesOfSpecies.Setup(model, treeIndex);
                    ruSpecies.Statistics.Add(treesOfSpecies, treeIndex, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                sapling.Clear(); // clear this tree (no carbon flow to the ground)
                for (int cellIndex = 0; cellIndex < saplingCell.Saplings.Length; ++cellIndex)
                {
                    if (saplingCell.Saplings[cellIndex].IsOccupied())
                    {
                        // add carbon to the ground
                        ResourceUnitSpecies srus = saplingCell.Saplings[cellIndex].GetResourceUnitSpecies(ru);
                        srus.SaplingStats.AddCarbonOfDeadSapling(saplingCell.Saplings[cellIndex].Height / srus.Species.SaplingGrowthParameters.HeightDiameterRatio * 100.0F);
                        saplingCell.Saplings[cellIndex].Clear();
                    }
                }
                return true; // need cleanup
            }
            // book keeping (only for survivors) for the sapling of the resource unit / species
            SaplingProperties saplingStats = ruSpecies.SaplingStats;
            float n_repr = species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(sapling.Height) / cohorts_on_px;
            if (sapling.Height > 1.3F)
            {
                saplingStats.LivingSaplings += n_repr;
            }
            else
            {
                saplingStats.LivingSaplingsSmall += n_repr;
            }
            saplingStats.LivingCohorts++;
            saplingStats.AverageHeight += sapling.Height;
            saplingStats.AverageAge += sapling.Age;
            saplingStats.AverageDeltaHPot += delta_h_pot;
            saplingStats.AverageDeltaHRealized += delta_h_pot * heightGrowthFactor;
            return false;
        }
    }
}
