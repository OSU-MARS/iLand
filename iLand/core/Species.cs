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
        private readonly StampContainer mLIPs; ///< ptr to the container of the LIP-pattern

        // biomass allometries:
        private double mFoliage_a, mFoliage_b;  ///< allometry (biomass = a * dbh^b) for foliage
        private double mWoody_a, mWoody_b; ///< allometry (biomass = a * dbh^b) for woody compartments aboveground
        private double mRoot_a, mRoot_b; ///< allometry (biomass = a * dbh^b) for roots (compound, fine and coarse roots as one pool)
        private double mBranch_a, mBranch_b; ///< allometry (biomass = a * dbh^b) for branches
        // cn-ratios
        private double mBarkThicknessFactor; ///< multiplier to estimate bark thickness (cm) from dbh

        // height-diameter-relationships
        private readonly Expression mHDlow; ///< minimum HD-relation as f(d) (open grown tree)
        private readonly Expression mHDhigh; ///< maximum HD-relation as f(d)
        // stem density and taper
        private double mFormFactor; ///< taper form factor of the stem [-] used for volume / stem-mass calculation calculation
        // mortality
        private double mDeathProb_stress; ///< max. prob. of death per year when tree suffering maximum stress
        // Aging
        private double mMaximumAge; ///< maximum age of species (years)
        private double mMaximumHeight; ///< maximum height of species (m) for aging
        private readonly Expression mAging;
        // environmental responses
        private double mRespVpdExponent; ///< exponent in vpd response calculation (Mkela 2008)
        private double mRespTempMin; ///< temperature response calculation offset
        private double mRespTempMax; ///< temperature response calculation: saturation point for temp. response
        private double mRespNitrogenClass; ///< nitrogen response class (1..3). fractional values (e.g. 1.2) are interpolated.
        private double mLightResponseClass; ///< light response class (1..5) (1=shade intolerant)
        // regeneration
        private int mMaturityYears; ///< a tree produces seeds if it is older than this parameter
        private double mSeedYearProbability; ///< probability that a year is a seed year (=1/avg.timespan between seedyears)
        // regeneration - seed dispersal
        private double mTM_as1; ///< seed dispersal paramaters (treemig)
        private double mTM_as2; ///< seed dispersal paramaters (treemig)
        private double mTM_ks; ///< seed dispersal paramaters (treemig)
        private readonly Expression mSerotiny; ///< function that decides (probabilistic) if a tree is serotinous; empty: serotiny not active

        // properties
        /// @property id 4-character unique identification of the tree species
        public string ID { get; private set; }
        /// the full name (e.g. Picea abies) of the species
        public string Name { get; private set; }
        public int Index { get; private set; } ///< unique index of species within current set
        public int PhenologyClass { get; private set; } ///< phenology class defined in project file. class 0 = evergreen
        public bool IsConiferous { get; private set; }
        public bool IsEvergreen { get; private set; }
        public bool IsSeedYear { get; private set; }
        // cn ratios
        public double CNRatioFoliage { get; private set; }
        public double CNRatioFineroot { get; private set; }
        public double CNRatioWood { get; private set; }
        // turnover rates
        public double TurnoverLeaf { get; private set; } ///< yearly turnover rate leafs
        public double TurnoverRoot { get; private set; } ///< yearly turnover rate root

        // mortality
        public double DeathProbabilityIntrinsic { get; private set; } ///< prob. of intrinsic death per year [0..1]
        public double FecundityM2 { get; private set; } ///< "surviving seeds" (cf. Moles et al) per m2, see also http://iland.boku.ac.at/fecundity
        public double FecunditySerotiny { get; private set; } ///< multiplier that increases fecundity for post-fire seed rain of serotinous species
        public double MaxCanopyConductance { get; private set; } ///< maximum canopy conductance in m/s
        public double NonSeedYearFraction { get; private set; }
        public double PsiMin { get; private set; }

        // snags
        public double SnagKsw { get; private set; } ///< standing woody debris (swd) decomposition rate
        public double SnagHalflife { get; private set; } ///< half-life-period of standing snags (years)
        public double SnagKyl { get; private set; } ///< decomposition rate for labile matter (litter) used in soil model
        public double SnagKyr { get; private set; } ///< decomposition rate for refractory matter (woody) used in soil model

        // growth
        public double SpecificLeafArea { get; private set; } ///< conversion factor from kg OTS to m2 LeafArea
        public double VolumeFactor { get; private set; } ///< factor for volume calculation: V = factor * D^2*H (incorporates density and the form of the bole)
        public double WoodDensity { get; private set; } ///< density of stem wood [kg/m3]

        public double FinerootFoliageRatio { get; private set; } ///< ratio of fineroot mass (kg) to foliage mass (kg)
        public EstablishmentParameters EstablishmentParameters { get; private set; }
        public SaplingGrowthParameters SaplingGrowthParameters { get; private set; }
        public SeedDispersal SeedDispersal { get; set; }
        public SpeciesSet SpeciesSet { get; private set; }

        public Species(SpeciesSet set)
        {
            this.mAging = new Expression();
            this.EstablishmentParameters = new EstablishmentParameters();
            this.Index = set.SpeciesCount();
            this.mLIPs = new StampContainer();
            this.mHDhigh = new Expression();
            this.mHDlow = new Expression();
            this.SaplingGrowthParameters = new SaplingGrowthParameters();
            this.SeedDispersal = null;
            this.mSerotiny = new Expression();
            this.SpeciesSet = set;
        }

        public bool Active { get; private set; } ///< active??? todo!

        // helpers during setup
        private bool GetBool(string s) { return (bool)SpeciesSet.GetVariable(s); } ///< during setup: get value of variable @p s as a boolean variable.
        private double GetDouble(string s) { return (double)SpeciesSet.GetVariable(s); }///< during setup: get value of variable @p s as a double.
        private int GetInt(string s) { return (int)SpeciesSet.GetVariable(s); } ///< during setup: get value of variable @p s as an integer.
        private string GetString(string s) { return (string)SpeciesSet.GetVariable(s); } ///< during setup: get value of variable @p s as a string.

        // allometries
        public double GetBarkThickness(double dbh) { return dbh * mBarkThicknessFactor; }
        public double GetBiomassFoliage(double dbh) { return mFoliage_a * Math.Pow(dbh, mFoliage_b); }
        public double GetBiomassWoody(double dbh) { return mWoody_a * Math.Pow(dbh, mWoody_b); }
        public double GetBiomassRoot(double dbh) { return mRoot_a * Math.Pow(dbh, mRoot_b); }
        public double GetBiomassBranch(double dbh) { return mBranch_a * Math.Pow(dbh, mBranch_b); }
        public double GetWoodFoliageRatio() { return mWoody_b / mFoliage_b; }

        public Stamp GetStamp(float dbh, float height) { return mLIPs.Stamp(dbh, height); }

        public double GetLightResponse(double lightResourceIndex) { return SpeciesSet.LightResponse(lightResourceIndex, mLightResponseClass); }
        public double GetNitrogenResponse(double availableNitrogen) { return SpeciesSet.NitrogenResponse(availableNitrogen, mRespNitrogenClass); }
        // parameters for seed dispersal
        public void GetTreeMigKernel(ref double ras1, ref double ras2, ref double ks) { ras1 = mTM_as1; ras2 = mTM_as2; ks = mTM_ks; }

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public void Setup()
        {
            Debug.Assert(SpeciesSet != null);
            // setup general information
            ID = GetString("shortName");
            Name = GetString("name");
            string stampFile = GetString("LIPFile");
            // load stamps
            mLIPs.Load(GlobalSettings.Instance.Path(stampFile, "lip"));
            // attach writer stamps to reader stamps
            mLIPs.AttachReaderStamps(SpeciesSet.ReaderStamps);
            if (GlobalSettings.Instance.Settings.GetBooleanParameter("debugDumpStamps", false))
            {
                Debug.WriteLine(mLIPs.Dump());
            }

            // general properties
            IsConiferous = GetBool("isConiferous");
            IsEvergreen = GetBool("isEvergreen");

            // setup allometries
            mFoliage_a = GetDouble("bmFoliage_a");
            mFoliage_b = GetDouble("bmFoliage_b");

            mWoody_a = GetDouble("bmWoody_a");
            mWoody_b = GetDouble("bmWoody_b");

            mRoot_a = GetDouble("bmRoot_a");
            mRoot_b = GetDouble("bmRoot_b");

            mBranch_a = GetDouble("bmBranch_a");
            mBranch_b = GetDouble("bmBranch_b");

            SpecificLeafArea = GetDouble("specificLeafArea");
            FinerootFoliageRatio = GetDouble("finerootFoliageRatio");

            mBarkThicknessFactor = GetDouble("barkThickness");

            // cn-ratios
            CNRatioFoliage = GetDouble("cnFoliage");
            CNRatioFineroot = GetDouble("cnFineRoot");
            CNRatioWood = GetDouble("cnWood");
            if (CNRatioFineroot * CNRatioFoliage * CNRatioWood == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: CN ratio is 0.0", ID));
            }

            // turnover rates
            TurnoverLeaf = GetDouble("turnoverLeaf");
            TurnoverRoot = GetDouble("turnoverRoot");

            // hd-relations
            mHDlow.SetAndParse(GetString("HDlow"));
            mHDhigh.SetAndParse(GetString("HDhigh"));
            mHDlow.Linearize(0.0, 100.0); // input: dbh (cm). above 100cm the formula will be directly executed
            mHDhigh.Linearize(0.0, 100.0);

            // form/density
            WoodDensity = GetDouble("woodDensity");
            mFormFactor = GetDouble("formFactor");
            // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h
            VolumeFactor = mFormFactor * Constant.QuarterPi;

            // snags
            SnagKsw = GetDouble("snagKSW"); // decay rate of SWD
            SnagHalflife = GetDouble("snagHalfLife");
            SnagKyl = GetDouble("snagKYL"); // decay rate labile
            SnagKyr = GetDouble("snagKYR"); // decay rate refractory matter

            if (mFoliage_a * mFoliage_b * mRoot_a * mRoot_b * mWoody_a * mWoody_b * mBranch_a * mBranch_b * WoodDensity * mFormFactor * SpecificLeafArea * FinerootFoliageRatio == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: one value is NULL in database.", ID));
            }
            // Aging
            mMaximumAge = GetDouble("maximumAge");
            mMaximumHeight = GetDouble("maximumHeight");
            mAging.SetAndParse(GetString("aging"));
            mAging.Linearize(0.0, 1.0); // input is harmonic mean of relative age and relative height
            if (mMaximumAge * mMaximumHeight == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}:invalid aging parameters.", ID));
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. see wiki: base+mortality
            double p_lucky = GetDouble("probIntrinsic");
            double p_lucky_stress = GetDouble("probStress");

            if (p_lucky * mMaximumAge * p_lucky_stress == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: invalid mortality parameters.", ID));
            }

            DeathProbabilityIntrinsic = 1.0 - Math.Pow(p_lucky, 1.0 / mMaximumAge);
            mDeathProb_stress = p_lucky_stress;

            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine("species " + Name + " probStress " + p_lucky_stress + " resulting probability: " + mDeathProb_stress);
            }

            // envirionmental responses
            mRespVpdExponent = GetDouble("respVpdExponent");
            mRespTempMin = GetDouble("respTempMin");
            mRespTempMax = GetDouble("respTempMax");
            if (mRespVpdExponent >= 0)
            {
                throw new NotSupportedException(String.Format("Error: vpd exponent >=0 for species (must be a negative value).", ID));
            }
            if (mRespTempMax == 0.0 || mRespTempMin >= mRespTempMax)
            {
                throw new NotSupportedException(String.Format("temperature response parameters invalid for species", ID));
            }

            mRespNitrogenClass = GetDouble("respNitrogenClass");
            if (mRespNitrogenClass < 1 || mRespNitrogenClass > 3)
            {
                throw new NotSupportedException(String.Format("nitrogen class invalid (must be >=1 and <=3) for species", ID));
            }

            // phenology
            PhenologyClass = GetInt("phenologyClass");

            // water
            MaxCanopyConductance = GetDouble("maxCanopyConductance");
            PsiMin = -Math.Abs(GetDouble("psiMin")); // force a negative value

            // light
            mLightResponseClass = GetDouble("lightResponseClass");
            if (mLightResponseClass < 1.0 || mLightResponseClass > 5.0)
            {
                throw new NotSupportedException(String.Format("invalid light response class for species {0}. Allowed: 1..5.", ID));
            }

            // regeneration
            int seed_year_interval = GetInt("seedYearInterval");
            if (seed_year_interval == 0)
            {
                throw new NotSupportedException(String.Format("seedYearInterval = 0 for {0}", ID));
            }
            mSeedYearProbability = 1 / (double)(seed_year_interval);
            mMaturityYears = GetInt("maturityYears");
            mTM_as1 = GetDouble("seedKernel_as1");
            mTM_as2 = GetDouble("seedKernel_as2");
            mTM_ks = GetDouble("seedKernel_ks0");
            FecundityM2 = GetDouble("fecundity_m2");
            NonSeedYearFraction = GetDouble("nonSeedYearFraction");
            // special case for serotinous trees (US)
            mSerotiny.SetExpression(GetString("serotinyFormula"));
            FecunditySerotiny = GetDouble("serotinyFecundity");

            // establishment parameters
            EstablishmentParameters.MinTemp = GetDouble("estMinTemp");
            EstablishmentParameters.ChillRequirement = GetInt("estChillRequirement");
            EstablishmentParameters.GddMin = GetInt("estGDDMin");
            EstablishmentParameters.GddMax = GetInt("estGDDMax");
            EstablishmentParameters.GddBaseTemperature = GetDouble("estGDDBaseTemp");
            EstablishmentParameters.GddBudBurst = GetInt("estBudBirstGDD");
            EstablishmentParameters.MinFrostFree = GetInt("estFrostFreeDays");
            EstablishmentParameters.FrostTolerance = GetDouble("estFrostTolerance");
            EstablishmentParameters.PsiMin = -Math.Abs(GetDouble("estPsiMin")); // force negative value

            // sapling and sapling growth parameters
            SaplingGrowthParameters.HeightGrowthPotential.SetAndParse(GetString("sapHeightGrowthPotential"));
            SaplingGrowthParameters.HeightGrowthPotential.Linearize(0.0, 4.0);
            SaplingGrowthParameters.HdSapling = (float)GetDouble("sapHDSapling");
            SaplingGrowthParameters.StressThreshold = GetDouble("sapStressThreshold");
            SaplingGrowthParameters.MaxStressYears = GetInt("sapMaxStressYears");
            SaplingGrowthParameters.ReferenceRatio = GetDouble("sapReferenceRatio");
            SaplingGrowthParameters.ReinekesR = GetDouble("sapReinekesR");
            SaplingGrowthParameters.BrowsingProbability = GetDouble("browsingProbability");
            SaplingGrowthParameters.SproutGrowth = GetDouble("sapSproutGrowth");
            if (SaplingGrowthParameters.SproutGrowth > 0.0)
            {
                if (SaplingGrowthParameters.SproutGrowth < 1.0 || SaplingGrowthParameters.SproutGrowth > 10)
                {
                    Debug.WriteLine("Value of 'sapSproutGrowth' dubious for species " + Name + "(value: " + SaplingGrowthParameters.SproutGrowth + ")");
                }
            }
            SaplingGrowthParameters.SetupReinekeLookup();
        }

        /** calculate fraction of stem wood increment base on dbh.
            allometric equation: a*d^b -> first derivation: a*b*d^(b-1)
            the ratio for stem is 1 minus the ratio of twigs to total woody increment at current "dbh". */
        public double AllometricFractionStem(double dbh)
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
        public double Aging(float height, int age)
        {
            double rel_height = Math.Min(height / mMaximumHeight, 0.999999); // 0.999999 -> avoid div/0
            double rel_age = Math.Min(age / mMaximumAge, 0.999999);

            // harmonic mean: http://en.wikipedia.org/wiki/Harmonic_mean
            double x = 1.0 - 2.0 / (1.0 / (1.0 - rel_height) + 1.0 / (1.0 - rel_age)); // Note:

            double aging_factor = mAging.Calculate(x);

            return Global.Limit(aging_factor, 0.0, 1.0); // limit to [0..1]
        }

        public int EstimateAge(float height)
        {
            int age_rel = (int)(mMaximumAge * height / mMaximumHeight);
            return age_rel;
        }

        /** Seed production.
           This function produces seeds if the tree is older than a species-specific age ("maturity")
           If seeds are produced, this information is stored in a "SeedMap"
          */
        /// check the maturity of the tree and flag the position as seed source appropriately
        public void SeedProduction(Tree tree)
        {
            if (SeedDispersal == null)
            {
                return; // regeneration is disabled
            }

            // if the tree is considered as serotinous (i.e. seeds need external trigger such as fire)
            if (IsTreeSerotinous(tree.Age))
            {
                return;
            }

            // no seed production if maturity age is not reached (species parameter) or if tree height is below 4m.
            if (tree.Age > mMaturityYears && tree.Height > 4.0F)
            {
                SeedDispersal.SetMatureTree(tree.LightCellIndex, tree.LeafArea);
            }
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool IsTreeSerotinous(int age)
        {
            if (mSerotiny.IsEmpty)
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            double p_serotinous = mSerotiny.Calculate(age);
            if (RandomGenerator.Random() < p_serotinous)
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
        public void NewYear()
        {
            if (SeedDispersal != null)
            {
                // decide whether current year is a seed year
                IsSeedYear = (RandomGenerator.Random() < mSeedYearProbability);
                if (IsSeedYear && GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("species " + ID + " has a seed year.");
                }
                // clear seed map
                SeedDispersal.Clear();
            }
        }

        public void GetHeightDiameterRatioLimits(double dbh, out double rLowHD, out double rHighHD)
        {
            rLowHD = mHDlow.Calculate(dbh);
            rHighHD = mHDhigh.Calculate(dbh);
        }

        /** vpdResponse calculates response on vpd.
            Input: vpd [kPa]*/
        public double VpdResponse(double vpd)
        {
            return Math.Exp(mRespVpdExponent * vpd);
        }

        /** temperatureResponse calculates response on delayed daily temperature.
            Input: average temperature [C]
            Note: slightly different from Mkela 2008: the maximum parameter (Sk) in iLand is interpreted as the absolute
                  temperature yielding a response of 1; in Mkela 2008, Sk is the width of the range (relative to the lower threhold)
            */
        public double TemperatureResponse(double delayed_temp)
        {
            double x = Math.Max(delayed_temp - mRespTempMin, 0.0);
            x = Math.Min(x / (mRespTempMax - mRespTempMin), 1.0);
            return x;
        }

        /** soilwaterResponse is a function of the current matrix potential of the soil.
          */
        public double SoilwaterResponse(double psi_kPa)
        {
            double psi_mpa = psi_kPa / 1000.0; // convert to MPa
            double result = Global.Limit((psi_mpa - PsiMin) / (-0.015 - PsiMin), 0.0, 1.0);
            return result;
        }

        /** calculate probabilty of death based on the current stress index. */
        public double GetDeathProbabilityForStress(double stress_index)
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
