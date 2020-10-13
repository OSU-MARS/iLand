using iLand.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Core
{
    /** @class Sapling
        @ingroup core
        Sapling stores saplings per species and resource unit and computes sapling growth (before recruitment).
        http://iland.boku.ac.at/sapling+growth+and+competition
        Saplings are established in a separate step (@sa Regeneration). If sapling reach a height of 4m, they are recruited and become "real" iLand-trees.
        Within the regeneration layer, a cohort-approach is applied.
        */
    // BUGBUG: copy/paste code shared with other sapling classes
    internal class Sapling
    {
        private ResourceUnitSpecies mRUS;
        private readonly List<SaplingTreeOld> mSaplingTrees;
        private double mSumDbhDied; ///< running sum of dbh of died trees (used to calculate detritus)

        public double AverageAge { get; private set; } ///< average age of saplings (years)
        public double AverageDeltaHPot { get; private set; } ///< average height increment potential (m)
        public double AverageDeltaHRealized { get; private set; } ///< average realized height increment
        public double AverageHeight { get; private set; } ///< average height of saplings (m)
        public CNPair CarbonGain { get; private set; }  ///< net growth (kg / ru) of saplings
        public CNPair CarbonLiving { get; private set; } ///< state of the living
        public int DeadSaplings { get; private set; } ///< number of trees died
        public int LivingSaplings { get; private set; } ///< number of trees (cohorts!!!) currently in the regeneration layer
        public int NewSaplings { get; private set; } ///< number of trees added
        public BitArray PresentPositions { get; private set; }
        public int RecruitedSaplings { get; private set; } ///< number recruited (i.e. grown out of regeneration layer)

        public void NewYear() { ClearStatistics(); }
        public void SetRU(ResourceUnitSpecies masterRUS) { mRUS = masterRUS; }

        public Sapling()
        {
            this.NewSaplings = 0;
            this.CarbonLiving = new CNPair();
            this.mRUS = null;
            this.PresentPositions = new BitArray(Constant.LightPerRUsize * Constant.LightPerRUsize);
            this.mSaplingTrees = new List<SaplingTreeOld>();

            ClearStatistics();
        }

        public void Clear()
        {
            mSaplingTrees.Clear();
            PresentPositions.SetAll(false);
        }

        // reset statistics, called at newYear
        public void ClearStatistics()
        {
            // mAdded: removed
            RecruitedSaplings = DeadSaplings = LivingSaplings = 0;
            mSumDbhDied = 0.0;
            AverageHeight = 0.0;
            AverageAge = 0.0;
            AverageDeltaHPot = AverageDeltaHRealized = 0.0;
        }

        public double RepresentedStemNumber(float height)
        {
            SaplingGrowthParameters  p = mRUS.Species.SaplingGrowthParameters;
            float dbh = height / p.HdSapling * 100.0F;
            double n = p.RepresentedStemNumberFromDiameter(dbh);
            return n;
        }

        /// maintenance function to clear dead/recruited saplings from storage
        public void CleanupStorage()
        {
            // seek last valid
            int back;
            for (back = mSaplingTrees.Count - 1; back >= 0; --back)
            {
                if (mSaplingTrees[back].IsValid())
                {
                    break;
                }
            }
            if (back < 0)
            {
                mSaplingTrees.Clear(); // no valid trees available
                return;
            }

            int forw = 0;
            while (forw < back)
            {
                if (!mSaplingTrees[forw].IsValid())
                {
                    mSaplingTrees[forw] = mSaplingTrees[back]; // copy (fill gap)
                    while (back > forw) // seek next valid
                    {
                        if (mSaplingTrees[--back].IsValid())
                        {
                            break;
                        }
                    }
                }
                ++forw;
            }
            if (back != mSaplingTrees.Count - 1)
            {
                mSaplingTrees.RemoveRange(back + 1, mSaplingTrees.Count - back - 1);
            }
        }

        // not a very good way of checking if sapling is present
        // maybe better: use also a (local) maximum sapling height grid
        // maybe better: use a bitset:
        // position: index of pixel on LIF (absolute index)
        public bool HasSapling(Point position)
        {
            Point  offset = mRUS.RU.CornerPointOffset;
            int index = (position.X - offset.X) * Constant.LightPerRUsize + (position.Y - offset.Y);
            if (index < 0)
            {
                Debug.WriteLine("Sapling error");
            }
            return PresentPositions[index];
            /*
            float *target = GlobalSettings.instance().model().grid().ptr(position.x(), position.y());
            List<SaplingTree>::const_iterator it;
            for (it = mSaplingTrees.constBegin(); it!=mSaplingTrees.constEnd(); ++it) {
                if (it.pixel==target)
                    return true;
            }
            return false;
            */
        }

        /// retrieve the height of the sapling at the location 'position' (given in LIF-coordinates)
        /// this is quite expensive and only done for initialization
        //public double HeightAt(Grid<float> lightGrid, Point position)
        //{
        //    if (!HasSapling(position))
        //    {
        //        return 0.0;
        //    }
        //    // ok, we'll have to search through all saplings
        //    int lif_ptr = lightGrid.IndexOf(position);
        //    for (int it = 0; it != mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].LightPixel == lif_ptr)
        //        {
        //            return mSaplingTrees[it].Height;
        //        }
        //    }
        //    return 0.0;
        //}

        private void SetBit(Point pos_index, bool value)
        {
            int index = (pos_index.X - mRUS.RU.CornerPointOffset.X) * Constant.LightPerRUsize + (pos_index.Y - mRUS.RU.CornerPointOffset.Y);
            PresentPositions[index] = value; // set bit: now there is a sapling there
        }

        /// add a sapling at the given position (index on the LIF grid, i.e. 2x2m)
        //public int AddSapling(Grid<float> lightGrid, Point pos_lif, float height = 0.5F, int age = 1)
        //{
        //    // adds a sapling...
        //    SaplingTreeOld t = new SaplingTreeOld();
        //    mSaplingTrees.Add(t);
        //    t.Height = height; // default is 5cm height
        //    t.Age.Age = (UInt16)age;
        //    t.LightPixel = lightGrid.IndexOf(pos_lif);
        //    SetBit(pos_lif, true);
        //    NewSaplings++;
        //    return mSaplingTrees.Count - 1; // index of the newly added tree.
        //}

        /// clear saplings on a given position (after recruitment)
        //public void ClearSaplings(Grid<float> lightGrid, Point position)
        //{
        //    float target = lightGrid[position];
        //    for (int it = 0; it < mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].LightPixel == target)
        //        {
        //            // trick: use a iterator to avoid a deep copy of the vector; then do an ugly const_cast to actually write the data
        //            //SaplingTree &t = *it;
        //            //const_cast<SaplingTree&>(t).pixel=0;
        //            ClearSapling(it, false); // kill sapling and move carbon to soil
        //        }
        //    }
        //    SetBit(position, false); // clear bit: now there is no sapling on this position
        //                             //int index = (position.x() - mRUS.ru().cornerPointOffset().x()) * cPxPerRU +(position.y() - mRUS.ru().cornerPointOffset().y());
        //                             //mSapBitset.set(index,false); // clear bit: now there is no sapling on this position
        //}

        /// clear saplings within a given rectangle
        //public void ClearSaplings(Grid<float> lightGrid, RectangleF rectangle, bool remove_biomass)
        //{
        //    for (int it = 0; it < mSaplingTrees.Count; ++it)
        //    {
        //        if (rectangle.Contains(lightGrid.GetCellCenterPoint(mSaplingTrees[it].Coordinate())))
        //        {
        //            ClearSapling(it, remove_biomass);
        //        }
        //    }
        //}

        public void ClearSapling(SaplingTreeOld tree, Grid<float> lightGrid, bool remove)
        {
            Point p = tree.Coordinate(lightGrid);
            tree.LightPixel = 0;
            SetBit(p, false); // no tree left
            if (!remove)
            {
                // killing of saplings:
                // if remove=false, then remember dbh/number of trees (used later in calculateGrowth() to estimate carbon flow)
                DeadSaplings++;
                mSumDbhDied += tree.Height / mRUS.Species.SaplingGrowthParameters.HdSapling * 100.0;
            }
        }

        public void ClearSapling(int index, Grid<float> lightGrid, bool remove)
        {
            Debug.Assert(index < mSaplingTrees.Count);
            ClearSapling(mSaplingTrees[index], lightGrid, remove);
        }

        /// growth function for an indivudal sapling.
        /// returns true, if sapling survives, false if sapling dies or is recruited to iLand.
        /// see also http://iland.boku.ac.at/recruitment
        private bool GrowSapling(SaplingTreeOld tree, Model model, double f_env_yr, Species species)
        {
            Point p = model.LightGrid.IndexOf(tree.LightPixel);
            //GlobalSettings.instance().model().heightGrid()[Grid::index5(tree.pixel-GlobalSettings.instance().model().grid().begin())];

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            double h_pot = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model, tree.Height); // TODO check if this can be source of crashes (race condition)
            double delta_h_pot = h_pot - tree.Height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            double lif_value = tree.LightPixel;
            double h_height_grid = model.HeightGrid[p.X / Constant.LightPerHeightSize, p.Y / Constant.LightPerHeightSize].Height;
            if (h_height_grid == 0.0)
            {
                throw new NotSupportedException(String.Format("growSapling: height grid at {0},{1} has value 0", p.X, p.Y));
            }

            double rel_height = tree.Height / h_height_grid;

            double lif_corrected = mRUS.Species.SpeciesSet.GetLriCorrection(model, lif_value, rel_height); // correction based on height

            double lr = mRUS.Species.GetLightResponse(model, lif_corrected); // species specific light response (LUI, light utilization index)

            double delta_h_factor = f_env_yr * lr; // relative growth

            if (h_pot < 0.0 || delta_h_pot < 0.0 || lif_corrected < 0.0 || lif_corrected > 1.0 || delta_h_factor < 0.0 || delta_h_factor > 1.0)
            {
                Debug.WriteLine("invalid values in growSapling");
            }

            // check browsing
            if (model.ModelSettings.BrowsingPressure > 0.0 && tree.Height <= 2.0F)
            {
                double pb = mRUS.Species.SaplingGrowthParameters.BrowsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) -> odds_mod = odds * browsingPressure -> p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = pb * model.ModelSettings.BrowsingPressure / (1.0 - pb + pb * model.ModelSettings.BrowsingPressure);
                if (model.RandomGenerator.Random() < p_browse)
                {
                    delta_h_factor = 0.0;
                }
            }

            // check mortality of saplings
            if (delta_h_factor < species.SaplingGrowthParameters.StressThreshold)
            {
                tree.Age.StressYears++;
                if (tree.Age.StressYears > species.SaplingGrowthParameters.MaxStressYears)
                {
                    // sapling dies...
                    ClearSapling(tree, model.LightGrid, false); // false: put carbon to the soil
                    return false;
                }
            }
            else
            {
                tree.Age.StressYears = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * delta_h_factor < 0.0F || delta_h_pot * delta_h_factor > 2.0, "growSapling", "implausible height growth.");

            // grow
            tree.Height += (float)(delta_h_pot * delta_h_factor);
            tree.Age.Age++; // increase age of sapling by 1

            // recruitment?
            if (tree.Height > 4.0F)
            {
                RecruitedSaplings++;

                ResourceUnit ru = mRUS.RU;
                float dbh = tree.Height / species.SaplingGrowthParameters.HdSapling * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(dbh);
                int to_establish = (int)n_trees;

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 -> add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (model.RandomGenerator.Random() < (n_trees - to_establish) || to_establish == 0)
                {
                    to_establish++;
                }

                // add a new tree
                for (int i = 0; i < to_establish; i++)
                {
                    Tree bigtree = ru.AddNewTree(model);
                    bigtree.LightCellPosition = p;
                    // add variation: add +/-10% to dbh and *independently* to height.
                    bigtree.Dbh = dbh * (float)model.RandomGenerator.Random(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation);
                    bigtree.SetHeight(tree.Height * (float)model.RandomGenerator.Random(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation));
                    bigtree.Species = species;
                    bigtree.SetAge(tree.Age.Age, tree.Height);
                    bigtree.RU = ru;
                    bigtree.Setup(model);
                    mRUS.Statistics.Add(bigtree, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                ClearSapling(tree, model.LightGrid, true); // remove this tree (but do not move biomass to soil)
                //        ru->clearSaplings(p); // remove all other saplings on the same pixel
                return false;
            }
            // book keeping (only for survivors)
            LivingSaplings++;
            AverageHeight += tree.Height;
            AverageAge += tree.Age.Age;
            AverageDeltaHPot += delta_h_pot;
            AverageDeltaHRealized += delta_h_pot * delta_h_factor;
            return true;
        }

        /** main growth function for saplings.
            Statistics are cleared at the beginning of the year.
            */
        public void CalculateGrowth(Model model)
        {
            Debug.Assert(mRUS != null);
            if (mSaplingTrees.Count == 0)
            {
                return;
            }

            Species species = mRUS.Species;

            // calculate necessary growth modifier (this is done only once per year)
            mRUS.Calculate(model, true); // calculate the 3pg module (this is done only if that did not happen up to now); true: call comes from regeneration
            double f_env_yr = mRUS.BiomassGrowth.EnvironmentalFactor;

            LivingSaplings = 0;
            for (int it = 0; it < mSaplingTrees.Count; ++it)
            {
                SaplingTreeOld tree = mSaplingTrees[it];
                if (tree.Height < 0)
                {
                    Debug.WriteLine("calculateGrowth(): h<0");
                }
                // if sapling is still living check execute growth routine
                if (tree.IsValid())
                {
                    // growing (increases mLiving if tree did not die, mDied otherwise)
                    if (GrowSapling(tree, model, f_env_yr, species))
                    {
                        // set the sapling height to the maximum value on the current pixel
                        //                ru.setMaxSaplingHeightAt(tree.coords(),tree.height);
                    }
                }
            }
            if (LivingSaplings != 0)
            {
                AverageHeight /= (double)LivingSaplings;
                AverageAge /= (double)LivingSaplings;
                AverageDeltaHPot /= (double)LivingSaplings;
                AverageDeltaHRealized /= (double)LivingSaplings;
            }
            // calculate carbon balance
            CNPair old_state = CarbonLiving;
            CarbonLiving.Clear();

            CNPair dead_wood = new CNPair();
            CNPair dead_fine = new CNPair(); // pools for mortality
            // average dbh
            if (LivingSaplings != 0)
            {
                // calculate the avg dbh and number of stems
                double avg_dbh = AverageHeight / species.SaplingGrowthParameters.HdSapling * 100.0;
                double n = LivingSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh);
                // woody parts: stem, branchse and coarse roots
                double woody_bm = species.GetBiomassWoody(avg_dbh) + species.GetBiomassBranch(avg_dbh) + species.GetBiomassRoot(avg_dbh);
                double foliage = species.GetBiomassFoliage(avg_dbh);
                double fineroot = foliage * species.FinerootFoliageRatio;

                CarbonLiving.AddBiomass(woody_bm * n, species.CNRatioWood);
                CarbonLiving.AddBiomass(foliage * n, species.CNRatioFoliage);
                CarbonLiving.AddBiomass(fineroot * n, species.CNRatioFineRoot);

                // turnover
                if (mRUS.RU.Snags != null)
                {
                    mRUS.RU.Snags.AddTurnoverLitter(species, foliage * species.TurnoverLeaf, fineroot * species.TurnoverRoot);
                }

                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (avg_dbh > 1.0)
                {
                    double avg_dbh_before = (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowthParameters.HdSapling * 100.0;
                    double n_before = LivingSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(Math.Max(1.0, avg_dbh_before));
                    if (n < n_before)
                    {
                        dead_wood.AddBiomass(woody_bm * (n_before - n), species.CNRatioWood);
                        dead_fine.AddBiomass(foliage * (n_before - n), species.CNRatioFoliage);
                        dead_fine.AddBiomass(fineroot * (n_before - n), species.CNRatioFineRoot);
                    }
                }

            }
            if (DeadSaplings != 0)
            {
                double avg_dbh_dead = mSumDbhDied / (double)DeadSaplings;
                double n = DeadSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                dead_wood.AddBiomass((species.GetBiomassWoody(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead) + species.GetBiomassRoot(avg_dbh_dead)) * n, species.CNRatioWood);
                double foliage = species.GetBiomassFoliage(avg_dbh_dead) * n;

                dead_fine.AddBiomass(foliage, species.CNRatioFoliage);
                dead_fine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CNRatioFineRoot);
            }
            if (!dead_wood.IsEmpty() || !dead_fine.IsEmpty())
            {
                if (mRUS.RU.Snags != null)
                {
                    mRUS.RU.Snags.AddToSoil(species, dead_wood, dead_fine);
                }
            }

            // calculate net growth:
            // delta of stocks
            CarbonGain = CarbonLiving + dead_fine + dead_wood - old_state;
            if (CarbonGain.C < 0)
            {
                CarbonGain.Clear();
            }
            if (mSaplingTrees.Count > LivingSaplings * 1.3)
            {
                CleanupStorage();
            }

            //    mRUS.statistics().add(this);
            model.GlobalSettings.SystemStatistics.SaplingCount += LivingSaplings;
            model.GlobalSettings.SystemStatistics.NewSaplings += NewSaplings;
            NewSaplings = 0; // reset

            //Debug.WriteLine(ru.index() << species.id()<< ": (living/avg.height):" <<  mLiving << mAvgHeight;
        }

        /// fill a grid with the maximum height of saplings per pixel (2x2m).
        /// this function is used for visualization only
        //public void FillMaxHeightGrid(Grid<float> grid)
        //{
        //    for (int it = 0; it != mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].IsValid())
        //        {
        //            Point p = mSaplingTrees[it].Coordinate(grid);
        //            if (grid[p] < mSaplingTrees[it].Height)
        //            {
        //                grid[p] = mSaplingTrees[it].Height;
        //            }
        //        }
        //    }
        //}
    }
}
