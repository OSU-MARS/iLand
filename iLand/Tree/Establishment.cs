using iLand.Input.ProjectFile;
using iLand.Simulation;
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
        */
    public class Establishment
    {
        private readonly World.Climate mClimate; // link to the current climate
        private readonly ResourceUnitTreeSpecies mRUspecies; // link to the resource unit species (links to production data and species respones)
        // TACA switches
        private double mSumLifValue;
        private int mLifCount;

        public double MeanSeedDensity { get; private set; } // average seed density on the RU
        public double AbioticEnvironment { get; private set; } // integrated value of abiotic environment (i.e.: TACA-climate + total iLand environment)
        public int NumberEstablished { get; private set; } // return number of newly established trees in the current year
        public bool TacaMinTemp { get; private set; } // TACA flag for minimum temperature
        public bool TacaChill { get; private set; } // TACA flag chilling requirement
        public bool TacaGdd { get; private set; } // TACA flag for growing degree days
        public bool TacaFrostFree { get; private set; } // TACA flag for number of frost free days
        public int TacaFrostDaysAfterBudburst { get; private set; } // number of frost days after bud birst
        public double WaterLimitation { get; private set; } // scalar value between 0 and 1 (1: no limitation, 0: no establishment)

        public Establishment(World.Climate climate, ResourceUnitTreeSpecies ruSpecies)
        {
            if (ruSpecies == null || ruSpecies.Species == null || ruSpecies.RU == null)
            {
                throw new ArgumentException("Species or resource unit is null");
            }

            this.mClimate = climate ?? throw new ArgumentNullException(nameof(climate), "No valid climate for a resource unit.");
            this.mRUspecies = ruSpecies;
            this.AbioticEnvironment = 0.0;
            this.MeanSeedDensity = 0.0;
            this.NumberEstablished = 0;
        }

        public double GetMeanLifValue() { return this.mLifCount > 0 ? this.mSumLifValue / this.mLifCount : 0.0; } // average LIF value of LIF pixels where establishment is tested

        public void Clear()
        {
            this.mLifCount = 0;
            this.mSumLifValue = 0.0;

            this.AbioticEnvironment = 0.0;
            this.NumberEstablished = 0;
            this.MeanSeedDensity = 0.0;
            this.TacaMinTemp = false;
            this.TacaChill = false;
            this.TacaGdd = false;
            this.TacaFrostFree = false;
            this.TacaFrostDaysAfterBudburst = 0;
            this.WaterLimitation = 0.0;
        }

        private double CalculateWaterLimitation(int leafOnStart, int leafOnEnd)
        {
            // return 1 if effect is disabled
            if (mRUspecies.Species.EstablishmentParameters.PsiMin >= 0.0)
            {
                return 1.0;
            }

            double psi_min = mRUspecies.Species.EstablishmentParameters.PsiMin;
            WaterCycle water = mRUspecies.RU.WaterCycle;

            // two week (14 days) running average of actual psi-values on the resource unit
            const int valuesToAverage = 14;
            double[] valuesInAverage = new double[valuesToAverage];
            double current_sum = 0.0;
            
            double min_average = 9999999.0;
            int daysInYear = mRUspecies.RU.Climate.GetDaysInYear();
            for (int dayOfYear = 0, averageIndex = 0; dayOfYear < daysInYear; ++dayOfYear)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                current_sum -= valuesInAverage[averageIndex];
                valuesInAverage[averageIndex] = water.SoilWaterPsi[dayOfYear];
                current_sum += valuesInAverage[averageIndex];

                if (dayOfYear >= leafOnStart && dayOfYear <= leafOnEnd)
                {
                    double current_avg = dayOfYear > 0 ? current_sum / Math.Min(dayOfYear, valuesToAverage) : current_sum;
                    min_average = Math.Min(min_average, current_avg);
                }

                // move to next value in the buffer
                averageIndex = ++averageIndex % valuesToAverage;
            }

            if (min_average > 1000.0)
            {
                return 1.0; // invalid vegetation period?
            }

            // calculate the response of the species to this value of psi (see also Species::soilwaterResponse())
            double psi_mpa = min_average / 1000.0; // convert to MPa
            double result = Maths.Limit((psi_mpa - psi_min) / (-0.015 - psi_min), 0.0, 1.0);

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

            TacaMinTemp = true; // minimum temperature threshold
            TacaChill = false;  // (total) chilling requirement
            TacaGdd = false;   // gdd-thresholds
            TacaFrostFree = false; // frost free days in vegetation period
            TacaFrostDaysAfterBudburst = 0; // frost days after bud birst

            int doy = 0;
            double GDD = 0.0;
            double GDD_BudBirst = 0.0;
            int chill_days = pheno.ChillingDaysAfterLeafOffInPreviousYear; // chilling days of the last autumn
            int frost_free = 0;
            TacaFrostDaysAfterBudburst = 0;
            bool chill_ok = false;
            bool buds_are_birst = false;
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
                    TacaMinTemp = false;
                }
                // count frost free days
                if (day.MinTemperature > 0.0)
                {
                    frost_free++;
                }
                // chilling requirement, GDD, bud birst
                if (day.MeanDaytimeTemperature >= -5.0 && day.MeanDaytimeTemperature < 5.0)
                {
                    chill_days++;
                }
                if (chill_days > establishment.ChillRequirement)
                {
                    chill_ok = true;
                }
                // GDDs above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (doy <= veg_period_end)
                {
                    // accumulate growing degree days
                    if (chill_ok && day.MeanDaytimeTemperature > establishment.GddBaseTemperature)
                    {
                        GDD += day.MeanDaytimeTemperature - establishment.GddBaseTemperature;
                        GDD_BudBirst += day.MeanDaytimeTemperature - establishment.GddBaseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud birst is reset
                    if (day.MeanDaytimeTemperature <= 0.0)
                    {
                        GDD_BudBirst = 0.0;
                    }
                    if (GDD_BudBirst > establishment.GddBudBurst)
                    {
                        buds_are_birst = true;
                    }
                    if (doy < veg_period_end && buds_are_birst && day.MinTemperature <= 0.0)
                    {
                        TacaFrostDaysAfterBudburst++;
                    }
                }
            }
            // chilling requirement
            if (chill_ok)
            {
                TacaChill = true;
            }

            // GDD requirements
            if (GDD > establishment.GddMin && GDD < establishment.GddMax)
            {
                TacaGdd = true;
            }

            // frost free days in the vegetation period
            if (frost_free > establishment.MinFrostFree)
            {
                TacaFrostFree = true;
            }

            // if all requirements are met:
            if (TacaChill && TacaMinTemp && TacaGdd && TacaFrostFree)
            {
                // negative effect of frost events after bud birst
                double frost_effect = 1.0;
                if (TacaFrostDaysAfterBudburst > 0)
                {
                    frost_effect = Math.Pow(establishment.FrostTolerance, Math.Sqrt(TacaFrostDaysAfterBudburst));
                }
                // negative effect due to water limitation on establishment [1: no effect]
                WaterLimitation = CalculateWaterLimitation(pheno.LeafOnStart, pheno.GetLeafOnDurationInDays());
                // combine drought and frost effect multiplicatively
                AbioticEnvironment = frost_effect * WaterLimitation;
            }
            else
            {
                AbioticEnvironment = 0.0; // if any of the requirements is not met
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
