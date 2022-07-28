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
            int currentSimulationYear = model.SimulationState.CurrentYear;
            if ((this.yearFilter.IsEmpty == false) && (yearFilter.Evaluate(currentSimulationYear) == 0.0))
            {
                return;
            }

            int resourceUnitCount = 0;
            int snowDays = 0;
            float evapotranspiration = 0.0F, runoff = 0.0F, rad = 0.0F, snowRad = 0.0F, precip = 0.0F;
            float stockable = 0.0F, stocked = 0.0F;
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                bool logResourceUnits = true;
                // switch off details if this is indicated in the conditionRU option
                if (this.resourceUnitFilter.IsEmpty == false)
                {
                    Debug.Assert(this.resourceUnitFilter.Wrapper != null);
                    ((ResourceUnitVariableAccessor)this.resourceUnitFilter.Wrapper).ResourceUnit = resourceUnit;
                    logResourceUnits = this.resourceUnitFilter.Evaluate(currentSimulationYear) != 0.0;
                }

                WaterCycle wc = resourceUnit.WaterCycle;
                if (logResourceUnits)
                {
                    insertRow.Parameters[0].Value = currentSimulationYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = resourceUnit.AreaWithTreesInM2 / Constant.ResourceUnitAreaInM2;
                    insertRow.Parameters[4].Value = resourceUnit.AreaInLandscapeInM2 / Constant.ResourceUnitAreaInM2;
                    insertRow.Parameters[5].Value = resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                    insertRow.Parameters[6].Value = wc.TotalEvapotranspiration;
                    insertRow.Parameters[7].Value = wc.TotalRunoff;
                    insertRow.Parameters[8].Value = wc.SnowDays;
                    insertRow.Parameters[9].Value = resourceUnit.Weather.TotalAnnualRadiation;
                    insertRow.Parameters[10].Value = wc.SnowDayRadiation;
                    insertRow.ExecuteNonQuery();
                }
                ++resourceUnitCount;
                stockable += resourceUnit.AreaInLandscapeInM2; 
                stocked += resourceUnit.AreaWithTreesInM2;
                precip += resourceUnit.Weather.GetTotalPrecipitationInCurrentYear();
                evapotranspiration += wc.TotalEvapotranspiration;
                runoff += wc.TotalRunoff; 
                snowDays += (int)wc.SnowDays;
                rad += resourceUnit.Weather.TotalAnnualRadiation;
                snowRad += wc.SnowDayRadiation;
            }

            // write landscape sums
            if (resourceUnitCount == 0)
            {
                return;
            }
            insertRow.Parameters[0].Value = currentSimulationYear; // codes -1/-1 for landscape level
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1;
            insertRow.Parameters[3].Value = stocked / resourceUnitCount / Constant.ResourceUnitAreaInM2;
            insertRow.Parameters[4].Value = stockable / resourceUnitCount / Constant.ResourceUnitAreaInM2;
            insertRow.Parameters[5].Value = precip / resourceUnitCount; // mean precip
            insertRow.Parameters[6].Value = evapotranspiration / resourceUnitCount;
            insertRow.Parameters[7].Value = runoff / resourceUnitCount;
            insertRow.Parameters[8].Value = snowDays / resourceUnitCount;
            insertRow.Parameters[9].Value = rad / resourceUnitCount;
            insertRow.Parameters[10].Value = snowRad / resourceUnitCount;
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
