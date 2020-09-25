using iLand.core;
using iLand.tools;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.output
{
    internal class CarbonFlowOut : Output
    {
        private readonly Expression mCondition; // condition for landscape-level output
        private readonly Expression mConditionDetails; // condition for resource-unit-level output

        public CarbonFlowOut()
        {
            this.mCondition = new Expression();
            this.mConditionDetails = new Expression();

            Name = "Carbon fluxes per RU or landscape/yr";
            TableName = "carbonflow";
            Description = "Carbon fluxes per resource unit and year and/or aggregated for the full landscape. All values are reported on a per hectare basis (use the area provided in carbon or stand outputs to scale to realized values on the respective resource unit)." + 
                          "For results limited to the project area, the data values need to be scaled to the stockable area." + System.Environment.NewLine +
                          "For landsacpe level outputs, data is always given per ha of (stockable) project area (i.e. scaling with stockable area is already included)." + System.Environment.NewLine +
                          "Furthermore, the following sign convention is used in iLand: fluxes " + System.Environment.NewLine +
                          "from the atmosphere to the ecosystem are positive, while C leaving the ecosystem is reported as negative C flux." + System.Environment.NewLine +
                          "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " + 
                          "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                          "(leaving 'conditionRU' blank enables details per default).";

            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(new OutputColumn("area_ha", "total stockable area of the resource unit (or landscape) (ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("GPP", "actually realized gross primary production, kg C; ((primary production|GPP)) including " +
                                         "the effect of decreasing productivity with age; note that a rough estimate of " +
                                         "((sapling growth and competition|#sapling C and N dynamics|sapling GPP)) is added to the GPP of adult trees here.", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NPP", "net primary production, kg C; calculated as NPP=GPP-Ra; Ra, the autotrophic respiration (kg C/ha) is calculated as" +
                                         " a fixed fraction of GPP in iLand (see ((primary production|here)) for details). ", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("Rh", "heterotrophic respiration, kg C; sum of C released to the atmosphere from detrital pools, i.e." +
                                         " ((snag dynamics|#Snag decomposition|snags)), ((soil C and N cycling|downed deadwood, litter, and mineral soil)).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("dist_loss", "disturbance losses, kg C; C that leaves the ecosystem as a result of disturbances, e.g. fire consumption", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("mgmt_loss", "management losses, kg C; C that leaves the ecosystem as a result of management interventions, e.g. harvesting", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NEP", "net ecosytem productivity kg C, NEP=NPP - Rh - disturbance losses - management losses. " +
                                         "Note that NEP is also equal to the total net changes over all ecosystem C pools, as reported in the " +
                                         "carbon output (cf. [http://www.jstor.org/stable/3061028|Randerson et al. 2002])", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("cumNPP", "cumulative NPP, kg C. This is a running sum of NPP (including tree NPP and sapling carbon gain).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("cumRh", "cumulative flux to atmosphere (heterotrophic respiration), kg C. This is a running sum of Rh.", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("cumNEP", "cumulative NEP (net ecosystem productivity), kg C. This is a running sum of NEP (positive values: carbon gain, negative values: carbon loss).", OutputDatatype.OutDouble));
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);

            condition = Settings().Value(".conditionRU", "");
            mConditionDetails.SetExpression(condition);
        }

        public override void Exec()
        {
            Model m = GlobalSettings.Instance.Model;

            // global condition
            if (!mCondition.IsEmpty && mCondition.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                return;
            }
            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mConditionDetails.IsEmpty && mConditionDetails.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                ru_level = false;
            }

            int ru_count = 0;
            List<double> v = new List<double>(10); // 11? data values per RU
            foreach (ResourceUnit ru in m.ResourceUnits) 
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                if (ru.Snags == null || ru.Snags == null)
                {
                    Debug.WriteLine("exec: resource unit without soil or snags module - no output generated.");
                    continue;
                }


                double npp = 0.0;
                double area_factor = ru.StockableArea / Constant.RUArea; //conversion factor
                npp += ru.Statistics.Npp * Constant.BiomassCFraction; // kg C/ha
                npp += ru.Statistics.NppSaplings * Constant.BiomassCFraction; // kgC/ha
                                                                          // Snag pools are not scaled per ha (but refer to the stockable RU), soil pools and biomass statistics (NPP, ...) are scaled.
                double to_atm = ru.Snags.FluxToAtmosphere.C / area_factor; // from snags, kg/ha
                to_atm += ru.Snags.FluxToAtmosphere.C * Constant.RUArea / 10.0; // soil: t/ha -> t/m2 -> kg/ha

                double to_dist = ru.Snags.FluxToDisturbance.C / area_factor; // convert to kgC/ha
                to_dist += ru.Snags.FluxToDisturbance.C * Constant.RUArea / 10.0; // kgC/ha

                double to_harvest = ru.Snags.FluxToExtern.C / area_factor; // kgC/ha

                double nep = npp - to_atm - to_harvest - to_dist; // kgC/ha

                if (ru_level)
                {
                    // keys
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(area_factor);
                    this.Add(npp / Constant.AutotrophicRespiration); // GPP_act
                    this.Add(npp); // NPP
                    this.Add(-to_atm); // rh
                    this.Add(-to_dist); // disturbance
                    this.Add(-to_harvest); // management loss
                    this.Add(nep); // nep
                    this.Add(ru.Variables.CumCarbonUptake);
                    this.Add(ru.Variables.CumCarbonToAtm);
                    this.Add(ru.Variables.CumNep);

                    WriteRow();
                }
                // landscape level
                ++ru_count;

                v.Add(area_factor); // total area in ha
                v.Add(npp / Constant.AutotrophicRespiration * area_factor); // GPP_act
                v.Add(npp * area_factor); // NPP
                v.Add(-to_atm * area_factor); // rh
                v.Add(-to_dist * area_factor); // disturbance
                v.Add(-to_harvest * area_factor); // management loss
                v.Add(nep * area_factor); // net ecosystem productivity
                v.Add(ru.Variables.CumCarbonUptake * area_factor); // cum. NPP
                v.Add(ru.Variables.CumCarbonToAtm * area_factor); // cum. Rh
                v.Add(ru.Variables.CumNep * area_factor); // cum. NEP
            }

            // write landscape sums
            // BUGBUG: C++ appars to only behave correctly for single RU case
            double total_stockable_area = v[0]; // total ha of stockable area
            if (ru_count == 0.0 || total_stockable_area == 0.0)
            {
                return;
            }
            this.Add(CurrentYear());
            this.Add(-1);
            this.Add(-1); // codes -1/-1 for landscape level
            this.Add(v[0]); // stockable area [m2]
            for (int i = 1; i < v.Count; ++i)
            {
                this.Add(v[i] / total_stockable_area);
            }
            WriteRow();
        }
    }
}
