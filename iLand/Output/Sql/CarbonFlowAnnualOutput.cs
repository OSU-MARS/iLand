using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public class CarbonFlowAnnualOutput : AnnualOutput
    {
        private readonly Expression yearFilter; // condition for landscape-level output
        private readonly Expression resourceUnitFilter; // condition for resource-unit-level output

        public CarbonFlowAnnualOutput()
        {
            this.yearFilter = new();
            this.resourceUnitFilter = new();

            this.Name = "Carbon fluxes per RU or landscape/yr";
            this.TableName = "carbonFlow";
            this.Description = "Carbon fluxes per resource unit and year and/or aggregated for the full landscape. All values are reported on a per hectare basis (use the area provided in carbon or stand outputs to scale to realized values on the respective resource unit)." +
                               "For results limited to the project area, the data values need to be scaled to the stockable area." + Environment.NewLine +
                               "For landsacpe level outputs, data is always given per ha of (stockable) project area (i.e. scaling with stockable area is already included)." + Environment.NewLine +
                               "Furthermore, the following sign convention is used in iLand: fluxes " + Environment.NewLine +
                               "from the atmosphere to the ecosystem are positive, while C leaving the ecosystem is reported as negative C flux." + Environment.NewLine +
                               "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";

            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(new("area_ha", "total stockable area of the resource unit (or landscape) (ha)", SqliteType.Real));
            this.Columns.Add(new("GPP", "actually realized gross primary production, kg C; ((primary production|GPP)) including " +
                                        "the effect of decreasing productivity with age; note that a rough estimate of " +
                                        "((sapling growth and competition|#sapling C and N dynamics|sapling GPP)) is added to the GPP of adult trees here.", SqliteType.Real));
            this.Columns.Add(new("NPP", "net primary production, kg C; calculated as NPP=GPP-Ra; Ra, the autotrophic respiration (kg C/ha) is calculated as" +
                                        " a fixed fraction of GPP in iLand (see ((primary production|here)) for details). ", SqliteType.Real));
            this.Columns.Add(new("Rh", "heterotrophic respiration, kg C; sum of C released to the atmosphere from detrital pools, i.e." +
                                       " ((snag dynamics|#Snag decomposition|snags)), ((soil C and N cycling|downed deadwood, litter, and mineral soil)).", SqliteType.Real));
            this.Columns.Add(new("dist_loss", "disturbance losses, kg C; C that leaves the ecosystem as a result of disturbances, e.g. fire consumption", SqliteType.Real));
            this.Columns.Add(new("mgmt_loss", "management losses, kg C; C that leaves the ecosystem as a result of management interventions, e.g. harvesting", SqliteType.Real));
            this.Columns.Add(new("NEP", "net ecosytem productivity kg C, NEP=NPP - Rh - disturbance losses - management losses. " +
                                        "Note that NEP is also equal to the total net changes over all ecosystem C pools, as reported in the " +
                                        "carbon output (cf. [http://www.jstor.org/stable/3061028|Randerson et al. 2002])", SqliteType.Real));
            this.Columns.Add(new("cumNPP", "cumulative NPP, kg C. This is a running sum of NPP (including tree NPP and sapling carbon gain).", SqliteType.Real));
            this.Columns.Add(new("cumRh", "cumulative flux to atmosphere (heterotrophic respiration), kg C. This is a running sum of Rh.", SqliteType.Real));
            this.Columns.Add(new("cumNEP", "cumulative NEP (net ecosystem productivity), kg C. This is a running sum of NEP (positive values: carbon gain, negative values: carbon loss).", SqliteType.Real));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            // use a condition for to control execution for the current year
            this.yearFilter.SetExpression(projectFile.Output.Sql.Carbon.Condition);
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.Carbon.ConditionRU);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            Debug.Assert(model.Landscape.ResourceUnits.Count > 0);

            // global condition
            int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
            if ((this.yearFilter.IsEmpty == false) && (this.yearFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                return;
            }
            bool logIndividualResourceUnits = true;
            // switch off details if this is indicated in the conditionRU option
            if ((this.resourceUnitFilter.IsEmpty == false) && (this.resourceUnitFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                logIndividualResourceUnits = false;
            }

            Span<float> accumulatedValues = stackalloc float[10]; // 10 data values
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                if ((resourceUnit.Snags == null) || (resourceUnit.Snags == null))
                {
                    // Debug.WriteLine("Resource unit lacks soil or snag data, no output generated.");
                    continue;
                }

                float areaFactor = resourceUnit.AreaInLandscapeInM2 / Constant.ResourceUnitAreaInM2; //conversion factor
                float npp = resourceUnit.Trees.TreeAndSaplingStatisticsForAllSpecies.TreeNppPerHa * Constant.DryBiomassCarbonFraction; // kg C/ha
                npp += resourceUnit.Trees.TreeAndSaplingStatisticsForAllSpecies.SaplingNppPerHa * Constant.DryBiomassCarbonFraction; // kgC/ha

                // Snag pools are not scaled per ha (but refer to the stockable RU), soil pools and biomass statistics (NPP, ...) 
                // are scaled.
                float toAtmosphere = resourceUnit.Snags.FluxToAtmosphere.C / areaFactor; // from snags, kg/ha
                toAtmosphere += 0.1F * resourceUnit.Snags.FluxToAtmosphere.C * Constant.ResourceUnitAreaInM2; // soil: t/ha -> t/m2 -> kg/ha

                float toDisturbance = resourceUnit.Snags.FluxToDisturbance.C / areaFactor; // convert to kgC/ha
                toDisturbance += 0.1F * resourceUnit.Snags.FluxToDisturbance.C * Constant.ResourceUnitAreaInM2; // kgC/ha

                float toHarvest = resourceUnit.Snags.FluxToExtern.C / areaFactor; // kgC/ha

                float nep = npp - toAtmosphere - toHarvest - toDisturbance; // kgC/ha

                if (logIndividualResourceUnits)
                {
                    insertRow.Parameters[0].Value = currentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = areaFactor;
                    insertRow.Parameters[4].Value = npp / model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier; // GPP_act
                    insertRow.Parameters[5].Value = npp; // NPP
                    insertRow.Parameters[6].Value = -toAtmosphere; // rh
                    insertRow.Parameters[7].Value = -toDisturbance; // disturbance
                    insertRow.Parameters[8].Value = -toHarvest; // management loss
                    insertRow.Parameters[9].Value = nep; // nep
                    insertRow.Parameters[10].Value = resourceUnit.CarbonCycle.TotalNpp;
                    insertRow.Parameters[11].Value = resourceUnit.CarbonCycle.TotalCarbonToAtmosphere;
                    insertRow.Parameters[12].Value = resourceUnit.CarbonCycle.TotalNep;
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
                accumulatedValues[7] += resourceUnit.CarbonCycle.TotalNpp * areaFactor; // cum. NPP
                accumulatedValues[8] += resourceUnit.CarbonCycle.TotalCarbonToAtmosphere * areaFactor; // cum. Rh
                accumulatedValues[9] += resourceUnit.CarbonCycle.TotalNep * areaFactor; // cum. NEP
            }

            // write landscape sums
            float totalStockableArea = accumulatedValues[0]; // total ha of stockable area
            if (totalStockableArea == 0.0F)
            {
                return;
            }
            insertRow.Parameters[0].Value = currentCalendarYear;
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
