﻿using System.Diagnostics;

namespace iLand.Core
{
    public class Production3PG
    {
        ///< f_env,yr: aggregate environmental factor [0..1}
        ///< f_env,yr: factor that aggregates the environment for the species over the year (weighted with the radiation pattern)
        public double EnvironmentalFactor { get; private set; }
        ///< monthly Gross Primary Production [kg Biomass / m2]
        public double[] Gpp { get; private set; }
        ///<  GPP production (yearly) (kg Biomass) per m2 (effective area)
        public double GppPerArea { get; private set; }
        /// fraction of biomass that should be distributed to roots
        public double RootFraction { get; private set; }
        ///< species specific responses
        public SpeciesResponse SpeciesResponse { get; set; }
        ///< utilizable radiation MJ/m2 and month
        public double[] UtilizablePar { get; private set; }

        public Production3PG()
        {
            this.EnvironmentalFactor = 0.0;
            this.Gpp = new double[12];
            this.GppPerArea = 0.0;
            this.SpeciesResponse = null;
            this.RootFraction = 0.0;
            this.UtilizablePar = new double[12];
        }

        /**
          This is based on the utilizable photosynthetic active radiation.
          @sa http://iland.boku.ac.at/primary+production
          The resulting radiation is MJ/m2       */
        private double CalculateUtilizablePar(int month)
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
            double response = SpeciesResponse.UtilizableRadiation[month];
            return response;
        }

        /** calculate the alphac (=photosynthetic efficiency) for the given month.
           this is based on a global efficiency, and modified per species.
           epsilon is in gC/MJ Radiation
          */
        private double CalculateEpsilon(int month, Model model)
        {
            double epsilon = model.ModelSettings.Epsilon; // maximum radiation use efficiency
            epsilon *= SpeciesResponse.NitrogenResponse * SpeciesResponse.Co2Response[month];
            return epsilon;
        }

        private double AbovegroundFraction(Model model)
        {
            double utilized_frac = 1.0;
            if (model.ModelSettings.UseParFractionBelowGroundAllocation)
            {
                // the Landsberg & Waring formulation takes into account the fraction of utilizeable to total radiation (but more complicated)
                // we originally used only nitrogen and added the U_utilized/U_radiation
                utilized_frac = SpeciesResponse.YearlyUtilizableRadiation / SpeciesResponse.YearlyRadiation;
            }
            double harsh = 1 - 0.8 / (1 + 2.5 * SpeciesResponse.NitrogenResponse * utilized_frac);
            return harsh;
        }

        public void Clear()
        {
            for (int i = 0; i < 12; i++)
            {
                Gpp[i] = 0.0; 
                UtilizablePar[i] = 0.0;
            }

            EnvironmentalFactor = 0.0;
            GppPerArea = 0.0;
            RootFraction = 0.0;
            // BUGBUG: speciesResponse?
        }

        /** calculate the stand-level NPP
          @ingroup core
          Standlevel (i.e ResourceUnit-level) production (NPP) following the 3PG approach from Landsberg and Waring.
          @sa http://iland.boku.ac.at/primary+production */
        public double Calculate(Model model)
        {
            Debug.Assert(SpeciesResponse != null);
            // Radiation: sum over all days of each month with foliage
            double year_raw_gpp = 0.0;
            Clear();
            double utilizable_rad, epsilon;
            // conversion from gC to kg Biomass: C/Biomass=0.5
            double gC_to_kg_biomass = 1.0 / (1000.0 * Constant.BiomassCFraction);
            for (int month = 0; month < 12; ++month)
            {
                utilizable_rad = CalculateUtilizablePar(month); // utilizable radiation of the month ... (MJ/m2)
                epsilon = CalculateEpsilon(month, model); // ... photosynthetic efficiency ... (gC/MJ)
                UtilizablePar[month] = utilizable_rad;
                Gpp[month] = utilizable_rad * epsilon * gC_to_kg_biomass; // ... results in GPP of the month kg Biomass/m2 (converted from gC/m2)
                year_raw_gpp += Gpp[month]; // kg Biomass/m2
            }

            // calculate f_env,yr: see http://iland.boku.ac.at/sapling+growth+and+competition
            double f_sum = 0.0;
            for (int i = 0; i < 12; i++)
            {
                f_sum += Gpp[i] / gC_to_kg_biomass; // == uAPar * epsilon_eff
            }

            //  the factor f_ref: parameter that scales response values to the range 0..1 (1 for best growth conditions) (species parameter)
            double perf_factor = SpeciesResponse.Species.SaplingGrowthParameters.ReferenceRatio;
            // f_env,yr=(uapar*epsilon_eff) / (APAR * epsilon_0 * fref)
            EnvironmentalFactor = f_sum / (model.ModelSettings.Epsilon * SpeciesResponse.YearlyRadiation * perf_factor);
            if (EnvironmentalFactor > 1.0)
            {
                if (EnvironmentalFactor > 1.5) // warning for large deviations
                {
                    Debug.WriteLine("WARNING: fEnvYear > 1 for " + SpeciesResponse.Species.ID + EnvironmentalFactor + " f_sum, epsilon, yearlyRad, refRatio " + f_sum + model.ModelSettings.Epsilon + SpeciesResponse.YearlyRadiation + perf_factor
                             + " check calibration of the sapReferenceRatio (fref) for this species!");
                }
                EnvironmentalFactor = 1.0;
            }

            // calculate fraction for belowground biomass
            RootFraction = 1.0 - AbovegroundFraction(model);

            // global value set?
            double dbg = model.GlobalSettings.Settings.ParamValue("gpp_per_year", 0);
            if (dbg > 0.0)
            {
                year_raw_gpp = dbg;
                RootFraction = 0.4;
            }

            // year GPP/rad: kg Biomass/m2
            GppPerArea = year_raw_gpp;
            return GppPerArea; // yearly GPP in kg Biomass/m2
        }
    }
}
