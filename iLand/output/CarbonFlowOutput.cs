using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace iLand.Output
{
    public class CarbonFlowOutput : Output
    {
        private readonly Expression mFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public CarbonFlowOutput()
        {
            this.mFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

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

            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(new SqlColumn("area_ha", "total stockable area of the resource unit (or landscape) (ha)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("GPP", "actually realized gross primary production, kg C; ((primary production|GPP)) including " +
                                         "the effect of decreasing productivity with age; note that a rough estimate of " +
                                         "((sapling growth and competition|#sapling C and N dynamics|sapling GPP)) is added to the GPP of adult trees here.", OutputDatatype.Double));
            Columns.Add(new SqlColumn("NPP", "net primary production, kg C; calculated as NPP=GPP-Ra; Ra, the autotrophic respiration (kg C/ha) is calculated as" +
                                         " a fixed fraction of GPP in iLand (see ((primary production|here)) for details). ", OutputDatatype.Double));
            Columns.Add(new SqlColumn("Rh", "heterotrophic respiration, kg C; sum of C released to the atmosphere from detrital pools, i.e." +
                                         " ((snag dynamics|#Snag decomposition|snags)), ((soil C and N cycling|downed deadwood, litter, and mineral soil)).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("dist_loss", "disturbance losses, kg C; C that leaves the ecosystem as a result of disturbances, e.g. fire consumption", OutputDatatype.Double));
            Columns.Add(new SqlColumn("mgmt_loss", "management losses, kg C; C that leaves the ecosystem as a result of management interventions, e.g. harvesting", OutputDatatype.Double));
            Columns.Add(new SqlColumn("NEP", "net ecosytem productivity kg C, NEP=NPP - Rh - disturbance losses - management losses. " +
                                         "Note that NEP is also equal to the total net changes over all ecosystem C pools, as reported in the " +
                                         "carbon output (cf. [http://www.jstor.org/stable/3061028|Randerson et al. 2002])", OutputDatatype.Double));
            Columns.Add(new SqlColumn("cumNPP", "cumulative NPP, kg C. This is a running sum of NPP (including tree NPP and sapling carbon gain).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("cumRh", "cumulative flux to atmosphere (heterotrophic respiration), kg C. This is a running sum of Rh.", OutputDatatype.Double));
            Columns.Add(new SqlColumn("cumNEP", "cumulative NEP (net ecosystem productivity), kg C. This is a running sum of NEP (positive values: carbon gain, negative values: carbon loss).", OutputDatatype.Double));
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            // use a condition for to control execuation for the current year
            string condition = globalSettings.Settings.GetString(".condition", "");
            mFilter.SetExpression(condition);

            condition = globalSettings.Settings.GetString(".conditionRU", "");
            mResourceUnitFilter.SetExpression(condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            if (!mFilter.IsEmpty && mFilter.Calculate(model, model.GlobalSettings.CurrentYear) == 0.0)
            {
                return;
            }
            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mResourceUnitFilter.IsEmpty && mResourceUnitFilter.Calculate(model, model.GlobalSettings.CurrentYear) == 0.0)
            {
                ru_level = false;
            }

            int ru_count = 0;
            double[] accumulatedValues = new double[10]; // 11? data values per RU
            foreach (ResourceUnit ru in model.ResourceUnits) 
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
                double areaFactor = ru.StockableArea / Constant.RUArea; //conversion factor
                npp += ru.Statistics.Npp * Constant.BiomassCFraction; // kg C/ha
                npp += ru.Statistics.NppSaplings * Constant.BiomassCFraction; // kgC/ha
                                                                          // Snag pools are not scaled per ha (but refer to the stockable RU), soil pools and biomass statistics (NPP, ...) are scaled.
                double to_atm = ru.Snags.FluxToAtmosphere.C / areaFactor; // from snags, kg/ha
                to_atm += ru.Snags.FluxToAtmosphere.C * Constant.RUArea / 10.0; // soil: t/ha -> t/m2 -> kg/ha

                double to_dist = ru.Snags.FluxToDisturbance.C / areaFactor; // convert to kgC/ha
                to_dist += ru.Snags.FluxToDisturbance.C * Constant.RUArea / 10.0; // kgC/ha

                double to_harvest = ru.Snags.FluxToExtern.C / areaFactor; // kgC/ha

                double nep = npp - to_atm - to_harvest - to_dist; // kgC/ha

                if (ru_level)
                {
                    // keys
                    this.Add(model.GlobalSettings.CurrentYear);
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(areaFactor);
                    this.Add(npp / Constant.AutotrophicRespiration); // GPP_act
                    this.Add(npp); // NPP
                    this.Add(-to_atm); // rh
                    this.Add(-to_dist); // disturbance
                    this.Add(-to_harvest); // management loss
                    this.Add(nep); // nep
                    this.Add(ru.Variables.CumCarbonUptake);
                    this.Add(ru.Variables.CumCarbonToAtm);
                    this.Add(ru.Variables.CumNep);

                    this.WriteRow(insertRow);
                }
                // landscape level
                ++ru_count;

                accumulatedValues[0] += areaFactor; // total area in ha
                accumulatedValues[1] += npp / Constant.AutotrophicRespiration * areaFactor; // GPP_act
                accumulatedValues[2] += npp * areaFactor; // NPP
                accumulatedValues[3] += -to_atm * areaFactor; // rh
                accumulatedValues[4] += -to_dist * areaFactor; // disturbance
                accumulatedValues[5] += -to_harvest * areaFactor; // management loss
                accumulatedValues[6] += nep * areaFactor; // net ecosystem productivity
                accumulatedValues[7] += ru.Variables.CumCarbonUptake * areaFactor; // cum. NPP
                accumulatedValues[8] += ru.Variables.CumCarbonToAtm * areaFactor; // cum. Rh
                accumulatedValues[9] += ru.Variables.CumNep * areaFactor; // cum. NEP
            }

            // write landscape sums
            // BUGBUG: C++ appars to only behave correctly for single RU case
            double total_stockable_area = accumulatedValues[0]; // total ha of stockable area
            if (ru_count == 0.0 || total_stockable_area == 0.0)
            {
                return;
            }
            this.Add(model.GlobalSettings.CurrentYear);
            this.Add(-1);
            this.Add(-1); // codes -1/-1 for landscape level
            this.Add(accumulatedValues[0]); // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                this.Add(accumulatedValues[valueIndex] / total_stockable_area);
            }
            this.WriteRow(insertRow);
        }
    }
}
