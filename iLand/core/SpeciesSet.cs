﻿using iLand.Input;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Xml;

namespace iLand.Core
{
    /** @class SpeciesSet
        A SpeciesSet acts as a container for individual Species objects. In iLand, theoretically,
        multiple species sets can be used in parallel.
        */
    public class SpeciesSet
    {
        private static readonly int mNRandomSets = 20;

        private readonly Dictionary<string, Species> mSpecies;
        // nitrogen response classes
        private double mNitrogen_1a, mNitrogen_1b; ///< parameters of nitrogen response class 1
        private double mNitrogen_2a, mNitrogen_2b; ///< parameters of nitrogen response class 2
        private double mNitrogen_3a, mNitrogen_3b; ///< parameters of nitrogen response class 3
        // CO2 response
        private double mCO2base, mCO2comp; ///< CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private double mCO2p0, mCO2beta0; ///< p0: production multiplier, beta0: relative productivity increase
        // Light Response classes
        private readonly Expression mLightResponseIntolerant; ///< light response function for the the most shade tolerant species
        private readonly Expression mLightResponseTolerant; ///< light response function for the most shade intolerant species
        private readonly Expression mLRICorrection; ///< function to modfiy LRI during read
        /// container holding the seed maps
        private readonly List<SeedDispersal> mSeedDispersal;

        public List<Species> ActiveSpecies { get; private set; } ///< list of species that are "active" (flag active in database)
        public string Name { get; private set; } ///< table name of the species set
        public List<int> RandomSpeciesOrder { get; private set; }
        public StampContainer ReaderStamps { get; private set; }

        public SpeciesSet()
        {
            this.ActiveSpecies = new List<Species>();
            this.mLightResponseIntolerant = new Expression();
            this.mLightResponseTolerant = new Expression();
            this.mLRICorrection = new Expression();
            this.RandomSpeciesOrder = new List<int>();
            this.ReaderStamps = new StampContainer();
            this.mSeedDispersal = new List<SeedDispersal>();
            this.mSpecies = new Dictionary<string, Species>();
        }

        public Species GetSpecies(string speciesID) { return mSpecies[speciesID]; }
        public double LriCorrection(double lightResourceIndex, double relativeHeight) { return mLRICorrection.Calculate(lightResourceIndex, relativeHeight); }
        public int SpeciesCount() { return mSpecies.Count; }

        public void Clear()
        {
            // BUGBUG: C++ doesn't clear other collections?
            mSpecies.Clear();
            mSeedDispersal.Clear();
            mSpecies.Clear();
            ActiveSpecies.Clear();
        }

        public Species Species(int index)
        {
            foreach (Species s in mSpecies.Values)
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
        public int Setup()
        {
            XmlHelper xml = GlobalSettings.Instance.Settings;
            string tableName = xml.GetString("model.species.source", "species");
            Name = tableName;
            string readerStampFile = xml.GetString("model.species.reader", "readerstamp.bin");
            readerStampFile = GlobalSettings.Instance.Path(readerStampFile, "lip");
            this.ReaderStamps.Load(readerStampFile);
            if (GlobalSettings.Instance.Settings.GetBooleanParameter("debugDumpStamps", false))
            {
                Debug.WriteLine(ReaderStamps.Dump());
            }

            this.Clear();
            using SqliteCommand speciesSelect = new SqliteCommand(String.Format("select * from {0}", tableName), GlobalSettings.Instance.DatabaseInput);
            // Debug.WriteLine("Loading species set from SQL table " + tableName + ".");
            using SpeciesReader speciesReader = new SpeciesReader(speciesSelect.ExecuteReader());
            while (speciesReader.Read())
            {
                bool isActive = speciesReader.Active();
                if (isActive == false)
                {
                    continue;
                }
                Species species = Core.Species.Load(speciesReader, this);
                mSpecies.Add(species.ID, species);
                if (species.Active)
                {
                    this.ActiveSpecies.Add(species);
                }
                Expression.AddSpecies(species.ID, species.Index);
            }

            Debug.WriteLine("Loaded " + mSpecies.Count + " active species.");
            //Debug.WriteLine("index, id, name");
            //foreach (Species s in this.ActiveSpecies)
            //{
            //    Debug.WriteLine(s.Index + " " + s.ID + " " + s.Name);
            //}

            // setup nitrogen response
            XmlHelper resp = new XmlHelper(xml.Node("model.species.nitrogenResponseClasses"));
            if (!resp.IsValid())
            {
                throw new XmlException("/project/model/species/nitrogenResponseClasses not present!");
            }
            this.mNitrogen_1a = resp.GetDouble("class_1_a");
            this.mNitrogen_1b = resp.GetDouble("class_1_b");
            this.mNitrogen_2a = resp.GetDouble("class_2_a");
            this.mNitrogen_2b = resp.GetDouble("class_2_b");
            this.mNitrogen_3a = resp.GetDouble("class_3_a");
            this.mNitrogen_3b = resp.GetDouble("class_3_b");
            if ((this.mNitrogen_1a == 0.0) || (this.mNitrogen_1b == 0.0) || 
                (this.mNitrogen_2a == 0.0) || (this.mNitrogen_2b == 0.0) ||
                (this.mNitrogen_3a == 0.0) || (this.mNitrogen_3b == 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/nitrogenResponseClasses/class_[1..3]_[a..b] is missing, less than zero, or zero.");
            }

            // setup CO2 response
            XmlHelper co2 = new XmlHelper(xml.Node("model.species.CO2Response"));
            this.mCO2base = co2.GetDouble("baseConcentration");
            this.mCO2comp = co2.GetDouble("compensationPoint");
            this.mCO2beta0 = co2.GetDouble("beta0");
            this.mCO2p0 = co2.GetDouble("p0");
            if ((this.mCO2base == 0.0) || (this.mCO2comp == 0.0) || (mCO2base - mCO2comp) == 0.0 || (this.mCO2beta0 == 0.0) || (this.mCO2p0 == 0.0))
            {
                throw new XmlException("At least one parameter of /project/model/species/CO2Response is missing, less than zero, or zero.");
            }

            // setup Light responses
            XmlHelper light = new XmlHelper(xml.Node("model.species.lightResponse"));
            mLightResponseTolerant.SetAndParse(light.GetString("shadeTolerant"));
            mLightResponseIntolerant.SetAndParse(light.GetString("shadeIntolerant"));
            mLightResponseTolerant.Linearize(0.0, 1.0);
            mLightResponseIntolerant.Linearize(0.0, 1.0);
            if (String.IsNullOrEmpty(mLightResponseTolerant.ExpressionString) || String.IsNullOrEmpty(mLightResponseIntolerant.ExpressionString))
            {
                throw new NotSupportedException("At least one parameter of /project/model/species/lightResponse is missing.");
            }
            // lri-correction
            mLRICorrection.SetAndParse(light.GetString("LRImodifier", "1"));
            // x: LRI, y: relative heigth
            mLRICorrection.Linearize(0.0, 1.0, 0.0, 1.0);

            CreateRandomSpeciesOrder();
            return mSpecies.Count;
        }

        public void SetupRegeneration()
        {
            SeedDispersal.SetupExternalSeeds();
            foreach (Species s in ActiveSpecies) 
            {
                s.SeedDispersal = new SeedDispersal(s);
                s.SeedDispersal.Setup(); // setup memory for the seed map (grid)
            }
            SeedDispersal.FinalizeExternalSeeds();
            // Debug.WriteLine("Setup of seed dispersal maps finished.");
        }

        public void SeedDistribution(Species species)
        {
            species.SeedDispersal.Execute();
        }

        public void Regeneration()
        {
            if (!GlobalSettings.Instance.Model.Settings.RegenerationEnabled)
            {
                return;
            }
            using DebugTimer t = new DebugTimer("SpeciesSet.Regeneration()");

            ThreadRunner runner = new ThreadRunner(ActiveSpecies); // initialize a thread runner object with all active species
            runner.Run(SeedDistribution);

            if (GlobalSettings.Instance.LogDebug())
            {
                Debug.WriteLine("seed dispersal finished.");
            }
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void NewYear()
        {
            if (!GlobalSettings.Instance.Model.Settings.RegenerationEnabled)
            {
                return;
            }
            foreach (Species s in ActiveSpecies) 
            {
                s.NewYear();
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

        public void GetRandomSpeciesSampleIndices(out int beginIndex, out int endIndex)
        {
            beginIndex = this.ActiveSpecies.Count * RandomGenerator.Random(0, mNRandomSets - 1);
            endIndex = beginIndex + this.ActiveSpecies.Count;
        }

        private void CreateRandomSpeciesOrder()
        {
            RandomSpeciesOrder.Clear();
            RandomSpeciesOrder.Capacity = ActiveSpecies.Count * mNRandomSets;
            for (int i = 0; i < mNRandomSets; ++i)
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
                    int index = RandomGenerator.Random(0, samples.Count);
                    RandomSpeciesOrder.Add(samples[index]);
                    samples.RemoveAt(index);
                }
            }
        }

        private double NitrogenResponse(double availableNitrogen, double NA, double NB)
        {
            if (availableNitrogen <= NB)
            {
                return 0;
            }
            double x = 1.0 - Math.Exp(NA * (availableNitrogen - NB));
            return x;
        }

        /// calculate nitrogen response for a given amount of available nitrogen and a respone class
        /// for fractional values, the response value is interpolated between the fixedly defined classes (1,2,3)
        public double NitrogenResponse(double availableNitrogen, double responseClass)
        {
            if (responseClass > 2.0)
            {
                if (responseClass == 3.0)
                {
                    return NitrogenResponse(availableNitrogen, mNitrogen_3a, mNitrogen_3b);
                }
                else
                {
                    // interpolate between 2 and 3
                    double value4 = NitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
                    double value3 = NitrogenResponse(availableNitrogen, mNitrogen_3a, mNitrogen_3b);
                    return value4 + (responseClass - 2) * (value3 - value4);
                }
            }
            if (responseClass == 2.0)
            {
                return NitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
            }
            if (responseClass == 1.0)
            {
                return NitrogenResponse(availableNitrogen, mNitrogen_1a, mNitrogen_1b);
            }
            // last ressort: interpolate between 1 and 2
            double value1 = NitrogenResponse(availableNitrogen, mNitrogen_1a, mNitrogen_1b);
            double value2 = NitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
            return value1 + (responseClass - 1) * (value2 - value1);
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
        public double LightResponse(double lightResourceIndex, double lightResponseClass)
        {
            double low = mLightResponseIntolerant.Calculate(lightResourceIndex);
            double high = mLightResponseTolerant.Calculate(lightResourceIndex);
            double result = low + 0.25 * (lightResponseClass - 1.0) * (high - low);
            return Global.Limit(result, 0.0, 1.0);
        }
    }
}
