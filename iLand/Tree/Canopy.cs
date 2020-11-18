using iLand.Input.ProjectFile;
using iLand.World;
using System;

namespace iLand.Tree
{
    /** Interception in crown canopy.
        Calculates the amount of preciptitation that does not reach the ground and
        is stored in the canopy. The approach is adopted from Picus 1.3.
        Returns the amount of precipitation (mm) that surpasses the canopy layer.
        @sa http://iland.boku.ac.at/water+cycle#precipitation_and_interception */
    public class Canopy
    {
        // Penman-Monteith parameters
        private readonly float mAirDensity; // density of air, kg/m³

        private float mLaiNeedle; // leaf area index of coniferous species
        private float mLaiBroadleaved; // leaf area index of broadlevaed species
        private float mLai; // total leaf area index
        private float maxCanopyConductance; // averaged maximum canopy conductance of current species distribution (m/s)

        // parameters for interception
        public float NeedleStorageFactor { get; set; } // factor for calculating water storage capacity for intercepted water for conifers
        public float BroadleafStorageFactor { get; set; } // the same for broadleaved

        public float EvaporationFromCanopy { get; private set; } // evaporation from canopy (mm)
        public float Interception { get; private set; } // mm water that is intercepted by the crown
        public float[] ReferenceEvapotranspirationByMonth { get; init; } // monthly reference ET (see Adair et al 2008)

        public Canopy(float airDensity)
        {
            this.mAirDensity = airDensity; // kg / m3

            this.ReferenceEvapotranspirationByMonth = new float[Constant.MonthsInYear];
        }

        public float Flow(float preciptitationInMM)
        {
            // sanity checks
            this.Interception = 0.0F;
            this.EvaporationFromCanopy = 0.0F;
            if (preciptitationInMM == 0.0F)
            {
                return 0.0F;
            }
            if (mLai == 0.0)
            {
                return preciptitationInMM;
            }
            float maxInterceptionInMM = 0.0F; // maximum interception based on the current foliage
            float maxStoragePotential = 0.0F; // storage capacity at very high LAI

            if (mLaiNeedle > 0.0F)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxNeedleFlow = 0.9F * MathF.Sqrt(1.03F - MathF.Exp(-0.055F * preciptitationInMM));
                maxInterceptionInMM += preciptitationInMM * (1.0F - maxNeedleFlow * mLaiNeedle / mLai);
                // (2) calculate maximum storage potential based on the current LAI
                //     by weighing the needle/decidious storage capacity
                maxStoragePotential += this.NeedleStorageFactor * mLaiNeedle / mLai;
            }

            if (mLaiBroadleaved > 0.0)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                float maxBroadleafFlow = 0.9F * MathF.Pow(1.22F - MathF.Exp(-0.055F * preciptitationInMM), 0.35F);
                maxInterceptionInMM += preciptitationInMM * (1.0F - maxBroadleafFlow) * mLaiBroadleaved / mLai;
                // (2) calculate maximum storage potential based on the current LAI
                maxStoragePotential += this.BroadleafStorageFactor * mLaiBroadleaved / mLai;
            }

            // the extent to which the maximum stoarge capacity is exploited, depends on LAI:
            float maxStorageInMM = maxStoragePotential * (1.0F - MathF.Exp(-0.5F * mLai));

            // (3) calculate actual interception and store for evaporation calculation
            this.Interception = MathF.Min(maxStorageInMM, maxInterceptionInMM);

            // (4) limit interception with amount of precipitation
            this.Interception = MathF.Min(this.Interception, preciptitationInMM);

            // (5) reduce precipitation by the amount is intercepted by the canopy
            return preciptitationInMM - this.Interception;
        }

        public void SetStandParameters(float laiNeedle, float laiBroadleaf, float maxCanopyConductance)
        {
            this.mLaiNeedle = laiNeedle;
            this.mLaiBroadleaved = laiBroadleaf;
            this.mLai = laiNeedle + laiBroadleaf;
            this.maxCanopyConductance = maxCanopyConductance;

            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.ReferenceEvapotranspirationByMonth[month] = 0.0F;
            }
        }

        // Returns the total sum of evaporation+transpiration in mm of the day.
        public float GetEvapotranspiration3PG(Project projectFile, ClimateDay day, float dayLengthInHours, float combinedResponse)
        {
            float vpdInMillibar = 10.0F * day.Vpd; // convert from kPa to mbar
            float meanDaytimeTemperature = day.MeanDaytimeTemperature; // average temperature of the day (degree C)
            float dayLengthInSeconds = 3600.0F * dayLengthInHours; // daylength in seconds (convert from length in hours)
            float rad = 1000.0F * 1000.0F * day.Radiation / dayLengthInSeconds; //convert from MJ/m2 (day sum) to average radiation flow W/m2 [MJ=MWs . /s * 1,000,000

            // the radiation: based on linear empirical function
            const float qa = -90.0F;
            const float qb = 0.8F;
            float net_rad = qa + qb * rad;

            // Landsberg original: float e20 = 2.2;  //rate of change of saturated VP with T at 20C
            const float vpdToSaturationDeficit = 0.000622F; //convert VPD to saturation deficit = 18/29/1000 = molecular weight of H2O/molecular weight of air
            const float latentHeatOfVaporization = 2460000.0F; // Latent heat of vaporization. Energy required per unit mass of water vaporized [J kg-1]
            float boundaryLayerConductance = projectFile.Model.Ecosystem.BoundaryLayerConductance; // boundary layer conductance

            // canopy conductance.
            // The species traits are weighted by LAI on the RU.
            // maximum canopy conductance: see getStandValues()
            // current response: see calculateSoilAtmosphereResponse(). This is basically a weighted average of min(water_response, vpd_response) for each species
            float gC = this.maxCanopyConductance * combinedResponse;
            float defTerm = this.mAirDensity * latentHeatOfVaporization * (vpdInMillibar * vpdToSaturationDeficit) * boundaryLayerConductance;

            //  with temperature-dependent  slope of  vapor pressure saturation curve
            // (following  Allen et al. (1998),  http://www.fao.org/docrep/x0490e/x0490e07.htm#atmospheric%20parameters)
            // svp_slope in mbar.
            //float svp_slope = 4098. * (6.1078 * exp(17.269 * temperature / (temperature + 237.3))) / ((237.3+temperature)*(237.3+temperature));

            // alternatively: very simple variant (following here the original 3PG code). This
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

            if (this.Interception > 0.0F)
            {
                // we assume that for evaporation from leaf surface gBL/gC -> 0
                float div_evap = 1.0F + svp_slope;
                float potentialCanopyEvaporation = (svp_slope * net_rad + defTerm) / div_evap / latentHeatOfVaporization * dayLengthInSeconds;
                // reduce the amount of transpiration on a wet day based on the approach of
                // Wigmosta et al (1994). See http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance

                float ratio_T_E = canopyTranspiration / potentialCanopyEvaporation;
                float canopyEvaporation = MathF.Min(potentialCanopyEvaporation, this.Interception);

                // for interception -> 0, the canopy transpiration is unchanged
                canopyTranspiration = (potentialCanopyEvaporation - canopyEvaporation) * ratio_T_E;

                this.Interception -= canopyEvaporation; // reduce interception
                this.EvaporationFromCanopy = canopyEvaporation; // evaporation from intercepted water
            }
            return canopyTranspiration;
        }
    }
}
