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
        private double mPAbiotic; ///< abiotic probability for establishment (climate)
        private Climate mClimate; ///< link to the current climate
        private ResourceUnitSpecies mRUS; ///< link to the resource unit species (links to production data and species respones)
        // some statistics
        private double mPxDensity;
        private int mNumberEstablished; // number of established trees in the current year
        // TACA switches
        private bool mTACA_min_temp; // minimum temperature threshold
        private bool mTACA_chill;  // (total) chilling requirement
        private bool mTACA_gdd;   // gdd-thresholds
        private bool mTACA_frostfree; // frost free days in vegetation period
        private int mTACA_frostAfterBuds; // frost days after bud birst
        private double mSumLIFvalue;
        private double mWaterLimitation; // scalar 0..1 signifying the drought limitation of establishment
        private int mLIFcount;

        public double avgSeedDensity() { return mPxDensity; } ///< average seed density on the RU
        public double abioticEnvironment() { return mPAbiotic; } ///< integrated value of abiotic environment (i.e.: TACA-climate + total iLand environment)
        public int numberEstablished() { return mNumberEstablished; } ///< return number of newly established trees in the current year
        public bool TACAminTemp() { return mTACA_min_temp; } ///< TACA flag for minimum temperature
        public bool TACAchill() { return mTACA_chill; } ///< TACA flag chilling requirement
        public bool TACgdd() { return mTACA_gdd; } ///< TACA flag for growing degree days
        public bool TACAfrostFree() { return mTACA_frostfree; } ///< TACA flag for number of frost free days
        public int TACAfrostDaysAfterBudBirst() { return mTACA_frostAfterBuds; } ///< number of frost days after bud birst
        public double avgLIFValue() { return mLIFcount > 0 ? mSumLIFvalue / (double)mLIFcount : 0.0; } ///< average LIF value of LIF pixels where establishment is tested
        public double waterLimitation() { return mWaterLimitation; } ///< scalar value between 0 and 1 (1: no limitation, 0: no establishment)

        public Establishment()
        {
            mPAbiotic = 0.0;
        }

        public Establishment(Climate climate, ResourceUnitSpecies rus)
        {
            setup(climate, rus);
        }

        public void setup(Climate climate, ResourceUnitSpecies rus)
        {
            mClimate = climate;
            mRUS = rus;
            mPAbiotic = 0.0;
            mPxDensity = 0.0;
            mNumberEstablished = 0;
            if (climate == null)
            {
                throw new NotSupportedException("setup: no valid climate for a resource unit.");
            }
            if (rus == null || rus.species() == null || rus.ru() == null)
            {
                throw new NotSupportedException("setup: important variable is null (are the species properly set up?).");
            }
        }

        public void clear()
        {
            mPAbiotic = 0.0;
            mNumberEstablished = 0;
            mPxDensity = 0.0;
            mTACA_min_temp = mTACA_chill = mTACA_gdd = mTACA_frostfree = false;
            mTACA_frostAfterBuds = 0;
            mSumLIFvalue = 0.0;
            mLIFcount = 0;
            mWaterLimitation = 0.0;
        }


        private double calculateWaterLimitation(int veg_period_start, int veg_period_end)
        {
            // return 1 if effect is disabled
            if (mRUS.species().establishmentParameters().psi_min >= 0.0)
            {
                return 1.0;
            }

            double psi_min = mRUS.species().establishmentParameters().psi_min;
            WaterCycle water = mRUS.ru().waterCycle();
            int days = mRUS.ru().climate().daysOfYear();

            // two week (14 days) running average of actual psi-values on the resource unit
            const int nwindow = 14;
            double[] psi_buffer = new double[nwindow];
            double current_sum = 0.0;

            int i_buffer = 0;
            double min_average = 9999999.0;
            double current_avg = 0.0;
            for (int day = 0; day < days; ++day)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                current_sum -= psi_buffer[i_buffer];
                psi_buffer[i_buffer] = water.psi_kPa(day);
                current_sum += psi_buffer[i_buffer];

                if (day >= veg_period_start && day <= veg_period_end)
                {
                    current_avg = day > 0 ? current_sum / Math.Min(day, nwindow) : current_sum;
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
            double result = Global.limit((psi_mpa - psi_min) / (-0.015 - psi_min), 0.0, 1.0);

            return result;
        }



        /** Calculate the abiotic environemnt for seedling for a given species and a given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: http://iland.boku.ac.at/establishment#abiotic_environment
         a model mockup in R: script_establishment.r

         */
        public void calculateAbioticEnvironment()
        {
            //DebugTimer t("est_abiotic"); t.setSilent();
            // make sure that required calculations (e.g. watercycle are already performed)
            mRUS.calculate(true); // calculate the 3pg module and run the water cycle (this is done only if that did not happen up to now); true: call comes from regeneration

            EstablishmentParameters p = mRUS.species().establishmentParameters();
            Phenology pheno = mClimate.phenology(mRUS.species().phenologyClass());

            mTACA_min_temp = true; // minimum temperature threshold
            mTACA_chill = false;  // (total) chilling requirement
            mTACA_gdd = false;   // gdd-thresholds
            mTACA_frostfree = false; // frost free days in vegetation period
            mTACA_frostAfterBuds = 0; // frost days after bud birst

            int doy = 0;
            double GDD = 0.0;
            double GDD_BudBirst = 0.0;
            int chill_days = pheno.chillingDaysLastYear(); // chilling days of the last autumn
            int frost_free = 0;
            mTACA_frostAfterBuds = 0;
            bool chill_ok = false;
            bool buds_are_birst = false;
            int veg_period_end = pheno.vegetationPeriodEnd();
            if (veg_period_end >= 365)
            {
                veg_period_end = mClimate.sun().dayShorter10_5hrs();
            }
            for (int index = mClimate.begin(); index != mClimate.end(); ++index, ++doy)
            {
                ClimateDay day = mClimate[index];
                // minimum temperature: if temp too low . set prob. to zero
                if (day.min_temperature < p.min_temp)
                {
                    mTACA_min_temp = false;
                }
                // count frost free days
                if (day.min_temperature > 0.0)
                {
                    frost_free++;
                }
                // chilling requirement, GDD, bud birst
                if (day.temperature >= -5.0 && day.temperature < 5.0)
                {
                    chill_days++;
                }
                if (chill_days > p.chill_requirement)
                {
                    chill_ok = true;
                }
                // GDDs above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (doy <= veg_period_end)
                {
                    // accumulate growing degree days
                    if (chill_ok && day.temperature > p.GDD_baseTemperature)
                    {
                        GDD += day.temperature - p.GDD_baseTemperature;
                        GDD_BudBirst += day.temperature - p.GDD_baseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud birst is reset
                    if (day.temperature <= 0.0)
                    {
                        GDD_BudBirst = 0.0;
                    }
                    if (GDD_BudBirst > p.bud_birst)
                    {
                        buds_are_birst = true;
                    }
                    if (doy < veg_period_end && buds_are_birst && day.min_temperature <= 0.0)
                    {
                        mTACA_frostAfterBuds++;
                    }
                }
            }
            // chilling requirement
            if (chill_ok)
            {
                mTACA_chill = true;
            }

            // GDD requirements
            if (GDD > p.GDD_min && GDD < p.GDD_max)
            {
                mTACA_gdd = true;
            }

            // frost free days in the vegetation period
            if (frost_free > p.frost_free)
            {
                mTACA_frostfree = true;
            }

            // if all requirements are met:
            if (mTACA_chill && mTACA_min_temp && mTACA_gdd && mTACA_frostfree)
            {
                // negative effect of frost events after bud birst
                double frost_effect = 1.0;
                if (mTACA_frostAfterBuds > 0)
                {
                    frost_effect = Math.Pow(p.frost_tolerance, Math.Sqrt(mTACA_frostAfterBuds));
                }
                // negative effect due to water limitation on establishment [1: no effect]
                mWaterLimitation = calculateWaterLimitation(pheno.vegetationPeriodStart(), pheno.vegetationPeriodLength());
                // combine drought and frost effect multiplicatively
                mPAbiotic = frost_effect * mWaterLimitation;
            }
            else
            {
                mPAbiotic = 0.0; // if any of the requirements is not met
            }
        }

        public void writeDebugOutputs()
        {
            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dEstablishment))
            {
                List<object> output = GlobalSettings.instance().debugList(mRUS.ru().index(), DebugOutputs.dEstablishment);
                // establishment details
                output.AddRange(new object[] { mRUS.species().id(),  mRUS.ru().index(), mRUS.ru().id(),
                                               avgSeedDensity(),
                                               TACAminTemp(), TACAchill(), TACAfrostFree(), TACgdd(),
                                               TACAfrostDaysAfterBudBirst(), waterLimitation(), abioticEnvironment(),
                                               mRUS.prod3PG().fEnvYear(), mRUS.constSaplingStat().newSaplings()
                                               //mSaplingStat.livingSaplings(), mSaplingStat.averageHeight(), mSaplingStat.averageAge(), mSaplingStat.averageDeltaHPot(), mSaplingStat.averageDeltaHRealized();
                                               //mSaplingStat.newSaplings(), mSaplingStat.diedSaplings(), mSaplingStat.recruitedSaplings(), mSpecies.saplingGrowthParameters().referenceRatio;
                });
            }

            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("establishment of RU " + mRUS.ru().index() + " species " + mRUS.species().id()
                         + " seeds density :" + avgSeedDensity()
                         + " abiotic environment: " + abioticEnvironment()
                         + " f_env,yr: " + mRUS.prod3PG().fEnvYear()
                         + " N(established):"  + numberEstablished());
            }
        }
    }
}
