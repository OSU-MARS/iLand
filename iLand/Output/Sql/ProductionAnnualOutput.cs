using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output.Sql
{
    /** ProductionOut describes finegrained production details on the level of resourceunits per month. */
    public class ProductionAnnualOutput : AnnualOutput
    {
        public ProductionAnnualOutput()
        {
            this.Name = "Production per month, species and resource unit";
            this.TableName = "productionMonth";
            this.Description = "Details about the 3-PG production submodule on monthly basis and for each species and resource unit.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new("month", "month of year", SqliteType.Integer));
            this.Columns.Add(new("tempResponse", "monthly average of daily respose value temperature", SqliteType.Real));
            this.Columns.Add(new("waterResponse", "monthly average of daily respose value soil water", SqliteType.Real));
            this.Columns.Add(new("vpdResponse", "monthly vapour pressure deficit respose.", SqliteType.Real));
            this.Columns.Add(new("co2Response", "monthly response value for ambient co2.", SqliteType.Real));
            this.Columns.Add(new("nitrogenResponse", "yearly respose value nitrogen", SqliteType.Real));
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
                    for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
                    {
                        insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                        insertRow.Parameters[1].Value = ruSpecies.ResourceUnit.ResourceUnitGridIndex;
                        insertRow.Parameters[2].Value = ruSpecies.ResourceUnit.ID;
                        insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                        insertRow.Parameters[4].Value = monthIndex + 1; // month
                        insertRow.Parameters[5].Value = growthModifiers.TemperatureModifierByMonth[monthIndex];
                        insertRow.Parameters[6].Value = growthModifiers.SoilWaterModifierByMonth[monthIndex];
                        insertRow.Parameters[7].Value = growthModifiers.VpdModifierByMonth[monthIndex];
                        insertRow.Parameters[8].Value = growthModifiers.CO2ModifierByMonth[monthIndex];
                        insertRow.Parameters[9].Value = growthModifiers.NitrogenModifierForYear;
                        insertRow.Parameters[10].Value = growthModifiers.GlobalRadiationByMonth[monthIndex];
                        insertRow.Parameters[11].Value = growth.UtilizablePar[monthIndex];
                        insertRow.Parameters[12].Value = growth.MonthlyGpp[monthIndex];
                        insertRow.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
