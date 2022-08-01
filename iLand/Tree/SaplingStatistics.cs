using iLand.Simulation;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /** The SaplingStat class stores statistics on the resource unit x species level.
      */
    public class SaplingStatistics
    {
        private float sumDeadDbhInCm; // running sum of dbh of died trees (used to calculate detritus)

        public float AverageAgeInYears { get; set; } // average age of saplings (years)
        public float AverageDeltaHPotential { get; set; } // average height increment potential (m)
        public float AverageDeltaHRealized { get; set; } // average realized height increment
        public float AverageHeight { get; set; } // average height of saplings (m)
        public CarbonNitrogenTuple CarbonNitrogenLiving { get; private init; } // kg carbon (kg / ru) of saplings
        public CarbonNitrogenTuple CarbonNitrogenGain { get; private set; } // net growth (kg / ru) of saplings
        public int DeadCohorts { get; private set; } // number of sapling cohorts died
        public int LivingCohorts { get; set; } // get the number of living sapling cohorts
        public float LivingSaplings { get; set; } // number of individual trees in the regen layer (using Reinekes R), with h>1.3m
        public float LivingSaplingsSmall { get; set; } // number of individual trees of cohorts < 1.3m height
        public int NewCohorts { get; set; } // number of tree cohorts added
        public int RecruitedCohorts { get; set; } // number of cohorts recruited (i.e. grown out of regeneration layer)

        public SaplingStatistics()
        {
            this.CarbonNitrogenGain = new();
            this.CarbonNitrogenLiving = new();
            this.ZeroStatistics();
        }

        public void AddDeadCohort(float dbhInCm)
        { 
            ++this.DeadCohorts;
            this.sumDeadDbhInCm += dbhInCm; 
        }

        public void AfterSaplingGrowth(Model model, ResourceUnit resourceUnit, TreeSpecies species)
        {
            this.AverageAgeAndHeights();
            if (model.SimulationState.CurrentYear == 0)
            {
                return; // no need for carbon flows in initial year
            }

            // calculate carbon balance
            CarbonNitrogenTuple previousCarbonLiving = this.CarbonNitrogenLiving;
            this.CarbonNitrogenLiving.Zero();

            CarbonNitrogenTuple deadWood = new(); // pools for mortality
            CarbonNitrogenTuple deadFine = new();
            // average dbh
            if (this.LivingCohorts > 0)
            {
                // calculate the avg dbh and number of stems
                float averageDbh = 100.0F * this.AverageHeight / species.SaplingGrowth.HeightDiameterRatio;
                // the number of "real" stems is given by the Reineke formula
                float nSaplings = this.LivingSaplings; // total number of saplings (>0.05m)

                // woody parts: stem, branchse and coarse roots
                float woodyBiomass = species.GetBiomassStem(averageDbh) + species.GetBiomassBranch(averageDbh) + species.GetBiomassCoarseRoot(averageDbh);
                float foliage = species.GetBiomassFoliage(averageDbh);
                float fineRoot = foliage * species.FinerootFoliageRatio;

                this.CarbonNitrogenLiving.AddBiomass(woodyBiomass * nSaplings, species.CarbonNitrogenRatioWood);
                this.CarbonNitrogenLiving.AddBiomass(foliage * nSaplings, species.CarbonNitrogenRatioFoliage);
                this.CarbonNitrogenLiving.AddBiomass(fineRoot * nSaplings, species.CarbonNitrogenRatioFineRoot);

                Debug.Assert(Single.IsNaN(this.CarbonNitrogenLiving.C) == false, "carbon NaN in calculate (living trees).");

                // turnover
                if (resourceUnit.Snags != null)
                {
                    resourceUnit.Snags.AddTurnoverLitter(species, foliage * species.TurnoverLeaf, fineRoot * species.TurnoverFineRoot);
                }
                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (averageDbh > 1.0F)
                {
                    float previousAverageDbh = 100.0F * (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowth.HeightDiameterRatio;
                    float nPreviousSaplings = this.LivingCohorts * species.SaplingGrowth.RepresentedStemNumberFromDiameter(MathF.Max(1.0F, previousAverageDbh));
                    if (nSaplings < nPreviousSaplings)
                    {
                        deadWood.AddBiomass(woodyBiomass * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioWood);
                        deadFine.AddBiomass(foliage * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioFoliage);
                        deadFine.AddBiomass(fineRoot * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioFineRoot);
                        Debug.Assert(Single.IsNaN(deadFine.C) == false, "Carbon NaN in self thinning calculation.");
                    }
                }
            }
            if (this.DeadCohorts != 0)
            {
                float avg_dbh_dead = sumDeadDbhInCm / this.DeadCohorts;
                float n = this.DeadCohorts * species.SaplingGrowth.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                deadWood.AddBiomass((species.GetBiomassStem(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead) + species.GetBiomassCoarseRoot(avg_dbh_dead)) * n, species.CarbonNitrogenRatioWood);
                float foliage = species.GetBiomassFoliage(avg_dbh_dead) * n;

                deadFine.AddBiomass(foliage, species.CarbonNitrogenRatioFoliage);
                deadFine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CarbonNitrogenRatioFineRoot);
                //Debug.WriteLineIf(Single.IsNaN(deadFine.C), "carbon NaN in calculate (died trees).");
            }
            if (!deadWood.HasNoCarbon() || !deadFine.HasNoCarbon())
            {
                if (resourceUnit.Snags != null)
                {
                    resourceUnit.Snags.AddToSoil(species, deadWood, deadFine);
                }
            }

            // calculate net growth:
            // delta of stocks
            this.CarbonNitrogenGain = this.CarbonNitrogenLiving + deadFine + deadWood - previousCarbonLiving;
            if (this.CarbonNitrogenGain.C < 0)
            {
                this.CarbonNitrogenGain.Zero();
            }

            //globalSettings.SystemStatistics.SaplingCount += LivingCohorts;
            //globalSettings.SystemStatistics.NewSaplings += NewSaplings;
        }

        public void AverageAgeAndHeights()
        {
            if (this.LivingCohorts != 0)
            {
                this.AverageAgeInYears /= this.LivingCohorts;
                this.AverageDeltaHPotential /= this.LivingCohorts;
                this.AverageDeltaHRealized /= this.LivingCohorts;
                this.AverageHeight /= this.LivingCohorts;
            }
        }

        ///  returns the *represented* (Reineke's Law) number of trees (N/ha) and the mean dbh/height (cm/m)
        public float GetLivingStemNumber(TreeSpecies species, out float averageDbh, out float averageHeight, out float averageAge)
        {
            averageHeight = this.AverageHeight;
            averageDbh = 100.0F * averageHeight / species.SaplingGrowth.HeightDiameterRatio;
            averageAge = this.AverageAgeInYears;
            float nSaplings = species.SaplingGrowth.RepresentedStemNumberFromDiameter(averageDbh);
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

        public void ZeroStatistics()
        {
            this.sumDeadDbhInCm = 0.0F;

            this.AverageAgeInYears = 0.0F;
            this.AverageDeltaHPotential = 0.0F;
            this.AverageDeltaHRealized = 0.0F;
            this.AverageHeight = 0.0F;
            this.DeadCohorts = 0;
            this.LivingCohorts = 0;
            this.LivingSaplings = 0.0F;
            this.LivingSaplingsSmall = 0.0F;
            this.NewCohorts = 0;
            this.RecruitedCohorts = 0;
        }
    }
}
