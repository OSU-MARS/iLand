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
            this.Columns.Add(new("month", "month of year", SqliteType.Integer));
            this.Columns.Add(new("tempModifier", "monthly average of daily modifier value temperature", SqliteType.Real));
            this.Columns.Add(new("waterModifier", "monthly average of daily modifier value soil water", SqliteType.Real));
            this.Columns.Add(new("vpdModifier", "monthly vapour pressure deficit modifier.", SqliteType.Real));
            this.Columns.Add(new("co2Modifier", "monthly response value for ambient co2.", SqliteType.Real));
            this.Columns.Add(new("nitrogenModifier", "yearly modifier value nitrogen", SqliteType.Real));
            this.Columns.Add(new("radiation_m2", "global radiation PAR in MJ per m2 and month", SqliteType.Real));
            this.Columns.Add(new("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", SqliteType.Real));
            this.Columns.Add(new("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeSpeciesGrowth growth = ruSpecies.TreeGrowth;
                    ResourceUnitTreeSpeciesGrowthModifiers growthModifiers = growth.Modifiers;
                    for (int monthIndex = 0; monthIndex < Constant.Time.MonthsInYear; ++monthIndex)
                    {
                        insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                        insertRow.Parameters[1].Value = ruSpecies.ResourceUnit.ID;
                        insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID;
                        insertRow.Parameters[3].Value = monthIndex + 1; // month
                        insertRow.Parameters[4].Value = growthModifiers.TemperatureModifierByMonth[monthIndex];
                        insertRow.Parameters[5].Value = growthModifiers.SoilWaterModifierByMonth[monthIndex];
                        insertRow.Parameters[6].Value = growthModifiers.VpdModifierByMonth[monthIndex];
                        insertRow.Parameters[7].Value = growthModifiers.CO2ModifierByMonth[monthIndex];
                        insertRow.Parameters[8].Value = growthModifiers.NitrogenModifierForYear;
                        insertRow.Parameters[9].Value = growthModifiers.SolarRadiationTotalByMonth[monthIndex];
                        insertRow.Parameters[10].Value = growth.UtilizableParByMonth[monthIndex];
                        insertRow.Parameters[11].Value = growth.MonthlyGpp[monthIndex];
                        insertRow.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
