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
    /** @class Species
      The behavior and general properties of tree species.
      Because the individual trees are designed as leightweight as possible, lots of stuff is done by the Species.
      Inter alia, Species do:
      - store all the precalcualted patterns for light competition (LIP, stamps)
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
        private float woodyA, woodyB; // allometry (biomass = a * dbh^b) for woody compartments aboveground

        // height-diameter-relationships
        private readonly Expression heightDiameterRatioLowerBound; // minimum HD-relation as f(d) (open grown tree)
        private readonly Expression heightDiameterRatioUpperBound; // maximum HD-relation as f(d)
        // mortality
        private float stressMortalityCoefficient; // max. prob. of death per year when tree suffering maximum stress
        // aging
        private readonly Expression aging;
        private float maximumAgeInYears; // maximum age of species (years)
        private float maximumHeightInM; // maximum height of species (m) for aging
        // environmental responses
        private float lightResponseClass; // light response class (1..5) (1=shade intolerant)
        private float modifierVpdK; // exponent in vpd response calculation (Mäkelä 2008)
        private float modifierTempMin; // temperature response calculation offset
        private float modifierTempMax; // temperature response calculation: saturation point for temp. response
        private float nitrogenResponseClass; // nitrogen response class (1..3). fractional values (e.g. 1.2) are interpolated.
        // regeneration
        private float mastYearProbability; // probability that a year is a seed year (=1/avg. timespan between seed years)
        private int minimumAgeInYearsForSeedProduction; // a tree produces seeds if it is older than this parameter
        // regeneration - seed dispersal
        private readonly Expression serotinyFormula; // function that decides (probabilistic) if a tree is serotinous; empty: serotiny not active
        private float treeMigAlphaS1; // seed dispersal parameters (TreeMig)
        private float treeMigAlphaS2; // seed dispersal parameters (TreeMig)
        private float treeMigKappaS; // seed dispersal parameters (TreeMig)

        // properties
        /// @property id 4-character unique identification of the tree species
        public string ID { get; private init; }
        public int Index { get; private init; } // unique index of species within current species set
        public bool IsConiferous { get; private set; }
        public bool IsEvergreen { get; private set; }
        public bool IsMastYear { get; private set; }
        public int LeafPhenologyID { get; private set; } // leaf phenology defined in project file or Constant.EvergreenLeafPhenologyID
        /// the full name (e.g. Picea abies) of the species
        public string Name { get; private init; }
        // cn ratios
        public float CNRatioFoliage { get; private set; }
        public float CNRatioFineRoot { get; private set; }
        public float CNRatioWood { get; private set; }
        // turnover rates
        public float TurnoverLeaf { get; private set; } // yearly turnover rate of leaves
        public float TurnoverFineRoot { get; private set; } // yearly turnover rate of roots

        // mortality
        public float DeathProbabilityFixed { get; private set; } // prob. of intrinsic death per year [0..1]
        public float FecundityM2 { get; private set; } // "surviving seeds" (cf. Moles et al) per m2, see also http://iland-model.org/fecundity
        public float FecunditySerotiny { get; private set; } // multiplier that increases fecundity for post-fire seed rain of serotinous species
        public float MaxCanopyConductance { get; private set; } // maximum canopy conductance in m/s
        public float NonMastYearFraction { get; private set; }

        // snags
        public float SnagDecompositionRate { get; private set; } // standing woody debris (swd) decomposition rate
        public float SnagHalflife { get; private set; } // half-life-period of standing snags (years)
        public float LitterDecompositionRate { get; private set; } // decomposition rate for labile matter (litter) used in soil model
        public float CoarseWoodyDebrisDecompositionRate { get; private set; } // decomposition rate for refractory matter (woody) used in soil model

        // growth
        public float MinimumSoilWaterPotential { get; private set; } // soil water potential in MPa, http://iland-model.org/soil+water+response
        public float SpecificLeafArea { get; private set; } // m²/kg; conversion factor from kg OTS to leaf area m²
        public float VolumeFactor { get; private set; } // factor for volume calculation: V = factor * D^2*H (incorporates density and the form of the bole)
        public float WoodDensity { get; private set; } // density of stem wood [kg/m3]

        public float FinerootFoliageRatio { get; private set; } // ratio of fineroot mass (kg) to foliage mass (kg)
        public SaplingEstablishmentParameters SaplingEstablishment { get; private init; }
        public SaplingGrowthParameters SaplingGrowth { get; private init; }
        public SeedDispersal? SeedDispersal { get; set; }
        public TreeSpeciesSet SpeciesSet { get; private init; }

        public TreeSpecies(TreeSpeciesSet speciesSet, string id, string name)
        {
            if (speciesSet == null)
            {
                throw new ArgumentNullException(nameof(speciesSet));
            }

            this.aging = new Expression();
            this.lightIntensityProfiles = new TreeSpeciesStamps();
            this.heightDiameterRatioUpperBound = new Expression();
            this.heightDiameterRatioLowerBound = new Expression();
            this.serotinyFormula = new Expression();

            this.SaplingEstablishment = new SaplingEstablishmentParameters();
            this.ID = id;
            this.Index = speciesSet.Count;
            this.Name = name;
            this.SaplingGrowth = new SaplingGrowthParameters();
            this.SeedDispersal = null;
            this.SpeciesSet = speciesSet;
        }

        public bool Active { get; private init; }

        // allometries
        public float GetBarkThickness(float dbh) 
        { 
            return this.barkFractionAtDbh * dbh;
        }

        public float GetBiomassFoliage(float dbh)
        { 
            return this.foliageA * MathF.Pow(dbh, this.foliageB); 
        }

        public float GetBiomassStem(float dbh) 
        { 
            return this.woodyA * MathF.Pow(dbh, this.woodyB); 
        }

        public float GetBiomassCoarseRoot(float dbh)
        { 
            return this.rootA * MathF.Pow(dbh, this.rootB); 
        }
        
        public float GetBiomassBranch(float dbh)
        {
            return this.branchA * MathF.Pow(dbh, this.branchB); 
        }
        
        public float GetStemFoliageRatio() 
        { 
            return this.woodyB / this.foliageB; // Duursma et al. 2007 eq 20
        }

        public LightStamp GetStamp(float dbh, float height)
        { 
            return this.lightIntensityProfiles.GetStamp(dbh, height); 
        }

        public float GetLightResponse(float lightResourceIndex) 
        { 
            return this.SpeciesSet.GetLightResponse(lightResourceIndex, this.lightResponseClass); 
        }

        public float GetNitrogenModifier(float availableNitrogen) 
        {
            return this.SpeciesSet.GetNitrogenModifier(availableNitrogen, this.nitrogenResponseClass); 
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

        /** main setup routine for tree species.
            Data is fetched from the open query (or file, ...) in the parent SpeciesSet using xyzVar() functions.
            This is called
            */
        public static TreeSpecies Load(Project projectFile, TreeSpeciesReader reader, TreeSpeciesSet speciesSet)
        {
            TreeSpecies species = new(speciesSet, reader.ID(), reader.Name())
            {
                Active = reader.Active(),
            };
            string stampFile = reader.LipFile();
            // load stamps
            species.lightIntensityProfiles.Load(projectFile.GetFilePath(ProjectDirectory.LightIntensityProfile, stampFile));
            // attach writer stamps to reader stamps
            species.lightIntensityProfiles.AttachReaderStamps(species.SpeciesSet.ReaderStamps);
            // if (projectFile.World.Debug.DumpStamps)
            // {
            //     Debug.WriteLine(species.mLightIntensityProfiles.Dump());
            // }

            // general properties
            species.IsConiferous = reader.IsConiferous();
            species.IsEvergreen = reader.IsEvergreen();

            // setup allometries
            species.foliageA = reader.BmFoliageA();
            species.foliageB = reader.BmFoliageB();

            species.woodyA = reader.BmWoodyA();
            species.woodyB = reader.BmWoodyB();

            species.rootA = reader.BmRootA();
            species.rootB = reader.BmRootB();

            species.branchA = reader.BmBranchA();
            species.branchB = reader.BmBranchB();

            species.SpecificLeafArea = reader.SpecificLeafArea();
            species.FinerootFoliageRatio = reader.FinerootFoliageRatio();

            species.barkFractionAtDbh = reader.BarkThickness();

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
            species.TurnoverFineRoot = reader.TurnoverRoot();

            // hd-relations
            species.heightDiameterRatioLowerBound.SetAndParse(reader.HdLow());
            species.heightDiameterRatioUpperBound.SetAndParse(reader.HdHigh());
            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                species.heightDiameterRatioLowerBound.Linearize(0.0, 100.0); // input: dbh (cm). above 100cm the formula will be directly executed
                species.heightDiameterRatioUpperBound.Linearize(0.0, 100.0);
            }

            // form/density
            species.WoodDensity = reader.WoodDensity();
            if ((species.WoodDensity <= 50.0F) || (species.WoodDensity > 2000.0F)) // balsa 100-250 kg/m³, black ironwood 1355 kg/m³
            {
                throw new SqliteException("Error loading '" + species.ID + "': wood density must be in the range of [50.0, 2000.0] kg/m³.", (int)SqliteErrorCode.Error);
            }
            float formFactor = reader.FormFactor();
            if ((formFactor <= 0.0F) || (formFactor > 1.0F)) // 0 = disc, 1 = cylinder
            {
                throw new SqliteException("Error loading '" + species.ID + "': taper form factor must be in the range (0.0, 1.0).", (int)SqliteErrorCode.Error);
            }
            species.VolumeFactor = Constant.QuarterPi * formFactor; // volume = formfactor*pi/4 *d^2*h -> volume = volumefactor * d^2 * h

            // decomposition rates
            species.CoarseWoodyDebrisDecompositionRate = reader.SnagKyr(); // decay rate refractory matter
            species.LitterDecompositionRate = reader.SnagKyl(); // decay rate labile
            species.SnagDecompositionRate = reader.SnagKsw(); // decay rate of SWD
            species.SnagHalflife = reader.SnagHalflife();

            if ((species.foliageA <= 0.0F) || (species.foliageA > 10.0F) ||
                (species.foliageB <= 0.0F) || (species.foliageB > 10.0F) ||
                (species.rootA <= 0.0F) || (species.rootA > 10.0F) ||
                (species.rootB <= 0.0F) || (species.rootB > 10.0F) ||
                (species.woodyA <= 0.0F) || (species.woodyA > 10.0F) ||
                (species.woodyB <= 0.0F) || (species.woodyB > 10.0F) ||
                (species.branchA <= 0.0F) || (species.branchA > 10.0F) ||
                (species.branchB <= 0.0F) || (species.branchB > 10.0F) ||
                (species.SpecificLeafArea <= 0.0F) || (species.SpecificLeafArea > 300.0F) || // nominal upper bound from mosses
                (species.FinerootFoliageRatio <= 0.0F))
            {
                throw new SqliteException("Error loading '" + species.ID + "': at least one biomass parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // aging
            species.maximumAgeInYears = reader.MaximumAge();
            species.maximumHeightInM = reader.MaximumHeight();
            species.aging.SetAndParse(reader.Aging());
            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                species.aging.Linearize(0.0, 1.0); // input is harmonic mean of relative age and relative height
            }
            if ((species.maximumAgeInYears <= 0.0F) || (species.maximumAgeInYears > 1000.0F * 1000.0F) ||
                (species.maximumHeightInM <= 0.0) || (species.maximumHeightInM > 200.0)) // Sequoia semperivirens (Hyperion) 115.7 m
            {
                throw new SqliteException("Error loading '" + species.ID + "': at least one aging parameter is zero, negative, or improbably high.", (int)SqliteErrorCode.Error);
            }

            // mortality
            // the probabilites (mDeathProb_...) are the yearly prob. of death.
            // from a population a fraction of p_lucky remains after ageMax years. see wiki: base+mortality
            float fixedMortalityBase = reader.ProbIntrinsic();
            float stressMortalityCoefficient = reader.ProbStress();
            if ((fixedMortalityBase < 0.0F) || (stressMortalityCoefficient < 0.0F) || (stressMortalityCoefficient > 1000.0F)) // sanity upper bound
            {
                throw new SqliteException("Error loading '" + species.ID + "': invalid mortality parameters.", (int)SqliteErrorCode.Error);
            }

            // TODO: probability of senescence as a function of age
            species.DeathProbabilityFixed = 1.0F - MathF.Pow(fixedMortalityBase, 1.0F / species.maximumAgeInYears);
            species.stressMortalityCoefficient = stressMortalityCoefficient;

            // envirionmental responses
            species.modifierVpdK = reader.RespVpdExponent();
            species.modifierTempMin = reader.RespTempMin();
            species.modifierTempMax = reader.RespTempMax();
            if (species.modifierVpdK >= 0.0F)
            {
                throw new SqliteException("Error loading '" + species.ID + "': VPD exponent greater than or equal to zero.", (int)SqliteErrorCode.Error);
            }
            if (species.modifierTempMax <= 0.0F || species.modifierTempMin >= species.modifierTempMax)
            {
                throw new SqliteException("Error loading '" + species.ID + "': invalid temperature response parameters.", (int)SqliteErrorCode.Error);
            }

            species.nitrogenResponseClass = reader.RespNitrogenClass();
            if (species.nitrogenResponseClass < 1.0F || species.nitrogenResponseClass > 3.0F)
            {
                throw new SqliteException("Error loading '" + species.ID + "': nitrogen response class must be in range [1.0 3.0].", (int)SqliteErrorCode.Error);
            }

            // phenology
            species.LeafPhenologyID = reader.PhenologyClass();

            // water
            species.MaxCanopyConductance = reader.MaxCanopyConductance();
            species.MinimumSoilWaterPotential = reader.PsiMin();

            // light
            species.lightResponseClass = reader.LightResponseClass();
            if (species.lightResponseClass < 1.0F || species.lightResponseClass > 5.0F)
            {
                throw new SqliteException("Error loading '" + species.ID + "': light response class must be in range [1.0 5.0].", (int)SqliteErrorCode.Error);
            }

            // regeneration
            // TODO: validation
            int mastYearInterval = reader.MastYearInterval();
            if (mastYearInterval < 1)
            {
                throw new SqliteException("Error loading '" + species.ID + "': seed year interval must be positive.", (int)SqliteErrorCode.Error);
            }
            species.mastYearProbability = 1.0F / mastYearInterval;
            species.minimumAgeInYearsForSeedProduction = reader.MaturityYears();
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

            // sapling and sapling growth parameters
            species.SaplingGrowth.HeightGrowthPotential.SetAndParse(reader.SaplingGrowthParametersHeightGrowthPotential());
            species.SaplingGrowth.HeightDiameterRatio = reader.SaplingGrowthParametersHdSapling();
            species.SaplingGrowth.StressThreshold = reader.SaplingGrowthParametersStressThreshold();
            species.SaplingGrowth.MaxStressYears = reader.SaplingGrowthParametersMaxStressYears();
            species.SaplingGrowth.ReferenceRatio = reader.SaplingGrowthParametersReferenceRatio();
            species.SaplingGrowth.ReinekeR = reader.SaplingGrowthParametersReinekesR();
            species.SaplingGrowth.BrowsingProbability = reader.SaplingGrowthParametersBrowsingProbability();
            species.SaplingGrowth.SproutGrowth = reader.SaplingGrowthParametersSproutGrowth();
            // if (species.SaplingGrowthParameters.SproutGrowth > 0.0F)
            // {
            //     if (species.SaplingGrowthParameters.SproutGrowth < 1.0F || species.SaplingGrowthParameters.SproutGrowth > 10.0F)
            //     {
            //         // TODO: convert to error?
            //         Debug.WriteLine("Value of 'sapSproutGrowth' dubious for species " + species.Name + "(value: " + species.SaplingGrowthParameters.SproutGrowth + ")");
            //     }
            // }
            species.SaplingGrowth.SetupReinekeLookup();
            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                species.SaplingGrowth.HeightGrowthPotential.Linearize(0.0, Constant.Sapling.MaximumHeight);
            }
            return species;
        }

        /** calculate fraction of stem wood increment base on dbh.
            allometric equation: a*d^b -> first derivation: a*b*d^(b-1)
            the ratio for stem is 1 minus the ratio of twigs to total woody increment at current "dbh". */
        public float GetStemFraction(float dbh)
        {
            float stemFraction = 1.0F - branchA * branchB * MathF.Pow(dbh, branchB - 1.0F) / (woodyA * woodyB * MathF.Pow(dbh, woodyB - 1.0F));
            return stemFraction;
        }

        /** Aging formula.
           calculates a relative "age" by combining a height- and an age-related term using a harmonic mean,
           and feeding this into the Landsberg and Waring formula.
           see http://iland-model.org/primary+production#respiration_and_aging
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

            float agingFactor = (float)aging.Evaluate(x);

            return Maths.Limit(agingFactor, 0.0F, 1.0F);
        }

        public int EstimateAgeFromHeight(float height)
        {
            int age = (int)(this.maximumAgeInYears * height / this.maximumHeightInM);
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
            if (tree.Age[treeIndex] > minimumAgeInYearsForSeedProduction && tree.Height[treeIndex] > 4.0F)
            {
                this.SeedDispersal.SetMatureTree(tree.LightCellIndexXY[treeIndex], tree.LeafArea[treeIndex]);
            }
        }

        /// returns true of a tree with given age/height is serotinous (i.e. seed release after fire)
        public bool IsTreeSerotinousRandom(RandomGenerator randomGenerator, int age)
        {
            if (this.serotinyFormula.IsEmpty)
            {
                return false;
            }
            // the function result (e.g. from a logistic regression model, e.g. Schoennagel 2013) is interpreted as probability
            float pSerotinous = (float)this.serotinyFormula.Evaluate(age);
            return randomGenerator.GetRandomProbability() < pSerotinous;
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
                this.IsMastYear = (model.RandomGenerator.GetRandomProbability() < mastYearProbability);
                if (this.IsMastYear && (model.Project.Output.Logging.LogLevel >= EventLevel.Informational))
                {
                    Trace.TraceInformation("Seed year for " + this.ID + ".");
                }
                // clear seed map
                this.SeedDispersal.Clear(model);
            }
        }

        public void GetHeightDiameterRatioLimits(float dbh, out float hdRatioLowerBound, out float hdRatioUpperBound)
        {
            hdRatioLowerBound = (float)heightDiameterRatioLowerBound.Evaluate(dbh);
            hdRatioUpperBound = (float)heightDiameterRatioUpperBound.Evaluate(dbh);
        }

        /** vpdResponse calculates response on vapor pressure deficit.
            Input: vpd [kPa]*/
        public float GetVpdModifier(float vpdInKiloPascals)
        {
            return MathF.Exp(this.modifierVpdK * vpdInKiloPascals);
        }

        /** temperatureResponse calculates response on delayed daily temperature.
            Input: average temperature [C]
            Note: slightly different from Mäkelä 2008: the maximum parameter (Sk) in iLand is interpreted as the absolute
                  temperature yielding a response of 1; in Mäkelä 2008, Sk is the width of the range (relative to the lower threshold)
            */
        public float GetTemperatureModifier(float dailyMA1orMonthlyTemperature)
        {
            float modifier = MathF.Max(dailyMA1orMonthlyTemperature - this.modifierTempMin, 0.0F);
            modifier = MathF.Min(modifier / (this.modifierTempMax - this.modifierTempMin), 1.0F);
            return modifier;
        }

        // soilwaterResponse is a function of the current matric potential of the soil.
        // iLand specific model chosen from Hanson 2004: http://iland-model.org/soil+water+response
        public float GetSoilWaterModifier(float psiInKilopascals)
        {
            float psiInMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterResponse = Maths.Limit((psiInMPa - this.MinimumSoilWaterPotential) / (-0.015F - this.MinimumSoilWaterPotential), 0.0F, 1.0F);
            return waterResponse;
        }

        /** calculate probabilty of death based on the current stress index. */
        public float GetDeathProbabilityForStress(float stressIndex)
        {
            if (stressIndex <= 0.0F)
            {
                return 0.0F;
            }
            float probability = 1.0F - MathF.Exp(-this.stressMortalityCoefficient * stressIndex);
            return probability;
        }
    }
}
