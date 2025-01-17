﻿// C++/core/{ establishment.h, establishment.cpp }
using iLand.Extensions;
using iLand.Input;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /// <summary>
    /// Finds leaf on and off dates and 2) chilling days for TACA sapling establishment. Currently assumes calendar year aligned simulation 
    /// years and a northern hemisphere leaf on to leaf off period occuring within a single calendar year.
    /// </summary>
    /// <remarks>
    /// Inputs for sapling establishment are:
    /// - availability of seeds: derived from the seed-maps per species <see cref="SeedDispersal"/>
    /// - quality of the abiotic environment (TACA model): calculations are performed here, based on weather and establishment modifiers
    /// - quality of the biotic environment, mainly light from LIF-values
    /// https://iland-model.org/establishment
    ///    
    /// TACA (tree and climate assessment) model:
    /// Nitshke CR, Innes JL. 2008. A tree and climate assessment tool for modelling ecosystem response to climate change.Ecological Modelling
    ///   210(3):263-277. https://doi.org/10.1016/j.ecolmodel.2007.07.026
    /// </remarks>
    public class SaplingEstablishment
    {
        // number of days that meet TACA chilling requirements (-5 to +5 °C) carried over from previous calendar year in northern hemisphere sites
        // Most likely zero in southern hemisphere sites.
        private float chillingDaysCarriedOverFromPreviousCalendarYear;
        // TODO: remove unused
        // private readonly List<float> soilWaterPotentialCircularBuffer;

        ///< the growing degree days (species specific)
        public float GrowingDegreeDays { get; private set; } // growing degree days, C++ growingDegreeDays(), mGDD

        public SaplingEstablishment()
        {
            this.chillingDaysCarriedOverFromPreviousCalendarYear = Constant.NoDataInt32;
            // this.soilWaterPotentialCircularBuffer = new(14); // for two week moving average, kPa
        }

        /** Estimate the abiotic environment effects on a species' seedling establishment on the given resource unit.
         The model is closely based on the TACA approach of Nitschke and Innes (2008), Ecol. Model 210, 263-277
         more details: https://iland-model.org/establishment#abiotic_environment
         a model mockup in R: script_establishment.r
         */
        public float CalculateFrostAndWaterModifier(Model model, Weather weather, ResourceUnitTreeSpecies ruSpecies) // C++: Establishment::calculateAbioticEnvironment()
        {
            // make sure that required calculations (e.g. watercycle are already performed)
            // TODO: why is CalculateBiomassGrowthForYear() called from three places?
            ruSpecies.CalculateBiomassGrowthForYear(model, fromSaplingEstablishmentOrGrowth: true); // calculate the 3-PG module and run the water cycle (this is done only if that did not happen up to now)

            // get start of fall chilling days: leaf off for deciduous species, 10.5 hour day length for evergreen species
            // TODO: why is a 10.5 hour day defined as the end of the growing seasion for all evergreens?
            // TODO: Nitshke and Innes define chilling days as beginning from the midpoint of the growing season, not from leaf off or 10.5 hours
            // TODO: support southern hemisphere sites
            LeafPhenology leafPhenology = weather.GetPhenology(ruSpecies.Species.LeafPhenologyID);
            WeatherTimeSeries weatherTimeSeries = weather.TimeSeries;

            // for the first simulation year, approximate previous fall's chilling days with the current year's chilling days
            if (this.chillingDaysCarriedOverFromPreviousCalendarYear == Constant.NoDataInt32)
            {
                this.chillingDaysCarriedOverFromPreviousCalendarYear = 0;
                if (weatherTimeSeries.Timestep == Timestep.Daily)
                {
                    int leafOffTimeSeriesIndex = SaplingEstablishment.GetLeafOffDailyWeatherIndex(weather, leafPhenology);
                    for (int weatherDayIndex = leafOffTimeSeriesIndex; weatherDayIndex < weatherTimeSeries.NextYearStartIndex; ++weatherDayIndex)
                    {
                        float daytimeMeanTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherDayIndex];
                        if (SaplingEstablishment.IsChillingDay(daytimeMeanTemperature))
                        {
                            ++this.chillingDaysCarriedOverFromPreviousCalendarYear;
                        }
                    }
                }
                else if (weatherTimeSeries.Timestep == Timestep.Monthly)
                {
                    bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
                    for (int monthIndex = 0, weatherMonthIndex = weatherTimeSeries.CurrentYearStartIndex; weatherMonthIndex < weatherTimeSeries.NextYearStartIndex; ++monthIndex, ++weatherMonthIndex)
                    {
                        float monthlyMeanDaytimeTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherMonthIndex];
                        float daysInMonth = (float)DateTimeExtensions.GetDaysInMonth(monthIndex, isLeapYear);
                        this.chillingDaysCarriedOverFromPreviousCalendarYear += SaplingEstablishment.EstimateChillingDaysInMonth(monthlyMeanDaytimeTemperature, daysInMonth);
                    }
                }
                else
                {
                    throw new NotSupportedException("Unhandled weather timestep " + weatherTimeSeries.Timestep + ".");
                }
            }
            float chillingDays = this.chillingDaysCarriedOverFromPreviousCalendarYear;
            this.chillingDaysCarriedOverFromPreviousCalendarYear = 0;

            // should we use microclimate temperatures?
            bool use_micro_clim = model.Project.Model.Microclimate.Enabled && model.Project.Model.Microclimate.EstablishmentEffect;
            Microclimate? microclimate = ruSpecies.ResourceUnit.Microclimate;
            if (use_micro_clim && (microclimate == null))
            {
                throw new InvalidOperationException("Project enables microclimate establishment effects but resource unit " + ruSpecies.ResourceUnit.ID + "'s microclimate is not instantiated.");
            }

            bool budsHaveBurst = false;
            SaplingEstablishmentParameters establishment = ruSpecies.Species.SaplingEstablishment;
            float frostDaysAfterBudburst = 0.0F; // frost days after budburst
            float frostFreeDaysInYear = 0.0F;
            this.GrowingDegreeDays = 0.0F;
            float growingDegreeDaysBudburst = 0.0F;
            bool tacaChillingDaysReached = false; // total chilling requirement
            bool tacaColdFatalityTemperatureNotReached = true; // minimum temperature threshold
            if (weatherTimeSeries.Timestep == Timestep.Daily)
            {
                int leafOffTimeSeriesIndex = SaplingEstablishment.GetLeafOffDailyWeatherIndex(weather, leafPhenology);
                for (int weatherDayIndex = weatherTimeSeries.CurrentYearStartIndex; weatherDayIndex < weatherTimeSeries.NextYearStartIndex; ++weatherDayIndex)
                {
                    float minTemperature = weatherTimeSeries.TemperatureMin[weatherDayIndex];
                    float daytimeMeanTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherDayIndex];
                    if (use_micro_clim)
                    {
                        // use microclimate calculations to modify the temperature for establishment
                        Debug.Assert(microclimate != null);
                        int monthIndex = weatherTimeSeries.Month[weatherDayIndex] - 1;
                        float mc_min_buf = microclimate.GetMinimumMicroclimateBuffering(monthIndex);
                        float mc_max_buf = microclimate.GetMaximumMicroclimateBuffering(monthIndex);
                        float mc_mean_buf = (mc_min_buf + mc_max_buf) / 2.0F;

                        minTemperature += mc_min_buf;
                        daytimeMeanTemperature += mc_mean_buf;
                    }

                    // if winterkill temperature is reached seedling establishment probability becomes zero
                    if (minTemperature < establishment.ColdFatalityTemperature)
                    {
                        tacaColdFatalityTemperatureNotReached = false;
                        break;
                    }

                    // count frost free days
                    if (minTemperature > 0.0F)
                    {
                        ++frostFreeDaysInYear;
                    }

                    // chilling days
                    if (SaplingEstablishment.IsChillingDay(daytimeMeanTemperature))
                    {
                        ++chillingDays;
                        if (weatherDayIndex > leafOffTimeSeriesIndex)
                        {
                            ++this.chillingDaysCarriedOverFromPreviousCalendarYear;
                        }
                    }
                    if (chillingDays > establishment.ChillingDaysRequired)
                    {
                        tacaChillingDaysReached = true;
                    }

                    // growing degree days above the base temperature are counted beginning from the day where the chilling requirements are met
                    // up to a fixed day ending the veg period (Nitshke and Innes 2008 Figure 1)
                    if (weatherDayIndex <= leafOffTimeSeriesIndex)
                    {
                        // accumulate growing degree days
                        if (tacaChillingDaysReached && (daytimeMeanTemperature > establishment.GrowingDegreeDaysBaseTemperature))
                        {
                            float growingDegrees = daytimeMeanTemperature - establishment.GrowingDegreeDaysBaseTemperature;
                            this.GrowingDegreeDays += growingDegrees;
                            growingDegreeDaysBudburst += growingDegrees;
                        }
                        // if day-frost occurs, the growing degree day counter for budburst is reset
                        if (daytimeMeanTemperature <= 0.0F)
                        {
                            growingDegreeDaysBudburst = 0.0F;
                        }
                        if (growingDegreeDaysBudburst > establishment.GrowingDegreeDaysForBudburst)
                        {
                            budsHaveBurst = true;
                        }
                        if (budsHaveBurst && (minTemperature <= 0.0F))
                        {
                            ++frostDaysAfterBudburst;
                        }
                    }
                }
            }
            else if (weatherTimeSeries.Timestep == Timestep.Monthly)
            {
                // monthly TODO: microclimate + this.mGDD
                bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
                (int leafOffMonthIndex, int leafOnEndDayOfMonth) = SaplingEstablishment.GetLeafOffMonthlyWeatherIndices(weather, leafPhenology, isLeapYear);
                for (int monthIndex = 0, weatherMonthIndex = weatherTimeSeries.CurrentYearStartIndex; weatherMonthIndex < weatherTimeSeries.NextYearStartIndex; ++monthIndex, ++weatherMonthIndex)
                {
                    // if winterkill temperature is reached seedling establishment probability becomes zero
                    float minTemperature = weatherTimeSeries.TemperatureMin[weatherMonthIndex];
                    if (minTemperature < establishment.ColdFatalityTemperature)
                    {
                        tacaColdFatalityTemperatureNotReached = false;
                        break;
                    }

                    // frost free days
                    float daysInMonth = (float)DateTimeExtensions.GetDaysInMonth(monthIndex, isLeapYear);
                    float frostDaysInMonth = SaplingEstablishment.EstimateFrostDaysInMonth(minTemperature, daysInMonth);
                    frostFreeDaysInYear += daysInMonth - frostDaysInMonth;

                    // chilling days
                    float monthlyMeanDaytimeTemperature = weatherTimeSeries.TemperatureDaytimeMean[weatherMonthIndex];
                    float chillingDaysInMonth = SaplingEstablishment.EstimateChillingDaysInMonth(monthlyMeanDaytimeTemperature, daysInMonth);
                    chillingDays += chillingDaysInMonth;
                    if (chillingDays > establishment.ChillingDaysRequired)
                    {
                        tacaChillingDaysReached = true;
                    }

                    if (monthIndex == leafOffMonthIndex)
                    {
                        float monthLeafOffFraction = 1.0F - leafOnEndDayOfMonth / daysInMonth;
                        this.chillingDaysCarriedOverFromPreviousCalendarYear += monthLeafOffFraction * chillingDaysInMonth;
                    }
                    else if (monthIndex > leafOffMonthIndex)
                    {
                        this.chillingDaysCarriedOverFromPreviousCalendarYear += chillingDaysInMonth;
                    }

                    // growing degree days above the base temperature
                    if (monthIndex <= leafOffMonthIndex)
                    {
                        // accumulate growing degree days
                        float growingDegreeDaysInMonth = 0.0F;
                        if (tacaChillingDaysReached)
                        {
                            growingDegreeDaysInMonth = SaplingEstablishment.EstimateGrowingDegreeDays(monthlyMeanDaytimeTemperature, daysInMonth, establishment);
                            float fractionOfMonthAfterChillingDaysReached = (chillingDays - establishment.ChillingDaysRequired) / chillingDaysInMonth;
                            if (fractionOfMonthAfterChillingDaysReached < 1.0F)
                            {
                                growingDegreeDaysInMonth *= fractionOfMonthAfterChillingDaysReached;
                            }
                            this.GrowingDegreeDays += growingDegreeDaysInMonth;
                            growingDegreeDaysBudburst += growingDegreeDaysInMonth;
                        }
                        // if a frost day occurs before budburst, the growing degree day counter for budburst is reset
                        if (frostDaysInMonth >= 1.0F)
                        {
                            growingDegreeDaysBudburst = 0.0F;
                        }
                        if (growingDegreeDaysBudburst > establishment.GrowingDegreeDaysForBudburst)
                        {
                            budsHaveBurst = true;
                        }
                        if (budsHaveBurst)
                        {
                            Debug.Assert((growingDegreeDaysInMonth > 0.0F) && (growingDegreeDaysBudburst > establishment.GrowingDegreeDaysForBudburst));
                            float fractionOfMonthAfterBudburstAndBeforeLeafOff = (growingDegreeDaysBudburst - establishment.GrowingDegreeDaysForBudburst) / growingDegreeDaysInMonth;
                            if (fractionOfMonthAfterBudburstAndBeforeLeafOff > 1.0F)
                            {
                                fractionOfMonthAfterBudburstAndBeforeLeafOff = 1.0F; 
                            }
                            if (monthIndex == leafOffMonthIndex)
                            {
                                float monthLeafOffFraction = 1.0F - leafOnEndDayOfMonth / daysInMonth;
                                fractionOfMonthAfterBudburstAndBeforeLeafOff -= monthLeafOffFraction;
                            }
                            if (fractionOfMonthAfterBudburstAndBeforeLeafOff > 0.0F)
                            {
                                float frostDaysInMonthAfterBudburstAndBeforeLeafOff = fractionOfMonthAfterBudburstAndBeforeLeafOff * frostDaysInMonth;
                                frostDaysAfterBudburst += frostDaysInMonthAfterBudburstAndBeforeLeafOff;
                            }
                        }
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled weather timestep " + weatherTimeSeries.Timestep + ".");
            }

            float frostAndWaterModifier = 0.0F; // if any of TACA's sapling establishment requirements is not met
            if (tacaChillingDaysReached && tacaColdFatalityTemperatureNotReached)
            {
                // frost free days in vegetation period
                bool tacaFrostFreeDaysReached = frostFreeDaysInYear > establishment.MinimumFrostFreeDays;
                // growing degree day requirement
                bool tacaGrowingDegreeDaysInRange = (this.GrowingDegreeDays > establishment.MinimumGrowingDegreeDays) && (this.GrowingDegreeDays < establishment.MaximumGrowingDegreeDays);
                if (tacaFrostFreeDaysReached && tacaGrowingDegreeDaysInRange)
                {
                    // negative effect of frost events after bud birst
                    float frostModifier = 1.0F;
                    if (frostDaysAfterBudburst > 0)
                    {
                        frostModifier = MathF.Pow(establishment.FrostTolerance, MathF.Sqrt(frostDaysAfterBudburst));
                    }
                    // reduction in establishment due to water limitation and drought induced mortality
                    float waterModifier = SaplingEstablishment.GetMinimumLeafOnSoilWaterModifier(ruSpecies);
                    // negative effect of a thick soil organic layer on regeneration [1: no effect]
                    float frozenGroundModifier = SaplingEstablishment.GetSoilOrganicLayerDepthModifier(ruSpecies);

                    // combine water and frost effects multiplicatively
                    frostAndWaterModifier = frostModifier * waterModifier * frozenGroundModifier;
                }
            }

            return frostAndWaterModifier;
        }

        private static float EstimateChillingDaysInMonth(float estimatedMonthlyMeanDaytimeTemperature, float daysInMonth)
        {
            // regression from the HJ Andrews Research Forest where the primary and secondary meteorology stations' chilling days per month
            // are acceptably approximated by a clamped Gaussian function of the mean daytime temperature: R² = 0.972, MAE 1.59 days
            // Since monthly mean temperatures are highly correlated (Pearson R > 0.93 on the Andrews) including other monthly temperatures
            // has negligible effect on error as their colinearity suppresses estimation of variance within months. Other predictors may be
            // helpful but have not been tested.
            // To constrain error propagation, coefficients here are based on regressing estimated monthly mean daytime temperatures against
            // actual chilling days and should be updated if monthly mean daytime temperature estimation changes.
            float temperatureRelativeToCenter = estimatedMonthlyMeanDaytimeTemperature + 0.0717958F;
            float chillingDays = 0.8648351F * daysInMonth * MathF.Exp(-0.0199024F * temperatureRelativeToCenter * temperatureRelativeToCenter);
            // Debug.Assert((chillingDays >= 0.0F) && (chillingDays <= daysInMonth)); // guaranteed by model form so long as exponent is multiplied by <= 1.0F
            return chillingDays;
        }

        private static float EstimateFrostDaysInMonth(float monthlyMeanDailyMinimumTemperature, float daysInMonth)
        {
            // logistic regression from the HJ Andrews Research Forest primary and secondary meteorology stations' frost days per month
            // R² = 0.969, MAE 1.49 days
            return 0.91946F * daysInMonth / (1.0F + MathF.Exp(0.59956F * (monthlyMeanDailyMinimumTemperature - 0.45742F)));
        }

        private static float EstimateGrowingDegreeDays(float estimatedMonthlyMeanDaytimeTemperature, float daysInMonth, SaplingEstablishmentParameters establishment)
        {
            // piecewise regression from the HJ Andrews Research Forest primary and secondary meteorology stations' growing degree days per month
            // R² = 0.994, MAE 12.6 degree days at 3.4 °C baseline temperature, 12.9 at 4.0 °C
            if (estimatedMonthlyMeanDaytimeTemperature < -2.0F)
            {
                return 0.0F;
            }
            if (estimatedMonthlyMeanDaytimeTemperature < 7.5F)
            {
                float shiftedTemperature = estimatedMonthlyMeanDaytimeTemperature + 1.879775F + 0.7967818F * (3.4F - establishment.GrowingDegreeDaysBaseTemperature);
                return 0.049491F * shiftedTemperature * shiftedTemperature;
            }

            return -98.780242F + 0.986055F * daysInMonth * (estimatedMonthlyMeanDaytimeTemperature + 3.4F - establishment.GrowingDegreeDaysBaseTemperature);
        }

        private static int GetLeafOffDailyWeatherIndex(Weather weather, LeafPhenology leafPhenology)
        {
            return weather.TimeSeries.CurrentYearStartIndex + (leafPhenology.IsEvergreen ? weather.Sun.LastDayLongerThan10_5Hours : leafPhenology.LeafOnEndDayOfYearIndex);
        }

        private static (int leafOffMonthIndex, int leafOnEndDayOfMonth) GetLeafOffMonthlyWeatherIndices(Weather weather, LeafPhenology leafPhenology, bool isLeapYear)
        {
            int leafOnEndDayOfYearIndex = leafPhenology.IsEvergreen ? weather.Sun.LastDayLongerThan10_5Hours : leafPhenology.LeafOnEndDayOfYearIndex;
            return DateTimeExtensions.DayOfYearToDayOfMonth(leafOnEndDayOfYearIndex, isLeapYear);
        }

        /// calculate effect of water limitation on establishment, returns scalar [0..1]
        private static float GetMinimumLeafOnSoilWaterModifier(ResourceUnitTreeSpecies ruSpecies) // C++: Establishment::calculateWaterLimitation()
        {
            // return 1.0 if water limitation is disabled
            float psi_min = ruSpecies.Species.SaplingEstablishment.DroughtMortalityPsiInMPa;
            if (Single.IsNaN(psi_min))
            {
                return 1.0F;
            }

            // get the psi min of the current year
            float psi_mpa = ruSpecies.ResourceUnit.WaterCycle.GetEstablishmentPsiMin(ruSpecies.Species.LeafPhenologyID);

            // calculate the response of the species to this value of psi (see also Species::soilwaterResponse())
            float result = Maths.Limit((psi_mpa - psi_min) / (-0.015F - psi_min), 0.0F, 1.0F);
            return result;
        }

        /// limitation if the depth of the soil organic layer is high (e.g. boreal forests)
        private static float GetSoilOrganicLayerDepthModifier(ResourceUnitTreeSpecies ruSpecies)
        {
            float est_SOLlimit = ruSpecies.Species.SaplingEstablishment.SOL_thickness;
            if (est_SOLlimit == 0.0F)
            {
                return 1.0F; // no limitation for the current species
            }
            Permafrost? ruPermafrost = ruSpecies.ResourceUnit.WaterCycle.Permafrost;
            if (ruPermafrost == null)
            {
                return 1.0F; // no limitation if permafrost module is disabled
            }

            float depthInCm = 100.0F * (ruPermafrost.GetMossLayerThicknessInM() + ruPermafrost.SoilOrganicLayerDepthInM); // to cm
            float effect = MathF.Exp(-est_SOLlimit * depthInCm);
            return effect;
        }

        private static bool IsChillingDay(float daytimeMeanTemperature)
        {
            // Nitshke and Innes define chilling as days between ±5 °C following the midpoint of the growing seasion but do not indicate which
            // daily temperature is used in the determination
            // It appears plausible Nitshke and Innes meant the daily mean temperature and that, in iLand C++, this was transposed to the
            // daytime mean temperature as iLand doesn't flow input daily mean temperatures. This suggests iLand may underestimate chilling days
            // since daytime mean temperatures are warmer than daily mean temperatures (over the HJ Andrews this bias averages 0-4 days per
            // month, depending on station, with the largest differences in March, April, and November and typically totals -20 to +2 days per
            // year).
            return (daytimeMeanTemperature >= -5.0F) && (daytimeMeanTemperature < 5.0F);
        }
    }
}
