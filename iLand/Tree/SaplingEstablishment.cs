using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Weather = iLand.World.Weather;

namespace iLand.Tree
{
    /// <summary>
    /// Finds leaf on and off dates and 2) chilling days for TACA sapling establishment. Currently assumes calendar year aligned simulation 
    /// years and a northern hemisphere leaf on to leaf off period occuring within a single calendar year.
    /// </summary>
    /// <remarks>
    /// Inputs for sapling establishment are:
    /// - availability of seeds: derived from the seed-maps per species <see cref="SeedDispersal"/>
    /// - quality of the abiotic environment (TACA model): calculations are performed here, based on weather and species responses
    /// - quality of the biotic environment, mainly light from LIF-values
    /// http://iland-model.org/establishment
    ///    
    /// TACA (tree and climate assessment) model:
    /// Nitshke CR, Innes JL. 2008. A tree and climate assessment tool for modelling ecosystem response to climate change.Ecological Modelling
    ///   210(3):263-277. https://doi.org/10.1016/j.ecolmodel.2007.07.026
    /// </remarks>
    public class SaplingEstablishment
    {
        // number of days that meet TACA chilling requirements (-5 to +5 °C) carried over from previous calendar year in northern hemisphere sites
        // Most likely zero in southern hemisphere sites.
        private int chillingDaysCarriedOverFromPreviousCalendarYear;

        private readonly List<float> soilWaterPotentialCircularBuffer;

        public SaplingEstablishment()
        {
            this.chillingDaysCarriedOverFromPreviousCalendarYear = Constant.NoDataInt32;
            this.soilWaterPotentialCircularBuffer = new(14); // for two week moving average, kPa
        }

        /** Estimate the abiotic environment effects on a species' seedling establishment on the given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: http://iland-model.org/establishment#abiotic_environment
         a model mockup in R: script_establishment.r
         */
        public float CalculateFrostAndWaterModifier(Project projectFile, Weather weather, ResourceUnitTreeSpecies ruSpecies)
        {
            // make sure that required calculations (e.g. watercycle are already performed)
            // TODO: why is CalculateBiomassGrowthForYear() called from three places?
            ruSpecies.CalculateBiomassGrowthForYear(projectFile, fromSaplingEstablishmentOrGrowth: true); // calculate the 3-PG module and run the water cycle (this is done only if that did not happen up to now)

            // get start of fall chilling days
            // for evergreen species: use the "bottom line" of 10.5 hrs daylength for the end of the vegetation season rather than leaf off
            // TODO: why does vegetation season cut off at this day length for all evergreens?
            // TODO: support southern hemisphere sites
            LeafPhenology leafPhenology = weather.GetPhenology(ruSpecies.Species.LeafPhenologyID);
            WeatherTimeSeries weatherTimeSeries = weather.TimeSeries;
            int leafOffTimeSeriesIndex = weatherTimeSeries.CurrentYearStartIndex + (leafPhenology.IsEvergreen ? weather.Sun.LastDayLongerThan10_5Hours : leafPhenology.LeafOnEndDayIndex);

            Debug.Assert(weatherTimeSeries.Timestep == Timestep.Daily);
            if (this.chillingDaysCarriedOverFromPreviousCalendarYear == Constant.NoDataInt32)
            {
                // for the first simulation year, approximate previous fall's chilling days with the current year's chilling days
                for (int weatherTimestepIndex = leafOffTimeSeriesIndex; weatherTimestepIndex < weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex)
                {
                    float daytimeMeanTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex];
                    if (daytimeMeanTemperature >= -5.0F && daytimeMeanTemperature < 5.0F)
                    {
                        ++this.chillingDaysCarriedOverFromPreviousCalendarYear;
                    }
                }

            }
            int chillingDays = this.chillingDaysCarriedOverFromPreviousCalendarYear;
            this.chillingDaysCarriedOverFromPreviousCalendarYear = 0;

            bool budsHaveBurst = false;
            SaplingEstablishmentParameters establishment = ruSpecies.Species.SaplingEstablishment;
            int frostDaysAfterBudBurst = 0; // frost days after bud burst
            int frostFreeDaysInYear = 0;
            float growingDegreeDays = 0.0F;
            float growingDegreeDaysBudBurst = 0.0F;
            bool tacaChillingRequirementSatisfied = false;  // total chilling requirement
            bool tacaMinimumTemperatureSatisfied = true; // minimum temperature threshold
            for (int weatherTimestepIndex = weatherTimeSeries.CurrentYearStartIndex; weatherTimestepIndex < weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex)
            {
                // minimum temperature: if temp too low . set prob. to zero
                if (weatherTimeSeries.TemperatureMin[weatherTimestepIndex] < establishment.ColdFatalityTemperature)
                {
                    tacaMinimumTemperatureSatisfied = false;
                }

                // count frost free days
                float minTemperature = weatherTimeSeries.TemperatureMin[weatherTimestepIndex];
                if (minTemperature > 0.0F)
                {
                    ++frostFreeDaysInYear;
                }

                // chilling days
                float daytimeMeanTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex];
                if (daytimeMeanTemperature >= -5.0F && daytimeMeanTemperature < 5.0F)
                {
                    ++chillingDays;
                    if (weatherTimestepIndex > leafOffTimeSeriesIndex)
                    {
                        ++this.chillingDaysCarriedOverFromPreviousCalendarYear;
                    }
                }
                if (chillingDays > establishment.ChillRequirement)
                {
                    tacaChillingRequirementSatisfied = true;
                }

                // growing degree days above the base temperature are counted if beginning from the day where the chilling requirements are met
                // up to a fixed day ending the veg period
                if (weatherTimestepIndex <= leafOffTimeSeriesIndex)
                {
                    // accumulate growing degree days
                    if (tacaChillingRequirementSatisfied && (daytimeMeanTemperature > establishment.GrowingDegreeDaysBaseTemperature))
                    {
                        growingDegreeDays += daytimeMeanTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                        growingDegreeDaysBudBurst += daytimeMeanTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                    }
                    // if day-frost occurs, the GDD counter for bud burst is reset
                    if (daytimeMeanTemperature <= 0.0F)
                    {
                        growingDegreeDaysBudBurst = 0.0F;
                    }
                    if (growingDegreeDaysBudBurst > establishment.GrowingDegreeDaysBudBurst)
                    {
                        budsHaveBurst = true;
                    }
                    if ((weatherTimestepIndex < leafOffTimeSeriesIndex) && budsHaveBurst && (minTemperature <= 0.0F))
                    {
                        ++frostDaysAfterBudBurst;
                    }
                }
            }

            float frostAndWaterModifier = 0.0F; // if any of TACA's sapling establishment requirements is not met
            if (tacaChillingRequirementSatisfied && tacaMinimumTemperatureSatisfied)
            {
                // frost free days in vegetation period
                bool tacaFrostFreeDaysSatisfied = (growingDegreeDays > establishment.MinimumGrowingDegreeDays) && (growingDegreeDays < establishment.MaximumGrowingDegreeDays);
                // growing degree day requirement
                bool tacaGrowingDegreeDaysSatisfied = frostFreeDaysInYear > establishment.MinimumFrostFreeDays;

                if (tacaGrowingDegreeDaysSatisfied && tacaFrostFreeDaysSatisfied)
                {
                    // negative effect of frost events after bud birst
                    float frostModifier = 1.0F;
                    if (frostDaysAfterBudBurst > 0)
                    {
                        frostModifier = MathF.Pow(establishment.FrostTolerance, MathF.Sqrt(frostDaysAfterBudBurst));
                    }
                    // reduction in establishment due to water limitation and drought induced mortality
                    int daysInYear = DateTimeExtensions.DaysInYear(weatherTimeSeries.IsCurrentlyLeapYear());
                    float waterModifier = this.GetMinimumLeafOnSoilWaterModifier(ruSpecies, leafPhenology.LeafOnStartDayIndex, leafPhenology.GetLeafOnDurationInDays(), daysInYear);
                    // combine water and frost effects multiplicatively
                    frostAndWaterModifier = frostModifier * waterModifier;
                }
            }

            return frostAndWaterModifier;
        }

        private float GetMinimumLeafOnSoilWaterModifier(ResourceUnitTreeSpecies ruSpecies, int leafOnStart, int leafOnEnd, int daysInYear)
        {
            // return 1.0 if water limitation is disabled
            if (Single.IsNaN(ruSpecies.Species.SaplingEstablishment.DroughtMortalityPsiInMPa))
            {
                return 1.0F;
            }

            // two week (14 days) running average of matric potential on the resource unit
            float currentPsiSum = 0.0F; // kPa
            for (int sumInitializationIndex = 0; sumInitializationIndex < this.soilWaterPotentialCircularBuffer.Count; ++sumInitializationIndex)
            {
                currentPsiSum += this.soilWaterPotentialCircularBuffer[sumInitializationIndex];
            }

            float miniumumLeafOnMovingAveragePsiInKPa = Single.MaxValue; // kPa
            WaterCycle waterCycle = ruSpecies.RU.WaterCycle;
            for (int dayOfYear = 0, movingAverageIndex = 0; dayOfYear < daysInYear; ++dayOfYear)
            {
                // running average: remove oldest item, add new item in a ringbuffer
                float soilWaterPotentialForDay = waterCycle.SoilWaterPotentialByWeatherTimestep[dayOfYear];
                if (this.soilWaterPotentialCircularBuffer.Count <= movingAverageIndex)
                {
                    this.soilWaterPotentialCircularBuffer.Add(soilWaterPotentialForDay);
                }
                else
                {
                    currentPsiSum -= this.soilWaterPotentialCircularBuffer[movingAverageIndex];
                    this.soilWaterPotentialCircularBuffer[movingAverageIndex] = soilWaterPotentialForDay;
                }
                currentPsiSum += this.soilWaterPotentialCircularBuffer[movingAverageIndex];

                if (dayOfYear >= leafOnStart && dayOfYear <= leafOnEnd)
                {
                    float currentPsiAverage = currentPsiSum / this.soilWaterPotentialCircularBuffer.Count;
                    miniumumLeafOnMovingAveragePsiInKPa = MathF.Min(miniumumLeafOnMovingAveragePsiInKPa, currentPsiAverage);
                }

                // move to next value in the circular buffer
                ++movingAverageIndex;
                if (movingAverageIndex >= this.soilWaterPotentialCircularBuffer.Count)
                {
                    movingAverageIndex = 0;
                }
            }

            if (miniumumLeafOnMovingAveragePsiInKPa > 1000.0F)
            {
                return 1.0F; // invalid leaf on period?
            }

            // calculate the response of the species to this value of psi (see also Species.GetSoilWaterModifier())
            float minimumEstablishmentPsiMPa = ruSpecies.Species.SaplingEstablishment.DroughtMortalityPsiInMPa;
            float minimumLeafOnMovingAveragePsiInMPa = 0.001F * miniumumLeafOnMovingAveragePsiInKPa; // convert to MPa
            float waterResponse = Maths.Limit((minimumLeafOnMovingAveragePsiInMPa - minimumEstablishmentPsiMPa) / (-0.015F - minimumEstablishmentPsiMPa), 0.0F, 1.0F);

            return waterResponse;
        }
    }
}
