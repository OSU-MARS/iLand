using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.World;
using System;

namespace iLand.Tree
{
    /** @class Establishment
        Establishment deals with the establishment process of saplings.
        http://iland.boku.ac.at/establishment
        Prerequisites for establishment are:
        the availability of seeds: derived from the seed-maps per Species (@sa SeedDispersal)
        the quality of the abiotic environment (TACA-model): calculations are performend here, based on climate and species responses
        the quality of the biotic environment, mainly light: based on the LIF-values

        TACA (tree and climate assessment) model:
        Nishke CR, Innes JL. 2008. A tree and climate assessment tool for modelling ecosystem response to climate change. Ecological Modelling
          210(3):263-277. https://doi.org/10.1016/j.ecolmodel.2007.07.026
        */
    public class Establishment
    {
        private readonly World.Climate mClimate; // link to the current climate
        private readonly ResourceUnitTreeSpecies mRUspecies; // link to the resource unit species (links to production data and species respones)
        private float mSumLifValue;
        private int mLifCount;

        public float MeanSeedDensity { get; private set; } // average seed density on the RU
        public float AbioticEnvironment { get; private set; } // integrated value of abiotic environment (i.e.: TACA-climate + total iLand environment)
        public int NumberEstablished { get; private set; } // return number of newly established trees in the current year
        // TACA switches
        public bool TacaMinTemp { get; private set; } // TACA flag for minimum temperature
        public bool TacaChill { get; private set; } // TACA flag chilling requirement
        public bool TacaGdd { get; private set; } // TACA flag for growing degree days
        public bool TacaFrostFree { get; private set; } // TACA flag for number of frost free days
        public int TacaFrostDaysAfterBudBurst { get; private set; } // number of frost days after bud birst
        public float WaterLimitation { get; private set; } // scalar value between 0 and 1 (1: no limitation, 0: no establishment)

        public Establishment(World.Climate climate, ResourceUnitTreeSpecies ruSpecies)
        {
            if (ruSpecies == null || ruSpecies.Species == null || ruSpecies.RU == null)
            {
                throw new ArgumentException("Species or resource unit is null");
            }

            this.mClimate = climate ?? throw new ArgumentNullException(nameof(climate), "No valid climate for a resource unit.");
            this.mRUspecies = ruSpecies;
            this.AbioticEnvironment = 0.0F;
            this.MeanSeedDensity = 0.0F;
            this.NumberEstablished = 0;
        }

        // average LIF value of LIF pixels where establishment is tested
        public float GetMeanLifValue() 
        { 
            return this.mLifCount > 0 ? this.mSumLifValue / this.mLifCount : 0.0F; 
        }

        public void Clear()
        {
            this.mLifCount = 0;
            this.mSumLifValue = 0.0F;

            this.AbioticEnvironment = 0.0F;
            this.NumberEstablished = 0;
            this.MeanSeedDensity = 0.0F;
            this.TacaMinTemp = false;
            this.TacaChill = false;
            this.TacaGdd = false;
            this.TacaFrostFree = false;
            this.TacaFrostDaysAfterBudBurst = 0;
            this.WaterLimitation = 0.0F;
        }

        private float CalculateWaterLimitation(int leafOnStart, int leafOnEnd)
        {
            // return 1 if effect is disabled
            if (mRUspecies.Species.EstablishmentParameters.PsiMin >= 0.0F)
            {
                return 1.0F;
            }

            float minimumPsiMPa = mRUspecies.Species.EstablishmentParameters.PsiMin;
            WaterCycle water = mRUspecies.RU.WaterCycle;

            // two week (14 days) running average of actual psi-values on the resource unit
            const int daysToAverage = 14;
            float[] soilWaterPotentialsInAverage = new float[daysToAverage];
            float currentPsiSum = 0.0F;
            
            float miniumumMovingAverage = Single.MaxValue;
            int daysInYear = mRUspecies.RU.Climate.GetDaysInYear();
            for (int dayOfYear = 0, averageIndex = 0; dayOfYear < daysInYear; ++dayOfYear)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                currentPsiSum -= soilWaterPotentialsInAverage[averageIndex];
                soilWaterPotentialsInAverage[averageIndex] = water.SoilWaterPsi[dayOfYear];
                currentPsiSum += soilWaterPotentialsInAverage[averageIndex];

                if (dayOfYear >= leafOnStart && dayOfYear <= leafOnEnd)
                {
                    float currentPsiAverage = dayOfYear > 0 ? currentPsiSum / MathF.Min(dayOfYear, daysToAverage) : currentPsiSum;
                    miniumumMovingAverage = MathF.Min(miniumumMovingAverage, currentPsiAverage);
                }

                // move to next value in the buffer
                averageIndex = ++averageIndex % daysToAverage;
            }

            if (miniumumMovingAverage > 1000.0)
            {
                return 1.0F; // invalid vegetation period?
            }

            // calculate the response of the species to this value of psi (see also Species::soilwaterResponse())
            float meanPsiMPa = 0.001F * miniumumMovingAverage; // convert to MPa
            float result = Maths.Limit((meanPsiMPa - minimumPsiMPa) / (-0.015F - minimumPsiMPa), 0.0F, 1.0F);

            return result;
        }

        /** Calculate the abiotic environemnt for seedling for a given species and a given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: http://iland.boku.ac.at/establishment#abiotic_environment
         a model mockup in R: script_establishment.r
         */
        public void CalculateAbioticEnvironment(Project projectFile)
        {
            //DebugTimer t("est_abiotic"); t.setSilent();
            // make sure that required calculations (e.g. watercycle are already performed)
            this.mRUspecies.CalculateBiomassGrowthForYear(projectFile, fromEstablishment: true); // calculate the 3pg module and run the water cycle (this is done only if that did not happen up to now); true: call comes from regeneration

            EstablishmentParameters establishment = mRUspecies.Species.EstablishmentParameters;
            Phenology pheno = mClimate.GetPhenology(mRUspecies.Species.PhenologyClass);

            this.TacaMinTemp = true; // minimum temperature threshold
            this.TacaChill = false;  // (total) chilling requirement
            this.TacaGdd = false;   // gdd-thresholds
            this.TacaFrostFree = false; // frost free days in vegetation period
            this.TacaFrostDaysAfterBudBurst = 0; // frost days after bud burst

            int doy = 0;
            float growingDegreeDays = 0.0F;
            float growingDegreeDaysBudBurst = 0.0F;
            int chillingDays = pheno.ChillingDaysAfterLeafOffInPreviousYear; // chilling days of the last autumn
            int frostFreeDays = 0;
            this.TacaFrostDaysAfterBudBurst = 0;
            bool chillRequirementSatisfied = false;
            bool budsHaveBurst = false;
            int veg_period_end = pheno.LeafOnEnd;
            if (veg_period_end >= 365)
            {
                veg_period_end = mClimate.Sun.LastDayLongerThan10_5Hours;
            }
            for (int index = mClimate.CurrentJanuary1; index != mClimate.NextJanuary1; ++index, ++doy)
            {
                ClimateDay day = mClimate[index];
                // minimum temperature: if temp too low . set prob. to zero
                if (day.MinTemperature < establishment.MinTemp)
                {
                    this.TacaMinTemp = false;
                }
                // count frost free days
                if (day.MinTemperature > 0.0)
                {
                    ++frostFreeDays;
                }
                // chilling requirement, GDD, bud birst
                if (day.MeanDaytimeTemperature >= -5.0 && day.MeanDaytimeTemperature < 5.0)
                {
                    ++chillingDays;
                }
                if (chillingDays > establishment.ChillRequirement)
                {
                    chillRequirementSatisfied = true;
                }
                // GDDs above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (doy <= veg_period_end)
                {
                    // accumulate growing degree days
                    if (chillRequirementSatisfied && day.MeanDaytimeTemperature > establishment.GrowingDegreeDaysBaseTemperature)
                    {
                        growingDegreeDays += day.MeanDaytimeTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                        growingDegreeDaysBudBurst += day.MeanDaytimeTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud burst is reset
                    if (day.MeanDaytimeTemperature <= 0.0)
                    {
                        growingDegreeDaysBudBurst = 0.0F;
                    }
                    if (growingDegreeDaysBudBurst > establishment.GddBudBurst)
                    {
                        budsHaveBurst = true;
                    }
                    if (doy < veg_period_end && budsHaveBurst && day.MinTemperature <= 0.0)
                    {
                        ++this.TacaFrostDaysAfterBudBurst;
                    }
                }
            }
            // chilling requirement
            if (chillRequirementSatisfied)
            {
                this.TacaChill = true;
            }

            // GDD requirements
            if (growingDegreeDays > establishment.MinimumGrowingDegreeDays && growingDegreeDays < establishment.MaximumGrowingDegreeDays)
            {
                this.TacaGdd = true;
            }

            // frost free days in the vegetation period
            if (frostFreeDays > establishment.MinimumFrostFreeDays)
            {
                this.TacaFrostFree = true;
            }

            // if all requirements are met:
            if (this.TacaChill && this.TacaMinTemp && this.TacaGdd && this.TacaFrostFree)
            {
                // negative effect of frost events after bud birst
                float frostEffect = 1.0F;
                if (this.TacaFrostDaysAfterBudBurst > 0)
                {
                    frostEffect = MathF.Pow(establishment.FrostTolerance, MathF.Sqrt(this.TacaFrostDaysAfterBudBurst));
                }
                // negative effect due to water limitation on establishment [1: no effect]
                this.WaterLimitation = this.CalculateWaterLimitation(pheno.LeafOnStart, pheno.GetLeafOnDurationInDays());
                // combine drought and frost effect multiplicatively
                this.AbioticEnvironment = frostEffect * this.WaterLimitation;
            }
            else
            {
                this.AbioticEnvironment = 0.0F; // if any of the requirements is not met
            }
        }

        //public void WriteDebugOutputs()
        //{
        //    if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.Establishment))
        //    {
        //        List<object> output = GlobalSettings.Instance.DebugList(mRUS.RU.Index, DebugOutputs.Establishment);
        //        // establishment details
        //        output.AddRange(new object[] { mRUS.Species.ID,  mRUS.RU.Index, mRUS.RU.ID,
        //                                       MeanSeedDensity, TacaMinTemp, TacaChill, TacaFrostFree, TacaGdd,
        //                                       TacaFrostDaysAfterBudburst, WaterLimitation, AbioticEnvironment,
        //                                       mRUS.BiomassGrowth.EnvironmentalFactor, mRUS.SaplingStats.NewSaplings
        //                                       //mSaplingStat.livingSaplings(), mSaplingStat.averageHeight(), mSaplingStat.averageAge(), mSaplingStat.averageDeltaHPot(), mSaplingStat.averageDeltaHRealized();
        //                                       //mSaplingStat.newSaplings(), mSaplingStat.diedSaplings(), mSaplingStat.recruitedSaplings(), mSpecies.saplingGrowthParameters().referenceRatio;
        //        });
        //    }

        //    if (GlobalSettings.Instance.LogDebug())
        //    {
        //        Debug.WriteLine("establishment of RU " + mRUS.RU.Index + " species " + mRUS.Species.ID +
        //                        " seeds density :" + MeanSeedDensity + 
        //                        " abiotic environment: " + AbioticEnvironment +
        //                        " f_env,yr: " + mRUS.BiomassGrowth.EnvironmentalFactor + 
        //                        " N(established):"  + NumberEstablished);
        //    }
        //}
    }
}
