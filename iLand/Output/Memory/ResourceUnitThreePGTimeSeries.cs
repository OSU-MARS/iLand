using iLand.Extensions;
using iLand.Tree;
using iLand.World;
using System;

namespace iLand.Output.Memory
{
    public class ResourceUnitThreePGTimeSeries
    {
        public int LengthInMonths { get; private set; }

        public float[] CO2Modifier { get; private set; } // multiplier
        public float[] Evapotranspiration { get; private set; } // mm
        public float[] MonthlyGpp { get; private set; } // kg biomass
        public float[] SoilWaterInfiltration { get; private set; } // mm
        public float[] SoilWaterModifier { get; private set; } // multiplier
        public float[] SoilWaterPotential { get; private set; } // kPa
        public float[] SolarRadiationTotal { get; private set; } // MJ/m²
        public float[] TemperatureModifier { get; private set; } // multiplier
        public float[] UtilizablePar { get; private set; } // photosynthetically active radiation, MJ/m²
        public float[] VpdModifier { get; private set; } // multiplier

        public ResourceUnitThreePGTimeSeries(int initialCapacityInYears)
        {
            this.LengthInMonths = 0;

            int initialCapacityInMonths = Constant.Time.MonthsInYear * initialCapacityInYears;
            this.CO2Modifier = new float[initialCapacityInMonths];
            this.Evapotranspiration = new float[initialCapacityInMonths];
            this.SoilWaterInfiltration = new float[initialCapacityInMonths];
            this.SoilWaterModifier = new float[initialCapacityInMonths];
            this.SoilWaterPotential = new float[initialCapacityInMonths];
            this.SolarRadiationTotal = new float[initialCapacityInMonths];
            this.MonthlyGpp = new float[initialCapacityInMonths];
            this.TemperatureModifier = new float[initialCapacityInMonths];
            this.UtilizablePar = new float[initialCapacityInMonths];
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

            ResourceUnitWaterCycle waterCycle = treeSpecies.ResourceUnit.WaterCycle;
            waterCycle.EvapotranspirationInMMByMonth.CopyTo(this.Evapotranspiration, this.LengthInMonths);
            waterCycle.InfiltrationInMMByMonth.CopyTo(this.SoilWaterInfiltration, this.LengthInMonths);

            float[] waterPotentialByWeatherTimestep = waterCycle.SoilWaterPotentialByWeatherTimestepInYear;
            if (waterPotentialByWeatherTimestep.Length == Constant.Time.MonthsInYear)
            {
                waterPotentialByWeatherTimestep.CopyTo(this.SoilWaterPotential, this.LengthInMonths);
            }
            else
            {
                Span<float> waterPotentialByMonth = stackalloc float[Constant.Time.MonthsInYear];
                waterPotentialByWeatherTimestep.ToMonthlyAverages(waterPotentialByMonth);
                waterPotentialByMonth.CopyTo(this.SoilWaterPotential.AsSpan()[this.LengthInMonths..]);
            }

            treeSpecies.TreeGrowth.MonthlyGpp.CopyTo(this.MonthlyGpp, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.CO2ModifierByMonth.CopyTo(this.CO2Modifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.SoilWaterModifierByMonth.CopyTo(this.SoilWaterModifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.TemperatureModifierByMonth.CopyTo(this.TemperatureModifier, this.LengthInMonths);
            treeSpecies.TreeGrowth.Modifiers.VpdModifierByMonth.CopyTo(this.VpdModifier, this.LengthInMonths);

            this.LengthInMonths += Constant.Time.MonthsInYear;
        }

        public void AddYearWithoutSpecies()
        {
            if (this.LengthInMonths == this.CapacityInMonths)
            {
                this.Extend();
            }

            Span<float> zero = stackalloc float[Constant.Time.MonthsInYear];
            zero.CopyTo(this.CO2Modifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.Evapotranspiration.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.MonthlyGpp.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.SoilWaterModifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.SoilWaterInfiltration.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.SoilWaterPotential.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.SolarRadiationTotal.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.TemperatureModifier.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.UtilizablePar.AsSpan()[this.LengthInMonths..]);
            zero.CopyTo(this.VpdModifier.AsSpan()[this.LengthInMonths..]);

            this.LengthInMonths += Constant.Time.MonthsInYear;
        }

        private void Extend()
        {
            int newCapacityInMonths = this.CapacityInMonths + Constant.Data.DefaultMonthlyAllocationIncrement;
            this.CO2Modifier = this.CO2Modifier.Resize(newCapacityInMonths);
            this.Evapotranspiration = this.Evapotranspiration.Resize(newCapacityInMonths);
            this.MonthlyGpp = this.MonthlyGpp.Resize(newCapacityInMonths);
            this.SoilWaterInfiltration = this.SoilWaterInfiltration.Resize(newCapacityInMonths);
            this.SoilWaterModifier = this.SoilWaterModifier.Resize(newCapacityInMonths);
            this.SoilWaterPotential = this.SoilWaterPotential.Resize(newCapacityInMonths);
            this.SolarRadiationTotal = this.SolarRadiationTotal.Resize(newCapacityInMonths);
            this.TemperatureModifier = this.TemperatureModifier.Resize(newCapacityInMonths);
            this.UtilizablePar = this.UtilizablePar.Resize(newCapacityInMonths);
            this.VpdModifier = this.VpdModifier.Resize(newCapacityInMonths);
        }
    }
}
