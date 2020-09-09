using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.core
{
    /** @class Species
      @ingroup core
      The behavior and general properties of tree species.
      Because the individual trees are designed as leightweight as possible, lots of stuff is done by the Species.
      Inter alia, Species do:
      - store all the precalcualted patterns for light competition (LIP, stamps)
      - do most of the growth (3PG) calculation
      */
    internal class Species
    {
        // helpers during setup
        private bool boolVar(string s) { return (bool)mSet.var(s); } ///< during setup: get value of variable @p s as a boolean variable.
        private double doubleVar(string s) { return (double)mSet.var(s); }///< during setup: get value of variable @p s as a double.
        private int intVar(string s) { return (int)mSet.var(s); } ///< during setup: get value of variable @p s as an integer.
        private string stringVar(string s) { return (string)mSet.var(s); } ///< during setup: get value of variable @p s as a string.
        private SpeciesSet mSet; ///< ptr. to the "parent" set
        private StampContainer mLIPs; ///< ptr to the container of the LIP-pattern
        private string mId;
        private string mName;

        private int mIndex; ///< internal index within the SpeciesSet
        private bool mConiferous; ///< true if confierous species (vs. broadleaved)
        private bool mEvergreen; ///< true if evergreen species
        // biomass allometries:
        private double mFoliage_a, mFoliage_b;  ///< allometry (biomass = a * dbh^b) for foliage
        private double mWoody_a, mWoody_b; ///< allometry (biomass = a * dbh^b) for woody compartments aboveground
        private double mRoot_a, mRoot_b; ///< allometry (biomass = a * dbh^b) for roots (compound, fine and coarse roots as one pool)
        private double mBranch_a, mBranch_b; ///< allometry (biomass = a * dbh^b) for branches
        // cn-ratios
        private double mCNFoliage, mCNFineroot, mCNWood; ///< CN-ratios for various tissue types; stem, branches and coarse roots are pooled as 'wood'
        private double mBarkThicknessFactor; ///< multiplier to estimate bark thickness (cm) from dbh

        private double mSpecificLeafArea; ///< conversion factor from kg OTS to m2 LeafArea
        // turnover rates
        private double mTurnoverLeaf; ///< yearly turnover rate leafs
        private double mTurnoverRoot; ///< yearly turnover rate root
        private double mFinerootFoliageRatio; ///< ratio of fineroot mass (kg) to foliage mass (kg)
        // height-diameter-relationships
        private Expression mHDlow; ///< minimum HD-relation as f(d) (open grown tree)
        private Expression mHDhigh; ///< maximum HD-relation as f(d)
        // stem density and taper
        private double mWoodDensity; ///< density of the wood [kg/m3]
        private double mFormFactor; ///< taper form factor of the stem [-] used for volume / stem-mass calculation calculation
        private double mVolumeFactor; ///< factor for volume calculation
        // snag dynamics
        private double mSnagKSW; ///< standing woody debris (swd) decomposition rate
        private double mSnagKYL; ///< decomposition rate for labile matter (litter) used in soil model
        private double mSnagKYR; ///< decomposition rate for refractory matter (woody) used in soil model
        private double mSnagHalflife; ///< half-life-period of standing snags (years)
        // mortality
        private double mDeathProb_intrinsic;  ///< prob. of intrinsic death per year [0..1]
        private double mDeathProb_stress; ///< max. prob. of death per year when tree suffering maximum stress
        // Aging
        private double mMaximumAge; ///< maximum age of species (years)
        private double mMaximumHeight; ///< maximum height of species (m) for aging
        private Expression mAging;
        // environmental responses
        private double mRespVpdExponent; ///< exponent in vpd response calculation (Mkela 2008)
        private double mRespTempMin; ///< temperature response calculation offset
        private double mRespTempMax; ///< temperature response calculation: saturation point for temp. response
        private double mRespNitrogenClass; ///< nitrogen response class (1..3). fractional values (e.g. 1.2) are interpolated.
        private double mPsiMin; ///< minimum water potential (MPa), i.e. wilting point (is below zero!)
        // water
        private double mMaxCanopyConductance; ///< maximum canopy conductance for transpiration (m/s)
        private int mPhenologyClass;
        private double mLightResponseClass; ///< light response class (1..5) (1=shade intolerant)
        // regeneration
        private SeedDispersal mSeedDispersal; ///< link to the seed dispersal map of the species
        private int mMaturityYears; ///< a tree produces seeds if it is older than this parameter
        private double mSeedYearProbability; ///< probability that a year is a seed year (=1/avg.timespan between seedyears)
        private bool mIsSeedYear; ///< true, if current year is a seed year. see also:
        private double mNonSeedYearFraction;  ///< fraction of the seed production in non-seed-years
        // regeneration - seed dispersal
        private double mFecundity_m2; ///< "surviving seeds" (cf. Moles et al) per m2, see also http://iland.boku.ac.at/fecundity
        private double mTM_as1; ///< seed dispersal paramaters (treemig)
        private double mTM_as2; ///< seed dispersal paramaters (treemig)
        private double mTM_ks; ///< seed dispersal paramaters (treemig)
        private EstablishmentParameters mEstablishmentParams; ///< collection of parameters used for establishment
        private SaplingGrowthParameters mSaplingGrowthParams; ///< collection of parameters for sapling growth
        private Expression mSerotiny; ///< function that decides (probabilistic) if a tree is serotinous; empty: serotiny not active
        private double mSerotinyFecundity; ///< multiplier that increases fecundity for post-fire seed rain of serotinous species

        private int mDisplayColor;

        public Species(SpeciesSet set)
        { 
            mSet = set; 
            mIndex = set.count(); 
            mSeedDispersal = null; 
        }

        public SpeciesSet speciesSet() { return mSet; }
        // properties
        public SeedDispersal seedDispersal() { return mSeedDispersal; }
        /// @property id 4-character unique identification of the tree species
        public string id() { return mId; }
        /// the full name (e.g. Picea Abies) of the species
        public string name() { return mName; }
        public int index() { return mIndex; } ///< unique index of species within current set
        public bool active() { return true; } ///< active??? todo!
        public int phenologyClass() { return mPhenologyClass; } ///< phenology class defined in project file. class 0 = evergreen
        public bool isConiferous() { return mConiferous; }
        public bool isEvergreen() { return mEvergreen; }
        public bool isSeedYear() { return mIsSeedYear; }

        // calculations: allometries
        public double biomassFoliage(double dbh) { return mFoliage_a * Math.Pow(dbh, mFoliage_b); }
        public double biomassWoody(double dbh) { return mWoody_a * Math.Pow(dbh, mWoody_b); }
        public double biomassRoot(double dbh) { return mRoot_a * Math.Pow(dbh, mRoot_b); }
        public double biomassBranch(double dbh) { return mBranch_a * Math.Pow(dbh, mBranch_b); }
        public double allometricRatio_wf() { return mWoody_b / mFoliage_b; }
        public double finerootFoliageRatio() { return mFinerootFoliageRatio; } ///< ratio of fineroot mass (kg) to foliage mass (kg)
        public double barkThickness(double dbh) { return dbh * mBarkThicknessFactor; }
        // cn ratios
        public double cnFoliage() { return mCNFoliage; }
        public double cnFineroot() { return mCNFineroot; }
        public double cnWood() { return mCNWood; }
        // turnover rates
        public double turnoverLeaf() { return mTurnoverLeaf; }
        public double turnoverRoot() { return mTurnoverRoot; }
        // snags
        public double snagKsw() { return mSnagKSW; }
        public double snagHalflife() { return mSnagHalflife; }
        public double snagKyl() { return mSnagKYL; } ///< decomposition rate for labile matter (litter) used in soil model
        public double snagKyr() { return mSnagKYR; } ///< decomposition rate for refractory matter (woody) used in soil model

        // growth
        public double volumeFactor() { return mVolumeFactor; } ///< factor for volume calculation: V = factor * D^2*H (incorporates density and the form of the bole)
        public double density() { return mWoodDensity; } ///< density of stem wood [kg/m3]
        public double specificLeafArea() { return mSpecificLeafArea; }
        // mortality
        public double deathProb_intrinsic() { return mDeathProb_intrinsic; }

        public void setSeedDispersal(SeedDispersal seed_dispersal) { mSeedDispersal = seed_dispersal; }
        public double nitrogenResponse(double availableNitrogen) { return mSet.nitrogenResponse(availableNitrogen, mRespNitrogenClass); }
        public double canopyConductance() { return mMaxCanopyConductance; } ///< maximum canopy conductance in m/s
        public double lightResponse(double lightResourceIndex) { return mSet.lightResponse(lightResourceIndex, mLightResponseClass); }
        public double psiMin() { return mPsiMin; }
        // parameters for seed dispersal
        public void treeMigKernel(ref double ras1, ref double ras2, ref double ks) { ras1 = mTM_as1; ras2 = mTM_as2; ks = mTM_ks; }
        public double fecundity_m2() { return mFecundity_m2; }
        public double nonSeedYearFraction() { return mNonSeedYearFraction; }
        public double fecunditySerotiny() { return mSerotinyFecundity; }

        public EstablishmentParameters establishmentParameters() { return mEstablishmentParams; }
        public SaplingGrowthParameters saplingGrowthParameters() { return mSaplingGrowthParams; }

        public Stamp stamp(float dbh, float height) { return mLIPs.stamp(dbh, height); }

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public void setup()
        {
            Debug.Assert(mSet != null);
            // setup general information
            mId = stringVar("shortName");
            mName = stringVar("name");
            mDisplayColor = 0;
            string stampFile = stringVar("LIPFile");
            // load stamps
            mLIPs.load(GlobalSettings.instance().path(stampFile, "lip"));
            // attach writer stamps to reader stamps
            mLIPs.attachReaderStamps(mSet.readerStamps());
            if (GlobalSettings.instance().settings().paramValueBool("debugDumpStamps", false))
            {
                Debug.WriteLine(mLIPs.dump());
            }

            // general properties
            mConiferous = boolVar("isConiferous");
            mEvergreen = boolVar("isEvergreen");

            // setup allometries
            mFoliage_a = doubleVar("bmFoliage_a");
            mFoliage_b = doubleVar("bmFoliage_b");

            mWoody_a = doubleVar("bmWoody_a");
            mWoody_b = doubleVar("bmWoody_b");

            mRoot_a = doubleVar("bmRoot_a");
            mRoot_b = doubleVar("bmRoot_b");

            mBranch_a = doubleVar("bmBranch_a");
            mBranch_b = doubleVar("bmBranch_b");

            mSpecificLeafArea = doubleVar("specificLeafArea");
            mFinerootFoliageRatio = doubleVar("finerootFoliageRatio");

            mBarkThicknessFactor = doubleVar("barkThickness");

            // cn-ratios
            mCNFoliage = doubleVar("cnFoliage");
            mCNFineroot = doubleVar("cnFineRoot");
            mCNWood = doubleVar("cnWood");
            if (mCNFineroot * mCNFoliage * mCNWood == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: CN ratio is 0.0", id()));
            }

            // turnover rates
            mTurnoverLeaf = doubleVar("turnoverLeaf");
            mTurnoverRoot = doubleVar("turnoverRoot");

            // hd-relations
            mHDlow.setAndParse(stringVar("HDlow"));
            mHDhigh.setAndParse(stringVar("HDhigh"));
            mHDlow.linearize(0.0, 100.0); // input: dbh (cm). above 100cm the formula will be directly executed
            mHDhigh.linearize(0.0, 100.0);

            // form/density
            mWoodDensity = doubleVar("woodDensity");
            mFormFactor = doubleVar("formFactor");
            // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h
            mVolumeFactor = mFormFactor * Constant.M_PI_4;

            // snags
            mSnagKSW = doubleVar("snagKSW"); // decay rate of SWD
            mSnagHalflife = doubleVar("snagHalfLife");
            mSnagKYL = doubleVar("snagKYL"); // decay rate labile
            mSnagKYR = doubleVar("snagKYR"); // decay rate refractory matter

            if (mFoliage_a * mFoliage_b * mRoot_a * mRoot_b * mWoody_a * mWoody_b * mBranch_a * mBranch_b * mWoodDensity * mFormFactor * mSpecificLeafArea * mFinerootFoliageRatio == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: one value is NULL in database.", id()));
            }
            // Aging
            mMaximumAge = doubleVar("maximumAge");
            mMaximumHeight = doubleVar("maximumHeight");
            mAging.setAndParse(stringVar("aging"));
            mAging.linearize(0.0, 1.0); // input is harmonic mean of relative age and relative height
            if (mMaximumAge * mMaximumHeight == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}:invalid aging parameters.", id()));
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. see wiki: base+mortality
            double p_lucky = doubleVar("probIntrinsic");
            double p_lucky_stress = doubleVar("probStress");

            if (p_lucky * mMaximumAge * p_lucky_stress == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: invalid mortality parameters.", id()));
            }

            mDeathProb_intrinsic = 1.0 - Math.Pow(p_lucky, 1.0 / mMaximumAge);
            mDeathProb_stress = p_lucky_stress;

            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("species " + name() + " probStress " + p_lucky_stress + " resulting probability: " + mDeathProb_stress);
            }

            // envirionmental responses
            mRespVpdExponent = doubleVar("respVpdExponent");
            mRespTempMin = doubleVar("respTempMin");
            mRespTempMax = doubleVar("respTempMax");
            if (mRespVpdExponent >= 0)
            {
                throw new NotSupportedException(String.Format("Error: vpd exponent >=0 for species (must be a negative value).", id()));
            }
            if (mRespTempMax == 0.0 || mRespTempMin >= mRespTempMax)
            {
                throw new NotSupportedException(String.Format("temperature response parameters invalid for species", id()));
            }

            mRespNitrogenClass = doubleVar("respNitrogenClass");
            if (mRespNitrogenClass < 1 || mRespNitrogenClass > 3)
            {
                throw new NotSupportedException(String.Format("nitrogen class invalid (must be >=1 and <=3) for species", id()));
            }

            // phenology
            mPhenologyClass = intVar("phenologyClass");

            // water
            mMaxCanopyConductance = doubleVar("maxCanopyConductance");
            mPsiMin = -Math.Abs(doubleVar("psiMin")); // force a negative value

            // light
            mLightResponseClass = doubleVar("lightResponseClass");
            if (mLightResponseClass < 1.0 || mLightResponseClass > 5.0)
            {
                throw new NotSupportedException(String.Format("invalid light response class for species {0}. Allowed: 1..5.", id()));
            }

            // regeneration
            int seed_year_interval = intVar("seedYearInterval");
            if (seed_year_interval == 0)
            {
                throw new NotSupportedException(String.Format("seedYearInterval = 0 for {0}", id()));
            }
            mSeedYearProbability = 1 / (double)(seed_year_interval);
            mMaturityYears = intVar("maturityYears");
            mTM_as1 = doubleVar("seedKernel_as1");
            mTM_as2 = doubleVar("seedKernel_as2");
            mTM_ks = doubleVar("seedKernel_ks0");
            mFecundity_m2 = doubleVar("fecundity_m2");
            mNonSeedYearFraction = doubleVar("nonSeedYearFraction");
            // special case for serotinous trees (US)
            mSerotiny.setExpression(stringVar("serotinyFormula"));
            mSerotinyFecundity = doubleVar("serotinyFecundity");

            // establishment parameters
            mEstablishmentParams.min_temp = doubleVar("estMinTemp");
            mEstablishmentParams.chill_requirement = intVar("estChillRequirement");
            mEstablishmentParams.GDD_min = intVar("estGDDMin");
            mEstablishmentParams.GDD_max = intVar("estGDDMax");
            mEstablishmentParams.GDD_baseTemperature = doubleVar("estGDDBaseTemp");
            mEstablishmentParams.bud_birst = intVar("estBudBirstGDD");
            mEstablishmentParams.frost_free = intVar("estFrostFreeDays");
            mEstablishmentParams.frost_tolerance = doubleVar("estFrostTolerance");
            mEstablishmentParams.psi_min = -Math.Abs(doubleVar("estPsiMin")); // force negative value

            // sapling and sapling growth parameters
            mSaplingGrowthParams.heightGrowthPotential.setAndParse(stringVar("sapHeightGrowthPotential"));
            mSaplingGrowthParams.heightGrowthPotential.linearize(0.0, 4.0);
            mSaplingGrowthParams.hdSapling = (float)doubleVar("sapHDSapling");
            mSaplingGrowthParams.stressThreshold = doubleVar("sapStressThreshold");
            mSaplingGrowthParams.maxStressYears = intVar("sapMaxStressYears");
            mSaplingGrowthParams.referenceRatio = doubleVar("sapReferenceRatio");
            mSaplingGrowthParams.ReinekesR = doubleVar("sapReinekesR");
            mSaplingGrowthParams.browsingProbability = doubleVar("browsingProbability");
            mSaplingGrowthParams.sproutGrowth = doubleVar("sapSproutGrowth");
            if (mSaplingGrowthParams.sproutGrowth > 0.0)
            {
                if (mSaplingGrowthParams.sproutGrowth < 1.0 || mSaplingGrowthParams.sproutGrowth > 10)
                {
                    Debug.WriteLine("Value of 'sapSproutGrowth' dubious for species " + name() + "(value: " + mSaplingGrowthParams.sproutGrowth + ")");
                }
            }
            mSaplingGrowthParams.setupReinekeLookup();
        }

        /** calculate fraction of stem wood increment base on dbh.
            allometric equation: a*d^b -> first derivation: a*b*d^(b-1)
            the ratio for stem is 1 minus the ratio of twigs to total woody increment at current "dbh". */
        public double allometricFractionStem(double dbh)
        {
            double fraction_stem = 1.0 - (mBranch_a * mBranch_b * Math.Pow(dbh, mBranch_b - 1.0)) / (mWoody_a * mWoody_b * Math.Pow(dbh, mWoody_b - 1));
            return fraction_stem;
        }

        /** Aging formula.
           calculates a relative "age" by combining a height- and an age-related term using a harmonic mean,
           and feeding this into the Landsberg and Waring formula.
           see http://iland.boku.ac.at/primary+production#respiration_and_aging
           @param useAge set to true if "real" tree age is available. If false, only the tree height is used.
          */
        public double aging(float height, int age)
        {
            double rel_height = Math.Min(height / mMaximumHeight, 0.999999); // 0.999999 -> avoid div/0
            double rel_age = Math.Min(age / mMaximumAge, 0.999999);

            // harmonic mean: http://en.wikipedia.org/wiki/Harmonic_mean
            double x = 1.0 - 2.0 / (1.0 / (1.0 - rel_height) + 1.0 / (1.0 - rel_age)); // Note:

            double aging_factor = mAging.calculate(x);

            return Global.limit(aging_factor, 0.0, 1.0); // limit to [0..1]
        }

        public int estimateAge(float height)
        {
            int age_rel = (int)(mMaximumAge * height / mMaximumHeight);
            return age_rel;
        }

        /** Seed production.
           This function produces seeds if the tree is older than a species-specific age ("maturity")
           If seeds are produced, this information is stored in a "SeedMap"
          */
        /// check the maturity of the tree and flag the position as seed source appropriately
        public void seedProduction(Tree tree)
        {
            if (mSeedDispersal == null)
            {
                return; // regeneration is disabled
            }

            // if the tree is considered as serotinous (i.e. seeds need external trigger such as fire)
            if (isTreeSerotinous(tree.age()))
            {
                return;
            }

            // no seed production if maturity age is not reached (species parameter) or if tree height is below 4m.
            if (tree.age() > mMaturityYears && tree.height() > 4.0F)
            {
                mSeedDispersal.setMatureTree(tree.positionIndex(), tree.leafArea());
            }
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool isTreeSerotinous(int age)
        {
            if (mSerotiny.isEmpty())
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            double p_serotinous = mSerotiny.calculate(age);
            if (RandomGenerator.drandom() < p_serotinous)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /** newYear is called by the SpeciesSet at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void newYear()
        {
            if (seedDispersal() != null)
            {
                // decide whether current year is a seed year
                mIsSeedYear = (RandomGenerator.drandom() < mSeedYearProbability);
                if (mIsSeedYear && GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("species " + id() + " has a seed year.");
                }
                // clear seed map
                seedDispersal().clear();
            }
        }

        public void hdRange(double dbh, out double rLowHD, out double rHighHD)
        {
            rLowHD = mHDlow.calculate(dbh);
            rHighHD = mHDhigh.calculate(dbh);
        }
        /** vpdResponse calculates response on vpd.
            Input: vpd [kPa]*/
        public double vpdResponse(double vpd)
        {
            return Math.Exp(mRespVpdExponent * vpd);
        }

        /** temperatureResponse calculates response on delayed daily temperature.
            Input: average temperature [C]
            Note: slightly different from Mkela 2008: the maximum parameter (Sk) in iLand is interpreted as the absolute
                  temperature yielding a response of 1; in Mkela 2008, Sk is the width of the range (relative to the lower threhold)
*/
        public double temperatureResponse(double delayed_temp)
        {
            double x = Math.Max(delayed_temp - mRespTempMin, 0.0);
            x = Math.Min(x / (mRespTempMax - mRespTempMin), 1.0);
            return x;
        }

        /** soilwaterResponse is a function of the current matrix potential of the soil.

          */
        public double soilwaterResponse(double psi_kPa)
        {
            double psi_mpa = psi_kPa / 1000.0; // convert to MPa
            double result = Global.limit((psi_mpa - mPsiMin) / (-0.015 - mPsiMin), 0.0, 1.0);
            return result;
        }

        /** calculate probabilty of death based on the current stress index. */
        public double deathProb_stress(double stress_index)
        {
            if (stress_index == 0.0)
            {
                return 0.0;
            }
            double result = 1.0 - Math.Exp(-mDeathProb_stress * stress_index);
            return result;
        }
    }
}
