// C++/core/snag.h
using iLand.Extensions;
using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.World;
using System;
using System.Diagnostics;
using System.Management.Automation.Runspaces;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    public class ResourceUnitSnags
    {
        private readonly float dbhLowerBreak; // diameter thresholds used to classify to SWD-Pools
        private readonly float dbhHigherBreak;
        private readonly float[] carbonThresholdByClass; // carbon content thresholds that are used to decide if the SWD-pool should be emptied
        private readonly float[] currentDecayRateByClass; // swd decay rate (average for trees of the current year)
        private readonly CarbonNitrogenPool[] toStandingWoodyByClass; // transfer pool; input of the year is collected here (for each size class)
        private readonly CarbonNitrogenTuple totalSnagInput; // total input to the snag state (i.e. mortality/harvest and litter)
        private CarbonNitrogenTuple standingWoodyToSoil; // total flux from standing dead wood (book-keeping) -> soil (kg/ha)

        public ResourceUnit ResourceUnit { get; private init; } // link to resource unit
        public CarbonNitrogenPool[] StandingWoodyDebrisByClass { get; private init; } // standing woody debris pool (0: smallest dimater class, e.g. <10cm, 1: medium, 2: largest class (e.g. >30cm)) kg/ha
        public float[] NumberOfSnagsByClass { get; private init; } // number of snags in diameter class
        public float[] AverageDbhByClass { get; private init; } // average diameter in class (cm)
        public float[] AverageHeightByClass { get; private init; } // average height in class (m)
        public float[] AverageVolumeByClass { get; private init; } // average volume in class (m3)
        public float[] TimeSinceDeathByClass { get; private init; } // time since death: mass-weighted age of the content of the snag pool
        public float[] StemDecompositionRateByClass { get; private init; } // standing woody debris decay rate (weighted average of species values)
        public float[] HalfLifeByClass { get; private init; } // half-life values (yrs) (averaged)
        public CarbonNitrogenPool[] BranchesAndCoarseRootsByYear { get; private init; } // pool for branch biomass and coarse root biomass (C++ mOtherWood)
        public int BranchCounter { get; set; } // index which of the branch pools should be emptied

        public float DeciduousFoliageLitter { get; private set; } // foliage litter (biomass) form deciduous trees of the year (kg/ha) [Note: is cleared at the end of the year] (C++ freshDeciduousFoliage(), mDeciduousFoliageLitter)
        public float LabileFluxAbovegroundCarbon { get; private set; } // C input to the labile flux from aboveground sources (kg/ha) (C++ labileFluxAbovegroundCarbon(), mLabileFluxAbovegroundCarbon)
        public float RefractoryFluxAbovegroundCarbon { get; private set; } // C input to the woody (refractory) pools from aboveground sources (kg/ha) (C++ refractoryFluxAbovegroundCarbon(), mRefrFluxAbovegroundCarbon)
        public float OtherWoodAbovegroundFraction { get; private set; } // fraction of other wood from aboveground

        public CarbonNitrogenTuple FluxToAtmosphere { get; private init; } // total kg/ha heterotrophic respiration / flux to atm
        public CarbonNitrogenTuple FluxToDisturbance { get; private set; } // total kg/ha due to disturbance (e.g. fire)
        public CarbonNitrogenTuple FluxToExtern { get; private init; } // total kg/ha harvests
        public CarbonNitrogenPool LabileFlux { get; private init; } // litter flux to the soil (kg/ha) (C++ labileFlux(), mLabileFlux)
        public CarbonNitrogenPool RefractoryFlux { get; private set; } // deadwood flux to the soil (kg/ha) (C++ refractoryFlux(), mRefractoryFlux)
        // TODO: resolve conflicting units in C++ getter (kg/RU) and field (kg/ha) comments
        public float StandingAndDebrisCarbon { get; private set; } // total carbon in snags (kg/RU): not scaled to 1ha!!, sum of carbon content in all snag compartments (kg/ha)  (C++ totalCarbon(), mTotalSnagCarbon)
        public CarbonNitrogenTuple TotalStanding { get; private set; } // sum of C and N in SWD pools (stems) kg/ha
        public CarbonNitrogenTuple TotalBranchesAndRoots { get; private set; } // sum of C and N in other woody pools (branches + coarse roots) kg/ha (C++ totalOtherWood(), mTotalOther)
        public float WeatherFactor { get; set; } // the 're' climate factor to modify decay rates (also used in ICBM/2N model)

        public ResourceUnitSnags(Project projectFile, ResourceUnit resourceUnit, ResourceUnitEnvironment environment)
        {
            // class size of snag classes
            // swdDBHClass12: class break between classes 1 and 2 for standing snags (DBH, cm)
            // swdDBHClass23: class break between classes 2 and 3 for standing snags (DBH, cm)
            float lowerDbhBreak = projectFile.World.Snag.DbhBreakpointSmallMedium;
            float upperDbhBreak = projectFile.World.Snag.DdhBreakpointMediumLarge;
            if ((lowerDbhBreak < 0.0F) || (lowerDbhBreak >= upperDbhBreak))
            {
                throw new ArgumentOutOfRangeException(nameof(projectFile), "Lower diameter class break in ProjectFile.Model.Settings.Soil is either negative or above the upper diameter class break.");
            }

            this.carbonThresholdByClass = new float[3];
            this.currentDecayRateByClass = new float[3];
            this.dbhLowerBreak = lowerDbhBreak;
            this.dbhHigherBreak = upperDbhBreak;
            this.standingWoodyToSoil = new();
            this.toStandingWoodyByClass = [ new CarbonNitrogenPool(), new CarbonNitrogenPool(), new CarbonNitrogenPool() ];
            this.totalSnagInput = new();

            this.AverageDbhByClass = new float[3];
            this.AverageHeightByClass = new float[3];
            this.AverageVolumeByClass = new float[3];
            this.HalfLifeByClass = new float[3];
            this.StemDecompositionRateByClass = new float[3];
            this.LabileFlux = new();
            this.NumberOfSnagsByClass = new float[3];
            this.BranchesAndCoarseRootsByYear = new CarbonNitrogenPool[5];
            this.ResourceUnit = resourceUnit;
            this.StandingWoodyDebrisByClass = [ new CarbonNitrogenPool(), new CarbonNitrogenPool(), new CarbonNitrogenPool() ];
            this.TimeSinceDeathByClass = new float[3];

            this.FluxToAtmosphere = new();
            this.FluxToDisturbance = new();
            this.FluxToExtern = new();
            this.RefractoryFlux = new();
            this.TotalBranchesAndRoots = new();
            this.TotalStanding = new();

            // threshold levels for emptying out the dbh-snag-classes
            // derived from PSME woody allometry, converted to C, with a threshold level set to 1%
            // values in kg!
            this.carbonThresholdByClass[0] = 0.5F * lowerDbhBreak;
            this.carbonThresholdByClass[1] = lowerDbhBreak + 0.5F * (upperDbhBreak - lowerDbhBreak);
            this.carbonThresholdByClass[2] = upperDbhBreak + 0.5F * (upperDbhBreak - lowerDbhBreak);
            for (int diameterClass = 0; diameterClass < 3; ++diameterClass)
            {
                this.carbonThresholdByClass[diameterClass] = 0.10568F * MathF.Pow(this.carbonThresholdByClass[diameterClass], 2.4247F) * 0.5F * 0.01F;
            }

            this.ResourceUnit = resourceUnit;
            this.WeatherFactor = 0.0F;
            // branches
            this.BranchCounter = 0;
            for (int diameterClass = 0; diameterClass < this.currentDecayRateByClass.Length; ++diameterClass)
            {
                this.currentDecayRateByClass[diameterClass] = 0.0F;

                this.AverageDbhByClass[diameterClass] = 0.0F;
                this.AverageHeightByClass[diameterClass] = 0.0F;
                this.AverageVolumeByClass[diameterClass] = 0.0F;
                this.StemDecompositionRateByClass[diameterClass] = 0.0F;
                this.HalfLifeByClass[diameterClass] = 0.0F;
                this.NumberOfSnagsByClass[diameterClass] = 0.0F;
                this.TimeSinceDeathByClass[diameterClass] = 0.0F;
            }

            this.StandingAndDebrisCarbon = 0.0F;
            if (this.dbhLowerBreak <= 0.0)
            {
                throw new NotSupportedException("SetupThresholds() not called or called with invalid parameters.");
            }

            // Inital values from XML file
            // put carbon of snags to the middle size class
            this.StandingWoodyDebrisByClass[1].C = environment.SnagStemCarbon;
            this.StandingWoodyDebrisByClass[1].N = this.StandingWoodyDebrisByClass[1].C / environment.SnagStemCNRatio;
            this.StandingWoodyDebrisByClass[1].DecompositionRate = environment.SnagStemDecompositionRate;
            this.StemDecompositionRateByClass[1] = environment.SnagStemDecompositionRate;
            this.NumberOfSnagsByClass[1] = environment.SnagsPerResourceUnit;
            this.HalfLifeByClass[1] = environment.SnagHalfLife;
            // and for the Branch/coarse root pools: split the init value into five chunks
            CarbonNitrogenPool branches = new(environment.SnagBranchRootCarbon,
                                              environment.SnagBranchRootCarbon / environment.SnagBranchRootCNRatio,
                                              environment.SnagBranchRootDecompositionRate);
            this.StandingAndDebrisCarbon = branches.C + this.StandingWoodyDebrisByClass[1].C;
            this.OtherWoodAbovegroundFraction = environment.SnagOtherWoodAbovegroundFraction;
            this.DeciduousFoliageLitter = 0.0F;

            branches *= 1.0F / this.BranchesAndCoarseRootsByYear.Length;
            for (int diameterClass = 0; diameterClass < this.BranchesAndCoarseRootsByYear.Length; ++diameterClass)
            {
                this.BranchesAndCoarseRootsByYear[diameterClass] = branches;
            }
        }

        public bool HasNoCarbon()
        {
            return this.LabileFlux.HasNoCarbon() && this.RefractoryFlux.HasNoCarbon() && (this.StandingAndDebrisCarbon == 0.0F);
        }

        /// a tree dies and the biomass of the tree is split between snags/soils/removals
        /// @param tree the tree to process
        /// @param stem_to_snag fraction (0..1) of the stem biomass that should be moved to a standing snag
        /// @param stem_to_soil fraction (0..1) of the stem biomass that should go directly to the soil
        /// @param branch_to_snag fraction (0..1) of the branch biomass that should be moved to a standing snag
        /// @param branch_to_soil fraction (0..1) of the branch biomass that should go directly to the soil
        /// @param foliage_to_soil fraction (0..1) of the foliage biomass that should go directly to the soil
        public void AddDisturbance(TreeListSpatial tree, int treeIndex, float stemToSnag, float stemToSoil, float branchToSnag, float branchToSoil, float foliageToSoil) 
        {
            this.AddBiomassPools(tree, treeIndex, stemToSnag, stemToSoil, branchToSnag, branchToSoil, foliageToSoil);
        }

        private int GetDiameterClassIndex(float dbh)
        {
            if (dbh < this.dbhLowerBreak)
            {
                return 0;
            }
            if (dbh > this.dbhHigherBreak)
            {
                return 2;
            }
            return 1;
        }

        public void ScaleInitialState()
        {
            float areaFactor = this.ResourceUnit.AreaInLandscapeInM2 / Constant.Grid.ResourceUnitAreaInM2; // fraction stockable area
            // avoid huge snag pools on very small resource units (see also soil.cpp)
            // area_factor = std::max(area_factor, 0.1);
            this.StandingWoodyDebrisByClass[1] *= areaFactor;
            this.NumberOfSnagsByClass[1] *= areaFactor;
            for (int diameterClass = 0; diameterClass < this.BranchesAndCoarseRootsByYear.Length; ++diameterClass)
            {
                this.BranchesAndCoarseRootsByYear[diameterClass] *= areaFactor;
            }
            this.StandingAndDebrisCarbon *= areaFactor;
        }

        // debug outputs
        //public List<object> DebugList()
        //{
        //    // list columns
        //    // for three pools
        //    List<object> list = new()
        //    {
        //        // totals
        //        mTotalCarbon, mTotalIn.C, FluxToAtmosphere.C, mStandingWoodyToSoil.C, mStandingWoodyToSoil.N,
        //        // fluxes to labile soil pool and to refractory soil pool
        //        LabileFlux.C, LabileFlux.N, RefractoryFlux.C, RefractoryFlux.N
        //    };
        //    for (int i = 0; i < 3; i++)
        //    {
        //        // pools "swdx_c", "swdx_n", "swdx_count", "swdx_tsd", "toswdx_c", "toswdx_n"
        //        list.AddRange(new object[] { mStandingWoodyDebris[i].C, mStandingWoodyDebris[i].N, mNumberOfSnags[i], mTimeSinceDeath[i], mToStandingWoody[i].C, mToStandingWoody[i].N,
        //                                     mAvgDbh[i], mAvgHeight[i], mAvgVolume[i] });
        //    }

        //    // branch/coarse wood pools (5 yrs)
        //    for (int i = 0; i < 5; i++)
        //    {
        //        list.Add(mOtherWood[i].C);
        //        list.Add(mOtherWood[i].N);
        //    }
        //    //    list.AddRange(new object[] { mOtherWood[mBranchCounter].C, mOtherWood[mBranchCounter].N,
        //    //           , mOtherWood[(mBranchCounter+1)%5].C, mOtherWood[(mBranchCounter+1)%5].N,
        //    //           , mOtherWood[(mBranchCounter+2)%5].C, mOtherWood[(mBranchCounter+2)%5].N,
        //    //           , mOtherWood[(mBranchCounter+3)%5].C, mOtherWood[(mBranchCounter+3)%5].N,
        //    //           , mOtherWood[(mBranchCounter+4)%5].C, mOtherWood[(mBranchCounter+4)%5].N });
        //    return list;
        //}

        // C++/core/snag.cpp: newYear()
        public void OnStartYear()
        {
            for (int classIndex = 0; classIndex < this.toStandingWoodyByClass.Length; ++classIndex)
            {
                this.toStandingWoodyByClass[classIndex].Zero(); // clear transfer pools to standing-woody-debris
                this.currentDecayRateByClass[classIndex] = 0.0F;
            }

            this.standingWoodyToSoil.Zero();
            this.totalSnagInput.Zero();

            this.FluxToAtmosphere.Zero();
            this.FluxToExtern.Zero();
            this.FluxToDisturbance.Zero();
            this.LabileFlux.Zero();
            this.RefractoryFlux.Zero();

            this.LabileFluxAbovegroundCarbon = 0.0F;
            this.RefractoryFluxAbovegroundCarbon = 0.0F;
        }

        /// <summary>
        /// Calculate the dynamic weather factor for decomposition 're'.
        /// </summary>
        /// <remarks>
        /// Decomposition rates are calculated per ResourceUnit because this is the granularity of water cycle tracking. It is assumed this function is called
        /// once per annual time step after the RU's water cycle has been updated for the year.
        /// </remarks>
        public void CalculateWeatherFactors(Model model)
        {
            // calculate the water-factor for each month
            // Adair CE, Parton WJ, del Grosso SL, et al. 2008. Simple three-pool model accurately describes patterns of long-term litter
            //   decomposition in diverse climates. Global Change Biology 14(11):2636-2660. https://doi.org/10.1111/j.1365-2486.2008.01674.x
            Span<float> waterFactorByMonth = stackalloc float[Constant.Time.MonthsInYear];
            for (int monthIndex = 0; monthIndex < Constant.Time.MonthsInYear; ++monthIndex)
            {
                float precipET0ratio;
                if (this.ResourceUnit.WaterCycle.Canopy.ReferenceEvapotranspirationByMonth[monthIndex] > 0.0F)
                {
                    precipET0ratio = this.ResourceUnit.Weather.PrecipitationByMonth[monthIndex] / this.ResourceUnit.WaterCycle.Canopy.ReferenceEvapotranspirationByMonth[monthIndex];
                }
                else
                {
                    precipET0ratio = 0.0F;
                }
                waterFactorByMonth[monthIndex] = 1.0F / (1.0F + 30.0F * MathF.Exp(-8.5F * precipET0ratio));
                // Debug.WriteLine("month " + month + " PET " + this.RU.WaterCycle.ReferenceEvapotranspiration()[month] + " prec " + this.RU.Weather.PrecipitationByMonth[month]);
            }

            bool use_microclimate = model.Project.Model.Microclimate.Enabled && model.Project.Model.Microclimate.DecompositionEffect;

            // the calculation of weather factors requires calculated evapotranspiration. In cases without vegetation (trees or saplings)
            float weatherFactorSumForYear = 0.0F;
            WeatherTimeSeries weatherTimeSeries = this.ResourceUnit.Weather.TimeSeries;
            for (int weatherTimestepIndex = weatherTimeSeries.CurrentYearStartIndex, dayOfYear = 0; weatherTimestepIndex != weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex, ++dayOfYear)
            {
                // empirical variable Q10 model of Lloyd and Taylor (1994), see also Adair et al. (2008)
                float ft = MathF.Exp(308.56F * (1.0F / 56.02F - 1.0F / (273.15F + weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex] - 227.13F)));

                float temp_day = weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex];
                if (use_microclimate)
                {
                    Debug.Assert(this.ResourceUnit.Microclimate != null); ;
                    float mc_mean_buffer = this.ResourceUnit.Microclimate.GetMeanMicroclimateBuffering(weatherTimeSeries.Month[weatherTimestepIndex] - 1);
                    temp_day += mc_mean_buffer;
                }

                // empirical variable Q10 model of Lloyd and Taylor (1994), see also Adair et al. (2008)
                // Note: function becomes unstable with very low temperatures (e.g. Alaska)
                ft = temp_day > -30.0F ? MathF.Exp(308.56F * (1.0F / 56.02F - 1.0F / ((273.15F + temp_day) - 227.13F))) : 0.0F;
                float fw = waterFactorByMonth[weatherTimeSeries.Month[weatherTimestepIndex] - 1];

                weatherFactorSumForYear += ft * fw;
            }

            // the weather factor is defined as the arithmentic annual mean value
            float weatherTimestepsInYear = weatherTimeSeries.Timestep switch
            {
                Timestep.Daily => DateTimeExtensions.GetDaysInYear(this.ResourceUnit.Weather.TimeSeries.IsCurrentlyLeapYear()),
                Timestep.Monthly => Constant.Time.MonthsInYear,
                _ => throw new NotSupportedException("Unhandled weather timestep " + weatherTimeSeries.Timestep + ".")
            };
            this.WeatherFactor = weatherFactorSumForYear / weatherTimestepsInYear;
        }

        /// set deciduous foliage litter to 0 - this is not ideal, but timing is complicated
        /// as litter input comes from both trees and saplings, and the only "user" at the moment
        /// is permafrost, which is executed between both processes
        public void ZeroDeciduousFoliage()
        {
            this.DeciduousFoliageLitter = 0.0F;
        }

        /// do the yearly calculation
        /// see https://iland-model.org/snag+dynamics
        public void RunYear(Model model)
        {
            this.standingWoodyToSoil.Zero();

            // calculate anyway, because also the soil module needs it (and currently one can have Snag and Soil only as a couple)
            this.CalculateWeatherFactors(model);
            float weatherFactor = this.WeatherFactor;
            if (this.HasNoCarbon()) // nothing to do
            {
                return;
            }

            // process branches and coarse roots: every year one of the five baskets is emptied and transfered to the refractory soil pool
            this.RefractoryFlux += this.BranchesAndCoarseRootsByYear[BranchCounter];
            this.RefractoryFluxAbovegroundCarbon += this.BranchesAndCoarseRootsByYear[this.BranchCounter].C * this.OtherWoodAbovegroundFraction; // content * aboveground_fraction
            this.BranchesAndCoarseRootsByYear[this.BranchCounter].Zero();
            this.BranchCounter = (this.BranchCounter + 1) % this.BranchesAndCoarseRootsByYear.Length; // increase index, roll over to 0.

            // decay of branches/coarse roots
            for (int year = 0; year < this.BranchesAndCoarseRootsByYear.Length; ++year)
            {
                if (this.BranchesAndCoarseRootsByYear[year].C > 0.0F)
                {
                    float survive_rate = MathF.Exp(-weatherFactor * this.BranchesAndCoarseRootsByYear[year].DecompositionRate); // parameter: the "kyr" value...
                    this.FluxToAtmosphere.C += this.BranchesAndCoarseRootsByYear[year].C * (1.0F - survive_rate); // flux to atmosphere (decayed carbon)
                    this.BranchesAndCoarseRootsByYear[year].C *= survive_rate;
                }
            }

            // process standing snags.
            // the input of the current year is in the mToSWD-Pools
            for (int diameterClass = 0; diameterClass < this.toStandingWoodyByClass.Length; ++diameterClass)
            {
                // update the swd-pool with this years' input
                if (this.toStandingWoodyByClass[diameterClass].HasNoCarbon() == false)
                {
                    // update decay rate (apply average yearly input to the state parameters)
                    this.StemDecompositionRateByClass[diameterClass] = this.StemDecompositionRateByClass[diameterClass] * (this.StandingWoodyDebrisByClass[diameterClass].C / (this.StandingWoodyDebrisByClass[diameterClass].C + toStandingWoodyByClass[diameterClass].C)) + currentDecayRateByClass[diameterClass] * (this.toStandingWoodyByClass[diameterClass].C / (this.StandingWoodyDebrisByClass[diameterClass].C + this.toStandingWoodyByClass[diameterClass].C));
                    //move content to the SWD pool
                    this.StandingWoodyDebrisByClass[diameterClass] += this.toStandingWoodyByClass[diameterClass];
                }

                if (this.StandingWoodyDebrisByClass[diameterClass].C > 0.0F)
                {
                    // reduce the Carbon (note: the N stays, thus the CN ratio changes)
                    // use the decay rate that is derived as a weighted average of all standing woody debris
                    float survive_rate = MathF.Exp(-this.StemDecompositionRateByClass[diameterClass] * weatherFactor); // 1: timestep
                    this.FluxToAtmosphere.C += this.StandingWoodyDebrisByClass[diameterClass].C * (1.0F - survive_rate);
                    this.StandingWoodyDebrisByClass[diameterClass].C *= survive_rate;

                    // transition to downed woody debris
                    // update: use negative exponential decay, species parameter: half-life
                    // modified for the climatic effect on decomposition, i.e. if decomp is slower, snags stand longer and vice versa
                    // this is loosely oriented on Standcarb2 (http://andrewsforest.oregonstate.edu/pubs/webdocs/models/standcarb2.htm),
                    // where lag times for cohort transitions are linearly modified with re although here individual good or bad years have
                    // an immediate effect, the average climatic influence should come through (and it is inherently transient)
                    // note that swd.hl is species-specific, and thus a weighted average over the species in the input (=mortality)
                    // needs to be calculated, followed by a weighted update of the previous swd.hl.
                    // As weights here we use stem number, as the processes here pertain individual snags
                    // calculate the transition probability of SWD to downed dead wood
                    float halfLife = this.HalfLifeByClass[diameterClass] / weatherFactor;
                    float rate = -Constant.Math.Ln2 / halfLife;

                    // higher decay rate for the class with smallest diameters
                    if (diameterClass == 0)
                    {
                        rate *= 2.0F;
                    }
                    float transfer = 1.0F - MathF.Exp(rate);
                    Debug.Assert((0.0F <= transfer) && (transfer <= 1.0F));

                    // calculate flow to soil pool...
                    this.standingWoodyToSoil += this.StandingWoodyDebrisByClass[diameterClass] * transfer;
                    this.RefractoryFlux += this.StandingWoodyDebrisByClass[diameterClass] * transfer;
                    this.RefractoryFluxAbovegroundCarbon += this.StandingWoodyDebrisByClass[diameterClass].C * transfer; // all snag biomass is aboveground

                    this.StandingWoodyDebrisByClass[diameterClass] *= 1.0F - transfer; // reduce pool
                    // calculate the stem number of remaining snags
                    this.NumberOfSnagsByClass[diameterClass] = NumberOfSnagsByClass[diameterClass] * (1.0F - transfer);

                    this.TimeSinceDeathByClass[diameterClass] += 1.0F;
                    // if stems<0.5, empty the whole cohort into DWD, i.e. release the last bit of C and N and clear the stats
                    // also, if the Carbon of an average snag is less than 10% of the original average tree
                    // (derived from allometries for the three diameter classes), the whole cohort is emptied out to DWD
                    if (this.NumberOfSnagsByClass[diameterClass] < 0.5 || this.StandingWoodyDebrisByClass[diameterClass].C / this.NumberOfSnagsByClass[diameterClass] < carbonThresholdByClass[diameterClass])
                    {
                        // clear the pool: add the rest to the soil, clear statistics of the pool
                        this.RefractoryFlux += this.StandingWoodyDebrisByClass[diameterClass];
                        this.RefractoryFluxAbovegroundCarbon += this.StandingWoodyDebrisByClass[diameterClass].C;
                        this.standingWoodyToSoil += this.StandingWoodyDebrisByClass[diameterClass];
                        this.StandingWoodyDebrisByClass[diameterClass].Zero();
                        this.AverageDbhByClass[diameterClass] = 0.0F;
                        this.AverageHeightByClass[diameterClass] = 0.0F;
                        this.AverageVolumeByClass[diameterClass] = 0.0F;
                        this.StemDecompositionRateByClass[diameterClass] = 0.0F;
                        this.currentDecayRateByClass[diameterClass] = 0.0F;
                        this.HalfLifeByClass[diameterClass] = 0.0F;
                        this.TimeSinceDeathByClass[diameterClass] = 0.0F;
                        this.NumberOfSnagsByClass[diameterClass] = 0.0F;
                    }
                }
            }

            // total carbon in the snag-container on the RU *after* processing is the content of the
            // standing woody debris pools + the branches
            this.TotalStanding = this.StandingWoodyDebrisByClass[0] + this.StandingWoodyDebrisByClass[1] + this.StandingWoodyDebrisByClass[2];
            this.TotalBranchesAndRoots = this.BranchesAndCoarseRootsByYear[0] + this.BranchesAndCoarseRootsByYear[1] + this.BranchesAndCoarseRootsByYear[2] + this.BranchesAndCoarseRootsByYear[3] + this.BranchesAndCoarseRootsByYear[4];
            this.StandingAndDebrisCarbon = this.TotalStanding.C + this.TotalBranchesAndRoots.C;

            Debug.Assert(this.StandingAndDebrisCarbon >= 0.0F);
        }

        /// foliage and fineroot litter is transferred during tree growth.
        public void AddTurnoverLitter(TreeSpecies species, float litterFoliage, float litterFineroot)
        {
            this.LabileFlux.AddBiomass(litterFoliage, species.CarbonNitrogenRatioFoliage, species.LitterDecompositionRate);
            this.LabileFluxAbovegroundCarbon += Constant.DryBiomassCarbonFraction * litterFoliage;
            this.LabileFlux.AddBiomass(litterFineroot, species.CarbonNitrogenRatioFineRoot, species.LitterDecompositionRate);
            if (species.IsEvergreen == false) // bugged in C++
            {
                this.DeciduousFoliageLitter += litterFoliage;
            }
            // Debug.WriteLineIf(Single.IsNaN(this.LabileFlux.C), "Labile carbon NaN.");
        }

        public void AddTurnoverWood(float woodyBiomass, TreeSpecies species)
        {
            // NOTE: currently, woody_biomass is *only* coarse root, therefore no split in aboveground/belowground flux here
            this.RefractoryFlux.AddBiomass(woodyBiomass, species.CarbonNitrogenRatioWood, species.CoarseWoodyDebrisDecompositionRate);
            // Debug.WriteLineIf(Single.IsNaN(this.RefractoryFlux.C), "addTurnoverWood: NaN");
        }

        /// <summary>
        /// Process the remnants of a single tree.
        /// </summary>
        /// <param name="tree">tree the tree to process</param>
        /// <param name="stemToSnag">fraction(0..1) of the stem biomass that should be moved to a standing snag</param>
        /// <param name="stemToSoil">fraction(0..1) of the stem biomass that should go directly to the soil</param>
        /// <param name="branchToSnag">fraction(0..1) of the branch biomass that should be moved to a standing snag</param>
        /// <param name="branchToSoil">fraction(0..1) of the branch biomass that should go directly to the soil</param>
        /// <param name="foliageToSoil">fraction(0..1) of the foliage biomass that should go directly to the soil</param>
        /// <remarks>
        /// The part of the stem / branch not covered by snag/soil fraction is removed from the system(e.g.harvest, fire).
        /// C++: Snag::addBiomassPools()
        /// </remarks>
        public void AddBiomassPools(TreeListSpatial tree, int treeIndex, float stemToSnag, float stemToSoil, float branchToSnag, float branchToSoil, float foliageToSoil)
        {
            TreeSpecies species = tree.Species;

            float branchBiomass = tree.GetBranchBiomass(treeIndex);
            // fine roots go to the labile pool
            this.LabileFlux.AddBiomass(tree.FineRootMassInKg[treeIndex], species.CarbonNitrogenRatioFineRoot, species.LitterDecompositionRate);

            // a part of the foliage goes to the soil
            this.LabileFlux.AddBiomass(tree.FoliageMassInKg[treeIndex] * foliageToSoil, species.CarbonNitrogenRatioFoliage, species.LitterDecompositionRate);
            this.LabileFluxAbovegroundCarbon += Constant.DryBiomassCarbonFraction * tree.FoliageMassInKg[treeIndex] * foliageToSoil;

            // aboveground fraction of the "other" pool: aboveground_current + ag_new / (total_c)
            float biomassCoarseRoot = tree.Species.GetBiomassCoarseRoot(treeIndex);
            if (this.TotalBranchesAndRoots.C + branchToSnag * branchBiomass + biomassCoarseRoot > 0.0F)
            {
                this.OtherWoodAbovegroundFraction = (this.TotalBranchesAndRoots.C * this.OtherWoodAbovegroundFraction + branchToSnag * branchBiomass) / (this.TotalBranchesAndRoots.C + branchToSnag * branchBiomass + biomassCoarseRoot);
            }

            // coarse roots and a part of branches are equally distributed over five years:
            float biomass_rest = 0.2F * (tree.CoarseRootMassInKg[treeIndex] + branchToSnag * branchBiomass);
            Debug.Assert(biomass_rest >= 0.0F);
            for (int year = 0; year < this.BranchesAndCoarseRootsByYear.Length; ++year)
            {
                // TODO: why five years?
                this.BranchesAndCoarseRootsByYear[year].AddBiomass(biomass_rest, species.CarbonNitrogenRatioWood, species.CoarseWoodyDebrisDecompositionRate);
            }

            // the other part of the branches goes directly to the soil
            this.RefractoryFlux.AddBiomass(branchBiomass * branchToSoil, species.CarbonNitrogenRatioWood, species.CoarseWoodyDebrisDecompositionRate);
            this.RefractoryFluxAbovegroundCarbon += Constant.DryBiomassCarbonFraction * branchBiomass * branchToSoil;

            // a part of the stem wood goes directly to the soil
            float biomassStemAndReserve = tree.StemMassInKg[treeIndex] + tree.NppReserveInKg[treeIndex];
            this.RefractoryFlux.AddBiomass(stemToSoil * biomassStemAndReserve, species.CarbonNitrogenRatioWood, species.CoarseWoodyDebrisDecompositionRate);
            this.RefractoryFluxAbovegroundCarbon += Constant.DryBiomassCarbonFraction * stemToSoil * biomassStemAndReserve;

            Debug.Assert(Single.IsNaN(this.RefractoryFlux.C) == false);

            // just for book-keeping: keep track of all inputs of branches / roots / swd into the "snag" pools
            this.totalSnagInput.AddBiomass(branchBiomass * branchToSnag + tree.CoarseRootMassInKg[0] + tree.StemMassInKg[0] * stemToSnag, species.CarbonNitrogenRatioWood);
            // stem biomass is transferred to the standing woody debris pool (SWD), increase stem number of pool
            int poolIndex = this.GetDiameterClassIndex(tree.DbhInCm[treeIndex]); // get right transfer pool

            if (stemToSnag > 0.0F)
            {
                // update statistics - stemnumber-weighted averages
                // note: here the calculations are repeated for every died trees (i.e. consecutive weighting ... but delivers the same results)
                float p_old = this.NumberOfSnagsByClass[poolIndex] / (this.NumberOfSnagsByClass[poolIndex] + 1); // weighting factor for state vars (based on stem numbers)
                float p_new = 1.0F / (this.NumberOfSnagsByClass[poolIndex] + 1.0F); // weighting factor for added tree (p_old + p_new = 1).
                this.AverageDbhByClass[poolIndex] = this.AverageDbhByClass[poolIndex] * p_old + tree.DbhInCm[treeIndex] * p_new;
                this.AverageHeightByClass[poolIndex] = this.AverageHeightByClass[poolIndex] * p_old + tree.HeightInM[treeIndex] * p_new;
                this.AverageVolumeByClass[poolIndex] = this.AverageVolumeByClass[poolIndex] * p_old + tree.GetStemVolume(treeIndex) * p_new;
                this.TimeSinceDeathByClass[poolIndex] = this.TimeSinceDeathByClass[poolIndex] * p_old + p_new;
                this.HalfLifeByClass[poolIndex] = this.HalfLifeByClass[poolIndex] * p_old + species.SnagHalflife * p_new;

                // average the decay rate (ksw); this is done based on the carbon content
                // aggregate all trees that die in the current year (and save weighted decay rates to CurrentKSW)
                if (biomassStemAndReserve > 0.0F)
                {
                    p_old = toStandingWoodyByClass[poolIndex].C / (toStandingWoodyByClass[poolIndex].C + Constant.DryBiomassCarbonFraction) * biomassStemAndReserve;
                    p_new = Constant.DryBiomassCarbonFraction * biomassStemAndReserve / (toStandingWoodyByClass[poolIndex].C + Constant.DryBiomassCarbonFraction * biomassStemAndReserve);
                    this.currentDecayRateByClass[poolIndex] = currentDecayRateByClass[poolIndex] * p_old + species.SnagDecompositionRate * p_new;
                }
                this.NumberOfSnagsByClass[poolIndex]++;
            }

            // finally add the biomass of the stem to the standing snag pool
            CarbonNitrogenPool toStandingWoody = toStandingWoodyByClass[poolIndex];
            toStandingWoody.AddBiomass(biomassStemAndReserve * stemToSnag, species.CarbonNitrogenRatioWood, species.CoarseWoodyDebrisDecompositionRate);
            Debug.Assert(toStandingWoody.C >= 0.0F);

            // the biomass that is not routed to snags or directly to the soil
            // is removed from the system (to atmosphere or harvested)
            this.FluxToExtern.AddBiomass(tree.FoliageMassInKg[treeIndex] * (1.0F - foliageToSoil) +
                                         branchBiomass * (1.0F - branchToSnag - branchToSoil) +
                                         biomassStemAndReserve * (1.0F - stemToSnag - stemToSoil), species.CarbonNitrogenRatioWood);
        }

        /// after the death of the tree the five biomass compartments are processed.
        public void AddMortality(TreeListSpatial trees, int treeIndex)
        {
            this.AddBiomassPools(trees, treeIndex, 1.0F, 0.0F, // all stem biomass goes to snag
                                                   1.0F, 0.0F,       // all branch biomass to snag
                                                   1.0F);            // all foliage to soil

            //    Species *species = tree.species();

            //    // immediate flows: 100% of foliage, 100% of fine roots: they go to the labile pool
            //    mLabileFlux.addBiomass(tree.biomassFoliage(), species.cnFoliage(), tree.species().snagKyl());
            //    mLabileFlux.addBiomass(tree.biomassFineRoot(), species.cnFineroot(), tree.species().snagKyl());

            //    // branches and coarse roots are equally distributed over five years:
            //    float biomass_rest = (tree.biomassBranch()+tree.biomassCoarseRoot()) * 0.2;
            //    for (int i=0;i<5; i++)
            //        mOtherWood[i].addBiomass(biomass_rest, species.cnWood(), tree.species().snagKyr());

            //    // just for book-keeping: keep track of all inputs into branches / roots / swd
            //    mTotalIn.addBiomass(tree.biomassBranch() + tree.biomassCoarseRoot() + tree.biomassStem() + tree.reserve, species.cnWood());
            //    // stem biomass is transferred to the standing woody debris pool (SWD), increase stem number of pool
            //    int pi = poolIndex(tree.dbh()); // get right transfer pool

            //    // update statistics - stemnumber-weighted averages
            //    // note: here the calculations are repeated for every died trees (i.e. consecutive weighting ... but delivers the same results)
            //    float p_old = mNumberOfSnags[pi] / (mNumberOfSnags[pi] + 1); // weighting factor for state vars (based on stem numbers)
            //    float p_new = 1.0 / (mNumberOfSnags[pi] + 1); // weighting factor for added tree (p_old + p_new = 1).
            //    mAvgDbh[pi] = mAvgDbh[pi]*p_old + tree.dbh()*p_new;
            //    mAvgHeight[pi] = mAvgHeight[pi]*p_old + tree.height()*p_new;
            //    mAvgVolume[pi] = mAvgVolume[pi]*p_old + tree.volume()*p_new;
            //    mTimeSinceDeath[pi] = mTimeSinceDeath[pi]*p_old + 1.*p_new;
            //    mHalfLife[pi] = mHalfLife[pi]*p_old + species.snagHalflife()* p_new;

            //    // average the decay rate (ksw); this is done based on the carbon content
            //    // aggregate all trees that die in the current year (and save weighted decay rates to CurrentKSW)
            //    if (tree.biomassStem + tree.reserve==0)
            //        throw new NotSupportedException("addMortality: tree without stem biomass!!");
            //    p_old = mToSWD[pi].C / (mToSWD[pi].C + tree.biomassStem()* CNPair.biomassCFraction);
            //    p_new =tree.biomassStem()* CNPair.biomassCFraction / (mToSWD[pi].C + tree.biomassStem()* CNPair.biomassCFraction);
            //    mCurrentKSW[pi] = mCurrentKSW[pi]*p_old + species.snagKsw() * p_new;
            //    mNumberOfSnags[pi]++;

            //    // finally add the biomass
            //    CNPool to_swd = mToSWD[pi];
            //    to_swd.addBiomass(tree.biomassStem(), species.cnWood(), tree.species().snagKyr());
        }

        /// add residual biomass of 'tree' after harvesting.
        /// remove_{stem, branch, foliage}_fraction: percentage of biomass compartment that is *removed* by the harvest operation [0..1] (i.e.: not to stay in the system)
        /// records on harvested biomass is collected (mTotalToExtern-pool).
        public void AddHarvest(TreeListSpatial trees, int treeIndex, float removeStemFraction, float removeBranchFraction, float removeFoliageFraction)
        {
            this.AddBiomassPools(trees, treeIndex, 0.0F, 1.0F - removeStemFraction, // "remove_stem_fraction" is removed . the rest goes to soil
                                                   0.0F, 1.0F - removeBranchFraction, // "remove_branch_fraction" is removed . the rest goes directly to the soil
                                                   1.0F - removeFoliageFraction); // the rest of foliage is routed to the soil
            //    Species *species = tree.species();

            //    // immediate flows: 100% of residual foliage, 100% of fine roots: they go to the labile pool
            //    mLabileFlux.addBiomass(tree.biomassFoliage() * (1.0 - remove_foliage_fraction), species.cnFoliage(), tree.species().snagKyl());
            //    mLabileFlux.addBiomass(tree.biomassFineRoot(), species.cnFineroot(), tree.species().snagKyl());

            //    // for branches, add all biomass that remains in the forest to the soil
            //    mRefractoryFlux.addBiomass(tree.biomassBranch()*(1.0 -remove_branch_fraction), species.cnWood(), tree.species().snagKyr());
            //    // the same treatment for stem residuals
            //    mRefractoryFlux.addBiomass(tree.biomassStem() * (1.0 - remove_stem_fraction), species.cnWood(), tree.species().snagKyr());

            //    // split the corase wood biomass into parts (slower decay)
            //    float biomass_rest = (tree.biomassCoarseRoot()) * 0.2;
            //    for (int i=0;i<5; i++)
            //        mOtherWood[i].addBiomass(biomass_rest, species.cnWood(), tree.species().snagKyr());

            //    // for book-keeping...
            //    mTotalToExtern.addBiomass(tree.biomassFoliage()*remove_foliage_fraction +
            //                              tree.biomassBranch()*remove_branch_fraction +
            //                              tree.biomassStem()*remove_stem_fraction, species.cnWood());
        }

        // add flow from regeneration layer (dead trees) to soil
        public void AddToSoil(TreeSpecies species, CarbonNitrogenTuple woodyDebris, CarbonNitrogenTuple litter, float woody_aboveground_C, float fine_aboveground_C)
        {
            this.LabileFlux.Add(litter, species.LitterDecompositionRate);
            this.LabileFluxAbovegroundCarbon += fine_aboveground_C;
            this.RefractoryFlux.Add(woodyDebris, species.CoarseWoodyDebrisDecompositionRate);
            this.RefractoryFluxAbovegroundCarbon += woody_aboveground_C;
            Debug.Assert(Single.IsNaN(this.LabileFlux.C) == false && Single.IsNaN(this.RefractoryFlux.C) == false, "NaN in C Pool");
        }

        public void AddBiomassToSoil(CarbonNitrogenPool woodyPool, CarbonNitrogenPool litterPool)
        {
            // add the biomass (fluxes in kg/ha)
            this.LabileFlux.Add(litterPool, litterPool.DecompositionRate);
            this.RefractoryFlux.Add(woodyPool, woodyPool.DecompositionRate);
            // assume all biomass input is from above
            this.LabileFluxAbovegroundCarbon += litterPool.C;
            this.RefractoryFluxAbovegroundCarbon += woodyPool.C;
        }

        /// disturbance function: remove the fraction of 'factor' of biomass from the SWD pools; 0: remove nothing, 1: remove all
        /// biomass removed by this function goes to the atmosphere
        public void RemoveCarbon(float factor)
        {
            Debug.Assert((0.0F <= factor) && (factor <= 1.0F));

            // reduce pools of currently standing dead wood and also of pools that are added
            // during (previous) management operations of the current year
            for (int diameterClass = 0; diameterClass < this.StandingWoodyDebrisByClass.Length; diameterClass++)
            {
                this.FluxToDisturbance += (this.StandingWoodyDebrisByClass[diameterClass] + toStandingWoodyByClass[diameterClass]) * factor;
                this.StandingWoodyDebrisByClass[diameterClass] *= 1.0F - factor;
                this.toStandingWoodyByClass[diameterClass] *= 1.0F - factor;
            }

            for (int year = 0; year < this.BranchesAndCoarseRootsByYear.Length; ++year)
            {
                this.FluxToDisturbance += this.BranchesAndCoarseRootsByYear[year] * factor;
                this.BranchesAndCoarseRootsByYear[year] *= 1.0F - factor;
            }
        }

        /// cut down swd (and branches) and move to soil pools
        /// @param factor 0: cut 0%, 1: cut and slash 100% of the wood
        // C++/core/snag.cpp: Snag::management()
        public void TransferStandingWoodToSoil(float fraction)
        {
            if (fraction < 0.0F || fraction > 1.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), "Invalid transfer fraction '" + fraction + "'.");
            }
            // swd pools
            for (int diameterClass = 0; diameterClass < this.StandingWoodyDebrisByClass.Length; diameterClass++)
            {
                this.standingWoodyToSoil += this.StandingWoodyDebrisByClass[diameterClass] * fraction;
                this.RefractoryFlux += this.StandingWoodyDebrisByClass[diameterClass] * fraction;
                this.RefractoryFluxAbovegroundCarbon += this.StandingWoodyDebrisByClass[diameterClass].C * fraction;
                this.StandingWoodyDebrisByClass[diameterClass] *= 1.0F - fraction;
                //mSWDtoSoil += mToSWD[i] * factor;
                //mToSWD[i] *= (1.0 - factor);
            }
            // what to do with the branches: now move also all wood to soil (note: this is note
            // very good w.r.t the coarse roots...
            for (int year = 0; year < this.BranchesAndCoarseRootsByYear.Length; ++year)
            {
                this.RefractoryFlux += this.BranchesAndCoarseRootsByYear[year] * fraction;
                this.BranchesAndCoarseRootsByYear[year] *= 1.0F - fraction;

                float ag_factor = this.OtherWoodAbovegroundFraction * fraction; // we move only aboveground carbon to the soil
                this.RefractoryFlux += this.BranchesAndCoarseRootsByYear[year] * ag_factor;
                this.RefractoryFluxAbovegroundCarbon += this.BranchesAndCoarseRootsByYear[year].C * ag_factor;
                this.BranchesAndCoarseRootsByYear[year] *= 1.0F - ag_factor;
                if (ag_factor < 1.0F)
                {
                    this.OtherWoodAbovegroundFraction = (this.OtherWoodAbovegroundFraction - ag_factor) / (1.0F - ag_factor); // the fraction of aboveground carbon
                }
            }
        }
    }
}
