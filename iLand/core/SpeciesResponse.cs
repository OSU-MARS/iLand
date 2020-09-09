using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.core
{
    /** @class SpeciesResponse
        @ingroup core
        Environmental responses relevant for production of a tree species on resource unit level.
        SpeciesResponse combines data from different sources and converts information about the environment
        into responses of a species. The spatial level is the "ResourceUnit", where homogenetiy of environmental factors
        is assumed. The temporal aggregation depends on the factor, but usually, the daily environmental data is
        aggregated to monthly response values (which subsequently are used during 3PG production).
        Sources are:
        - vapour pressure deficit (dryness of atmosphere): directly from climate data (daily)
        - soil water status (dryness of soil)(daily)
        - temperature: directly from climate data (daily)
        - phenology: @sa Phenology, combines several sources (quasi-monthly)
        - CO2: @sa SpeciesSet::co2Response() based on ambient CO2 level (climate data), nitrogen and soil water responses (yearly)
        - nitrogen: based on the amount of available nitrogen (yearly)
        */
    internal class SpeciesResponse
    {
        private ResourceUnit mRu;
        private Species mSpecies;

        private double[] mRadiation; ///<  radiation sums per month (MJ/m2)
        private double[] mUtilizableRadiation; ///< sum of daily radiation*minResponse (MJ/m2)
        private double[] mTempResponse; ///< average of temperature response
        private double[] mSoilWaterResponse; ///< average of soilwater response
        private double[] mVpdResponse; ///< mean of vpd-response
        private double mNitrogenResponse;
        private double[] mCO2Response;
        private double mTotalRadiation;  ///< total radiation of the year (MJ/m2)
        private double mTotalUtilizeableRadiation; ///< yearly sum of utilized radiation (MJ/m2)

        // access components
        public Species species() { return mSpecies; }
        public ResourceUnit resourceUnit() { return mRu; }
        // access responses
        public double[] tempResponse() { return mTempResponse; }
        public double[] soilWaterResponse() { return mSoilWaterResponse; }
        public double[] globalRadiation() { return mRadiation; } ///< radiation sum in MJ/m2
        public double[] utilizableRadiation() { return mUtilizableRadiation; } ///< utilizable radiation (rad*responses)
        public double[] vpdResponse() { return mVpdResponse; }
        public double[] co2Response() { return mCO2Response; }
        public double nitrogenResponse() { return mNitrogenResponse; }
        public double yearlyRadiation() { return mTotalRadiation; }
        public double totalUtilizeableRadiation() { return mTotalUtilizeableRadiation; }

        public SpeciesResponse()
        {
            mTempResponse = new double[12];
            mSoilWaterResponse = new double[12];
            mRadiation = new double[12];
            mUtilizableRadiation = new double[12];
            mVpdResponse = new double[12];
            mCO2Response = new double[12];
            mSpecies = null;
            mRu = null;
        }

        public void clear()
        {
            for (int i = 0; i < 12; i++)
            {
                mCO2Response[i] = mSoilWaterResponse[i] = mTempResponse[i] = mRadiation[i] = mUtilizableRadiation[i] = mVpdResponse[i] = 0.0;
            }
            mNitrogenResponse = 0.0;
            mTotalRadiation = 0.0;
            mTotalUtilizeableRadiation = 0.0;
        }

        public void setup(ResourceUnitSpecies rus)
        {
            mSpecies = rus.species();
            mRu = rus.ru();
            clear();
        }

        /// response calculation called during water cycle
        /// calculates minimum-response of vpd-response and soilwater response
        /// calculate responses for VPD and Soil Water. Return the minimum of those responses
        /// @param psi_kPa psi of the soil in kPa
        /// @param vpd vapor pressure deficit in kPa
        /// @return minimum of soil water and vpd response
        public void soilAtmosphereResponses(double psi_kPa, double vpd, out double rMinResponse)
        {
            double water_resp = mSpecies.soilwaterResponse(psi_kPa);
            double vpd_resp = mSpecies.vpdResponse(vpd);
            rMinResponse = Math.Min(water_resp, vpd_resp);
        }

        /// Main function that calculates monthly / annual species responses
        public void calculate()
        {
            using DebugTimer tpg = new DebugTimer("calculate");
            clear(); // reset values

            // calculate yearly responses
            WaterCycle water = mRu.waterCycle();
            Phenology pheno = mRu.climate().phenology(mSpecies.phenologyClass());
            int veg_begin = pheno.vegetationPeriodStart();
            int veg_end = pheno.vegetationPeriodEnd();

            // yearly response
            double nitrogen = mRu.resouceUnitVariables().nitrogenAvailable;
            // Nitrogen response: a yearly value based on available nitrogen
            mNitrogenResponse = mSpecies.nitrogenResponse(nitrogen);
            double ambient_co2 = ClimateDay.co2; // CO2 level of first day of year (co2 is static)

            double water_resp, vpd_resp, temp_resp, min_resp;
            double utilizeable_radiation;
            int doy = 0;
            int month;
            for (int index = mRu.climate().begin(); index != mRu.climate().end(); ++index)
            {
                ClimateDay day = mRu.climate()[index];
                month = day.month - 1;
                // environmental responses
                water_resp = mSpecies.soilwaterResponse(water.psi_kPa(doy));
                vpd_resp = mSpecies.vpdResponse(day.vpd);
                temp_resp = mSpecies.temperatureResponse(day.temp_delayed);
                mSoilWaterResponse[month] += water_resp;
                mTempResponse[month] += temp_resp;
                mVpdResponse[month] += vpd_resp;
                mRadiation[month] += day.radiation;

                if (doy >= veg_begin && doy <= veg_end)
                {
                    // environmental responses for the day
                    // combine responses
                    min_resp = Math.Min(Math.Min(vpd_resp, temp_resp), water_resp);
                    // calculate utilizable radiation, Eq. 4, http://iland.boku.ac.at/primary+production
                    utilizeable_radiation = day.radiation * min_resp;
                }
                else
                {
                    utilizeable_radiation = 0.0; // no utilizable radiation outside of vegetation period
                    min_resp = 0.0;
                }
                mUtilizableRadiation[month] += utilizeable_radiation;
                doy++;
                //DBGMODE(
                if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dDailyResponses))
                {
                    List<object> output = GlobalSettings.instance().debugList(day.id(), DebugOutputs.dDailyResponses);
                    // climatic variables
                    output.AddRange(new object[] { mSpecies.id(), day.id(), mRu.index(), mRu.id() }); // date, day.temperature, day.vpd, day.preciptitation, day.radiation;
                    output.AddRange(new object[] { water_resp, temp_resp, vpd_resp, day.radiation, utilizeable_radiation });
                }
                //); // DBGMODE()

            }
            mTotalRadiation = mRu.climate().totalRadiation();
            // monthly values
            for (int i = 0; i < 12; i++)
            {
                double days = mRu.climate().days(i);
                mTotalUtilizeableRadiation += mUtilizableRadiation[i];
                mSoilWaterResponse[i] /= days;
                mTempResponse[i] /= days;
                mVpdResponse[i] /= days;
                mCO2Response[i] = mSpecies.speciesSet().co2Response(ambient_co2,
                                                                   mNitrogenResponse,
                                                                   mSoilWaterResponse[i]);
            }

        }
    }
}
