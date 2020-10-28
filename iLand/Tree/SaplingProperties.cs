using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /** The SaplingStat class stores statistics on the resource unit x species level.
      */
    public class SaplingProperties
    {
        private float mSumDbhDied; // running sum of dbh of died trees (used to calculate detritus)

        public float AverageAge { get; set; } // average age of saplings (years)
        public float AverageDeltaHPot { get; set; } // average height increment potential (m)
        public float AverageDeltaHRealized { get; set; } // average realized height increment
        public float AverageHeight { get; set; } // average height of saplings (m)
        public CarbonNitrogenTuple CarbonLiving { get; private set; } // kg Carbon (kg/ru) of saplings
        public CarbonNitrogenTuple CarbonGain { get; private set; } // net growth (kg / ru) of saplings
        public int DeadSaplings { get; private set; } // number of tree cohorts died
        public int LivingCohorts { get; set; } // get the number of cohorts
        public float LivingSaplings { get; set; } // number of individual trees in the regen layer (using Reinekes R), with h>1.3m
        public float LivingSaplingsSmall { get; set; } // number of individual trees of cohorts < 1.3m height
        public int NewSaplings { get; set; } // number of tree cohorts added
        public int RecruitedSaplings { get; set; } // number of cohorts recruited (i.e. grown out of regeneration layer)

        public SaplingProperties()
        {
            CarbonGain = new CarbonNitrogenTuple();
            CarbonLiving = new CarbonNitrogenTuple();
            ClearStatistics();
        }

        public void AddCarbonOfDeadSapling(float dbh)
        { 
            ++DeadSaplings;
            mSumDbhDied += dbh; 
        }

        public void ClearStatistics()
        {
            RecruitedSaplings = DeadSaplings = LivingCohorts = 0;
            LivingSaplings = 0.0F;
            LivingSaplingsSmall = 0.0F;
            mSumDbhDied = 0.0F;
            AverageHeight = 0.0F;
            AverageAge = 0.0F;
            AverageDeltaHPot = 0.0F;
            AverageDeltaHRealized = 0.0F;
            NewSaplings = 0;
        }

        public void Recalculate(Simulation.Model model, ResourceUnit ru, TreeSpecies species)
        {
            if (LivingCohorts != 0)
            {
                this.AverageHeight /= this.LivingCohorts;
                this.AverageAge /= this.LivingCohorts;
                this.AverageDeltaHPot /= this.LivingCohorts;
                this.AverageDeltaHRealized /= this.LivingCohorts;
            }
            if (model.ModelSettings.CurrentYear == 0)
            {
                return; // no need for carbon flows in initial run
            }

            // calculate carbon balance
            CarbonNitrogenTuple old_state = CarbonLiving;
            CarbonLiving.Zero();

            CarbonNitrogenTuple dead_wood = new CarbonNitrogenTuple(); // pools for mortality
            CarbonNitrogenTuple dead_fine = new CarbonNitrogenTuple();
            // average dbh
            if (LivingCohorts > 0)
            {
                // calculate the avg dbh and number of stems
                float avg_dbh = 100.0F * this.AverageHeight / species.SaplingGrowthParameters.HeightDiameterRatio;
                // the number of "real" stems is given by the Reineke formula
                float n = this.LivingSaplings; // total number of saplings (>0.05m)

                // woody parts: stem, branchse and coarse roots
                float woody_bm = species.GetBiomassWoody(avg_dbh) + species.GetBiomassBranch(avg_dbh) + species.GetBiomassRoot(avg_dbh);
                float foliage = species.GetBiomassFoliage(avg_dbh);
                float fineroot = foliage * species.FinerootFoliageRatio;

                CarbonLiving.AddBiomass(woody_bm * n, species.CNRatioWood);
                CarbonLiving.AddBiomass(foliage * n, species.CNRatioFoliage);
                CarbonLiving.AddBiomass(fineroot * n, species.CNRatioFineRoot);

                Debug.Assert(Double.IsNaN(CarbonLiving.C) == false, "carbon NaN in calculate (living trees).");

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
                    float avg_dbh_before = 100.0F * (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowthParameters.HeightDiameterRatio;
                    float n_before = LivingCohorts * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(MathF.Max(1.0F, avg_dbh_before));
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
                float avg_dbh_dead = mSumDbhDied / this.DeadSaplings;
                float n = this.DeadSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                dead_wood.AddBiomass((species.GetBiomassWoody(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead) + species.GetBiomassRoot(avg_dbh_dead)) * n, species.CNRatioWood);
                float foliage = species.GetBiomassFoliage(avg_dbh_dead) * n;

                dead_fine.AddBiomass(foliage, species.CNRatioFoliage);
                dead_fine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CNRatioFineRoot);
                Debug.WriteLineIf(Double.IsNaN(dead_fine.C), "carbon NaN in calculate (died trees).");
            }
            if (!dead_wood.HasNoCarbon() || !dead_fine.HasNoCarbon())
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
                CarbonGain.Zero();
            }

            //globalSettings.SystemStatistics.SaplingCount += LivingCohorts;
            //globalSettings.SystemStatistics.NewSaplings += NewSaplings;
        }

        ///  returns the *represented* (Reineke's Law) number of trees (N/ha) and the mean dbh/height (cm/m)
        public double GetLivingStemNumber(TreeSpecies species, out float averageDbh, out float averageHeight, out float averageAge)
        {
            averageHeight = this.AverageHeight;
            averageDbh = 100.0F * averageHeight / species.SaplingGrowthParameters.HeightDiameterRatio;
            averageAge = this.AverageAge;
            double n = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(averageDbh);
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
