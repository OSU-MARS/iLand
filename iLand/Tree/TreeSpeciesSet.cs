using iLand.Input.ProjectFile;
using iLand.Input.Tree;
using iLand.World;
using iLand.Tool;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** A SpeciesSet acts as a container for individual Species objects. In iLand, theoretically,
        multiple species sets can be used in parallel.
        */
    public class TreeSpeciesSet
    {
        private const int RandomSets = 20;

        private readonly SortedList<WorldFloraID, TreeSpecies> treeSpeciesByID;
        // nitrogen response classes
        private readonly float class1K, class1minimum; // parameters of nitrogen response class 1
        private readonly float class2K, class2minimum; // parameters of nitrogen response class 2
        private readonly float class3K, class3minimum; // parameters of nitrogen response class 3
        // CO2 response
        private readonly float co2baseConcentration, co2compensationPoint; // CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private readonly float co2p0, co2beta0; // p0: production multiplier, beta0: relative productivity increase
        // light response
        private readonly Expression lightResponseShadeIntolerant; // light response function for the the most shade tolerant species
        private readonly Expression lightResponseShadeTolerant; // light response function for the most shade intolerant species
        private readonly Expression relativeHeightModifer; // function to modfiy LRI during read
        /// container holding the seed maps
        //private readonly List<SeedDispersal> mSeedDispersal;

        public List<TreeSpecies> ActiveSpecies { get; private init; } // list of species that are "active" (flag active in database)
        public List<int> RandomSpeciesOrder { get; private init; }
        public TreeSpeciesStamps ReaderStamps { get; private init; }
        public string SqlTableName { get; private init; } // table name of the species set

        public TreeSpeciesSet(Project projectFile, string sqlTableName)
        {
            // load active tree species from a database table and create/setup the species
            string? iLandAssemblyFilePath = Path.GetDirectoryName(typeof(TreeSpeciesSet).Assembly.Location);
            Debug.Assert(iLandAssemblyFilePath != null);
            string readerStampFilePath = Path.Combine(iLandAssemblyFilePath, Constant.File.ReaderStampFileName);

            this.lightResponseShadeIntolerant = new();
            this.lightResponseShadeTolerant = new();
            this.relativeHeightModifer = new();
            //this.mSeedDispersal = new();
            this.treeSpeciesByID = [];

            this.ActiveSpecies = [];
            this.RandomSpeciesOrder = []; // lazy initialization
            this.ReaderStamps = new(readerStampFilePath);
            this.SqlTableName = sqlTableName;

            string speciesDatabaseFilePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Species.DatabaseFile);
            using SqliteConnection speciesDatabase = Landscape.GetDatabaseConnection(speciesDatabaseFilePath, openReadOnly: true);
            using SqliteCommand speciesSelect = new(String.Format("select * from {0}", this.SqlTableName), speciesDatabase);
            // Debug.WriteLine("Loading species set from SQL table " + tableName + ".");
            using TreeSpeciesReader speciesReader = new(speciesSelect.ExecuteReader());
            while (speciesReader.Read())
            {
                bool isActive = speciesReader.Active();
                if (isActive == false)
                {
                    continue;
                }
                TreeSpecies species = Tree.TreeSpecies.Load(projectFile, speciesReader, this);
                Debug.Assert(species.Active);
                this.ActiveSpecies.Add(species);
                this.treeSpeciesByID.Add(species.WorldFloraID, species);
            }

            // setup nitrogen response
            this.class1K = projectFile.World.Species.NitrogenResponseClasses.Class1K;
            this.class1minimum = projectFile.World.Species.NitrogenResponseClasses.Class1Minimum;
            this.class2K = projectFile.World.Species.NitrogenResponseClasses.Class2K;
            this.class2minimum = projectFile.World.Species.NitrogenResponseClasses.Class2Minimum;
            this.class3K = projectFile.World.Species.NitrogenResponseClasses.Class3K;
            this.class3minimum = projectFile.World.Species.NitrogenResponseClasses.Class3Minimum;
            if ((this.class1K >= 0.0) || (this.class1minimum <= 0.0) ||
                (this.class2K >= 0.0) || (this.class2minimum <= 0.0) ||
                (this.class3K >= 0.0) || (this.class3minimum <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/nitrogenResponseClasses/class_[1..3]_[a..b] is missing, has an incorrect sign, or is zero.");
            }

            // setup CO2 response
            this.co2baseConcentration = projectFile.World.Species.CO2Response.BaseConcentration;
            this.co2compensationPoint = projectFile.World.Species.CO2Response.CompensationPoint;
            this.co2beta0 = projectFile.World.Species.CO2Response.Beta0;
            this.co2p0 = projectFile.World.Species.CO2Response.P0;
            if ((this.co2baseConcentration <= 0.0) || (this.co2compensationPoint <= 0.0) || (this.co2beta0 <= 0.0) || (this.co2p0 <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/CO2Response is missing, less than zero, or zero.");
            }
            if (this.co2baseConcentration <= this.co2compensationPoint)
            {
                throw new XmlException("Atmospheric CO₂ concentration is at or below the compensation point. Plants would be unable to grow and GPP would be negative.");
            }

            // setup light responses
            this.lightResponseShadeTolerant.Parse(projectFile.World.Species.LightResponse.ShadeTolerant);
            this.lightResponseShadeIntolerant.Parse(projectFile.World.Species.LightResponse.ShadeIntolerant);
            if (String.IsNullOrEmpty(lightResponseShadeTolerant.ExpressionString) || String.IsNullOrEmpty(lightResponseShadeIntolerant.ExpressionString))
            {
                throw new NotSupportedException("At least one parameter of /project/model/species/lightResponse is missing.");
            }
            // lri-correction
            this.relativeHeightModifer.Parse(projectFile.World.Species.LightResponse.RelativeHeightLriModifier);

            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                this.lightResponseShadeTolerant.Linearize(0.0F, 1.0F);
                this.lightResponseShadeIntolerant.Linearize(0.0F, 1.0F);
                // x: LRI, y: relative height
                this.relativeHeightModifer.Linearize(0.0F, 1.0F, 0.0F, 1.0F);
            }
        }

        public TreeSpecies this[WorldFloraID speciesID]
        {
            get { return this.treeSpeciesByID[speciesID]; }
        }

        public TreeSpecies this[int speciesIndex]
        {
            get { return this.ActiveSpecies[speciesIndex]; }
        }

        public int Count
        {
            get { return this.treeSpeciesByID.Count; }
        }

        public void CreateRandomSpeciesOrder(RandomGenerator randomGenerator)
        {
            this.RandomSpeciesOrder.Clear();
            this.RandomSpeciesOrder.Capacity = this.ActiveSpecies.Count * TreeSpeciesSet.RandomSets;
            for (int setIndex = 0; setIndex < TreeSpeciesSet.RandomSets; ++setIndex)
            {
                List<int> samples = new(ActiveSpecies.Count);
                // fill list
                foreach (TreeSpecies species in this.ActiveSpecies)
                {
                    samples.Add(species.Index);
                }
                // sample and reduce list
                while (samples.Count > 0)
                {
                    int index = randomGenerator.GetRandomInteger(0, samples.Count);
                    this.RandomSpeciesOrder.Add(samples[index]);
                    samples.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Find the CO₂ growth modifier for the ambient CO₂, water modifier, and nitrogen modifer given.
        /// </summary>
        /// <param name="atmosphericCO2">atmospheric CO₂ concentration at this simulation timestep (ppm)</param>
        /// <param name="nitrogenModifier">nitrogen response of the species (yearly)</param>
        /// <param name="soilWaterModifier">soil water response (mean value for a month)</param>
        /// <returns></returns>
        /// <remarks>
        /// Unlike other growth modifiers, when not limited by nitrogen or water, the CO₂ modifier is greater than one for
        /// atmospheric CO₂ concentrations above the base concentration. For a 380 ppm base concentration RCP 8.5 emissions yield a 
        /// modifier near 1.3 in year 2100.
        /// 
        /// Friedlingstein P, Fung I, Holland E et a. 1995. On the contribution of CO₂ fertilization to the missing biospheric sink. Global
        ///   Biogeochemical Cycles 9(4):541-556. https://doi.org/10.1029/95GB02381
        /// See also: http://iland-model.org/CO2+response
        /// </remarks>
        public float GetCarbonDioxideModifier(float atmosphericCO2, float nitrogenModifier, float soilWaterModifier)
        {
            Debug.Assert((nitrogenModifier >= 0.0F) && (nitrogenModifier <= 1.000001F) && 
                         (soilWaterModifier >= 0.0F) && (soilWaterModifier <= 1.000001F));
            if ((atmosphericCO2 < this.co2compensationPoint) || (nitrogenModifier == 0.0F))
            {
                // atmospheric concentration below compensation point -> modifier would be negative
                // nitrogen = 0 -> r becomes 1 -> divide by zero in k2
                return 0.0F;
            }

            float beta = this.co2beta0 * (2.0F - soilWaterModifier) * nitrogenModifier;
            float r = 1.0F + Constant.Math.Ln2 * beta; // NPP increase for a doubling of atmospheric CO2 (Eq. 17)

            // fertilization function (Farquhar 1980) based on Michaelis-Menten expressions
            // Farquhar GD, von Caemmerer S, Berry JA. 1980. A biochemical model of photosynthetic CO₂ assimilation in leaves of C₃ species.
            //   Planta 149:78–90. https://doi.org/10.1007/BF00386231
            float deltaCO2 =  this.co2baseConcentration - this.co2compensationPoint;
            float k2 = (2.0F * this.co2baseConcentration - this.co2compensationPoint - r * deltaCO2) / ((r - 1.0F) * deltaCO2 * (2.0F * this.co2baseConcentration - this.co2compensationPoint)); // Eq. 16
            float k1 = (1.0F + k2 * deltaCO2) / deltaCO2;

            float co2modifier = this.co2p0 * k1 * (atmosphericCO2 - this.co2compensationPoint) / (1 + k2 * (atmosphericCO2 - this.co2compensationPoint)); // Eq. 16
            return co2modifier;
        }

        /** calculates the lightResponse based on a value for LRI and the species lightResponseClass.
            LightResponse is classified from 1 (very shade inolerant) and 5 (very shade tolerant) and interpolated for values between 1 and 5.
            Returns a value between 0..1
            @sa http://iland-model.org/allocation#reserve_and_allocation_to_stem_growth */
        public float GetLightResponse(float lightResourceIndex, float lightResponseClass)
        {
            float intolerant = this.lightResponseShadeIntolerant.Evaluate(lightResourceIndex);
            float tolerant = this.lightResponseShadeTolerant.Evaluate(lightResourceIndex);
            float response = intolerant + 0.25F * (lightResponseClass - 1.0F) * (tolerant - intolerant);
            return Maths.Limit(response, 0.0F, 1.0F);
        }

        public float GetLriCorrection(float lightResourceIndex, float relativeHeight)
        {
            return this.relativeHeightModifer.Evaluate(lightResourceIndex, relativeHeight);
        }

        /// calculate nitrogen response for a given amount of available nitrogen and a response class
        /// for fractional values, the response value is interpolated between the fixedly defined classes (1,2,3)
        public float GetNitrogenModifier(float availableNitrogen, float responseClass)
        {
            if (responseClass > 2.0F)
            {
                if (responseClass == 3.0F)
                {
                    return TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class3K, this.class3minimum);
                }
                else
                {
                    // linear interpolation between 2 and 3
                    float modifier2 = TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class2K, this.class2minimum);
                    float modifier3 = TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class3K, this.class3minimum);
                    return modifier2 + (responseClass - 2.0F) * (modifier3 - modifier2);
                }
            }
            else if (responseClass == 2.0F)
            {
                return TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class2K, this.class2minimum);
            }
            else if (responseClass == 1.0F)
            {
                return TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class1K, this.class1minimum);
            }
            else
            {
                // linear interpolation between 1 and 2
                float modifier1 = TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class1K, this.class1minimum);
                float modifier2 = TreeSpeciesSet.GetNitrogenModifier(availableNitrogen, this.class2K, this.class2minimum);
                return modifier1 + (responseClass - 1.0F) * (modifier2 - modifier1);
            }
        }

        private static float GetNitrogenModifier(float availableNitrogen, float nitrogenK, float minimumNitrogen)
        {
            if (availableNitrogen <= minimumNitrogen)
            {
                return 0.0F;
            }
            float nitrogenModifier = 1.0F - MathF.Exp(nitrogenK * (availableNitrogen - minimumNitrogen));
            return nitrogenModifier;
        }

        public void GetRandomSpeciesSampleIndices(RandomGenerator randomGenerator, out int beginIndex, out int endIndex)
        {
            beginIndex = this.ActiveSpecies.Count * randomGenerator.GetRandomInteger(0, TreeSpeciesSet.RandomSets - 1);
            endIndex = beginIndex + this.ActiveSpecies.Count;
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void OnStartYear(Model model)
        {
            if (model.Project.Model.Settings.RegenerationEnabled == false)
            {
                return;
            }
            foreach (TreeSpecies species in this.ActiveSpecies)
            {
                species.OnStartYear(model);
            }
        }

        public void SetupSeedDispersal(Model model)
        {
            foreach (TreeSpecies species in this.ActiveSpecies)
            {
                species.SeedDispersal = new(species);
                species.SeedDispersal.Setup(model); // setup memory for the seed map (grid)
            }
            // Debug.WriteLine("Setup of seed dispersal maps finished.");
        }
    }
}
