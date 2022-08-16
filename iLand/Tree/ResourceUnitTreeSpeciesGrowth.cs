using iLand.Input.ProjectFile;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    public class ResourceUnitTreeSpeciesGrowth
    {
        //  GPP production (yearly) (kg Biomass) per m² (effective area)
        public float AnnualGpp { get; private set; }
        // species specific responses to abiotic environment
        public ResourceUnitTreeSpeciesGrowthModifiers Modifiers { get; private set; }
        // monthly Gross Primary Production [kg Biomass / m²]
        public float[] MonthlyGpp { get; private init; }
        /// fraction of biomass that should be distributed to roots
        public float RootFraction { get; private set; }
        // f_env,yr: aggregate environmental factor [0..1}
        // f_env,yr: factor that aggregates the environment for the species over the year (weighted with the radiation pattern)
        public float SiteEnvironmentSaplingHeightGrowthMultiplier { get; private set; }
        // utilizable radiation MJ/m² and month
        public float[] UtilizablePar { get; private init; }

        public ResourceUnitTreeSpeciesGrowth(ResourceUnit resourceUnit, ResourceUnitTreeSpecies ruSpecies)
        {
            this.AnnualGpp = 0.0F;
            this.Modifiers = new(resourceUnit, ruSpecies);
            this.MonthlyGpp = new float[Constant.MonthsInYear];
            this.RootFraction = 0.0F;
            this.SiteEnvironmentSaplingHeightGrowthMultiplier = 0.0F;
            this.UtilizablePar = new float[Constant.MonthsInYear];
        }

        /** calculate a resource unit's GPP
          ResourceUnit-level production following the 3-PG approach from Landsberg and Waring.
          @sa http://iland-model.org/primary+production */
        public void CalculateGppForYear(Project projectFile)
        {
            this.ZeroMonthlyAndAnnualValues();

            // Radiation: sum over all days of each month with foliage
            // conversion from gC to kg Biomass: C/Biomass=0.5
            float resourceUnitGppForYear = 0.0F;
            const float gramsCarbonToKilogramsBiomass = 0.001F / Constant.DryBiomassCarbonFraction;
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                // This is based on the utilizable photosynthetic active radiation.
                // http://iland-model.org/primary+production
                // calculate the available radiation. This is done in ResourceUnitTreeSpeciesGrowthModifiers.CalculateMonthlyGrowthModifiers()
                // see Equation (3)
                // multiplicative approach: responses are averaged one by one and multiplied on a monthly basis
                //    float utilizableRadiation = this.Modifiers.absorbedRadiation[month] *
                //                                this.Modifiers.vpdModifier[month] *
                //                                this.Modifiers.soilWaterModifier[month] *
                //                                this.Modifiers.tempModifier[month];
                // minimum approach: for each day the minimum of vpd, temp, and soil water is calculated, then averaged for each month
                //   float response = this.Modifiers.absorbedRadiation[month] *
                //                    this.Modifiers.minimumModifiers[month];
                float utilizableRadiation = this.Modifiers.UtilizableRadiationByMonth[monthIndex]; // utilizable radiation of the month ... (MJ/m2)
                // calculate the alphac (=photosynthetic efficiency) for the given month, gC/MJ radiation
                //  this is based on a global efficiency, and modified per species
                // maximum radiation use efficiency
                float epsilon = projectFile.Model.Ecosystem.LightUseEpsilon * this.Modifiers.NitrogenModifierForYear * this.Modifiers.CO2ModifierByMonth[monthIndex];

                this.UtilizablePar[monthIndex] = utilizableRadiation;
                this.MonthlyGpp[monthIndex] = utilizableRadiation * epsilon * gramsCarbonToKilogramsBiomass; // ... results in GPP of the month kg Biomass/m2 (converted from gC/m2)
                resourceUnitGppForYear += this.MonthlyGpp[monthIndex]; // kg biomass/m2

                Debug.Assert(this.MonthlyGpp[monthIndex] >= 0.0);
            }

            // calculate f_env,yr: see http://iland-model.org/sapling+growth+and+competition
            float f_sum = 0.0F;
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                f_sum += this.MonthlyGpp[month] / gramsCarbonToKilogramsBiomass; // == uAPar * epsilon_eff
            }

            // the factor f_ref: parameter that scales response values to the range 0..1 (1 for best growth conditions) (species parameter)
            float siteEnvironmentHeightDivisor = this.Modifiers.Species.SaplingGrowth.ReferenceRatio;
            // f_env,yr=(uapar*epsilon_eff) / (APAR * epsilon_0 * fref)
            this.SiteEnvironmentSaplingHeightGrowthMultiplier = f_sum / (projectFile.Model.Ecosystem.LightUseEpsilon * this.Modifiers.TotalRadiationForYear * siteEnvironmentHeightDivisor);
            if (this.SiteEnvironmentSaplingHeightGrowthMultiplier > 1.0F)
            {
                if (this.SiteEnvironmentSaplingHeightGrowthMultiplier > 1.5F) // error on large deviations TODO: why a threshold of 1.5 instead of ~1.000001?
                {
                    throw new NotSupportedException("fEnvYear > 1 for " + this.Modifiers.Species.ID + this.SiteEnvironmentSaplingHeightGrowthMultiplier + " f_sum, epsilon, yearlyRad, refRatio " + f_sum + projectFile.Model.Ecosystem.LightUseEpsilon + this.Modifiers.TotalRadiationForYear + siteEnvironmentHeightDivisor
                             + " check calibration of the sapReferenceRatio (fref) for this species!");
                }
                this.SiteEnvironmentSaplingHeightGrowthMultiplier = 1.0F;
            }

            // calculate fraction for belowground biomass
            float utilizedRadiationFraction = 1.0F;
            if (projectFile.Model.Settings.UseParFractionBelowGroundAllocation)
            {
                // the Landsberg & Waring formulation takes into account the fraction of utilizeable to total radiation (but more complicated)
                // we originally used only nitrogen and added the U_utilized/U_radiation
                utilizedRadiationFraction = this.Modifiers.UtilizableRadiationForYear / this.Modifiers.TotalRadiationForYear;
            }
            float abovegroundFraction = 1.0F - 0.8F / (1.0F + 2.5F * this.Modifiers.NitrogenModifierForYear * utilizedRadiationFraction);
            this.RootFraction = 1.0F - abovegroundFraction;

            // global value set?
            float gppOverride = projectFile.Model.Settings.OverrideGppPerYear;
            if (Single.IsNaN(gppOverride) == false)
            {
                resourceUnitGppForYear = gppOverride;
                this.RootFraction = 0.4F; // TODO: why is this triggered by a GPP override?
            }

            // yearly GPP in kg biomass/m²
            this.AnnualGpp = resourceUnitGppForYear;
        }

        public void ZeroMonthlyAndAnnualValues()
        {
            Array.Fill(this.MonthlyGpp, 0.0F);
            Array.Fill(this.UtilizablePar, 0.0F);

            this.AnnualGpp = 0.0F;
            this.RootFraction = 0.0F;
            this.SiteEnvironmentSaplingHeightGrowthMultiplier = 0.0F;
            // BUGBUG: shouldn't growth modifiers also be zeroed at this point for encapsulation?
        }
    }
}
