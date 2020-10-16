using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Trees
{
    /** @class SpeciesResponse
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

        public double[] TempResponse { get; private set; } // average of temperature response
        public double[] SoilWaterResponse { get; private set; } // average of soilwater response
        public double[] GlobalRadiation { get; private set; } // radiation sum in MJ/m2
        public double[] UtilizableRadiation { get; private set; } // sum of daily radiation*minResponse (MJ/m2)
        public double[] VpdResponse { get; private set; } // mean of vpd-response
        public double[] Co2Response { get; private set; }
        public double NitrogenResponse { get; private set; }
        public double YearlyRadiation { get; private set; } // total radiation of the year (MJ/m2)
        public double YearlyUtilizableRadiation { get; private set; } // yearly sum of utilized radiation (MJ/m2)

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
            for (int month = 0; month < 12; ++month)
            {
                Co2Response[month] = SoilWaterResponse[month] = TempResponse[month] = GlobalRadiation[month] = UtilizableRadiation[month] = VpdResponse[month] = 0.0;
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
            double water_resp = Species.SoilWaterResponse(psi_kPa);
            double vpd_resp = Species.VpdResponse(vpd);
            rMinResponse = Math.Min(water_resp, vpd_resp);
        }

        /// Main function that calculates monthly / annual species responses
        public void Calculate(Climate climate)
        {
            //using DebugTimer tpg = model.DebugTimers.Create("SpeciesResponse.Calculate()");
            Clear(); // reset values

            // calculate yearly responses
            WaterCycle water = ResourceUnit.WaterCycle;
            Phenology phenonology = ResourceUnit.Climate.Phenology(Species.PhenologyClass);
            int leafOnIndex = phenonology.LeafOnStart;
            int leafOffIndex = phenonology.LeafOnEnd;

            // nitrogen response: a yearly value based on available nitrogen
            this.NitrogenResponse = Species.GetNitrogenResponse(this.ResourceUnit.Soil.PlantAvailableNitrogen);
            Debug.Assert(this.NitrogenResponse >= 0.0);
            double ambientCo2 = climate.CarbonDioxidePpm; // CO2 level of first day of year (co2 is static)

            int dayOfYear = 0;
            for (int dayIndex = ResourceUnit.Climate.CurrentJanuary1; dayIndex < ResourceUnit.Climate.NextJanuary1; ++dayIndex, ++dayOfYear)
            {
                ClimateDay day = ResourceUnit.Climate[dayIndex];
                int monthIndex = day.Month - 1;
                // environmental responses
                this.GlobalRadiation[monthIndex] += day.Radiation;

                double soilWaterResponse = Species.SoilWaterResponse(water.Psi[dayOfYear]);
                this.SoilWaterResponse[monthIndex] += soilWaterResponse;
                
                double tempResponse = Species.TemperatureResponse(day.TempDelayed);
                this.TempResponse[monthIndex] += tempResponse;
                
                double vpdResponse = Species.VpdResponse(day.Vpd);
                this.VpdResponse[monthIndex] += vpdResponse;

                double utilizableRadiation;
                if (dayOfYear >= leafOnIndex && dayOfYear <= leafOffIndex)
                {
                    // environmental responses for the day
                    // combine responses
                    double minimumResponse = Math.Min(Math.Min(vpdResponse, tempResponse), soilWaterResponse);
                    // calculate utilizable radiation, Eq. 4, http://iland.boku.ac.at/primary+production
                    utilizableRadiation = day.Radiation * minimumResponse;
                    Debug.Assert(minimumResponse >= 0.0);
                }
                else
                {
                    utilizableRadiation = 0.0; // no utilizable radiation outside of vegetation period
                }
                UtilizableRadiation[monthIndex] += utilizableRadiation;
                //DBGMODE(
                //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.DailyResponses))
                //{
                //    List<object> output = GlobalSettings.Instance.DebugList(day.ID(), DebugOutputs.DailyResponses);
                //    // climatic variables
                //    output.AddRange(new object[] { Species.ID, day.ID(), ResourceUnit.Index, ResourceUnit.ID }); // date, day.temperature, day.vpd, day.preciptitation, day.radiation;
                //    output.AddRange(new object[] { water_resp, temp_resp, vpd_resp, day.Radiation, utilizeable_radiation });
                //}
                //); // DBGMODE()
            }

            // monthly values
            for (int monthIndex = 0; monthIndex < 12; monthIndex++)
            {
                double daysInMonth = ResourceUnit.Climate.GetDaysInMonth(monthIndex);
                this.Co2Response[monthIndex] = Species.SpeciesSet.CarbonDioxideResponse(ambientCo2, NitrogenResponse, SoilWaterResponse[monthIndex]);
                this.SoilWaterResponse[monthIndex] /= daysInMonth;
                this.TempResponse[monthIndex] /= daysInMonth;
                this.VpdResponse[monthIndex] /= daysInMonth;
                this.YearlyUtilizableRadiation += this.UtilizableRadiation[monthIndex];
                Debug.Assert(Co2Response[monthIndex] > 0.0);
            }
            this.YearlyRadiation = this.ResourceUnit.Climate.TotalAnnualRadiation;
        }
    }
}
