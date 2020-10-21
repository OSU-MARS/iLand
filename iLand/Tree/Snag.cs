using iLand.Simulation;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    public class Snag
    {
        private double mDbhLower = -1.0;
        private double mDbhHigher = 0.0; // thresholds used to classify to SWD-Pools
        private readonly double[] mCarbonThreshold = new double[] { 0.0, 0.0, 0.0 }; // carbon content thresholds that are used to decide if the SWD-pool should be emptied

        public ResourceUnit mRU; // link to resource unit
        public CarbonNitrogenPool[] mStandingWoodyDebris; // standing woody debris pool (0: smallest dimater class, e.g. <10cm, 1: medium, 2: largest class (e.g. >30cm)) kg/ha
        public float[] mNumberOfSnags; // number of snags in diameter class
        public double[] mAvgDbh; // average diameter in class (cm)
        public double[] mAvgHeight; // average height in class (m)
        public double[] mAvgVolume; // average volume in class (m3)
        public double[] mTimeSinceDeath; // time since death: mass-weighted age of the content of the snag pool
        public float[] mKsw; // standing woody debris decay rate (weighted average of species values)
        private readonly float[] mCurrentKSW; // swd decay rate (average for trees of the current year)
        public float[] mHalfLife; // half-life values (yrs) (averaged)
        private readonly CarbonNitrogenPool[] mToStandingWoody; // transfer pool; input of the year is collected here (for each size class)
        public CarbonNitrogenPool[] mOtherWood; // pool for branch biomass and coarse root biomass
        public int mBranchCounter; // index which of the branch pools should be emptied
        private double mTotalCarbon; // sum of carbon content in all snag compartments (kg/ha)
        private readonly CarbonNitrogenTuple mTotalIn; // total input to the snag state (i.e. mortality/harvest and litter)
        private CarbonNitrogenTuple mStandingWoodyToSoil; // total flux from standing dead wood (book-keeping) -> soil (kg/ha)

        public float ClimateFactor { get; set; } // the 're' climate factor to modify decay rates (also used in ICBM/2N model)
        public CarbonNitrogenTuple FluxToAtmosphere { get; private set; } // total kg/ha heterotrophic respiration / flux to atm
        public CarbonNitrogenTuple FluxToDisturbance { get; private set; } // total kg/ha due to disturbance (e.g. fire)
        public CarbonNitrogenTuple FluxToExtern { get; private set; } // total kg/ha harvests
        public CarbonNitrogenPool LabileFlux { get; private set; } // litter flux to the soil (kg/ha)
        public CarbonNitrogenPool RefractoryFlux { get; private set; } // deadwood flux to the soil (kg/ha)
        public double TotalCarbon { get; private set; } // total carbon in snags (kg/ha)
        public CarbonNitrogenTuple TotalSwd { get; private set; } // sum of C and N in SWD pools (stems) kg/ha
        public CarbonNitrogenTuple TotalOtherWood { get; private set; } // sum of C and N in other woody pools (branches + coarse roots) kg/ha

        public Snag()
        {
            this.mAvgDbh = new double[3];
            this.mAvgHeight = new double[3];
            this.mAvgVolume = new double[3];
            this.mCurrentKSW = new float[3];
            this.mHalfLife = new float[3];
            this.mKsw = new float[3];
            this.LabileFlux = new CarbonNitrogenPool();
            this.mNumberOfSnags = new float[3];
            this.mOtherWood = new CarbonNitrogenPool[5];
            this.mRU = null;
            this.mStandingWoodyDebris = new CarbonNitrogenPool[] { new CarbonNitrogenPool(), new CarbonNitrogenPool(), new CarbonNitrogenPool() };
            this.mStandingWoodyToSoil = new CarbonNitrogenTuple();
            this.mTimeSinceDeath = new double[3];
            this.mToStandingWoody = new CarbonNitrogenPool[3] { new CarbonNitrogenPool(), new CarbonNitrogenPool(), new CarbonNitrogenPool() };
            this.mTotalIn = new CarbonNitrogenTuple();

            this.FluxToAtmosphere = new CarbonNitrogenTuple();
            this.FluxToDisturbance = new CarbonNitrogenTuple();
            this.FluxToExtern = new CarbonNitrogenTuple();
            this.RefractoryFlux = new CarbonNitrogenPool();
            this.TotalOtherWood = null;
            this.TotalSwd = null;
        }

        public bool IsEmpty()
        {
            return LabileFlux.IsEmpty() && RefractoryFlux.IsEmpty() && IsStateEmpty();
        }
        private bool IsStateEmpty() { return mTotalCarbon == 0.0; }

        /// a tree dies and the biomass of the tree is split between snags/soils/removals
        /// @param tree the tree to process
        /// @param stem_to_snag fraction (0..1) of the stem biomass that should be moved to a standing snag
        /// @param stem_to_soil fraction (0..1) of the stem biomass that should go directly to the soil
        /// @param branch_to_snag fraction (0..1) of the branch biomass that should be moved to a standing snag
        /// @param branch_to_soil fraction (0..1) of the branch biomass that should go directly to the soil
        /// @param foliage_to_soil fraction (0..1) of the foliage biomass that should go directly to the soil
        public void AddDisturbance(Trees tree, int treeIndex, float stemToSnag, float stemToSoil, float branchToSnag, float branchToSoil, float foliageToSoil) 
        {
            AddBiomassPools(tree, treeIndex, stemToSnag, stemToSoil, branchToSnag, branchToSoil, foliageToSoil);
        }

        private int PoolIndex(float dbh)
        {
            if (dbh < mDbhLower)
            {
                return 0;
            }
            if (dbh > mDbhHigher)
            {
                return 2;
            }
            return 1;
        }

        public void SetupThresholds(double lower, double upper)
        {
            if ((lower < 0.0) || (lower >= upper))
            {
                throw new ArgumentOutOfRangeException();
            }
            if ((mDbhLower == lower) && (mDbhHigher == upper))
            {
                return;
            }
            mDbhLower = lower;
            mDbhHigher = upper;
            mCarbonThreshold[0] = lower / 2.0;
            mCarbonThreshold[1] = lower + (upper - lower) / 2.0;
            mCarbonThreshold[2] = upper + (upper - lower) / 2.0;
            //# threshold levels for emptying out the dbh-snag-classes
            //# derived from Psme woody allometry, converted to C, with a threshold level set to 10%
            //# values in kg!
            for (int i = 0; i < 3; i++)
            {
                mCarbonThreshold[i] = 0.10568 * Math.Pow(mCarbonThreshold[i], 2.4247) * 0.5 * 0.1;
            }
        }

        public void Setup(Model model, ResourceUnit ru)
        {
            mRU = ru;
            ClimateFactor = 0.0F;
            // branches
            mBranchCounter = 0;
            for (int i = 0; i < 3; i++)
            {
                mTimeSinceDeath[i] = 0.0;
                mNumberOfSnags[i] = 0.0F;
                mAvgDbh[i] = 0.0;
                mAvgHeight[i] = 0.0;
                mAvgVolume[i] = 0.0;
                mKsw[i] = 0.0F;
                mCurrentKSW[i] = 0.0F;
                mHalfLife[i] = 0.0F;
            }
            mTotalCarbon = 0.0;
            if (mDbhLower <= 0.0)
            {
                throw new NotSupportedException("SetupThresholds() not called or called with invalid parameters.");
            }

            // Inital values from XML file
            float kyr = model.Project.Model.Site.YoungRefractoryDecompositionRate; // TODO: why does the standing woody pool decay at the soil rate rather than the snag decomposition rate?
            // put carbon of snags to the middle size class
            mStandingWoodyDebris[1].C = model.Project.Model.Initialization.Snags.StandingWoodyDebrisCarbon;
            mStandingWoodyDebris[1].N = mStandingWoodyDebris[1].C / model.Project.Model.Initialization.Snags.StandingWoodyDebrisCarbonNitrogenRatio;
            mStandingWoodyDebris[1].DecompositionRate = kyr;
            mKsw[1] = model.Project.Model.Initialization.Snags.StandingWoodyDebrisDecompositionRate;
            mNumberOfSnags[1] = model.Project.Model.Initialization.Snags.SnagsPerResourceUnit;
            mHalfLife[1] = model.Project.Model.Initialization.Snags.StandingWoodyDebrisHalfLife;
            // and for the Branch/coarse root pools: split the init value into five chunks
            CarbonNitrogenPool other = new CarbonNitrogenPool(model.Project.Model.Initialization.Snags.OtherCarbon,
                                                              model.Project.Model.Initialization.Snags.OtherCarbon / model.Project.Model.Initialization.Snags.OtherCarbonNitrogenRatio,
                                                              kyr);

            mTotalCarbon = other.C + mStandingWoodyDebris[1].C;

            other *= 0.2F;
            for (int i = 0; i < 5; i++)
            {
                mOtherWood[i] = other;
            }
        }

        public void ScaleInitialState()
        {
            float area_factor = mRU.StockableArea / Constant.RUArea; // fraction stockable area
            // avoid huge snag pools on very small resource units (see also soil.cpp)
            // area_factor = std::max(area_factor, 0.1);
            mStandingWoodyDebris[1] *= area_factor;
            mNumberOfSnags[1] *= area_factor;
            for (int i = 0; i < 5; i++)
            {
                mOtherWood[i] *= area_factor;
            }
            mTotalCarbon *= area_factor;
        }

        // debug outputs
        //public List<object> DebugList()
        //{
        //    // list columns
        //    // for three pools
        //    List<object> list = new List<object>()
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

        public void NewYear()
        {
            for (int i = 0; i < 3; i++)
            {
                mToStandingWoody[i].Clear(); // clear transfer pools to standing-woody-debris
                mCurrentKSW[i] = 0.0F;
            }

            mStandingWoodyToSoil.Clear();
            mTotalIn.Clear();

            FluxToAtmosphere.Clear();
            FluxToExtern.Clear();
            FluxToDisturbance.Clear();
            LabileFlux.Clear();
            RefractoryFlux.Clear();
        }

        /// calculate the dynamic climate modifier for decomposition 're'
        /// calculation is done on the level of ResourceUnit because the water content per day is needed.
        public void CalculateClimateFactors(Model model)
        {
            // the calculation of climate factors requires calculated evapotranspiration. In cases without vegetation (trees or saplings)
            // we have to trigger the water cycle calculation for ourselves [ the waterCycle checks if it has already been run in a year and doesn't run twice in that case ]
            mRU.WaterCycle.Run(model);
            double ft, fw;
            double f_sum = 0.0;
            int iday = 0;
            // calculate the water-factor for each month (see Adair et al 2008)
            double[] fw_month = new double[12];
            double ratio;
            for (int month = 0; month < 12; ++month)
            {
                if (mRU.WaterCycle.ReferenceEvapotranspiration()[month] > 0.0)
                {
                    ratio = mRU.Climate.PrecipitationByMonth[month] / mRU.WaterCycle.ReferenceEvapotranspiration()[month];
                }
                else
                {
                    ratio = 0.0;
                }
                fw_month[month] = 1.0 / (1.0 + 30.0 * Math.Exp(-8.5 * ratio));
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("month " + month + " PET " + mRU.WaterCycle.ReferenceEvapotranspiration()[month] + " prec " + mRU.Climate.PrecipitationByMonth[month]);
                }
            }

            for (int index = mRU.Climate.CurrentJanuary1; index != mRU.Climate.NextJanuary1; ++index, ++iday)
            {
                ClimateDay day = mRU.Climate[index];
                ft = Math.Exp(308.56 * (1.0 / 56.02 - 1.0 / ((273.15 + day.MeanDaytimeTemperature) - 227.13)));  // empirical variable Q10 model of Lloyd and Taylor (1994), see also Adair et al. (2008)
                fw = fw_month[day.Month - 1];

                f_sum += ft * fw;
            }
            // the climate factor is defined as the arithmentic annual mean value
            this.ClimateFactor = (float)f_sum / mRU.Climate.DaysOfYear();
        }

        /// do the yearly calculation
        /// see http://iland.boku.ac.at/snag+dynamics
        public void CalculateYear(Model model)
        {
            mStandingWoodyToSoil.Clear();

            // calculate anyway, because also the soil module needs it (and currently one can have Snag and Soil only as a couple)
            CalculateClimateFactors(model);
            float climate_factor_re = this.ClimateFactor;
            if (IsEmpty()) // nothing to do
            {
                return;
            }
            // process branches: every year one of the five baskets is emptied and transfered to the refractory soil pool
            RefractoryFlux += mOtherWood[mBranchCounter];
            mOtherWood[mBranchCounter].Clear();
            mBranchCounter = (mBranchCounter + 1) % 5; // increase index, roll over to 0.

            // decay of branches/coarse roots
            for (int year = 0; year < 5; ++year)
            {
                if (mOtherWood[year].C > 0.0F)
                {
                    float survive_rate = MathF.Exp(-climate_factor_re * mOtherWood[year].DecompositionRate); // parameter: the "kyr" value...
                    this.FluxToAtmosphere.C += mOtherWood[year].C * (1.0F - survive_rate); // flux to atmosphere (decayed carbon)
                    mOtherWood[year].C *= survive_rate;
                }
            }

            // process standing snags.
            // the input of the current year is in the mToSWD-Pools
            for (int i = 0; i < 3; i++)
            {
                // update the swd-pool with this years' input
                if (!mToStandingWoody[i].IsEmpty())
                {
                    // update decay rate (apply average yearly input to the state parameters)
                    mKsw[i] = mKsw[i] * (mStandingWoodyDebris[i].C / (mStandingWoodyDebris[i].C + mToStandingWoody[i].C)) + mCurrentKSW[i] * (mToStandingWoody[i].C / (mStandingWoodyDebris[i].C + mToStandingWoody[i].C));
                    //move content to the SWD pool
                    mStandingWoodyDebris[i] += mToStandingWoody[i];
                }

                if (mStandingWoodyDebris[i].C > 0.0F)
                {
                    // reduce the Carbon (note: the N stays, thus the CN ratio changes)
                    // use the decay rate that is derived as a weighted average of all standing woody debris
                    float survive_rate = MathF.Exp(-mKsw[i] * climate_factor_re); // 1: timestep
                    this.FluxToAtmosphere.C += mStandingWoodyDebris[i].C * (1.0F - survive_rate);
                    mStandingWoodyDebris[i].C *= survive_rate;

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

                    float half_life = mHalfLife[i] / climate_factor_re;
                    float rate = -Constant.Ln2 / half_life; // M_LN2: math. constant

                    // higher decay rate for the class with smallest diameters
                    if (i == 0)
                    {
                        rate *= 2.0F;
                    }
                    float transfer = 1.0F - MathF.Exp(rate);

                    // calculate flow to soil pool...
                    mStandingWoodyToSoil += mStandingWoodyDebris[i] * transfer;
                    RefractoryFlux += mStandingWoodyDebris[i] * transfer;
                    mStandingWoodyDebris[i] *= (1.0F - transfer); // reduce pool
                                                 // calculate the stem number of remaining snags
                    mNumberOfSnags[i] = mNumberOfSnags[i] * (1.0F - transfer);

                    mTimeSinceDeath[i] += 1.0;
                    // if stems<0.5, empty the whole cohort into DWD, i.e. release the last bit of C and N and clear the stats
                    // also, if the Carbon of an average snag is less than 10% of the original average tree
                    // (derived from allometries for the three diameter classes), the whole cohort is emptied out to DWD
                    if (mNumberOfSnags[i] < 0.5 || mStandingWoodyDebris[i].C / mNumberOfSnags[i] < mCarbonThreshold[i])
                    {
                        // clear the pool: add the rest to the soil, clear statistics of the pool
                        RefractoryFlux += mStandingWoodyDebris[i];
                        mStandingWoodyToSoil += mStandingWoodyDebris[i];
                        mStandingWoodyDebris[i].Clear();
                        mAvgDbh[i] = 0.0;
                        mAvgHeight[i] = 0.0;
                        mAvgVolume[i] = 0.0;
                        mKsw[i] = 0.0F;
                        mCurrentKSW[i] = 0.0F;
                        mHalfLife[i] = 0.0F;
                        mTimeSinceDeath[i] = 0.0;
                    }
                }
            }
            // total carbon in the snag-container on the RU *after* processing is the content of the
            // standing woody debris pools + the branches
            mTotalCarbon = mStandingWoodyDebris[0].C + mStandingWoodyDebris[1].C + mStandingWoodyDebris[2].C + mOtherWood[0].C + mOtherWood[1].C + mOtherWood[2].C + mOtherWood[3].C + mOtherWood[4].C;
            this.TotalSwd = mStandingWoodyDebris[0] + mStandingWoodyDebris[1] + mStandingWoodyDebris[2];
            this.TotalOtherWood = mOtherWood[0] + mOtherWood[1] + mOtherWood[2] + mOtherWood[3] + mOtherWood[4];
        }

        /// foliage and fineroot litter is transferred during tree growth.
        public void AddTurnoverLitter(Species species, float litter_foliage, float litter_fineroot)
        {
            LabileFlux.AddBiomass(litter_foliage, species.CNRatioFoliage, species.SnagKyl);
            LabileFlux.AddBiomass(litter_fineroot, species.CNRatioFineRoot, species.SnagKyl);
            Debug.WriteLineIf(Double.IsNaN(LabileFlux.C), "addTurnoverLitter: NaN");
        }

        public void AddTurnoverWood(Species species, float woody_biomass)
        {
            RefractoryFlux.AddBiomass(woody_biomass, species.CNRatioWood, species.SnagKyr);
            Debug.WriteLineIf(Double.IsNaN(RefractoryFlux.C), "addTurnoverWood: NaN");
        }

        /** process the remnants of a single tree.
            The part of the stem / branch not covered by snag/soil fraction is removed from the system (e.g. harvest, fire)
            @param tree the tree to process
            @param stem_to_snag fraction (0..1) of the stem biomass that should be moved to a standing snag
            @param stem_to_soil fraction (0..1) of the stem biomass that should go directly to the soil
            @param branch_to_snag fraction (0..1) of the branch biomass that should be moved to a standing snag
            @param branch_to_soil fraction (0..1) of the branch biomass that should go directly to the soil
            @param foliage_to_soil fraction (0..1) of the foliage biomass that should go directly to the soil
            */
        public void AddBiomassPools(Trees tree, int treeIndex, float stemToSnag, float stemToSoil, float branchToSnag, float branchToSoil, float foliageToSoil)
        {
            Species species = tree.Species;

            float branchBiomass = tree.GetBranchBiomass(treeIndex);
            // fine roots go to the labile pool
            LabileFlux.AddBiomass(tree.FineRootMass[treeIndex], species.CNRatioFineRoot, species.SnagKyl);

            // a part of the foliage goes to the soil
            LabileFlux.AddBiomass(tree.FoliageMass[treeIndex] * foliageToSoil, species.CNRatioFoliage, species.SnagKyl);

            // coarse roots and a part of branches are equally distributed over five years:
            float biomass_rest = 0.2F * (tree.CoarseRootMass[treeIndex] + branchToSnag * branchBiomass);
            for (int year = 0; year < 5; ++year)
            {
                // TODO: why five years?
                mOtherWood[year].AddBiomass(biomass_rest, species.CNRatioWood, species.SnagKyr);
            }

            // the other part of the branches goes directly to the soil
            RefractoryFlux.AddBiomass(branchBiomass * branchToSoil, species.CNRatioWood, species.SnagKyr);
            // a part of the stem wood goes directly to the soil
            RefractoryFlux.AddBiomass(tree.StemMass[treeIndex] * stemToSoil, species.CNRatioWood, species.SnagKyr);

            // just for book-keeping: keep track of all inputs of branches / roots / swd into the "snag" pools
            mTotalIn.AddBiomass(branchBiomass * branchToSnag + tree.CoarseRootMass[0] + tree.StemMass[0] * stemToSnag, species.CNRatioWood);
            // stem biomass is transferred to the standing woody debris pool (SWD), increase stem number of pool
            int pi = PoolIndex(tree.Dbh[treeIndex]); // get right transfer pool

            if (stemToSnag > 0.0)
            {
                // update statistics - stemnumber-weighted averages
                // note: here the calculations are repeated for every died trees (i.e. consecutive weighting ... but delivers the same results)
                float p_old = mNumberOfSnags[pi] / (mNumberOfSnags[pi] + 1); // weighting factor for state vars (based on stem numbers)
                float p_new = 1.0F / (mNumberOfSnags[pi] + 1.0F); // weighting factor for added tree (p_old + p_new = 1).
                mAvgDbh[pi] = mAvgDbh[pi] * p_old + tree.Dbh[treeIndex] * p_new;
                mAvgHeight[pi] = mAvgHeight[pi] * p_old + tree.Height[treeIndex] * p_new;
                mAvgVolume[pi] = mAvgVolume[pi] * p_old + tree.GetStemVolume(treeIndex) * p_new;
                mTimeSinceDeath[pi] = mTimeSinceDeath[pi] * p_old + p_new;
                mHalfLife[pi] = mHalfLife[pi] * p_old + species.SnagHalflife * p_new;

                // average the decay rate (ksw); this is done based on the carbon content
                // aggregate all trees that die in the current year (and save weighted decay rates to CurrentKSW)
                p_old = mToStandingWoody[pi].C / (mToStandingWoody[pi].C + tree.StemMass[treeIndex] * Constant.BiomassCFraction);
                p_new = tree.StemMass[treeIndex] * Constant.BiomassCFraction / (mToStandingWoody[pi].C + tree.StemMass[treeIndex] * Constant.BiomassCFraction);
                mCurrentKSW[pi] = mCurrentKSW[pi] * p_old + species.SnagKsw * p_new;
                mNumberOfSnags[pi]++;
            }

            // finally add the biomass of the stem to the standing snag pool
            CarbonNitrogenPool to_swd = mToStandingWoody[pi];
            to_swd.AddBiomass(tree.StemMass[treeIndex] * stemToSnag, species.CNRatioWood, species.SnagKyr);

            // the biomass that is not routed to snags or directly to the soil
            // is removed from the system (to atmosphere or harvested)
            FluxToExtern.AddBiomass(tree.FoliageMass[treeIndex] * (1.0F - foliageToSoil) +
                                    branchBiomass * (1.0F - branchToSnag - branchToSoil) +
                                    tree.StemMass[treeIndex] * (1.0F - stemToSnag - stemToSoil), species.CNRatioWood);

        }

        /// after the death of the tree the five biomass compartments are processed.
        public void AddMortality(Trees trees, int treeIndex)
        {
            this.AddBiomassPools(trees, treeIndex, 1.0F, 0.0F, // all stem biomass goes to snag
                                                   1.0F, 0.0F,       // all branch biomass to snag
                                                   1.0F);            // all foliage to soil

            //    Species *species = tree.species();

            //    // immediate flows: 100% of foliage, 100% of fine roots: they go to the labile pool
            //    mLabileFlux.addBiomass(tree.biomassFoliage(), species.cnFoliage(), tree.species().snagKyl());
            //    mLabileFlux.addBiomass(tree.biomassFineRoot(), species.cnFineroot(), tree.species().snagKyl());

            //    // branches and coarse roots are equally distributed over five years:
            //    double biomass_rest = (tree.biomassBranch()+tree.biomassCoarseRoot()) * 0.2;
            //    for (int i=0;i<5; i++)
            //        mOtherWood[i].addBiomass(biomass_rest, species.cnWood(), tree.species().snagKyr());

            //    // just for book-keeping: keep track of all inputs into branches / roots / swd
            //    mTotalIn.addBiomass(tree.biomassBranch() + tree.biomassCoarseRoot() + tree.biomassStem(), species.cnWood());
            //    // stem biomass is transferred to the standing woody debris pool (SWD), increase stem number of pool
            //    int pi = poolIndex(tree.dbh()); // get right transfer pool

            //    // update statistics - stemnumber-weighted averages
            //    // note: here the calculations are repeated for every died trees (i.e. consecutive weighting ... but delivers the same results)
            //    double p_old = mNumberOfSnags[pi] / (mNumberOfSnags[pi] + 1); // weighting factor for state vars (based on stem numbers)
            //    double p_new = 1.0 / (mNumberOfSnags[pi] + 1); // weighting factor for added tree (p_old + p_new = 1).
            //    mAvgDbh[pi] = mAvgDbh[pi]*p_old + tree.dbh()*p_new;
            //    mAvgHeight[pi] = mAvgHeight[pi]*p_old + tree.height()*p_new;
            //    mAvgVolume[pi] = mAvgVolume[pi]*p_old + tree.volume()*p_new;
            //    mTimeSinceDeath[pi] = mTimeSinceDeath[pi]*p_old + 1.*p_new;
            //    mHalfLife[pi] = mHalfLife[pi]*p_old + species.snagHalflife()* p_new;

            //    // average the decay rate (ksw); this is done based on the carbon content
            //    // aggregate all trees that die in the current year (and save weighted decay rates to CurrentKSW)
            //    if (tree.biomassStem()==0)
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
        public void AddHarvest(Trees trees, int treeIndex, float removeStemFraction, float removeBranchFraction, float removeFoliageFraction)
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
            //    double biomass_rest = (tree.biomassCoarseRoot()) * 0.2;
            //    for (int i=0;i<5; i++)
            //        mOtherWood[i].addBiomass(biomass_rest, species.cnWood(), tree.species().snagKyr());

            //    // for book-keeping...
            //    mTotalToExtern.addBiomass(tree.biomassFoliage()*remove_foliage_fraction +
            //                              tree.biomassBranch()*remove_branch_fraction +
            //                              tree.biomassStem()*remove_stem_fraction, species.cnWood());
        }

        // add flow from regeneration layer (dead trees) to soil
        public void AddToSoil(Species species, CarbonNitrogenTuple woodyDebris, CarbonNitrogenTuple litter)
        {
            this.LabileFlux.Add(litter, species.SnagKyl);
            this.RefractoryFlux.Add(woodyDebris, species.SnagKyr);
            Debug.WriteLineIf(Double.IsNaN(LabileFlux.C) || Double.IsNaN(RefractoryFlux.C), "addToSoil: NaN in C Pool");
        }

        /// disturbance function: remove the fraction of 'factor' of biomass from the SWD pools; 0: remove nothing, 1: remove all
        /// biomass removed by this function goes to the atmosphere
        public void RemoveCarbon(float factor)
        {
            // reduce pools of currently standing dead wood and also of pools that are added
            // during (previous) management operations of the current year
            for (int i = 0; i < 3; i++)
            {
                FluxToDisturbance += (mStandingWoodyDebris[i] + mToStandingWoody[i]) * factor;
                mStandingWoodyDebris[i] *= 1.0F - factor;
                mToStandingWoody[i] *= 1.0F - factor;
            }

            for (int i = 0; i < 5; i++)
            {
                FluxToDisturbance += mOtherWood[i] * factor;
                mOtherWood[i] *= 1.0F - factor;
            }
        }

        /// cut down swd (and branches) and move to soil pools
        /// @param factor 0: cut 0%, 1: cut and slash 100% of the wood
        public void Management(float factor)
        {
            if (factor < 0.0F || factor > 1.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(factor), "Invalid factor '" + factor + "'.");
            }
            // swd pools
            for (int i = 0; i < 3; i++)
            {
                mStandingWoodyToSoil += mStandingWoodyDebris[i] * factor;
                RefractoryFlux += mStandingWoodyDebris[i] * factor;
                mStandingWoodyDebris[i] *= 1.0F - factor;
                //mSWDtoSoil += mToSWD[i] * factor;
                //mToSWD[i] *= (1.0 - factor);
            }
            // what to do with the branches: now move also all wood to soil (note: this is note
            // very good w.r.t the coarse roots...
            for (int i = 0; i < 5; i++)
            {
                RefractoryFlux += mOtherWood[i] * factor;
                mOtherWood[i] *= 1.0F - factor;
            }
        }
    }
}
