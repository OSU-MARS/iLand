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
    public class SpeciesSet
    {
        private const int RandomSets = 20;

        private readonly Dictionary<string, Species> mSpeciesByID;
        // nitrogen response classes
        private float mNitrogen1a, mNitrogen1b; // parameters of nitrogen response class 1
        private float mNitrogen2a, mNitrogen2b; // parameters of nitrogen response class 2
        private float mNitrogen3a, mNitrogen3b; // parameters of nitrogen response class 3
        // CO2 response
        private double mCO2base, mCO2comp; // CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private double mCO2p0, mCO2beta0; // p0: production multiplier, beta0: relative productivity increase
        // Light Response classes
        private readonly Expression mLightResponseIntolerant; // light response function for the the most shade tolerant species
        private readonly Expression mLightResponseTolerant; // light response function for the most shade intolerant species
        private readonly Expression mLriCorrection; // function to modfiy LRI during read
        /// container holding the seed maps
        private readonly List<SeedDispersal> mSeedDispersal;

        public List<Species> ActiveSpecies { get; private set; } // list of species that are "active" (flag active in database)
        public List<int> RandomSpeciesOrder { get; private set; }
        public SpeciesStamps ReaderStamps { get; private set; }
        public string SqlTableName { get; private set; } // table name of the species set

        public SpeciesSet(string sqlTableName)
        {
            this.mLightResponseIntolerant = new Expression();
            this.mLightResponseTolerant = new Expression();
            this.mLriCorrection = new Expression();
            this.mSeedDispersal = new List<SeedDispersal>();
            this.mSpeciesByID = new Dictionary<string, Species>();

            this.ActiveSpecies = new List<Species>();
            this.RandomSpeciesOrder = new List<int>();
            this.ReaderStamps = new SpeciesStamps();
            this.SqlTableName = sqlTableName;
        }

        public Species GetSpecies(string speciesID) { return mSpeciesByID[speciesID]; }
        public int SpeciesCount() { return mSpeciesByID.Count; }

        public void Clear()
        {
            // BUGBUG: C++ doesn't clear other collections?
            mSeedDispersal.Clear();
            mSpeciesByID.Clear();
            ActiveSpecies.Clear();
        }

        public float GetLriCorrection(Simulation.Model model, float lightResourceIndex, float relativeHeight) 
        { 
            return (float)mLriCorrection.Evaluate(model, lightResourceIndex, relativeHeight); 
        }

        public Species Species(int index)
        {
            foreach (Species s in mSpeciesByID.Values)
            {
                if (s.Index == index)
                {
                    return s;
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

            this.Clear();

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
                Species species = Tree.Species.Load(model, speciesReader, this);
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
            this.mNitrogen1a = model.Project.Model.Species.NitrogenResponseClasses.Class1A;
            this.mNitrogen1b = model.Project.Model.Species.NitrogenResponseClasses.Class1B;
            this.mNitrogen2a = model.Project.Model.Species.NitrogenResponseClasses.Class2A;
            this.mNitrogen2b = model.Project.Model.Species.NitrogenResponseClasses.Class2B;
            this.mNitrogen3a = model.Project.Model.Species.NitrogenResponseClasses.Class3A;
            this.mNitrogen3b = model.Project.Model.Species.NitrogenResponseClasses.Class3B;
            if ((this.mNitrogen1a >= 0.0) || (this.mNitrogen1b <= 0.0) || 
                (this.mNitrogen2a >= 0.0) || (this.mNitrogen2b <= 0.0) ||
                (this.mNitrogen3a >= 0.0) || (this.mNitrogen3b <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/nitrogenResponseClasses/class_[1..3]_[a..b] is missing, has an incorrect sign, or is zero.");
            }

            // setup CO2 response
            this.mCO2base = model.Project.Model.Species.CO2Response.BaseConcentration;
            this.mCO2comp = model.Project.Model.Species.CO2Response.CompensationPoint;
            this.mCO2beta0 = model.Project.Model.Species.CO2Response.Beta0;
            this.mCO2p0 = model.Project.Model.Species.CO2Response.P0;
            if ((this.mCO2base <= 0.0) || (this.mCO2comp <= 0.0) || (this.mCO2beta0 <= 0.0) || (this.mCO2p0 <= 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/CO2Response is missing, less than zero, or zero.");
            }
            if (mCO2base <= mCO2comp)
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

            CreateRandomSpeciesOrder(model);
            return mSpeciesByID.Count;
        }

        public void SetupRegeneration(Simulation.Model model)
        {
            foreach (Species species in ActiveSpecies) 
            {
                species.SeedDispersal = new SeedDispersal(species);
                species.SeedDispersal.Setup(model); // setup memory for the seed map (grid)
                species.SeedDispersal.SetupExternalSeeds(model);
            }
            // Debug.WriteLine("Setup of seed dispersal maps finished.");
        }

        public void SeedDistribution(Species species, Simulation.Model model)
        {
            species.SeedDispersal.Execute(model);
        }

        public void Regeneration(Simulation.Model model)
        {
            if (!model.ModelSettings.RegenerationEnabled)
            {
                return;
            }
            //using DebugTimer t = model.DebugTimers.Create("SpeciesSet.Regeneration()");

            ThreadRunner runner = new ThreadRunner(ActiveSpecies); // initialize a thread runner object with all active species
            runner.Run(SeedDistribution, model);

            if (model.Files.LogDebug())
            {
                Debug.WriteLine("seed dispersal finished.");
            }
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void NewYear(Simulation.Model model)
        {
            if (model.ModelSettings.RegenerationEnabled == false)
            {
                return;
            }
            foreach (Species species in ActiveSpecies) 
            {
                species.NewYear(model);
            }
        }

        public object GetVariable(SqliteDataReader reader, string columnName)
        {
            int idx = reader.GetOrdinal(columnName);
            if (idx >= 0)
            {
                return reader.GetValue(idx);
            }
            throw new NotSupportedException("SpeciesSet: variable not set: " + columnName);
        }

        public void GetRandomSpeciesSampleIndices(Simulation.Model model, out int beginIndex, out int endIndex)
        {
            beginIndex = this.ActiveSpecies.Count * model.RandomGenerator.Random(0, RandomSets - 1);
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
                foreach (Species s in ActiveSpecies)
                {
                    samples.Add(s.Index);
                }
                // sample and reduce list
                while (samples.Count > 0)
                {
                    int index = model.RandomGenerator.Random(0, samples.Count);
                    RandomSpeciesOrder.Add(samples[index]);
                    samples.RemoveAt(index);
                }
            }
        }

        /// calculate nitrogen response for a given amount of available nitrogen and a response class
        /// for fractional values, the response value is interpolated between the fixedly defined classes (1,2,3)
        public float NitrogenResponse(float availableNitrogen, float responseClass)
        {
            if (responseClass > 2.0F)
            {
                if (responseClass == 3.0F)
                {
                    return NitrogenResponse(availableNitrogen, mNitrogen3a, mNitrogen3b);
                }
                else
                {
                    // interpolate between 2 and 3
                    float value4 = NitrogenResponse(availableNitrogen, mNitrogen2a, mNitrogen2b);
                    float value3 = NitrogenResponse(availableNitrogen, mNitrogen3a, mNitrogen3b);
                    return value4 + (responseClass - 2.0F) * (value3 - value4);
                }
            }
            if (responseClass == 2.0F)
            {
                return NitrogenResponse(availableNitrogen, mNitrogen2a, mNitrogen2b);
            }
            if (responseClass == 1.0F)
            {
                return NitrogenResponse(availableNitrogen, mNitrogen1a, mNitrogen1b);
            }
            // last ressort: interpolate between 1 and 2
            float value1 = NitrogenResponse(availableNitrogen, mNitrogen1a, mNitrogen1b);
            float value2 = NitrogenResponse(availableNitrogen, mNitrogen2a, mNitrogen2b);
            return value1 + (responseClass - 1.0F) * (value2 - value1);
        }

        private float NitrogenResponse(float availableNitrogen, float nitrogenK, float minimumNitrogen)
        {
            if (availableNitrogen <= minimumNitrogen)
            {
                return 0.0F;
            }
            float x = 1.0F - MathF.Exp(nitrogenK * (availableNitrogen - minimumNitrogen));
            return x;
        }

        /** calculation for the CO2 response for the ambientCO2 for the water- and nitrogen responses given.
            The calculation follows Friedlingsstein 1995 (see also links to equations in code)
            see also: http://iland.boku.ac.at/CO2+response
            @param ambientCO2 current CO2 concentration (ppm)
            @param nitrogenResponse (yearly) nitrogen response of the species
            @param soilWaterReponse soil water response (mean value for a month)
            */
        public double CarbonDioxideResponse(double ambientCO2, double nitrogenResponse, double soilWaterResponse)
        {
            if (nitrogenResponse == 0.0)
            {
                return 0.0;
            }

            double co2_water = 2.0 - soilWaterResponse;
            double beta = mCO2beta0 * co2_water * nitrogenResponse;

            double r = 1.0 + Constant.Ln2 * beta; // NPP increase for a doubling of atmospheric CO2 (Eq. 17)

            // fertilization function (cf. Farquhar, 1980) based on Michaelis-Menten expressions
            double deltaC = mCO2base - mCO2comp;
            double K2 = ((2 * mCO2base - mCO2comp) - r * deltaC) / ((r - 1.0) * deltaC * (2 * mCO2base - mCO2comp)); // Eq. 16
            double K1 = (1.0 + K2 * deltaC) / deltaC;

            double response = mCO2p0 * K1 * (ambientCO2 - mCO2comp) / (1 + K2 * (ambientCO2 - mCO2comp)); // Eq. 16
            return response;

        }

        /** calculates the lightResponse based on a value for LRI and the species lightResponseClass.
            LightResponse is classified from 1 (very shade inolerant) and 5 (very shade tolerant) and interpolated for values between 1 and 5.
            Returns a value between 0..1
            @sa http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth */
        public float LightResponse(Simulation.Model model, float lightResourceIndex, float lightResponseClass)
        {
            float intolerant = (float)mLightResponseIntolerant.Evaluate(model, lightResourceIndex);
            float tolerant = (float)mLightResponseTolerant.Evaluate(model, lightResourceIndex);
            float response = intolerant + 0.25F * (lightResponseClass - 1.0F) * (tolerant - intolerant);
            return Global.Limit(response, 0.0F, 1.0F);
        }
    }
}
