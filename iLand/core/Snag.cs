using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    internal class Snag
    {
        private static double mDBHLower = -1.0;
        private static double mDBHHigher = 0.0; ///< thresholds used to classify to SWD-Pools
        private static double[] mCarbonThreshold = new double[] { 0.0, 0.0, 0.0 }; ///< carbon content thresholds that are used to decide if the SWD-pool should be emptied

        public double mClimateFactor; ///< current mean climate factor (influenced by temperature and soil water content)
        public ResourceUnit mRU; ///< link to resource unit
                                 /// access SWDPool as function of diameter (cm)
        public CNPool[] mSWD; ///< standing woody debris pool (0: smallest dimater class, e.g. <10cm, 1: medium, 2: largest class (e.g. >30cm)) kg/ha
        public CNPair mTotalSWD; ///< sum of mSWD[x]
        public double[] mNumberOfSnags; ///< number of snags in diameter class
        public double[] mAvgDbh; ///< average diameter in class (cm)
        public double[] mAvgHeight; ///< average height in class (m)
        public double[] mAvgVolume; ///< average volume in class (m3)
        public double[] mTimeSinceDeath; ///< time since death: mass-weighted age of the content of the snag pool
        public double[] mKSW; ///< standing woody debris decay rate (weighted average of species values)
        private double[] mCurrentKSW; ///< swd decay rate (average for trees of the current year)
        public double[] mHalfLife; ///< half-life values (yrs) (averaged)
        private CNPool[] mToSWD; ///< transfer pool; input of the year is collected here (for each size class)
        private CNPool mLabileFlux; ///< flux to labile soil pools (kg/ha)
        private CNPool mRefractoryFlux; ///< flux to the refractory soil pool (kg/ha)
        public CNPool[] mOtherWood; ///< pool for branch biomass and coarse root biomass
        private CNPair mTotalOther; ///< sum of mOtherWood[x]
        public int mBranchCounter; ///< index which of the branch pools should be emptied
        private double mTotalSnagCarbon; ///< sum of carbon content in all snag compartments (kg/ha)
        private CNPair mTotalIn; ///< total input to the snag state (i.e. mortality/harvest and litter)
        private CNPair mSWDtoSoil; ///< total flux from standing dead wood (book-keeping) -> soil (kg/ha)
        private CNPair mTotalToAtm; ///< flux to atmosphere (kg/ha)
        private CNPair mTotalToExtern; ///< total flux of masses removed from the site (i.e. harvesting) kg/ha
        private CNPair mTotalToDisturbance; ///< fluxes due to disturbance

        private int poolIndex(float dbh)
        {
            if (dbh < mDBHLower)
            {
                return 0;
            }
            if (dbh > mDBHHigher)
            {
                return 2;
            }
            return 1;
        }
        bool isStateEmpty() { return mTotalSnagCarbon == 0.0; }
        public bool isEmpty()
        {
            return mLabileFlux.isEmpty() && mRefractoryFlux.isEmpty() && isStateEmpty();
        }
        public CNPool labileFlux() { return mLabileFlux; } ///< litter flux to the soil (kg/ha)
        public CNPool refractoryFlux() { return mRefractoryFlux; } ///< deadwood flux to the soil (kg/ha)
        public double climateFactor() { return mClimateFactor; } ///< the 're' climate factor to modify decay rates (also used in ICBM/2N model)
        public double totalCarbon() { return mTotalSnagCarbon; } ///< total carbon in snags (kg/ha)
        public CNPair totalSWD() { return mTotalSWD; } ///< sum of C and N in SWD pools (stems) kg/ha
        public CNPair totalOtherWood() { return mTotalOther; } ///< sum of C and N in other woody pools (branches + coarse roots) kg/ha
        public CNPair fluxToAtmosphere() { return mTotalToAtm; } ///< total kg/ha heterotrophic respiration / flux to atm
        public CNPair fluxToExtern() { return mTotalToExtern; } ///< total kg/ha harvests
        public CNPair fluxToDisturbance() { return mTotalToDisturbance; } ///< total kg/ha due to disturbance (e.g. fire)

        public Snag()
        {
            mRU = null;
            mSWD = new CNPool[3];
            mNumberOfSnags = new double[3];
            mAvgDbh = new double[3];
            mAvgHeight = new double[3];
            mAvgVolume = new double[3];
            mTimeSinceDeath = new double[3];
            mKSW = new double[3];
            mCurrentKSW = new double[3];
            mHalfLife = new double[3];
            mToSWD = new CNPool[3];
            mOtherWood = new CNPool[5];

            CNPair.setCFraction(CNPair.biomassCFraction);
        }

        /// a tree dies and the biomass of the tree is split between snags/soils/removals
        /// @param tree the tree to process
        /// @param stem_to_snag fraction (0..1) of the stem biomass that should be moved to a standing snag
        /// @param stem_to_soil fraction (0..1) of the stem biomass that should go directly to the soil
        /// @param branch_to_snag fraction (0..1) of the branch biomass that should be moved to a standing snag
        /// @param branch_to_soil fraction (0..1) of the branch biomass that should go directly to the soil
        /// @param foliage_to_soil fraction (0..1) of the foliage biomass that should go directly to the soil
        public void addDisturbance(Tree tree, double stem_to_snag, double stem_to_soil, double branch_to_snag, double branch_to_soil, double foliage_to_soil) 
        {
            addBiomassPools(tree, stem_to_snag, stem_to_soil, branch_to_snag, branch_to_soil, foliage_to_soil);
        }

        public static void setupThresholds(double lower, double upper)
        {
            if (mDBHLower == lower)
            {
                return;
            }
            mDBHLower = lower;
            mDBHHigher = upper;
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

        public void setup(ResourceUnit ru)
        {
            mRU = ru;
            mClimateFactor = 0.0;
            // branches
            mBranchCounter = 0;
            for (int i = 0; i < 3; i++)
            {
                mTimeSinceDeath[i] = 0.0;
                mNumberOfSnags[i] = 0.0;
                mAvgDbh[i] = 0.0;
                mAvgHeight[i] = 0.0;
                mAvgVolume[i] = 0.0;
                mKSW[i] = 0.0;
                mCurrentKSW[i] = 0.0;
                mHalfLife[i] = 0.0;
            }
            mTotalSnagCarbon = 0.0;
            if (mDBHLower <= 0)
            {
                throw new NotSupportedException("setupThresholds() not called or called with invalid parameters.");
            }

            // Inital values from XML file
            XmlHelper xml = GlobalSettings.instance().settings();
            double kyr = xml.valueDouble("model.site.youngRefractoryDecompRate", -1);
            // put carbon of snags to the middle size class
            xml.setCurrentNode("model.initialization.snags");
            mSWD[1].C = xml.valueDouble(".swdC");
            mSWD[1].N = mSWD[1].C / xml.valueDouble(".swdCN", 50.0);
            mSWD[1].setParameter(kyr);
            mKSW[1] = xml.valueDouble(".swdDecompRate");
            mNumberOfSnags[1] = xml.valueDouble(".swdCount");
            mHalfLife[1] = xml.valueDouble(".swdHalfLife");
            // and for the Branch/coarse root pools: split the init value into five chunks
            CNPool other = new CNPool(xml.valueDouble(".otherC"), xml.valueDouble(".otherC") / xml.valueDouble(".otherCN", 50.0), kyr);

            mTotalSnagCarbon = other.C + mSWD[1].C;

            other *= 0.2;
            for (int i = 0; i < 5; i++)
            {
                mOtherWood[i] = other;
            }
        }

        public void scaleInitialState()
        {
            double area_factor = mRU.stockableArea() / Constant.cRUArea; // fraction stockable area
                                                                         // avoid huge snag pools on very small resource units (see also soil.cpp)
                                                                         // area_factor = std::max(area_factor, 0.1);
            mSWD[1] *= area_factor;
            mNumberOfSnags[1] *= area_factor;
            for (int i = 0; i < 5; i++)
            {
                mOtherWood[i] *= area_factor;
            }
            mTotalSnagCarbon *= area_factor;
        }

        // debug outputs
        public List<object> debugList()
        {
            // list columns
            // for three pools
            List<object> list = new List<object>()
            {
            // totals
            mTotalSnagCarbon, mTotalIn.C, mTotalToAtm.C, mSWDtoSoil.C, mSWDtoSoil.N,
            // fluxes to labile soil pool and to refractory soil pool
            mLabileFlux.C, mLabileFlux.N, mRefractoryFlux.C, mRefractoryFlux.N
            };
            for (int i = 0; i < 3; i++)
            {
                // pools "swdx_c", "swdx_n", "swdx_count", "swdx_tsd", "toswdx_c", "toswdx_n"
                list.AddRange(new object[] { mSWD[i].C, mSWD[i].N, mNumberOfSnags[i], mTimeSinceDeath[i], mToSWD[i].C, mToSWD[i].N,
                                             mAvgDbh[i], mAvgHeight[i], mAvgVolume[i] });
            }

            // branch/coarse wood pools (5 yrs)
            for (int i = 0; i < 5; i++)
            {
                list.Add(mOtherWood[i].C);
                list.Add(mOtherWood[i].N);
            }
            //    list.AddRange(new object[] { mOtherWood[mBranchCounter].C, mOtherWood[mBranchCounter].N,
            //           , mOtherWood[(mBranchCounter+1)%5].C, mOtherWood[(mBranchCounter+1)%5].N,
            //           , mOtherWood[(mBranchCounter+2)%5].C, mOtherWood[(mBranchCounter+2)%5].N,
            //           , mOtherWood[(mBranchCounter+3)%5].C, mOtherWood[(mBranchCounter+3)%5].N,
            //           , mOtherWood[(mBranchCounter+4)%5].C, mOtherWood[(mBranchCounter+4)%5].N });
            return list;
        }

        public void newYear()
        {
            for (int i = 0; i < 3; i++)
            {
                mToSWD[i].clear(); // clear transfer pools to standing-woody-debris
                mCurrentKSW[i] = 0.0;
            }
            mLabileFlux.clear();
            mRefractoryFlux.clear();
            mTotalToAtm.clear();
            mTotalToExtern.clear();
            mTotalToDisturbance.clear();
            mTotalIn.clear();
            mSWDtoSoil.clear();
        }

        /// calculate the dynamic climate modifier for decomposition 're'
        /// calculation is done on the level of ResourceUnit because the water content per day is needed.
        public double calculateClimateFactors()
        {
            // the calculation of climate factors requires calculated evapotranspiration. In cases without vegetation (trees or saplings)
            // we have to trigger the water cycle calculation for ourselves [ the waterCycle checks if it has already been run in a year and doesn't run twice in that case ]
            mRU.waterCycle().run();
            double ft, fw;
            double f_sum = 0.0;
            int iday = 0;
            // calculate the water-factor for each month (see Adair et al 2008)
            double[] fw_month = new double[12];
            double ratio;
            for (int m = 0; m < 12; m++)
            {
                if (mRU.waterCycle().referenceEvapotranspiration()[m] > 0.0)
                {
                    ratio = mRU.climate().precipitationMonth()[m] / mRU.waterCycle().referenceEvapotranspiration()[m];
                }
                else
                {
                    ratio = 0.0;
                }
                fw_month[m] = 1.0 / (1.0 + 30.0 * Math.Exp(-8.5 * ratio));
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("month " + m + " PET " + mRU.waterCycle().referenceEvapotranspiration()[m] + " prec " + mRU.climate().precipitationMonth()[m]);
                }
            }

            for (int index = mRU.climate().begin(); index != mRU.climate().end(); ++index, ++iday)
            {
                ClimateDay day = mRU.climate()[index];
                ft = Math.Exp(308.56 * (1.0 / 56.02 - 1.0 / ((273.15 + day.temperature) - 227.13)));  // empirical variable Q10 model of Lloyd and Taylor (1994), see also Adair et al. (2008)
                fw = fw_month[day.month - 1];

                f_sum += ft * fw;
            }
            // the climate factor is defined as the arithmentic annual mean value
            mClimateFactor = f_sum / (double)mRU.climate().daysOfYear();
            return mClimateFactor;
        }

        /// do the yearly calculation
        /// see http://iland.boku.ac.at/snag+dynamics
        public void calculateYear()
        {
            mSWDtoSoil.clear();

            // calculate anyway, because also the soil module needs it (and currently one can have Snag and Soil only as a couple)
            calculateClimateFactors();
            double climate_factor_re = mClimateFactor;
            if (isEmpty()) // nothing to do
            {
                return;
            }
            // process branches: every year one of the five baskets is emptied and transfered to the refractory soil pool
            mRefractoryFlux += mOtherWood[mBranchCounter];
            mOtherWood[mBranchCounter].clear();
            mBranchCounter = (mBranchCounter + 1) % 5; // increase index, roll over to 0.

            // decay of branches/coarse roots
            for (int i = 0; i < 5; i++)
            {
                if (mOtherWood[i].C > 0.0)
                {
                    double survive_rate = Math.Exp(-climate_factor_re * mOtherWood[i].parameter()); // parameter: the "kyr" value...
                    mTotalToAtm.C += mOtherWood[i].C * (1.0 - survive_rate); // flux to atmosphere (decayed carbon)
                    mOtherWood[i].C *= survive_rate;
                }
            }

            // process standing snags.
            // the input of the current year is in the mToSWD-Pools
            for (int i = 0; i < 3; i++)
            {
                // update the swd-pool with this years' input
                if (!mToSWD[i].isEmpty())
                {
                    // update decay rate (apply average yearly input to the state parameters)
                    mKSW[i] = mKSW[i] * (mSWD[i].C / (mSWD[i].C + mToSWD[i].C)) + mCurrentKSW[i] * (mToSWD[i].C / (mSWD[i].C + mToSWD[i].C));
                    //move content to the SWD pool
                    mSWD[i] += mToSWD[i];
                }

                if (mSWD[i].C > 0)
                {
                    // reduce the Carbon (note: the N stays, thus the CN ratio changes)
                    // use the decay rate that is derived as a weighted average of all standing woody debris
                    double survive_rate = Math.Exp(-mKSW[i] * climate_factor_re * 1.0); // 1: timestep
                    mTotalToAtm.C += mSWD[i].C * (1.0 - survive_rate);
                    mSWD[i].C *= survive_rate;

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

                    double half_life = mHalfLife[i] / climate_factor_re;
                    double rate = -Constant.M_LN2 / half_life; // M_LN2: math. constant

                    // higher decay rate for the class with smallest diameters
                    if (i == 0)
                    {
                        rate *= 2.0;
                    }
                    double transfer = 1.0 - Math.Exp(rate);

                    // calculate flow to soil pool...
                    mSWDtoSoil += mSWD[i] * transfer;
                    mRefractoryFlux += mSWD[i] * transfer;
                    mSWD[i] *= (1.0 - transfer); // reduce pool
                                                 // calculate the stem number of remaining snags
                    mNumberOfSnags[i] = mNumberOfSnags[i] * (1.0 - transfer);

                    mTimeSinceDeath[i] += 1.0;
                    // if stems<0.5, empty the whole cohort into DWD, i.e. release the last bit of C and N and clear the stats
                    // also, if the Carbon of an average snag is less than 10% of the original average tree
                    // (derived from allometries for the three diameter classes), the whole cohort is emptied out to DWD
                    if (mNumberOfSnags[i] < 0.5 || mSWD[i].C / mNumberOfSnags[i] < mCarbonThreshold[i])
                    {
                        // clear the pool: add the rest to the soil, clear statistics of the pool
                        mRefractoryFlux += mSWD[i];
                        mSWDtoSoil += mSWD[i];
                        mSWD[i].clear();
                        mAvgDbh[i] = 0.0;
                        mAvgHeight[i] = 0.0;
                        mAvgVolume[i] = 0.0;
                        mKSW[i] = 0.0;
                        mCurrentKSW[i] = 0.0;
                        mHalfLife[i] = 0.0;
                        mTimeSinceDeath[i] = 0.0;
                    }
                }
            }
            // total carbon in the snag-container on the RU *after* processing is the content of the
            // standing woody debris pools + the branches
            mTotalSnagCarbon = mSWD[0].C + mSWD[1].C + mSWD[2].C + mOtherWood[0].C + mOtherWood[1].C + mOtherWood[2].C + mOtherWood[3].C + mOtherWood[4].C;
            mTotalSWD = mSWD[0] + mSWD[1] + mSWD[2];
            mTotalOther = mOtherWood[0] + mOtherWood[1] + mOtherWood[2] + mOtherWood[3] + mOtherWood[4];
        }

        /// foliage and fineroot litter is transferred during tree growth.
        public void addTurnoverLitter(Species species, double litter_foliage, double litter_fineroot)
        {
            mLabileFlux.addBiomass(litter_foliage, species.cnFoliage(), species.snagKyl());
            mLabileFlux.addBiomass(litter_fineroot, species.cnFineroot(), species.snagKyl());
            Debug.WriteLineIf(Double.IsNaN(mLabileFlux.C), "addTurnoverLitter: NaN");
        }

        public void addTurnoverWood(Species species, double woody_biomass)
        {
            mRefractoryFlux.addBiomass(woody_biomass, species.cnWood(), species.snagKyr());
            Debug.WriteLineIf(Double.IsNaN(mRefractoryFlux.C), "addTurnoverWood: NaN");
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
        public void addBiomassPools(Tree tree, double stem_to_snag, double stem_to_soil, double branch_to_snag, double branch_to_soil, double foliage_to_soil)
        {
            Species species = tree.species();

            double branch_biomass = tree.biomassBranch();
            // fine roots go to the labile pool
            mLabileFlux.addBiomass(tree.biomassFineRoot(), species.cnFineroot(), species.snagKyl());

            // a part of the foliage goes to the soil
            mLabileFlux.addBiomass(tree.biomassFoliage() * foliage_to_soil, species.cnFoliage(), species.snagKyl());

            //coarse roots and a part of branches are equally distributed over five years:
            double biomass_rest = (tree.biomassCoarseRoot() + branch_to_snag * branch_biomass) * 0.2;
            for (int i = 0; i < 5; i++)
            {
                mOtherWood[i].addBiomass(biomass_rest, species.cnWood(), species.snagKyr());
            }

            // the other part of the branches goes directly to the soil
            mRefractoryFlux.addBiomass(branch_biomass * branch_to_soil, species.cnWood(), species.snagKyr());
            // a part of the stem wood goes directly to the soil
            mRefractoryFlux.addBiomass(tree.biomassStem() * stem_to_soil, species.cnWood(), species.snagKyr());

            // just for book-keeping: keep track of all inputs of branches / roots / swd into the "snag" pools
            mTotalIn.addBiomass(tree.biomassBranch() * branch_to_snag + tree.biomassCoarseRoot() + tree.biomassStem() * stem_to_snag, species.cnWood());
            // stem biomass is transferred to the standing woody debris pool (SWD), increase stem number of pool
            int pi = poolIndex(tree.dbh()); // get right transfer pool

            if (stem_to_snag > 0.0)
            {
                // update statistics - stemnumber-weighted averages
                // note: here the calculations are repeated for every died trees (i.e. consecutive weighting ... but delivers the same results)
                double p_old = mNumberOfSnags[pi] / (mNumberOfSnags[pi] + 1); // weighting factor for state vars (based on stem numbers)
                double p_new = 1.0 / (mNumberOfSnags[pi] + 1); // weighting factor for added tree (p_old + p_new = 1).
                mAvgDbh[pi] = mAvgDbh[pi] * p_old + tree.dbh() * p_new;
                mAvgHeight[pi] = mAvgHeight[pi] * p_old + tree.height() * p_new;
                mAvgVolume[pi] = mAvgVolume[pi] * p_old + tree.volume() * p_new;
                mTimeSinceDeath[pi] = mTimeSinceDeath[pi] * p_old + p_new;
                mHalfLife[pi] = mHalfLife[pi] * p_old + species.snagHalflife() * p_new;

                // average the decay rate (ksw); this is done based on the carbon content
                // aggregate all trees that die in the current year (and save weighted decay rates to CurrentKSW)
                p_old = mToSWD[pi].C / (mToSWD[pi].C + tree.biomassStem() * CNPair.biomassCFraction);
                p_new = tree.biomassStem() * CNPair.biomassCFraction / (mToSWD[pi].C + tree.biomassStem() * CNPair.biomassCFraction);
                mCurrentKSW[pi] = mCurrentKSW[pi] * p_old + species.snagKsw() * p_new;
                mNumberOfSnags[pi]++;
            }

            // finally add the biomass of the stem to the standing snag pool
            CNPool to_swd = mToSWD[pi];
            to_swd.addBiomass(tree.biomassStem() * stem_to_snag, species.cnWood(), species.snagKyr());

            // the biomass that is not routed to snags or directly to the soil
            // is removed from the system (to atmosphere or harvested)
            mTotalToExtern.addBiomass(tree.biomassFoliage() * (1.0 - foliage_to_soil) +
                                      branch_biomass * (1.0 - branch_to_snag - branch_to_soil) +
                                      tree.biomassStem() * (1.0 - stem_to_snag - stem_to_soil), species.cnWood());

        }


        /// after the death of the tree the five biomass compartments are processed.
        public void addMortality(Tree tree)
        {
            addBiomassPools(tree, 1.0, 0.0,  // all stem biomass goes to snag
                            1.0, 0.0,        // all branch biomass to snag
                            1.0);           // all foliage to soil

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
        public void addHarvest(Tree tree, double remove_stem_fraction, double remove_branch_fraction, double remove_foliage_fraction)
        {
            addBiomassPools(tree, 0.0, 1.0 - remove_stem_fraction, // "remove_stem_fraction" is removed . the rest goes to soil
                                  0.0, 1.0 - remove_branch_fraction, // "remove_branch_fraction" is removed . the rest goes directly to the soil
                                  1.0 - remove_foliage_fraction); // the rest of foliage is routed to the soil
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
        public void addToSoil(Species species, CNPair woody_pool, CNPair litter_pool)
        {
            mLabileFlux.add(litter_pool, species.snagKyl());
            mRefractoryFlux.add(woody_pool, species.snagKyr());
            Debug.WriteLineIf(Double.IsNaN(mLabileFlux.C) || Double.IsNaN(mRefractoryFlux.C), "addToSoil: NaN in C Pool");
        }

        /// disturbance function: remove the fraction of 'factor' of biomass from the SWD pools; 0: remove nothing, 1: remove all
        /// biomass removed by this function goes to the atmosphere
        public void removeCarbon(double factor)
        {
            // reduce pools of currently standing dead wood and also of pools that are added
            // during (previous) management operations of the current year
            for (int i = 0; i < 3; i++)
            {
                mTotalToDisturbance += (mSWD[i] + mToSWD[i]) * factor;
                mSWD[i] *= (1.0 - factor);
                mToSWD[i] *= (1.0 - factor);
            }

            for (int i = 0; i < 5; i++)
            {
                mTotalToDisturbance += mOtherWood[i] * factor;
                mOtherWood[i] *= (1.0 - factor);
            }
        }

        /// cut down swd (and branches) and move to soil pools
        /// @param factor 0: cut 0%, 1: cut and slash 100% of the wood
        public void management(double factor)
        {
            if (factor < 0.0 || factor > 1.0)
            {
                throw new NotSupportedException(String.Format("Invalid factor in management: '{0}'", factor));
            }
            // swd pools
            for (int i = 0; i < 3; i++)
            {
                mSWDtoSoil += mSWD[i] * factor;
                mRefractoryFlux += mSWD[i] * factor;
                mSWD[i] *= (1.0 - factor);
                //mSWDtoSoil += mToSWD[i] * factor;
                //mToSWD[i] *= (1.0 - factor);
            }
            // what to do with the branches: now move also all wood to soil (note: this is note
            // very good w.r.t the coarse roots...
            for (int i = 0; i < 5; i++)
            {
                mRefractoryFlux += mOtherWood[i] * factor;
                mOtherWood[i] *= (1.0 - factor);
            }
        }
    }
}
