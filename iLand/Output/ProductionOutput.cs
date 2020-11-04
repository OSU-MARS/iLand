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
            this.TableName = "production_month";
            this.Description = "Details about the 3PG production submodule on monthly basis and for each species and resource unit.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("month", "month of year", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("tempResponse", "monthly average of daily respose value temperature", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("waterResponse", "monthly average of daily respose value soil water", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("vpdResponse", "monthly vapour pressure deficit respose.", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("co2Response", "monthly response value for ambient co2.", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("nitrogenResponse", "yearly respose value nitrogen", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("radiation_m2", "global radiation PAR in MJ per m2 and month", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", OutputDatatype.Double));
        }

        private void LogYear(Model model, ResourceUnitSpecies ruSpecies, SqliteCommand insertRow)
        {
            ResourceUnitSpeciesGrowth growth = ruSpecies.BiomassGrowth;
            ResourceUnitSpeciesResponse speciesResponse = growth.SpeciesResponse;
            for (int month = 0; month < 12; month++)
            {
                insertRow.Parameters[0].Value = model.CurrentYear;
                insertRow.Parameters[1].Value = ruSpecies.RU.GridIndex;
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

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            //using DebugTimer t = model.DebugTimers.Create("ProductionOutput.LogYear()");
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.TreeSpecies)
                {
                    this.LogYear(model, rus, insertRow);
                }
            }
        }
    }
}
