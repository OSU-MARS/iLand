﻿using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace iLand.Output
{
    public class CarbonFlowAnnualOutput : AnnualOutput
    {
        private readonly Expression mYearFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public CarbonFlowAnnualOutput()
        {
            this.mYearFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

            this.Name = "Carbon fluxes per RU or landscape/yr";
            this.TableName = "carbonFlow";
            this.Description = "Carbon fluxes per resource unit and year and/or aggregated for the full landscape. All values are reported on a per hectare basis (use the area provided in carbon or stand outputs to scale to realized values on the respective resource unit)." + 
                               "For results limited to the project area, the data values need to be scaled to the stockable area." + System.Environment.NewLine +
                               "For landsacpe level outputs, data is always given per ha of (stockable) project area (i.e. scaling with stockable area is already included)." + System.Environment.NewLine +
                               "Furthermore, the following sign convention is used in iLand: fluxes " + System.Environment.NewLine +
                               "from the atmosphere to the ecosystem are positive, while C leaving the ecosystem is reported as negative C flux." + System.Environment.NewLine +
                               "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " + 
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";

            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(new SqlColumn("area_ha", "total stockable area of the resource unit (or landscape) (ha)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("GPP", "actually realized gross primary production, kg C; ((primary production|GPP)) including " +
                                           "the effect of decreasing productivity with age; note that a rough estimate of " +
                                           "((sapling growth and competition|#sapling C and N dynamics|sapling GPP)) is added to the GPP of adult trees here.", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NPP", "net primary production, kg C; calculated as NPP=GPP-Ra; Ra, the autotrophic respiration (kg C/ha) is calculated as" +
                                           " a fixed fraction of GPP in iLand (see ((primary production|here)) for details). ", SqliteType.Real));
            this.Columns.Add(new SqlColumn("Rh", "heterotrophic respiration, kg C; sum of C released to the atmosphere from detrital pools, i.e." +
                                           " ((snag dynamics|#Snag decomposition|snags)), ((soil C and N cycling|downed deadwood, litter, and mineral soil)).", SqliteType.Real));
            this.Columns.Add(new SqlColumn("dist_loss", "disturbance losses, kg C; C that leaves the ecosystem as a result of disturbances, e.g. fire consumption", SqliteType.Real));
            this.Columns.Add(new SqlColumn("mgmt_loss", "management losses, kg C; C that leaves the ecosystem as a result of management interventions, e.g. harvesting", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NEP", "net ecosytem productivity kg C, NEP=NPP - Rh - disturbance losses - management losses. " +
                                           "Note that NEP is also equal to the total net changes over all ecosystem C pools, as reported in the " +
                                           "carbon output (cf. [http://www.jstor.org/stable/3061028|Randerson et al. 2002])", SqliteType.Real));
            this.Columns.Add(new SqlColumn("cumNPP", "cumulative NPP, kg C. This is a running sum of NPP (including tree NPP and sapling carbon gain).", SqliteType.Real));
            this.Columns.Add(new SqlColumn("cumRh", "cumulative flux to atmosphere (heterotrophic respiration), kg C. This is a running sum of Rh.", SqliteType.Real));
            this.Columns.Add(new SqlColumn("cumNEP", "cumulative NEP (net ecosystem productivity), kg C. This is a running sum of NEP (positive values: carbon gain, negative values: carbon loss).", SqliteType.Real));
        }

        public override void Setup(Model model)
        {
            // use a condition for to control execution for the current year
            this.mYearFilter.SetExpression(model.Project.Output.Annual.Carbon.Condition);
            this.mResourceUnitFilter.SetExpression(model.Project.Output.Annual.Carbon.ConditionRU);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            Debug.Assert(model.Landscape.ResourceUnits.Count > 0);

            // global condition
            if ((this.mYearFilter.IsEmpty == false) && (this.mYearFilter.Evaluate(model.CurrentYear) == 0.0))
            {
                return;
            }
            bool logIndividualResourceUnits = true;
            // switch off details if this is indicated in the conditionRU option
            if ((this.mResourceUnitFilter.IsEmpty == false) && (this.mResourceUnitFilter.Evaluate(model.CurrentYear) == 0.0))
            {
                logIndividualResourceUnits = false;
            }

            float[] accumulatedValues = new float[10]; // 10 data values
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits) 
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }
                if (ru.Snags == null || ru.Snags == null)
                {
                    // Debug.WriteLine("Resource unit lacks soil or snag data, no output generated.");
                    continue;
                }

                float areaFactor = ru.AreaInLandscape / Constant.RUArea; //conversion factor
                float npp = ru.Trees.StatisticsForAllSpeciesAndStands.TreeNpp * Constant.BiomassCFraction; // kg C/ha
                npp += ru.Trees.StatisticsForAllSpeciesAndStands.SaplingNpp * Constant.BiomassCFraction; // kgC/ha
                
                // Snag pools are not scaled per ha (but refer to the stockable RU), soil pools and biomass statistics (NPP, ...) 
                // are scaled.
                float toAtmosphere = ru.Snags.FluxToAtmosphere.C / areaFactor; // from snags, kg/ha
                toAtmosphere += 0.1F * ru.Snags.FluxToAtmosphere.C * Constant.RUArea; // soil: t/ha -> t/m2 -> kg/ha

                float toDisturbance = ru.Snags.FluxToDisturbance.C / areaFactor; // convert to kgC/ha
                toDisturbance += 0.1F * ru.Snags.FluxToDisturbance.C * Constant.RUArea; // kgC/ha

                float toHarvest = ru.Snags.FluxToExtern.C / areaFactor; // kgC/ha

                float nep = npp - toAtmosphere - toHarvest - toDisturbance; // kgC/ha

                if (logIndividualResourceUnits)
                {
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = areaFactor;
                    insertRow.Parameters[4].Value = npp / model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier; // GPP_act
                    insertRow.Parameters[5].Value = npp; // NPP
                    insertRow.Parameters[6].Value = -toAtmosphere; // rh
                    insertRow.Parameters[7].Value = -toDisturbance; // disturbance
                    insertRow.Parameters[8].Value = -toHarvest; // management loss
                    insertRow.Parameters[9].Value = nep; // nep
                    insertRow.Parameters[10].Value = ru.CarbonCycle.TotalNpp;
                    insertRow.Parameters[11].Value = ru.CarbonCycle.TotalCarbonToAtmosphere;
                    insertRow.Parameters[12].Value = ru.CarbonCycle.TotalNep;
                    insertRow.ExecuteNonQuery();
                }

                // landscape level
                accumulatedValues[0] += areaFactor; // total area in ha
                accumulatedValues[1] += npp / model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier * areaFactor; // GPP_act
                accumulatedValues[2] += npp * areaFactor; // NPP
                accumulatedValues[3] += -toAtmosphere * areaFactor; // rh
                accumulatedValues[4] += -toDisturbance * areaFactor; // disturbance
                accumulatedValues[5] += -toHarvest * areaFactor; // management loss
                accumulatedValues[6] += nep * areaFactor; // net ecosystem productivity
                accumulatedValues[7] += ru.CarbonCycle.TotalNpp * areaFactor; // cum. NPP
                accumulatedValues[8] += ru.CarbonCycle.TotalCarbonToAtmosphere * areaFactor; // cum. Rh
                accumulatedValues[9] += ru.CarbonCycle.TotalNep * areaFactor; // cum. NEP
            }

            // write landscape sums
            float totalStockableArea = accumulatedValues[0]; // total ha of stockable area
            if (totalStockableArea == 0.0F)
            {
                return;
            }
            insertRow.Parameters[0].Value = model.CurrentYear;
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1; // codes -1/-1 for landscape level
            insertRow.Parameters[3].Value = accumulatedValues[0]; // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                insertRow.Parameters[3 + valueIndex].Value = accumulatedValues[valueIndex] / totalStockableArea;
            }
            insertRow.ExecuteNonQuery();
        }
    }
}
