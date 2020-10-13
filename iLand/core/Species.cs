using iLand.Input;
using iLand.Tools;
using System;
using System.Diagnostics;

namespace iLand.Core
{
    /** @class Species
      @ingroup core
      The behavior and general properties of tree species.
      Because the individual trees are designed as leightweight as possible, lots of stuff is done by the Species.
      Inter alia, Species do:
      - store all the precalcualted patterns for light competition (LIP, stamps)
      - do most of the growth (3PG) calculation
      */
    public class Species
    {
        private readonly SpeciesStamps mLIPs; ///< ptr to the container of the LIP-pattern

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
        public double CNRatioFineRoot { get; private set; }
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
            if (set == null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            this.mAging = new Expression();
            this.EstablishmentParameters = new EstablishmentParameters();
            this.Index = set.SpeciesCount();
            this.mLIPs = new SpeciesStamps();
            this.mHDhigh = new Expression();
            this.mHDlow = new Expression();
            this.SaplingGrowthParameters = new SaplingGrowthParameters();
            this.SeedDispersal = null;
            this.mSerotiny = new Expression();
            this.SpeciesSet = set;
        }

        public bool Active { get; private set; }

        // allometries
        public double GetBarkThickness(double dbh) { return dbh * mBarkThicknessFactor; }
        public double GetBiomassFoliage(double dbh) { return mFoliage_a * Math.Pow(dbh, mFoliage_b); }
        public double GetBiomassWoody(double dbh) { return mWoody_a * Math.Pow(dbh, mWoody_b); }
        public double GetBiomassRoot(double dbh) { return mRoot_a * Math.Pow(dbh, mRoot_b); }
        public double GetBiomassBranch(double dbh) { return mBranch_a * Math.Pow(dbh, mBranch_b); }
        public double GetWoodFoliageRatio() { return mWoody_b / mFoliage_b; }

        public Stamp GetStamp(float dbh, float height) { return mLIPs.GetStamp(dbh, height); }

        public double GetLightResponse(Model model, double lightResourceIndex) 
        { 
            return SpeciesSet.LightResponse(model, lightResourceIndex, mLightResponseClass); 
        }

        public double GetNitrogenResponse(double availableNitrogen) 
        {
            return SpeciesSet.NitrogenResponse(availableNitrogen, mRespNitrogenClass); 
        }

        // parameters for seed dispersal
        public void GetTreeMigKernel(ref double ras1, ref double ras2, ref double ks) { ras1 = mTM_as1; ras2 = mTM_as2; ks = mTM_ks; }

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public static Species Load(SpeciesReader reader, SpeciesSet speciesSet, Model model)
        {
            Species species = new Species(speciesSet)
            {
                Active = reader.Active(),
                ID = reader.ID(),
                Name = reader.Name()
            };
            string stampFile = reader.LipFile();
            // load stamps
            species.mLIPs.Load(model.GlobalSettings.Path(stampFile, "lip"));
            // attach writer stamps to reader stamps
            species.mLIPs.AttachReaderStamps(species.SpeciesSet.ReaderStamps);
            if (model.GlobalSettings.Settings.GetBooleanParameter("debugDumpStamps", false))
            {
                Debug.WriteLine(species.mLIPs.Dump());
            }

            // general properties
            species.IsConiferous = reader.IsConiferous();
            species.IsEvergreen = reader.IsEvergreen();

            // setup allometries
            species.mFoliage_a = reader.BmFoliageA();
            species.mFoliage_b = reader.BmFoliageB();

            species.mWoody_a = reader.BmWoodyA();
            species.mWoody_b = reader.BmWoodyB();

            species.mRoot_a = reader.BmRootA();
            species.mRoot_b = reader.BmRootB();

            species.mBranch_a = reader.BmBranchA();
            species.mBranch_b = reader.BmBranchB();

            species.SpecificLeafArea = reader.SpecificLeafArea();
            species.FinerootFoliageRatio = reader.FinerootFoliageRatio();

            species.mBarkThicknessFactor = reader.BarkThickness();

            // cn-ratios
            species.CNRatioFoliage = reader.CnFoliage();
            species.CNRatioFineRoot = reader.CnFineroot();
            species.CNRatioWood = reader.CnWood();
            if (species.CNRatioFineRoot * species.CNRatioFoliage * species.CNRatioWood == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: CN ratio is 0.0", species.ID));
            }

            // turnover rates
            species.TurnoverLeaf = reader.TurnoverLeaf();
            species.TurnoverRoot = reader.TurnoverRoot();

            // hd-relations
            species.mHDlow.SetAndParse(reader.HdLow());
            species.mHDhigh.SetAndParse(reader.HdHigh());
            species.mHDlow.Linearize(model, 0.0, 100.0); // input: dbh (cm). above 100cm the formula will be directly executed
            species.mHDhigh.Linearize(model, 0.0, 100.0);

            // form/density
            species.WoodDensity = reader.WoodDensity();
            species.mFormFactor = reader.FormFactor();
            // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h
            species.VolumeFactor = species.mFormFactor * Constant.QuarterPi;

            // snags
            species.SnagKsw = reader.SnagKsw(); // decay rate of SWD
            species.SnagHalflife = reader.SnagHalflife();
            species.SnagKyl = reader.SnagKyl(); // decay rate labile
            species.SnagKyr = reader.SnagKyr(); // decay rate refractory matter

            if ((species.mFoliage_a == 0.0) ||
                (species.mFoliage_b == 0.0) ||
                (species.mRoot_a == 0.0) ||
                (species.mRoot_b == 0.0) ||
                (species.mWoody_a == 0.0) ||
                (species.mWoody_b == 0.0) ||
                (species.mBranch_a == 0.0) ||
                (species.mBranch_b == 0.0) ||
                (species.WoodDensity == 0.0) ||
                (species.mFormFactor == 0.0) ||
                (species.SpecificLeafArea == 0.0) ||
                (species.FinerootFoliageRatio == 0.0))
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: one value is NULL in database.", species.ID));
            }
            // Aging
            species.mMaximumAge = reader.MaximumAge();
            species.mMaximumHeight = reader.MaximumHeight();
            species.mAging.SetAndParse(reader.Aging());
            species.mAging.Linearize(model, 0.0, 1.0); // input is harmonic mean of relative age and relative height
            if (species.mMaximumAge * species.mMaximumHeight == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}:invalid aging parameters.", species.ID));
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. see wiki: base+mortality
            double p_lucky = reader.ProbIntrinsic();
            double p_lucky_stress = reader.ProbStress();

            if (p_lucky * species.mMaximumAge * p_lucky_stress == 0.0)
            {
                throw new NotSupportedException(String.Format("Error setting up species {0}: invalid mortality parameters.", species.ID));
            }

            species.DeathProbabilityIntrinsic = 1.0 - Math.Pow(p_lucky, 1.0 / species.mMaximumAge);
            species.mDeathProb_stress = p_lucky_stress;

            if (model.GlobalSettings.LogInfo())
            {
                Debug.WriteLine("species " + species.Name + " probStress " + p_lucky_stress + " resulting probability: " + species.mDeathProb_stress);
            }

            // envirionmental responses
            species.mRespVpdExponent = reader.RespVpdExponent();
            species.mRespTempMin = reader.RespTempMin();
            species.mRespTempMax = reader.RespTempMax();
            if (species.mRespVpdExponent >= 0)
            {
                throw new NotSupportedException(String.Format("Error: vpd exponent >=0 for species (must be a negative value).", species.ID));
            }
            if (species.mRespTempMax == 0.0 || species.mRespTempMin >= species.mRespTempMax)
            {
                throw new NotSupportedException(String.Format("temperature response parameters invalid for species", species.ID));
            }

            species.mRespNitrogenClass = reader.RespNitrogenClass();
            if (species.mRespNitrogenClass < 1 || species.mRespNitrogenClass > 3)
            {
                throw new NotSupportedException(String.Format("nitrogen class invalid (must be >=1 and <=3) for species", species.ID));
            }

            // phenology
            species.PhenologyClass = reader.PhenologyClass();

            // water
            species.MaxCanopyConductance = reader.MaxCanopyConductance();
            species.PsiMin = reader.PsiMin();

            // light
            species.mLightResponseClass = reader.LightResponseClass();
            if (species.mLightResponseClass < 1.0 || species.mLightResponseClass > 5.0)
            {
                throw new NotSupportedException(String.Format("invalid light response class for species {0}. Allowed: 1..5.", species.ID));
            }

            // regeneration
            int seed_year_interval = reader.SeedYearInterval();
            if (seed_year_interval == 0)
            {
                throw new NotSupportedException(String.Format("seedYearInterval = 0 for {0}", species.ID));
            }
            species.mSeedYearProbability = 1 / (double)(seed_year_interval);
            species.mMaturityYears = reader.MaturityYears();
            species.mTM_as1 = reader.SeedKernelAs1();
            species.mTM_as2 = reader.SeedKernelAs2();
            species.mTM_ks = reader.SeedKernelKs0();
            species.FecundityM2 = reader.FecundityM2();
            species.NonSeedYearFraction = reader.NonSeedYearFraction();
            // special case for serotinous trees (US)
            species.mSerotiny.SetExpression(reader.SerotinyFormula());
            species.FecunditySerotiny = reader.FecunditySerotiny();

            // establishment parameters
            species.EstablishmentParameters.MinTemp = reader.EstablishmentParametersMinTemp();
            species.EstablishmentParameters.ChillRequirement = reader.EstablishmentParametersChillRequirement();
            species.EstablishmentParameters.GddMin = reader.EstablishmentParametersGddMin();
            species.EstablishmentParameters.GddMax = reader.EstablishmentParametersGddMax();
            species.EstablishmentParameters.GddBaseTemperature = reader.EstablishmentParametersGddBaseTemperature();
            species.EstablishmentParameters.GddBudBurst = reader.EstablishmentParametersGddBudBurst();
            species.EstablishmentParameters.MinFrostFree = reader.EstablishmentParametersMinFrostFree();
            species.EstablishmentParameters.FrostTolerance = reader.EstablishmentParametersFrostTolerance();
            species.EstablishmentParameters.PsiMin = reader.EstablishmentParametersPsiMin();

            // sapling and sapling growth parameters
            species.SaplingGrowthParameters.HeightGrowthPotential.SetAndParse(reader.SaplingGrowthParametersHeightGrowthPotential());
            species.SaplingGrowthParameters.HeightGrowthPotential.Linearize(model, 0.0, 4.0);
            species.SaplingGrowthParameters.HdSapling = reader.SaplingGrowthParametersHdSapling();
            species.SaplingGrowthParameters.StressThreshold = reader.SaplingGrowthParametersStressThreshold();
            species.SaplingGrowthParameters.MaxStressYears = reader.SaplingGrowthParametersMaxStressYears();
            species.SaplingGrowthParameters.ReferenceRatio = reader.SaplingGrowthParametersReferenceRatio();
            species.SaplingGrowthParameters.ReinekesR = reader.SaplingGrowthParametersReinekesR();
            species.SaplingGrowthParameters.BrowsingProbability = reader.SaplingGrowthParametersBrowsingProbability();
            species.SaplingGrowthParameters.SproutGrowth = reader.SaplingGrowthParametersSproutGrowth();
            if (species.SaplingGrowthParameters.SproutGrowth > 0.0)
            {
                if (species.SaplingGrowthParameters.SproutGrowth < 1.0 || species.SaplingGrowthParameters.SproutGrowth > 10)
                {
                    Debug.WriteLine("Value of 'sapSproutGrowth' dubious for species " + species.Name + "(value: " + species.SaplingGrowthParameters.SproutGrowth + ")");
                }
            }
            species.SaplingGrowthParameters.SetupReinekeLookup();

            return species;
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
        public double Aging(Model model, float height, int age)
        {
            double rel_height = Math.Min(height / mMaximumHeight, 0.999999); // 0.999999 -> avoid div/0
            double rel_age = Math.Min(age / mMaximumAge, 0.999999);

            // harmonic mean: http://en.wikipedia.org/wiki/Harmonic_mean
            double x = 1.0 - 2.0 / (1.0 / (1.0 - rel_height) + 1.0 / (1.0 - rel_age)); // Note:

            double aging_factor = mAging.Calculate(model, x);

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
        public void SeedProduction(Model model, Tree tree)
        {
            if (this.SeedDispersal == null)
            {
                return; // regeneration is disabled
            }

            // if the tree is considered as serotinous (i.e. seeds need external trigger such as fire)
            if (this.IsTreeSerotinous(model, tree.Age))
            {
                return;
            }

            // no seed production if maturity age is not reached (species parameter) or if tree height is below 4m.
            if (tree.Age > mMaturityYears && tree.Height > 4.0F)
            {
                SeedDispersal.SetMatureTree(tree.LightCellPosition, tree.LeafArea);
            }
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool IsTreeSerotinous(Model model, int age)
        {
            if (mSerotiny.IsEmpty)
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            double pSerotinous = mSerotiny.Calculate(model, age);
            if (model.RandomGenerator.Random() < pSerotinous)
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
        public void NewYear(Model model)
        {
            if (SeedDispersal != null)
            {
                // decide whether current year is a seed year
                IsSeedYear = (model.RandomGenerator.Random() < mSeedYearProbability);
                if (IsSeedYear && model.GlobalSettings.LogDebug())
                {
                    Debug.WriteLine("species " + ID + " has a seed year.");
                }
                // clear seed map
                SeedDispersal.Clear(model.GlobalSettings);
            }
        }

        public void GetHeightDiameterRatioLimits(Model model, double dbh, out double rLowHD, out double rHighHD)
        {
            rLowHD = mHDlow.Calculate(model, dbh);
            rHighHD = mHDhigh.Calculate(model, dbh);
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
        public double SoilWaterResponse(double psi_kPa)
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
