// C++/core/{ tree.h, tree.cpp }
using iLand.Extensions;
using iLand.Input.ProjectFile;
using iLand.Input.Tree;
using iLand.Tool;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** The behavior and general properties of tree species.
      Because the individual trees are designed as leightweight as possible, lots of stuff is done by the Species.
      Inter alia, Species do:
      - store all the precalculated patterns for light competition (LIP, stamps)
      - do most of the growth (3-PG) calculation
      */
    public class TreeSpecies
    {
        private readonly TreeSpeciesStamps lightIntensityProfiles;

        // carbon-nitrogen ratios
        private float barkFractionAtDbh; // multiplier to estimate bark thickness (cm) from dbh
        // biomass allometries
        private float branchA, branchB; // allometry (biomass = a * dbh^b) for branches
        private float foliageA, foliageB;  // allometry (biomass = a * dbh^b) for foliage
        private float rootA, rootB; // allometry (biomass = a * dbh^b) for roots (compound, fine and coarse roots as one pool)
        private float stemA, stemB; // allometry (biomass = a * dbh^b) for woody compartments aboveground

        // height-diameter-relationships
        private readonly ExpressionHeightDiameterRatioBounded heightDiameterRatioLowerBound; // minimum HD-relation as f(d) (open grown tree)
        private readonly ExpressionHeightDiameterRatioBounded heightDiameterRatioUpperBound; // maximum HD-relation as f(d)
        // mortality
        private float stressMortalityCoefficient; // max. prob. of death per year when tree suffering maximum stress
        // aging
        private readonly ExpressionAging aging;
        private float maximumAgeInYears; // maximum age of species (years)
        private float maximumHeightInM; // maximum height of species (m) for aging
        // environmental responses
        private float modifierTempMin; // temperature response calculation offset
        private float modifierTempMax; // temperature response calculation: saturation point for temp. response
        private float nitrogenResponseClass; // nitrogen response class (1..3). fractional values (e.g. 1.2) are interpolated.
        // regeneration
        private float mastYearProbability; // probability that a year is a seed year (=1/avg. timespan between seed years)
        // regeneration - seed dispersal
        private readonly Expression serotinyFormula; // function that decides (probabilistic) if a tree is serotinous; empty: serotiny not active
        private float treeMigAlphaS1; // seed dispersal parameters (TreeMig)
        private float treeMigAlphaS2; // seed dispersal parameters (TreeMig)
        private float treeMigKappaS; // seed dispersal parameters (TreeMig)

        public bool Active { get; private init; }

        public TreeListBiometric EmptyTreeList { get; private init; }
        public int Index { get; private init; } // unique index of species within current species set
        public bool IsConiferous { get; private set; }
        public bool IsEvergreen { get; private set; }
        public bool IsMastYear { get; private set; }
        public int LeafPhenologyID { get; private set; } // leaf phenology defined in project file or Constant.EvergreenLeafPhenologyID, C++ phenologyClass(), mPhenologyClass
        public float LightResponseClass { get; private set; } // light response class (1..5) (1=shade intolerant)
        /// the full name (e.g. Picea abies) of the species
        public string Name { get; private init; }
        public WorldFloraID WorldFloraID { get; private init; }

        // carbon:nitrogen ratios
        public float CarbonNitrogenRatioFoliage { get; private set; }
        public float CarbonNitrogenRatioFineRoot { get; private set; }
        public float CarbonNitrogenRatioWood { get; private set; }
        // turnover rates
        public float TurnoverLeaf { get; private set; } // yearly turnover rate of leaves
        public float TurnoverFineRoot { get; private set; } // yearly turnover rate of roots

        // mortality
        public float DeathProbabilityFixed { get; private set; } // prob. of intrinsic death per year [0..1]
        public float FecundityM2 { get; private set; } // "surviving seeds" (cf. Moles et al) per m2, see also https://iland-model.org/fecundity
        public float FecunditySerotiny { get; private set; } // multiplier that increases fecundity for post-fire seed rain of serotinous species
        public float MaxCanopyConductance { get; private set; } // maximum canopy conductance in m/s
        public float NonMastYearFraction { get; private set; }

        // snags
        public float SnagDecompositionRate { get; private set; } // standing woody debris (swd) decomposition rate
        public float SnagHalflife { get; private set; } // half-life-period of standing snags (years)
        public float LitterDecompositionRate { get; private set; } // decomposition rate for labile matter (litter) used in soil model
        public float CoarseWoodyDebrisDecompositionRate { get; private set; } // decomposition rate for refractory matter (woody) used in soil model

        // growth
        public float MinimumSoilWaterPotential { get; private set; } // soil water potential in MPa, https://iland-model.org/soil+water+response
        public float SpecificLeafArea { get; private set; } // m²/kg; conversion factor from kg OTS to leaf area m²
        public float VolumeFactor { get; private set; } // factor for volume calculation: V = factor * D^2*H (incorporates density and the form of the bole)
        public float WoodDensity { get; private set; } // density of stem wood [kg/m3]

        // environmental responses
        public float ModifierVpdK { get; private set; } // exponent in vpd response calculation (Mäkelä 2008), C++ mRespVpdExponent

        // regeneration
        public float FinerootFoliageRatio { get; private set; } // ratio of fineroot mass (kg) to foliage mass (kg)
        public UInt16 MinimumAgeInYearsForSeedProduction { get; private set; } // a tree produces seeds if it is older than this parameter, C++ mMaturityYears
        public SaplingEstablishmentParameters SaplingEstablishment { get; private init; }
        public SaplingGrowthParameters SaplingGrowth { get; private init; }
        public SeedDispersal? SeedDispersal { get; set; }
        public TreeSpeciesSet SpeciesSet { get; private init; }

        private TreeSpecies(TreeSpeciesSet speciesSet, WorldFloraID speciesID, string name, string stampFilePath)
        {
            this.aging = new();
            this.lightIntensityProfiles = new(stampFilePath);
            this.heightDiameterRatioUpperBound = new();
            this.heightDiameterRatioLowerBound = new();
            this.serotinyFormula = new();

            this.EmptyTreeList = new(this, 0);
            this.Index = speciesSet.Count;
            this.Name = name;
            this.SaplingEstablishment = new();
            this.SaplingGrowth = new();
            this.SeedDispersal = null;
            this.SpeciesSet = speciesSet;
            this.WorldFloraID = speciesID;

            // attach writer stamps to reader stamps
            this.lightIntensityProfiles.AttachReaderStamps(speciesSet.ReaderStamps);
        }

        // TODO: consolidate with underlying fields
        public float AllometricExponentStem { get { return this.stemB; } }
        public float AllometricExponentBranch { get { return this.branchB; } }
        public float AllometricExponentFoliage { get { return this.foliageB; } }

        /** Seed production.
           This function produces seeds if the tree is older than a species-specific age ("maturity")
           If seeds are produced, this information is stored in a "SeedMap"
          */
        /// check the maturity of the tree and flag the position as seed source appropriately
        public void DisperseSeeds(RandomGenerator randomGenerator, TreeListSpatial tree, int treeIndex) // C++: Species::seedProduction()
        {
            if (this.SeedDispersal == null)
            {
                return; // regeneration is disabled
            }

            // if the tree is considered as serotinous (i.e. seeds need external trigger such as fire)
            if (this.IsTreeSerotinousRandom(randomGenerator, tree.AgeInYears[treeIndex]))
            {
                return;
            }

            // no seed production if maturity age is not reached (species parameter) or if tree height is below 4m.
            if (tree.AgeInYears[treeIndex] > this.MinimumAgeInYearsForSeedProduction)
            {
                this.SeedDispersal.SetMatureTree(tree.LightCellIndexXY[treeIndex], tree.LeafAreaInM2[treeIndex]);
            }
        }

        public UInt16 EstimateAgeFromHeight(float height)
        {
            float ageInYears = this.maximumAgeInYears * height / this.maximumHeightInM;
            return (UInt16)ageInYears;
        }

        /** Aging formula.
           calculates a relative "age" by combining a height- and an age-related term using a harmonic mean,
           and feeding this into the Landsberg and Waring formula.
           see https://iland-model.org/primary+production#respiration_and_aging
           @param useAge set to true if "real" tree age is available. If false, only the tree height is used.
          */
        public float GetAgingFactor(float height, int age)
        {
            Debug.Assert(height > 0.0F);
            Debug.Assert(age > 1);

            float relativeHeight = MathF.Min(height / maximumHeightInM, 0.999999F); // 0.999999 -> avoid div/0
            float relativeAge = MathF.Min(age / maximumAgeInYears, 0.999999F);

            // harmonic mean: http://en.wikipedia.org/wiki/Harmonic_mean
            float x = 1.0F - 2.0F / (1.0F / (1.0F - relativeHeight) + 1.0F / (1.0F - relativeAge));

            float agingFactor = aging.Evaluate(x);

            return Maths.Limit(agingFactor, 0.0F, 1.0F);
        }

        // calculations: allometries for the tree compartments (stem, branches, foliage, fineroots, coarse roots)
        public float GetBarkThickness(float dbhInCm) 
        { 
            return this.barkFractionAtDbh * dbhInCm;
        }

        public float GetBiomassBranch(float dbhInCm)
        {
            return this.branchA * MathF.Pow(dbhInCm, this.branchB);
        }

        public float GetBiomassCoarseRoot(float dbhInCm)
        {
            return this.rootA * MathF.Pow(dbhInCm, this.rootB);
        }

        public float GetBiomassFoliage(float dbhInCm)
        { 
            return this.foliageA * MathF.Pow(dbhInCm, this.foliageB); 
        }

        public float GetBiomassStem(float dbhInCm) 
        { 
            return this.stemA * MathF.Pow(dbhInCm, this.stemB); 
        }

        public (float hdRatioLowerBound, float hdRatioUpperBound) GetHeightDiameterRatioLimits(float dbhInCm)
        {
            float hdRatioLowerBound = heightDiameterRatioLowerBound.Evaluate(dbhInCm);
            float hdRatioUpperBound = heightDiameterRatioUpperBound.Evaluate(dbhInCm);
            return (hdRatioLowerBound, hdRatioUpperBound);
        }

        public float GetLightResponse(float lightResourceIndex)
        {
            return this.SpeciesSet.GetLightResponse(lightResourceIndex, this.LightResponseClass);
        }

        // calculate probabilty of death based on the current stress index
        public float GetMortalityProbability(float stressIndex)
        {
            if (stressIndex <= 0.0F)
            {
                return 0.0F;
            }
            float probability = 1.0F - MathF.Exp(-this.stressMortalityCoefficient * stressIndex);
            return probability;
        }

        public float GetNitrogenModifier(float availableNitrogen)
        {
            return this.SpeciesSet.GetNitrogenModifier(availableNitrogen, this.nitrogenResponseClass);
        }

        // iLand specific model chosen from Hanson 2004: https://iland-model.org/soil+water+response
        public float GetSoilWaterModifier(float psiInKilopascals)
        {
            float psiInMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterModifier = Maths.Limit((psiInMPa - this.MinimumSoilWaterPotential) / (-0.015F - this.MinimumSoilWaterPotential), 0.0F, 1.0F);
            return waterModifier;
        }

        public LightStamp GetStamp(float dbhInCm, float heightInM)
        { 
            return this.lightIntensityProfiles.GetStamp(dbhInCm, heightInM); 
        }

        public float GetStemFoliageRatio()
        {
            return this.stemB / this.foliageB; // Duursma et al. 2007 eq 20
        }

        /** calculate fraction of stem wood increment base on dbh.
            allometric equation: a*d^b -> first derivation: a*b*d^(b-1)
            the ratio for stem is 1 minus the ratio of twigs to total woody increment at current "dbh". */
        public float GetStemFraction(float dbh) // C++: Species::allometricFractionStem()
        {
            float inc_branch_per_d = this.branchA * this.branchB* MathF.Pow(dbh, this.branchB - 1.0F);
            float inc_woody_per_d = this.stemA * this.stemB * MathF.Pow(dbh, this.stemB - 1.0F);
            float stemFraction = inc_woody_per_d / (inc_branch_per_d + inc_woody_per_d);
            return stemFraction;
        }

        /** Input: average temperature, °C
            Slightly different from Mäkelä 2008 with daily weather series. The maximum parameter (Sk) in iLand is interpreted as the absolute
            temperature yielding a response of 1; in Mäkelä 2008, Sk is the width of the range (relative to the lower threshold)
            */
        public float GetTemperatureModifier(float dailyMA1orMonthlyTemperature)
        {
            float modifier = MathF.Max(dailyMA1orMonthlyTemperature - this.modifierTempMin, 0.0F);
            modifier = MathF.Min(modifier / (this.modifierTempMax - this.modifierTempMin), 1.0F);
            return modifier;
        }

        // parameters for seed dispersal from equation 6 of
        // Lischke H, Zimmermanna NE, Bolliger J, et al. 2006. TreeMig: A forest-landscape model for simulating spatio-temporal patterns from stand 
        //   to landscape scale. Ecological Modelling 199(4):409-420. https://doi.org/10.1016/j.ecolmodel.2005.11.046
        public void GetTreeMigKernel(out float alphaS1, out float alphaS2, out float kappaS) 
        { 
            alphaS1 = this.treeMigAlphaS1; 
            alphaS2 = this.treeMigAlphaS2; 
            kappaS = this.treeMigKappaS; 
        }

        public float GetVpdModifier(float vpdInKiloPascals)
        {
            return MathF.Exp(this.ModifierVpdK * vpdInKiloPascals);
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool IsTreeSerotinousRandom(RandomGenerator randomGenerator, int age)
        {
            if (this.serotinyFormula.IsEmpty)
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            float pSerotinous = this.serotinyFormula.Evaluate(age);
            return randomGenerator.GetRandomProbability() < pSerotinous;
        }

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public static TreeSpecies Load(Project projectFile, TreeSpeciesReader reader, TreeSpeciesSet speciesSet)
        {
            string stampFilePath = projectFile.GetFilePath(ProjectDirectory.LightIntensityProfile, reader.LipFile());
            TreeSpecies species = new(speciesSet, WorldFloraIDExtensions.Parse(reader.ID()), reader.Name(), stampFilePath)
            {
                Active = reader.Active(),

                // general properties
                IsConiferous = reader.IsConiferous(),
                IsEvergreen = reader.IsEvergreen(),

                // setup allometries
                foliageA = reader.BmFoliageA(),
                foliageB = reader.BmFoliageB(),

                stemA = reader.BmWoodyA(),
                stemB = reader.BmWoodyB(),

                rootA = reader.BmRootA(),
                rootB = reader.BmRootB(),

                branchA = reader.BmBranchA(),
                branchB = reader.BmBranchB(),

                SpecificLeafArea = reader.SpecificLeafArea(),
                FinerootFoliageRatio = reader.FinerootFoliageRatio(),

                barkFractionAtDbh = reader.BarkThickness(),

                // cn-ratios
                CarbonNitrogenRatioFoliage = reader.CnFoliage(),
                CarbonNitrogenRatioFineRoot = reader.CnFineroot(),
                CarbonNitrogenRatioWood = reader.CnWood()
            };

            if ((species.CarbonNitrogenRatioFineRoot <= 0.0F) || (species.CarbonNitrogenRatioFineRoot > 1000.0F) ||
                (species.CarbonNitrogenRatioFoliage <= 0.0F) || (species.CarbonNitrogenRatioFoliage > 1000.0F) ||
                (species.CarbonNitrogenRatioWood <= 0.0F) || (species.CarbonNitrogenRatioFoliage > 1000.0F))
            {
                throw new SqliteException("Error reading " + species.WorldFloraID + ": at least one carbon-nitrogen ratio is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // turnover rates
            species.TurnoverLeaf = reader.TurnoverLeaf();
            species.TurnoverFineRoot = reader.TurnoverRoot();

            // hd-relations
            species.heightDiameterRatioLowerBound.Parse(reader.HdLow());
            species.heightDiameterRatioUpperBound.Parse(reader.HdHigh());
            //if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            //{
            //    species.heightDiameterRatioLowerBound.Linearize(0.0F, 100.0F); // input: dbh (cm). above 100cm the formula will be directly executed
            //    species.heightDiameterRatioUpperBound.Linearize(0.0F, 100.0F);
            //}

            // form/density
            species.WoodDensity = reader.WoodDensity();
            if ((species.WoodDensity <= 50.0F) || (species.WoodDensity > 2000.0F)) // balsa 100-250 kg/m³, black ironwood 1355 kg/m³
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': wood density must be in the range of [50.0, 2000.0] kg/m³.", (int)SqliteErrorCode.Error);
            }
            float formFactor = reader.FormFactor();
            if ((formFactor <= 0.0F) || (formFactor > 1.0F)) // 0 = disc, 1 = cylinder
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': taper form factor must be in the range (0.0, 1.0).", (int)SqliteErrorCode.Error);
            }
            species.VolumeFactor = 0.25F * MathF.PI * formFactor; // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h

            // decomposition rates
            species.CoarseWoodyDebrisDecompositionRate = reader.SnagKyr(); // decay rate refractory matter
            species.LitterDecompositionRate = reader.SnagKyl(); // decay rate labile
            species.SnagDecompositionRate = reader.SnagKsw(); // decay rate of SWD
            species.SnagHalflife = reader.SnagHalflife();

            if ((species.foliageA <= 0.0F) || (species.foliageA > 10.0F) ||
                (species.foliageB <= 0.0F) || (species.foliageB > 10.0F) ||
                (species.rootA <= 0.0F) || (species.rootA > 10.0F) ||
                (species.rootB <= 0.0F) || (species.rootB > 10.0F) ||
                (species.stemA <= 0.0F) || (species.stemA > 10.0F) ||
                (species.stemB <= 0.0F) || (species.stemB > 10.0F) ||
                (species.branchA <= 0.0F) || (species.branchA > 10.0F) ||
                (species.branchB <= 0.0F) || (species.branchB > 10.0F) ||
                (species.SpecificLeafArea <= 0.0F) || (species.SpecificLeafArea > 300.0F) || // nominal upper bound from mosses
                (species.FinerootFoliageRatio <= 0.0F))
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': at least one biomass parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // aging
            species.maximumAgeInYears = reader.MaximumAge();
            species.maximumHeightInM = reader.MaximumHeight();
            species.aging.Parse(reader.Aging());
            //if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            //{
            //    species.aging.Linearize(0.0F, 1.0F); // input is harmonic mean of relative age and relative height
            //}
            if ((species.maximumAgeInYears <= 0.0F) || (species.maximumAgeInYears > 1000.0F * 1000.0F) ||
                (species.maximumHeightInM <= 0.0) || (species.maximumHeightInM > 200.0)) // Sequoia semperivirens (Hyperion) 115.7 m
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': at least one aging parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. https://iland-model.org/base+mortality
            float fixedMortalityBase = reader.ProbIntrinsic();
            float stressMortalityCoefficient = reader.ProbStress();
            if ((fixedMortalityBase < 0.0F) || (stressMortalityCoefficient < 0.0F) || (stressMortalityCoefficient > 1000.0F)) // sanity upper bound
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': invalid mortality parameters.", (int)SqliteErrorCode.Error);
            }

            // TODO: probability of senescence as a function of age
            species.DeathProbabilityFixed = 1.0F - MathF.Pow(fixedMortalityBase, 1.0F / species.maximumAgeInYears);
            species.stressMortalityCoefficient = stressMortalityCoefficient;

            // environmental responses
            species.ModifierVpdK = reader.RespVpdExponent();
            species.modifierTempMin = reader.RespTempMin();
            species.modifierTempMax = reader.RespTempMax();
            if (species.ModifierVpdK >= 0.0F)
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': VPD exponent greater than or equal to zero.", (int)SqliteErrorCode.Error);
            }
            if (species.modifierTempMax <= 0.0F || species.modifierTempMin >= species.modifierTempMax)
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': invalid temperature response parameters.", (int)SqliteErrorCode.Error);
            }

            species.nitrogenResponseClass = reader.RespNitrogenClass();
            if (species.nitrogenResponseClass < 1.0F || species.nitrogenResponseClass > 3.0F)
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': nitrogen response class must be in range [1.0 3.0].", (int)SqliteErrorCode.Error);
            }

            // phenology
            species.LeafPhenologyID = reader.PhenologyClass();

            // water
            species.MaxCanopyConductance = reader.MaxCanopyConductance();
            species.MinimumSoilWaterPotential = reader.PsiMin();

            // light
            species.LightResponseClass = reader.LightResponseClass();
            if (species.LightResponseClass < 1.0F || species.LightResponseClass > 5.0F)
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': light response class must be in range [1.0 5.0].", (int)SqliteErrorCode.Error);
            }

            // regeneration
            // TODO: validation
            int mastYearInterval = reader.MastYearInterval();
            if (mastYearInterval < 1)
            {
                throw new SqliteException("Error loading '" + species.WorldFloraID + "': seed year interval must be positive.", (int)SqliteErrorCode.Error);
            }
            species.mastYearProbability = 1.0F / mastYearInterval;
            species.MinimumAgeInYearsForSeedProduction = (UInt16)reader.MaturityYears();
            species.treeMigAlphaS1 = reader.SeedKernelAs1();
            species.treeMigAlphaS2 = reader.SeedKernelAs2();
            species.treeMigKappaS = reader.SeedKernelKs0();
            species.FecundityM2 = reader.FecundityM2();
            species.NonMastYearFraction = reader.NonMastYearFraction();
            // special case for serotinous trees (US)
            species.serotinyFormula.SetExpression(reader.SerotinyFormula());
            species.FecunditySerotiny = reader.FecunditySerotiny();

            // establishment parameters
            species.SaplingEstablishment.ColdFatalityTemperature = reader.EstablishmentParametersMinTemp();
            species.SaplingEstablishment.ChillingDaysRequired = reader.EstablishmentParametersChillRequirement();
            species.SaplingEstablishment.MinimumGrowingDegreeDays = reader.EstablishmentParametersGrowingDegreeDaysMin();
            species.SaplingEstablishment.MaximumGrowingDegreeDays = reader.EstablishmentParametersGrowingDegreeDaysMax();
            species.SaplingEstablishment.GrowingDegreeDaysBaseTemperature = reader.EstablishmentParametersGrowingDegreeDaysBaseTemperature();
            species.SaplingEstablishment.GrowingDegreeDaysForBudburst = reader.EstablishmentParametersGrowingDegreeDaysBudBurst();
            species.SaplingEstablishment.MinimumFrostFreeDays = reader.EstablishmentParametersMinFrostFree();
            species.SaplingEstablishment.FrostTolerance = reader.EstablishmentParametersFrostTolerance();
            species.SaplingEstablishment.DroughtMortalityPsiInMPa = reader.EstablishmentParametersPsiMin();
            if (projectFile.Model.Permafrost.Enabled)
            {
                // TODO: uncomment once species parameters are updated
                // species.SaplingEstablishment.SOL_thickness = reader.EstablishmentParametersSoilOrganicLayerThicknessEffect();
                if (species.SaplingEstablishment.SOL_thickness < 0.0F)
                {
                    throw new SqliteException("Soil organic layer thickness (estSOLthickness) " + species.SaplingEstablishment.SOL_thickness + " for " + species.Name + " is negative.", (int)SqliteErrorCode.Error);
                }
            }

            // sapling and sapling growth parameters
            species.SaplingGrowth.AdultSproutProbability = reader.SaplingGrowthAdultSproutProbability();
            species.SaplingGrowth.HeightGrowthPotential.Parse(reader.SaplingGrowthParametersHeightGrowthPotential());
            species.SaplingGrowth.HeightDiameterRatio = reader.SaplingGrowthParametersHdSapling();
            species.SaplingGrowth.StressThreshold = reader.SaplingGrowthParametersStressThreshold();
            species.SaplingGrowth.MaxStressYears = reader.SaplingGrowthParametersMaxStressYears();
            species.SaplingGrowth.ReferenceRatio = reader.SaplingGrowthParametersReferenceRatio();
            species.SaplingGrowth.ReinekeR = reader.SaplingGrowthParametersReinekesR();
            species.SaplingGrowth.BrowsingProbability = reader.SaplingGrowthParametersBrowsingProbability();
            species.SaplingGrowth.SproutGrowth = reader.SaplingGrowthParametersSproutGrowth();
            if ((species.SaplingGrowth.AdultSproutProbability < 0.0F) || (species.SaplingGrowth.AdultSproutProbability > 1.0F))
            {
                throw new SqliteException("sapAdultSproutProbability " + species.SaplingGrowth.AdultSproutProbability + " for species " + species.Name + " is not in the range [0.0, 1.0].", (int)SqliteErrorCode.Error);
            }
            if ((species.SaplingGrowth.SproutGrowth < 1.0F) || (species.SaplingGrowth.SproutGrowth > 10.0F))
            {
                throw new SqliteException("sapSproutGrowth " + species.SaplingGrowth.SproutGrowth + " for species " + species.Name + " is not in the range [1.0, 10.0].", (int)SqliteErrorCode.Error);
            }
            species.SaplingGrowth.SetupReinekeLookup();

            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                species.SaplingGrowth.HeightGrowthPotential.Linearize(0.0F, Constant.RegenerationLayerHeight);
            }
            return species;
        }

        /** newYear is called by the SpeciesSet at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void OnStartYear(Model model) // C++: Species::newYear()
        {
            if (this.SeedDispersal != null)
            {
                // decide whether current year is a seed year
                // TODO: link to weather conditions and time since last seed year/
                this.IsMastYear = model.RandomGenerator.Value!.GetRandomProbability() < mastYearProbability;
                if (this.IsMastYear && (model.Project.Output.Logging.LogLevel >= EventLevel.Informational))
                {
                    Trace.TraceInformation("Seed year for " + this.WorldFloraID + ".");
                }
                // clear seed maps
                this.SeedDispersal.Clear(model);
            }
        }
    }
}
