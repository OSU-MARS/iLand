using iLand.Extensions;
using iLand.Input;
using iLand.Input.Weather;
using iLand.Tool;
using System;
using System.Diagnostics;
using Moss = iLand.Input.ProjectFile.Moss;
using PermafrostSettings = iLand.Input.ProjectFile.Permafrost;

namespace iLand.World
{
    public class Permafrost
    {
        private FTResult mResult; ///< keep the results of the last run for debug output
        private readonly ResourceUnit resourceUnit;

        /// resource unit's maximum root accessible soil depth when fully thawed
        private float mSoilDepthInM; // ResourceUnitEnvironment.SoilPlantAccessibleDepthInCm, C++ mSoilDepth
        private float mFC; ///< field capacity iLand of the full soil column (mm)
        /// top of frozen layer (m) when thawing (above that soil is thawed)
        private float mTop;
        private bool mTopFrozen; ///< is the top of the soil frozen?
        /// bottom of the frozen layer (m) (important for seasonal permafrost; soil is frozen *up to* this depth)
        /// for permanent permafrost, the bottom is infinite?
        private float mBottomInM;
        private float mFreezeBackInM; ///< depth (m) up to which the soil is frozen again (in autumn)

        /// thermal conductivity for dry (unfrozen) soil W/m/K
        private float mKdry;
        /// thermal conductivity for saturated (unfrozen) soil W/m/K
        private float mKsat;
        /// thermal conductivity for saturated and frozen soil W/m/K
        private float mKice;
        // TODO: simplify to coefficient of thermal conductivity
        private bool mSoilIsCoarse; ///< switch based on sand content (used for calc. of thermal conductivity)

        private readonly PermafrostSettings settings; // TODO: remove

        /// depth (m) at where below the soil is frozen (at the end of the year)
        public float CurrentSoilFrozenInM { get; private set; } /// depth of soil (m) that is currently frozen (this is a part of the soil plant accessible soil), C++: Permafrost::depthFrozen(), mCurrentSoilFrozenInM
                                                                /// amount of water (mm) that is trapped in ice (at the end of the year)
        public float CurrentWaterFrozenInMM { get; private set; } // /// amount of water (mm) trapped currently in ice, C++: Permafrost::waterFrozen(), mCurrentSoilFrozen
        /// temperature deep below the surface (updated annually)
        public float GroundBaseTemperature { get; private set; } ///< temperature (C) of the zone with secondary heat flux, C++: Permafrost::groundBaseTemperature(), mGroundBaseTemperature        
        /// moss biomass (kg/m2)
        public float MossBiomass { get; private set; } ///< moss biomass in kg/m2, C++: Permafrost::mossBiomass(), mMossBiomass
        /// thickness of the soil organic layer (in meters)
        public float SoilOrganicLayerDepthInM { get; private set; } ///< depth of the soil organic layer on top of the mineral soil (m), C++: Permafrost::SOLLayerThickness(), mSOLDepthInM

        public SStats Statistics { get; private init; } // C++: stats

        public Permafrost(PermafrostSettings settings, ResourceUnit resourceUnit)
        {
            this.CurrentSoilFrozenInM = Single.NaN;
            this.CurrentWaterFrozenInMM = Single.NaN;
            this.mBottomInM = Single.NaN;
            this.mFC = Single.NaN;
            this.mFreezeBackInM = Single.NaN;
            this.GroundBaseTemperature = Single.NaN;
            this.mKdry = Single.NaN;
            this.mKice = Single.NaN;
            this.mKsat = Single.NaN;
            this.MossBiomass = Single.NaN;
            this.mResult = new();
            this.SoilOrganicLayerDepthInM = Single.NaN;
            this.mSoilIsCoarse = false;
            this.mTop = Single.NaN;
            this.resourceUnit = resourceUnit;
            this.settings = settings;

            this.Statistics = new();
        }

        /// thickness of the moss layer in meters
        public float GetMossLayerThicknessInM() { return this.MossBiomass / this.settings.Moss.BulkDensity; } // kg/m2 / rho [kg/m3] = m,  // C++: Permafrost::mossLayerThickness()

        /// burn some of the live moss (kg / ha)
        public void BurnMoss(float biomass_kg)
        {
            this.MossBiomass -= biomass_kg / Constant.Grid.ResourceUnitAreaInM2;
            this.MossBiomass = Single.Max(this.MossBiomass, Moss.MossMinBiomass);
        }

        /// add permafrost related debug output to the list 'out'
        //public void debugData(out DebugList)
        //{
        //    // permafrost
        //    out << mTop << mBottom << mFreezeBack << mResult.delta_mm << mResult.delta_soil
        //        << thermalConductivity(false) << mCurrentSoilFrozen << mCurrentWaterFrozen << mWC.mFieldCapacity;
        //    // moss
        //    out << stats.mossFLight << stats.mossFDecid;
        //}

        /// start a new year
        public void OnNewYear() // C++: Permafrost::newYear()
        {
            // reset stats
            this.Statistics.Clear();
            this.RunMossYear();

            // calculate the depth of the organic layer
            ResourceUnitSoil? s = this.resourceUnit.Soil;
            if (s != null)
            {
                // the fuel layer is the sum of yL (leaves, needles, and twigs) and yR (coarse downed woody debris) pools (t / ha)
                float abovegroundCarbon = s.YoungLabile.C * s.YoungLabileAbovegroundFraction + s.YoungRefractory.C * s.YoungRefractoryAbovegroundFraction;
                // biomass t/ha = kg moss biomass/kgC * 10*kg/m2 / rho [kg/m3] = m
                this.SoilOrganicLayerDepthInM = 0.1F / Constant.DryBiomassCarbonFraction * abovegroundCarbon / this.settings.SoilOrganicLayerDensity;
                // add moss layer
                this.SoilOrganicLayerDepthInM += this.GetMossLayerThicknessInM();
            }
            // adapt temperature of deep soil
            // 10 year running average
            this.GroundBaseTemperature = 0.9F * this.GroundBaseTemperature + 0.1F * this.resourceUnit.Weather.MeanAnnualTemperature;
        }

        /// run the permafrost calculations for a given resource unit and day
        public void CalculateTimestepFreezeThaw(int weatherTimestepIndex) // C++: Permafrost::run()
        {
            WeatherTimeSeries weatherSeries = this.resourceUnit.Weather.TimeSeries;
            int daysInTimestep = weatherSeries.GetDaysInTimestep(weatherTimestepIndex);
            FTResult delta;
            float meanTemperature = weatherSeries.EstimateMeanTemperature(weatherTimestepIndex);
            if (meanTemperature > 0.0F)
            {
                // thaw
                if (this.mFreezeBackInM > 0.0F)
                {
                    // first thaw the top layer that may be again frozen temporarily
                    delta = this.CalculateTimestepFreezeThaw(this.mFreezeBackInM, meanTemperature, daysInTimestep, lowerIceEdge: true, fromAbove: true);
                    this.mFreezeBackInM = delta.NewDepthInM;
                }
                else
                {
                    // thawing from above (soil above mTop is thawed)
                    delta = this.CalculateTimestepFreezeThaw(this.mTop, meanTemperature, daysInTimestep, lowerIceEdge: false, fromAbove: true);
                    this.mTop = delta.NewDepthInM;
                    if (this.mTop > 0.0F)
                    {
                        this.mTopFrozen = false;
                    }
                    if (this.mTop >= this.mBottomInM)
                    {
                        // the soil is fully thawed
                        this.mBottomInM = 0.0F;
                        this.mTop = 0.0F;
                        this.mFreezeBackInM = 0.0F;
                    }
                }
            }
            else if (meanTemperature < 0.0F)
            {
                // freezing
                if (this.mTopFrozen)
                {
                    // energy flows from above through the frozen soil
                    delta = this.CalculateTimestepFreezeThaw(this.mBottomInM, meanTemperature, daysInTimestep, lowerIceEdge: true, fromAbove: true);
                    this.mBottomInM = delta.NewDepthInM;
                }
                else
                {
                    // freeze back
                    delta = this.CalculateTimestepFreezeThaw(this.mFreezeBackInM, meanTemperature, daysInTimestep, lowerIceEdge: true, fromAbove: true);
                    this.mFreezeBackInM = delta.NewDepthInM;
                    if (this.mFreezeBackInM >= mTop)
                    {
                        // freeze back completed; the soil now is frozen
                        // from the top down to "bottom"
                        this.mTopFrozen = true;
                        this.mBottomInM = Single.Max(this.mTop, this.mBottomInM);
                        this.mTop = 0.0F;
                        this.mFreezeBackInM = 0.0F;
                    }
                }

                if ((weatherSeries.Month[weatherTimestepIndex] == 3) && ((weatherSeries.Timestep == Input.Timestep.Monthly) || (DateTimeExtensions.DayOfYearToDayOfMonth(weatherTimestepIndex, weatherSeries.IsCurrentlyLeapYear()).dayOfMonthIndex == 1)))
                {
                    // test for special cases
                    if (this.mFreezeBackInM < this.mTop && this.mFreezeBackInM > 0.0F)
                    {
                        // freezeback not completed, nonetheless we reset
                        this.mTopFrozen = true;
                        this.mBottomInM = Single.Max(this.mTop, this.mBottomInM);
                        this.mFreezeBackInM = 0.0F;
                        this.mTop = 0.0F;
                    }
                }
            }
            else
            {
                // no change if temp == 0
                delta = new();
            }

            // effect of ground temperature
            FTResult delta_ground = new();
            if (this.GroundBaseTemperature < 0.0F)
            {
                delta_ground = this.CalculateTimestepFreezeThaw(this.mTop, this.GroundBaseTemperature, daysInTimestep, lowerIceEdge: false, fromAbove: false);
                this.mTop = delta_ground.NewDepthInM;
            }
            if (this.GroundBaseTemperature > 0.0F)
            {
                delta_ground = this.CalculateTimestepFreezeThaw(this.mBottomInM, this.GroundBaseTemperature, daysInTimestep, lowerIceEdge: true, fromAbove: false);
                this.mBottomInM = delta_ground.NewDepthInM;
            }

            // keep some variables (debug outputs)
            this.mResult.WaterDeltaInMM = delta.WaterDeltaInMM + delta_ground.WaterDeltaInMM;
            this.mResult.IceDeltaInM = delta.IceDeltaInM + delta_ground.IceDeltaInM;

            // effect of freezing/thawing on the water storage of the iLand water bucket
            ResourceUnitWaterCycle waterCycle = this.resourceUnit.WaterCycle;
            if ((this.mResult.WaterDeltaInMM != 0.0F) && (this.mResult.IceDeltaInM != 0.0F) && (this.settings.SimulateOnly == false))
            {
                this.CurrentWaterFrozenInMM = Single.Min(Single.Max(this.CurrentWaterFrozenInMM - this.mResult.WaterDeltaInMM, 0.0F), this.mFC);

                Debug.Assert(waterCycle.SoilWaterRetention != null);
                float newActiveLayerDepthInMM = Single.Max(waterCycle.SoilWaterRetention.SoilPlantAccessibleDepthInMM + this.mResult.IceDeltaInM * 1000.0F, 0.0F); // change in mm
                this.CurrentSoilFrozenInM = Single.Min(Single.Max(this.CurrentSoilFrozenInM - this.mResult.IceDeltaInM, 0.0F), this.mSoilDepthInM);
                waterCycle.SetActiveLayerDepth(newActiveLayerDepthInMM);
            }

            // stats (annual)
            this.Statistics.MaxThawDepthInM = Single.Max(this.Statistics.MaxThawDepthInM, mBottomInM == 0.0F ? this.settings.MaxPermafrostDepth : mTop);
            this.Statistics.MaxFreezeDepthInM = Single.Max(this.Statistics.MaxFreezeDepthInM, mBottomInM);
            this.Statistics.MaxSnowDepthInM = Single.Max(this.Statistics.MaxSnowDepthInM, waterCycle.Snowpack.GetDepthInM());

            //    if (clim_day.month == 7 && mWC.mFieldCapacity == 0.)
            //        QMessageBox::warning(0, "Permafrost havoc!", "debug", QMessageBox::Ok, QMessageBox::Cancel);
        }

        public void SetFromSnapshot(float moss_biomass, float soil_temp, float depth_frozen, float water_frozen) // C++: Permafrost::setFromSnapshot()
        {
            this.MossBiomass = moss_biomass;
            this.GroundBaseTemperature = soil_temp;
            this.CurrentSoilFrozenInM = depth_frozen;
            this.CurrentWaterFrozenInMM = water_frozen;
        }

        public void Setup(ResourceUnitEnvironment environment) // C++: Permafrost::setup()
        {
            this.GroundBaseTemperature = this.settings.InitialGroundTemperature;

            float initialDepthFrozenInM = this.settings.InitialDepthFrozenInM;
            if (initialDepthFrozenInM < this.settings.MaxPermafrostDepth)
            {
                // seasonal permafrost
                this.mBottomInM = initialDepthFrozenInM;
            }
            else
            {
                this.mBottomInM = this.settings.MaxPermafrostDepth; // permanent permafrost
            }
            this.mTop = 0.0F; // we assume that the top of the soil is frozen at the beginning of the sim (1st of January)
            this.mTopFrozen = true;
            this.mFreezeBackInM = 0.0F; // we are not in "freezeback mode" (autumn)

            this.SoilOrganicLayerDepthInM = 0.0F;
            if (this.resourceUnit.Soil == null)
            {
                this.SoilOrganicLayerDepthInM = this.settings.SoilOrganicLayerDefaultDepth;
                // qWarning() << "Permafrost is enabled, but soil carbon cycle is not. Running Permafrost with constant soil organic layer (permafrost.organicLayerDefaultDepth)= " << mSOLDepth;
            }

            ResourceUnitWaterCycle waterCycle = this.resourceUnit.WaterCycle;
            Debug.Assert(waterCycle.SoilWaterRetention != null);
            this.mSoilDepthInM = 0.01F * environment.SoilPlantAccessibleDepthInCm; // max soil depth (m)
            this.mFC = waterCycle.FieldCapacityInMM;

            this.CurrentSoilFrozenInM = Single.Min(initialDepthFrozenInM, this.mSoilDepthInM);
            float fraction_frozen = this.CurrentSoilFrozenInM / this.mSoilDepthInM;
            this.CurrentWaterFrozenInMM = waterCycle.CurrentSoilWaterInMM * fraction_frozen;

            this.SetupSoilThermalConductivity(environment);
            this.MossBiomass = this.settings.Moss.Biomass;
            this.Statistics.Clear();

            if (this.settings.SimulateOnly == false)
            {
                waterCycle.SetActiveLayerDepth(1000.0F * (this.mSoilDepthInM - this.CurrentSoilFrozenInM));
            }
        }

        /// annual calculations for the moss layer
        private void RunMossYear() // C++: Permafrost::calculateMoss()
        {
            // See supplementary material S1 for details
            //if (mWC.mRU.id() == 58664)
            //    qDebug() << " debug debug debuk";

            // 1) Available Light
            // get leaf area index for canopy and moss layer
            float LAI_canopy = this.resourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.LeafAreaIndex;
            float LAI_moss = this.MossBiomass * Moss.SLA;

            float light_below = MathF.Exp(-this.settings.Moss.LightK * (LAI_canopy + LAI_moss));

            // f_light is a linear function with 0 at the light compensation point, and 1 at the light saturation level
            float f_light = (light_below - this.settings.Moss.LightCompensationPoint) / (this.settings.Moss.LightSaturationPoint - this.settings.Moss.LightCompensationPoint);
            f_light = Maths.Limit(f_light, 0.0F, 1.0F); // clamp to interval [0,1]

            // 2) dessication (=dryout) of moss if the canopy is too open (lack of stomatal control of moss plants)
            // removed the dessication effect for now. Doesn't work as expected, and dessication could be part of a (future)
            // rework of the water cycle
            //float al = exp(-0.25 * LAI_canopy);
            //float f_dryout = 1.0F;

            //if (al > 0.5)
            //    f_dryout = 1.25 - al*al;

            // (2.3) Effect of deciduous litter
            // get fresh deciduous litter (t/ha)
            float fresh_dec_litter = 0.0F;
            if (this.resourceUnit.Snags != null)
            {
                fresh_dec_litter = this.resourceUnit.Snags.DeciduousFoliageLitter / 1000.0F; // from kg/ha to tons/ha
            }

            float f_deciduous = MathF.Exp(-this.settings.Moss.DeciduousInhibitionFactor * fresh_dec_litter);

            // (3) Total productivity
            // Assimilation (kg/m2): modifiers reduce the potential productivity of 0.3 kg/m2/yr
            float moss_assimilation = Moss.MossMaxProductivity * f_light * f_deciduous; // (note: dessication was here: * f_dryout )

            // producitvity [kg / kg biomass/yr]
            float effective_assimilation = Moss.SLA * moss_assimilation;
            // annual respiration loss (kg/m2/yr) (flux to atmosphere)
            float moss_rt = MossBiomass * settings.Moss.RespirationQ;
            // annual turnover (biomass to replace) (kg/m2/yr) (flux to litter)
            float moss_turnover = MossBiomass * settings.Moss.RespirationB;

            // net productivty (kg/m2/yr): assimilation - respiration - turnover
            float moss_prod = effective_assimilation * MossBiomass - moss_rt - moss_turnover;

            // (4) update moss pool and add produced biomass
            this.MossBiomass += moss_prod;
            // avoid values below 0; assume a minimum biomass always remains
            this.MossBiomass = Single.Max(this.MossBiomass, Moss.MossMinBiomass);

            // dead moss is transferred to the forest floor fine litter pool in iLand
            if ((this.resourceUnit.Snags != null) && (moss_turnover > 0.0F))
            {
                // scale up from m2 to stockable area
                float stockable_area = this.resourceUnit.AreaInLandscapeInM2;
                CarbonNitrogenPool litter_input = new(Constant.DryBiomassCarbonFraction * stockable_area * moss_turnover,
                                                      Constant.DryBiomassCarbonFraction* stockable_area* moss_turnover / this.settings.Moss.CarbonNitrogenRatio,
                                                      this.settings.Moss.DecompositionRate);
                this.resourceUnit.Snags.AddBiomassToSoil(new CarbonNitrogenPool(), litter_input);
            }

            // save some stats for moss
            Statistics.MossFLight = f_light;
            Statistics.MossFDecididuous = f_deciduous;
            // stats.mossFCanopy = f_dryout;
        }

        /// setup of thermal properties of the soil on RU
        private void SetupSoilThermalConductivity(ResourceUnitEnvironment environment) // C++: Permafrost::setupThermalConductivity()
        {
            // Calcluation of thermal conductivity based on the approach of Farouki 1981 (as described in Bonan 2019)
            float pct_sand = environment.SoilSand;
            float pct_clay = environment.SoilClay;
            this.mSoilIsCoarse = pct_sand >= 50; // fine-texture soil: < 50% sand

            // (relative) volumetric water content at saturation = porosity
            ResourceUnitWaterCycle waterCycle = this.resourceUnit.WaterCycle;
            Debug.Assert(waterCycle.SoilWaterRetention != null);
            float VWCsat = waterCycle.SoilWaterRetention.SaturationPotentialInKPa;
            float rho_soil = 2700.0F * (1.0F - VWCsat);

            // Eq 5.27
            this.mKdry = (0.135F * rho_soil + 64.7F) / (2700.0F - 0.947F * rho_soil);

            // Conductivity of solids (Ksol): use an equation from CLM3 (https://opensky.ucar.edu/islandora/object/technotes:393)
            // scale between 8.8 (quartz) and 2.92 (clay), Eq 10
            float k_sol = (8.8F * pct_sand + 2.92F * pct_clay) / (pct_sand + pct_clay);

            // constants for water and ice (Bonan)
            const float k_water = 0.57F; // W/m/K water
            const float k_ice = 2.29F; // W/m/K ice

            // Eq 8/9
            this.mKsat = MathF.Pow(k_sol, (1.0F - VWCsat)) * MathF.Pow(k_water, VWCsat);
            this.mKice = MathF.Pow(k_sol, (1.0F - VWCsat)) * MathF.Pow(k_ice, VWCsat);

            // qDebug() << "Setup Permafrost: RID " << mWC.mRU.id() << QString(": VWCsat: %1, Kdry: %2, Ksat: %3, Kice: %4. (rho_soil: %5)").arg(VWCsat).arg(mKdry).arg(mKsat).arg(mKice).arg(rho_soil);
        }

        /// thermal conductivity of the mineral soil [W / m2 / K]
        private float GetSoilThermalConductivity(bool from_below) // C++: Permafrost::thermalConductivity()
        {
            //    static int bug_counter = 0;
            //    if (mWC.fieldCapacity()>0 && mWC.fieldCapacity() < 0.0000001)
            //        ++bug_counter;

            // assume full water saturation in the soil for energy flux from below
            ResourceUnitWaterCycle waterCycle = this.resourceUnit.WaterCycle;
            float rel_water_content;
            if (!from_below && waterCycle.FieldCapacityInMM > 0.001F)
            {
                rel_water_content = Maths.Limit(waterCycle.CurrentSoilWaterInMM / waterCycle.FieldCapacityInMM, 0.001F, 1.0F);
            }
            else
            {
                rel_water_content = 1.0F;
            }

            // Eq 4
            float k_e = 1.0F + (this.mSoilIsCoarse ? 0.7F : 1.0F) * MathF.Log10(rel_water_content);
            // Eq 4
            float k = this.mKdry + (this.mKsat - this.mKdry) * k_e;
            return k;
        }

        private float GetSoilThermalConductivityFrozen() // C++: Permafrost::thermalConductivityFrozen()
        {
            float rel_water_content = 1.0F; // assume saturation
            if (this.CurrentSoilFrozenInM > 0.0F)
            {
                rel_water_content = 0.001F * this.CurrentWaterFrozenInMM / this.CurrentSoilFrozenInM;
            }

            // for frozen soil k_e = rel_water_content
            float k = mKdry + (mKice - mKdry) * rel_water_content;
            return k;
        }

        private FTResult CalculateTimestepFreezeThaw(float at, float temp, int daysInTimestep, bool lowerIceEdge, bool fromAbove) // C++: Permafrost::calcFreezeThaw()
        {
            FTResult result = new()
            {
                OriginalDepthInM = at,
                NewDepthInM = at
            };

            // check for all frozen / thawed
            if ((this.mTop == 0.0F) && (this.mBottomInM == 0.0F) && (temp >= 0.0F))
            {
                return result; // everything is already thawed
            }
            if ((this.mTop == 0.0F) && (this.mBottomInM >= this.settings.MaxPermafrostDepth) && (temp <= 0.0F))
            {
                return result; // everything is frozen
            }

            ResourceUnitWaterCycle mWC = this.resourceUnit.WaterCycle;
            const float cTempIce = 0.0F; // temperature of frozen soil
            float Rtotal; // thermal resistence [m2*K / W]
            if (fromAbove)
            {
                // (1) calc thermal resistance of the soil (including snow and organic layer)
                float d_snow = mWC.Snowpack.GetDepthInM();
                float lambda_soil = GetSoilThermalConductivity(false);

                // thermal resistance of the whole sandwich of layers [m2*K / W]
                Rtotal = d_snow / this.settings.LambdaSnow + SoilOrganicLayerDepthInM / this.settings.LambdaOrganicLayer + Single.Max(at, 0.05F) / lambda_soil;
            }
            else
            {
                // energy flux from below
                float dist_to_layer = Single.Max(this.settings.GroundBaseDepth - at, 0.5F);
                float lambda_soil = this.GetSoilThermalConductivity(from_below: true); // unfrozen
                // todo: Question: is soil below fully saturated?
                if (temp < cTempIce)
                {
                    lambda_soil = this.GetSoilThermalConductivityFrozen(); // frozen
                }
                Rtotal = dist_to_layer / lambda_soil;
            }

            // energy flux I (J/s): depends on resistance and temperature difference (delta T)
            float I = 1.0F / Rtotal * (temp - cTempIce);

            // total energy transferred per day: I * MJ/day * days in timestep
            float Einput = I * 86400.0F / 1000000.0F * daysInTimestep;

            // this energy now can freeze (or thaw) an amount of water at the edge of the active layer
            // Efusion: MJ/litre = MJ/mm / m2; Einput/Efusion: mm/day; a positive value indicates thawing
            float delta_mm = Einput / this.settings.LatentHeatOfFusion;
            // the amount of freezing/thawing is capped per day (to ensure numerical stability close to the surface)
            float maxFreezeThawInMM = this.settings.MaxFreezeThawPerDayInMMH2O * daysInTimestep;
            delta_mm = Single.Max(Single.Min(delta_mm, maxFreezeThawInMM), -maxFreezeThawInMM);

            //float max_water_content = mWC.fieldCapacity() / this.mSoilDepth; // [-]

            // the water content of soil to freeze is determined by the current water content (mm/mm)
            float current_water_content = this.mSoilDepthInM > 0.0F ? mWC.CurrentSoilWaterInMM / this.mSoilDepthInM : 0.0F;
            // for thawing, the water content is taken from the "frozen soil bucket"
            if ((I > 0.0F) && (this.CurrentSoilFrozenInM > 0.0F))
            {
                current_water_content = this.CurrentWaterFrozenInMM / this.CurrentSoilFrozenInM / 1000.0F;
            }

            // convert to change in units of soil (m)
            float delta_soil;
            // we use the actual iLand water content only if there is at least 10cm of unfrozen soil left, and
            // the the current position is within the depth of the iLand soil. We assume saturated conditions otherwise.
            if ((current_water_content > 0.0F) && (this.mSoilDepthInM > 100.0F) && (at < mSoilDepthInM))
            {
                delta_soil = delta_mm / current_water_content / 1000.0F; // current water content
            }
            else
            {
                delta_soil = delta_mm / (mFC / mSoilDepthInM); // assume saturated soil
            }

            float new_depth;
            if (lowerIceEdge)
            {
                new_depth = at - delta_soil;
            }
            else
            {
                new_depth = at + delta_soil;
            }

            // test against boundaries and limit
            // if further freezing cannot be realized (because all water is frozen already), no change in soil depth
            if ((delta_soil == 0.0F) && (delta_mm < 0.0F))
            {
                delta_mm = 0.0F;
            }

            // fluxes within the depth of the effective soil depth
            if (new_depth < 0.0F)
            {
                // full thawing can not be realized
                float factor = Single.Abs(at / delta_soil);
                delta_mm *= factor;
                delta_soil *= factor;
                new_depth = 0.0F;
            }
            else if (at > this.mSoilDepthInM && new_depth > this.mSoilDepthInM)
            {
                // no effect on effective soil layer (changes happening to deep below)
                delta_mm = 0.0F;
                delta_soil = 0.0F;
            }
            else if ((at <= this.mSoilDepthInM && new_depth > this.mSoilDepthInM) || (at >= this.mSoilDepthInM && new_depth < this.mSoilDepthInM))
            {
                // full freezing/thawing around the lower boundary of the soil
                // can not be realized
                float factor = 1.0F - Single.Abs((new_depth - this.mSoilDepthInM) / delta_soil);
                delta_mm *= factor;
                delta_soil *= factor;
            }

            if (new_depth > this.settings.MaxPermafrostDepth)
            {
                // limit to max depth of permafrost - no effect on fluxes
                new_depth = this.settings.MaxPermafrostDepth;
            }

            result.WaterDeltaInMM = delta_mm;
            result.IceDeltaInM = delta_soil;
            result.NewDepthInM = new_depth;
            return result;
        }

        // TODO: remove
        private struct FTResult
        {
            public float WaterDeltaInMM { get; set; } // change of water (mm within iLand water bucket), freezing is negative, C++ delta_mm
            public float IceDeltaInM { get; set; } // change of ice layer (m) (within iLand water bucket), freezing is negative, C++ delta_soil
            public float NewDepthInM { get; set; } // final depth (m), C++ new_depth
            public float OriginalDepthInM { get; set; }  // starting depth (m), C++ orig_depth

            public FTResult()
            {
                this.WaterDeltaInMM = 0.0F;
                this.IceDeltaInM = 0.0F;
                this.NewDepthInM = 0.0F;
                this.OriginalDepthInM = 0.0F;
            }
        }

        public class SStats // C++: SStats
        {
            public float MaxSnowDepthInM { get; set; } ///< maxium snow depth (m) of a year, C++ maxSnowDepth
            public int DaysOfSnowCover { get; set; } ///< days of the year with snow cover, C++ daysSnowCover
            public float MaxFreezeDepthInM { get; set; } ///< maximum depth of frozen soil (m), C++ maxFreezeDepth
            public float MaxThawDepthInM { get; set; } ///< maximum depth of thawed soil (m), C++ maxThawDepth
            public float MossFLight { get; set; }  ///< value of fLight (-), C++ mossFLight
            public float MossFDecididuous { get; set; }  ///< value of fDecid (-), C++ mossFDecid

            public void Clear() // C++: SStats::reset()
            {
                this.DaysOfSnowCover = 0;
                this.MaxFreezeDepthInM = 0.0F;
                this.MaxSnowDepthInM = 0.0F;
                this.MaxThawDepthInM = 0.0F; 
            }
        }
    }
}
