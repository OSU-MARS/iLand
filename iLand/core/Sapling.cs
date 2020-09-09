using iLand.tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
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
        private static double mRecruitmentVariation = 0.1; // +/- 10%
        private static double mBrowsingPressure = 0.0;

        public static void setRecruitmentVariation(double variation) { mRecruitmentVariation = variation; }

        private ResourceUnitSpecies mRUS;
        private List<SaplingTreeOld> mSaplingTrees;
        private BitArray mSapBitset;
        private int mAdded; ///< number of trees added
        private int mRecruited; ///< number recruited (i.e. grown out of regeneration layer)
        private int mDied; ///< number of trees died
        private double mSumDbhDied; ///< running sum of dbh of died trees (used to calculate detritus)
        private int mLiving; ///< number of trees (cohorts!!!) currently in the regeneration layer
        private double mAvgHeight; ///< average height of saplings (m)
        private double mAvgAge; ///< average age of saplings (years)
        private double mAvgDeltaHPot; ///< average height increment potential (m)
        private double mAvgHRealized; ///< average realized height increment
        private CNPair mCarbonLiving;
        private CNPair mCarbonGain; ///< net growth (kg / ru) of saplings

        // access to statistics
        public int newSaplings() { return mAdded; }
        public int diedSaplings() { return mDied; }
        public int livingSaplings() { return mLiving; } ///< get the number
        public int recruitedSaplings() { return mRecruited; }
        public double averageHeight() { return mAvgHeight; }
        public double averageAge() { return mAvgAge; }
        public double averageDeltaHPot() { return mAvgDeltaHPot; }
        public double averageDeltaHRealized() { return mAvgHRealized; }

        // carbon and nitrogen
        public CNPair carbonLiving() { return mCarbonLiving; } ///< state of the living
        public CNPair carbonGain() { return mCarbonGain; } ///< state of the living
        // output maps
        public BitArray presentPositions() { return mSapBitset; }

        public void setup(ResourceUnitSpecies masterRUS) { mRUS = masterRUS; }
        public void newYear() { clearStatistics(); }

        public Sapling()
        {
            mSapBitset = new BitArray(Constant.cPxPerRU * Constant.cPxPerRU);
            mRUS = null;
            clearStatistics();
            mAdded = 0;
        }

        public void clear()
        {
            mSaplingTrees.Clear();
            mSapBitset.SetAll(false);
        }

        // reset statistics, called at newYear
        public void clearStatistics()
        {
            // mAdded: removed
            mRecruited = mDied = mLiving = 0;
            mSumDbhDied = 0.0;
            mAvgHeight = 0.0;
            mAvgAge = 0.0;
            mAvgDeltaHPot = mAvgHRealized = 0.0;
        }

        public void updateBrowsingPressure()
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

        /// get the *represented* (Reineke's Law) number of trees (N/ha)
        public double livingStemNumber(double rAvgDbh, double rAvgHeight, double rAvgAge)
        {
            double total = 0.0;
            double dbh_sum = 0.0;
            double h_sum = 0.0;
            double age_sum = 0.0;
            SaplingGrowthParameters p = mRUS.species().saplingGrowthParameters();
            for (int it = 0; it != mSaplingTrees.Count; ++it)
            {
                float dbh = mSaplingTrees[it].height / p.hdSapling * 100.0F;
                if (dbh < 1.0) // minimum size: 1cm
                {
                    continue;
                }
                double n = p.representedStemNumber(dbh); // one cohort on the pixel represents that number of trees
                dbh_sum += n * dbh;
                h_sum += n * mSaplingTrees[it].height;
                age_sum += n * mSaplingTrees[it].age.age;
                total += n;
            }
            if (total > 0.0)
            {
                dbh_sum /= total;
                h_sum /= total;
                age_sum /= total;
            }
            rAvgDbh = dbh_sum;
            rAvgHeight = h_sum;
            rAvgAge = age_sum;
            return total;
        }

        public double representedStemNumber(float height)
        {
            SaplingGrowthParameters  p = mRUS.species().saplingGrowthParameters();
            float dbh = height / p.hdSapling * 100.0F;
            double n = p.representedStemNumber(dbh);
            return n;
        }

        /// maintenance function to clear dead/recruited saplings from storage
        public void cleanupStorage()
        {
            // seek last valid
            int back;
            for (back = mSaplingTrees.Count - 1; back >= 0; --back)
            {
                if (mSaplingTrees[back].isValid())
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
                if (!mSaplingTrees[forw].isValid())
                {
                    mSaplingTrees[forw] = mSaplingTrees[back]; // copy (fill gap)
                    while (back > forw) // seek next valid
                    {
                        if (mSaplingTrees[--back].isValid())
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
        public bool hasSapling(Point position)
        {
            Point  offset = mRUS.ru().cornerPointOffset();
            int index = (position.X - offset.X) * Constant.cPxPerRU + (position.Y - offset.Y);
            if (index < 0)
            {
                Debug.WriteLine("Sapling error");
            }
            return mSapBitset[index];
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
        public double heightAt(Point position)
        {
            if (!hasSapling(position))
            {
                return 0.0;
            }
            // ok, we'll have to search through all saplings
            int lif_ptr = GlobalSettings.instance().model().grid().index(position);
            for (int it = 0; it != mSaplingTrees.Count; ++it)
            {
                if (mSaplingTrees[it].pixel == lif_ptr)
                {
                    return mSaplingTrees[it].height;
                }
            }
            return 0.0;
        }

        private void setBit(Point pos_index, bool value)
        {
            int index = (pos_index.X - mRUS.ru().cornerPointOffset().X) * Constant.cPxPerRU + (pos_index.Y - mRUS.ru().cornerPointOffset().Y);
            mSapBitset[index] = value; // set bit: now there is a sapling there
        }

        /// add a sapling at the given position (index on the LIF grid, i.e. 2x2m)
        public int addSapling(Point pos_lif, float height = 0.5F, int age = 1)
        {
            // adds a sapling...
            SaplingTreeOld t = new SaplingTreeOld();
            mSaplingTrees.Add(t);
            t.height = height; // default is 5cm height
            t.age.age = (UInt16)age;
            Grid<float>  lif_map = GlobalSettings.instance().model().grid();
            t.pixel = lif_map.index(pos_lif);
            setBit(pos_lif, true);
            mAdded++;
            return mSaplingTrees.Count - 1; // index of the newly added tree.
        }

        /// clear  saplings on a given position (after recruitment)
        public void clearSaplings(Point position)
        {
            float target = GlobalSettings.instance().model().grid().ptr(position.X, position.Y);
            for (int it = 0; it < mSaplingTrees.Count; ++it)
            {
                if (mSaplingTrees[it].pixel == target)
                {
                    // trick: use a iterator to avoid a deep copy of the vector; then do an ugly const_cast to actually write the data
                    //SaplingTree &t = *it;
                    //const_cast<SaplingTree&>(t).pixel=0;
                    clearSapling(it, false); // kill sapling and move carbon to soil
                }
            }
            setBit(position, false); // clear bit: now there is no sapling on this position
                                     //int index = (position.x() - mRUS.ru().cornerPointOffset().x()) * cPxPerRU +(position.y() - mRUS.ru().cornerPointOffset().y());
                                     //mSapBitset.set(index,false); // clear bit: now there is no sapling on this position
        }

        /// clear saplings within a given rectangle
        public void clearSaplings(RectangleF rectangle, bool remove_biomass)
        {
            Grid<float> grid = GlobalSettings.instance().model().grid();
            for (int it = 0; it < mSaplingTrees.Count; ++it)
            {
                if (rectangle.Contains(grid.cellCenterPoint(mSaplingTrees[it].coords())))
                {
                    clearSapling(it, remove_biomass);
                }
            }
        }

        public void clearSapling(SaplingTreeOld tree, bool remove)
        {
            Point p = tree.coords();
            tree.pixel = 0;
            setBit(p, false); // no tree left
            if (!remove)
            {
                // killing of saplings:
                // if remove=false, then remember dbh/number of trees (used later in calculateGrowth() to estimate carbon flow)
                mDied++;
                mSumDbhDied += tree.height / mRUS.species().saplingGrowthParameters().hdSapling * 100.0;
            }
        }

        public void clearSapling(int index, bool remove)
        {
            Debug.Assert(index < mSaplingTrees.Count);
            clearSapling(mSaplingTrees[index], remove);
        }

        /// growth function for an indivudal sapling.
        /// returns true, if sapling survives, false if sapling dies or is recruited to iLand.
        /// see also http://iland.boku.ac.at/recruitment
        private bool growSapling(SaplingTreeOld tree, double f_env_yr, Species species)
        {
            Point p = GlobalSettings.instance().model().grid().indexOf(tree.pixel);
            //GlobalSettings.instance().model().heightGrid()[Grid::index5(tree.pixel-GlobalSettings.instance().model().grid().begin())];

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            double h_pot = species.saplingGrowthParameters().heightGrowthPotential.calculate(tree.height); // TODO check if this can be source of crashes (race condition)
            double delta_h_pot = h_pot - tree.height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            double lif_value = tree.pixel;
            double h_height_grid = GlobalSettings.instance().model().heightGrid().valueAtIndex(p.X / Constant.cPxPerHeight, p.Y / Constant.cPxPerHeight).height;
            if (h_height_grid == 0.0)
            {
                throw new NotSupportedException(String.Format("growSapling: height grid at {0},{1} has value 0", p.X, p.Y));
            }

            double rel_height = tree.height / h_height_grid;

            double lif_corrected = mRUS.species().speciesSet().LRIcorrection(lif_value, rel_height); // correction based on height

            double lr = mRUS.species().lightResponse(lif_corrected); // species specific light response (LUI, light utilization index)

            double delta_h_factor = f_env_yr * lr; // relative growth

            if (h_pot < 0.0 || delta_h_pot < 0.0 || lif_corrected < 0.0 || lif_corrected > 1.0 || delta_h_factor < 0.0 || delta_h_factor > 1.0)
            {
                Debug.WriteLine("invalid values in growSapling");
            }

            // check browsing
            if (mBrowsingPressure > 0.0 && tree.height <= 2.0F)
            {
                double pb = mRUS.species().saplingGrowthParameters().browsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) -> odds_mod = odds * browsingPressure -> p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = pb * mBrowsingPressure / (1.0 - pb + pb * mBrowsingPressure);
                if (RandomGenerator.drandom() < p_browse)
                {
                    delta_h_factor = 0.0;
                }
            }

            // check mortality of saplings
            if (delta_h_factor < species.saplingGrowthParameters().stressThreshold)
            {
                tree.age.stress_years++;
                if (tree.age.stress_years > species.saplingGrowthParameters().maxStressYears)
                {
                    // sapling dies...
                    clearSapling(tree, false); // false: put carbon to the soil
                    return false;
                }
            }
            else
            {
                tree.age.stress_years = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * delta_h_factor < 0.0F || delta_h_pot * delta_h_factor > 2.0, "growSapling", "implausible height growth.");

            // grow
            tree.height += (float)(delta_h_pot * delta_h_factor);
            tree.age.age++; // increase age of sapling by 1

            // recruitment?
            if (tree.height > 4.0F)
            {
                mRecruited++;

                ResourceUnit ru = mRUS.ru();
                float dbh = tree.height / species.saplingGrowthParameters().hdSapling * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.saplingGrowthParameters().representedStemNumber(dbh);
                int to_establish = (int)n_trees;

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 -> add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (RandomGenerator.drandom() < (n_trees - to_establish) || to_establish == 0)
                {
                    to_establish++;
                }

                // add a new tree
                for (int i = 0; i < to_establish; i++)
                {
                    Tree bigtree = ru.newTree();
                    bigtree.setPosition(p);
                    // add variation: add +/-10% to dbh and *independently* to height.
                    bigtree.setDbh(dbh * (float)RandomGenerator.nrandom(1.0 - mRecruitmentVariation, 1.0 + mRecruitmentVariation));
                    bigtree.setHeight(tree.height * (float)RandomGenerator.nrandom(1.0 - mRecruitmentVariation, 1.0 + mRecruitmentVariation));
                    bigtree.setSpecies(species);
                    bigtree.setAge(tree.age.age, tree.height);
                    bigtree.setRU(ru);
                    bigtree.setup();
                    mRUS.statistics().add(bigtree, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                clearSapling(tree, true); // remove this tree (but do not move biomass to soil)
                                          //        ru->clearSaplings(p); // remove all other saplings on the same pixel

                return false;
            }
            // book keeping (only for survivors)
            mLiving++;
            mAvgHeight += tree.height;
            mAvgAge += tree.age.age;
            mAvgDeltaHPot += delta_h_pot;
            mAvgHRealized += delta_h_pot * delta_h_factor;
            return true;
        }

        /** main growth function for saplings.
            Statistics are cleared at the beginning of the year.
            */
        public void calculateGrowth()
        {
            Debug.Assert(mRUS != null);
            if (mSaplingTrees.Count == 0)
            {
                return;
            }

            ResourceUnit ru = mRUS.ru();
            Species species = mRUS.species();

            // calculate necessary growth modifier (this is done only once per year)
            mRUS.calculate(true); // calculate the 3pg module (this is done only if that did not happen up to now); true: call comes from regeneration
            double f_env_yr = mRUS.prod3PG().fEnvYear();

            mLiving = 0;
            for (int it = 0; it < mSaplingTrees.Count; ++it)
            {
                SaplingTreeOld  tree = mSaplingTrees[it];
                if (tree.height < 0)
                {
                    Debug.WriteLine("calculateGrowth(): h<0");
                }
                // if sapling is still living check execute growth routine
                if (tree.isValid())
                {
                    // growing (increases mLiving if tree did not die, mDied otherwise)
                    if (growSapling(tree, f_env_yr, species))
                    {
                        // set the sapling height to the maximum value on the current pixel
                        //                ru.setMaxSaplingHeightAt(tree.coords(),tree.height);
                    }
                }
            }
            if (mLiving != 0)
            {
                mAvgHeight /= (double)mLiving;
                mAvgAge /= (double)mLiving;
                mAvgDeltaHPot /= (double)mLiving;
                mAvgHRealized /= (double)mLiving;
            }
            // calculate carbon balance
            CNPair old_state = mCarbonLiving;
            mCarbonLiving.clear();

            CNPair dead_wood = new CNPair();
            CNPair dead_fine = new CNPair(); // pools for mortality
            // average dbh
            if (mLiving != 0)
            {
                // calculate the avg dbh and number of stems
                double avg_dbh = mAvgHeight / species.saplingGrowthParameters().hdSapling * 100.0;
                double n = mLiving * species.saplingGrowthParameters().representedStemNumber(avg_dbh);
                // woody parts: stem, branchse and coarse roots
                double woody_bm = species.biomassWoody(avg_dbh) + species.biomassBranch(avg_dbh) + species.biomassRoot(avg_dbh);
                double foliage = species.biomassFoliage(avg_dbh);
                double fineroot = foliage * species.finerootFoliageRatio();

                mCarbonLiving.addBiomass(woody_bm * n, species.cnWood());
                mCarbonLiving.addBiomass(foliage * n, species.cnFoliage());
                mCarbonLiving.addBiomass(fineroot * n, species.cnFineroot());

                // turnover
                if (mRUS.ru().snag() != null)
                {
                    mRUS.ru().snag().addTurnoverLitter(species, foliage * species.turnoverLeaf(), fineroot * species.turnoverRoot());
                }

                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (avg_dbh > 1.0)
                {
                    double avg_dbh_before = (mAvgHeight - mAvgHRealized) / species.saplingGrowthParameters().hdSapling * 100.0;
                    double n_before = mLiving * species.saplingGrowthParameters().representedStemNumber(Math.Max(1.0, avg_dbh_before));
                    if (n < n_before)
                    {
                        dead_wood.addBiomass(woody_bm * (n_before - n), species.cnWood());
                        dead_fine.addBiomass(foliage * (n_before - n), species.cnFoliage());
                        dead_fine.addBiomass(fineroot * (n_before - n), species.cnFineroot());
                    }
                }

            }
            if (mDied != 0)
            {
                double avg_dbh_dead = mSumDbhDied / (double)mDied;
                double n = mDied * species.saplingGrowthParameters().representedStemNumber(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                dead_wood.addBiomass((species.biomassWoody(avg_dbh_dead) + species.biomassBranch(avg_dbh_dead) + species.biomassRoot(avg_dbh_dead)) * n, species.cnWood());
                double foliage = species.biomassFoliage(avg_dbh_dead) * n;

                dead_fine.addBiomass(foliage, species.cnFoliage());
                dead_fine.addBiomass(foliage * species.finerootFoliageRatio(), species.cnFineroot());
            }
            if (!dead_wood.isEmpty() || !dead_fine.isEmpty())
            {
                if (mRUS.ru().snag() != null)
                {
                    mRUS.ru().snag().addToSoil(species, dead_wood, dead_fine);
                }
            }

            // calculate net growth:
            // delta of stocks
            mCarbonGain = mCarbonLiving + dead_fine + dead_wood - old_state;
            if (mCarbonGain.C < 0)
            {
                mCarbonGain.clear();
            }
            if (mSaplingTrees.Count > mLiving * 1.3)
            {
                cleanupStorage();
            }

            //    mRUS.statistics().add(this);
            GlobalSettings.instance().systemStatistics().saplingCount += mLiving;
            GlobalSettings.instance().systemStatistics().newSaplings += mAdded;
            mAdded = 0; // reset

            //Debug.WriteLine(ru.index() << species.id()<< ": (living/avg.height):" <<  mLiving << mAvgHeight;
        }

        /// fill a grid with the maximum height of saplings per pixel (2x2m).
        /// this function is used for visualization only
        public void fillMaxHeightGrid(Grid<float> grid)
        {
            for (int it = 0; it != mSaplingTrees.Count; ++it)
            {
                if (mSaplingTrees[it].isValid())
                {
                    Point p = mSaplingTrees[it].coords();
                    if (grid.valueAtIndex(p) < mSaplingTrees[it].height)
                    {
                        grid[p] = mSaplingTrees[it].height;
                    }
                }
            }
        }
    }
}
