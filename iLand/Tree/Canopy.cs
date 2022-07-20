using iLand.Input;
using iLand.Input.ProjectFile;
using System;

namespace iLand.Tree
{
    /** Interception in crown canopy.
        Calculates the amount of preciptitation that does not reach the ground and
        is stored in the canopy. The approach is adopted from Picus 1.3.
        Returns the amount of precipitation (mm) that surpasses the canopy layer.
        @sa http://iland-model.org/water+cycle#precipitation_and_interception */
    public class Canopy
    {
        // Penman-Monteith parameters
        private readonly float airDensity; // density of air, kg/m³

        private float laiBroadleaf; // leaf area index of broadlevaed species
        private float laiNeedleleaf; // leaf area index of coniferous species
        private float laiTotal; // total leaf area index
        // averaged maximum canopy conductance of current species distribution (m/s)
        // Also stated in mmol H₂O / (m²s) of projected leaf area. Conversion to iLand's units is gmax_mmol / gmax_ms = P/RT with
        // P = atmospheric pressure, T = air temp, R = gas constant:
        //   P/RT = 100 kPa / (8.31446261815324 J/(K mol) (25 + 273.15°C)) = 40,339.55 mmol / m³ at standard temperature and pressure
        // since 1 kPa = 1000 J / m³ => gmax_ms = gmax_mmol = 0.000024790 gmax_mmol.
        // The 0.017 m/s used in species_param_europe.sqlite is 686 mmol H₂O / (m²s) which is appears high for temperate forest per
        // Körner 1995 as cited by Landsberg 1997.
        // Körner C. 1995. Leaf Diffusive Conductances in the Major Vegetation Types of the Globe, chapter 22 in Schulze ED, Caldwell
        //   MM eds. Ecophysiology of Photosynthesis. Springer-Verlag, Berlin Germany. https://doi.org/10.1007/978-3-642-79354-7_22
        private float meanMaxCanopyConductance;

        // parameters for interception
        public float NeedleStorageInMM { get; set; } // factor for calculating water storage capacity for intercepted water for conifers
        public float BroadleafStorageInMM { get; set; } // the same for broadleaved

        public float EvaporationFromCanopy { get; private set; } // evaporation from canopy (mm)
        public float TotalInterceptedWaterInMM { get; private set; } // mm water that is intercepted by the crown
        public float[] ReferenceEvapotranspirationByMonth { get; private init; } // mm/day

        public Canopy(float airDensity)
        {
            this.airDensity = airDensity; // kg/m³

            this.ReferenceEvapotranspirationByMonth = new float[Constant.MonthsInYear];
        }

        /// <returns>Total throughfall during timestep in mm.</returns>
        public float FlowPrecipitationTimestep(float totalTimestepPrecipitationInMM, float daysInTimestep)
        {
            // sanity checks
            this.TotalInterceptedWaterInMM = 0.0F;
            this.EvaporationFromCanopy = 0.0F;
            if (totalTimestepPrecipitationInMM == 0.0F)
            {
                return 0.0F;
            }
            if (this.laiTotal == 0.0)
            {
                return totalTimestepPrecipitationInMM;
            }

            float totalInterceptionInMM = 0.0F; // maximum interception based on the current foliage
            float totalStoragePotential = 0.0F; // storage capacity at very high LAI

            // TODO: with monthly timesteps, account for nonuniformity in daily precipitation
            float dayOrMeanDailyPrecipitationInMM = totalTimestepPrecipitationInMM / daysInTimestep;
            if (this.laiNeedleleaf > 0.0F)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxNeedleleafFlow = 0.9F * MathF.Sqrt(1.03F - MathF.Exp(-0.055F * dayOrMeanDailyPrecipitationInMM));
                totalInterceptionInMM += totalTimestepPrecipitationInMM * (1.0F - maxNeedleleafFlow * this.laiNeedleleaf / this.laiTotal);
                // (2) calculate maximum storage potential based on the current LAI
                //     by weighing the needle/decidious storage capacity
                totalStoragePotential += this.NeedleStorageInMM * this.laiNeedleleaf / this.laiTotal;
            }

            if (this.laiBroadleaf > 0.0)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxBroadleafFlow = 0.9F * MathF.Pow(1.22F - MathF.Exp(-0.055F * dayOrMeanDailyPrecipitationInMM), 0.35F);
                totalInterceptionInMM += totalTimestepPrecipitationInMM * (1.0F - maxBroadleafFlow) * this.laiBroadleaf / this.laiTotal;
                // (2) calculate maximum storage potential based on the current LAI
                totalStoragePotential += this.BroadleafStorageInMM * this.laiBroadleaf / this.laiTotal;
            }

            // the extent to which the maximum stoarge capacity is exploited, depends on LAI:
            float availableStorageInMM = totalStoragePotential * (1.0F - MathF.Exp(-0.5F * this.laiTotal));

            // (3) calculate actual interception and store for evaporation calculation
            this.TotalInterceptedWaterInMM = MathF.Min(availableStorageInMM, totalInterceptionInMM);

            // (4) limit interception with amount of precipitation
            this.TotalInterceptedWaterInMM = MathF.Min(this.TotalInterceptedWaterInMM, totalTimestepPrecipitationInMM);

            // (5) throughfall is precipitation minus the amount intercepted by the canopy
            return totalTimestepPrecipitationInMM - this.TotalInterceptedWaterInMM;
        }

        public void OnStartYear(float laiNeedle, float laiBroadleaf, float meanMaxCanopyConductance)
        {
            this.laiNeedleleaf = laiNeedle;
            this.laiBroadleaf = laiBroadleaf;
            this.laiTotal = laiNeedle + laiBroadleaf;
            this.meanMaxCanopyConductance = meanMaxCanopyConductance;

            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.ReferenceEvapotranspirationByMonth[month] = 0.0F;
            }
        }

        // returns the total sum of evaporation+transpiration in mm of the day
        public float FlowEvapotranspirationTimestep3PG(Project projectFile, WeatherTimeSeries weatherTimeSeries, int weatherTimestepIndex, float dayLengthInHours, float soilAtmosphereModifier)
        {
            float vpdInMillibar = 10.0F * weatherTimeSeries.VpdMeanInKPa[weatherTimestepIndex]; // convert from kPa to mbar
            float meanDaytimeTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]; // average air temperature of the day (°C at 2 m height)
            float dayLengthInSeconds = 3600.0F * dayLengthInHours; // daylength in seconds (convert from length in hours)
            float meanRadiationPower = 1000.0F * 1000.0F * weatherTimeSeries.SolarRadiationTotal[weatherTimestepIndex] / dayLengthInSeconds; //convert from MJ/m² (day sum) to average radiation flow W/m² [MJ = MWs . /s * 1,000,000

            // the radiation: based on linear empirical function
            float netRadiation = -90.0F + 0.8F * meanRadiationPower; // qa + qb * radiation

            // Landsberg original: float e20 = 2.2;  // rate of change of saturated VP with T at 20C
            const float vpdToSaturationDeficit = 0.000622F; // convert VPD to saturation deficit = 18/29/1000 = molecular weight of H₂O/molecular weight of air
            const float latentHeatOfVaporization = 2460000.0F; // Latent heat of vaporization. Energy required per unit mass of water vaporized, J/kg
            float boundaryLayerConductance = projectFile.Model.Ecosystem.BoundaryLayerConductance; // gA, m/s

            // canopy conductance.
            // The species traits are weighted by LAI on the RU.
            // maximum canopy conductance: see getStandValues()
            // current response: see WaterCycle.RunYear(). This is basically a weighted average of min(water_response, vpd_response) for
            // each species.
            float gC = this.meanMaxCanopyConductance * soilAtmosphereModifier;
            float defTerm = this.airDensity * latentHeatOfVaporization * (vpdInMillibar * vpdToSaturationDeficit) * boundaryLayerConductance;

            // with temperature-dependent slope of the saturated vapor pressure curve
            // (following  Allen et al. (1998), http://www.fao.org/docrep/x0490e/x0490e07.htm#atmospheric%20parameters)
            // svp_slope in kPa/°C
            //float svpSlope = 4098.0F * (0.6108F * MathF.Exp(17.269F * meanTemp / (meanTemp + 237.3F))) / ((237.3F + meanTemp) * (237.3F + meanTemp));

            // alternatively, following Landsberg and Sands 2010 §7.2.1 (https://github.com/trotsiuk/r3PG/issues/84)
            const float svpSlope = 0.145F; // FAO56 saturation vapor pressure slope at 20° C
            // divide saturated vapor pressure slope by the approximate psychrometric constant for actively ventilated psychrometers to match 3-PGmix
            // Psychrometric constant is higher for other psychrometer types and is elevation dependent since it varies with air pressure
            // (https://www.fao.org/3/x0490e/x0490e07.htm#calculation%20procedures).
            const float s = svpSlope / 0.066F;

            float evapotranspiration = (s * netRadiation + defTerm) / (1.0F + s + boundaryLayerConductance / gC);
            float canopyTranspiration = evapotranspiration / latentHeatOfVaporization * dayLengthInSeconds;

            // calculate reference evapotranspiration (et0 for 12 cm tall grass field, 1-9 mm/day increasing with temperature, decreasing with humidity)
            // Code here follows the de facto FAO56 Penman-Monteith standard as other 3-PG variants do.
            // Allen RG, Pereira LS, Raes D, Smith M. 1998. Crop evapotranspiration - Guidelines for computing crop water requirements - FAO
            //   Irrigation and drainage paper 56. Food and Agriculture Organization of the United Nations.
            //   Chapter 4 - Determination of ETo https://www.fao.org/3/x0490e/x0490e08.htm
            // FAO definition of saturation vapor pressure slope = 4098 * 0.6108 * exp(17.27 * Tmean / (Tmean + 237.3)) / (Tmean + 237.3)^2 kPa/°C
            //
            // However, numerous studies of other evapotranspiration methods exist and, as FAO56 is oriented to field crops, other approaches
            // may be more effective for forest simulation. See, for example,
            // Ershadi A, McCabe FA, Evans JP, Wood EF. 2015. Impact of model structure and parameterization on Penman–Monteith type evaporation
            //   models. Journal of Hydrology 525:521-535. https://doi.org/10.1016/j.jhydrol.2015.04.008
            const float psychrometricConstant = 0.0672718682328237F; // kPa/°C
            const float windSpeed = 2.0F; // wind speed at 2 m height, m/s
            float net_rad_mj_day = netRadiation * dayLengthInSeconds / 1000000.0F; // convert W/m² again to MJ/m²-day
            float et0_numerator = 0.408F * s * net_rad_mj_day + psychrometricConstant * 900.0F / (meanDaytimeTemperature + 273.15F) * windSpeed * weatherTimeSeries.VpdMeanInKPa[weatherTimestepIndex];
            float et0_denominator = s + psychrometricConstant * (1.0F + 0.34F * windSpeed);
            float et0_day = et0_numerator / et0_denominator; // FAO56, Chapter 2
            this.ReferenceEvapotranspirationByMonth[weatherTimeSeries.Month[weatherTimestepIndex] - 1] += et0_day;

            if (this.TotalInterceptedWaterInMM > 0.0F)
            {
                // we assume that for evaporation from leaf surface gBL/gC -> 0
                float div_evap = 1.0F + s;
                float potentialCanopyEvaporation = (s * netRadiation + defTerm) / div_evap / latentHeatOfVaporization * dayLengthInSeconds;
                // reduce the amount of transpiration on a wet day based on the approach of
                // Wigmosta et al (1994). See http://iland-model.org/water+cycle#transpiration_and_canopy_conductance

                float ratio_T_E = canopyTranspiration / potentialCanopyEvaporation;
                float canopyEvaporation = MathF.Min(potentialCanopyEvaporation, this.TotalInterceptedWaterInMM);

                // for interception -> 0, the canopy transpiration is unchanged
                canopyTranspiration = (potentialCanopyEvaporation - canopyEvaporation) * ratio_T_E;

                this.TotalInterceptedWaterInMM -= canopyEvaporation; // reduce interception
                this.EvaporationFromCanopy = canopyEvaporation; // evaporation from intercepted water
            }
            return canopyTranspiration;
        }
    }
}
