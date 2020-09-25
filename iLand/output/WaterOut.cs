using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class WaterOut : Output
    {
        private readonly Expression mCondition; // condition for landscape-level output
        private readonly Expression mConditionDetails; // condition for resource-unit-level output

        public WaterOut()
        {
            this.mCondition = new Expression();
            this.mConditionDetails = new Expression();

            Name = "Water output";
            TableName = "water";
            Description = "Annual water cycle output on resource unit/landscape unit." + System.Environment.NewLine +
                          "The output includes annual averages of precipitation, evapotranspiration, water excess, " +
                          "snow cover, and radiation input. The spatial resolution is landscape averages and/or resource unit level (i.e. 100m pixels). " +
                          "Landscape level averages are indicated by -1 for the 'ru' and 'index' columns." + System.Environment.NewLine + System.Environment.NewLine +
                          "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                          "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                          "(leaving 'conditionRU' blank enables details per default).";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(new OutputColumn("stocked_area", "area (ha/ha) which is stocked (covered by crowns, absorbing radiation)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("stockable_area", "area (ha/ha) which is stockable (and within the project area)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("precipitation_mm", "Annual precipitation sum (mm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("et_mm", "Evapotranspiration (mm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("excess_mm", "annual sum of water loss due to lateral outflow/groundwater flow (mm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("snowcover_days", "days with snowcover >0mm", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("total_radiation", "total incoming radiation over the year (MJ/m2), sum of data in climate input)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("radiation_snowcover", "sum of radiation input (MJ/m2) for days with snow cover", OutputDatatype.OutInteger));
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

            double ru_count = 0.0;
            int snow_days = 0;
            double et = 0.0, excess = 0.0, rad = 0.0, snow_rad = 0.0, p = 0.0;
            double stockable = 0.0, stocked = 0.0;
            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                WaterCycle wc = ru.WaterCycle;
                if (ru_level)
                {
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(ru.StockedArea / Constant.RUArea);
                    this.Add(ru.StockableArea / Constant.RUArea);
                    this.Add(ru.Climate.AnnualPrecipitation());
                    this.Add(wc.TotalEvapotranspiration);
                    this.Add(wc.TotalWaterLoss);
                    this.Add(wc.SnowDays);
                    this.Add(ru.Climate.TotalRadiation);
                    this.Add(wc.SnowDayRad);
                    WriteRow();
                }
                ++ru_count;
                stockable += ru.StockableArea; 
                stocked += ru.StockedArea;
                p += ru.Climate.AnnualPrecipitation();
                et += wc.TotalEvapotranspiration; excess += wc.TotalWaterLoss; 
                snow_days += (int)wc.SnowDays;
                rad += ru.Climate.TotalRadiation;
                snow_rad += wc.SnowDayRad;
            }

            // write landscape sums
            if (ru_count == 0.0)
            {
                return;
            }
            this.Add(CurrentYear());
            this.Add(-1);
            this.Add(-1); // codes -1/-1 for landscape level
            this.Add(stocked / ru_count / Constant.RUArea);
            this.Add(stockable / ru_count / Constant.RUArea);
            this.Add(p / ru_count); // mean precip
            this.Add(et / ru_count);
            this.Add(excess / ru_count);
            this.Add(snow_days / ru_count);
            this.Add(rad / ru_count);
            this.Add(snow_rad / ru_count);
            WriteRow();
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);

            condition = Settings().Value(".conditionRU", "");
            mConditionDetails.SetExpression(condition);
        }
    }
}
