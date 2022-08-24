﻿using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LeafPhenology = iLand.Tree.LeafPhenology;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    // A weather input time series and calculations on that weather data. May be 1:1 with resource units or may be 1 weather:many resource units
    // (leaf on-off phenology is therefore a Weather member rather than kept at the resource unit level).
    // http://iland-model.org/ClimateData
    public abstract class Weather
    {
        protected int CurrentDataYear { get; set; } // current year in weather data cached in memory (relative); one less than GlobalSettings.CurrentYear
        protected bool DoRandomSampling { get; private init; } // if true, the sequence of years is randomized
        protected int RandomListIndex { get; set; } // current index of the randomYearList for random sampling
        protected List<int> RandomYearList { get; private init; } // for random sampling of years
        protected List<int> SampledYears { get; private init; } // list of sampled years to use
        protected List<LeafPhenology> TreeSpeciesPhenology { get; private init; } // phenology calculations
        protected int YearsToLoad { get; private init; } // number of years to load from database

        public CO2TimeSeriesMonthly CO2ByMonth { get; protected init; } // ppm
        public float MeanAnnualTemperature { get; protected set; } // °C
        public float[] PrecipitationByMonth { get; private init; } // mm
        public Sun Sun { get; private init; } // solar radiation class
        public float[] DaytimeMeanTemperatureByMonth { get; private init; } // °C
        public float TotalAnnualRadiation { get; protected set; } // return radiation sum (MJ/m²) of the whole year

        protected Weather(Project projectFile)
        {
            this.CurrentDataYear = -1; // start with -1 as the first call to NextYear() will go to year 0
            this.DoRandomSampling = projectFile.World.Weather.RandomSamplingEnabled;
            this.TreeSpeciesPhenology = new();
            this.RandomListIndex = -1;
            this.RandomYearList = new();
            this.SampledYears = new();
            this.YearsToLoad = projectFile.World.Weather.DailyWeatherChunkSizeInYears;

            string co2filePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Weather.CO2File);
            string? co2fileExtension = Path.GetExtension(projectFile.World.Weather.CO2File);
            CO2ReaderMonthly co2reader = co2fileExtension switch
            {
                Constant.File.CsvExtension => new CO2ReaderMonthlyCsv(co2filePath, Constant.Data.DefaultMonthlyAllocationIncrement),
                Constant.File.FeatherExtension => new CO2ReaderMonthlyFeather(co2filePath, Constant.Data.DefaultMonthlyAllocationIncrement),
                _ => throw new NotSupportedException("Unhandled CO₂ file extension '" + co2fileExtension + "'.")
            };

            this.CO2ByMonth = co2reader.MonthlyCO2;
            this.PrecipitationByMonth = new float[Constant.MonthsInYear];
            this.Sun = new(projectFile.World.Geometry.Latitude);
            this.DaytimeMeanTemperatureByMonth = new float[Constant.MonthsInYear];

            if (this.DoRandomSampling)
            {
                string? list = projectFile.World.Weather.RandomSamplingList;
                if (String.IsNullOrEmpty(list) == false)
                {
                    List<string> strlist = Regex.Split(list, "\\W+").ToList();
                    foreach (string s in strlist)
                    {
                        this.RandomYearList.Add(Int32.Parse(s, CultureInfo.InvariantCulture));
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
        }

        public abstract WeatherTimeSeries TimeSeries
        {
            get;
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
        public Tree.LeafPhenology GetPhenology(int phenologyID)
        {
            if (phenologyID >= this.TreeSpeciesPhenology.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(phenologyID), "Phenology group " + phenologyID + "not present. Is /project/model/species/phenology missing elements?");
            }

            Tree.LeafPhenology phenology = this.TreeSpeciesPhenology[phenologyID];
            if (phenology.ID == phenologyID)
            {
                return phenology;
            }

            // search...
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; index++)
            {
                phenology = this.TreeSpeciesPhenology[phenologyID];
                if (phenology.ID == phenologyID)
                {
                    return phenology;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(phenologyID), String.Format("Error at SpeciesSet::phenology(): invalid group: {0}", phenologyID));
        }
    }

    public abstract class Weather<TWeatherTimeSeries> : Weather where TWeatherTimeSeries : WeatherTimeSeries
    {
        private readonly TWeatherTimeSeries timeSeries;

        protected Weather(Project projectFile, TWeatherTimeSeries timeSeries) 
            : base(projectFile)
        {
            this.timeSeries = timeSeries;

            // populate leaf phenology groups
            this.TreeSpeciesPhenology.Add(LeafPhenology<TWeatherTimeSeries>.CreateEvergreen(this));
            foreach (Input.ProjectFile.LeafPhenology phenology in projectFile.World.Species.Phenology)
            {
                if (phenology.ID < 1)
                {
                    throw new XmlException("Invalid phenology ID " + phenology.ID + " (ID 0 is reserved for evergreen leaves retained year round).");
                }
                LeafPhenology<TWeatherTimeSeries> phenologyForSpecies = new(this, phenology);
                this.TreeSpeciesPhenology.Add(phenologyForSpecies);
            }
        }

        public override TWeatherTimeSeries TimeSeries
        {
            get { return this.timeSeries; }
        }
    }
}
