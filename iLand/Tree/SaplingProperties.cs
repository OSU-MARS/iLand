using iLand.Simulation;
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
        public float AverageDeltaHPotential { get; set; } // average height increment potential (m)
        public float AverageDeltaHRealized { get; set; } // average realized height increment
        public float AverageHeight { get; set; } // average height of saplings (m)
        public CarbonNitrogenTuple CarbonLiving { get; private init; } // kg Carbon (kg/ru) of saplings
        public CarbonNitrogenTuple CarbonGain { get; private set; } // net growth (kg / ru) of saplings
        public int DeadSaplings { get; private set; } // number of tree cohorts died
        public int LivingCohorts { get; set; } // get the number of cohorts
        public float LivingSaplings { get; set; } // number of individual trees in the regen layer (using Reinekes R), with h>1.3m
        public float LivingSaplingsSmall { get; set; } // number of individual trees of cohorts < 1.3m height
        public int NewSaplings { get; set; } // number of tree cohorts added
        public int RecruitedSaplings { get; set; } // number of cohorts recruited (i.e. grown out of regeneration layer)

        public SaplingProperties()
        {
            this.CarbonGain = new CarbonNitrogenTuple();
            this.CarbonLiving = new CarbonNitrogenTuple();
            this.ClearStatistics();
        }

        public void AddCarbonOfDeadSapling(float dbh)
        { 
            ++this.DeadSaplings;
            this.mSumDbhDied += dbh; 
        }

        public void ClearStatistics()
        {
            this.mSumDbhDied = 0.0F;
            this.AverageAge = 0.0F;
            this.AverageDeltaHPotential = 0.0F;
            this.AverageDeltaHRealized = 0.0F;
            this.AverageHeight = 0.0F;
            this.LivingSaplings = 0.0F;
            this.LivingSaplingsSmall = 0.0F;
            this.NewSaplings = 0;
            this.RecruitedSaplings = 0;
            this.DeadSaplings = 0;
            this.LivingCohorts = 0;
        }

        public void AfterSaplingGrowth(Model model, ResourceUnit ru, TreeSpecies species)
        {
            this.AverageAgeAndHeights();
            if (model.CurrentYear == 0)
            {
                return; // no need for carbon flows in initial run
            }

            // calculate carbon balance
            CarbonNitrogenTuple previousCarbonLiving = this.CarbonLiving;
            this.CarbonLiving.Zero();

            CarbonNitrogenTuple deadWood = new(); // pools for mortality
            CarbonNitrogenTuple deadFine = new();
            // average dbh
            if (this.LivingCohorts > 0)
            {
                // calculate the avg dbh and number of stems
                float averageDbh = 100.0F * this.AverageHeight / species.SaplingGrowthParameters.HeightDiameterRatio;
                // the number of "real" stems is given by the Reineke formula
                float nSaplings = this.LivingSaplings; // total number of saplings (>0.05m)

                // woody parts: stem, branchse and coarse roots
                float woodyBiomass = species.GetBiomassStem(averageDbh) + species.GetBiomassBranch(averageDbh) + species.GetBiomassRoot(averageDbh);
                float foliage = species.GetBiomassFoliage(averageDbh);
                float fineRoot = foliage * species.FinerootFoliageRatio;

                this.CarbonLiving.AddBiomass(woodyBiomass * nSaplings, species.CNRatioWood);
                this.CarbonLiving.AddBiomass(foliage * nSaplings, species.CNRatioFoliage);
                this.CarbonLiving.AddBiomass(fineRoot * nSaplings, species.CNRatioFineRoot);

                Debug.Assert(Single.IsNaN(this.CarbonLiving.C) == false, "carbon NaN in calculate (living trees).");

                // turnover
                if (ru.Snags != null)
                {
                    ru.Snags.AddTurnoverLitter(species, foliage * species.TurnoverLeaf, fineRoot * species.TurnoverFineRoot);
                }
                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (averageDbh > 1.0F)
                {
                    float previousAverageDbh = 100.0F * (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowthParameters.HeightDiameterRatio;
                    float nPreviousSaplings = this.LivingCohorts * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(MathF.Max(1.0F, previousAverageDbh));
                    if (nSaplings < nPreviousSaplings)
                    {
                        deadWood.AddBiomass(woodyBiomass * (nPreviousSaplings - nSaplings), species.CNRatioWood);
                        deadFine.AddBiomass(foliage * (nPreviousSaplings - nSaplings), species.CNRatioFoliage);
                        deadFine.AddBiomass(fineRoot * (nPreviousSaplings - nSaplings), species.CNRatioFineRoot);
                        Debug.Assert(Double.IsNaN(deadFine.C) == false, "Carbon NaN in self thinning calculation.");
                    }
                }
            }
            if (this.DeadSaplings != 0)
            {
                float avg_dbh_dead = mSumDbhDied / this.DeadSaplings;
                float n = this.DeadSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                deadWood.AddBiomass((species.GetBiomassStem(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead) + species.GetBiomassRoot(avg_dbh_dead)) * n, species.CNRatioWood);
                float foliage = species.GetBiomassFoliage(avg_dbh_dead) * n;

                deadFine.AddBiomass(foliage, species.CNRatioFoliage);
                deadFine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CNRatioFineRoot);
                Debug.WriteLineIf(Double.IsNaN(deadFine.C), "carbon NaN in calculate (died trees).");
            }
            if (!deadWood.HasNoCarbon() || !deadFine.HasNoCarbon())
            {
                if (ru.Snags != null)
                {
                    ru.Snags.AddToSoil(species, deadWood, deadFine);
                }
            }

            // calculate net growth:
            // delta of stocks
            this.CarbonGain = this.CarbonLiving + deadFine + deadWood - previousCarbonLiving;
            if (this.CarbonGain.C < 0)
            {
                this.CarbonGain.Zero();
            }

            //globalSettings.SystemStatistics.SaplingCount += LivingCohorts;
            //globalSettings.SystemStatistics.NewSaplings += NewSaplings;
        }

        public void AverageAgeAndHeights()
        {
            if (this.LivingCohorts != 0)
            {
                this.AverageAge /= this.LivingCohorts;
                this.AverageDeltaHPotential /= this.LivingCohorts;
                this.AverageDeltaHRealized /= this.LivingCohorts;
                this.AverageHeight /= this.LivingCohorts;
            }
        }

        ///  returns the *represented* (Reineke's Law) number of trees (N/ha) and the mean dbh/height (cm/m)
        public float GetLivingStemNumber(TreeSpecies species, out float averageDbh, out float averageHeight, out float averageAge)
        {
            averageHeight = this.AverageHeight;
            averageDbh = 100.0F * averageHeight / species.SaplingGrowthParameters.HeightDiameterRatio;
            averageAge = this.AverageAge;
            float nSaplings = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(averageDbh);
            return nSaplings;
            // *** old code (sapling.cpp) ***
            //    float total = 0.0;
            //    float dbh_sum = 0.0;
            //    float h_sum = 0.0;
            //    float age_sum = 0.0;
            //    SaplingGrowthParameters &p = mRUS.species().saplingGrowthParameters();
            //    for (QVector<SaplingTreeOld>::const_iterator it = mSaplingTrees.constBegin(); it!=mSaplingTrees.constEnd(); ++it) {
            //        float dbh = it.height / p.hdSapling * 100.f;
            //        if (dbh<1.) // minimum size: 1cm
            //            continue;
            //        float n = p.representedStemNumber(dbh); // one cohort on the pixel represents that number of trees
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
