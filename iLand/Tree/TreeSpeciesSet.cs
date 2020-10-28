using iLand.Simulation;
using iLand.Input;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

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
        private float mNitrogen1A, mNitrogen1B; // parameters of nitrogen response class 1
        private float mNitrogen2A, mNitrogen2B; // parameters of nitrogen response class 2
        private float mNitrogen3A, mNitrogen3B; // parameters of nitrogen response class 3
        // CO2 response
        private float mCO2base, mCO2compensationPoint; // CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private float mCO2p0, mCO2beta0; // p0: production multiplier, beta0: relative productivity increase
        // Light Response classes
        private readonly Expression mLightResponseIntolerant; // light response function for the the most shade tolerant species
        private readonly Expression mLightResponseTolerant; // light response function for the most shade intolerant species
        private readonly Expression mLriCorrection; // function to modfiy LRI during read
        /// container holding the seed maps
        //private readonly List<SeedDispersal> mSeedDispersal;

        public List<TreeSpecies> ActiveSpecies { get; private set; } // list of species that are "active" (flag active in database)
        public List<int> RandomSpeciesOrder { get; private set; }
        public TreeSpeciesStamps ReaderStamps { get; private set; }
        public string SqlTableName { get; private set; } // table name of the species set

        public TreeSpeciesSet(string sqlTableName)
        {
            this.mLightResponseIntolerant = new Expression();
            this.mLightResponseTolerant = new Expression();
            this.mLriCorrection = new Expression();
            //this.mSeedDispersal = new List<SeedDispersal>();
            this.mSpeciesByID = new Dictionary<string, TreeSpecies>();

            this.ActiveSpecies = new List<TreeSpecies>();
            this.RandomSpeciesOrder = new List<int>();
            this.ReaderStamps = new TreeSpeciesStamps();
            this.SqlTableName = sqlTableName;
        }

        public TreeSpecies GetSpecies(string speciesID) { return mSpeciesByID[speciesID]; }
        public int SpeciesCount() { return mSpeciesByID.Count; }

        public float GetLriCorrection(Simulation.Model model, float lightResourceIndex, float relativeHeight) 
        { 
            return (float)mLriCorrection.Evaluate(model, lightResourceIndex, relativeHeight); 
        }

        public TreeSpecies GetSpecies(int index)
        {
            foreach (TreeSpecies species in mSpeciesByID.Values)
            {
                if (species.Index == index)
                {
                    return species;
                }
            }
            return null;
        }

        /** loads active species from a database table and creates/setups the species.
            The function uses the global database-connection.
          */
        public int Setup(Simulation.Model model)
        {
            string readerStampFile = model.Files.GetPath(model.Project.Model.Species.ReaderStampFile, "lip");
            this.ReaderStamps.Load(readerStampFile);
            if (model.Project.Model.Parameter.DebugDumpStamps)
            {
                Debug.WriteLine(ReaderStamps.Dump());
            }

            string speciesDatabaseFilePath = model.Files.GetPath(model.Project.System.Database.In, "database");
            using SqliteConnection speciesDatabase = model.Files.GetDatabaseConnection(speciesDatabaseFilePath, true);
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
                TreeSpecies species = Tree.TreeSpecies.Load(model, speciesReader, this);
                mSpeciesByID.Add(species.ID, species);
                if (species.Active)
                {
                    this.ActiveSpecies.Add(species);
                }
                // Expression.AddSpecies(species.ID, species.Index);
            }

            Debug.WriteLine("Loaded " + mSpeciesByID.Count + " active species.");
            //Debug.WriteLine("index, id, name");
            //foreach (Species s in this.ActiveSpecies)
            //{
            //    Debug.WriteLine(s.Index + " " + s.ID + " " + s.Name);
            //}

            // setup nitrogen response
            this.mNitrogen1A = model.Project.Model.Species.NitrogenResponseClasses.Class1A;
            this.mNitrogen1B = model.Project.Model.Species.NitrogenResponseClasses.Class1B;
            this.mNitrogen2A = model.Project.Model.Species.NitrogenResponseClasses.Class2A;
            this.mNitrogen2B = model.Project.Model.Species.NitrogenResponseClasses.Class2B;
            this.mNitrogen3A = model.Project.Model.Species.NitrogenResponseClasses.Class3A;
            this.mNitrogen3B = model.Project.Model.Species.NitrogenResponseClasses.Class3B;
            if ((this.mNitrogen1A >= 0.0) || (this.mNitrogen1B <= 0.0) || 
                (this.mNitrogen2A >= 0.0) || (this.mNitrogen2B <= 0.0) ||
                (this.mNitrogen3A >= 0.0) || (this.mNitrogen3B <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/nitrogenResponseClasses/class_[1..3]_[a..b] is missing, has an incorrect sign, or is zero.");
            }

            // setup CO2 response
            this.mCO2base = model.Project.Model.Species.CO2Response.BaseConcentration;
            this.mCO2compensationPoint = model.Project.Model.Species.CO2Response.CompensationPoint;
            this.mCO2beta0 = model.Project.Model.Species.CO2Response.Beta0;
            this.mCO2p0 = model.Project.Model.Species.CO2Response.P0;
            if ((this.mCO2base <= 0.0) || (this.mCO2compensationPoint <= 0.0) || (this.mCO2beta0 <= 0.0) || (this.mCO2p0 <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/CO2Response is missing, less than zero, or zero.");
            }
            if (mCO2base <= mCO2compensationPoint)
            {
                throw new XmlException("Atmospheric CO₂ concentration is at or below the compensation point. Plants would be unable to grow and GPP would be negative.");
            }

            // setup Light responses
            mLightResponseTolerant.SetAndParse(model.Project.Model.Species.LightResponse.ShadeTolerant);
            mLightResponseIntolerant.SetAndParse(model.Project.Model.Species.LightResponse.ShadeIntolerant);
            if (String.IsNullOrEmpty(mLightResponseTolerant.ExpressionString) || String.IsNullOrEmpty(mLightResponseIntolerant.ExpressionString))
            {
                throw new NotSupportedException("At least one parameter of /project/model/species/lightResponse is missing.");
            }
            // lri-correction
            mLriCorrection.SetAndParse(model.Project.Model.Species.LightResponse.LriModifier);

            if (model.Project.System.Settings.ExpressionLinearizationEnabled)
            {
                mLightResponseTolerant.Linearize(model, 0.0, 1.0);
                mLightResponseIntolerant.Linearize(model, 0.0, 1.0);
                // x: LRI, y: relative height
                mLriCorrection.Linearize(model, 0.0, 1.0, 0.0, 1.0);
            }

            this.CreateRandomSpeciesOrder(model);
            return mSpeciesByID.Count;
        }

        public void SetupSeedDispersal(Simulation.Model model)
        {
            foreach (TreeSpecies species in this.ActiveSpecies) 
            {
                species.SeedDispersal = new SeedDispersal(species);
                species.SeedDispersal.Setup(model); // setup memory for the seed map (grid)
                species.SeedDispersal.SetupExternalSeeds(model);
            }
            // Debug.WriteLine("Setup of seed dispersal maps finished.");
        }

        public void DisperseSeedsForYear(Simulation.Model model)
        {
            if (model.ModelSettings.RegenerationEnabled == false)
            {
                return;
            }
            //using DebugTimer t = model.DebugTimers.Create("SpeciesSet.Regeneration()");

            ThreadRunner runner = new ThreadRunner(this.ActiveSpecies); // initialize a thread runner object with all active species
            runner.Run(model, this.DisperseSeedsForYear);

            if (model.Files.LogDebug())
            {
                Debug.WriteLine("Seed dispersal finished.");
            }
        }

        private void DisperseSeedsForYear(Simulation.Model model, TreeSpecies species)
        {
            species.SeedDispersal.DisperseSeeds(model);
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void OnStartYear(Simulation.Model model)
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

        public void GetRandomSpeciesSampleIndices(Simulation.Model model, out int beginIndex, out int endIndex)
        {
            beginIndex = this.ActiveSpecies.Count * model.RandomGenerator.GetRandomInteger(0, TreeSpeciesSet.RandomSets - 1);
            endIndex = beginIndex + this.ActiveSpecies.Count;
        }

        private void CreateRandomSpeciesOrder(Simulation.Model model)
        {
            RandomSpeciesOrder.Clear();
            RandomSpeciesOrder.Capacity = ActiveSpecies.Count * RandomSets;
            for (int i = 0; i < RandomSets; ++i)
            {
                List<int> samples = new List<int>(ActiveSpecies.Count);
                // fill list
                foreach (TreeSpecies s in ActiveSpecies)
                {
                    samples.Add(s.Index);
                }
                // sample and reduce list
                while (samples.Count > 0)
                {
                    int index = model.RandomGenerator.GetRandomInteger(0, samples.Count);
                    RandomSpeciesOrder.Add(samples[index]);
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
                    return this.GetNitrogenResponse(availableNitrogen, mNitrogen3A, mNitrogen3B);
                }
                else
                {
                    // interpolate between 2 and 3
                    float value4 = this.GetNitrogenResponse(availableNitrogen, mNitrogen2A, mNitrogen2B);
                    float value3 = this.GetNitrogenResponse(availableNitrogen, mNitrogen3A, mNitrogen3B);
                    return value4 + (responseClass - 2.0F) * (value3 - value4);
                }
            }
            if (responseClass == 2.0F)
            {
                return this.GetNitrogenResponse(availableNitrogen, mNitrogen2A, mNitrogen2B);
            }
            if (responseClass == 1.0F)
            {
                return this.GetNitrogenResponse(availableNitrogen, mNitrogen1A, mNitrogen1B);
            }
            // last ressort: interpolate between 1 and 2
            float value1 = this.GetNitrogenResponse(availableNitrogen, mNitrogen1A, mNitrogen1B);
            float value2 = this.GetNitrogenResponse(availableNitrogen, mNitrogen2A, mNitrogen2B);
            return value1 + (responseClass - 1.0F) * (value2 - value1);
        }

        private float GetNitrogenResponse(float availableNitrogen, float nitrogenK, float minimumNitrogen)
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
            see also: http://iland.boku.ac.at/CO2+response
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
            float deltaC = mCO2base - mCO2compensationPoint;
            float k2 = (2.0F * mCO2base - mCO2compensationPoint - r * deltaC) / ((r - 1.0F) * deltaC * (2.0F * mCO2base - mCO2compensationPoint)); // Eq. 16
            float k1 = (1.0F + k2 * deltaC) / deltaC;

            float response = mCO2p0 * k1 * (ambientCO2 - mCO2compensationPoint) / (1 + k2 * (ambientCO2 - mCO2compensationPoint)); // Eq. 16
            return response;
        }

        /** calculates the lightResponse based on a value for LRI and the species lightResponseClass.
            LightResponse is classified from 1 (very shade inolerant) and 5 (very shade tolerant) and interpolated for values between 1 and 5.
            Returns a value between 0..1
            @sa http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth */
        public float GetLightResponse(Simulation.Model model, float lightResourceIndex, float lightResponseClass)
        {
            float intolerant = (float)mLightResponseIntolerant.Evaluate(model, lightResourceIndex);
            float tolerant = (float)mLightResponseTolerant.Evaluate(model, lightResourceIndex);
            float response = intolerant + 0.25F * (lightResponseClass - 1.0F) * (tolerant - intolerant);
            return Maths.Limit(response, 0.0F, 1.0F);
        }
    }
}
