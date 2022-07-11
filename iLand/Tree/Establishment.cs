using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.World;
using System;

namespace iLand.Tree
{
    /** @class Establishment
        Establishment deals with the establishment process of saplings.
        http://iland-model.org/establishment
        Prerequisites for establishment are:
        the availability of seeds: derived from the seed-maps per Species (@sa SeedDispersal)
        the quality of the abiotic environment (TACA-model): calculations are performend here, based on weather and species responses
        the quality of the biotic environment, mainly light: based on the LIF-values

        TACA (tree and climate assessment) model:
        Nishke CR, Innes JL. 2008. A tree and climate assessment tool for modelling ecosystem response to climate change. Ecological Modelling
          210(3):263-277. https://doi.org/10.1016/j.ecolmodel.2007.07.026
        */
    public class Establishment
    {
        private float sumLifValue;
        private int lifCount;

        // TACA switches
        private bool tacaChill; // TACA flag chilling requirement
        private int tacaFrostDaysAfterBudBurst; // number of frost days after bud birst
        private bool tacaFrostFree; // TACA flag for number of frost free days
        private bool tacaGdd; // TACA flag for growing degree days
        private bool tacaMinTemp; // TACA flag for minimum temperature
        private float waterLimitation; // scalar value between 0 and 1 (1: no limitation, 0: no establishment)

        public float AbioticEnvironment { get; private set; } // integrated value of abiotic environment (i.e.: TACA-climate + total iLand environment)
        public float MeanSeedDensity { get; private set; } // average seed density on the RU
        public int NumberEstablished { get; private set; } // return number of newly established trees in the current year

        public Establishment()
        {
            this.tacaChill = false;
            this.tacaFrostDaysAfterBudBurst = 0;
            this.tacaFrostFree = false;
            this.tacaGdd = false;
            this.tacaFrostDaysAfterBudBurst = 0;
            this.waterLimitation = 0.0F;

            this.AbioticEnvironment = 0.0F;
            this.MeanSeedDensity = 0.0F;
            this.NumberEstablished = 0;
        }

        // average LIF value of LIF pixels where establishment is tested
        public float GetMeanLifValue() 
        { 
            return this.lifCount > 0 ? this.sumLifValue / this.lifCount : 0.0F; 
        }

        public void Clear()
        {
            this.lifCount = 0;
            this.sumLifValue = 0.0F;

            this.AbioticEnvironment = 0.0F;
            this.NumberEstablished = 0;
            this.MeanSeedDensity = 0.0F;
            this.tacaMinTemp = false;
            this.tacaChill = false;
            this.tacaGdd = false;
            this.tacaFrostFree = false;
            this.tacaFrostDaysAfterBudBurst = 0;
            this.waterLimitation = 0.0F;
        }

        private static float CalculateWaterLimitation(ResourceUnitTreeSpecies ruSpecies, int leafOnStart, int leafOnEnd)
        {
            // return 1 if effect is disabled
            if (ruSpecies.Species.EstablishmentParameters.PsiMin >= 0.0F)
            {
                return 1.0F;
            }

            float minimumPsiMPa = ruSpecies.Species.EstablishmentParameters.PsiMin;
            WaterCycle water = ruSpecies.RU.WaterCycle;

            // two week (14 days) running average of actual psi-values on the resource unit
            const int daysToAverage = 14;
            float[] soilWaterPotentialsInAverage = new float[daysToAverage];
            float currentPsiSum = 0.0F;
            
            float miniumumMovingAverage = Single.MaxValue;
            int daysInYear = ruSpecies.RU.Weather.GetDaysInYear();
            for (int dayOfYear = 0, averageIndex = 0; dayOfYear < daysInYear; ++dayOfYear)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                currentPsiSum -= soilWaterPotentialsInAverage[averageIndex];
                soilWaterPotentialsInAverage[averageIndex] = water.SoilWaterPotentialByDay[dayOfYear];
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

            // calculate the response of the species to this value of psi (see also Species.GetSoilWaterModifier())
            float meanPsiMPa = 0.001F * miniumumMovingAverage; // convert to MPa
            float result = Maths.Limit((meanPsiMPa - minimumPsiMPa) / (-0.015F - minimumPsiMPa), 0.0F, 1.0F);

            return result;
        }

        /** Calculate the abiotic environemnt for seedling for a given species and a given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: http://iland-model.org/establishment#abiotic_environment
         a model mockup in R: script_establishment.r
         */
        public void CalculateAbioticEnvironment(Project projectFile, WeatherDaily weather, ResourceUnitTreeSpecies ruSpecies)
        {
            // make sure that required calculations (e.g. watercycle are already performed)
            // TODO: why is CalculateBiomassGrowthForYear() called from three places?
            ruSpecies.CalculateBiomassGrowthForYear(projectFile, fromSaplingEstablishmentOrGrowth: true); // calculate the 3-PG module and run the water cycle (this is done only if that did not happen up to now); true: call comes from regeneration

            EstablishmentParameters establishment = ruSpecies.Species.EstablishmentParameters;
            Phenology phenonology = weather.GetPhenology(ruSpecies.Species.PhenologyClass);

            this.tacaMinTemp = true; // minimum temperature threshold
            this.tacaChill = false;  // (total) chilling requirement
            this.tacaGdd = false;   // gdd-thresholds
            this.tacaFrostFree = false; // frost free days in vegetation period
            this.tacaFrostDaysAfterBudBurst = 0; // frost days after bud burst

            int dayOfyear = 0;
            float growingDegreeDays = 0.0F;
            float growingDegreeDaysBudBurst = 0.0F;
            int chillingDays = phenonology.ChillingDaysAfterLeafOffInPreviousYear; // chilling days of the last autumn
            int frostFreeDays = 0;
            this.tacaFrostDaysAfterBudBurst = 0;
            bool chillRequirementSatisfied = false;
            bool budsHaveBurst = false;
            int leafOnEndDay = phenonology.LeafOnEnd;
            if (leafOnEndDay >= 365)
            {
                leafOnEndDay = weather.Sun.LastDayLongerThan10_5Hours;
            }

            WeatherTimeSeriesDaily dailyWeather = weather.TimeSeries;
            for (int dayIndex = weather.CurrentJanuary1; dayIndex != weather.NextJanuary1; ++dayIndex, ++dayOfyear)
            {
                // minimum temperature: if temp too low . set prob. to zero
                if (dailyWeather.TemperatureMin[dayIndex] < establishment.MinTemp)
                {
                    this.tacaMinTemp = false;
                }
                // count frost free days
                float minTemperature = dailyWeather.TemperatureMin[dayIndex];
                if (minTemperature > 0.0F)
                {
                    ++frostFreeDays;
                }
                // chilling requirement, GDD, bud burst
                float daytimeMeanTemperature = dailyWeather.TemperatureDaytimeMean[dayIndex];
                if (daytimeMeanTemperature >= -5.0F && daytimeMeanTemperature < 5.0F)
                {
                    ++chillingDays;
                }
                if (chillingDays > establishment.ChillRequirement)
                {
                    chillRequirementSatisfied = true;
                }
                // GDDs above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (dayOfyear <= leafOnEndDay)
                {
                    // accumulate growing degree days
                    if (chillRequirementSatisfied && daytimeMeanTemperature > establishment.GrowingDegreeDaysBaseTemperature)
                    {
                        growingDegreeDays += daytimeMeanTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                        growingDegreeDaysBudBurst += daytimeMeanTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud burst is reset
                    if (daytimeMeanTemperature <= 0.0F)
                    {
                        growingDegreeDaysBudBurst = 0.0F;
                    }
                    if (growingDegreeDaysBudBurst > establishment.GddBudBurst)
                    {
                        budsHaveBurst = true;
                    }
                    if ((dayOfyear < leafOnEndDay) && budsHaveBurst && (minTemperature <= 0.0F))
                    {
                        ++this.tacaFrostDaysAfterBudBurst;
                    }
                }
            }
            // chilling requirement
            if (chillRequirementSatisfied)
            {
                this.tacaChill = true;
            }

            // GDD requirements
            if (growingDegreeDays > establishment.MinimumGrowingDegreeDays && growingDegreeDays < establishment.MaximumGrowingDegreeDays)
            {
                this.tacaGdd = true;
            }

            // frost free days in the vegetation period
            if (frostFreeDays > establishment.MinimumFrostFreeDays)
            {
                this.tacaFrostFree = true;
            }

            // if all requirements are met:
            if (this.tacaChill && this.tacaMinTemp && this.tacaGdd && this.tacaFrostFree)
            {
                // negative effect of frost events after bud birst
                float frostEffect = 1.0F;
                if (this.tacaFrostDaysAfterBudBurst > 0)
                {
                    frostEffect = MathF.Pow(establishment.FrostTolerance, MathF.Sqrt(this.tacaFrostDaysAfterBudBurst));
                }
                // negative effect due to water limitation on establishment [1: no effect]
                this.waterLimitation = Establishment.CalculateWaterLimitation(ruSpecies, phenonology.LeafOnStart, phenonology.GetLeafOnDurationInDays());
                // combine drought and frost effect multiplicatively
                this.AbioticEnvironment = frostEffect * this.waterLimitation;
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
