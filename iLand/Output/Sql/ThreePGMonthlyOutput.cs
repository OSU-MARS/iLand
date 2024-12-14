// C++/output/{ productionout.h, productionout.cpp }
using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output.Sql
{
    // 3-PG monthly timesteps by resource unit tree species, triggered at simulation year end to write all 12 months in the calendar year
    public class ThreePGMonthlyOutput : AnnualOutput
    {
        public ThreePGMonthlyOutput()
        {
            this.Name = "3-PG monthly timesteps by resource unit tree species";
            this.TableName = "monthly3PG";
            this.Description = "Details about the 3-PG production submodule on monthly basis and for each tree species on each resource unit.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("month", "Month of year.", SqliteType.Integer));
            this.Columns.Add(new("CO2_beta", "Monthly value for effective beta (CO₂ fertilization). β = β₀ * fN * (2-fSW)", SqliteType.Real));
            this.Columns.Add(new("phenology", "Proportion of the month (0..1) that is within the vegetation period (and thus it is assumed that leaves are out).", SqliteType.Real));
            this.Columns.Add(new("tempModifier", "Monthly average of daily modifier value temperature", SqliteType.Real));
            this.Columns.Add(new("waterModifier", "Monthly average of daily modifier value soil water", SqliteType.Real));
            this.Columns.Add(new("vpdModifier", "Monthly vapour pressure deficit modifier.", SqliteType.Real));
            this.Columns.Add(new("co2Modifier", "Monthly response value for ambient co2.", SqliteType.Real));
            this.Columns.Add(new("nitrogenModifier", "Yearly modifier value nitrogen", SqliteType.Real));
            this.Columns.Add(new("radiation_m2", "Global photosynthetically active radiation (PAR), MJ/m².", SqliteType.Real));
            this.Columns.Add(new("utilizableRadiation_m2", "Utilizable PAR, MJ/m² (sum of daily rad*min(respVpd,respWater,respTemp)).", SqliteType.Real));
            this.Columns.Add(new("GPP_kg_m2", "GPP (without aging) in kg biomass/m².", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeSpeciesGrowth growth = ruSpecies.TreeGrowth;
                    ResourceUnitTreeSpeciesGrowthModifiers growthModifiers = growth.Modifiers;
                    LeafPhenology phenology = resourceUnit.Weather.GetPhenology(ruSpecies.Species.LeafPhenologyID);

                    for (int monthIndex = 0; monthIndex < Constant.Time.MonthsInYear; ++monthIndex)
                    {
                        float atmosphericCO2 = model.Landscape.CO2ByMonth.CO2ConcentrationInPpm[monthIndex];
                        float soilWaterModifier = growthModifiers.SoilWaterModifierByMonth[monthIndex];
                        insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                        insertRow.Parameters[1].Value = ruSpecies.ResourceUnit.ID;
                        insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID;
                        insertRow.Parameters[3].Value = monthIndex + 1; // month
                        insertRow.Parameters[4].Value = ruSpecies.Species.SpeciesSet.GetCarbonDioxideModifier(atmosphericCO2, growthModifiers.NitrogenModifierForYear, soilWaterModifier);
                        insertRow.Parameters[5].Value = phenology.LeafOnFractionByMonth[monthIndex];
                        insertRow.Parameters[6].Value = growthModifiers.TemperatureModifierByMonth[monthIndex];
                        insertRow.Parameters[7].Value = soilWaterModifier;
                        insertRow.Parameters[8].Value = growthModifiers.VpdModifierByMonth[monthIndex];
                        insertRow.Parameters[9].Value = growthModifiers.CO2ModifierByMonth[monthIndex];
                        insertRow.Parameters[10].Value = growthModifiers.NitrogenModifierForYear;
                        insertRow.Parameters[11].Value = growthModifiers.SolarRadiationTotalByMonth[monthIndex];
                        insertRow.Parameters[12].Value = growth.UtilizableParByMonth[monthIndex];
                        insertRow.Parameters[13].Value = growth.MonthlyGpp[monthIndex];
                        insertRow.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
