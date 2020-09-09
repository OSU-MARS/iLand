using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    /** The Saplings class the container for the establishment and sapling growth in iLand.
     *
     */
    internal class Saplings
    {
        private static double mRecruitmentVariation = 0.1; // +/- 10%
        private static double mBrowsingPressure = 0.0;

        public static void setRecruitmentVariation(double variation) { mRecruitmentVariation = variation; }

        public void setup()
        {
            //mGrid.setup(GlobalSettings.instance().model().grid().metricRect(), GlobalSettings.instance().model().grid().cellsize());
            Grid<float> lif_grid = GlobalSettings.instance().model().grid();
            // mask out out-of-project areas
            Grid<HeightGridValue> hg = GlobalSettings.instance().model().heightGrid();
            for (int i = 0; i < lif_grid.count(); ++i)
            {
                ResourceUnit ru = null;
                SaplingCell s = cell(lif_grid.indexOf(i), false, ref ru); // false: retrieve also invalid cells
                if (s != null)
                {
                    if (!hg.valueAtIndex(lif_grid.index5(i)).isValid())
                    {
                        s.state = SaplingCell.ECellState.CellInvalid;
                    }
                    else
                    {
                        s.state = SaplingCell.ECellState.CellFree;
                    }
                }
            }
        }

        public void calculateInitialStatistics(ResourceUnit ru)
        {
            SaplingCell[] sap_cells = ru.saplingCellArray();
            if (sap_cells == null)
            {
                return;
            }
            for (int i = 0; i < Constant.cPxPerHectare; ++i)
            {
                SaplingCell s = sap_cells[i];
                if (s.state != SaplingCell.ECellState.CellInvalid)
                {
                    int cohorts_on_px = s.n_occupied();
                    for (int j = 0; j < SaplingCell.NSAPCELLS; ++j)
                    {
                        if (s.saplings[j].is_occupied())
                        {
                            SaplingTree tree = s.saplings[j];
                            ResourceUnitSpecies rus = tree.resourceUnitSpecies(ru);
                            rus.saplingStat().mLiving++;
                            double n_repr = rus.species().saplingGrowthParameters().representedStemNumberH(tree.height) / (double)(cohorts_on_px);
                            if (tree.height > 1.3f)
                            {
                                rus.saplingStat().mLivingSaplings += n_repr;
                            }
                            else
                            {
                                rus.saplingStat().mLivingSmallSaplings += n_repr;
                            }

                            rus.saplingStat().mAvgHeight += tree.height;
                            rus.saplingStat().mAvgAge += tree.age;
                        }
                    }
                }
            }
        }

        public void establishment(ResourceUnit ru)
        {
            Grid<float> lif_grid = GlobalSettings.instance().model().grid();

            Point imap = ru.cornerPointOffset(); // offset on LIF/saplings grid
            Point iseedmap = new Point(imap.X / 10, imap.Y / 10); // seed-map has 20m resolution, LIF 2m . factor 10
            for (int i = 0; i < ru.ruSpecies().Count; ++i)
            {
                ru.ruSpecies()[i].saplingStat().clearStatistics();
            }

            double[] lif_corr = new double[Constant.cPxPerHectare];
            for (int i = 0; i < Constant.cPxPerHectare; ++i)
            {
                lif_corr[i] = -1.0;
            }

            ru.speciesSet().randomSpeciesOrder(out int sbegin, out int send);
            for (int species_idx = sbegin; species_idx != send; ++species_idx)
            {
                // start from a random species (and cycle through the available species)
                ResourceUnitSpecies rus = ru.ruSpecies()[species_idx];
                rus.establishment().clear();

                // check if there are seeds of the given species on the resource unit
                float seeds = 0.0F;
                Grid<float>  seedmap = rus.species().seedDispersal().seedMap();
                for (int iy = 0; iy < 5; ++iy)
                {
                    int p = seedmap.index(iseedmap.X, iseedmap.Y);
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
                rus.establishment().calculateAbioticEnvironment();
                double abiotic_env = rus.establishment().abioticEnvironment();
                if (abiotic_env == 0.0)
                {
                    rus.establishment().writeDebugOutputs();
                    continue;
                }

                // loop over all 2m cells on this resource unit
                SaplingCell[] sap_cells = ru.saplingCellArray();
                for (int iy = 0; iy < Constant.cPxPerRU; ++iy)
                {
                    int isc = lif_grid.index(imap.X, imap.Y + iy); // index on 2m cell
                    for (int ix = 0; ix < Constant.cPxPerRU; ++ix, ++isc)
                    {
                        SaplingCell s = sap_cells[iy * Constant.cPxPerRU + ix]; // pointer to a row
                        if (s.state == SaplingCell.ECellState.CellFree)
                        {
                            // is a sapling of the current species already on the pixel?
                            // * test for sapling height already in cell state
                            // * test for grass-cover already in cell state
                            SaplingTree stree = null;
                            SaplingTree[] slots = s.saplings;
                            for (int i = 0; i < SaplingCell.NSAPCELLS; ++i)
                            {
                                if (stree == null && !slots[i].is_occupied())
                                {
                                    stree = slots[i];
                                }
                                if (slots[i].species_index == species_idx)
                                {
                                    stree = null;
                                    break;
                                }
                            }

                            if (stree != null)
                            {
                                // grass cover?
                                float seed_map_value = seedmap[lif_grid.index10(isc)];
                                if (seed_map_value == 0.0F)
                                {
                                    continue;
                                }
                                float lif_value = lif_grid[isc];

                                double lif_corrected = lif_corr[iy * Constant.cPxPerRU + ix];
                                // calculate the LIFcorrected only once per pixel; the relative height is 0 (light level on the forest floor)
                                if (lif_corrected < 0.0)
                                {
                                    lif_corrected = rus.species().speciesSet().LRIcorrection(lif_value, 0.0);
                                }

                                // check for the combination of seed availability and light on the forest floor
                                if (RandomGenerator.drandom() < seed_map_value * lif_corrected * abiotic_env)
                                {
                                    // ok, lets add a sapling at the given position (age is incremented later)
                                    stree.setSapling(0.05f, 0, species_idx);
                                    s.checkState();
                                    rus.saplingStat().mAdded++;
                                }
                            }

                        }
                    }
                }
                // create debug output related to establishment
                rus.establishment().writeDebugOutputs();
            }

        }

        public void saplingGrowth(ResourceUnit ru)
        {
            Grid<HeightGridValue> height_grid = GlobalSettings.instance().model().heightGrid();
            Grid<float> lif_grid = GlobalSettings.instance().model().grid();

            Point imap = ru.cornerPointOffset();
            bool need_check = false;
            SaplingCell[] sap_cells = ru.saplingCellArray();

            for (int iy = 0; iy < Constant.cPxPerRU; ++iy)
            {
                int isc = lif_grid.index(imap.X, imap.Y + iy);
                for (int ix = 0; ix < Constant.cPxPerRU; ++ix, ++isc)
                {
                    SaplingCell s = sap_cells[iy * Constant.cPxPerRU + ix]; // ptr to row
                    if (s.state != SaplingCell.ECellState.CellInvalid)
                    {
                        need_check = false;
                        int n_on_px = s.n_occupied();
                        for (int i = 0; i < SaplingCell.NSAPCELLS; ++i)
                        {
                            if (s.saplings[i].is_occupied())
                            {
                                // growth of this sapling tree
                                HeightGridValue hgv = height_grid[height_grid.index5(isc)];
                                float lif_value = lif_grid[isc];

                                need_check |= growSapling(ru, s, s.saplings[i], isc, hgv.height, lif_value, n_on_px);
                            }
                        }
                        if (need_check)
                        {
                            s.checkState();
                        }
                    }
                }
            }

            // store statistics on saplings/regeneration
            for (int i = 0; i < ru.ruSpecies().Count; ++i)
            {
                ResourceUnitSpecies species = ru.ruSpecies()[i];
                species.saplingStat().calculate(species.species(), ru);
                species.statistics().add(species.saplingStat());
            }

            // debug output related to saplings
            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dSaplingGrowth))
            {
                // establishment details
                for (int it = 0; it != ru.ruSpecies().Count; ++it)
                {
                    ResourceUnitSpecies species = ru.ruSpecies()[it];
                    if (species.saplingStat().livingCohorts() == 0)
                    {
                        continue;
                    }

                    List<object> output = GlobalSettings.instance().debugList(ru.index(), DebugOutputs.dSaplingGrowth);
                    output.AddRange(new object[] { species.species().id(), ru.index(), ru.id(),
                                                   species.saplingStat().livingCohorts(), species.saplingStat().averageHeight(), species.saplingStat().averageAge(),
                                                   species.saplingStat().averageDeltaHPot(), species.saplingStat().averageDeltaHRealized(),
                                                   species.saplingStat().newSaplings(), species.saplingStat().diedSaplings(),
                                                   species.saplingStat().recruitedSaplings(), species.species().saplingGrowthParameters().referenceRatio });
                }
            }

        }

        /// return the SaplingCell (i.e. container for the ind. saplings) for the given 2x2m coordinates
        /// if 'only_valid' is true, then 0 is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell cell(Point lif_coords, bool only_valid, ref ResourceUnit rRUPtr)
        {
            Grid<float> lif_grid = GlobalSettings.instance().model().grid();

            // in this case, getting the actual cell is quite cumbersome: first, retrieve the resource unit, then the
            // cell based on the offset of the given coordiantes relative to the corner of the resource unit.
            ResourceUnit ru = GlobalSettings.instance().model().ru(lif_grid.cellCenterPoint(lif_coords));
            if (rRUPtr != null)
            {
                rRUPtr = ru;
            }

            if (ru != null)
            {
                Point local_coords = lif_coords.Subtract(ru.cornerPointOffset());
                int idx = local_coords.Y * Constant.cPxPerRU + local_coords.X;
                Debug.WriteLineIf(idx < 0 || idx >= Constant.cPxPerHectare, "invalid coords in cell");
                SaplingCell s = ru.saplingCellArray()[idx];
                if (s != null && (!only_valid || s.state != SaplingCell.ECellState.CellInvalid))
                {
                    return s;
                }
            }
            return null;
        }

        public void clearSaplings(RectangleF rectangle, bool remove_biomass)
        {
            GridRunner<float> runner = new GridRunner<float>(GlobalSettings.instance().model().grid(), rectangle);
            for (runner.next(); runner.isValid(); runner.next())
            {
                ResourceUnit ru = null;
                SaplingCell s = cell(runner.currentIndex(), true, ref ru);
                if (s != null)
                {
                    clearSaplings(s, ru, remove_biomass);
                }
            }
        }

        public void clearSaplings(SaplingCell s, ResourceUnit ru, bool remove_biomass)
        {
            if (s != null)
            {
                for (int i = 0; i < SaplingCell.NSAPCELLS; ++i)
                {
                    if (s.saplings[i].is_occupied())
                    {
                        if (!remove_biomass)
                        {
                            ResourceUnitSpecies rus = s.saplings[i].resourceUnitSpecies(ru);
                            if (rus == null && rus.species() != null)
                            {
                                Debug.WriteLine("clearSaplings(): invalid resource unit!!!");
                                return;
                            }
                            rus.saplingStat().addCarbonOfDeadSapling(s.saplings[i].height / rus.species().saplingGrowthParameters().hdSapling * 100.0F);
                        }
                        s.saplings[i].clear();
                    }
                }
                s.checkState();
            }
        }

        public int addSprout(Tree t)
        {
            if (t.species().saplingGrowthParameters().sproutGrowth == 0.0)
            {
                return 0;
            }
            ResourceUnit ru = null;
            SaplingCell sc = cell(t.positionIndex(), true, ref ru);
            if (sc == null)
            {
                return 0;
            }
            clearSaplings(sc, t.ru(), false);
            SaplingTree st = sc.addSapling(0.05f, 0, t.species().index());
            if (st != null)
            {
                st.set_sprout(true);
            }

            // neighboring cells
            double crown_area = t.crownRadius() * t.crownRadius() * Math.PI; //m2
            // calculate how many cells on the ground are covered by the crown (this is a rather rough estimate)
            // n_cells: in addition to the original cell
            int n_cells = (int)Math.Round(crown_area / (double)(Constant.cPxSize * Constant.cPxSize) - 1.0);
            if (n_cells > 0)
            {
                int[] offsets_x = new int[] { 1, 1, 0, -1, -1, -1, 0, 1 };
                int[] offsets_y = new int[] { 0, 1, 1, 1, 0, -1, -1, -1 };
                int s = RandomGenerator.irandom(0, 8);
                ru = null;
                while (n_cells > 0)
                {
                    sc = cell(t.positionIndex().Add(new Point(offsets_x[s], offsets_y[s])), true, ref ru);
                    if (sc != null)
                    {
                        clearSaplings(sc, ru, false);
                        st = sc.addSapling(0.05F, 0, t.species().index());
                        if (st != null)
                        {
                            st.set_sprout(true);
                        }
                    }

                    s = (s + 1) % 8; --n_cells;
                }
            }
            return 1;
        }

        public static void updateBrowsingPressure()
        {
            if (GlobalSettings.instance().settings().valueBool("model.settings.browsing.enabled"))
            {
                mBrowsingPressure = GlobalSettings.instance().settings().valueDouble("model.settings.browsing.browsingPressure");
            }
            else
            {
                mBrowsingPressure = 0.0;
            }
        }

        private bool growSapling(ResourceUnit ru, SaplingCell scell, SaplingTree tree, int isc, float dom_height, float lif_value, int cohorts_on_px)
        {
            ResourceUnitSpecies rus = tree.resourceUnitSpecies(ru);
            Species species = rus.species();

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            double h_pot = species.saplingGrowthParameters().heightGrowthPotential.calculate(tree.height);
            double delta_h_pot = h_pot - tree.height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            if (dom_height == 0.0F)
            {
                throw new NotSupportedException(String.Format("growSapling: height grid at {0} has value 0", isc));
            }

            double rel_height = tree.height / dom_height;

            double lif_corrected = species.speciesSet().LRIcorrection(lif_value, rel_height); // correction based on height

            double lr = species.lightResponse(lif_corrected); // species specific light response (LUI, light utilization index)

            rus.calculate(true); // calculate the 3pg module (this is done only once per RU); true: call comes from regeneration
            double f_env_yr = rus.prod3PG().fEnvYear();

            double delta_h_factor = f_env_yr * lr; // relative growth

            if (h_pot < 0.0 || delta_h_pot < 0.0 || lif_corrected < 0.0 || lif_corrected > 1.0 || delta_h_factor < 0.0 || delta_h_factor > 1.0)
            {
                Debug.WriteLine("invalid values in Sapling::growSapling");
            }

            // sprouts grow faster. Sprouts therefore are less prone to stress (threshold), and can grow higher than the growth potential.
            if (tree.is_sprout())
            {
                delta_h_factor = delta_h_factor * species.saplingGrowthParameters().sproutGrowth;
            }

            // check browsing
            if (mBrowsingPressure > 0.0 && tree.height <= 2.0F)
            {
                double p = rus.species().saplingGrowthParameters().browsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) . odds_mod = odds * browsingPressure . p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = p * mBrowsingPressure / (1.0 - p + p * mBrowsingPressure);
                if (RandomGenerator.drandom() < p_browse)
                {
                    delta_h_factor = 0.0;
                }
            }

            // check mortality of saplings
            if (delta_h_factor < species.saplingGrowthParameters().stressThreshold)
            {
                tree.stress_years++;
                if (tree.stress_years > species.saplingGrowthParameters().maxStressYears)
                {
                    // sapling dies...
                    rus.saplingStat().addCarbonOfDeadSapling(tree.height / species.saplingGrowthParameters().hdSapling * 100.0F);
                    tree.clear();
                    return true; // need cleanup
                }
            }
            else
            {
                tree.stress_years = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * delta_h_factor < 0.0F || (!tree.is_sprout() && delta_h_pot * delta_h_factor > 2.0), "Sapling::growSapling", "implausible height growth.");

            // grow
            tree.height += (float)(delta_h_pot * delta_h_factor);
            tree.age++; // increase age of sapling by 1

            // recruitment?
            if (tree.height > 4.0F)
            {
                rus.saplingStat().mRecruited++;

                float dbh = tree.height / species.saplingGrowthParameters().hdSapling * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.saplingGrowthParameters().representedStemNumber(dbh);
                int to_establish = (int)(n_trees);

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 . add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (RandomGenerator.drandom() < (n_trees - to_establish) || to_establish == 0)
                {
                    to_establish++;
                }

                // add a new tree
                for (int i = 0; i < to_establish; i++)
                {
                    Tree bigtree = ru.newTree();
                    bigtree.setPosition(GlobalSettings.instance().model().grid().indexOf(isc));
                    // add variation: add +/-N% to dbh and *independently* to height.
                    bigtree.setDbh((float)(dbh * RandomGenerator.nrandom(1.0 - mRecruitmentVariation, 1.0 + mRecruitmentVariation)));
                    bigtree.setHeight((float)(tree.height * RandomGenerator.nrandom(1.0 - mRecruitmentVariation, 1.0 + mRecruitmentVariation)));
                    bigtree.setSpecies(species);
                    bigtree.setAge(tree.age, tree.height);
                    bigtree.setRU(ru);
                    bigtree.setup();
                    rus.statistics().add(bigtree, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                tree.clear(); // clear this tree (no carbon flow to the ground)
                for (int i = 0; i < SaplingCell.NSAPCELLS; ++i)
                {
                    if (scell.saplings[i].is_occupied())
                    {
                        // add carbon to the ground
                        ResourceUnitSpecies srus = scell.saplings[i].resourceUnitSpecies(ru);
                        srus.saplingStat().addCarbonOfDeadSapling(scell.saplings[i].height / srus.species().saplingGrowthParameters().hdSapling * 100.0F);
                        scell.saplings[i].clear();
                    }
                }
                return true; // need cleanup
            }
            // book keeping (only for survivors) for the sapling of the resource unit / species
            SaplingStat ss = rus.saplingStat();
            double n_repr = species.saplingGrowthParameters().representedStemNumberH(tree.height) / (double)(cohorts_on_px);
            if (tree.height > 1.3F)
            {
                ss.mLivingSaplings += n_repr;
            }
            else
            {
                ss.mLivingSmallSaplings += n_repr;
            }
            ss.mLiving++;
            ss.mAvgHeight += tree.height;
            ss.mAvgAge += tree.age;
            ss.mAvgDeltaHPot += delta_h_pot;
            ss.mAvgHRealized += delta_h_pot * delta_h_factor;
            return false;
        }

    }
}
