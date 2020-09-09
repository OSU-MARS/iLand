using iLand.core;
using iLand.tools;

namespace iLand.output
{
    /** ProductionOut describes finegrained production details on the level of resourceunits per month. */
    internal class ProductionOut : Output
    {
        public ProductionOut()
        {
            setName("Production per month, species and resource unit", "production_month");
            setDescription("Details about the 3PG production submodule on monthly basis and for each species and resource unit.");
            this.columns().Add(OutputColumn.year());
            this.columns().Add(OutputColumn.ru());
            this.columns().Add(OutputColumn.id());
            this.columns().Add(OutputColumn.species());
            this.columns().Add(new OutputColumn("month", "month of year", OutputDatatype.OutInteger));
            this.columns().Add(new OutputColumn("tempResponse", "monthly average of daily respose value temperature", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("waterResponse", "monthly average of daily respose value soil water", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("vpdResponse", "monthly vapour pressure deficit respose.", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("co2Response", "monthly response value for ambient co2.", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("nitrogenResponse", "yearly respose value nitrogen", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("radiation_m2", "global radiation PAR in MJ per m2 and month", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("utilizableRadiation_m2", "utilizable PAR in MJ per m2 and month (sum of daily rad*min(respVpd,respWater,respTemp))", OutputDatatype.OutDouble));
            this.columns().Add(new OutputColumn("GPP_kg_m2", "GPP (without Aging) in kg Biomass/m2", OutputDatatype.OutDouble));
        }

        public void execute(ResourceUnitSpecies rus)
        {
            Production3PG prod = rus.prod3PG();
            SpeciesResponse resp = prod.mResponse;
            for (int i = 0; i < 12; i++)
            {
                this.add(currentYear());
                this.add(rus.ru().index());
                this.add(rus.ru().id());
                this.add(rus.species().id());
                this.add(i + 1); // month
                // responses
                this.add(resp.tempResponse()[i]);
                this.add(resp.soilWaterResponse()[i]);
                this.add(resp.vpdResponse()[i]);
                this.add(resp.co2Response()[i]);
                this.add(resp.nitrogenResponse());
                this.add(resp.globalRadiation()[i]);
                this.add(prod.mUPAR[i]);
                this.add(prod.mGPP[i]);
                writeRow();
            }
        }

        public override void exec()
        {
            using DebugTimer t = new DebugTimer("ProductionOut");
            Model m = GlobalSettings.instance().model();

            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.ruSpecies())
                {
                    execute(rus);
                }
            }
        }
    }
}
