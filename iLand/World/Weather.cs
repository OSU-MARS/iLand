using iLand.Input.ProjectFile;
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    // Handles weather input data and performs some basic related calculations on that data.
    // http://iland-model.org/ClimateData
    public abstract class Weather
    {
        protected int CurrentDataYear { get; set; } // current year in weather data cached in memory (relative); one less than GlobalSettings.CurrentYear
        protected bool DoRandomSampling { get; private init; } // if true, the sequence of years is randomized
        protected int RandomListIndex { get; set; } // current index of the randomYearList for random sampling
        protected List<int> RandomYearList { get; private init; } // for random sampling of years
        protected List<int> SampledYears { get; private init; } // list of sampled years to use
        protected List<Phenology> TreeSpeciesPhenology { get; private init; } // phenology calculations
        protected int YearsToLoad { get; private init; } // number of years to load from database

        public float AtmosphericCO2ConcentrationInPpm { get; protected set; }
        public float MeanAnnualTemperature { get; protected set; } // °C
        public float[] PrecipitationByMonth { get; private init; } // mm
        public Sun Sun { get; private init; } // solar radiation class
        public float[] TemperatureByMonth { get; private init; } // °C
        public float TotalAnnualRadiation { get; protected set; } // return radiation sum (MJ/m²) of the whole year

        public Weather(Project projectFile)
        {
            this.CurrentDataYear = -1; // start with -1 as the first call to NextYear() will go to year 0
            this.DoRandomSampling = projectFile.World.Weather.RandomSamplingEnabled;
            this.TreeSpeciesPhenology = new();
            this.RandomListIndex = -1;
            this.RandomYearList = new();
            this.SampledYears = new();
            this.YearsToLoad = projectFile.World.Weather.BatchYears;

            this.PrecipitationByMonth = new float[Constant.MonthsInYear];
            this.Sun = new Sun();
            this.Sun.Setup(Maths.ToRadians(projectFile.World.Geometry.Latitude));
            this.TemperatureByMonth = new float[Constant.MonthsInYear];

            if (this.DoRandomSampling)
            {
                string? list = projectFile.World.Weather.RandomSamplingList;
                if (String.IsNullOrEmpty(list) == false)
                {
                    List<string> strlist = Regex.Split(list, "\\W+").ToList();
                    foreach (string s in strlist)
                    {
                        this.RandomYearList.Add(Int32.Parse(s));
                    }
                    // check for validity
                    foreach (int year in this.RandomYearList)
                    {
                        if (year < 0 || year >= this.YearsToLoad)
                        {
                            throw new NotSupportedException("Invalid randomSamplingList! Year numbers are 0-based and must to between 0 and batchYears-1 (check value of batchYears)!!!");
                        }
                    }
                }
            }

            this.SetupTreePhenology(projectFile);
        }

        /// annual precipitation sum (mm)
        public float GetTotalPrecipitationInCurrentYear() 
        { 
            float totalPrecip = 0.0F;
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                totalPrecip += this.PrecipitationByMonth[month];
            }
            return totalPrecip; 
        }

        public abstract void OnStartYear(Model model);

        // phenology class of given type
        public Phenology GetPhenology(int phenologyIndex)
        {
            if (phenologyIndex >= this.TreeSpeciesPhenology.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(phenologyIndex), "Phenology group " + phenologyIndex + "not present. Is /project/model/species/phenology missing elements?");
            }

            Phenology phenology = this.TreeSpeciesPhenology[phenologyIndex];
            if (phenology.LeafType == phenologyIndex)
            {
                return phenology;
            }

            // search...
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; index++)
            {
                phenology = this.TreeSpeciesPhenology[phenologyIndex];
                if (phenology.LeafType == phenologyIndex)
                {
                    return phenology;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(phenologyIndex), String.Format("Error at SpeciesSet::phenology(): invalid group: {0}", phenologyIndex));
        }

        // setup of phenology groups
        private void SetupTreePhenology(Project project)
        {
            this.TreeSpeciesPhenology.Clear();
            this.TreeSpeciesPhenology.Add(new Phenology((WeatherDaily)this)); // id=0

            // TODO: remove PhenologyType and make Phenology XML serializable
            foreach (PhenologyType phenology in project.World.Species.Phenology)
            {
                if (phenology.ID < 0)
                {
                    throw new XmlException("Invalid leaf type ID " + phenology.ID + ".");
                }
                Phenology phenologyForSpecies = new(phenology.ID,
                                                    (WeatherDaily)this, 
                                                    phenology.VpdMin,
                                                    phenology.VpdMax,
                                                    phenology.DayLengthMin,
                                                    phenology.DayLengthMax,
                                                    phenology.TempMin,
                                                    phenology.TempMax);
                this.TreeSpeciesPhenology.Add(phenologyForSpecies);
            } 
        }
    }
}
