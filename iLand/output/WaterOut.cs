using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class WaterOut : Output
    {
        private Expression mCondition; // condition for landscape-level output
        private Expression mConditionDetails; // condition for resource-unit-level output

        public WaterOut()
        {
            setName("Water output", "water");
            setDescription("Annual water cycle output on resource unit/landscape unit." + System.Environment.NewLine +
                       "The output includes annual averages of precipitation, evapotranspiration, water excess, " +
                       "snow cover, and radiation input. The spatial resolution is landscape averages and/or resource unit level (i.e. 100m pixels). " +
                       "Landscape level averages are indicated by -1 for the 'ru' and 'index' columns." + System.Environment.NewLine + System.Environment.NewLine +
                       "You can specify a 'condition' to limit output execution to specific years (variable 'year'). " +
                       "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                       "(leaving 'conditionRU' blank enables details per default).");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(new OutputColumn("stocked_area", "area (ha/ha) which is stocked (covered by crowns, absorbing radiation)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("stockable_area", "area (ha/ha) which is stockable (and within the project area)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("precipitation_mm", "Annual precipitation sum (mm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("et_mm", "Evapotranspiration (mm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("excess_mm", "annual sum of water loss due to lateral outflow/groundwater flow (mm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("snowcover_days", "days with snowcover >0mm", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("total_radiation", "total incoming radiation over the year (MJ/m2), sum of data in climate input)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("radiation_snowcover", "sum of radiation input (MJ/m2) for days with snow cover", OutputDatatype.OutInteger));
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

            double ru_count = 0.0;
            int snow_days = 0;
            double et = 0.0, excess = 0.0, rad = 0.0, snow_rad = 0.0, p = 0.0;
            double stockable = 0.0, stocked = 0.0;
            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }
                WaterCycle wc = ru.waterCycle();
                if (ru_level)
                {
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(ru.stockedArea() / Constant.cRUArea);
                    this.add(ru.stockableArea() / Constant.cRUArea);
                    this.add(ru.climate().annualPrecipitation());
                    this.add(wc.mTotalET);
                    this.add(wc.mTotalExcess);
                    this.add(wc.mSnowDays);
                    this.add(ru.climate().totalRadiation());
                    this.add(wc.mSnowRad);
                    writeRow();
                }
                ++ru_count;
                stockable += ru.stockableArea(); stocked += ru.stockedArea();
                p += ru.climate().annualPrecipitation();
                et += wc.mTotalET; excess += wc.mTotalExcess; 
                snow_days += (int)wc.mSnowDays;
                rad += ru.climate().totalRadiation();
                snow_rad += wc.mSnowRad;
            }

            // write landscape sums
            if (ru_count == 0.0)
            {
                return;
            }
            this.add(currentYear());
            this.add(-1);
            this.add(-1); // codes -1/-1 for landscape level
            this.add(stocked / ru_count / Constant.cRUArea);
            this.add(stockable / ru_count / Constant.cRUArea);
            this.add(p / ru_count); // mean precip
            this.add(et / ru_count);
            this.add(excess / ru_count);
            this.add(snow_days / ru_count);
            this.add(rad / ru_count);
            this.add(snow_rad / ru_count);
            writeRow();
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);

            condition = settings().value(".conditionRU", "");
            mConditionDetails.setExpression(condition);
        }
    }
}
