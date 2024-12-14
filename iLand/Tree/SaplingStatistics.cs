// C++/core/{ saplings.h, saplings.cpp }
using iLand.World;
using System;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

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
        public float BasalArea { get; private set; } // basal area (m²) of saplings
        public CarbonNitrogenTuple CarbonNitrogenLiving { get; private init; } // kg carbon (kg / ru) of saplings
        public CarbonNitrogenTuple CarbonNitrogenGain { get; private set; } // net growth (kg / ru) of saplings
        public float CarbonOfRecruitedTrees { get; set; } // carbon that is added when trees >4m are created from saplings
        public Int16 CohortsWithDbh { get; set; } ///< number of cohorts that are >1.3m
        public Int16 DeadCohorts { get; private set; } // number of sapling cohorts died
        public float LeafArea { get; set; } // total leaf area (on all pixels of the resource unit)
        public float LeafAreaIndex { get; private set; } // leaf area index (m²/m²)
        public Int16 LivingCohorts { get; set; } // get the number of living sapling cohorts (C++ mLiving)
        public float LivingSaplings { get; set; } // number of individual trees in the regen layer (using Reineke's R), with h>1.3m
        public float LivingSaplingsSmall { get; set; } // number of individual trees in the regen layer (using Reinke's R), no height threshold
        // TODO: rename NewCohorts* for clarity?
        public Int16 NewCohorts { get; set; } // number of tree cohorts added (establishment, mAdded)
        public Int16 NewCohortsVegetative { get; set; } // number of cohorts added (vegetative sprouting, mAddedVegetative)
        public Int16 RecruitedCohorts { get; set; } // number of cohorts recruited (i.e. grown out of regeneration layer)

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

        // C++/core/saplings.cpp: calculate()
        public void AfterSaplingGrowth(Model model, ResourceUnit resourceUnit, TreeSpecies species)
        {
            // average age and heights
            if (this.LivingCohorts != 0)
            {
                this.AverageAgeInYears /= this.LivingCohorts;
                this.AverageDeltaHPotential /= this.LivingCohorts;
                this.AverageDeltaHRealized /= this.LivingCohorts;
                this.AverageHeight /= this.LivingCohorts;
            }

            // calculate carbon balance
            CarbonNitrogenTuple previousCarbonLiving = this.CarbonNitrogenLiving;
            this.CarbonNitrogenLiving.Zero();

            CarbonNitrogenTuple deadWood = new(); // pools for mortality
            CarbonNitrogenTuple deadFine = new();
            float c_turnover = 0.0F;
            float dead_wood_ag = 0.0F; // carbon aboveground
            float dead_fine_ag = 0.0F;
            // average dbh
            if (this.LivingCohorts > 0)
            {
                // calculate the avg dbh and number of stems
                float averageDbh = 100.0F * this.AverageHeight / species.SaplingGrowth.HeightDiameterRatio;
                // the number of "real" stems is given by the Reineke formula
                float nSaplings = this.LivingSaplings; // total number of saplings (>1.3m) (=represented stems, result of the Reineke equation)

                // woody parts: stem, branches, and coarse roots
                float woodyBiomass = species.GetBiomassStem(averageDbh) + species.GetBiomassBranch(averageDbh) + species.GetBiomassCoarseRoot(averageDbh);
                float foliage = species.GetBiomassFoliage(averageDbh);
                float fineRoot = foliage * species.FinerootFoliageRatio;
                this.LeafArea = foliage * nSaplings * species.SpecificLeafArea; // calculate leaf area on n saplings using the species specific SLA
                this.LeafAreaIndex = resourceUnit.AreaInLandscapeInM2 > 0.0F ? this.LeafArea / resourceUnit.AreaInLandscapeInM2 : 0.0F;
                this.BasalArea = Constant.Foresters * averageDbh * averageDbh * nSaplings;

                this.CarbonNitrogenLiving.AddBiomass(woodyBiomass * nSaplings, species.CarbonNitrogenRatioWood);
                this.CarbonNitrogenLiving.AddBiomass(foliage * nSaplings, species.CarbonNitrogenRatioFoliage);
                this.CarbonNitrogenLiving.AddBiomass(fineRoot * nSaplings, species.CarbonNitrogenRatioFineRoot);
                c_turnover = Constant.DryBiomassCarbonFraction * (foliage * species.TurnoverLeaf + fineRoot * species.TurnoverFineRoot) * nSaplings;

                Debug.Assert(Single.IsNaN(this.CarbonNitrogenLiving.C) == false, "carbon NaN in calculate (living trees).");

                // turnover
                // TODO: why is sapling turnover being added to snag pool?
                resourceUnit.Snags?.AddTurnoverLitter(species, foliage * species.TurnoverLeaf * nSaplings, fineRoot * species.TurnoverFineRoot * nSaplings);
                
                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                if (averageDbh > 1.0F)
                {
                    // compare only with cohorts >1.3m
                    float previousAverageDbh = 100.0F * (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowth.HeightDiameterRatio;
                    float nPreviousSaplings = this.CohortsWithDbh * species.SaplingGrowth.RepresentedStemNumberFromDiameter(MathF.Max(1.0F, previousAverageDbh));
                    if (nSaplings < nPreviousSaplings)
                    {
                        deadWood.AddBiomass(woodyBiomass * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioWood);
                        deadFine.AddBiomass(foliage * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioFoliage);
                        deadFine.AddBiomass(fineRoot * (nPreviousSaplings - nSaplings), species.CarbonNitrogenRatioFineRoot);
                        Debug.Assert(Single.IsNaN(deadFine.C) == false, "Carbon NaN in self thinning calculation.");
                    }
                }
            }
            else
            {
                this.LeafArea = 0.0F; // leaf area is not cleared at the beginning of the regen loop (for the water cycle)
            }

            if (model.SimulationState.IsFirstSimulationYear())
            {
                return; // no need for carbon flows in initial run
            }

            if (this.DeadCohorts != 0)
            {
                float avg_dbh_dead = sumDeadDbhInCm / this.DeadCohorts;
                float nSaplings = this.DeadCohorts * species.SaplingGrowth.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                float bm_above = (species.GetBiomassStem(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead)) * nSaplings;
                deadWood.AddBiomass(species.GetBiomassCoarseRoot(avg_dbh_dead) * nSaplings + bm_above, species.CarbonNitrogenRatioWood);
                dead_wood_ag += Constant.DryBiomassCarbonFraction * bm_above;

                float foliage = species.GetBiomassFoliage(avg_dbh_dead) * nSaplings;

                deadFine.AddBiomass(foliage, species.CarbonNitrogenRatioFoliage);
                deadFine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CarbonNitrogenRatioFineRoot);
                dead_fine_ag += Constant.DryBiomassCarbonFraction * foliage;
                //Debug.WriteLineIf(Single.IsNaN(deadFine.C), "carbon NaN in calculate (died trees).");
            }
            if (!deadWood.HasNoCarbon() || !deadFine.HasNoCarbon())
            {
                resourceUnit.Snags?.AddToSoil(species, deadWood, deadFine, dead_wood_ag, dead_fine_ag);
            }

            // calculate net growth:
            // delta of stocks
            this.CarbonNitrogenGain = this.CarbonNitrogenLiving + deadFine + deadWood - previousCarbonLiving;
            this.CarbonNitrogenGain.C += c_turnover + this.CarbonOfRecruitedTrees; // correction for newly created trees
            if (this.CarbonNitrogenGain.C < 0)
            {
                this.CarbonNitrogenGain.Zero();
            }

            //globalSettings.SystemStatistics.SaplingCount += LivingCohorts;
            //globalSettings.SystemStatistics.NewSaplings += NewSaplings;
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
            this.BasalArea = 0.0F;
            this.CarbonOfRecruitedTrees = 0.0F;
            this.CohortsWithDbh = 0;
            this.DeadCohorts = 0;
            this.LeafArea = 0.0F;
            this.LeafAreaIndex = 0.0F;
            this.LivingCohorts = 0;
            this.LivingSaplings = 0.0F;
            this.LivingSaplingsSmall = 0.0F;
            this.NewCohorts = 0;
            this.NewCohortsVegetative = 0;
            this.RecruitedCohorts = 0;
        }
    }
}
