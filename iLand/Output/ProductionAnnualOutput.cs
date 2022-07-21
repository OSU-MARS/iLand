using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
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
            this.Columns.Add(new SqlColumn("month", "month of year", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("tempResponse", "monthly average of daily respose value temperature", SqliteType.Real));
            this.Columns.Add(new SqlColumn("waterResponse", "monthly average of daily respose value soil water", SqliteType.Real));
            this.Columns.Add(new SqlColumn("vpdResponse", "monthly vapour pressure deficit respose.", SqliteType.Real));
            this.Columns.Add(new SqlColumn("co2Response", "monthly response value for ambient co2.", SqliteType.Real));
            this.Columns.Add(new SqlColumn("nitrogenResponse", "yearly respose value nitrogen", SqliteType.Real));
            this.Columns.Add(new SqlColumn("radiation_m2", "global radiation PAR in MJ per m2 and month", SqliteType.Real));
            this.Columns.Add(new SqlColumn("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", SqliteType.Real));
            this.Columns.Add(new SqlColumn("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeSpeciesGrowth growth = ruSpecies.TreeGrowth;
                    ResourceUnitTreeSpeciesGrowthModifiers growthModifiers = growth.Modifiers;
                    for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
                    {
                        insertRow.Parameters[0].Value = model.CurrentYear;
                        insertRow.Parameters[1].Value = ruSpecies.RU.ResourceUnitGridIndex;
                        insertRow.Parameters[2].Value = ruSpecies.RU.ID;
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
