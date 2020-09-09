using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.core
{
    /** The SaplingStat class stores statistics on the resource unit x species level.
      */
    internal class SaplingStat
    {
        public int mAdded; ///< number of tree cohorts added
        public int mRecruited; ///< number of cohorts recruited (i.e. grown out of regeneration layer)
        private int mDied; ///< number of tree cohorts died
        private double mSumDbhDied; ///< running sum of dbh of died trees (used to calculate detritus)
        public int mLiving; ///< number of trees (cohorts!!!) currently in the regeneration layer
        public double mLivingSaplings; ///< number of individual trees in the regen layer (using Reinekes R), with h>1.3m
        public double mLivingSmallSaplings; ///< number of individual trees of cohorts < 1.3m height
        public double mAvgHeight; ///< average height of saplings (m)
        public double mAvgAge; ///< average age of saplings (years)
        public double mAvgDeltaHPot; ///< average height increment potential (m)
        public double mAvgHRealized; ///< average realized height increment
        private CNPair mCarbonLiving; ///< kg Carbon (kg/ru) of saplings
        private CNPair mCarbonGain; ///< net growth (kg / ru) of saplings

        public SaplingStat()
        {
            mCarbonGain = new CNPair();
            mCarbonLiving = new CNPair();
            clearStatistics();
        }

        // actions
        public void addCarbonOfDeadSapling(float dbh) { mDied++; mSumDbhDied += dbh; }

        // access to statistics
        public int newSaplings() { return mAdded; }
        public int diedSaplings() { return mDied; }
        public int livingCohorts() { return mLiving; } ///< get the number of cohorts
        public double livingSaplings() { return mLivingSaplings; }
        public double livingSaplingsSmall() { return mLivingSmallSaplings; }
        public int recruitedSaplings() { return mRecruited; }

        public double averageHeight() { return mAvgHeight; }
        public double averageAge() { return mAvgAge; }
        public double averageDeltaHPot() { return mAvgDeltaHPot; }
        public double averageDeltaHRealized() { return mAvgHRealized; }
        // carbon and nitrogen
        public CNPair carbonLiving() { return mCarbonLiving; } ///< state of the living
        public CNPair carbonGain() { return mCarbonGain; } ///< state of the living

        public void clearStatistics()
        {
            mRecruited = mDied = mLiving = 0;
            mLivingSaplings = 0.0;
            mLivingSmallSaplings = 0.0;
            mSumDbhDied = 0.0;
            mAvgHeight = 0.0;
            mAvgAge = 0.0;
            mAvgDeltaHPot = mAvgHRealized = 0.0;
            mAdded = 0;
        }

        public void calculate(Species species, ResourceUnit ru)
        {
            if (mLiving != 0)
            {
                mAvgHeight /= (double)mLiving;
                mAvgAge /= (double)mLiving;
                mAvgDeltaHPot /= (double)mLiving;
                mAvgHRealized /= (double)mLiving;
            }
            if (GlobalSettings.instance().currentYear() == 0)
            {
                return; // no need for carbon flows in initial run
            }

            // calculate carbon balance
            CNPair old_state = mCarbonLiving;
            mCarbonLiving.clear();

            CNPair dead_wood = new CNPair(); // pools for mortality
            CNPair dead_fine = new CNPair();
            // average dbh
            if (mLiving > 0)
            {
                // calculate the avg dbh and number of stems
                double avg_dbh = mAvgHeight / species.saplingGrowthParameters().hdSapling * 100.0;
                // the number of "real" stems is given by the Reineke formula
                double n = mLivingSaplings; // total number of saplings (>0.05m)

                // woody parts: stem, branchse and coarse roots
                double woody_bm = species.biomassWoody(avg_dbh) + species.biomassBranch(avg_dbh) + species.biomassRoot(avg_dbh);
                double foliage = species.biomassFoliage(avg_dbh);
                double fineroot = foliage * species.finerootFoliageRatio();

                mCarbonLiving.addBiomass(woody_bm * n, species.cnWood());
                mCarbonLiving.addBiomass(foliage * n, species.cnFoliage());
                mCarbonLiving.addBiomass(fineroot * n, species.cnFineroot());

                Debug.WriteLineIf(Double.IsNaN(mCarbonLiving.C), "carbon NaN in calculate (living trees).");

                // turnover
                if (ru.snag() != null)
                {
                    ru.snag().addTurnoverLitter(species, foliage * species.turnoverLeaf(), fineroot * species.turnoverRoot());
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
                        Debug.WriteLineIf(Double.IsNaN(dead_fine.C), "carbon NaN in calculate (self thinning).");
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
                Debug.WriteLineIf(Double.IsNaN(dead_fine.C), "carbon NaN in calculate (died trees).");
            }
            if (!dead_wood.isEmpty() || !dead_fine.isEmpty())
            {
                if (ru.snag() != null)
                {
                    ru.snag().addToSoil(species, dead_wood, dead_fine);
                }
            }

            // calculate net growth:
            // delta of stocks
            mCarbonGain = mCarbonLiving + dead_fine + dead_wood - old_state;
            if (mCarbonGain.C < 0)
            {
                mCarbonGain.clear();
            }

            GlobalSettings.instance().systemStatistics().saplingCount += mLiving;
            GlobalSettings.instance().systemStatistics().newSaplings += mAdded;
        }

        ///  returns the *represented* (Reineke's Law) number of trees (N/ha) and the mean dbh/height (cm/m)
        public double livingStemNumber(Species species, out double rAvgDbh, out double rAvgHeight, out double rAvgAge)
        {
            rAvgHeight = averageHeight();
            rAvgDbh = rAvgHeight / species.saplingGrowthParameters().hdSapling * 100.0F;
            rAvgAge = averageAge();
            double n = species.saplingGrowthParameters().representedStemNumber(rAvgDbh);
            return n;
            // *** old code (sapling.cpp) ***
            //    double total = 0.0;
            //    double dbh_sum = 0.0;
            //    double h_sum = 0.0;
            //    double age_sum = 0.0;
            //    SaplingGrowthParameters &p = mRUS.species().saplingGrowthParameters();
            //    for (QVector<SaplingTreeOld>::const_iterator it = mSaplingTrees.constBegin(); it!=mSaplingTrees.constEnd(); ++it) {
            //        float dbh = it.height / p.hdSapling * 100.f;
            //        if (dbh<1.) // minimum size: 1cm
            //            continue;
            //        double n = p.representedStemNumber(dbh); // one cohort on the pixel represents that number of trees
            //        dbh_sum += n*dbh;
            //        h_sum += n*it.height;
            //        age_sum += n*it.age.age;
            //        total += n;
            //    }
            //    if (total>0.) {
            //        dbh_sum /= total;
            //        h_sum /= total;
            //        age_sum /= total;
            //    }
            //    rAvgDbh = dbh_sum;
            //    rAvgHeight = h_sum;
            //    rAvgAge = age_sum;
            //    return total;
        }
    }
}
