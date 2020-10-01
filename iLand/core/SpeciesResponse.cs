using iLand.Tools;
using System;
using System.Collections.Generic;

namespace iLand.Core
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
    public class SpeciesResponse
    {
        public Species Species { get; private set; }
        public ResourceUnit ResourceUnit { get; private set; }

        public double[] TempResponse { get; private set; } ///< average of temperature response
        public double[] SoilWaterResponse { get; private set; } ///< average of soilwater response
        public double[] GlobalRadiation { get; private set; } ///< radiation sum in MJ/m2
        public double[] UtilizableRadiation { get; private set; } ///< sum of daily radiation*minResponse (MJ/m2)
        public double[] VpdResponse { get; private set; } ///< mean of vpd-response
        public double[] Co2Response { get; private set; }
        public double NitrogenResponse { get; private set; }
        public double YearlyRadiation { get; private set; } ///< total radiation of the year (MJ/m2)
        public double YearlyUtilizableRadiation { get; private set; } ///< yearly sum of utilized radiation (MJ/m2)

        public SpeciesResponse()
        {
            TempResponse = new double[12];
            SoilWaterResponse = new double[12];
            GlobalRadiation = new double[12];
            UtilizableRadiation = new double[12];
            VpdResponse = new double[12];
            Co2Response = new double[12];
            Species = null;
            ResourceUnit = null;
        }

        public void Clear()
        {
            for (int i = 0; i < 12; i++)
            {
                Co2Response[i] = SoilWaterResponse[i] = TempResponse[i] = GlobalRadiation[i] = UtilizableRadiation[i] = VpdResponse[i] = 0.0;
            }
            NitrogenResponse = 0.0;
            YearlyRadiation = 0.0;
            YearlyUtilizableRadiation = 0.0;
        }

        public void Setup(ResourceUnitSpecies rus)
        {
            Species = rus.Species;
            ResourceUnit = rus.RU;
            Clear();
        }

        /// response calculation called during water cycle
        /// calculates minimum-response of vpd-response and soilwater response
        /// calculate responses for VPD and Soil Water. Return the minimum of those responses
        /// @param psi_kPa psi of the soil in kPa
        /// @param vpd vapor pressure deficit in kPa
        /// @return minimum of soil water and vpd response
        public void SoilAtmosphereResponses(double psi_kPa, double vpd, out double rMinResponse)
        {
            double water_resp = Species.SoilwaterResponse(psi_kPa);
            double vpd_resp = Species.VpdResponse(vpd);
            rMinResponse = Math.Min(water_resp, vpd_resp);
        }

        /// Main function that calculates monthly / annual species responses
        public void Calculate()
        {
            using DebugTimer tpg = new DebugTimer("SpeciesResponse.Calculate()");
            Clear(); // reset values

            // calculate yearly responses
            WaterCycle water = ResourceUnit.WaterCycle;
            Phenology pheno = ResourceUnit.Climate.Phenology(Species.PhenologyClass);
            int veg_begin = pheno.LeafOnStart;
            int veg_end = pheno.LeafOnEnd;

            // yearly response
            double nitrogen = ResourceUnit.Variables.NitrogenAvailable;
            // Nitrogen response: a yearly value based on available nitrogen
            NitrogenResponse = Species.GetNitrogenResponse(nitrogen);
            double ambient_co2 = ClimateDay.CarbonDioxidePpm; // CO2 level of first day of year (co2 is static)

            double water_resp, vpd_resp, temp_resp, min_resp;
            double utilizeable_radiation;
            int doy = 0;
            int month;
            for (int index = ResourceUnit.Climate.Begin; index != ResourceUnit.Climate.End; ++index)
            {
                ClimateDay day = ResourceUnit.Climate[index];
                month = day.Month - 1;
                // environmental responses
                water_resp = Species.SoilwaterResponse(water.Psi(doy));
                vpd_resp = Species.VpdResponse(day.Vpd);
                temp_resp = Species.TemperatureResponse(day.TempDelayed);
                SoilWaterResponse[month] += water_resp;
                TempResponse[month] += temp_resp;
                VpdResponse[month] += vpd_resp;
                GlobalRadiation[month] += day.Radiation;

                if (doy >= veg_begin && doy <= veg_end)
                {
                    // environmental responses for the day
                    // combine responses
                    min_resp = Math.Min(Math.Min(vpd_resp, temp_resp), water_resp);
                    // calculate utilizable radiation, Eq. 4, http://iland.boku.ac.at/primary+production
                    utilizeable_radiation = day.Radiation * min_resp;
                }
                else
                {
                    utilizeable_radiation = 0.0; // no utilizable radiation outside of vegetation period
                    min_resp = 0.0;
                }
                UtilizableRadiation[month] += utilizeable_radiation;
                doy++;
                //DBGMODE(
                if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.DailyResponses))
                {
                    List<object> output = GlobalSettings.Instance.DebugList(day.ID(), DebugOutputs.DailyResponses);
                    // climatic variables
                    output.AddRange(new object[] { Species.ID, day.ID(), ResourceUnit.Index, ResourceUnit.ID }); // date, day.temperature, day.vpd, day.preciptitation, day.radiation;
                    output.AddRange(new object[] { water_resp, temp_resp, vpd_resp, day.Radiation, utilizeable_radiation });
                }
                //); // DBGMODE()

            }
            YearlyRadiation = ResourceUnit.Climate.TotalRadiation;
            // monthly values
            for (int i = 0; i < 12; i++)
            {
                double days = ResourceUnit.Climate.Days(i);
                YearlyUtilizableRadiation += UtilizableRadiation[i];
                SoilWaterResponse[i] /= days;
                TempResponse[i] /= days;
                VpdResponse[i] /= days;
                Co2Response[i] = Species.SpeciesSet.CarbonDioxideResponse(ambient_co2,
                                                                   NitrogenResponse,
                                                                   SoilWaterResponse[i]);
            }

        }
    }
}
