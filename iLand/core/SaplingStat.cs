using iLand.Tools;
using System;
using System.Diagnostics;

namespace iLand.Core
{
    /** The SaplingStat class stores statistics on the resource unit x species level.
      */
    public class SaplingStat
    {
        private double mSumDbhDied; ///< running sum of dbh of died trees (used to calculate detritus)

        public double AverageAge { get; set; } ///< average age of saplings (years)
        public double AverageDeltaHPot { get; set; } ///< average height increment potential (m)
        public double AverageDeltaHRealized { get; set; } ///< average realized height increment
        public double AverageHeight { get; set; } ///< average height of saplings (m)
        public CNPair CarbonLiving { get; private set; } ///< kg Carbon (kg/ru) of saplings
        public CNPair CarbonGain { get; private set; } ///< net growth (kg / ru) of saplings
        public int DeadSaplings { get; private set; } ///< number of tree cohorts died
        public int LivingCohorts { get; set; } ///< get the number of cohorts
        public double LivingSaplings { get; set; } ///< number of individual trees in the regen layer (using Reinekes R), with h>1.3m
        public double LivingSaplingsSmall { get; set; } ///< number of individual trees of cohorts < 1.3m height
        public int NewSaplings { get; set; } ///< number of tree cohorts added
        public int RecruitedSaplings { get; set; } ///< number of cohorts recruited (i.e. grown out of regeneration layer)

        public SaplingStat()
        {
            CarbonGain = new CNPair();
            CarbonLiving = new CNPair();
            ClearStatistics();
        }

        public void AddCarbonOfDeadSapling(float dbh)
        { 
            DeadSaplings++;
            mSumDbhDied += dbh; 
        }

        public void ClearStatistics()
        {
            RecruitedSaplings = DeadSaplings = LivingCohorts = 0;
            LivingSaplings = 0.0;
            LivingSaplingsSmall = 0.0;
            mSumDbhDied = 0.0;
            AverageHeight = 0.0;
            AverageAge = 0.0;
            AverageDeltaHPot = AverageDeltaHRealized = 0.0;
            NewSaplings = 0;
        }

        public void Calculate(Species species, ResourceUnit ru)
        {
            if (LivingCohorts != 0)
            {
                AverageHeight /= (double)LivingCohorts;
                AverageAge /= (double)LivingCohorts;
                AverageDeltaHPot /= (double)LivingCohorts;
                AverageDeltaHRealized /= (double)LivingCohorts;
            }
            if (GlobalSettings.Instance.CurrentYear == 0)
            {
                return; // no need for carbon flows in initial run
            }

            // calculate carbon balance
            CNPair old_state = CarbonLiving;
            CarbonLiving.Clear();

            CNPair dead_wood = new CNPair(); // pools for mortality
            CNPair dead_fine = new CNPair();
            // average dbh
            if (LivingCohorts > 0)
            {
                // calculate the avg dbh and number of stems
                double avg_dbh = AverageHeight / species.SaplingGrowthParameters.HdSapling * 100.0;
                // the number of "real" stems is given by the Reineke formula
                double n = LivingSaplings; // total number of saplings (>0.05m)

                // woody parts: stem, branchse and coarse roots
                double woody_bm = species.GetBiomassWoody(avg_dbh) + species.GetBiomassBranch(avg_dbh) + species.GetBiomassRoot(avg_dbh);
                double foliage = species.GetBiomassFoliage(avg_dbh);
                double fineroot = foliage * species.FinerootFoliageRatio;

                CarbonLiving.AddBiomass(woody_bm * n, species.CNRatioWood);
                CarbonLiving.AddBiomass(foliage * n, species.CNRatioFoliage);
                CarbonLiving.AddBiomass(fineroot * n, species.CNRatioFineRoot);

                Debug.WriteLineIf(Double.IsNaN(CarbonLiving.C), "carbon NaN in calculate (living trees).");

                // turnover
                if (ru.Snags != null)
                {
                    ru.Snags.AddTurnoverLitter(species, foliage * species.TurnoverLeaf, fineroot * species.TurnoverRoot);
                }
                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (avg_dbh > 1.0)
                {
                    double avg_dbh_before = (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowthParameters.HdSapling * 100.0;
                    double n_before = LivingCohorts * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(Math.Max(1.0, avg_dbh_before));
                    if (n < n_before)
                    {
                        dead_wood.AddBiomass(woody_bm * (n_before - n), species.CNRatioWood);
                        dead_fine.AddBiomass(foliage * (n_before - n), species.CNRatioFoliage);
                        dead_fine.AddBiomass(fineroot * (n_before - n), species.CNRatioFineRoot);
                        Debug.WriteLineIf(Double.IsNaN(dead_fine.C), "carbon NaN in calculate (self thinning).");
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
                Debug.WriteLineIf(Double.IsNaN(dead_fine.C), "carbon NaN in calculate (died trees).");
            }
            if (!dead_wood.IsEmpty() || !dead_fine.IsEmpty())
            {
                if (ru.Snags != null)
                {
                    ru.Snags.AddToSoil(species, dead_wood, dead_fine);
                }
            }

            // calculate net growth:
            // delta of stocks
            CarbonGain = CarbonLiving + dead_fine + dead_wood - old_state;
            if (CarbonGain.C < 0)
            {
                CarbonGain.Clear();
            }

            GlobalSettings.Instance.SystemStatistics.SaplingCount += LivingCohorts;
            GlobalSettings.Instance.SystemStatistics.NewSaplings += NewSaplings;
        }

        ///  returns the *represented* (Reineke's Law) number of trees (N/ha) and the mean dbh/height (cm/m)
        public double LivingStemNumber(Species species, out double rAvgDbh, out double rAvgHeight, out double rAvgAge)
        {
            rAvgHeight = AverageHeight;
            rAvgDbh = rAvgHeight / species.SaplingGrowthParameters.HdSapling * 100.0F;
            rAvgAge = AverageAge;
            double n = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(rAvgDbh);
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
