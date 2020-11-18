using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    /** ProductionOut describes finegrained production details on the level of resourceunits per month. */
    public class ProductionOutput : Output
    {
        public ProductionOutput()
        {
            this.Name = "Production per month, species and resource unit";
            this.TableName = "productionMonth";
            this.Description = "Details about the 3PG production submodule on monthly basis and for each species and resource unit.";
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
            //using DebugTimer t = model.DebugTimers.Create("ProductionOutput.LogYear()");
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeSpeciesGrowth growth = ruSpecies.BiomassGrowth;
                    ResourceUnitTreeSpeciesResponse speciesResponse = growth.SpeciesResponse;
                    for (int month = 0; month < Constant.MonthsInYear; ++month)
                    {
                        insertRow.Parameters[0].Value = model.CurrentYear;
                        insertRow.Parameters[1].Value = ruSpecies.RU.ResourceUnitGridIndex;
                        insertRow.Parameters[2].Value = ruSpecies.RU.EnvironmentID;
                        insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                        insertRow.Parameters[4].Value = month + 1; // month
                                                                   // responses
                        insertRow.Parameters[5].Value = speciesResponse.TempResponseByMonth[month];
                        insertRow.Parameters[6].Value = speciesResponse.SoilWaterResponseByMonth[month];
                        insertRow.Parameters[7].Value = speciesResponse.VpdResponseByMonth[month];
                        insertRow.Parameters[8].Value = speciesResponse.CO2ResponseByMonth[month];
                        insertRow.Parameters[9].Value = speciesResponse.NitrogenResponseForYear;
                        insertRow.Parameters[10].Value = speciesResponse.GlobalRadiationByMonth[month];
                        insertRow.Parameters[11].Value = growth.UtilizablePar[month];
                        insertRow.Parameters[12].Value = growth.MonthlyGpp[month];
                        insertRow.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
