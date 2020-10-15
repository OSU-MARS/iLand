using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Trees
{
    /** The Saplings class the container for the establishment and sapling growth in iLand.
     */
    public class Saplings
    {
        public void Setup(Model model)
        {
            //mGrid.setup(GlobalSettings.instance().model().grid().metricRect(), GlobalSettings.instance().model().grid().cellsize());
            Grid<float> lif_grid = model.LightGrid;
            // mask out out-of-project areas
            Grid<HeightCell> hg = model.HeightGrid;
            for (int i = 0; i < lif_grid.Count; ++i)
            {
                ResourceUnit ru = null;
                SaplingCell s = Cell(lif_grid.IndexOf(i), model, false, ref ru); // false: retrieve also invalid cells
                if (s != null)
                {
                    if (!hg[lif_grid.Index5(i)].IsInWorld())
                    {
                        s.State = SaplingCell.SaplingCellState.Invalid;
                    }
                    else
                    {
                        s.State = SaplingCell.SaplingCellState.Free;
                    }
                }
            }
        }

        public void CalculateInitialStatistics(ResourceUnit ru)
        {
            SaplingCell[] sap_cells = ru.SaplingCells;
            if (sap_cells == null)
            {
                return;
            }
            for (int i = 0; i < Constant.LightCellsPerHectare; ++i)
            {
                SaplingCell s = sap_cells[i];
                if (s.State != SaplingCell.SaplingCellState.Invalid)
                {
                    int cohorts_on_px = s.GetOccupiedSlotCount();
                    for (int j = 0; j < SaplingCell.SaplingsPerCell; ++j)
                    {
                        if (s.Saplings[j].IsOccupied())
                        {
                            SaplingTree tree = s.Saplings[j];
                            ResourceUnitSpecies rus = tree.ResourceUnitSpecies(ru);
                            rus.SaplingStats.LivingCohorts++;
                            double n_repr = rus.Species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(tree.Height) / (double)(cohorts_on_px);
                            if (tree.Height > 1.3f)
                            {
                                rus.SaplingStats.LivingSaplings += n_repr;
                            }
                            else
                            {
                                rus.SaplingStats.LivingSaplingsSmall += n_repr;
                            }

                            rus.SaplingStats.AverageHeight += tree.Height;
                            rus.SaplingStats.AverageAge += tree.Age;
                        }
                    }
                }
            }
        }

        public void Establishment(ResourceUnit ru, Model model)
        {
            Grid<float> lif_grid = model.LightGrid;

            Point imap = ru.CornerPointOffset; // offset on LIF/saplings grid
            Point iseedmap = new Point(imap.X / 10, imap.Y / 10); // seed-map has 20m resolution, LIF 2m . factor 10
            for (int i = 0; i < ru.Species.Count; ++i)
            {
                ru.Species[i].SaplingStats.ClearStatistics();
            }

            double[] lif_corr = new double[Constant.LightCellsPerHectare];
            for (int i = 0; i < Constant.LightCellsPerHectare; ++i)
            {
                lif_corr[i] = -1.0;
            }

            ru.SpeciesSet.GetRandomSpeciesSampleIndices(model, out int sampleBegin, out int sampleEnd);
            for (int sampleIndex = sampleBegin; sampleIndex != sampleEnd; ++sampleIndex)
            {
                // start from a random species (and cycle through the available species)
                int speciesIndex = ru.SpeciesSet.RandomSpeciesOrder[sampleIndex];
                ResourceUnitSpecies rus = ru.Species[speciesIndex];
                rus.Establishment.Clear();

                // check if there are seeds of the given species on the resource unit
                float seeds = 0.0F;
                Grid<float> seedmap = rus.Species.SeedDispersal.SeedMap;
                for (int iy = 0; iy < 5; ++iy)
                {
                    int p = seedmap.IndexOf(iseedmap.X, iseedmap.Y);
                    for (int ix = 0; ix < 5; ++ix)
                    {
                        seeds += seedmap[p++];
                    }
                }
                // if there are no seeds: no need to do more
                if (seeds == 0.0F)
                {
                    continue;
                }

                // calculate the abiotic environment (TACA)
                rus.Establishment.CalculateAbioticEnvironment(model);
                double abiotic_env = rus.Establishment.AbioticEnvironment;
                if (abiotic_env == 0.0)
                {
                    // rus.Establishment.WriteDebugOutputs();
                    continue;
                }

                // loop over all 2m cells on this resource unit
                SaplingCell[] sap_cells = ru.SaplingCells;
                for (int iy = 0; iy < Constant.LightPerRUsize; ++iy)
                {
                    int isc = lif_grid.IndexOf(imap.X, imap.Y + iy); // index on 2m cell
                    for (int ix = 0; ix < Constant.LightPerRUsize; ++ix, ++isc)
                    {
                        SaplingCell s = sap_cells[iy * Constant.LightPerRUsize + ix]; // pointer to a row
                        if (s.State == SaplingCell.SaplingCellState.Free)
                        {
                            // is a sapling of the current species already on the pixel?
                            // * test for sapling height already in cell state
                            // * test for grass-cover already in cell state
                            SaplingTree stree = null;
                            SaplingTree[] slots = s.Saplings;
                            for (int i = 0; i < SaplingCell.SaplingsPerCell; ++i)
                            {
                                if (stree == null && !slots[i].IsOccupied())
                                {
                                    stree = slots[i];
                                }
                                if (slots[i].SpeciesIndex == speciesIndex)
                                {
                                    stree = null;
                                    break;
                                }
                            }

                            if (stree != null)
                            {
                                // grass cover?
                                float seed_map_value = seedmap[lif_grid.Index10(isc)];
                                if (seed_map_value == 0.0F)
                                {
                                    continue;
                                }
                                float lif_value = lif_grid[isc];

                                double lif_corrected = lif_corr[iy * Constant.LightPerRUsize + ix];
                                // calculate the LIFcorrected only once per pixel; the relative height is 0 (light level on the forest floor)
                                if (lif_corrected < 0.0)
                                {
                                    lif_corrected = rus.Species.SpeciesSet.GetLriCorrection(model, lif_value, 0.0);
                                }

                                // check for the combination of seed availability and light on the forest floor
                                if (model.RandomGenerator.Random() < seed_map_value * lif_corrected * abiotic_env)
                                {
                                    // ok, lets add a sapling at the given position (age is incremented later)
                                    stree.SetSapling(0.05f, 0, speciesIndex);
                                    s.CheckState();
                                    rus.SaplingStats.NewSaplings++;
                                }
                            }

                        }
                    }
                }
                // create debug output related to establishment
                // rus.Establishment.WriteDebugOutputs();
            }
        }

        public void SaplingGrowth(ResourceUnit ru, Model model)
        {
            Grid<HeightCell> height_grid = model.HeightGrid;
            Grid<float> lif_grid = model.LightGrid;

            Point imap = ru.CornerPointOffset;
            SaplingCell[] sap_cells = ru.SaplingCells;

            for (int iy = 0; iy < Constant.LightPerRUsize; ++iy)
            {
                int isc = lif_grid.IndexOf(imap.X, imap.Y + iy);
                for (int ix = 0; ix < Constant.LightPerRUsize; ++ix, ++isc)
                {
                    SaplingCell s = sap_cells[iy * Constant.LightPerRUsize + ix]; // ptr to row
                    if (s.State != SaplingCell.SaplingCellState.Invalid)
                    {
                        bool need_check = false;
                        int n_on_px = s.GetOccupiedSlotCount();
                        for (int i = 0; i < SaplingCell.SaplingsPerCell; ++i)
                        {
                            if (s.Saplings[i].IsOccupied())
                            {
                                // growth of this sapling tree
                                HeightCell hgv = height_grid[height_grid.Index5(isc)];
                                float lif_value = lif_grid[isc];

                                need_check |= GrowSapling(ru, model, s, s.Saplings[i], isc, hgv.Height, lif_value, n_on_px);
                            }
                        }
                        if (need_check)
                        {
                            s.CheckState();
                        }
                    }
                }
            }

            // store statistics on saplings/regeneration
            for (int i = 0; i < ru.Species.Count; ++i)
            {
                ResourceUnitSpecies species = ru.Species[i];
                species.SaplingStats.Calculate(species.Species, ru, model.GlobalSettings);
                species.Statistics.Add(species.SaplingStats);
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
        /// if 'only_valid' is true, then 0 is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell Cell(Point lif_coords, Model model, bool only_valid, ref ResourceUnit rRUPtr)
        {
            Grid<float> lif_grid = model.LightGrid;

            // in this case, getting the actual cell is quite cumbersome: first, retrieve the resource unit, then the
            // cell based on the offset of the given coordiantes relative to the corner of the resource unit.
            ResourceUnit ru = model.GetResourceUnit(lif_grid.GetCellCenterPoint(lif_coords));
            if (rRUPtr != null)
            {
                rRUPtr = ru;
            }

            if (ru != null)
            {
                Point local_coords = lif_coords.Subtract(ru.CornerPointOffset);
                int idx = local_coords.Y * Constant.LightPerRUsize + local_coords.X;
                Debug.WriteLineIf(idx < 0 || idx >= Constant.LightCellsPerHectare, "invalid coords in cell");
                SaplingCell s = ru.SaplingCells[idx];
                if (s != null && (!only_valid || s.State != SaplingCell.SaplingCellState.Invalid))
                {
                    return s;
                }
            }
            return null;
        }

        public void ClearSaplings(RectangleF rectangle, Model model, bool remove_biomass)
        {
            GridRunner<float> runner = new GridRunner<float>(model.LightGrid, rectangle);
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                ResourceUnit ru = null;
                SaplingCell s = Cell(runner.CurrentIndex(), model, true, ref ru);
                if (s != null)
                {
                    ClearSaplings(s, ru, remove_biomass);
                }
            }
        }

        public void ClearSaplings(SaplingCell s, ResourceUnit ru, bool remove_biomass)
        {
            if (s != null)
            {
                for (int i = 0; i < SaplingCell.SaplingsPerCell; ++i)
                {
                    if (s.Saplings[i].IsOccupied())
                    {
                        if (!remove_biomass)
                        {
                            ResourceUnitSpecies rus = s.Saplings[i].ResourceUnitSpecies(ru);
                            if (rus == null && rus.Species != null)
                            {
                                Debug.WriteLine("clearSaplings(): invalid resource unit!!!");
                                return;
                            }
                            rus.SaplingStats.AddCarbonOfDeadSapling(s.Saplings[i].Height / rus.Species.SaplingGrowthParameters.HdSapling * 100.0F);
                        }
                        s.Saplings[i].Clear();
                    }
                }
                s.CheckState();
            }
        }

        public int AddSprout(Tree t, Model model)
        {
            if (t.Species.SaplingGrowthParameters.SproutGrowth == 0.0)
            {
                return 0;
            }
            ResourceUnit ru = null;
            SaplingCell sc = Cell(t.LightCellPosition, model, true, ref ru);
            if (sc == null)
            {
                return 0;
            }
            ClearSaplings(sc, t.RU, false);
            SaplingTree st = sc.AddSapling(0.05f, 0, t.Species.Index);
            if (st != null)
            {
                st.SetSprout(true);
            }

            // neighboring cells
            double crown_area = t.GetCrownRadius() * t.GetCrownRadius() * Math.PI; //m2
            // calculate how many cells on the ground are covered by the crown (this is a rather rough estimate)
            // n_cells: in addition to the original cell
            int n_cells = (int)Math.Round(crown_area / (double)(Constant.LightSize * Constant.LightSize) - 1.0);
            if (n_cells > 0)
            {
                int[] offsets_x = new int[] { 1, 1, 0, -1, -1, -1, 0, 1 };
                int[] offsets_y = new int[] { 0, 1, 1, 1, 0, -1, -1, -1 };
                int s = model.RandomGenerator.Random(0, 8);
                ru = null;
                while (n_cells > 0)
                {
                    sc = Cell(t.LightCellPosition.Add(new Point(offsets_x[s], offsets_y[s])), model, true, ref ru);
                    if (sc != null)
                    {
                        ClearSaplings(sc, ru, false);
                        st = sc.AddSapling(0.05F, 0, t.Species.Index);
                        if (st != null)
                        {
                            st.SetSprout(true);
                        }
                    }

                    s = (s + 1) % 8; --n_cells;
                }
            }
            return 1;
        }

        private bool GrowSapling(ResourceUnit ru, Model model, SaplingCell scell, SaplingTree tree, int isc, float dom_height, float lif_value, int cohorts_on_px)
        {
            ResourceUnitSpecies rus = tree.ResourceUnitSpecies(ru);
            Species species = rus.Species;

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            double h_pot = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model, tree.Height);
            double delta_h_pot = h_pot - tree.Height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            if (dom_height == 0.0F)
            {
                throw new NotSupportedException(String.Format("growSapling: height grid at {0} has value 0", isc));
            }

            double rel_height = tree.Height / dom_height;

            double lif_corrected = species.SpeciesSet.GetLriCorrection(model, lif_value, rel_height); // correction based on height

            double lr = species.GetLightResponse(model, lif_corrected); // species specific light response (LUI, light utilization index)

            rus.Calculate(model, true); // calculate the 3pg module (this is done only once per RU); true: call comes from regeneration
            double f_env_yr = rus.BiomassGrowth.EnvironmentalFactor;

            double delta_h_factor = f_env_yr * lr; // relative growth

            if (h_pot < 0.0 || delta_h_pot < 0.0 || lif_corrected < 0.0 || lif_corrected > 1.0 || delta_h_factor < 0.0 || delta_h_factor > 1.0)
            {
                Debug.WriteLine("invalid values in Sapling::growSapling");
            }

            // sprouts grow faster. Sprouts therefore are less prone to stress (threshold), and can grow higher than the growth potential.
            if (tree.IsSprout())
            {
                delta_h_factor *= species.SaplingGrowthParameters.SproutGrowth;
            }

            // check browsing
            if (model.ModelSettings.BrowsingPressure > 0.0 && tree.Height <= 2.0F)
            {
                double p = rus.Species.SaplingGrowthParameters.BrowsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) . odds_mod = odds * browsingPressure . p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = p * model.ModelSettings.BrowsingPressure / (1.0 - p + p * model.ModelSettings.BrowsingPressure);
                if (model.RandomGenerator.Random() < p_browse)
                {
                    delta_h_factor = 0.0;
                }
            }

            // check mortality of saplings
            if (delta_h_factor < species.SaplingGrowthParameters.StressThreshold)
            {
                tree.StressYears++;
                if (tree.StressYears > species.SaplingGrowthParameters.MaxStressYears)
                {
                    // sapling dies...
                    rus.SaplingStats.AddCarbonOfDeadSapling(tree.Height / species.SaplingGrowthParameters.HdSapling * 100.0F);
                    tree.Clear();
                    return true; // need cleanup
                }
            }
            else
            {
                tree.StressYears = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * delta_h_factor < 0.0F || (!tree.IsSprout() && delta_h_pot * delta_h_factor > 2.0), "Sapling::growSapling", "implausible height growth.");

            // grow
            tree.Height += (float)(delta_h_pot * delta_h_factor);
            tree.Age++; // increase age of sapling by 1

            // recruitment?
            if (tree.Height > 4.0F)
            {
                rus.SaplingStats.RecruitedSaplings++;

                float dbh = tree.Height / species.SaplingGrowthParameters.HdSapling * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(dbh);
                int to_establish = (int)(n_trees);

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 . add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (model.RandomGenerator.Random() < (n_trees - to_establish) || to_establish == 0)
                {
                    to_establish++;
                }

                // add a new tree
                for (int i = 0; i < to_establish; i++)
                {
                    Tree bigtree = ru.AddNewTree(model);
                    bigtree.LightCellPosition = model.LightGrid.IndexOf(isc);
                    // add variation: add +/-N% to dbh and *independently* to height.
                    bigtree.Dbh = (float)(dbh * model.RandomGenerator.Random(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation));
                    bigtree.SetHeight((float)(tree.Height * model.RandomGenerator.Random(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation)));
                    bigtree.Species = species;
                    bigtree.SetAge(tree.Age, tree.Height);
                    bigtree.RU = ru;
                    bigtree.Setup(model);
                    rus.Statistics.Add(bigtree, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                tree.Clear(); // clear this tree (no carbon flow to the ground)
                for (int i = 0; i < SaplingCell.SaplingsPerCell; ++i)
                {
                    if (scell.Saplings[i].IsOccupied())
                    {
                        // add carbon to the ground
                        ResourceUnitSpecies srus = scell.Saplings[i].ResourceUnitSpecies(ru);
                        srus.SaplingStats.AddCarbonOfDeadSapling(scell.Saplings[i].Height / srus.Species.SaplingGrowthParameters.HdSapling * 100.0F);
                        scell.Saplings[i].Clear();
                    }
                }
                return true; // need cleanup
            }
            // book keeping (only for survivors) for the sapling of the resource unit / species
            SaplingStat ss = rus.SaplingStats;
            double n_repr = species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(tree.Height) / (double)(cohorts_on_px);
            if (tree.Height > 1.3F)
            {
                ss.LivingSaplings += n_repr;
            }
            else
            {
                ss.LivingSaplingsSmall += n_repr;
            }
            ss.LivingCohorts++;
            ss.AverageHeight += tree.Height;
            ss.AverageAge += tree.Age;
            ss.AverageDeltaHPot += delta_h_pot;
            ss.AverageDeltaHRealized += delta_h_pot * delta_h_factor;
            return false;
        }
    }
}
