using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class WaterOutput : Output
    {
        private readonly Expression mFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public WaterOutput()
        {
            this.mFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

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
            this.Columns.Add(new SqlColumn("stocked_area", "area (ha/ha) which is stocked (covered by crowns, absorbing radiation)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("stockable_area", "area (ha/ha) which is stockable (and within the project area)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("precipitation_mm", "Annual precipitation sum (mm)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("et_mm", "Evapotranspiration (mm)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("excess_mm", "annual sum of water loss due to lateral outflow/groundwater flow (mm)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("snowcover_days", "days with snowcover >0mm", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("total_radiation", "total incoming radiation over the year (MJ/m2), sum of data in climate input)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("radiation_snowcover", "sum of radiation input (MJ/m2) for days with snow cover", OutputDatatype.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            if (!mFilter.IsEmpty && mFilter.Evaluate(model.CurrentYear) == 0.0)
            {
                return;
            }
            bool logResourceUnits = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mResourceUnitFilter.IsEmpty && mResourceUnitFilter.Evaluate(model.CurrentYear) == 0.0)
            {
                logResourceUnits = false;
            }

            int resourceUnitCount = 0;
            int snowDays = 0;
            float evapotranspiration = 0.0F, runoff = 0.0F, rad = 0.0F, snowRad = 0.0F, precip = 0.0F;
            float stockable = 0.0F, stocked = 0.0F;
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }
                WaterCycle wc = ru.WaterCycle;
                if (logResourceUnits)
                {
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = ru.AreaWithTrees / Constant.RUArea;
                    insertRow.Parameters[4].Value = ru.AreaInLandscape / Constant.RUArea;
                    insertRow.Parameters[5].Value = ru.Climate.GetTotalPrecipitationInCurrentYear();
                    insertRow.Parameters[6].Value = wc.TotalEvapotranspiration;
                    insertRow.Parameters[7].Value = wc.TotalRunoff;
                    insertRow.Parameters[8].Value = wc.SnowDays;
                    insertRow.Parameters[9].Value = ru.Climate.TotalAnnualRadiation;
                    insertRow.Parameters[10].Value = wc.SnowDayRadiation;
                    insertRow.ExecuteNonQuery();
                }
                ++resourceUnitCount;
                stockable += ru.AreaInLandscape; 
                stocked += ru.AreaWithTrees;
                precip += ru.Climate.GetTotalPrecipitationInCurrentYear();
                evapotranspiration += wc.TotalEvapotranspiration;
                runoff += wc.TotalRunoff; 
                snowDays += (int)wc.SnowDays;
                rad += ru.Climate.TotalAnnualRadiation;
                snowRad += wc.SnowDayRadiation;
            }

            // write landscape sums
            if (resourceUnitCount == 0)
            {
                return;
            }
            insertRow.Parameters[0].Value = model.CurrentYear; // codes -1/-1 for landscape level
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1;
            insertRow.Parameters[3].Value = stocked / resourceUnitCount / Constant.RUArea;
            insertRow.Parameters[4].Value = stockable / resourceUnitCount / Constant.RUArea;
            insertRow.Parameters[5].Value = precip / resourceUnitCount; // mean precip
            insertRow.Parameters[6].Value = evapotranspiration / resourceUnitCount;
            insertRow.Parameters[7].Value = runoff / resourceUnitCount;
            insertRow.Parameters[8].Value = snowDays / resourceUnitCount;
            insertRow.Parameters[9].Value = rad / resourceUnitCount;
            insertRow.Parameters[10].Value = snowRad / resourceUnitCount;
            insertRow.ExecuteNonQuery();
        }

        public override void Setup(Model model)
        {
            // use a condition for to control execuation for the current year
            mFilter.SetExpression(model.Project.Output.Water.Condition);
            mResourceUnitFilter.SetExpression(model.Project.Output.Water.ConditionRU);
        }
    }
}
