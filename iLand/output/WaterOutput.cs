using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    internal class WaterOutput : Output
    {
        private readonly Expression mFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public WaterOutput()
        {
            this.mFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

            Name = "Water output";
            TableName = "water";
            Description = "Annual water cycle output on resource unit/landscape unit." + System.Environment.NewLine +
                          "The output includes annual averages of precipitation, evapotranspiration, water excess, " +
                          "snow cover, and radiation input. The spatial resolution is landscape averages and/or resource unit level (i.e. 100m pixels). " +
                          "Landscape level averages are indicated by -1 for the 'ru' and 'index' columns." + System.Environment.NewLine + System.Environment.NewLine +
                          "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                          "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                          "(leaving 'conditionRU' blank enables details per default).";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(new SqlColumn("stocked_area", "area (ha/ha) which is stocked (covered by crowns, absorbing radiation)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("stockable_area", "area (ha/ha) which is stockable (and within the project area)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("precipitation_mm", "Annual precipitation sum (mm)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("et_mm", "Evapotranspiration (mm)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("excess_mm", "annual sum of water loss due to lateral outflow/groundwater flow (mm)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("snowcover_days", "days with snowcover >0mm", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("total_radiation", "total incoming radiation over the year (MJ/m2), sum of data in climate input)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("radiation_snowcover", "sum of radiation input (MJ/m2) for days with snow cover", OutputDatatype.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            if (!mFilter.IsEmpty && mFilter.Calculate(model.GlobalSettings, model.GlobalSettings.CurrentYear) == 0.0)
            {
                return;
            }
            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mResourceUnitFilter.IsEmpty && mResourceUnitFilter.Calculate(model.GlobalSettings, model.GlobalSettings.CurrentYear) == 0.0)
            {
                ru_level = false;
            }

            double ru_count = 0.0;
            int snow_days = 0;
            double et = 0.0, excess = 0.0, rad = 0.0, snow_rad = 0.0, p = 0.0;
            double stockable = 0.0, stocked = 0.0;
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                WaterCycle wc = ru.WaterCycle;
                if (ru_level)
                {
                    this.Add(model.GlobalSettings.CurrentYear);
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(ru.StockedArea / Constant.RUArea);
                    this.Add(ru.StockableArea / Constant.RUArea);
                    this.Add(ru.Climate.AnnualPrecipitation());
                    this.Add(wc.TotalEvapotranspiration);
                    this.Add(wc.TotalWaterLoss);
                    this.Add(wc.SnowDays);
                    this.Add(ru.Climate.TotalAnnualRadiation);
                    this.Add(wc.SnowDayRad);
                    this.WriteRow(insertRow);
                }
                ++ru_count;
                stockable += ru.StockableArea; 
                stocked += ru.StockedArea;
                p += ru.Climate.AnnualPrecipitation();
                et += wc.TotalEvapotranspiration; excess += wc.TotalWaterLoss; 
                snow_days += (int)wc.SnowDays;
                rad += ru.Climate.TotalAnnualRadiation;
                snow_rad += wc.SnowDayRad;
            }

            // write landscape sums
            if (ru_count == 0.0)
            {
                return;
            }
            this.Add(model.GlobalSettings.CurrentYear, -1, -1); // codes -1/-1 for landscape level
            this.Add(stocked / ru_count / Constant.RUArea);
            this.Add(stockable / ru_count / Constant.RUArea);
            this.Add(p / ru_count); // mean precip
            this.Add(et / ru_count);
            this.Add(excess / ru_count);
            this.Add(snow_days / ru_count);
            this.Add(rad / ru_count);
            this.Add(snow_rad / ru_count);
            this.WriteRow(insertRow);
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            // use a condition for to control execuation for the current year
            string condition = globalSettings.Settings.GetString(".condition", "");
            mFilter.SetExpression(condition);

            condition = globalSettings.Settings.GetString(".conditionRU", "");
            mResourceUnitFilter.SetExpression(condition);
        }
    }
}
