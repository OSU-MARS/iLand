using iLand.Simulation;
using iLand.Tools;
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
            Name = "Production per month, species and resource unit";
            TableName = "production_month";
            Description = "Details about the 3PG production submodule on monthly basis and for each species and resource unit.";
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

        private void LogYear(Model model, ResourceUnitSpecies rus, SqliteCommand insertRow)
        {
            Production3PG prod = rus.BiomassGrowth;
            SpeciesResponse resp = prod.SpeciesResponse;
            for (int i = 0; i < 12; i++)
            {
                insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                insertRow.Parameters[1].Value = rus.RU.Index;
                insertRow.Parameters[2].Value = rus.RU.ID;
                insertRow.Parameters[3].Value = rus.Species.ID;
                insertRow.Parameters[4].Value = i + 1; // month
                // responses
                insertRow.Parameters[5].Value = resp.TempResponse[i];
                insertRow.Parameters[6].Value = resp.SoilWaterResponse[i];
                insertRow.Parameters[7].Value = resp.VpdResponse[i];
                insertRow.Parameters[8].Value = resp.Co2Response[i];
                insertRow.Parameters[9].Value = resp.NitrogenResponse;
                insertRow.Parameters[10].Value = resp.GlobalRadiation[i];
                insertRow.Parameters[11].Value = prod.UtilizablePar[i];
                insertRow.Parameters[12].Value = prod.MonthlyGpp[i];
                insertRow.ExecuteNonQuery();
            }
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            //using DebugTimer t = model.DebugTimers.Create("ProductionOutput.LogYear()");
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    this.LogYear(model, rus, insertRow);
                }
            }
        }
    }
}
