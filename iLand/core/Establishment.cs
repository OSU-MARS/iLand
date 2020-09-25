using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    /** @class Establishment
    Establishment deals with the establishment process of saplings.
    http://iland.boku.ac.at/establishment
    Prerequisites for establishment are:
    the availability of seeds: derived from the seed-maps per Species (@sa SeedDispersal)
    the quality of the abiotic environment (TACA-model): calculations are performend here, based on climate and species responses
    the quality of the biotic environment, mainly light: based on the LIF-values
    */
    internal class Establishment
    {
        private Climate mClimate; ///< link to the current climate
        private ResourceUnitSpecies mRUS; ///< link to the resource unit species (links to production data and species respones)
        // TACA switches
        private double mSumLIFvalue;
        private int mLIFcount;

        public double MeanSeedDensity { get; private set; } ///< average seed density on the RU
        public double AbioticEnvironment { get; private set; } ///< integrated value of abiotic environment (i.e.: TACA-climate + total iLand environment)
        public int NumberEstablished { get; private set; } ///< return number of newly established trees in the current year
        public bool TacaMinTemp { get; private set; } ///< TACA flag for minimum temperature
        public bool TacaChill { get; private set; } ///< TACA flag chilling requirement
        public bool TacaGdd { get; private set; } ///< TACA flag for growing degree days
        public bool TacaFrostFree { get; private set; } ///< TACA flag for number of frost free days
        public int TacaFrostDaysAfterBudburst { get; private set; } ///< number of frost days after bud birst
        public double WaterLimitation { get; private set; } ///< scalar value between 0 and 1 (1: no limitation, 0: no establishment)

        public Establishment()
        {
            AbioticEnvironment = 0.0;
        }

        public Establishment(Climate climate, ResourceUnitSpecies rus)
        {
            Setup(climate, rus);
        }

        public double MeanLifValue() { return mLIFcount > 0 ? mSumLIFvalue / (double)mLIFcount : 0.0; } ///< average LIF value of LIF pixels where establishment is tested

        public void Setup(Climate climate, ResourceUnitSpecies rus)
        {
            mClimate = climate;
            mRUS = rus;
            AbioticEnvironment = 0.0;
            MeanSeedDensity = 0.0;
            NumberEstablished = 0;
            if (climate == null)
            {
                throw new NotSupportedException("setup: no valid climate for a resource unit.");
            }
            if (rus == null || rus.Species == null || rus.RU == null)
            {
                throw new NotSupportedException("setup: important variable is null (are the species properly set up?).");
            }
        }

        public void Clear()
        {
            AbioticEnvironment = 0.0;
            NumberEstablished = 0;
            MeanSeedDensity = 0.0;
            TacaMinTemp = TacaChill = TacaGdd = TacaFrostFree = false;
            TacaFrostDaysAfterBudburst = 0;
            mSumLIFvalue = 0.0;
            mLIFcount = 0;
            WaterLimitation = 0.0;
        }

        private double CalculateWaterLimitation(int veg_period_start, int veg_period_end)
        {
            // return 1 if effect is disabled
            if (mRUS.Species.EstablishmentParameters.PsiMin >= 0.0)
            {
                return 1.0;
            }

            double psi_min = mRUS.Species.EstablishmentParameters.PsiMin;
            WaterCycle water = mRUS.RU.WaterCycle;
            int days = mRUS.RU.Climate.DaysOfYear();

            // two week (14 days) running average of actual psi-values on the resource unit
            const int nwindow = 14;
            double[] psi_buffer = new double[nwindow];
            double current_sum = 0.0;

            int i_buffer = 0;
            double min_average = 9999999.0;
            for (int day = 0; day < days; ++day)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                current_sum -= psi_buffer[i_buffer];
                psi_buffer[i_buffer] = water.Psi(day);
                current_sum += psi_buffer[i_buffer];

                if (day >= veg_period_start && day <= veg_period_end)
                {
                    double current_avg = day > 0 ? current_sum / Math.Min(day, nwindow) : current_sum;
                    min_average = Math.Min(min_average, current_avg);
                }

                // move to next value in the buffer
                i_buffer = ++i_buffer % nwindow;
            }

            if (min_average > 1000.0)
            {
                return 1.0; // invalid vegetation period?
            }

            // calculate the response of the species to this value of psi (see also Species::soilwaterResponse())
            double psi_mpa = min_average / 1000.0; // convert to MPa
            double result = Global.Limit((psi_mpa - psi_min) / (-0.015 - psi_min), 0.0, 1.0);

            return result;
        }

        /** Calculate the abiotic environemnt for seedling for a given species and a given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: http://iland.boku.ac.at/establishment#abiotic_environment
         a model mockup in R: script_establishment.r

         */
        public void CalculateAbioticEnvironment()
        {
            //DebugTimer t("est_abiotic"); t.setSilent();
            // make sure that required calculations (e.g. watercycle are already performed)
            mRUS.Calculate(true); // calculate the 3pg module and run the water cycle (this is done only if that did not happen up to now); true: call comes from regeneration

            EstablishmentParameters p = mRUS.Species.EstablishmentParameters;
            Phenology pheno = mClimate.Phenology(mRUS.Species.PhenologyClass);

            TacaMinTemp = true; // minimum temperature threshold
            TacaChill = false;  // (total) chilling requirement
            TacaGdd = false;   // gdd-thresholds
            TacaFrostFree = false; // frost free days in vegetation period
            TacaFrostDaysAfterBudburst = 0; // frost days after bud birst

            int doy = 0;
            double GDD = 0.0;
            double GDD_BudBirst = 0.0;
            int chill_days = pheno.ChillingDaysLastYear; // chilling days of the last autumn
            int frost_free = 0;
            TacaFrostDaysAfterBudburst = 0;
            bool chill_ok = false;
            bool buds_are_birst = false;
            int veg_period_end = pheno.LeafOnEnd;
            if (veg_period_end >= 365)
            {
                veg_period_end = mClimate.Sun.LastDayLongerThan10_5Hours;
            }
            for (int index = mClimate.Begin; index != mClimate.End; ++index, ++doy)
            {
                ClimateDay day = mClimate[index];
                // minimum temperature: if temp too low . set prob. to zero
                if (day.MinTemperature < p.MinTemp)
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
                if (chill_days > p.ChillRequirement)
                {
                    chill_ok = true;
                }
                // GDDs above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (doy <= veg_period_end)
                {
                    // accumulate growing degree days
                    if (chill_ok && day.MeanDaytimeTemperature > p.GddBaseTemperature)
                    {
                        GDD += day.MeanDaytimeTemperature - p.GddBaseTemperature;
                        GDD_BudBirst += day.MeanDaytimeTemperature - p.GddBaseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud birst is reset
                    if (day.MeanDaytimeTemperature <= 0.0)
                    {
                        GDD_BudBirst = 0.0;
                    }
                    if (GDD_BudBirst > p.GddBudBurst)
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
            if (GDD > p.GddMin && GDD < p.GddMax)
            {
                TacaGdd = true;
            }

            // frost free days in the vegetation period
            if (frost_free > p.MinFrostFree)
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
                    frost_effect = Math.Pow(p.FrostTolerance, Math.Sqrt(TacaFrostDaysAfterBudburst));
                }
                // negative effect due to water limitation on establishment [1: no effect]
                WaterLimitation = CalculateWaterLimitation(pheno.LeafOnStart, pheno.LeafOnDuration());
                // combine drought and frost effect multiplicatively
                AbioticEnvironment = frost_effect * WaterLimitation;
            }
            else
            {
                AbioticEnvironment = 0.0; // if any of the requirements is not met
            }
        }

        public void WriteDebugOutputs()
        {
            if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.Establishment))
            {
                List<object> output = GlobalSettings.Instance.DebugList(mRUS.RU.Index, DebugOutputs.Establishment);
                // establishment details
                output.AddRange(new object[] { mRUS.Species.ID,  mRUS.RU.Index, mRUS.RU.ID,
                                               MeanSeedDensity, TacaMinTemp, TacaChill, TacaFrostFree, TacaGdd,
                                               TacaFrostDaysAfterBudburst, WaterLimitation, AbioticEnvironment,
                                               mRUS.BiomassGrowth.EnvironmentalFactor, mRUS.SaplingStats.NewSaplings
                                               //mSaplingStat.livingSaplings(), mSaplingStat.averageHeight(), mSaplingStat.averageAge(), mSaplingStat.averageDeltaHPot(), mSaplingStat.averageDeltaHRealized();
                                               //mSaplingStat.newSaplings(), mSaplingStat.diedSaplings(), mSaplingStat.recruitedSaplings(), mSpecies.saplingGrowthParameters().referenceRatio;
                });
            }

            if (GlobalSettings.Instance.LogDebug())
            {
                Debug.WriteLine("establishment of RU " + mRUS.RU.Index + " species " + mRUS.Species.ID +
                                " seeds density :" + MeanSeedDensity + 
                                " abiotic environment: " + AbioticEnvironment +
                                " f_env,yr: " + mRUS.BiomassGrowth.EnvironmentalFactor + 
                                " N(established):"  + NumberEstablished);
            }
        }
    }
}
