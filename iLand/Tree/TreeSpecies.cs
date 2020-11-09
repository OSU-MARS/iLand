using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** @class Species
      The behavior and general properties of tree species.
      Because the individual trees are designed as leightweight as possible, lots of stuff is done by the Species.
      Inter alia, Species do:
      - store all the precalcualted patterns for light competition (LIP, stamps)
      - do most of the growth (3PG) calculation
      */
    public class TreeSpecies
    {
        private readonly TreeSpeciesStamps mLightIntensityProfiles; // ptr to the container of the LIP-pattern

        // biomass allometries:
        private float mFoliageA, mFoliageB;  // allometry (biomass = a * dbh^b) for foliage
        private float mWoodyA, mWoodyB; // allometry (biomass = a * dbh^b) for woody compartments aboveground
        private float mRootA, mRootB; // allometry (biomass = a * dbh^b) for roots (compound, fine and coarse roots as one pool)
        private float mBranchA, mBranchB; // allometry (biomass = a * dbh^b) for branches
        // cn-ratios
        private float mBarkThicknessFactor; // multiplier to estimate bark thickness (cm) from dbh

        // height-diameter-relationships
        private readonly Expression mHDlow; // minimum HD-relation as f(d) (open grown tree)
        private readonly Expression mHDhigh; // maximum HD-relation as f(d)
        // stem density and taper
        private float mFormFactor; // taper form factor of the stem [-] used for volume / stem-mass calculation calculation
        // mortality
        private float mStressMortalityCoefficient; // max. prob. of death per year when tree suffering maximum stress
        // Aging
        private float mMaximumAge; // maximum age of species (years)
        private float mMaximumHeight; // maximum height of species (m) for aging
        private readonly Expression mAging;
        // environmental responses
        private float mRespVpdExponent; // exponent in vpd response calculation (Mkela 2008)
        private float mRespTempMin; // temperature response calculation offset
        private float mRespTempMax; // temperature response calculation: saturation point for temp. response
        private float mRespNitrogenClass; // nitrogen response class (1..3). fractional values (e.g. 1.2) are interpolated.
        private float mLightResponseClass; // light response class (1..5) (1=shade intolerant)
        // regeneration
        private int mMaturityYears; // a tree produces seeds if it is older than this parameter
        private double mSeedYearProbability; // probability that a year is a seed year (=1/avg.timespan between seedyears)
        // regeneration - seed dispersal
        private float mTM_as1; // seed dispersal parameters (treemig)
        private float mTM_as2; // seed dispersal parameters (treemig)
        private float mTM_ks; // seed dispersal parameters (treemig)
        private readonly Expression mSerotiny; // function that decides (probabilistic) if a tree is serotinous; empty: serotiny not active

        // properties
        /// @property id 4-character unique identification of the tree species
        public string ID { get; private set; }
        /// the full name (e.g. Picea abies) of the species
        public string Name { get; private set; }
        public int Index { get; private set; } // unique index of species within current set
        public int PhenologyClass { get; private set; } // phenology class defined in project file. class 0 = evergreen
        public bool IsConiferous { get; private set; }
        public bool IsEvergreen { get; private set; }
        public bool IsSeedYear { get; private set; } // TODO; IsMastYear?
        // cn ratios
        public float CNRatioFoliage { get; private set; }
        public float CNRatioFineRoot { get; private set; }
        public float CNRatioWood { get; private set; }
        // turnover rates
        public float TurnoverLeaf { get; private set; } // yearly turnover rate leafs
        public float TurnoverRoot { get; private set; } // yearly turnover rate root

        // mortality
        public double DeathProbabilityFixed { get; private set; } // prob. of intrinsic death per year [0..1]
        public float FecundityM2 { get; private set; } // "surviving seeds" (cf. Moles et al) per m2, see also http://iland.boku.ac.at/fecundity
        public float FecunditySerotiny { get; private set; } // multiplier that increases fecundity for post-fire seed rain of serotinous species
        public float MaxCanopyConductance { get; private set; } // maximum canopy conductance in m/s
        public float NonSeedYearFraction { get; private set; }
        public float PsiMin { get; private set; }

        // snags
        public float SnagKsw { get; private set; } // standing woody debris (swd) decomposition rate
        public float SnagHalflife { get; private set; } // half-life-period of standing snags (years)
        public float SnagKyl { get; private set; } // decomposition rate for labile matter (litter) used in soil model
        public float SnagKyr { get; private set; } // decomposition rate for refractory matter (woody) used in soil model

        // growth
        public float SpecificLeafArea { get; private set; } // m²/kg; conversion factor from kg OTS to leaf area m²
        public float VolumeFactor { get; private set; } // factor for volume calculation: V = factor * D^2*H (incorporates density and the form of the bole)
        public float WoodDensity { get; private set; } // density of stem wood [kg/m3]

        public float FinerootFoliageRatio { get; private set; } // ratio of fineroot mass (kg) to foliage mass (kg)
        public EstablishmentParameters EstablishmentParameters { get; private set; }
        public SaplingGrowthParameters SaplingGrowthParameters { get; private set; }
        public SeedDispersal SeedDispersal { get; set; }
        public TreeSpeciesSet SpeciesSet { get; private set; }

        public TreeSpecies(TreeSpeciesSet set)
        {
            if (set == null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            this.mAging = new Expression();
            this.EstablishmentParameters = new EstablishmentParameters();
            this.Index = set.SpeciesCount();
            this.mLightIntensityProfiles = new TreeSpeciesStamps();
            this.mHDhigh = new Expression();
            this.mHDlow = new Expression();
            this.SaplingGrowthParameters = new SaplingGrowthParameters();
            this.SeedDispersal = null;
            this.mSerotiny = new Expression();
            this.SpeciesSet = set;
        }

        public bool Active { get; private set; }

        // allometries
        public float GetBarkThickness(float dbh) { return dbh * mBarkThicknessFactor; }
        public float GetBiomassFoliage(float dbh) { return mFoliageA * MathF.Pow(dbh, mFoliageB); }
        public float GetBiomassWoody(float dbh) { return mWoodyA * MathF.Pow(dbh, mWoodyB); }
        public float GetBiomassRoot(float dbh) { return mRootA * MathF.Pow(dbh, mRootB); }
        public float GetBiomassBranch(float dbh) { return mBranchA * MathF.Pow(dbh, mBranchB); }
        public float GetWoodFoliageRatio() { return mWoodyB / mFoliageB; } // TODO: why are only exponent powers considered?

        public LightStamp GetStamp(float dbh, float height) { return mLightIntensityProfiles.GetStamp(dbh, height); }

        public float GetLightResponse(float lightResourceIndex) 
        { 
            return this.SpeciesSet.GetLightResponse(lightResourceIndex, mLightResponseClass); 
        }

        public float GetNitrogenResponse(float availableNitrogen) 
        {
            return this.SpeciesSet.GetNitrogenResponse(availableNitrogen, mRespNitrogenClass); 
        }

        // parameters for seed dispersal
        public void GetTreeMigKernel(out float as1, out float as2, out float ks) 
        { 
            as1 = mTM_as1; 
            as2 = mTM_as2; 
            ks = mTM_ks; 
        }

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public static TreeSpecies Load(Project projectFile, SpeciesReader reader, TreeSpeciesSet speciesSet)
        {
            TreeSpecies species = new TreeSpecies(speciesSet)
            {
                Active = reader.Active(),
                ID = reader.ID(),
                Name = reader.Name()
            };
            string stampFile = reader.LipFile();
            // load stamps
            species.mLightIntensityProfiles.Load(projectFile.GetFilePath(ProjectDirectory.LightIntensityProfile, stampFile));
            // attach writer stamps to reader stamps
            species.mLightIntensityProfiles.AttachReaderStamps(species.SpeciesSet.ReaderStamps);
            if (projectFile.Model.Parameter.DebugDumpStamps)
            {
                Debug.WriteLine(species.mLightIntensityProfiles.Dump());
            }

            // general properties
            species.IsConiferous = reader.IsConiferous();
            species.IsEvergreen = reader.IsEvergreen();

            // setup allometries
            species.mFoliageA = reader.BmFoliageA();
            species.mFoliageB = reader.BmFoliageB();

            species.mWoodyA = reader.BmWoodyA();
            species.mWoodyB = reader.BmWoodyB();

            species.mRootA = reader.BmRootA();
            species.mRootB = reader.BmRootB();

            species.mBranchA = reader.BmBranchA();
            species.mBranchB = reader.BmBranchB();

            species.SpecificLeafArea = reader.SpecificLeafArea();
            species.FinerootFoliageRatio = reader.FinerootFoliageRatio();

            species.mBarkThicknessFactor = reader.BarkThickness();

            // cn-ratios
            species.CNRatioFoliage = reader.CnFoliage();
            species.CNRatioFineRoot = reader.CnFineroot();
            species.CNRatioWood = reader.CnWood();
            if ((species.CNRatioFineRoot <= 0.0F) || (species.CNRatioFineRoot > 1000.0F) ||
                (species.CNRatioFoliage <= 0.0F) || (species.CNRatioFoliage > 1000.0F) ||
                (species.CNRatioWood <= 0.0F) || (species.CNRatioFoliage > 1000.0F))
            {
                throw new SqliteException("Error reading " + species.ID + ": at least one carbon-nitrogen ratio is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // turnover rates
            species.TurnoverLeaf = reader.TurnoverLeaf();
            species.TurnoverRoot = reader.TurnoverRoot();

            // hd-relations
            species.mHDlow.SetAndParse(reader.HdLow());
            species.mHDhigh.SetAndParse(reader.HdHigh());
            if (projectFile.System.Settings.ExpressionLinearizationEnabled)
            {
                species.mHDlow.Linearize(0.0, 100.0); // input: dbh (cm). above 100cm the formula will be directly executed
                species.mHDhigh.Linearize(0.0, 100.0);
            }

            // form/density
            species.WoodDensity = reader.WoodDensity();
            species.mFormFactor = reader.FormFactor();
            // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h
            species.VolumeFactor = Constant.QuarterPi * species.mFormFactor;

            // snags
            species.SnagKsw = reader.SnagKsw(); // decay rate of SWD
            species.SnagHalflife = reader.SnagHalflife();
            species.SnagKyl = reader.SnagKyl(); // decay rate labile
            species.SnagKyr = reader.SnagKyr(); // decay rate refractory matter

            if ((species.mFoliageA <= 0.0F) || (species.mFoliageA > 10.0F) ||
                (species.mFoliageB <= 0.0F) || (species.mFoliageB > 10.0F) ||
                (species.mRootA <= 0.0F) || (species.mRootA > 10.0F) ||
                (species.mRootB <= 0.0F) || (species.mRootB > 10.0F) ||
                (species.mWoodyA <= 0.0F) || (species.mWoodyA > 10.0F) ||
                (species.mWoodyB <= 0.0F) || (species.mWoodyB > 10.0F) ||
                (species.mBranchA <= 0.0F) || (species.mBranchA > 10.0F) ||
                (species.mBranchB <= 0.0F) || (species.mBranchB > 10.0F) ||
                (species.WoodDensity <= 50.0F) || (species.WoodDensity > 2000.0F) || // balsa 100-250 kg/m³, black ironwood 1355 kg/m³
                (species.mFormFactor <= 0.0F) || (species.mFormFactor > 1.0F) || // 0 = disc, 1 = cylinder
                (species.SpecificLeafArea <= 0.0F) || (species.SpecificLeafArea > 300.0F) || // nominal upper bound from mosses
                (species.FinerootFoliageRatio <= 0.0F))
            {
                throw new SqliteException("Error loading " + species.ID + ": at least one biomass parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }
            // Aging
            species.mMaximumAge = reader.MaximumAge();
            species.mMaximumHeight = reader.MaximumHeight();
            species.mAging.SetAndParse(reader.Aging());
            if (projectFile.System.Settings.ExpressionLinearizationEnabled)
            {
                species.mAging.Linearize(0.0, 1.0); // input is harmonic mean of relative age and relative height
            }
            if ((species.mMaximumAge <= 0.0F) || (species.mMaximumAge > 1000.0F * 1000.0F) ||
                (species.mMaximumHeight <= 0.0) || (species.mMaximumHeight > 200.0)) // Sequoia semperivirens (Hyperion) 115.7 m
            {
                throw new SqliteException("Error loading " + species.ID + ": at least one aging parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. see wiki: base+mortality
            float fixedMortalityBase = reader.ProbIntrinsic();
            float stressMortalityCoefficient = reader.ProbStress();
            if ((fixedMortalityBase < 0.0F) || (stressMortalityCoefficient < 0.0F) || (stressMortalityCoefficient > 1000.0F)) // sanity upper bound
            {
                throw new SqliteException("Error loading " + species.ID + ": invalid mortality parameters.", (int)SqliteErrorCode.Error);
            }

            // TODO: probability of senescence as a function of age
            species.DeathProbabilityFixed = 1.0F - MathF.Pow(fixedMortalityBase, 1.0F / species.mMaximumAge);
            species.mStressMortalityCoefficient = stressMortalityCoefficient;

            // envirionmental responses
            species.mRespVpdExponent = reader.RespVpdExponent();
            species.mRespTempMin = reader.RespTempMin();
            species.mRespTempMax = reader.RespTempMax();
            if (species.mRespVpdExponent >= 0.0F)
            {
                throw new SqliteException("Error loading " + species.ID + ": VPD exponent greater than or equal to zero.", (int)SqliteErrorCode.Error);
            }
            if (species.mRespTempMax <= 0.0F || species.mRespTempMin >= species.mRespTempMax)
            {
                throw new SqliteException("Error loading " + species.ID + ": invalid temperature response parameters.", (int)SqliteErrorCode.Error);
            }

            species.mRespNitrogenClass = reader.RespNitrogenClass();
            if (species.mRespNitrogenClass < 1.0F || species.mRespNitrogenClass > 3.0F)
            {
                throw new SqliteException("Error loading " + species.ID + ": nitrogen response class must be in range [1.0 3.0].", (int)SqliteErrorCode.Error);
            }

            // phenology
            species.PhenologyClass = reader.PhenologyClass();

            // water
            species.MaxCanopyConductance = reader.MaxCanopyConductance();
            species.PsiMin = reader.PsiMin();

            // light
            species.mLightResponseClass = reader.LightResponseClass();
            if (species.mLightResponseClass < 1.0F || species.mLightResponseClass > 5.0F)
            {
                throw new SqliteException("Error loading " + species.ID + ": light response class must be in range [1.0 5.0].", (int)SqliteErrorCode.Error);
            }

            // regeneration
            // TODO: validation
            int seedYearInterval = reader.SeedYearInterval();
            if (seedYearInterval < 1)
            {
                throw new SqliteException("Error loading " + species.ID + ": seed year interval must be positive.", (int)SqliteErrorCode.Error);
            }
            species.mSeedYearProbability = 1.0 / seedYearInterval;
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
            species.SaplingGrowthParameters.HeightDiameterRatio = reader.SaplingGrowthParametersHdSapling();
            species.SaplingGrowthParameters.StressThreshold = reader.SaplingGrowthParametersStressThreshold();
            species.SaplingGrowthParameters.MaxStressYears = reader.SaplingGrowthParametersMaxStressYears();
            species.SaplingGrowthParameters.ReferenceRatio = reader.SaplingGrowthParametersReferenceRatio();
            species.SaplingGrowthParameters.ReinekeR = reader.SaplingGrowthParametersReinekesR();
            species.SaplingGrowthParameters.BrowsingProbability = reader.SaplingGrowthParametersBrowsingProbability();
            species.SaplingGrowthParameters.SproutGrowth = reader.SaplingGrowthParametersSproutGrowth();
            if (species.SaplingGrowthParameters.SproutGrowth > 0.0F)
            {
                if (species.SaplingGrowthParameters.SproutGrowth < 1.0F || species.SaplingGrowthParameters.SproutGrowth > 10.0F)
                {
                    // TODO: convert to error?
                    Debug.WriteLine("Value of 'sapSproutGrowth' dubious for species " + species.Name + "(value: " + species.SaplingGrowthParameters.SproutGrowth + ")");
                }
            }
            species.SaplingGrowthParameters.SetupReinekeLookup();
            if (projectFile.System.Settings.ExpressionLinearizationEnabled)
            {
                species.SaplingGrowthParameters.HeightGrowthPotential.Linearize(0.0, Constant.Sapling.MaximumHeight);
            }
            return species;
        }

        /** calculate fraction of stem wood increment base on dbh.
            allometric equation: a*d^b -> first derivation: a*b*d^(b-1)
            the ratio for stem is 1 minus the ratio of twigs to total woody increment at current "dbh". */
        public float GetStemFraction(float dbh)
        {
            float stemFraction = 1.0F - mBranchA * mBranchB * MathF.Pow(dbh, mBranchB - 1.0F) / (mWoodyA * mWoodyB * MathF.Pow(dbh, mWoodyB - 1.0F));
            return stemFraction;
        }

        /** Aging formula.
           calculates a relative "age" by combining a height- and an age-related term using a harmonic mean,
           and feeding this into the Landsberg and Waring formula.
           see http://iland.boku.ac.at/primary+production#respiration_and_aging
           @param useAge set to true if "real" tree age is available. If false, only the tree height is used.
          */
        public float GetAgingFactor(float height, int age)
        {
            Debug.Assert(height > 0.0F);
            Debug.Assert(age > 1);

            float relativeHeight = MathF.Min(height / mMaximumHeight, 0.999999F); // 0.999999 -> avoid div/0
            float relativeAge = MathF.Min(age / mMaximumAge, 0.999999F);

            // harmonic mean: http://en.wikipedia.org/wiki/Harmonic_mean
            float x = 1.0F - 2.0F / (1.0F / (1.0F - relativeHeight) + 1.0F / (1.0F - relativeAge)); // Note:

            float agingFactor = (float)mAging.Evaluate(x);

            return Maths.Limit(agingFactor, 0.0F, 1.0F);
        }

        public int EstimateAgeFromHeight(float height)
        {
            int age = (int)(this.mMaximumAge * height / this.mMaximumHeight);
            return age;
        }

        /** Seed production.
           This function produces seeds if the tree is older than a species-specific age ("maturity")
           If seeds are produced, this information is stored in a "SeedMap"
          */
        /// check the maturity of the tree and flag the position as seed source appropriately
        public void DisperseSeeds(RandomGenerator randomGenerator, Trees tree, int treeIndex)
        {
            if (this.SeedDispersal == null)
            {
                return; // regeneration is disabled
            }

            // if the tree is considered as serotinous (i.e. seeds need external trigger such as fire)
            if (this.IsTreeSerotinousRandom(randomGenerator, tree.Age[treeIndex]))
            {
                return;
            }

            // no seed production if maturity age is not reached (species parameter) or if tree height is below 4m.
            if (tree.Age[treeIndex] > mMaturityYears && tree.Height[treeIndex] > 4.0F)
            {
                this.SeedDispersal.SetMatureTree(tree.LightCellPosition[treeIndex], tree.LeafArea[treeIndex]);
            }
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool IsTreeSerotinousRandom(RandomGenerator randomGenerator, int age)
        {
            if (mSerotiny.IsEmpty)
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            double pSerotinous = mSerotiny.Evaluate(age);
            return randomGenerator.GetRandomDouble() < pSerotinous;
        }

        /** newYear is called by the SpeciesSet at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void OnStartYear(Model model)
        {
            if (this.SeedDispersal != null)
            {
                // decide whether current year is a seed year
                // TODO: link to weather conditions and time since last seed year/
                this.IsSeedYear = (model.RandomGenerator.GetRandomDouble() < mSeedYearProbability);
                if (this.IsSeedYear && (model.Project.System.Settings.LogLevel <= EventLevel.Informational))
                {
                    Trace.TraceInformation("Seed year for " + this.ID + ".");
                }
                // clear seed map
                this.SeedDispersal.Clear(model);
            }
        }

        public void GetHeightDiameterRatioLimits(float dbh, out float hdRatioLowerBound, out float hdRatioUpperBound)
        {
            hdRatioLowerBound = (float)mHDlow.Evaluate(dbh);
            hdRatioUpperBound = (float)mHDhigh.Evaluate(dbh);
        }

        /** vpdResponse calculates response on vpd.
            Input: vpd [kPa]*/
        public float GetVpdResponse(float vpd)
        {
            return MathF.Exp(this.mRespVpdExponent * vpd);
        }

        /** temperatureResponse calculates response on delayed daily temperature.
            Input: average temperature [C]
            Note: slightly different from Mkela 2008: the maximum parameter (Sk) in iLand is interpreted as the absolute
                  temperature yielding a response of 1; in Mkela 2008, Sk is the width of the range (relative to the lower threhold)
            */
        public float GetTemperatureResponse(float laggedTemperature)
        {
            float response = MathF.Max(laggedTemperature - this.mRespTempMin, 0.0F);
            response = MathF.Min(response / (this.mRespTempMax - this.mRespTempMin), 1.0F);
            return response;
        }

        /** soilwaterResponse is a function of the current matrix potential of the soil.
          */
        public float GetSoilWaterResponse(float psiInKilopascals)
        {
            float psiInMPa = 0.001F * psiInKilopascals; // convert to MPa
            float response = Maths.Limit((psiInMPa - this.PsiMin) / (-0.015F - this.PsiMin), 0.0F, 1.0F);
            return response;
        }

        /** calculate probabilty of death based on the current stress index. */
        public float GetDeathProbabilityForStress(float stressIndex)
        {
            if (stressIndex <= 0.0F)
            {
                return 0.0F;
            }
            float probability = 1.0F - MathF.Exp(-mStressMortalityCoefficient * stressIndex);
            return probability;
        }
    }
}
