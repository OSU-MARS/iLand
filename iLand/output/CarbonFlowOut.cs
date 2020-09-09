using iLand.core;
using iLand.tools;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.output
{
    internal class CarbonFlowOut : Output
    {
        private Expression mCondition; // condition for landscape-level output
        private Expression mConditionDetails; // condition for resource-unit-level output

        public CarbonFlowOut()
        {
            setName("Carbon fluxes per RU or landscape/yr", "carbonflow");
            setDescription("Carbon fluxes per resource unit and year and/or aggregated for the full landscape. All values are reported on a per hectare basis (use the area provided in carbon or stand outputs to scale to realized values on the respective resource unit)." + 
                "For results limited to the project area, the data values need to be scaled to the stockable area." + System.Environment.NewLine +
                "For landsacpe level outputs, data is always given per ha of (stockable) project area (i.e. scaling with stockable area is already included)." + System.Environment.NewLine +
                "Furthermore, the following sign convention is used in iLand: fluxes " + System.Environment.NewLine +
                "from the atmosphere to the ecosystem are positive, while C leaving the ecosystem is reported as negative C flux." + System.Environment.NewLine +
                "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " + 
                "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                "(leaving 'conditionRU' blank enables details per default).");

            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(new OutputColumn("area_ha", "total stockable area of the resource unit (or landscape) (ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("GPP", "actually realized gross primary production, kg C; ((primary production|GPP)) including " +
                                           "the effect of decreasing productivity with age; note that a rough estimate of " +
                                           "((sapling growth and competition|#sapling C and N dynamics|sapling GPP)) is added to the GPP of adult trees here.", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NPP", "net primary production, kg C; calculated as NPP=GPP-Ra; Ra, the autotrophic respiration (kg C/ha) is calculated as" +
                                           " a fixed fraction of GPP in iLand (see ((primary production|here)) for details). ", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("Rh", "heterotrophic respiration, kg C; sum of C released to the atmosphere from detrital pools, i.e." +
                                           " ((snag dynamics|#Snag decomposition|snags)), ((soil C and N cycling|downed deadwood, litter, and mineral soil)).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("dist_loss", "disturbance losses, kg C; C that leaves the ecosystem as a result of disturbances, e.g. fire consumption", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("mgmt_loss", "management losses, kg C; C that leaves the ecosystem as a result of management interventions, e.g. harvesting", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NEP", "net ecosytem productivity kg C, NEP=NPP - Rh - disturbance losses - management losses. " +
                                           "Note that NEP is also equal to the total net changes over all ecosystem C pools, as reported in the " +
                                           "carbon output (cf. [http://www.jstor.org/stable/3061028|Randerson et al. 2002])", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("cumNPP", "cumulative NPP, kg C. This is a running sum of NPP (including tree NPP and sapling carbon gain).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("cumRh", "cumulative flux to atmosphere (heterotrophic respiration), kg C. This is a running sum of Rh.", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("cumNEP", "cumulative NEP (net ecosystem productivity), kg C. This is a running sum of NEP (positive values: carbon gain, negative values: carbon loss).", OutputDatatype.OutDouble));
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);

            condition = settings().value(".conditionRU", "");
            mConditionDetails.setExpression(condition);
        }


        public override void exec()
        {
            Model m = GlobalSettings.instance().model();

            // global condition
            if (!mCondition.isEmpty() && mCondition.calculate(GlobalSettings.instance().currentYear()) == 0.0)
            {
                return;
            }
            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mConditionDetails.isEmpty() && mConditionDetails.calculate(GlobalSettings.instance().currentYear()) == 0.0)
            {
                ru_level = false;
            }

            double npp = 0.0;
            int ru_count = 0;
            List<double> v = new List<double>(10); // 11? data values per RU
            foreach (ResourceUnit ru in m.ruList()) 
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }
                if (ru.soil() == null || ru.snag() == null)
                {
                    Debug.WriteLine("exec: resource unit without soil or snags module - no output generated.");
                    continue;
                }


                npp = 0.0;
                double area_factor = ru.stockableArea() / Constant.cRUArea; //conversion factor
                npp += ru.statistics().npp() * Constant.biomassCFraction; // kg C/ha
                npp += ru.statistics().nppSaplings() * Constant.biomassCFraction; // kgC/ha
                                                                          // Snag pools are not scaled per ha (but refer to the stockable RU), soil pools and biomass statistics (NPP, ...) are scaled.
                double to_atm = ru.snag().fluxToAtmosphere().C / area_factor; // from snags, kg/ha
                to_atm += ru.soil().fluxToAtmosphere().C * Constant.cRUArea / 10.0; // soil: t/ha -> t/m2 -> kg/ha

                double to_dist = ru.snag().fluxToDisturbance().C / area_factor; // convert to kgC/ha
                to_dist += ru.soil().fluxToDisturbance().C * Constant.cRUArea / 10.0; // kgC/ha

                double to_harvest = ru.snag().fluxToExtern().C / area_factor; // kgC/ha

                double nep = npp - to_atm - to_harvest - to_dist; // kgC/ha

                if (ru_level)
                {
                    // keys
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(area_factor);
                    this.add(npp / Constant.cAutotrophicRespiration); // GPP_act
                    this.add(npp); // NPP
                    this.add(-to_atm); // rh
                    this.add(-to_dist); // disturbance
                    this.add(-to_harvest); // management loss
                    this.add(nep); // nep
                    this.add(ru.resouceUnitVariables().cumCarbonUptake);
                    this.add(ru.resouceUnitVariables().cumCarbonToAtm);
                    this.add(ru.resouceUnitVariables().cumNEP);

                    writeRow();
                }
                // landscape level
                ++ru_count;

                v.Add(area_factor); // total area in ha
                v.Add(npp / Constant.cAutotrophicRespiration * area_factor); // GPP_act
                v.Add(npp * area_factor); // NPP
                v.Add(-to_atm * area_factor); // rh
                v.Add(-to_dist * area_factor); // disturbance
                v.Add(-to_harvest * area_factor); // management loss
                v.Add(nep * area_factor); // net ecosystem productivity
                v.Add(ru.resouceUnitVariables().cumCarbonUptake * area_factor); // cum. NPP
                v.Add(ru.resouceUnitVariables().cumCarbonToAtm * area_factor); // cum. Rh
                v.Add(ru.resouceUnitVariables().cumNEP * area_factor); // cum. NEP
            }

            // write landscape sums
            // BUGBUG: C++ appars to only behave correctly for single RU case
            double total_stockable_area = v[0]; // total ha of stockable area
            if (ru_count == 0.0 || total_stockable_area == 0.0)
            {
                return;
            }
            this.add(currentYear());
            this.add(-1);
            this.add(-1); // codes -1/-1 for landscape level
            this.add(v[0]); // stockable area [m2]
            for (int i = 1; i < v.Count; ++i)
            {
                this.add(v[i] / total_stockable_area);
            }
            writeRow();
        }
    }
}
