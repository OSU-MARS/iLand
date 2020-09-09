using iLand.tools;
using System.Diagnostics;

namespace iLand.core
{
    internal class Production3PG
    {
        public SpeciesResponse mResponse; ///< species specific responses
        public double[] mUPAR; ///< utilizable radiation MJ/m2 and month
        public double[] mGPP; ///< monthly Gross Primary Production [kg Biomass / m2]
        private double mRootFraction; ///< fraction of production that flows into roots
        private double mGPPperArea; ///< kg GPP Biomass / m2 interception area
        private double mEnvYear; ///< f_env,yr: factor that aggregates the environment for the species over the year (weighted with the radiation pattern)

        public void setResponse(SpeciesResponse response) { mResponse = response; }
        public double rootFraction() { return mRootFraction; } /// fraction of biomass that should be distributed to roots
        public double GPPperArea() { return mGPPperArea; } ///<  GPP production (yearly) (kg Biomass) per m2 (effective area)
        public double fEnvYear() { return mEnvYear; } ///< f_env,yr: aggregate environmental factor [0..1}

        public Production3PG()
        {
            clear();
            mResponse = null;
            mEnvYear = 0.0;
            mUPAR = new double[12];
            mGPP = new double[12];
        }

        /**
          This is based on the utilizable photosynthetic active radiation.
          @sa http://iland.boku.ac.at/primary+production
          The resulting radiation is MJ/m2       */
        private double calculateUtilizablePAR(int month)
        {
            // calculate the available radiation. This is done at SpeciesResponse-Level (SpeciesResponse::calculate())
            // see Equation (3)
            // multiplicative approach: responses are averaged one by one and multiplied on a monthly basis
            //    double response = mResponse.absorbedRadiation()[month] *
            //                      mResponse.vpdResponse()[month] *
            //                      mResponse.soilWaterResponse()[month] *
            //                      mResponse.tempResponse()[month];
            // minimum approach: for each day the minimum aof vpd, temp, soilwater is calculated, then averaged for each month
            //double response = mResponse.absorbedRadiation()[month] *
            //                  mResponse.minimumResponses()[month];
            double response = mResponse.utilizableRadiation()[month];
            return response;
        }

        /** calculate the alphac (=photosynthetic efficiency) for the given month.
           this is based on a global efficiency, and modified per species.
           epsilon is in gC/MJ Radiation
          */
        private double calculateEpsilon(int month)
        {
            double epsilon = GlobalSettings.instance().model().settings().epsilon; // maximum radiation use efficiency
            epsilon *= mResponse.nitrogenResponse() * mResponse.co2Response()[month];
            return epsilon;
        }

        private double abovegroundFraction()
        {
            double utilized_frac = 1.0;
            if (GlobalSettings.instance().model().settings().usePARFractionBelowGroundAllocation)
            {
                // the Landsberg & Waring formulation takes into account the fraction of utilizeable to total radiation (but more complicated)
                // we originally used only nitrogen and added the U_utilized/U_radiation
                utilized_frac = mResponse.totalUtilizeableRadiation() / mResponse.yearlyRadiation();
            }
            double harsh = 1 - 0.8 / (1 + 2.5 * mResponse.nitrogenResponse() * utilized_frac);
            return harsh;
        }

        public void clear()
        {
            for (int i = 0; i < 12; i++)
            {
                mGPP[i] = 0.0; 
                mUPAR[i] = 0.0;
            }
            mEnvYear = 0.0;
            mGPPperArea = 0.0;
            mRootFraction = 0.0;
        }

        /** calculate the stand-level NPP
          @ingroup core
          Standlevel (i.e ResourceUnit-level) production (NPP) following the 3PG approach from Landsberg and Waring.
          @sa http://iland.boku.ac.at/primary+production */
        public double calculate()
        {
            Debug.Assert(mResponse != null);
            // Radiation: sum over all days of each month with foliage
            double year_raw_gpp = 0.0;
            clear();
            double utilizable_rad, epsilon;
            // conversion from gC to kg Biomass: C/Biomass=0.5
            double gC_to_kg_biomass = 1.0 / (Constant.biomassCFraction * 1000.0);
            for (int i = 0; i < 12; i++)
            {
                utilizable_rad = calculateUtilizablePAR(i); // utilizable radiation of the month ... (MJ/m2)
                epsilon = calculateEpsilon(i); // ... photosynthetic efficiency ... (gC/MJ)
                mUPAR[i] = utilizable_rad;
                mGPP[i] = utilizable_rad * epsilon * gC_to_kg_biomass; // ... results in GPP of the month kg Biomass/m2 (converted from gC/m2)
                year_raw_gpp += mGPP[i]; // kg Biomass/m2
            }

            // calculate f_env,yr: see http://iland.boku.ac.at/sapling+growth+and+competition
            double f_sum = 0.0;
            for (int i = 0; i < 12; i++)
            {
                f_sum += mGPP[i] / gC_to_kg_biomass; // == uAPar * epsilon_eff
            }

            //  the factor f_ref: parameter that scales response values to the range 0..1 (1 for best growth conditions) (species parameter)
            double perf_factor = mResponse.species().saplingGrowthParameters().referenceRatio;
            // f_env,yr=(uapar*epsilon_eff) / (APAR * epsilon_0 * fref)
            mEnvYear = f_sum / (GlobalSettings.instance().model().settings().epsilon * mResponse.yearlyRadiation() * perf_factor);
            if (mEnvYear > 1.0)
            {
                if (mEnvYear > 1.5) // warning for large deviations
                    Debug.WriteLine("WARNING: fEnvYear > 1 for " + mResponse.species().id() + mEnvYear + " f_sum, epsilon, yearlyRad, refRatio " + f_sum + GlobalSettings.instance().model().settings().epsilon + mResponse.yearlyRadiation() + perf_factor
                             + " check calibration of the sapReferenceRatio (fref) for this species!");
                mEnvYear = 1.0;
            }

            // calculate fraction for belowground biomass
            mRootFraction = 1.0 - abovegroundFraction();

            // global value set?
            double dbg = GlobalSettings.instance().settings().paramValue("gpp_per_year", 0);
            if (dbg > 0.0)
            {
                year_raw_gpp = dbg;
                mRootFraction = 0.4;
            }

            // year GPP/rad: kg Biomass/m2
            mGPPperArea = year_raw_gpp;
            return mGPPperArea; // yearly GPP in kg Biomass/m2
        }
    }
}
