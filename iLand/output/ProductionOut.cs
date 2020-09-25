using iLand.core;
using iLand.tools;

namespace iLand.output
{
    /** ProductionOut describes finegrained production details on the level of resourceunits per month. */
    internal class ProductionOut : Output
    {
        public ProductionOut()
        {
            Name = "Production per month, species and resource unit";
            TableName = "production_month";
            Description = "Details about the 3PG production submodule on monthly basis and for each species and resource unit.";
            this.Columns.Add(OutputColumn.CreateYear());
            this.Columns.Add(OutputColumn.CreateResourceUnit());
            this.Columns.Add(OutputColumn.CreateID());
            this.Columns.Add(OutputColumn.CreateSpecies());
            this.Columns.Add(new OutputColumn("month", "month of year", OutputDatatype.OutInteger));
            this.Columns.Add(new OutputColumn("tempResponse", "monthly average of daily respose value temperature", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("waterResponse", "monthly average of daily respose value soil water", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("vpdResponse", "monthly vapour pressure deficit respose.", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("co2Response", "monthly response value for ambient co2.", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("nitrogenResponse", "yearly respose value nitrogen", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("radiation_m2", "global radiation PAR in MJ per m2 and month", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", OutputDatatype.OutDouble));
            this.Columns.Add(new OutputColumn("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", OutputDatatype.OutDouble));
        }

        public void Execute(ResourceUnitSpecies rus)
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
                WriteRow();
            }
        }

        public override void Exec()
        {
            using DebugTimer t = new DebugTimer("ProductionOut");
            Model m = GlobalSettings.Instance.Model;

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    Execute(rus);
                }
            }
        }
    }
}
