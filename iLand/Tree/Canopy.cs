using iLand.Input.ProjectFile;
using iLand.World;
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
        private readonly float mAirDensity; // density of air, kg/m³

        private float mLaiNeedle; // leaf area index of coniferous species
        private float mLaiBroadleaf; // leaf area index of broadlevaed species
        private float mLai; // total leaf area index
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
        public float StoredWaterInMM { get; private set; } // mm water that is intercepted by the crown
        public float[] ReferenceEvapotranspirationByMonth { get; private init; } // monthly reference ET (see Adair et al 2008)

        public Canopy(float airDensity)
        {
            this.mAirDensity = airDensity; // kg / m3

            this.ReferenceEvapotranspirationByMonth = new float[Constant.MonthsInYear];
        }

        public float FlowDayToStorage(float precipitationInMM)
        {
            // sanity checks
            this.StoredWaterInMM = 0.0F;
            this.EvaporationFromCanopy = 0.0F;
            if (precipitationInMM == 0.0F)
            {
                return 0.0F;
            }
            if (this.mLai == 0.0)
            {
                return precipitationInMM;
            }
            float maxInterceptionInMM = 0.0F; // maximum interception based on the current foliage
            float maxStoragePotential = 0.0F; // storage capacity at very high LAI

            if (this.mLaiNeedle > 0.0F)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxNeedleFlow = 0.9F * MathF.Sqrt(1.03F - MathF.Exp(-0.055F * precipitationInMM));
                maxInterceptionInMM += precipitationInMM * (1.0F - maxNeedleFlow * this.mLaiNeedle / this.mLai);
                // (2) calculate maximum storage potential based on the current LAI
                //     by weighing the needle/decidious storage capacity
                maxStoragePotential += this.NeedleStorageInMM * this.mLaiNeedle / this.mLai;
            }

            if (this.mLaiBroadleaf > 0.0)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxBroadleafFlow = 0.9F * MathF.Pow(1.22F - MathF.Exp(-0.055F * precipitationInMM), 0.35F);
                maxInterceptionInMM += precipitationInMM * (1.0F - maxBroadleafFlow) * this.mLaiBroadleaf / this.mLai;
                // (2) calculate maximum storage potential based on the current LAI
                maxStoragePotential += this.BroadleafStorageInMM * this.mLaiBroadleaf / this.mLai;
            }

            // the extent to which the maximum stoarge capacity is exploited, depends on LAI:
            float maxStorageInMM = maxStoragePotential * (1.0F - MathF.Exp(-0.5F * this.mLai));

            // (3) calculate actual interception and store for evaporation calculation
            this.StoredWaterInMM = MathF.Min(maxStorageInMM, maxInterceptionInMM);

            // (4) limit interception with amount of precipitation
            this.StoredWaterInMM = MathF.Min(this.StoredWaterInMM, precipitationInMM);

            // (5) throughfall is precipitation minus the amount intercepted by the canopy
            return precipitationInMM - this.StoredWaterInMM;
        }

        public void SetStandParameters(float laiNeedle, float laiBroadleaf, float meanMaxCanopyConductance)
        {
            this.mLaiNeedle = laiNeedle;
            this.mLaiBroadleaf = laiBroadleaf;
            this.mLai = laiNeedle + laiBroadleaf;
            this.meanMaxCanopyConductance = meanMaxCanopyConductance;

            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.ReferenceEvapotranspirationByMonth[month] = 0.0F;
            }
        }

        // returns the total sum of evaporation+transpiration in mm of the day
        public float FlowDayEvapotranspiration3PG(Project projectFile, ClimateDay day, float dayLengthInHours, float soilAtmosphereResponse)
        {
            float vpdInMillibar = 10.0F * day.Vpd; // convert from kPa to mbar
            float meanDaytimeTemperature = day.MeanDaytimeTemperature; // average temperature of the day (degree C)
            float dayLengthInSeconds = 3600.0F * dayLengthInHours; // daylength in seconds (convert from length in hours)
            float rad = 1000.0F * 1000.0F * day.Radiation / dayLengthInSeconds; //convert from MJ/m2 (day sum) to average radiation flow W/m2 [MJ=MWs . /s * 1,000,000

            // the radiation: based on linear empirical function
            const float qa = -90.0F;
            const float qb = 0.8F;
            float net_rad = qa + qb * rad;

            // Landsberg original: float e20 = 2.2;  // rate of change of saturated VP with T at 20C
            const float vpdToSaturationDeficit = 0.000622F; // convert VPD to saturation deficit = 18/29/1000 = molecular weight of H2O/molecular weight of air
            const float latentHeatOfVaporization = 2460000.0F; // Latent heat of vaporization. Energy required per unit mass of water vaporized [J kg-1]
            float boundaryLayerConductance = projectFile.Model.Ecosystem.BoundaryLayerConductance; // gA, m/s

            // canopy conductance.
            // The species traits are weighted by LAI on the RU.
            // maximum canopy conductance: see getStandValues()
            // current response: see calculateSoilAtmosphereModifier(). This is basically a weighted average of min(water_response, vpd_response) for
            // each species.
            float gC = this.meanMaxCanopyConductance * soilAtmosphereResponse;
            float defTerm = this.mAirDensity * latentHeatOfVaporization * (vpdInMillibar * vpdToSaturationDeficit) * boundaryLayerConductance;

            // with temperature-dependent slope of vapor pressure saturation curve
            // (following  Allen et al. (1998), http://www.fao.org/docrep/x0490e/x0490e07.htm#atmospheric%20parameters)
            // svp_slope in mbar.
            //float svp_slope = 4098. * (6.1078 * exp(17.269 * temperature / (temperature + 237.3))) / ((237.3+temperature)*(237.3+temperature));

            // alternatively: very simple variant (following here the original 3-PG code). This
            // keeps yields +- same results for summer, but slightly lower values in winter (2011/03/16)
            const float svp_slope = 2.2F;

            float evapotranspiration = (svp_slope * net_rad + defTerm) / (1.0F + svp_slope + boundaryLayerConductance / gC);
            float canopyTranspiration = evapotranspiration / latentHeatOfVaporization * dayLengthInSeconds;

            // calculate reference evapotranspiration
            // see Adair et al 2008
            const float psychrometricConstant = 0.0672718682328237F; // kPa/degC
            const float windspeed = 2.0F; // m/s
            float net_rad_mj_day = net_rad * dayLengthInSeconds / 1000000.0F; // convert W/m2 again to MJ/m2*day
            float et0_day = 0.408F * svp_slope * net_rad_mj_day + psychrometricConstant * 900.0F / (meanDaytimeTemperature + 273.15F) * windspeed * day.Vpd;
            float et0_div = svp_slope + psychrometricConstant * (1.0F + 0.34F * windspeed);
            et0_day /= et0_div;
            this.ReferenceEvapotranspirationByMonth[day.Month - 1] += et0_day;

            if (this.StoredWaterInMM > 0.0F)
            {
                // we assume that for evaporation from leaf surface gBL/gC -> 0
                float div_evap = 1.0F + svp_slope;
                float potentialCanopyEvaporation = (svp_slope * net_rad + defTerm) / div_evap / latentHeatOfVaporization * dayLengthInSeconds;
                // reduce the amount of transpiration on a wet day based on the approach of
                // Wigmosta et al (1994). See http://iland-model.org/water+cycle#transpiration_and_canopy_conductance

                float ratio_T_E = canopyTranspiration / potentialCanopyEvaporation;
                float canopyEvaporation = MathF.Min(potentialCanopyEvaporation, this.StoredWaterInMM);

                // for interception -> 0, the canopy transpiration is unchanged
                canopyTranspiration = (potentialCanopyEvaporation - canopyEvaporation) * ratio_T_E;

                this.StoredWaterInMM -= canopyEvaporation; // reduce interception
                this.EvaporationFromCanopy = canopyEvaporation; // evaporation from intercepted water
            }
            return canopyTranspiration;
        }
    }
}
