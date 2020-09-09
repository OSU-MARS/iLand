using iLand.tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    /** @class SpeciesSet
        A SpeciesSet acts as a container for individual Species objects. In iLand, theoretically,
        multiple species sets can be used in parallel.
        */
    internal class SpeciesSet
    {
        private static int mNRandomSets = 20;

        private string mName;
        private List<Species> mActiveSpecies; ///< list of species that are "active" (flag active in database)
        private Dictionary<string, Species> mSpecies;
        private List<int> mRandomSpeciesOrder;
        private SqliteDataReader mDataReader;
        private StampContainer mReaderStamp;
        // nitrogen response classes
        private double mNitrogen_1a, mNitrogen_1b; ///< parameters of nitrogen response class 1
        private double mNitrogen_2a, mNitrogen_2b; ///< parameters of nitrogen response class 2
        private double mNitrogen_3a, mNitrogen_3b; ///< parameters of nitrogen response class 3
        // CO2 response
        private double mCO2base, mCO2comp; ///< CO2 concentration of measurements (base) and CO2 compensation point (comp)
        private double mCO2p0, mCO2beta0; ///< p0: production multiplier, beta0: relative productivity increase
        // Light Response classes
        private Expression mLightResponseIntolerant; ///< light response function for the the most shade tolerant species
        private Expression mLightResponseTolerant; ///< light response function for the most shade intolerant species
        private Expression mLRICorrection; ///< function to modfiy LRI during read
                                           /// container holding the seed maps
        private List<SeedDispersal> mSeedDispersal;

        public string name() { return mName; } ///< table name of the species set
        // access
        public List<Species> activeSpecies() { return mActiveSpecies; } ///< list of species that are "active" (flag active in database)
        public Species species(string speciesId) { return mSpecies[speciesId]; }
        public StampContainer readerStamps() { return mReaderStamp; }
        public int count() { return mSpecies.Count; }
        /// return 2 iterators. The range between 'rBegin' and 'rEnd' are indices of the current species set (all species are included, order is random).
        // calculations
        public double LRIcorrection(double lightResourceIndex, double relativeHeight) { return mLRICorrection.calculate(lightResourceIndex, relativeHeight); }

        public SpeciesSet()
        {
            mDataReader = null;
        }

        public void clear()
        {
            mSpecies.Clear();
            mSeedDispersal.Clear();
            mSpecies.Clear();
            mActiveSpecies.Clear();
        }

        public Species species(int index)
        {
            foreach (Species s in mSpecies.Values)
            {
                if (s.index() == index)
                {
                    return s;
                }
            }
            return null;
        }

        /** loads active species from a database table and creates/setups the species.
            The function uses the global database-connection.
          */
        public int setup()
        {
            XmlHelper xml = GlobalSettings.instance().settings();
            string tableName = xml.value("model.species.source", "species");
            mName = tableName;
            string readerFile = xml.value("model.species.reader", "reader.bin");
            readerFile = GlobalSettings.instance().path(readerFile, "lip");
            mReaderStamp.load(readerFile);
            if (GlobalSettings.instance().settings().paramValueBool("debugDumpStamps", false))
            {
                Debug.WriteLine(mReaderStamp.dump());
            }

            using SqliteCommand query = new SqliteCommand(String.Format("select * from {0}", tableName), GlobalSettings.instance().dbin());
            clear();
            Debug.WriteLine("attempting to load a species set from " + tableName);
            using SqliteDataReader queryReader = query.ExecuteReader();
            for (; queryReader.HasRows; queryReader.Read())
            {
                if ((int)var("active") == 0)
                {
                    continue;
                }
                Species s = new Species(this); // create
                                               // call setup routine (which calls var() to retrieve values
                s.setup();

                mSpecies.Add(s.id(), s); // store
                if (s.active())
                {
                    mActiveSpecies.Add(s);
                }
                Expression.addConstant(s.id(), s.index());
            } // while query.next()
            Debug.WriteLine("loaded " + mSpecies.Count + " active species:");
            Debug.WriteLine("index, id, name");
            foreach (Species s in mActiveSpecies)
            {
                Debug.WriteLine(s.index() + " " + s.id() + " " + s.name());
            }
            mDataReader = null;

            // setup nitrogen response
            XmlHelper resp = new XmlHelper(xml.node("model.species.nitrogenResponseClasses"));
            if (!resp.isValid())
            {
                throw new NotSupportedException("model.species.nitrogenResponseClasses not present!");
            }
            mNitrogen_1a = resp.valueDouble("class_1_a");
            mNitrogen_1b = resp.valueDouble("class_1_b");
            mNitrogen_2a = resp.valueDouble("class_2_a");
            mNitrogen_2b = resp.valueDouble("class_2_b");
            mNitrogen_3a = resp.valueDouble("class_3_a");
            mNitrogen_3b = resp.valueDouble("class_3_b");
            if (mNitrogen_1a * mNitrogen_1b * mNitrogen_2a * mNitrogen_2b * mNitrogen_3a * mNitrogen_3b == 0)
            {
                throw new NotSupportedException("at least one parameter of model.species.nitrogenResponseClasses is not valid (value=0)!");
            }

            // setup CO2 response
            XmlHelper co2 = new XmlHelper(xml.node("model.species.CO2Response"));
            mCO2base = co2.valueDouble("baseConcentration");
            mCO2comp = co2.valueDouble("compensationPoint");
            mCO2beta0 = co2.valueDouble("beta0");
            mCO2p0 = co2.valueDouble("p0");
            if (mCO2base * mCO2comp * (mCO2base - mCO2comp) * mCO2beta0 * mCO2p0 == 0)
            {
                throw new NotSupportedException("at least one parameter of model.species.CO2Response is not valid!");
            }

            // setup Light responses
            XmlHelper light = new XmlHelper(xml.node("model.species.lightResponse"));
            mLightResponseTolerant.setAndParse(light.value("shadeTolerant"));
            mLightResponseIntolerant.setAndParse(light.value("shadeIntolerant"));
            mLightResponseTolerant.linearize(0.0, 1.0);
            mLightResponseIntolerant.linearize(0.0, 1.0);
            if (String.IsNullOrEmpty(mLightResponseTolerant.expression()) || String.IsNullOrEmpty(mLightResponseIntolerant.expression()))
            {
                throw new NotSupportedException("at least one parameter of model.species.lightResponse is empty!");
            }
            // lri-correction
            mLRICorrection.setAndParse(light.value("LRImodifier", "1"));
            // x: LRI, y: relative heigth
            mLRICorrection.linearize2d(0.0, 1.0, 0.0, 1.0);

            createRandomSpeciesOrder();
            return mSpecies.Count;
        }

        public void setupRegeneration()
        {
            SeedDispersal.setupExternalSeeds();
            foreach (Species s in mActiveSpecies) 
            {
                SeedDispersal sd = new SeedDispersal(s);
                sd.setup(); // setup memory for the seed map (grid)
                s.setSeedDispersal(sd); // establish the link between species and the map
            }
            SeedDispersal.finalizeExternalSeeds();
            Debug.WriteLine("Setup of seed dispersal maps finished.");
        }

        public void nc_seed_distribution(Species species)
        {
            species.seedDispersal().execute();
        }

        public void regeneration()
        {
            if (!GlobalSettings.instance().model().settings().regenerationEnabled)
            {
                return;
            }
            using DebugTimer t = new DebugTimer("seed dispersal (all species)");

            ThreadRunner runner = new ThreadRunner(mActiveSpecies); // initialize a thread runner object with all active species
            runner.run(nc_seed_distribution);

            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("seed dispersal finished.");
            }
        }

        /** newYear is called by Model::runYear at the beginning of a year before any growth occurs.
          This is used for various initializations, e.g. to clear seed dispersal maps
          */
        public void newYear()
        {
            if (!GlobalSettings.instance().model().settings().regenerationEnabled)
            {
                return;
            }
            foreach (Species s in mActiveSpecies) 
            {
                s.newYear();
            }
        }

        /** retrieves variables from the datasource available during the setup of species.
          */
        public object var(string varName)
        {
            Debug.Assert(mDataReader != null);

            int idx = mDataReader.GetOrdinal(varName);
            if (idx >= 0)
            {
                return mDataReader.GetValue(idx);
            }
            throw new NotSupportedException("SpeciesSet: variable not set: " + varName);
            //throw new NotSupportedException(string("load species parameter: field {0} not found!").arg(varName));
            // lookup in defaults
            //Debug.WriteLine("variable" << varName << "not found - using default.";
            //return GlobalSettings.instance().settingDefaultValue(varName);
        }

        public void randomSpeciesOrder(out int rBegin, out int rEnd)
        {
            int iset = RandomGenerator.irandom(0, mNRandomSets);
            rBegin = iset * mActiveSpecies.Count;
            rEnd = rBegin + mActiveSpecies.Count;
        }

        private void createRandomSpeciesOrder()
        {
            mRandomSpeciesOrder.Clear();
            mRandomSpeciesOrder.Capacity = mActiveSpecies.Count * mNRandomSets;
            for (int i = 0; i < mNRandomSets; ++i)
            {
                List<int> samples = new List<int>(mActiveSpecies.Count);
                // fill list
                foreach (Species s in mActiveSpecies)
                {
                    samples.Add(s.index());
                }
                // sample and reduce list
                while (samples.Count > 0)
                {
                    int index = RandomGenerator.irandom(0, samples.Count);
                    mRandomSpeciesOrder.Add(samples[index]);
                    samples.RemoveAt(index);
                }
            }
        }

        private double nitrogenResponse(double availableNitrogen, double NA, double NB)
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
        public double nitrogenResponse(double availableNitrogen, double responseClass)
        {
            if (responseClass > 2.0)
            {
                if (responseClass == 3.0)
                {
                    return nitrogenResponse(availableNitrogen, mNitrogen_3a, mNitrogen_3b);
                }
                else
                {
                    // interpolate between 2 and 3
                    double value4 = nitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
                    double value3 = nitrogenResponse(availableNitrogen, mNitrogen_3a, mNitrogen_3b);
                    return value4 + (responseClass - 2) * (value3 - value4);
                }
            }
            if (responseClass == 2.0)
            {
                return nitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
            }
            if (responseClass == 1.0)
            {
                return nitrogenResponse(availableNitrogen, mNitrogen_1a, mNitrogen_1b);
            }
            // last ressort: interpolate between 1 and 2
            double value1 = nitrogenResponse(availableNitrogen, mNitrogen_1a, mNitrogen_1b);
            double value2 = nitrogenResponse(availableNitrogen, mNitrogen_2a, mNitrogen_2b);
            return value1 + (responseClass - 1) * (value2 - value1);
        }

        /** calculation for the CO2 response for the ambientCO2 for the water- and nitrogen responses given.
            The calculation follows Friedlingsstein 1995 (see also links to equations in code)
            see also: http://iland.boku.ac.at/CO2+response
            @param ambientCO2 current CO2 concentration (ppm)
            @param nitrogenResponse (yearly) nitrogen response of the species
            @param soilWaterReponse soil water response (mean value for a month)
*/
        public double co2Response(double ambientCO2, double nitrogenResponse, double soilWaterResponse)
        {
            if (nitrogenResponse == 0.0)
            {
                return 0.0;
            }

            double co2_water = 2.0 - soilWaterResponse;
            double beta = mCO2beta0 * co2_water * nitrogenResponse;

            double r = 1.0 + Constant.M_LN2 * beta; // NPP increase for a doubling of atmospheric CO2 (Eq. 17)

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
        public double lightResponse(double lightResourceIndex, double lightResponseClass)
        {
            double low = mLightResponseIntolerant.calculate(lightResourceIndex);
            double high = mLightResponseTolerant.calculate(lightResourceIndex);
            double result = low + 0.25 * (lightResponseClass - 1.0) * (high - low);
            return Global.limit(result, 0.0, 1.0);
        }
    }
}
