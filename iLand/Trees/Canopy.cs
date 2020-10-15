using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using System;

namespace iLand.Trees
{
    /** Interception in crown canopy.
        Calculates the amount of preciptitation that does not reach the ground and
        is stored in the canopy. The approach is adopted from Picus 1.3.
        Returns the amount of precipitation (mm) that surpasses the canopy layer.
        @sa http://iland.boku.ac.at/water+cycle#precipitation_and_interception */
    internal class Canopy
    {
        private double mLAINeedle; // leaf area index of coniferous species
        private double mLAIBroadleaved; // leaf area index of broadlevaed species
        private double mLAI; // total leaf area index
        // Penman-Monteith parameters
        private double mAirDensity; // density of air [kg / m3]
        // parameters for interception
        public double NeedleFactor { get; set; } ///< factor for calculating water storage capacity for intercepted water for conifers
        public double DecidousFactor { get; set; } ///< the same for broadleaved

        public double Interception { get; private set; } ///< mm water that is intercepted by the crown
        public double EvaporationCanopy { get; private set; } ///< evaporation from canopy (mm)
        public double AvgMaxCanopyConductance { get; private set; } ///< averaged maximum canopy conductance of current species distribution (m/s)
        public double[] ReferenceEvapotranspiration { get; private set; } ///< monthly reference ET (see Adair et al 2008)

        public Canopy()
        {
            this.ReferenceEvapotranspiration = new double[12];
        }

        public double Flow(double preciptitation_mm)
        {
            // sanity checks
            Interception = 0.0;
            EvaporationCanopy = 0.0;
            if (mLAI == 0.0)
            {
                return preciptitation_mm;
            }
            if (preciptitation_mm == 0.0)
            {
                return 0.0;
            }
            double max_interception_mm = 0.0; // maximum interception based on the current foliage
            double max_storage_potential = 0.0; // storage capacity at very high LAI

            if (mLAINeedle > 0.0)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                double max_flow_needle = 0.9 * Math.Sqrt(1.03 - Math.Exp(-0.055 * preciptitation_mm));
                max_interception_mm += preciptitation_mm * (1.0 - max_flow_needle * mLAINeedle / mLAI);
                // (2) calculate maximum storage potential based on the current LAI
                //     by weighing the needle/decidious storage capacity
                max_storage_potential += NeedleFactor * mLAINeedle / mLAI;
            }

            if (mLAIBroadleaved > 0.0)
            {
                // (1) calculate maximum fraction of thru-flow the crown (based on precipitation)
                double max_flow_broad = 0.9 * Math.Pow(1.22 - Math.Exp(-0.055 * preciptitation_mm), 0.35);
                max_interception_mm += preciptitation_mm * (1.0 - max_flow_broad) * mLAIBroadleaved / mLAI;
                // (2) calculate maximum storage potential based on the current LAI
                max_storage_potential += DecidousFactor * mLAIBroadleaved / mLAI;
            }

            // the extent to which the maximum stoarge capacity is exploited, depends on LAI:
            double max_storage_mm = max_storage_potential * (1.0 - Math.Exp(-0.5 * mLAI));

            // (3) calculate actual interception and store for evaporation calculation
            Interception = Math.Min(max_storage_mm, max_interception_mm);

            // (4) limit interception with amount of precipitation
            Interception = Math.Min(Interception, preciptitation_mm);

            // (5) reduce preciptitaion by the amount that is intercepted by the canopy
            return preciptitation_mm - Interception;
        }

        /// sets up the canopy. fetch some global parameter values...
        public void Setup(Model model)
        {
            mAirDensity = model.ModelSettings.AirDensity; // kg / m3
        }

        public void SetStandParameters(double LAIneedle, double LAIbroadleave, double maxCanopyConductance)
        {
            mLAINeedle = LAIneedle;
            mLAIBroadleaved = LAIbroadleave;
            mLAI = LAIneedle + LAIbroadleave;
            AvgMaxCanopyConductance = maxCanopyConductance;

            // clear aggregation containers
            for (int i = 0; i < 12; ++i)
            {
                ReferenceEvapotranspiration[i] = 0.0;
            }
        }

        // Returns the total sum of evaporation+transpiration in mm of the day.
        public double Evapotranspiration3PG(ClimateDay climate, Model model, double daylength_h, double combined_response)
        {
            double vpd_mbar = climate.Vpd * 10.0; // convert from kPa to mbar
            double temperature = climate.MeanDaytimeTemperature; // average temperature of the day (degree C)
            double daylength = daylength_h * 3600.0; // daylength in seconds (convert from length in hours)
            double rad = climate.Radiation / daylength * 1000000; //convert from MJ/m2 (day sum) to average radiation flow W/m2 [MJ=MWs . /s * 1,000,000

            // the radiation: based on linear empirical function
            double qa = -90.0;
            double qb = 0.8;
            double net_rad = qa + qb * rad;

            //: Landsberg original: double e20 = 2.2;  //rate of change of saturated VP with T at 20C
            double VPDconv = 0.000622; //convert VPD to saturation deficit = 18/29/1000 = molecular weight of H2O/molecular weight of air
            double latent_heat = 2460000.0; // Latent heat of vaporization. Energy required per unit mass of water vaporized [J kg-1]

            double gBL = model.ModelSettings.BoundaryLayerConductance; // boundary layer conductance

            // canopy conductance.
            // The species traits are weighted by LAI on the RU.
            // maximum canopy conductance: see getStandValues()
            // current response: see calculateSoilAtmosphereResponse(). This is basically a weighted average of min(water_response, vpd_response) for each species
            double gC = AvgMaxCanopyConductance * combined_response;


            double defTerm = mAirDensity * latent_heat * (vpd_mbar * VPDconv) * gBL;

            //  with temperature-dependent  slope of  vapor pressure saturation curve
            // (following  Allen et al. (1998),  http://www.fao.org/docrep/x0490e/x0490e07.htm#atmospheric%20parameters)
            // svp_slope in mbar.
            //double svp_slope = 4098. * (6.1078 * exp(17.269 * temperature / (temperature + 237.3))) / ((237.3+temperature)*(237.3+temperature));

            // alternatively: very simple variant (following here the original 3PG code). This
            // keeps yields +- same results for summer, but slightly lower values in winter (2011/03/16)
            double svp_slope = 2.2;

            double div = (1.0 + svp_slope + gBL / gC);
            double Etransp = (svp_slope * net_rad + defTerm) / div;
            double canopy_transpiration = Etransp / latent_heat * daylength;

            // calculate reference evapotranspiration
            // see Adair et al 2008
            const double psychrometric_const = 0.0672718682328237; // kPa/degC
            double windspeed = 2.0; // m/s
            double net_rad_mj_day = net_rad * daylength / 1000000.0; // convert W/m2 again to MJ/m2*day
            double et0_day = 0.408 * svp_slope * net_rad_mj_day + psychrometric_const * 900.0 / (temperature + 273.15) * windspeed * climate.Vpd;
            double et0_div = svp_slope + psychrometric_const * (1.0 + 0.34 * windspeed);
            et0_day /= et0_div;
            ReferenceEvapotranspiration[climate.Month - 1] += et0_day;

            if (Interception > 0.0)
            {
                // we assume that for evaporation from leaf surface gBL/gC -> 0
                double div_evap = 1.0 + svp_slope;
                double evap_canopy_potential = (svp_slope * net_rad + defTerm) / div_evap / latent_heat * daylength;
                // reduce the amount of transpiration on a wet day based on the approach of
                // Wigmosta et al (1994). See http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance

                double ratio_T_E = canopy_transpiration / evap_canopy_potential;
                double evap_canopy = Math.Min(evap_canopy_potential, Interception);

                // for interception -> 0, the canopy transpiration is unchanged
                canopy_transpiration = (evap_canopy_potential - evap_canopy) * ratio_T_E;

                Interception -= evap_canopy; // reduce interception
                EvaporationCanopy = evap_canopy; // evaporation from intercepted water

            }
            return canopy_transpiration;
        }
    }
}
