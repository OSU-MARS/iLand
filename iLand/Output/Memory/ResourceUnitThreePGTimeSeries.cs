using iLand.Extensions;
using iLand.Tree;
using System;

namespace iLand.Output.Memory
{
    public class ResourceUnitThreePGTimeSeries
    {
        public int LengthInMonths { get; private set; }

        public float[] SolarRadiationTotal { get; private set; }
        public float[] UtilizablePar { get; private set; } // photosynthetically active radiation
        public float[] MonthlyGpp { get; private set; }
        public float[] CO2Modifier { get; private set; }
        public float[] SoilWaterModifier { get; private set; }
        public float[] TemperatureModifier { get; private set; }
        public float[] VpdModifier { get; private set; }

        public ResourceUnitThreePGTimeSeries(int initialCapacityInYears)
        {
            this.LengthInMonths = 0;

            int initialCapacityInMonths = Constant.MonthsInYear * initialCapacityInYears;
            this.SolarRadiationTotal = new float[initialCapacityInMonths];
            this.UtilizablePar = new float[initialCapacityInMonths];
            this.MonthlyGpp = new float[initialCapacityInMonths];
            this.CO2Modifier = new float[initialCapacityInMonths];
            this.SoilWaterModifier = new float[initialCapacityInMonths];
            this.TemperatureModifier = new float[initialCapacityInMonths];
            this.VpdModifier = new float[initialCapacityInMonths];
        }

        public int CapacityInMonths
        {
            get { return this.SolarRadiationTotal.Length; }
        }

        public void AddYear(ResourceUnitTreeSpecies treeSpecies)
        {
            if (this.LengthInMonths == this.CapacityInMonths)
            {
                this.Extend();
            }

            treeSpecies.TreeGrowth.Modifiers.SolarRadiationTotalByMonth.CopyTo(this.SolarRadiationTotal, this.LengthInMonths);
            treeSpecies.TreeGrowth.UtilizableParByMonth.CopyTo(this.UtilizablePar, this.LengthInMonths);
            treeSpecies.TreeGrowth.MonthlyGpp.CopyTo(this.MonthlyGpp, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.CO2ModifierByMonth.CopyTo(this.CO2Modifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.SoilWaterModifierByMonth.CopyTo(this.SoilWaterModifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.TemperatureModifierByMonth.CopyTo(this.TemperatureModifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.VpdModifierByMonth.CopyTo(this.VpdModifier, this.LengthInMonths);

            this.LengthInMonths += Constant.MonthsInYear;
        }

        public void AddYearWithoutSpecies()
        {
            if (this.LengthInMonths == this.CapacityInMonths)
            {
                this.Extend();
            }

            Span<float> zero = stackalloc float[Constant.MonthsInYear];
            zero.CopyTo(this.SolarRadiationTotal.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.UtilizablePar.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.MonthlyGpp.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.CO2Modifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.SoilWaterModifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.TemperatureModifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.VpdModifier.AsSpan()[this.LengthInMonths..]);

            this.LengthInMonths += Constant.MonthsInYear;
        }

        private void Extend()
        {
            int newCapacityInMonths = this.CapacityInMonths + Constant.Data.DefaultMonthlyAllocationIncrement;
            this.SolarRadiationTotal = this.SolarRadiationTotal.Resize(newCapacityInMonths);
            this.UtilizablePar = this.UtilizablePar.Resize(newCapacityInMonths);
            this.MonthlyGpp = this.MonthlyGpp.Resize(newCapacityInMonths);
            this.CO2Modifier = this.CO2Modifier.Resize(newCapacityInMonths);
            this.SoilWaterModifier = this.SoilWaterModifier.Resize(newCapacityInMonths);
            this.TemperatureModifier = this.TemperatureModifier.Resize(newCapacityInMonths);
            this.VpdModifier = this.VpdModifier.Resize(newCapacityInMonths);
        }
    }
}
