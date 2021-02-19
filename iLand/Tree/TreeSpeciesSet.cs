using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.World;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** @class SpeciesSet
        A SpeciesSet acts as a container for individual Species objects. In iLand, theoretically,
        multiple species sets can be used in parallel.
        */
    public class TreeSpeciesSet
    {
        private const int RandomSets = 20;

        private readonly Dictionary<string, TreeSpecies> mSpeciesByID;
        // nitrogen response classes
        private float class1K, class1minimum; // parameters of nitrogen response class 1
        private float class2K, class2minimum; // parameters of nitrogen response class 2
        private float class3K, class3minimum; // parameters of nitrogen response class 3
        // CO2 response
        private float mCO2baseConcentration, mCO2compensationPoint; // CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private float mCO2p0, mCO2beta0; // p0: production multiplier, beta0: relative productivity increase
        // Light Response classes
        private readonly Expression mLightResponseIntolerant; // light response function for the the most shade tolerant species
        private readonly Expression mLightResponseTolerant; // light response function for the most shade intolerant species
        private readonly Expression relativeHeightModifer; // function to modfiy LRI during read
        /// container holding the seed maps
        //private readonly List<SeedDispersal> mSeedDispersal;

        public List<TreeSpecies> ActiveSpecies { get; private init; } // list of species that are "active" (flag active in database)
        public List<int> RandomSpeciesOrder { get; private init; }
        public TreeSpeciesStamps ReaderStamps { get; private init; }
        public string SqlTableName { get; private init; } // table name of the species set

        public TreeSpeciesSet(string sqlTableName)
        {
            this.mLightResponseIntolerant = new Expression();
            this.mLightResponseTolerant = new Expression();
            this.relativeHeightModifer = new Expression();
            //this.mSeedDispersal = new List<SeedDispersal>();
            this.mSpeciesByID = new Dictionary<string, TreeSpecies>();

            this.ActiveSpecies = new List<TreeSpecies>();
            this.RandomSpeciesOrder = new List<int>(); // lazy initialization
            this.ReaderStamps = new TreeSpeciesStamps();
            this.SqlTableName = sqlTableName;
        }

        public TreeSpecies this[string speciesID]
        {
            get { return this.mSpeciesByID[speciesID]; }
        }

        public TreeSpecies this[int speciesIndex]
        {
            get { return this.ActiveSpecies[speciesIndex]; }
        }

        public int Count
        {
            get { return this.mSpeciesByID.Count; }
        }

        public float GetLriCorrection(float lightResourceIndex, float relativeHeight)
        {
            return (float)this.relativeHeightModifer.Evaluate(lightResourceIndex, relativeHeight);
        }

        /** loads active species from a database table and creates/setups the species.
            The function uses the global database-connection.
          */
        public int Setup(Project projectFile)
        {
            string readerStampFile = projectFile.GetFilePath(ProjectDirectory.LightIntensityProfile, projectFile.World.Species.ReaderStampFile);
            this.ReaderStamps.Load(readerStampFile);
            if (projectFile.World.Debug.DumpStamps)
            {
                Debug.WriteLine(this.ReaderStamps.Dump());
            }

            string speciesDatabaseFilePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Species.DatabaseFile);
            using SqliteConnection speciesDatabase = Landscape.GetDatabaseConnection(speciesDatabaseFilePath, true);
            using SqliteCommand speciesSelect = new SqliteCommand(String.Format("select * from {0}", this.SqlTableName), speciesDatabase);
            // Debug.WriteLine("Loading species set from SQL table " + tableName + ".");
            using SpeciesReader speciesReader = new SpeciesReader(speciesSelect.ExecuteReader());
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
                this.mSpeciesByID.Add(species.ID, species);
            }

            //Debug.WriteLine("Loaded " + mSpeciesByID.Count + " active species.");
            //Debug.WriteLine("index, id, name");
            //foreach (Species s in this.ActiveSpecies)
            //{
            //    Debug.WriteLine(s.Index + " " + s.ID + " " + s.Name);
            //}

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
            this.mCO2baseConcentration = projectFile.World.Species.CO2Response.BaseConcentration;
            this.mCO2compensationPoint = projectFile.World.Species.CO2Response.CompensationPoint;
            this.mCO2beta0 = projectFile.World.Species.CO2Response.Beta0;
            this.mCO2p0 = projectFile.World.Species.CO2Response.P0;
            if ((this.mCO2baseConcentration <= 0.0) || (this.mCO2compensationPoint <= 0.0) || (this.mCO2beta0 <= 0.0) || (this.mCO2p0 <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/CO2Response is missing, less than zero, or zero.");
            }
            if (this.mCO2baseConcentration <= this.mCO2compensationPoint)
            {
                throw new XmlException("Atmospheric CO₂ concentration is at or below the compensation point. Plants would be unable to grow and GPP would be negative.");
            }

            // setup light responses
            this.mLightResponseTolerant.SetAndParse(projectFile.World.Species.LightResponse.ShadeTolerant);
            this.mLightResponseIntolerant.SetAndParse(projectFile.World.Species.LightResponse.ShadeIntolerant);
            if (String.IsNullOrEmpty(mLightResponseTolerant.ExpressionString) || String.IsNullOrEmpty(mLightResponseIntolerant.ExpressionString))
            {
                throw new NotSupportedException("At least one parameter of /project/model/species/lightResponse is missing.");
            }
            // lri-correction
            this.relativeHeightModifer.SetAndParse(projectFile.World.Species.LightResponse.RelativeHeightLriModifier);

            if (projectFile.Model.Settings.ExpressionLinearizationEnabled)
            {
                this.mLightResponseTolerant.Linearize(0.0, 1.0);
                this.mLightResponseIntolerant.Linearize(0.0, 1.0);
                // x: LRI, y: relative height
                this.relativeHeightModifer.Linearize(0.0, 1.0, 0.0, 1.0);
            }

            return mSpeciesByID.Count;
        }

        public void SetupSeedDispersal(Model model)
        {
            foreach (TreeSpecies species in this.ActiveSpecies) 
            {
                species.SeedDispersal = new SeedDispersal(species);
                species.SeedDispersal.Setup(model); // setup memory for the seed map (grid)
                species.SeedDispersal.SetupExternalSeeds(model);
            }
            // Debug.WriteLine("Setup of seed dispersal maps finished.");
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void OnStartYear(Model model)
        {
            if (model.ModelSettings.RegenerationEnabled == false)
            {
                return;
            }
            foreach (TreeSpecies species in this.ActiveSpecies) 
            {
                species.OnStartYear(model);
            }
        }

        //public object GetVariable(SqliteDataReader reader, string columnName)
        //{
        //    int index = reader.GetOrdinal(columnName);
        //    if (index >= 0)
        //    {
        //        return reader.GetValue(index);
        //    }
        //    throw new SqliteException("Column " + columnName + " not present.", (int)SqliteErrorCode.Error);
        //}

        public void GetRandomSpeciesSampleIndices(RandomGenerator randomGenerator, out int beginIndex, out int endIndex)
        {
            beginIndex = this.ActiveSpecies.Count * randomGenerator.GetRandomInteger(0, TreeSpeciesSet.RandomSets - 1);
            endIndex = beginIndex + this.ActiveSpecies.Count;
        }

        public void CreateRandomSpeciesOrder(RandomGenerator randomGenerator)
        {
            this.RandomSpeciesOrder.Clear();
            this.RandomSpeciesOrder.Capacity = this.ActiveSpecies.Count * TreeSpeciesSet.RandomSets;
            for (int setIndex = 0; setIndex < TreeSpeciesSet.RandomSets; ++setIndex)
            {
                List<int> samples = new List<int>(ActiveSpecies.Count);
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

        /// calculate nitrogen response for a given amount of available nitrogen and a response class
        /// for fractional values, the response value is interpolated between the fixedly defined classes (1,2,3)
        public float GetNitrogenResponse(float availableNitrogen, float responseClass)
        {
            if (responseClass > 2.0F)
            {
                if (responseClass == 3.0F)
                {
                    return TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class3K, this.class3minimum);
                }
                else
                {
                    // linear interpolation between 2 and 3
                    float response2 = TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class2K, this.class2minimum);
                    float response3 = TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class3K, this.class3minimum);
                    return response2 + (responseClass - 2.0F) * (response3 - response2);
                }
            }
            else if (responseClass == 2.0F)
            {
                return TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class2K, this.class2minimum);
            }
            else if (responseClass == 1.0F)
            {
                return TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class1K, this.class1minimum);
            }
            else
            {
                // linear interpolation between 1 and 2
                float response1 = TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class1K, this.class1minimum);
                float response2 = TreeSpeciesSet.GetNitrogenResponse(availableNitrogen, this.class2K, this.class2minimum);
                return response1 + (responseClass - 1.0F) * (response2 - response1);
            }
        }

        private static float GetNitrogenResponse(float availableNitrogen, float nitrogenK, float minimumNitrogen)
        {
            if (availableNitrogen <= minimumNitrogen)
            {
                return 0.0F;
            }
            float response = 1.0F - MathF.Exp(nitrogenK * (availableNitrogen - minimumNitrogen));
            return response;
        }

        /** calculation for the CO2 response for the ambientCO2 for the water- and nitrogen responses given.
            The calculation follows Friedlingsstein 1995 (see also links to equations in code)
            see also: http://iland-model.org/CO2+response
            @param ambientCO2 current CO2 concentration (ppm)
            @param nitrogenResponse (yearly) nitrogen response of the species
            @param soilWaterReponse soil water response (mean value for a month)
            */
        public float GetCarbonDioxideResponse(float ambientCO2, float nitrogenResponse, float soilWaterResponse)
        {
            Debug.Assert((nitrogenResponse >= 0.0F) && (nitrogenResponse <= 1.000001F));
            Debug.Assert((soilWaterResponse >= 0.0F) && (soilWaterResponse <= 1.000001F));
            if (nitrogenResponse == 0.0F)
            {
                return 0.0F;
            }

            float beta = mCO2beta0 * (2.0F - soilWaterResponse) * nitrogenResponse;
            float r = 1.0F + Constant.Ln2 * beta; // NPP increase for a doubling of atmospheric CO2 (Eq. 17)

            // fertilization function (cf. Farquhar, 1980) based on Michaelis-Menten expressions
            float deltaC =  mCO2baseConcentration - mCO2compensationPoint;
            float k2 = (2.0F * mCO2baseConcentration - mCO2compensationPoint - r * deltaC) / ((r - 1.0F) * deltaC * (2.0F * mCO2baseConcentration - mCO2compensationPoint)); // Eq. 16
            float k1 = (1.0F + k2 * deltaC) / deltaC;

            float response = mCO2p0 * k1 * (ambientCO2 - mCO2compensationPoint) / (1 + k2 * (ambientCO2 - mCO2compensationPoint)); // Eq. 16
            return response;
        }

        /** calculates the lightResponse based on a value for LRI and the species lightResponseClass.
            LightResponse is classified from 1 (very shade inolerant) and 5 (very shade tolerant) and interpolated for values between 1 and 5.
            Returns a value between 0..1
            @sa http://iland-model.org/allocation#reserve_and_allocation_to_stem_growth */
        public float GetLightResponse(float lightResourceIndex, float lightResponseClass)
        {
            float intolerant = (float)mLightResponseIntolerant.Evaluate(lightResourceIndex);
            float tolerant = (float)mLightResponseTolerant.Evaluate(lightResourceIndex);
            float response = intolerant + 0.25F * (lightResponseClass - 1.0F) * (tolerant - intolerant);
            return Maths.Limit(response, 0.0F, 1.0F);
        }
    }
}
