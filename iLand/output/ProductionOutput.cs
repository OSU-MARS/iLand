using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    /** ProductionOut describes finegrained production details on the level of resourceunits per month. */
    internal class ProductionOutput : Output
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
            this.Columns.Add(new SqlColumn("month", "month of year", OutputDatatype.OutInteger));
            this.Columns.Add(new SqlColumn("tempResponse", "monthly average of daily respose value temperature", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("waterResponse", "monthly average of daily respose value soil water", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("vpdResponse", "monthly vapour pressure deficit respose.", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("co2Response", "monthly response value for ambient co2.", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("nitrogenResponse", "yearly respose value nitrogen", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("radiation_m2", "global radiation PAR in MJ per m2 and month", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", OutputDatatype.OutDouble));
            this.Columns.Add(new SqlColumn("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", OutputDatatype.OutDouble));
        }

        private void LogYear(ResourceUnitSpecies rus, SqliteCommand insertRow)
        {
            Production3PG prod = rus.BiomassGrowth;
            SpeciesResponse resp = prod.SpeciesResponse;
            for (int i = 0; i < 12; i++)
            {
                this.Add(CurrentYear());
                this.Add(rus.RU.Index);
                this.Add(rus.RU.ID);
                this.Add(rus.Species.ID);
                this.Add(i + 1); // month
                // responses
                this.Add(resp.TempResponse[i]);
                this.Add(resp.SoilWaterResponse[i]);
                this.Add(resp.VpdResponse[i]);
                this.Add(resp.Co2Response[i]);
                this.Add(resp.NitrogenResponse);
                this.Add(resp.GlobalRadiation[i]);
                this.Add(prod.mUPAR[i]);
                this.Add(prod.mGPP[i]);
                this.WriteRow(insertRow);
            }
        }

        protected override void LogYear(SqliteCommand insertRow)
        {
            using DebugTimer t = new DebugTimer("ProductionOutput.LogYear()");
            Model m = GlobalSettings.Instance.Model;

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    this.LogYear(rus, insertRow);
                }
            }
        }
    }
}
