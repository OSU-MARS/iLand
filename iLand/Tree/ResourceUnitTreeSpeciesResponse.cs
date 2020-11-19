using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
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
    public class ResourceUnitTreeSpeciesResponse
    {
        public ResourceUnit ResourceUnit { get; private init; }
        public TreeSpecies Species { get; private init; }

        public float[] CO2ResponseByMonth { get; private init; }
        public float[] GlobalRadiationByMonth { get; private init; } // radiation sum in MJ/m2
        public float[] SoilWaterResponseByMonth { get; private init; } // average of soilwater response
        public float NitrogenResponseForYear { get; private set; }
        public float RadiationForYear { get; private set; } // total radiation of the year (MJ/m2)
        public float[] TempResponseByMonth { get; private init; } // average of temperature response
        public float[] UtilizableRadiationByMonth { get; private init; } // sum of daily radiation*minResponse (MJ/m2)
        public float UtilizableRadiationForYear { get; private set; } // yearly sum of utilized radiation (MJ/m2)
        public float[] VpdResponseByMonth { get; private init; } // mean of vpd-response

        public ResourceUnitTreeSpeciesResponse(ResourceUnit ru, ResourceUnitTreeSpecies ruSpecies)
        {
            this.Species = ruSpecies.Species;
            this.ResourceUnit = ru;

            this.CO2ResponseByMonth = new float[Constant.MonthsInYear];
            this.GlobalRadiationByMonth = new float[Constant.MonthsInYear];
            this.NitrogenResponseForYear = 0.0F;
            this.RadiationForYear = 0.0F;
            this.SoilWaterResponseByMonth = new float[Constant.MonthsInYear];
            this.TempResponseByMonth = new float[Constant.MonthsInYear];
            this.UtilizableRadiationByMonth = new float[Constant.MonthsInYear];
            this.UtilizableRadiationForYear = 0.0F;
            this.VpdResponseByMonth = new float[Constant.MonthsInYear];

            // this.Zero();
        }

        public void Zero()
        {
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.CO2ResponseByMonth[month] = 0.0F;
                this.SoilWaterResponseByMonth[month] = 0.0F;
                this.TempResponseByMonth[month] = 0.0F;
                this.GlobalRadiationByMonth[month] = 0.0F;
                this.UtilizableRadiationByMonth[month] = 0.0F;
                this.VpdResponseByMonth[month] = 0.0F;
            }

            this.NitrogenResponseForYear = 0.0F;
            this.RadiationForYear = 0.0F;
            this.UtilizableRadiationForYear = 0.0F;
        }

        /// response calculation called during water cycle
        /// calculates minimum-response of vpd-response and soilwater response
        /// calculate responses for VPD and Soil Water. Return the minimum of those responses
        /// @param psi_kPa psi of the soil in kPa
        /// @param vpd vapor pressure deficit in kPa
        /// @return minimum of soil water and vpd response
        public void GetLimitingSoilWaterOrVpdResponse(float psiInKilopascals, float vpd, out float minResponse)
        {
            float waterResponse = this.Species.GetSoilWaterResponse(psiInKilopascals);
            float vpdResponse = this.Species.GetVpdResponse(vpd);
            minResponse = MathF.Min(waterResponse, vpdResponse);
        }

        /// Main function that calculates monthly / annual species responses
        public void CalculateUtilizableRadiation(Climate climate)
        {
            //using DebugTimer tpg = model.DebugTimers.Create("SpeciesResponse.Calculate()");
            this.Zero(); // reset values

            // calculate yearly responses
            WaterCycle ruWaterCycle = this.ResourceUnit.WaterCycle;
            Phenology phenology = this.ResourceUnit.Climate.GetPhenology(this.Species.PhenologyClass);
            int leafOnIndex = phenology.LeafOnStart;
            int leafOffIndex = phenology.LeafOnEnd;

            // nitrogen response: a yearly value based on available nitrogen
            if (this.ResourceUnit.Soil == null)
            {
                this.NitrogenResponseForYear = 1.0F; // available nitrogen calculations are disabled, so default to making nitrogen non-limiting
            }
            else
            {
                this.NitrogenResponseForYear = this.Species.GetNitrogenResponse(this.ResourceUnit.Soil.PlantAvailableNitrogen);
                Debug.Assert(this.NitrogenResponseForYear >= 0.0);
            }

            int dayOfYear = 0;
            for (int dayIndex = this.ResourceUnit.Climate.CurrentJanuary1; dayIndex < this.ResourceUnit.Climate.NextJanuary1; ++dayIndex, ++dayOfYear)
            {
                ClimateDay day = this.ResourceUnit.Climate[dayIndex];
                int monthIndex = day.Month - 1;
                // environmental responses
                this.GlobalRadiationByMonth[monthIndex] += day.Radiation;

                float soilWaterResponse = this.Species.GetSoilWaterResponse(ruWaterCycle.SoilWaterPsi[dayOfYear]);
                this.SoilWaterResponseByMonth[monthIndex] += soilWaterResponse;

                float tempResponse = this.Species.GetTemperatureResponse(day.MeanDaytimeTemperatureMA1);
                this.TempResponseByMonth[monthIndex] += tempResponse;

                float vpdResponse = this.Species.GetVpdResponse(day.Vpd);
                this.VpdResponseByMonth[monthIndex] += vpdResponse;

                // no utilizable radiation if day is outside of leaf on period so nothing to add
                // If needed, tapering could be used to approxiate leaf out and senescence. Fixed on-off dates are also not reactive to weather.
                if (dayOfYear >= leafOnIndex && dayOfYear <= leafOffIndex)
                {
                    // environmental responses for the day
                    // combine responses
                    float minimumResponse = MathF.Min(MathF.Min(vpdResponse, tempResponse), soilWaterResponse);
                    // calculate utilizable radiation, Eq. 4, http://iland.boku.ac.at/primary+production
                    float utilizableRadiation = day.Radiation * minimumResponse;
                    
                    Debug.Assert(minimumResponse >= 0.0 && minimumResponse < 1.000001);
                    Debug.Assert(utilizableRadiation >= 0.0 && utilizableRadiation < 100.0); // sanity upper bound
                    this.UtilizableRadiationByMonth[monthIndex] += utilizableRadiation;
                }
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
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                float daysInMonth = this.ResourceUnit.Climate.GetDaysInMonth(month);
                this.SoilWaterResponseByMonth[month] /= daysInMonth;
                this.TempResponseByMonth[month] /= daysInMonth;
                this.VpdResponseByMonth[month] /= daysInMonth;
                // CO2 response equations assume nitrogen and soil water responses are in [0 1], so CO2 response is calculated after finding the average soil water
                // response
                float ambientCO2 = climate.CarbonDioxidePpm; // CO2 level of first day of year (co2 is static)
                this.CO2ResponseByMonth[month] = this.Species.SpeciesSet.GetCarbonDioxideResponse(ambientCO2, this.NitrogenResponseForYear, this.SoilWaterResponseByMonth[month]);
                this.UtilizableRadiationForYear += this.UtilizableRadiationByMonth[month];

                Debug.Assert((this.CO2ResponseByMonth[month] > 0.0) && (this.CO2ResponseByMonth[month] <= 1.000001));
                Debug.Assert((this.SoilWaterResponseByMonth[month] >= 0.0) && (this.SoilWaterResponseByMonth[month] <= 1.000001));
                Debug.Assert((this.TempResponseByMonth[month] >= 0.0) && (this.TempResponseByMonth[month] <= 1.000001));
                Debug.Assert((this.VpdResponseByMonth[month] > 0.0) && (this.VpdResponseByMonth[month] <= 1.000001));
                Debug.Assert(this.UtilizableRadiationByMonth[month] >= 0.0); // utilizable radiation will be zero if the limiting response is zero
            }
            this.RadiationForYear = this.ResourceUnit.Climate.TotalAnnualRadiation;
        }
    }
}
