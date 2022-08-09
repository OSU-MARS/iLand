using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.World;
using Microsoft.Data.Sqlite;
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
                               "snow cover, and radiation input. The spatial resolution is landscape averages and/or resource unit level (i.e. 100m pixels). " +
                               "Landscape level averages are indicated by -1 for the 'ru' and 'index' columns." + System.Environment.NewLine + System.Environment.NewLine +
                               "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(new("stocked_area", "area (ha/ha) which is stocked (covered by crowns, absorbing radiation)", SqliteType.Real));
            this.Columns.Add(new("stockable_area", "area (ha/ha) which is stockable (and within the project area)", SqliteType.Real));
            this.Columns.Add(new("precipitation_mm", "Annual precipitation sum (mm)", SqliteType.Real));
            this.Columns.Add(new("et_mm", "Evapotranspiration (mm)", SqliteType.Real));
            this.Columns.Add(new("excess_mm", "annual sum of water loss due to lateral outflow/groundwater flow (mm)", SqliteType.Real));
            this.Columns.Add(new("snowcover_days", "days with snowcover >0mm", SqliteType.Integer));
            this.Columns.Add(new("total_radiation", "total incoming radiation over the year (MJ/m2), sum of data in weather input)", SqliteType.Real));
            this.Columns.Add(new("radiation_snowcover", "sum of radiation input (MJ/m2) for days with snow cover", SqliteType.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
            if ((this.yearFilter.IsEmpty == false) && (yearFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                return;
            }

            int resourceUnitCount = 0;
            float snowDays = 0.0F;
            float totalEvapotranspiration = 0.0F;
            float totalRunoff = 0.0F;
            float totalSolarRadiation = 0.0F;
            float snowDaySolarRadiation = 0.0F;
            float totalAnnualPrecipitation = 0.0F;
            float stockableAreaInM2 = 0.0F;
            float stockedAreaInM2 = 0.0F;
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

                WaterCycle waterCycle = resourceUnit.WaterCycle;
                if (logResourceUnit)
                {
                    insertRow.Parameters[0].Value = currentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = resourceUnit.AreaWithTreesInM2 / Constant.ResourceUnitAreaInM2;
                    insertRow.Parameters[4].Value = resourceUnit.AreaInLandscapeInM2 / Constant.ResourceUnitAreaInM2;
                    insertRow.Parameters[5].Value = resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                    insertRow.Parameters[6].Value = waterCycle.TotalEvapotranspiration;
                    insertRow.Parameters[7].Value = waterCycle.TotalRunoff;
                    insertRow.Parameters[8].Value = waterCycle.SnowDays;
                    insertRow.Parameters[9].Value = resourceUnit.Weather.TotalAnnualRadiation;
                    insertRow.Parameters[10].Value = waterCycle.SnowDayRadiation;
                    insertRow.ExecuteNonQuery();
                }

                stockableAreaInM2 += resourceUnit.AreaInLandscapeInM2; 
                stockedAreaInM2 += resourceUnit.AreaWithTreesInM2;
                totalAnnualPrecipitation += resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                totalEvapotranspiration += waterCycle.TotalEvapotranspiration;
                totalRunoff += waterCycle.TotalRunoff; 
                snowDays += waterCycle.SnowDays;
                totalSolarRadiation += resourceUnit.Weather.TotalAnnualRadiation;
                snowDaySolarRadiation += waterCycle.SnowDayRadiation;
                ++resourceUnitCount;
            }

            // write landscape-level averages
            if (resourceUnitCount == 0)
            {
                return;
            }
            insertRow.Parameters[0].Value = currentCalendarYear; // codes -1/-1 for landscape level
            insertRow.Parameters[1].Value = -1; // resource unit grid index
            insertRow.Parameters[2].Value = -1; // resource unit ID
            insertRow.Parameters[3].Value = stockedAreaInM2 / (Constant.ResourceUnitAreaInM2 * resourceUnitCount);
            insertRow.Parameters[4].Value = stockableAreaInM2 / (Constant.ResourceUnitAreaInM2 * resourceUnitCount);
            insertRow.Parameters[5].Value = totalAnnualPrecipitation / resourceUnitCount; // mean precip
            insertRow.Parameters[6].Value = totalEvapotranspiration / resourceUnitCount;
            insertRow.Parameters[7].Value = totalRunoff / resourceUnitCount;
            insertRow.Parameters[8].Value = snowDays / resourceUnitCount;
            insertRow.Parameters[9].Value = totalSolarRadiation / resourceUnitCount;
            insertRow.Parameters[10].Value = snowDaySolarRadiation / resourceUnitCount;
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
