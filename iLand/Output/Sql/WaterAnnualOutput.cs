// C++/output/{ waterout.h, waterout.cpp }
using iLand.Extensions;
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
    public class WaterAnnualOutput : AnnualOutput
    {
        private readonly Expression resourceUnitFilter; // condition for resource-unit-level output
        private readonly Expression yearFilter; // condition for landscape-level output

        public WaterAnnualOutput()
        {
            this.resourceUnitFilter = new();
            this.yearFilter = new();

            this.Name = "Water output";
            this.TableName = "water";
            this.Description = "Annual water cycle output on resource unit/landscape unit." + System.Environment.NewLine +
                               "The output includes annual averages of precipitation, evapotranspiration, water excess, " +
                               "snow cover, and radiation input. The difference of precip - (evapotranspiration + excess) is the evaporation from intercepted precipitation. " +
                               "The spatial resolution is landscape averages and/or resource unit level (i.e. 100m pixels). " +
                               "Landscape level averages are indicated by -1 for the 'ru' and 'index' columns.+n+n" +
                               "Columns related to permafrost are 0 when permafrost module is disabled. The given values for depth " +
                               "are independent from the soil depth of iLand (e.g., soil depth can be 0.5m, but maxDepthFrozen can be 1.5m). " + System.Environment.NewLine +
                               "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(new("stocked_area", "Fraction of resource unit area (ha/ha) which is stocked (covered by crowns, absorbing radiation).", SqliteType.Real));
            this.Columns.Add(new("stockable_area", "Fraction of resource unit area (ha/ha) which is stockable (and within the project area).", SqliteType.Real));
            this.Columns.Add(new("precipitation_mm", "Annual precipitation sum, mm water column.", SqliteType.Real));
            this.Columns.Add(new("mean_annual_temp", "Mean annual temperature (°C).", SqliteType.Real));
            this.Columns.Add(new("evapotranspiration_mm", "Evapotranspiration, mm water column.", SqliteType.Real));
            this.Columns.Add(new("runoff_mm", "Annual sum of water loss due to lateral outflow/groundwater flow, mm water column.", SqliteType.Real));
            this.Columns.Add(new("total_radiation", "Total incoming radiation over the year (MJ/m²), sum of data in weather input)", SqliteType.Real));
            this.Columns.Add(new("radiation_snowcover", "sum of radiation input (MJ/m²) for days with snow cover", SqliteType.Integer));
            this.Columns.Add(new("effective_lai", "effective LAI (m²/m²) including LAI of adult trees, saplings, and ground cover", SqliteType.Real));
            this.Columns.Add(new("mean_swc_mm", "Mean soil water content of the year (mm).", SqliteType.Real));
            this.Columns.Add(new("mean_swc_gs_mm", "Mean soil water content in the growing season (fixed: April - September) (mm).", SqliteType.Real));
            this.Columns.Add(new("maxDepthFrozen", "Permafrost: maximum depth of freezing (m). The value is 2m when soil is fully frozen in a year.", SqliteType.Real));
            this.Columns.Add(new("maxDepthThawed", "Permafrost: maximum depth of thawing (m). The value is 2m if soil is fully thawed in a year.", SqliteType.Real));
            this.Columns.Add(new("maxSnowCover", "Permafrost: maximum snow height (m) in a year.", SqliteType.Real));
            this.Columns.Add(new("SOLLayer", "Permafrost: total depth of soil organic layer (excl. live moss) (m).", SqliteType.Real));
            this.Columns.Add(new("mossLayer", "Depth of the live moss layer (m).", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
            if ((this.yearFilter.IsEmpty == false) && (yearFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                return;
            }

            int permafrostResourceUnitCount = 0;
            int resourceUnitCount = 0;
            float stockableAreaInM2 = 0.0F;
            float stockedAreaInM2 = 0.0F;
            float totalAnnualPrecipitation = 0.0F;
            float totalAnnualEvapotranspiration = 0.0F;
            float totalEffectiveLeafAreaIndex = 0.0F;
            float totalMeanAnnualTemperature = 0.0F;
            float totalMeanSoilWaterContent = 0.0F;
            float totalMeanSoilWaterContentGrowingSeason = 0.0F;
            float totalMaxFreezeDepth = 0.0F;
            float totalMaxSnowDepth = 0.0F;
            float totalMaxThawDepth = 0.0F;
            float totalRunoff = 0.0F;
            float totalSnowRadiation = 0.0F;
            float totalSolarRadiation = 0.0F;
            for (int resourceUnitIndex = 0; resourceUnitIndex < model.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnit resourceUnit = model.Landscape.ResourceUnits[resourceUnitIndex];
                bool logResourceUnit = true;
                // switch off details if this is indicated in the conditionRU option
                if (this.resourceUnitFilter.IsEmpty == false)
                {
                    Debug.Assert(this.resourceUnitFilter.Wrapper != null);
                    ((ResourceUnitVariableAccessor)this.resourceUnitFilter.Wrapper).ResourceUnit = resourceUnit;
                    logResourceUnit = this.resourceUnitFilter.Evaluate(currentCalendarYear) != 0.0F;
                }

                ResourceUnitWaterCycle waterCycle = resourceUnit.WaterCycle;
                float ruAnnualTotalEvapotransipration = waterCycle.EvapotranspirationInMMByMonth.Sum();
                float ruAnnualTotalRunoff = waterCycle.RunoffInMMByMonth.Sum();
                float ruEffectiveLeafAreaIndex = waterCycle.EffectiveLeafAreaIndex;
                float ruMaxFreezeDepth = 0.0F;
                float ruMaxSnowDepth = Single.NaN; // TODO: get from snowpack if not available from permafrost
                float ruMaxThawDepth = 0.0F;
                if (waterCycle.Permafrost != null)
                {
                    ruMaxFreezeDepth = waterCycle.Permafrost.Statistics.MaxFreezeDepthInM;
                    ruMaxThawDepth = waterCycle.Permafrost.Statistics.MaxThawDepthInM;
                    ruMaxSnowDepth = waterCycle.Permafrost.Statistics.MaxSnowDepthInM;
                }
                float ruMeanAnnualTemperature = resourceUnit.Weather.MeanAnnualTemperature;
                float ruMeanSoilWaterContent = waterCycle.MeanSoilWaterContentAnnualInMM;
                float ruMeanSoilWaterContentGrowingSeason = waterCycle.MeanSoilWaterContentGrowingSeasonInMM;
                float ruSnowRadiation = waterCycle.SnowDayRadiation;

                if (logResourceUnit)
                {
                    insertRow.Parameters[0].Value = currentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ID;
                    insertRow.Parameters[2].Value = resourceUnit.AreaWithTreesInM2 / Constant.Grid.ResourceUnitAreaInM2;
                    insertRow.Parameters[3].Value = resourceUnit.AreaInLandscapeInM2 / Constant.Grid.ResourceUnitAreaInM2;
                    insertRow.Parameters[4].Value = resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                    insertRow.Parameters[5].Value = ruMeanAnnualTemperature;
                    insertRow.Parameters[6].Value = ruAnnualTotalEvapotransipration;
                    insertRow.Parameters[7].Value = ruAnnualTotalRunoff;
                    insertRow.Parameters[8].Value = resourceUnit.Weather.TotalAnnualRadiation;
                    insertRow.Parameters[9].Value = ruSnowRadiation;
                    insertRow.Parameters[10].Value = ruEffectiveLeafAreaIndex;
                    insertRow.Parameters[11].Value = ruMeanSoilWaterContent;
                    insertRow.Parameters[12].Value = ruMeanSoilWaterContentGrowingSeason;
                    if (waterCycle.Permafrost != null)
                    {
                        insertRow.Parameters[13].Value = ruMaxFreezeDepth;
                        insertRow.Parameters[14].Value = ruMaxThawDepth;
                        insertRow.Parameters[15].Value = ruMaxSnowDepth;
                        insertRow.Parameters[16].Value = waterCycle.Permafrost.SoilOrganicLayerDepthInM;
                        insertRow.Parameters[17].Value = waterCycle.Permafrost.GetMossLayerThicknessInM();
                    }
                    else
                    {
                        insertRow.Parameters[13].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[14].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[15].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[16].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[17].Value = Constant.Data.SqliteNaN;
                    }
                    insertRow.ExecuteNonQuery();
                }

                stockableAreaInM2 += resourceUnit.AreaInLandscapeInM2;
                stockedAreaInM2 += resourceUnit.AreaWithTreesInM2;
                totalAnnualPrecipitation += resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                totalAnnualEvapotranspiration += ruAnnualTotalEvapotransipration;
                totalEffectiveLeafAreaIndex += ruEffectiveLeafAreaIndex;
                if (waterCycle.Permafrost != null)
                {
                    totalMaxFreezeDepth = ruMaxFreezeDepth;
                    totalMaxThawDepth = ruMaxThawDepth;
                    totalMaxSnowDepth = ruMaxSnowDepth;
                    ++permafrostResourceUnitCount;
                }
                totalMeanAnnualTemperature += ruMeanAnnualTemperature;
                totalMeanSoilWaterContent = ruMeanSoilWaterContent;
                totalMeanSoilWaterContentGrowingSeason = ruMeanSoilWaterContentGrowingSeason;
                totalRunoff += ruAnnualTotalRunoff;
                totalSnowRadiation += ruSnowRadiation;
                totalSolarRadiation += resourceUnit.Weather.TotalAnnualRadiation;
                ++resourceUnitCount;
            }

            // write landscape-level averages
            if (resourceUnitCount == 0)
            {
                return;
            }

            insertRow.Parameters[0].Value = currentCalendarYear;
            insertRow.Parameters[1].Value = -1; // use -1 as resource unit ID to indicate whole landscape
            insertRow.Parameters[2].Value = stockedAreaInM2 / (Constant.Grid.ResourceUnitAreaInM2 * resourceUnitCount);
            insertRow.Parameters[3].Value = stockableAreaInM2 / (Constant.Grid.ResourceUnitAreaInM2 * resourceUnitCount);
            insertRow.Parameters[4].Value = totalAnnualPrecipitation / resourceUnitCount; // mean precipitation per resource unit
            insertRow.Parameters[6].Value = totalMeanAnnualTemperature / resourceUnitCount;
            insertRow.Parameters[7].Value = totalAnnualEvapotranspiration / resourceUnitCount;
            insertRow.Parameters[8].Value = totalRunoff / resourceUnitCount;
            insertRow.Parameters[9].Value = totalSolarRadiation / resourceUnitCount;
            insertRow.Parameters[9].Value = totalSnowRadiation / resourceUnitCount;
            insertRow.Parameters[10].Value = totalEffectiveLeafAreaIndex / resourceUnitCount;
            insertRow.Parameters[11].Value = totalMeanSoilWaterContent / resourceUnitCount;
            insertRow.Parameters[12].Value = totalMeanSoilWaterContentGrowingSeason / resourceUnitCount;
            if (permafrostResourceUnitCount > 0)
            {
                insertRow.Parameters[13].Value = totalMaxFreezeDepth / permafrostResourceUnitCount;
                insertRow.Parameters[14].Value = totalMaxThawDepth / permafrostResourceUnitCount;
                insertRow.Parameters[15].Value = totalMaxSnowDepth / permafrostResourceUnitCount;
            }
            else
            {
                insertRow.Parameters[13].Value = Constant.Data.SqliteNaN;
                insertRow.Parameters[14].Value = Constant.Data.SqliteNaN;
                insertRow.Parameters[15].Value = Constant.Data.SqliteNaN;
            }
            insertRow.Parameters[16].Value = Constant.Data.SqliteNaN;
            insertRow.Parameters[17].Value = Constant.Data.SqliteNaN;
            insertRow.ExecuteNonQuery();
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            // use a condition for to control execuation for the current year
            this.yearFilter.SetExpression(projectFile.Output.Sql.Water.Condition);
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.Water.ConditionRU);
            this.resourceUnitFilter.Wrapper = new ResourceUnitVariableAccessor(simulationState);
        }
    }
}
