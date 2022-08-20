﻿using iLand.Input; // used in release builds
using iLand.Input.Weather; // used in release builds
using iLand.Input.Tree; // used in release builds
using iLand.Input.ProjectFile; // used in release builds
using iLand.Output.Memory;
using iLand.Tree;
using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Weather = iLand.World.Weather;
using Model = iLand.Simulation.Model;
using SQLitePCL;
using Newtonsoft.Json.Linq;

namespace iLand.Test
{
    [TestClass]
    public class ModelTest : LandTest
    {
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Elliott State Research Forest, southwestern Oregon, USA. Individual tree segmentation of 2021 Coos County Oregon LiDAR 
        /// Consortium tiles s04110w07050 and s04110w07020 (two adjacent tiles in the northeastern Elliott out of 501 tiles covering
        /// the forest).
        /// </summary>
        [TestMethod]
        public void Elliott()
        {
            // 4 km: 81 weather cells in available, 37 of which cover the unbuffered Elliott
            // 200 m: 20,440 weather cells available, 50 of which fall within unit test window
            int expectedWeatherIDs200m = 50;
            int expectedWeatherTimesteps = 12 * (2100 - 2022 + 1); // data files start in 2011 but Elliott.xml sets start year to 2022
            int expectedResourceUnitCount = 190; // unit test window covers 190 resource units, 34494 resource units cover the Elliott (unbuffered)
            for (int reliabilityIteration = 0; reliabilityIteration < 1; ++reliabilityIteration)
            {
                using Model elliott = LandTest.LoadProject(LandTest.GetElliottProjectPath(this.TestContext!)); // ~4 seconds in debug
                Assert.IsTrue(elliott.Landscape.ResourceUnits.Count == expectedResourceUnitCount);
                ModelTest.VerifyElliottModel(elliott);
                ModelTest.VerifyElliottOutput(elliott, simulationYear: 0);

                // check .feather weather read from weather file path in Elliott project file
                Assert.IsTrue(elliott.Landscape.WeatherByID.Count == expectedWeatherIDs200m);
                foreach (Weather monthlyWeather in elliott.Landscape.WeatherByID.Values)
                {
                    Assert.IsTrue(monthlyWeather.TimeSeries.Count == expectedWeatherTimesteps);
                }

                // located tracked resource units
                ObservedResourceUnitTrajectory observedTrajectory82597 = new() // wholly contained within stand 78
                {
                    NonmonotonicGrowthTolerance = 0.02F,
                    NppTolerance = 0.04F,
                    StemVolumeTolerance = 0.02F,
                    TreeNppTolerance = 0.05F
                };

                ResourceUnit? resourceUnit82597 = null;
                int resourceUnit82597index = -1;
                for (int resourceUnitIndex = 0; resourceUnitIndex < elliott.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
                {
                    ResourceUnit resourceUnit = elliott.Landscape.ResourceUnits[resourceUnitIndex];
                    if (resourceUnit.ID == 82597)
                    {
                        resourceUnit82597 = resourceUnit;
                        resourceUnit82597index = resourceUnitIndex;
                        break;
                    }
                }
                Assert.IsTrue(resourceUnit82597 != null);
                observedTrajectory82597.AddYear(resourceUnit82597); // add initial observations (simulation year 0)

                // run as many singlethreaded timesteps as are resonable for a unit test with debug build performance
                // 10 years, two tree tiles: ~1.3 Zen 3 core-seconds per iteration in debug (most of time is in project loading)
                for (int simulationYear = 1; simulationYear < 11; ++simulationYear)
                {
                    elliott.RunYear();
                    observedTrajectory82597.AddYear(resourceUnit82597);

                    ModelTest.VerifyElliottOutput(elliott, simulationYear);
                }

                ModelTest.VerifyElliottResourceUnits(elliott);
                ModelTest.VerifyElliottWeather(elliott);

                // regex for reformatting copy/paste of values from watch window: "\s+\[\d+]\s+(\d+.\d{1,3})\d*\s+float\r?\n" -> "$1F, "
                // Values are relatively sensitive to stochastic mortality's influences on stand trajectory. Test should reliably pass but
                // changes to random number generation appear likely to require expected values be updated.
                List<float> expectedGppBySimulationYear = new()
                {
                    0.0F, 15.450F, 15.564F, 15.453F, 15.706F, 15.383F, 
                    15.701F, 15.846F, 15.707F, 15.792F, 15.869F
                };
                List<float> expectedNppBySimulationYear = new()
                {
                    0.0F, 50746.117F, 51789.003F, 51903.867F, 53107.093F, 52297.18F, 
                    53638F, 54336.765F, 53995.175F, 54398.253F, 54749.98F
                };
                List<float> expectedStemVolumeBySimulationYear = new()
                {
                    1166.786F, 1221.545F, 1276.808F, 1331.800F, 1388.283F, 1442.904F, 
                    1499.338F, 1556.354F, 1612.148F, 1668.454F, 1725.025F
                };
                    
                ResourceUnitAllSpeciesTrajectory? resourceUnit82597trajectory = elliott.Output.ResourceUnitTrajectories[resourceUnit82597index].AllTreeSpeciesTrajectory;
                observedTrajectory82597.Verify(resourceUnit82597trajectory, 126.0F, expectedGppBySimulationYear, expectedNppBySimulationYear, expectedStemVolumeBySimulationYear);

                // verify Pacific Northwest tree species loading
                TreeSpeciesSet pnwSpecies = elliott.Landscape.SpeciesSetsByTableName[Constant.Data.DefaultSpeciesTable];
                TreeSpecies abam = pnwSpecies["abam"];
                TreeSpecies abgr = pnwSpecies["abgr"];
                TreeSpecies abpr = pnwSpecies["abpr"];
                TreeSpecies acma = pnwSpecies["acma"];
                TreeSpecies alru = pnwSpecies["alru"];
                TreeSpecies pisi = pnwSpecies["pisi"];
                TreeSpecies pipo = pnwSpecies["pipo"]; // TODO: what ecoregion are parameters for?
                TreeSpecies psme = pnwSpecies["psme"];
                TreeSpecies tshe = pnwSpecies["tshe"];
                TreeSpecies tsme = pnwSpecies["tsme"];
                TreeSpecies thpl = pnwSpecies["thpl"];

                Assert.IsTrue(pnwSpecies.ActiveSpecies.Count == 11);
                Assert.IsTrue(pnwSpecies.Count == 11);
                Assert.IsTrue((abam.ID == "abam") && (abgr.ID == "abgr") && (abpr.ID == "abpr") && (acma.ID == "acma") && (alru.ID == "alru") && 
                              (pisi.ID == "pisi") && (pipo.ID == "pipo") && (psme.ID == "psme") && (tshe.ID == "tshe") && (tsme.ID == "tsme") && 
                              (thpl.ID == "thpl"));
                ModelTest.VerifyDouglasFirPacificNorthwest(elliott);

                // in the interests of test runtime, limit larger file testing to release builds
                #if !DEBUG
                int availableResourceUnits = 34494;
                int availableWeatherIDs4km = 81; // 81 4 km weather cells in input, 37 of which cover the unbuffered Elliott
                int availableWeatherTimesteps = 12 * (2100 - 2011 + 1);

                // check full resource unit reads (basic .csv functionality is covered on Kalkalpen, Elliot project uses .feather subset)
                ResourceUnitEnvironment defaultEnvironment = new(elliott.Project.World);

                string resourceUnitCsvPath = elliott.Project.GetFilePath(ProjectDirectory.Gis, "resource units unbuffered 4 km weather.csv");
                ResourceUnitReaderCsv resourceUnitCsvReader = new(resourceUnitCsvPath, defaultEnvironment);
                Assert.IsTrue(resourceUnitCsvReader.Environments.Count == availableResourceUnits);

                string resourceUnitFeatherPath = elliott.Project.GetFilePath(ProjectDirectory.Gis, "resource units unbuffered 4 km weather.feather");
                ResourceUnitReaderFeather resourceUnitFeatherReader = new(resourceUnitFeatherPath, defaultEnvironment);
                Assert.IsTrue(resourceUnitFeatherReader.Environments.Count == availableResourceUnits);

                // check monthly .csv weather read
                string weatherFeatherFilePath = elliott.Project.GetFilePath(ProjectDirectory.Database, "weather 4 km 2011-2100 13GCMssp370.csv");
                WeatherReaderMonthlyCsv weatherFeatherReader = new(weatherFeatherFilePath, startYear: 2010); // should have no effect: actual start year in file is 2011
                Assert.IsTrue(weatherFeatherReader.MonthlyWeatherByID.Count == availableWeatherIDs4km);
                foreach (WeatherTimeSeriesMonthly monthlyWeatherTimeSeries in weatherFeatherReader.MonthlyWeatherByID.Values)
                {
                    Assert.IsTrue(monthlyWeatherTimeSeries.Count == availableWeatherTimesteps);
                }

                // check individual tree .csv read for a tile (Elliott project uses .feather)
                string individualTreeTilePath = elliott.Project.GetFilePath(ProjectDirectory.Init, "TSegD_H10Cr20h10A50MD7_s04110w07020.csv");
                IndividualTreeReader individualTreeReader = (IndividualTreeReader)TreeReader.Create(individualTreeTilePath);
                Assert.IsTrue(individualTreeReader.HeightInM.Count == 13124);
                #endif
            }
        }

        /// <summary>
        /// Kalkalpen National Park, Austria
        /// </summary>
        [TestMethod]
        public void Kalkalpen()
        {
            for (int reliabilityIteration = 0; reliabilityIteration < 1 /* 100 */; ++reliabilityIteration)
            {
                using Model kalkalpen = LandTest.LoadProject(LandTest.GetKalkalpenProjectPath(this.TestContext!));
                ModelTest.VerifyKalkalpenResourceUnits(kalkalpen, afterTimestep: false);
                ModelTest.VerifyKalkalpenModel(kalkalpen);
                ModelTest.VerifyNorwaySpruce(kalkalpen);

                ObservedResourceUnitTrees startOfYearTrees = new();
                ObservedResourceUnitTrees endOfYearTrees = new();

                ResourceUnit resourceUnit1 = kalkalpen.Landscape.ResourceUnits[0];
                endOfYearTrees.ObserveResourceUnit(resourceUnit1);
                for (int simulationYear = 0; simulationYear < 3; ++simulationYear)
                {
                    if (startOfYearTrees.DiameterInCmByTreeID.Count == 0)
                    {
                        startOfYearTrees.ObserveResourceUnit(resourceUnit1);
                    }

                    kalkalpen.RunYear();

                    ModelTest.VerifyKalkalpenResourceUnits(kalkalpen, afterTimestep: true);

                    endOfYearTrees.ObserveResourceUnit(resourceUnit1);
                    ModelTest.VerifyKalkalpenResourceUnitTrees(kalkalpen.Landscape.ResourceUnits, simulationYear, startOfYearTrees, endOfYearTrees);
                    ModelTest.VerifyLightAndHeightGrids(kalkalpen.Landscape, maxHeight: 45.0F + 0.1F * simulationYear);

                    (startOfYearTrees, endOfYearTrees) = (endOfYearTrees, startOfYearTrees);
                }
            }

            //RumpleIndex rumpleIndex = new();
            //rumpleIndex.Calculate(kalkalpen);
            //float index = rumpleIndex.Value(kalkalpen);
            //Assert.IsTrue(MathF.Abs(index - 0.0) < 0.001);

            // check calculation: numbers for Jenness paper
            //ReadOnlySpan<float> hs = stackalloc float[] { 165, 170, 145, 160, 183, 155, 122, 175, 190 };
            //float area = rumpleIndex.CalculateSurfaceArea(hs, 100);
        }

        [TestMethod]
        public void MalcolmKnapp14()
        {
            using Model plot14 = LandTest.LoadProject(LandTest.GetMalcolmKnappProjectPath(TestConstant.MalcolmKnapp.Plot14));
            ModelTest.VerifyMalcolmKnappResourceUnit(plot14);

            ObservedResourceUnitTrajectory observedTrajectory = new();

            ResourceUnit resourceUnit = plot14.Landscape.ResourceUnits[0];
            observedTrajectory.AddYear(resourceUnit);
            for (int simulationYear = 1; simulationYear < 29; ++simulationYear)
            {
                plot14.RunYear();

                observedTrajectory.AddYear(resourceUnit);
                Assert.IsTrue(plot14.Landscape.ResourceUnits.Count == 1);
                Assert.IsTrue(plot14.Landscape.ResourceUnits[0].Trees.TreeStatisticsByStandID.Count == 1);
                Assert.IsTrue(plot14.Landscape.ResourceUnits[0].Trees.TreeStatisticsByStandID.ContainsKey(14));
            }

            ModelTest.VerifyMalcolmKnappModel(plot14);
            ModelTest.VerifyMalcolmKnappWeather(plot14);
            ModelTest.VerifyDouglasFirPacificNorthwest(plot14);

            // regex for reformatting copy/paste of values from watch window: "\s+\[\d+]\s+(\d+.\d{1,3})\d*\s+float\r?\n" -> "$1F, "
            // Values are relatively sensitive to stochastic mortality's influences on stand trajectory. Test should reliably pass but
            // changes to random number generation appear likely to require expected values be updated.
            List<float> expectedGppByYear = new()
            {
                // with input data in NAD83 / BC Albers (EPSG:3005) and Douglas-fir light stamps updated to iLand 1.0 height:diameter
                // ratio spacing
                0.0F,10.332F, 11.101F, 13.942F, 11.197F, 13.367F, 10.300F, 12.123F, 12.582F, 12.812F, 
                10.991F, 11.269F, 9.763F, 10.914F, 9.690F, 12.477F, 11.038F, 11.317F, 10.039F, 8.122F, 
                9.139F, 11.834F, 8.867F, 11.034F, 10.034F, 13.091F, 10.851F, 12.163F, 12.639F
            };
            List<float> expectedNppByYear = new()
            {
                0.0F, 15121.153F, 16567.48F, 21124.502F, 17659.486F, 21233.345F, 16907.064F, 19966.412F, 20802.783F, 22333.103F, 
                19217.291F, 20211.252F, 17536.902F, 19613.64F, 17416.525F, 22425.482F, 19859.281F, 20366.093F, 18062.011F, 14609.35F, 
                16428.11F, 21257.44F, 15932.751F, 19820.877F, 18021.93F, 23516.998F, 19478.73F, 21812.896F, 22652.115F
            };
            List<float> expectedStemVolumeByYear = new()
            {
                107.503F, 120.144F, 134.572F, 155.985F, 171.044F, 191.837F, 205.168F, 223.533F, 243.057F, 263.543F, 
                278.798F, 295.564F, 307.320F, 321.637F, 331.338F, 347.055F, 362.951F, 377.981F, 385.101F, 389.623F, 
                394.295F, 410.941F, 417.181F, 433.390F, 443.233F, 465.463F, 475.591F, 484.418F, 500.627F
            };

            observedTrajectory.Verify(plot14.Output.ResourceUnitTrajectories[0].AllTreeSpeciesTrajectory, 222.0F, expectedGppByYear, expectedNppByYear, expectedStemVolumeByYear); ;
        }

        private static void VerifyDouglasFirPacificNorthwest(Model model)
        {
            TreeSpecies douglasFir = model.Landscape.ResourceUnits[0].Trees.TreeSpeciesSet[0];
            Assert.IsTrue(douglasFir.Active);
            Assert.IsTrue(String.Equals(douglasFir.ID, "psme", StringComparison.Ordinal));
            // maximumHeight   100
            // aging   1 / (1 + (x/0.95)^4)
            // douglasFir.Aging();
            // barkThickness   0.065
            // bmWoody_a   0.10568
            // bmWoody_b   2.4247
            // bmFoliage_a 0.05226
            // bmFoliage_b 1.7009
            // bmRoot_a    0.0418
            // bmRoot_b    2.33
            // bmBranch_a  0.04004
            // bmBranch_b  2.1382
            Assert.IsTrue(MathF.Abs(douglasFir.CarbonNitrogenRatioFineRoot - 9.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.CarbonNitrogenRatioFoliage - 60.3F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.CarbonNitrogenRatioWood - 452.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.DeathProbabilityFixed - 0.00355005264F) < 0.000001F); // transformed from 0.67
            // probStress  6.9
            // displayColor D6F288
            Assert.IsTrue(douglasFir.SaplingEstablishment.ChillingDaysRequired == 30);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingEstablishment.FrostTolerance - 0.5F) < 0.001);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingEstablishment.GrowingDegreeDaysBaseTemperature - 3.4F) < 0.001F);
            Assert.IsTrue(douglasFir.SaplingEstablishment.GrowingDegreeDaysForBudburst == 255);
            Assert.IsTrue(douglasFir.SaplingEstablishment.MaximumGrowingDegreeDays == 3261);
            Assert.IsTrue(douglasFir.SaplingEstablishment.MinimumGrowingDegreeDays == 177);
            Assert.IsTrue(douglasFir.SaplingEstablishment.MinimumFrostFreeDays == 65);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingEstablishment.ColdFatalityTemperature + 37.0F) < 0.001F);
            Assert.IsTrue(Single.IsNaN(douglasFir.SaplingEstablishment.DroughtMortalityPsiInMPa));
            Assert.IsTrue(MathF.Abs(douglasFir.FecundityM2 - 20.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.FecunditySerotiny - 0.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.FinerootFoliageRatio - 1.0F) < 0.001F);
            // HDlow   145.0998 * 1 * 0.8 * (1 - 0.28932) * d ^ -0.28932
            // HDhigh  100 / d + 25 + 100 * exp(-0.3 * (0.08 * d) ^ 1.5) + 120 * exp(-0.01 * d)
            Assert.IsTrue(String.Equals(douglasFir.ID, "psme", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.Index == 0);
            Assert.IsTrue(douglasFir.IsConiferous == true);
            Assert.IsTrue(douglasFir.IsEvergreen == true);
            Assert.IsTrue(douglasFir.IsMastYear == false);
            Assert.IsTrue(douglasFir.IsTreeSerotinousRandom(model.RandomGenerator, 40) == false);
            // lightResponseClass  2.78
            Assert.IsTrue(MathF.Abs(douglasFir.MaxCanopyConductance - 0.017F) < 0.001F);
            Assert.IsTrue(String.Equals(douglasFir.Name, "Pseudotsuga menziesii", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(MathF.Abs(douglasFir.NonMastYearFraction - 0.25F) < 0.001F);
            Assert.IsTrue(douglasFir.LeafPhenologyID == 0);
            Assert.IsTrue(MathF.Abs(douglasFir.MinimumSoilWaterPotential + 1.234F) < 0.001F);
            // respNitrogenClass   2
            // respTempMax 20
            // respTempMin 0
            // respVpdExponent - 0.6
            // maturityYears   14
            // mastYearInterval    5
            // nonMastYearFraction 0.25
            // seedKernel_as1  30
            // seedKernel_as2  200
            // seedKernel_ks0  0.2
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowth.BrowsingProbability - 0.5F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowth.HeightDiameterRatio - 112.0F) < 0.001F);
            Assert.IsTrue(String.Equals(douglasFir.SaplingGrowth.HeightGrowthPotential.ExpressionString, "1.2*72.2*(1-(1-(h/72.2)^(1/3))*exp(-0.0427))^3", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.SaplingGrowth.MaxStressYears == 2);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowth.ReferenceRatio - 0.503F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowth.ReinekeR - 164.0F) < 0.001F);
            Assert.IsTrue(douglasFir.SaplingGrowth.RepresentedClasses.Count == 41);
            Assert.IsTrue(Single.IsNaN(douglasFir.SaplingGrowth.SproutGrowth));
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowth.StressThreshold - 0.1F) < 0.001F);
            Assert.IsTrue(douglasFir.SeedDispersal == null);
            // Assert.IsTrue(String.Equals(douglasFir.SeedDispersal.DumpNextYearFileName, null, StringComparison.OrdinalIgnoreCase));
            // Assert.IsTrue(douglasFir.SeedDispersal.SeedMap == null);
            // Assert.IsTrue(Object.ReferenceEquals(douglasFir.SeedDispersal.Species, douglasFir));
            Assert.IsTrue(douglasFir.SnagHalflife == 20);
            Assert.IsTrue(MathF.Abs(douglasFir.SnagDecompositionRate - 0.04F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.LitterDecompositionRate - 0.22F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.CoarseWoodyDebrisDecompositionRate - 0.08F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.SpecificLeafArea - 5.80F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.TurnoverLeaf - 0.2F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.TurnoverFineRoot - 0.33F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.VolumeFactor - 0.353429F) < 0.001F); // 0.45 * pi/4
            Assert.IsTrue(MathF.Abs(douglasFir.WoodDensity - 450.0F) < 0.001F);

            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                Assert.IsTrue(Object.ReferenceEquals(douglasFir.SpeciesSet, resourceUnit.Trees.TreeSpeciesSet));
                Assert.IsTrue(resourceUnit.Trees.TreeSpeciesSet.Count == 11);
                Assert.IsTrue(Object.ReferenceEquals(douglasFir, resourceUnit.Trees.TreeSpeciesSet.ActiveSpecies[0]));
            }
        }

        private static void VerifyElliottModel(Model model)
        {
            Assert.IsTrue(model.Landscape.WeatherFirstCalendarYear == 2022);
            Assert.IsTrue(model.Output.ResourceUnitTrajectories.Count == model.Landscape.ResourceUnits.Count);
            Assert.IsTrue(model.Output.StandTrajectoriesByID.Count == 14);
            Assert.IsTrue(model.Project.Model.Ecosystem.AirDensity == 1.2041F);
            Assert.IsTrue(model.Project.Model.Ecosystem.BoundaryLayerConductance == 0.2F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LightUseEpsilon == 2.8F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LaiThresholdForConstantStandConductance == 3.0F);
            Assert.IsTrue(model.Project.Model.Ecosystem.ResourceUnitLightExtinctionCoefficient == 0.5F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient == 0.5F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TemperatureMA1tau == 5.0F);
            Assert.IsTrue(model.Project.Model.Settings.CarbonCycleEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.GrowthEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.MortalityEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.RegenerationEnabled == false);
            Assert.IsTrue(model.Project.Model.Settings.UseParFractionBelowGroundAllocation == true);
            Assert.IsTrue(MathF.Abs(model.Project.World.Geometry.Latitude - 43.57F) < 0.001F);
            Assert.IsTrue(model.Project.World.Geometry.IsTorus == false);
            Assert.IsTrue(model.SimulationState.CurrentCalendarYear == 2021); // since VerifyElliottModel() is called before timestepping starts
        }

        private static void VerifyElliottOutput(Model model, int simulationYear)
        {
            // resource units
            for (int resourceUnitIndex = 0; resourceUnitIndex < model.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = model.Output.ResourceUnitTrajectories[resourceUnitIndex];
                Assert.IsTrue(resourceUnitTrajectory.HasAllTreeSpeciesStatistics);
                Assert.IsTrue(resourceUnitTrajectory.HasTreeSpeciesStatistics);
                Assert.IsTrue(resourceUnitTrajectory.TreeSpeciesTrajectories.Length == 1);

                ModelTest.VerifyTrajectory(resourceUnitTrajectory.AllTreeSpeciesTrajectory, simulationYear);
                ModelTest.VerifyTrajectory(resourceUnitTrajectory.TreeSpeciesTrajectories[0], simulationYear);
            }

            // stands
            for (int standIndex = 0; standIndex < model.Output.StandTrajectoriesByID.Count; ++standIndex)
            {
                int standID = model.Output.StandTrajectoriesByID.Keys[standIndex];
                StandTrajectory standTrajectory = model.Output.StandTrajectoriesByID.Values[standIndex];
                Assert.IsTrue(standTrajectory.StandID == standID);

                ModelTest.VerifyTrajectory(standTrajectory, simulationYear);
            }
        }

        private static void VerifyElliottResourceUnits(Model model)
        {
            for (int resourceUnitIndex = 0; resourceUnitIndex < model.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnit resourceUnit = model.Landscape.ResourceUnits[resourceUnitIndex];
                ResourceUnitTrajectory resourceUnitTrajectory = model.Output.ResourceUnitTrajectories[resourceUnitIndex];
                Assert.IsTrue(Object.ReferenceEquals(resourceUnit, resourceUnitTrajectory.ResourceUnit));
                // resource unit variables read from weather file which are aren't currently test accessible
                //   ru.Snags: swdC, swdCount, swdCN, swdHalfLife, swdDecomRate, otherC, other CN
                // resource unit variables read from project file which are aren't currently test accessible
                //   ru.Soil: qb, qh, el, er, leaching, nitrogenDeposition, soilPlantAccessibleDepth,
                //            mKo (decomposition rate), mH (humification rate0
                //   ru.WaterCycle.Canopy: interceptionStorageNeedle, interceptionStorageBroadleaf, snowMeltTemperature,
                //                         waterUseSoilSaturation, pctSand, pctSilt, pctClay
                //   ru.SpeciesSet: nitrogenResponseClasses 1a, 1b, 2a, 2b, 3a, 3b
                //                  CO2 baseConcentration, compensationPoint, beta0, p0
                //                  lightResponse shadeIntolerant, shadeTolerant, LRImodifier
                //ru.Snags.ClimateFactor;
                //ru.Snags.FluxToAtmosphere;
                //ru.Snags.FluxToDisturbance;
                //ru.Snags.FluxToExtern;
                //ru.Snags.RefractoryFlux;
                //ru.Snags.RemoveCarbon;
                //ru.Soil.ClimateDecompositionFactor;
                //ru.Soil.FluxToAtmosphere;
                //ru.Soil.FluxToDisturbance;
                //ru.Soil.InputLabile;
                //ru.Soil.InputRefractory;
                AssertNullable.IsNotNull(resourceUnit.Soil);
                Assert.IsTrue(resourceUnit.Soil.Parameters.UseDynamicAvailableNitrogen == false);
                Assert.IsTrue((resourceUnit.Soil.OrganicMatter.C > 10.0F) && (resourceUnit.Soil.OrganicMatter.C < 1000.0F), "Soil: organic carbon");
                Assert.IsTrue((resourceUnit.Soil.OrganicMatter.N > 0.0F) && (resourceUnit.Soil.OrganicMatter.N < 10.0F), "Soil: organic nitrogen");
                Assert.IsTrue((resourceUnit.Soil.PlantAvailableNitrogen - 2000.0F) < 0.001F, "Soil: plant available nitrogen");
                Assert.IsTrue((resourceUnit.Soil.YoungLabile.C > 0.1F) && (resourceUnit.Soil.YoungLabile.C < 100.0F), "Soil: young labile carbon");
                Assert.IsTrue((resourceUnit.Soil.YoungLabile.N > 0.0F) && (resourceUnit.Soil.YoungLabile.N < 50.0F), "Soil: young labile nitrogen");
                Assert.IsTrue((resourceUnit.Soil.YoungLabile.DecompositionRate > 0.1F) && (resourceUnit.Soil.YoungLabile.DecompositionRate < 0.8F), "Soil: young labile decomposition rate");
                Assert.IsTrue((resourceUnit.Soil.YoungRefractory.C > 10.0F) && (resourceUnit.Soil.YoungRefractory.C < 200.0F), "Soil: young refractory carbon");
                Assert.IsTrue((resourceUnit.Soil.YoungRefractory.N > 0.0F) && (resourceUnit.Soil.YoungRefractory.N < 20.0F), "Soil: young refractory nitrogen");
                Assert.IsTrue((resourceUnit.Soil.YoungRefractory.DecompositionRate > 0.01F) && (resourceUnit.Soil.YoungRefractory.DecompositionRate < 0.8F), "Soil: young refractory decomposition rate");
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;
                Assert.IsTrue((resourceUnit.WaterCycle.CanopyConductance > 0.001F) && (resourceUnit.WaterCycle.CanopyConductance < 0.1F), "Water cycle: canopy conductance"); // initially zero
                Assert.IsTrue((resourceUnit.WaterCycle.CurrentSoilWater >= 0.0F) && (resourceUnit.WaterCycle.CurrentSoilWater <= resourceUnit.WaterCycle.FieldCapacity), "Water cycle: current water content of " + resourceUnit.WaterCycle.CurrentSoilWater + " mm is negative or greater than the field capacity of " + resourceUnit.WaterCycle.FieldCapacity + " mm.");
                Assert.IsTrue((resourceUnit.WaterCycle.FieldCapacity > 300.0F) && (resourceUnit.WaterCycle.FieldCapacity < 1000.0F), "Soil: field capacity is " + resourceUnit.WaterCycle.FieldCapacity + " mm.");
                Assert.IsTrue(resourceUnit.WaterCycle.SoilWaterPotentialByWeatherTimestepInYear.Length == Constant.MonthsInYear, "Water cycle: water potential length");
                foreach (float psi in resourceUnit.WaterCycle.SoilWaterPotentialByWeatherTimestepInYear)
                {
                    Assert.IsTrue((psi <= 0.0F) && (psi > -6000.0F), "Water cycle: water potential of " + psi + " kpa.");
                }
                Assert.IsTrue((resourceUnit.WaterCycle.SnowDayRadiation >= 0.0F) && (resourceUnit.WaterCycle.SnowDayRadiation < 5000.0F), "Water cycle: snow radiation"); // TODO: link to snow days?
                Assert.IsTrue((resourceUnit.WaterCycle.SnowDays >= 0.0F) && (resourceUnit.WaterCycle.SnowDays <= 10.0F), "Water cycle: snow days");
                Assert.IsTrue((resourceUnit.WaterCycle.TotalAnnualEvapotranspirationInMM > 1.0F) && (resourceUnit.WaterCycle.TotalAnnualEvapotranspirationInMM < 250.0F), "Soil: evapotranspiration"); // zero at initialization
                Assert.IsTrue((resourceUnit.WaterCycle.TotalAnnualRunoffInMM > 100.0F) && (resourceUnit.WaterCycle.TotalAnnualRunoffInMM < 3000.0F), "Soil: runoff"); // zero at initialization
            }

            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 190);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.CellCount == 190);
        }

        private static void VerifyElliottWeather(Model model)
        {
            Assert.IsTrue(model.Landscape.WeatherByID.Count == 50);
            foreach (Weather weather in model.Landscape.WeatherByID.Values)
            {
                Tree.LeafPhenology evergreen = weather.GetPhenology(Constant.EvergreenLeafPhenologyID);
                // private phenology variables read from the project file
                //   vpdMin, vpdMax, dayLengthMin, dayLengthMax, tempMintempMax
                //conifer.ChillingDaysLastYear;
                //conifer.ID;
                //conifer.LeafOnEnd;
                //conifer.LeafOnFraction;
                //conifer.LeafOnStart;
                //Tree.LeafPhenology broadleaf = weather.GetPhenology(1);
                //Tree.LeafPhenology deciduousConifer = weather.GetPhenology(2);

                // private climate variables
                //   weatherID, dailyWeatherChunkSizeInYears, temperatureShift, precipitationShift, randomSamplingEnabled, randomSamplingList, startYear
                Assert.IsTrue((weather.MeanAnnualTemperature > 0.0) && (weather.MeanAnnualTemperature < 30.0));
                // Assert.IsTrue(String.Equals(climate.ClimateTableName, "HaneyUBC", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(evergreen.ID == 0);
                //Assert.IsTrue(broadleaf.ID == 1);
                //Assert.IsTrue(deciduousConifer.ID == 2);
                // climate.PrecipitationMonth;
                Assert.IsTrue((weather.Sun.LastDayLongerThan10_5Hours > 0) && (weather.Sun.LastDayLongerThan10_5Hours < 365));
                Assert.IsTrue((weather.Sun.LastDayLongerThan14_5Hours > 0) && (weather.Sun.LastDayLongerThan14_5Hours < 365));
                Assert.IsTrue(weather.Sun.LongestDayIndex == 172);
                Assert.IsTrue(weather.Sun.IsNorthernHemisphere, "Sun.IsNorthernHemisphere = " + weather.Sun.IsNorthernHemisphere + ".");
                // climate.TemperatureMonth;
                Assert.IsTrue((weather.TotalAnnualRadiation > 3000.0) && (weather.TotalAnnualRadiation < 9000.0));
            }
        }

        private static void VerifyKalkalpenModel(Model model)
        {
            float worldBufferWidthInM = 60.0F;
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.ProjectExtent.Height == 200.0F + 2.0F * worldBufferWidthInM);
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.ProjectExtent.Width == 100.0F + 2.0F * worldBufferWidthInM);
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.ProjectExtent.X == 0.0F);
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.ProjectExtent.Y == 0.0F);
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.SizeX == 22);
            Assert.IsTrue(model.Landscape.VegetationHeightGrid.SizeY == 32);
            Assert.IsTrue(model.Landscape.LightGrid.ProjectExtent.Height == 200.0F + 2.0F * worldBufferWidthInM); // 100 x 200 m world + 60 m buffering = 220 x 320 m
            Assert.IsTrue(model.Landscape.LightGrid.ProjectExtent.Width == 100.0F + 2.0F * worldBufferWidthInM);
            Assert.IsTrue(model.Landscape.LightGrid.ProjectExtent.X == 0.0F);
            Assert.IsTrue(model.Landscape.LightGrid.ProjectExtent.Y == 0.0F);
            Assert.IsTrue(model.Landscape.LightGrid.SizeX == 110);
            Assert.IsTrue(model.Landscape.LightGrid.SizeY == 160);
            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 2);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.ProjectExtent.Height == 200.0F);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.ProjectExtent.Width == 100.0F);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.ProjectExtent.X == worldBufferWidthInM);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.ProjectExtent.Y == worldBufferWidthInM);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.SizeX == 1);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.SizeY == 2);
            Assert.IsTrue(model.Landscape.StandRaster.IsSetup() == false);
            Assert.IsTrue(model.Landscape.WeatherByID.Count == 1);
            Assert.IsTrue(model.Landscape.WeatherFirstCalendarYear == 1950);
            Assert.IsTrue(model.Project.Model.Settings.MaxThreads == 1);
            Assert.IsTrue(model.SimulationState.CurrentCalendarYear == 1949);
        }

        private static void VerifyKalkalpenResourceUnits(Model model, bool afterTimestep)
        {
            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 2);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.CellCount == 2);

            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                // not currently checked
                //ru.CornerPointOffset;
                //ru.HasDeadTrees;
                //ru.SaplingCells;
                //ru.Snags;
                //ru.Soil;
                //ru.Species;
                //ru.SpeciesSet;
                //ru.Trees;
                //ru.Variables;
                Assert.IsTrue(model.Landscape.ResourceUnitGrid.ProjectExtent.Contains(resourceUnit.ProjectExtent));
                Assert.IsTrue((resourceUnit.ProjectExtent.Height == Constant.ResourceUnitSizeInM) && (resourceUnit.ProjectExtent.Width == Constant.ResourceUnitSizeInM) &&
                              (resourceUnit.ProjectExtent.X == model.Project.World.Geometry.BufferWidthInM) &&
                              (MathF.Abs(resourceUnit.ProjectExtent.Y % 100.0F - model.Project.World.Geometry.BufferWidthInM) < 0.001F));
                Assert.IsTrue((resourceUnit.ID == 1) || (resourceUnit.ID == 10));
                Assert.IsTrue((resourceUnit.ResourceUnitGridIndex == 0) || (resourceUnit.ResourceUnitGridIndex == 1));
                Assert.IsTrue(resourceUnit.AreaInLandscapeInM2 == Constant.ResourceUnitAreaInM2);
                if (afterTimestep)
                {
                    Assert.IsTrue((resourceUnit.AreaWithTreesInM2 > 0.0F) && (resourceUnit.AreaWithTreesInM2 <= Constant.ResourceUnitAreaInM2));
                }
                else
                {
                    Assert.IsTrue(resourceUnit.AreaWithTreesInM2 == 0.0F); // not set during model load
                }

                // resource unit variables read from weather file which are aren't currently test accessible
                //   ru.Snags: swdC, swdCount, swdCN, swdHalfLife, swdDecomRate, otherC, other CN
                // resource unit variables read from project file which are aren't currently test accessible
                //   ru.Soil: qb, qh, el, er, leaching, nitrogenDeposition, soilPlantAccessibleDepth,
                //            mKo (decomposition rate), mH (humification rate0
                //   ru.WaterCycle.Canopy: interceptionStorageNeedle, interceptionStorageBroadleaf, snowMeltTemperature,
                //                         waterUseSoilSaturation, pctSand, pctSilt, pctClay
                //   ru.SpeciesSet: nitrogenResponseClasses 1a, 1b, 2a, 2b, 3a, 3b
                //                  CO2 baseConcentration, compensationPoint, beta0, p0
                //                  lightResponse shadeIntolerant, shadeTolerant, LRImodifier
                //ru.Snags.ClimateFactor;
                //ru.Snags.FluxToAtmosphere;
                //ru.Snags.FluxToDisturbance;
                //ru.Snags.FluxToExtern;
                //ru.Snags.RefractoryFlux;
                //ru.Snags.RemoveCarbon;
                //ru.Soil.ClimateDecompositionFactor;
                //ru.Soil.FluxToAtmosphere;
                //ru.Soil.FluxToDisturbance;
                //ru.Soil.InputLabile;
                //ru.Soil.InputRefractory;
                AssertNullable.IsNotNull(resourceUnit.Soil);
                Assert.IsTrue(resourceUnit.Soil.Parameters.UseDynamicAvailableNitrogen == true);
                //Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.C - 161.086F) < 0.001F, "Soil: organic carbon");
                //Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.N - 17.73954F) < 0.00001F, "Soil: organic nitrogen");
                //Assert.IsTrue(MathF.Abs(ru.Soil.PlantAvailableNitrogen - 56.186F) < 0.001F, "Soil: plant available nitrogen");
                //Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.C - 4.8414983F) < 0.001F, "Soil: young labile carbon");
                //Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.N - 0.2554353F) < 0.0001F, "Soil: young labile nitrogen");
                //Assert.IsTrue(ru.Soil.YoungLabile.DecompositionRate == 0.322F, "Soil: young labile decomposition rate");
                //Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.C - 45.97414F) < 0.001F, "Soil: young refractory carbon");
                //Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.N - 0.261731F) < 0.0001F, "Soil: young refractory nitrogen");
                //Assert.IsTrue(ru.Soil.YoungRefractory.DecompositionRate == 0.1790625F, "Soil: young refractory decomposition rate");
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;

                Assert.IsTrue((resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor > 0.0F) && (resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor < 1.0F));
                if (afterTimestep)
                {
                    Assert.IsTrue((resourceUnit.Trees.AverageLightRelativeIntensity > 0.0F) && (resourceUnit.Trees.AverageLightRelativeIntensity <= 1.0F));
                    Assert.IsTrue((resourceUnit.Trees.PhotosyntheticallyActiveArea > 0.0F) && (resourceUnit.Trees.PhotosyntheticallyActiveArea <= Constant.ResourceUnitAreaInM2));
                    Assert.IsTrue((resourceUnit.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea > 0.0F) && (resourceUnit.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea <= 1.0F));
                }
                else
                {
                    Assert.IsTrue(resourceUnit.Trees.AverageLightRelativeIntensity == 0.0F);
                    Assert.IsTrue(resourceUnit.Trees.PhotosyntheticallyActiveArea == 0.0F);
                    Assert.IsTrue(resourceUnit.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea == 0.0F);
                }
                Assert.IsTrue(resourceUnit.Trees.TreeStatisticsByStandID.Count == 1, "Expected tree statistics for one stand but got statistics for " + resourceUnit.Trees.TreeStatisticsByStandID.Count + " stands.");
                Assert.IsTrue(resourceUnit.Trees.TreeStatisticsByStandID.ContainsKey(Constant.DefaultStandID), "Expected zero tree statistics by stand ID but got " + resourceUnit.Trees.TreeStatisticsByStandID.Count + ".");
                Assert.IsTrue((resourceUnit.Trees.TotalLeafArea > 0.0F) && (resourceUnit.Trees.TotalLeafArea < 20.0F * Constant.ResourceUnitAreaInM2));

                //Assert.IsTrue(ru.WaterCycle.CanopyConductance == 0.0F, "Water cycle: canopy conductance"); // initially zero
                //Assert.IsTrue((ru.WaterCycle.CurrentSoilWaterContent >= 0.0) && (ru.WaterCycle.CurrentSoilWaterContent <= ru.WaterCycle.FieldCapacity), "Water cycle: current water content of " + ru.WaterCycle.CurrentSoilWaterContent + " mm is negative or greater than the field capacity of " + ru.WaterCycle.FieldCapacity + " mm.");
                //Assert.IsTrue(MathF.Abs(ru.WaterCycle.FieldCapacity - 29.2064552F) < 0.001F, "Soil: field capacity");
                //Assert.IsTrue(ru.WaterCycle.SoilWaterPotentialByDay.Length == Constant.DaysInLeapYear, "Water cycle: water potential length");
                //foreach (float psi in ru.WaterCycle.SoilWaterPotentialByDay)
                //{
                //    Assert.IsTrue((psi <= 0.0F) && (psi > -6000.0F), "Water cycle: water potential");
                //}
                //Assert.IsTrue((ru.WaterCycle.SnowDayRadiation >= 0.0F) && (ru.WaterCycle.SnowDayRadiation < 5000.0F), "Water cycle: snow radiation"); // TODO: link to snow days?
                //Assert.IsTrue((ru.WaterCycle.SnowDays >= 0.0F) && (ru.WaterCycle.SnowDays <= Constant.DaysInLeapYear), "Water cycle: snow days");
                //Assert.IsTrue(MathF.Abs(ru.WaterCycle.SoilPlantAccessibleDepthInMM - 1340.0F) < 0.001F, "Soil: depth");
                //Assert.IsTrue(ru.WaterCycle.TotalEvapotranspiration == 0.0F, "Soil: evapotranspiration"); // zero at initialization
                //Assert.IsTrue(ru.WaterCycle.TotalRunoff == 0.0F, "Soil: runoff"); // zero at initialization
            }
        }

        private static void VerifyKalkalpenResourceUnitTrees(List<ResourceUnit> resourceUnits, int simulationYear, ObservedResourceUnitTrees startOfYearTrees, ObservedResourceUnitTrees endOfYearTrees)
        {
            // growth on observed resource unit
            float averageDiameterGrowth = 0.0F;
            float averageHeightGrowth = 0.0F;
            foreach ((int treeID, float height) in endOfYearTrees.HeightInMByTreeID)
            {
                float initialDiameter = startOfYearTrees.DiameterInCmByTreeID[treeID];
                float initialHeight = startOfYearTrees.HeightInMByTreeID[treeID];
                float finalDiameter = endOfYearTrees.DiameterInCmByTreeID[treeID];
                float finalHeight = height;
                averageDiameterGrowth += finalDiameter - initialDiameter;
                averageHeightGrowth += finalHeight - initialHeight;
                Assert.IsTrue(finalDiameter >= initialDiameter);
                Assert.IsTrue(finalDiameter < 1.2F * initialDiameter);
                Assert.IsTrue(finalHeight >= initialHeight);
                Assert.IsTrue(finalHeight < 1.1F * initialHeight);
            }

            int treesObserved = endOfYearTrees.HeightInMByTreeID.Count;
            averageDiameterGrowth /= treesObserved;
            averageHeightGrowth /= treesObserved;

            Assert.IsTrue(averageDiameterGrowth > MathF.Max(0.2F - 0.01F * simulationYear, 0.0F), "Average diameter growth is " + averageDiameterGrowth + " cm.");
            Assert.IsTrue(averageHeightGrowth > MathF.Max(0.2F - 0.01F * simulationYear, 0.0F), "Average height growth is " + averageHeightGrowth + " m.");

            // trees on resource units
            int minimumTreeCount = 30 - 6 - 2 * simulationYear; // TODO: wide tolerance required due to stochastic mortality
            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnit resourceUnit = resourceUnits[resourceUnitIndex];
                int resourceUnitTreeSpeciesCount = resourceUnit.Trees.TreesBySpeciesID.Count;
                Assert.IsTrue(resourceUnitTreeSpeciesCount >= minimumTreeCount, "Expected " + minimumTreeCount + " trees but got " + resourceUnitTreeSpeciesCount + ".");
                Assert.IsTrue(resourceUnit.Trees.TreesBySpeciesID.Count >= minimumTreeCount);
                Assert.IsTrue(startOfYearTrees.DiameterInCmByTreeID.Count >= minimumTreeCount);
                Assert.IsTrue(startOfYearTrees.HeightInMByTreeID.Count >= minimumTreeCount);
                Assert.IsTrue(endOfYearTrees.DiameterInCmByTreeID.Count >= minimumTreeCount);
                Assert.IsTrue(endOfYearTrees.HeightInMByTreeID.Count >= minimumTreeCount);

                // check living trees
                SortedList<string, TreeListSpatial> resourceUnitTrees = resourceUnit.Trees.TreesBySpeciesID;
                foreach (TreeListSpatial treesOfSpecies in resourceUnitTrees.Values)
                {
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        Assert.IsTrue((treesOfSpecies.Age[treeIndex] > 0 + simulationYear) && (treesOfSpecies.Age[treeIndex] < 100 + simulationYear));
                        Assert.IsTrue(treesOfSpecies.GetBasalArea(treeIndex) > 0.0);
                        Assert.IsTrue((treesOfSpecies.CoarseRootMassInKg[treeIndex] >= 0.0F) && (treesOfSpecies.CoarseRootMassInKg[treeIndex] < 1E6F));
                        Assert.IsTrue((treesOfSpecies.DbhInCm[treeIndex] > 0.0F) && (treesOfSpecies.DbhInCm[treeIndex] < 200.0F));
                        Assert.IsTrue((treesOfSpecies.DbhDeltaInCm[treeIndex] >= 0.0F) && (treesOfSpecies.DbhDeltaInCm[treeIndex] < 10.0F));
                        Assert.IsTrue((treesOfSpecies.FineRootMassInKg[treeIndex] > 0.0F) && (treesOfSpecies.FineRootMassInKg[treeIndex] < 1E6F));
                        Assert.IsTrue((treesOfSpecies.FoliageMassInKg[treeIndex] > 0.0F) && (treesOfSpecies.FoliageMassInKg[treeIndex] < 1000.0F));
                        Assert.IsTrue(treesOfSpecies.GetBranchBiomass(treeIndex) > 0.0F);
                        Assert.IsTrue(treesOfSpecies.GetCrownRadius(treeIndex) > 0.0F);
                        Assert.IsTrue(treesOfSpecies.IsCutDown(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsDead(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsDeadBarkBeetle(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsDeadFire(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsDeadWind(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsHarvested(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsMarkedAsCropCompetitor(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsMarkedAsCropTree(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsMarkedForCut(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.IsMarkedForHarvest(treeIndex) == false);
                        Assert.IsTrue(treesOfSpecies.GetStemVolume(treeIndex) > 0.0F);
                        Assert.IsTrue((treesOfSpecies.HeightInM[treeIndex] > 0.0F) && (treesOfSpecies.HeightInM[treeIndex] < 100.0F));
                        Assert.IsTrue((treesOfSpecies.TreeID[treeIndex] > 0) && (treesOfSpecies.TreeID[treeIndex] < 40));
                        Assert.IsTrue((treesOfSpecies.LeafAreaInM2[treeIndex] > 0.0F) && (treesOfSpecies.LeafAreaInM2[treeIndex] < 1000.0F));
                        // Assert.IsTrue((tree.LightCellPosition);
                        Assert.IsTrue((treesOfSpecies.LightResourceIndex[treeIndex] > 0.0F) && (treesOfSpecies.LightResourceIndex[treeIndex] <= 1.0F));
                        Assert.IsTrue((treesOfSpecies.LightResponse[treeIndex] > -0.5F) && (treesOfSpecies.LightResponse[treeIndex] <= 1.0F));
                        Assert.IsTrue((treesOfSpecies.NppReserveInKg[treeIndex] > 0.0F) && (treesOfSpecies.NppReserveInKg[treeIndex] < 1E4F));
                        Assert.IsTrue((treesOfSpecies.Opacity[treeIndex] > 0.0F) && (treesOfSpecies.Opacity[treeIndex] <= 1.0F));
                        Assert.IsTrue(object.ReferenceEquals(treesOfSpecies.ResourceUnit, resourceUnit));
                        // Assert.IsTrue(tree.Species.ID);
                        // Assert.IsTrue(tree.Stamp);
                        Assert.IsTrue((treesOfSpecies.StemMassInKg[treeIndex] > 0.0) && (treesOfSpecies.CoarseRootMassInKg[treeIndex] < 1E6));
                        Assert.IsTrue((treesOfSpecies.StressIndex[treeIndex] >= 0.0) && (treesOfSpecies.CoarseRootMassInKg[treeIndex] < 1E6));
                    }

                    Assert.IsTrue(treesOfSpecies.Capacity == 4);
                    Assert.IsTrue(treesOfSpecies.Count == (treesOfSpecies.Species.ID == "psme" ? 2 : 1), "Expected one or two living trees of species '" + treesOfSpecies.Species.ID + "'.");
                }
                // Salix caprea is viable as placed on the resource unit and should stress out of the stand in the first timestep. It
                // should therefore be dropped as tree species on the resource unit.
                Assert.IsTrue(resourceUnitTrees.ContainsKey("saca") == false, "Salix caprea did not die out of stand.");
            }
        }

        private static void VerifyLightAndHeightGrids(Landscape landscape, float maxHeight)
        {
            // light grid
            Grid<float> lightGrid = landscape.LightGrid;
            float maxLight = Single.MinValue;
            float meanLight = 0.0F;
            float minLight = Single.MaxValue;
            for (int lightIndex = 0; lightIndex < lightGrid.CellCount; ++lightIndex)
            {
                float light = lightGrid[lightIndex];
                maxLight = MathF.Max(light, maxLight);
                meanLight += light;
                minLight = MathF.Min(light, minLight);
            }
            meanLight /= lightGrid.CellCount;

            Assert.IsTrue((minLight >= 0.0F) && (minLight < 1.0F));
            Assert.IsTrue((meanLight > minLight) && (meanLight < maxLight));
            Assert.IsTrue(maxLight == 1.0F);

            // height grid
            Grid<float> heightGrid = landscape.VegetationHeightGrid;
            float maxGridHeight = Single.MinValue;
            float meanGridHeight = 0.0F;
            float minGridHeight = Single.MaxValue;
            for (int heightIndex = 0; heightIndex < heightGrid.CellCount; ++heightIndex)
            {
                float vegetationHeightInM = heightGrid[heightIndex];
                maxGridHeight = MathF.Max(vegetationHeightInM, maxGridHeight);
                meanGridHeight += vegetationHeightInM;
                minGridHeight = MathF.Min(vegetationHeightInM, minGridHeight);
            }
            meanGridHeight /= heightGrid.CellCount;

            Assert.IsTrue(minGridHeight >= 0.0F);
            Assert.IsTrue((meanGridHeight > minGridHeight) && (meanGridHeight < maxGridHeight));
            Assert.IsTrue(maxGridHeight < maxHeight);
        }

        private static void VerifyMalcolmKnappModel(Model model)
        {
            Assert.IsTrue(model.Project.Model.Ecosystem.AirDensity == 1.204F);
            Assert.IsTrue(model.Project.Model.Ecosystem.BoundaryLayerConductance == 0.2F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LightUseEpsilon == 2.7F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LaiThresholdForConstantStandConductance == 3.0F);
            Assert.IsTrue(model.Project.Model.Ecosystem.ResourceUnitLightExtinctionCoefficient == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TemperatureMA1tau == 6.0F);
            Assert.IsTrue(model.Project.Model.Settings.RegenerationEnabled == false);
            Assert.IsTrue(model.Project.Model.Settings.MortalityEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.GrowthEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.CarbonCycleEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.UseParFractionBelowGroundAllocation == true);
            Assert.IsTrue(MathF.Abs(model.Project.World.Geometry.Latitude - 49.261F) < 0.003F);
            Assert.IsTrue(model.Project.World.Geometry.IsTorus == true);
        }

        private static void VerifyMalcolmKnappResourceUnit(Model model)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                // resource unit variables read from weather file which are aren't currently test accessible
                //   ru.Snags: swdC, swdCount, swdCN, swdHalfLife, swdDecomRate, otherC, other CN
                // resource unit variables read from project file which are aren't currently test accessible
                //   ru.Soil: qb, qh, el, er, leaching, nitrogenDeposition, soilPlantAccessibleDepth,
                //            mKo (decomposition rate), mH (humification rate0
                //   ru.WaterCycle.Canopy: interceptionStorageNeedle, interceptionStorageBroadleaf, snowMeltTemperature,
                //                         waterUseSoilSaturation, pctSand, pctSilt, pctClay
                //   ru.SpeciesSet: nitrogenResponseClasses 1a, 1b, 2a, 2b, 3a, 3b
                //                  CO2 baseConcentration, compensationPoint, beta0, p0
                //                  lightResponse shadeIntolerant, shadeTolerant, LRImodifier
                //ru.Snags.ClimateFactor;
                //ru.Snags.FluxToAtmosphere;
                //ru.Snags.FluxToDisturbance;
                //ru.Snags.FluxToExtern;
                //ru.Snags.RefractoryFlux;
                //ru.Snags.RemoveCarbon;
                //ru.Soil.ClimateDecompositionFactor;
                //ru.Soil.FluxToAtmosphere;
                //ru.Soil.FluxToDisturbance;
                //ru.Soil.InputLabile;
                //ru.Soil.InputRefractory;
                AssertNullable.IsNotNull(resourceUnit.Soil);
                Assert.IsTrue(resourceUnit.Soil.Parameters.UseDynamicAvailableNitrogen == false);
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.OrganicMatter.C - 161.086F) < 0.001F, "Soil: organic carbon");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.OrganicMatter.N - 17.73954F) < 0.00001F, "Soil: organic nitrogen");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.PlantAvailableNitrogen - 56.186F) < 0.001F, "Soil: plant available nitrogen");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.YoungLabile.C - 4.8414983F) < 0.001F, "Soil: young labile carbon");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.YoungLabile.N - 0.2554353F) < 0.0001F, "Soil: young labile nitrogen");
                Assert.IsTrue(resourceUnit.Soil.YoungLabile.DecompositionRate == 0.322F, "Soil: young labile decomposition rate");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.YoungRefractory.C - 45.97414F) < 0.001F, "Soil: young refractory carbon");
                Assert.IsTrue(MathF.Abs(resourceUnit.Soil.YoungRefractory.N - 0.261731F) < 0.0001F, "Soil: young refractory nitrogen");
                Assert.IsTrue(resourceUnit.Soil.YoungRefractory.DecompositionRate == 0.1790625F, "Soil: young refractory decomposition rate");
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;
                Assert.IsTrue(resourceUnit.WaterCycle.CanopyConductance == 0.0F, "Water cycle: canopy conductance"); // initially zero
                Assert.IsTrue((resourceUnit.WaterCycle.CurrentSoilWater >= 0.0) && (resourceUnit.WaterCycle.CurrentSoilWater <= resourceUnit.WaterCycle.FieldCapacity), "Water cycle: current water content of " + resourceUnit.WaterCycle.CurrentSoilWater + " mm is negative or greater than the field capacity of " + resourceUnit.WaterCycle.FieldCapacity + " mm.");
                Assert.IsTrue(MathF.Abs(resourceUnit.WaterCycle.FieldCapacity - 29.2064552F) < 0.001F, "Soil: field capacity is " + resourceUnit.WaterCycle.FieldCapacity + " mm.");
                Assert.IsTrue(resourceUnit.WaterCycle.SoilWaterPotentialByWeatherTimestepInYear.Length == Constant.DaysInLeapYear, "Water cycle: water potential length");
                foreach (float psi in resourceUnit.WaterCycle.SoilWaterPotentialByWeatherTimestepInYear)
                {
                    Assert.IsTrue((psi <= 0.0F) && (psi > -6000.0F), "Water cycle: water potential");
                }
                Assert.IsTrue((resourceUnit.WaterCycle.SnowDayRadiation >= 0.0F) && (resourceUnit.WaterCycle.SnowDayRadiation < 5000.0F), "Water cycle: snow radiation"); // TODO: link to snow days?
                Assert.IsTrue((resourceUnit.WaterCycle.SnowDays >= 0.0F) && (resourceUnit.WaterCycle.SnowDays <= Constant.DaysInLeapYear), "Water cycle: snow days");
                Assert.IsTrue(resourceUnit.WaterCycle.TotalAnnualEvapotranspirationInMM == 0.0F, "Soil: evapotranspiration"); // zero at initialization
                Assert.IsTrue(resourceUnit.WaterCycle.TotalAnnualRunoffInMM == 0.0F, "Soil: runoff"); // zero at initialization
            }

            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 1);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.CellCount == 1);
        }

        private static void VerifyMalcolmKnappWeather(Model model)
        {
            Assert.IsTrue(model.Landscape.WeatherByID.Count == 1);
            foreach (Weather weather in model.Landscape.WeatherByID.Values)
            {
                Tree.LeafPhenology conifer = weather.GetPhenology(Constant.EvergreenLeafPhenologyID);
                // private phenology variables read from the project file
                //   vpdMin, vpdMax, dayLengthMin, dayLengthMax, tempMintempMax
                //conifer.ChillingDaysLastYear;
                //conifer.ID;
                //conifer.LeafOnEnd;
                //conifer.LeafOnFraction;
                //conifer.LeafOnStart;
                Tree.LeafPhenology broadleaf = weather.GetPhenology(1);
                Tree.LeafPhenology deciduousConifer = weather.GetPhenology(2);

                // private climate variables
                //   weatherID, dailyWeatherChunkSizeInYears, temperatureShift, precipitationShift, randomSamplingEnabled, randomSamplingList, startYear
                Assert.IsTrue((weather.MeanAnnualTemperature > 0.0) && (weather.MeanAnnualTemperature < 30.0));
                // Assert.IsTrue(String.Equals(climate.ClimateTableName, "HaneyUBC", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(conifer.ID == 0);
                Assert.IsTrue(broadleaf.ID == 1);
                Assert.IsTrue(deciduousConifer.ID == 2);
                // climate.PrecipitationMonth;
                Assert.IsTrue((weather.Sun.LastDayLongerThan10_5Hours > 0) && (weather.Sun.LastDayLongerThan10_5Hours < 365));
                Assert.IsTrue((weather.Sun.LastDayLongerThan14_5Hours > 0) && (weather.Sun.LastDayLongerThan14_5Hours < 365));
                Assert.IsTrue(weather.Sun.LongestDayIndex == 172);
                Assert.IsTrue(weather.Sun.IsNorthernHemisphere, "Sun.IsNorthernHemisphere = " + weather.Sun.IsNorthernHemisphere + ".");
                // climate.TemperatureMonth;
                Assert.IsTrue((weather.TotalAnnualRadiation > 4000.0) && (weather.TotalAnnualRadiation < 5000.0));
            }
        }

        private static void VerifyNorwaySpruce(Model model)
        {
            TreeSpecies species = model.Landscape.ResourceUnits[0].Trees.TreeSpeciesSet["piab"];
            AssertNullable.IsNotNull(species);

            // PIAB: 1/(1 + (x/0.55)^2)
            float youngAgingFactor = species.GetAgingFactor(10.0F, 10);
            float middleAgingFactor = species.GetAgingFactor(40.0F, 80);
            float oldAgingFactor = species.GetAgingFactor(55.5F, 575);

            Assert.IsTrue(MathF.Abs(youngAgingFactor - 0.964912F) < 0.001F);
            Assert.IsTrue(MathF.Abs(middleAgingFactor - 0.481931F) < 0.001F);
            Assert.IsTrue(MathF.Abs(oldAgingFactor - 0.2375708F) < 0.001F);

            // PIAB: mf = 0.095565 * dbh^1.56
            // round(0.095565 * c(2, 20, 50, 100) ^ 1.56, 5)
            Assert.IsTrue(String.Equals(species.ID, "piab", StringComparison.Ordinal));
            Assert.IsTrue(MathF.Abs(species.GetBiomassFoliage(2) - 0.281777F) < 0.001F);
            Assert.IsTrue(MathF.Abs(species.GetBiomassFoliage(20) - 10.23070F) < 0.001F);
            Assert.IsTrue(MathF.Abs(species.GetBiomassFoliage(50) - 42.72598F) < 0.001F);
            Assert.IsTrue(MathF.Abs(species.GetBiomassFoliage(100) - 125.97920F) < 0.001F);

            // PIAB: HDlow = 170*(1)*d^-0.5, HDhigh = (195.547*1.004*(-0.2396+1)*d^-0.2396)*1
            // round(170*(1)*c(3.3, 10, 33)^-0.5, 2)
            // round((195.547*1.004*(-0.2396+1)*c(3.3, 10, 33)^-0.2396)*1, 2)
            species.GetHeightDiameterRatioLimits(3.3F, out float lowLimitSmall, out float highLimitSmall);
            species.GetHeightDiameterRatioLimits(10.0F, out float lowLimitMedium, out float highLimitMedium);
            species.GetHeightDiameterRatioLimits(33.0F, out float lowLimitLarge, out float highLimitLarge);

            Assert.IsTrue(MathF.Abs(lowLimitSmall - 93.58F) < 0.01F);
            Assert.IsTrue(MathF.Abs(lowLimitMedium - 53.76F) < 0.01F);
            Assert.IsTrue(MathF.Abs(lowLimitLarge - 29.59F) < 0.01F);
            Assert.IsTrue(MathF.Abs(highLimitSmall - 112.15F) < 0.01F);
            Assert.IsTrue(MathF.Abs(highLimitMedium - 85.99F) < 0.01F);
            Assert.IsTrue(MathF.Abs(highLimitLarge - 64.59F) < 0.01F);

            // PIAB: 44.7*(1-(1-(h/44.7)^(1/3))*exp(-0.044))^3
            // round(44.7*(1-(1-(c(0.25, 1, 4.5)/44.7)^(1/3))*exp(-0.044))^3, 3)
            float shortPotential = species.SaplingGrowth.HeightGrowthPotential.Evaluate(0.25F);
            float mediumPotential = species.SaplingGrowth.HeightGrowthPotential.Evaluate(1.0F);
            float tallPotential = species.SaplingGrowth.HeightGrowthPotential.Evaluate(4.5F);

            Assert.IsTrue(MathF.Abs(shortPotential - 0.431F) < 0.01F);
            Assert.IsTrue(MathF.Abs(mediumPotential - 1.367F) < 0.01F);
            Assert.IsTrue(MathF.Abs(tallPotential - 5.202F) < 0.01F);
        }

        private static void VerifyTrajectory(StandOrResourceUnitTrajectory trajectory, int simulationYear)
        {
            float averageDbhInCm = trajectory.AverageDbhByYear[simulationYear];
            float averageHeightInM = trajectory.AverageHeightByYear[simulationYear];
            float basalAreaInM2PerHa = trajectory.BasalAreaByYear[simulationYear];
            float branchCarbonInKgPerHa = trajectory.BranchCarbonByYear[simulationYear];
            float branchNitrogenInKgPerHa = trajectory.BranchNitrogenByYear[simulationYear];
            float coarseRootCarbonInKgPerHa = trajectory.CoarseRootCarbonByYear[simulationYear];
            float coarseRootNitrogenInKgPerHa = trajectory.CoarseRootNitrogenByYear[simulationYear];
            float saplingCohortsPerHectare = trajectory.SaplingCohortsPerHectareByYear[simulationYear];
            float fineRootCarbonInKgPerHa = trajectory.FineRootCarbonByYear[simulationYear];
            float fineRootNitrogenInKgPerHa = trajectory.FineRootNitrogenByYear[simulationYear];
            float foliageCarbonInKgPerHa = trajectory.FoliageCarbonByYear[simulationYear];
            float foliageNitrogenInKgPerHa = trajectory.FoliageNitrogenByYear[simulationYear];
            float leafAreaIndex = trajectory.LeafAreaIndexByYear[simulationYear];
            float liveStemVolumeInM3PerHa = trajectory.LiveStemVolumeByYear[simulationYear];
            float meanSaplingAgeInYears = trajectory.SaplingMeanAgeByYear[simulationYear];
            float regenerationCarbonInKgPerHa = trajectory.RegenerationCarbonByYear[simulationYear];
            float regenerationNitrogenInKgPerHa = trajectory.RegenerationNitrogenByYear[simulationYear];
            float saplingsPerHectare = trajectory.SaplingsPerHectareByYear[simulationYear];
            float stemCarbonInKgPerHa = trajectory.StemCarbonByYear[simulationYear];
            float stemNitrogenInKgPerHa = trajectory.StemNitrogenByYear[simulationYear];
            float treeAbovegroundNppInKgPerHa = trajectory.TreeNppAbovegroundByYear[simulationYear];
            float treeNppInKgPerHa = trajectory.TreeNppByYear[simulationYear];
            float treesPerHectare = trajectory.TreesPerHectareByYear[simulationYear];

            Assert.IsTrue((Single.IsNaN(averageDbhInCm) == false) && (averageDbhInCm >= 0.0F) && (averageDbhInCm < 200.0F));
            Assert.IsTrue((Single.IsNaN(averageHeightInM) == false) && (averageHeightInM >= 0.0F) && (averageHeightInM < 100.0F));
            Assert.IsTrue((Single.IsNaN(basalAreaInM2PerHa) == false) && (basalAreaInM2PerHa >= 0.0F) && (basalAreaInM2PerHa < 200.0F));
            Assert.IsTrue((Single.IsNaN(leafAreaIndex) == false) && (leafAreaIndex >= 0.0F) && (leafAreaIndex < 20.0F));
            Assert.IsTrue((Single.IsNaN(liveStemVolumeInM3PerHa) == false) && (liveStemVolumeInM3PerHa >= 0.0F) && (liveStemVolumeInM3PerHa < 3000.0F));
            Assert.IsTrue((Single.IsNaN(treeAbovegroundNppInKgPerHa) == false) && (treeAbovegroundNppInKgPerHa >= 0.0F) && (treeAbovegroundNppInKgPerHa < 50000.0F));
            Assert.IsTrue((Single.IsNaN(treeNppInKgPerHa) == false) && (treeNppInKgPerHa >= 0.0F) && (treeNppInKgPerHa < 75000.0F));

            Assert.IsTrue((Single.IsNaN(meanSaplingAgeInYears) == false) && (meanSaplingAgeInYears >= 0.0F) && (meanSaplingAgeInYears < 20.0F));
            Assert.IsTrue((Single.IsNaN(saplingCohortsPerHectare) == false) && (saplingCohortsPerHectare >= 0.0F) && (saplingCohortsPerHectare < 200.0F));
            Assert.IsTrue((Single.IsNaN(saplingsPerHectare) == false) && (saplingsPerHectare >= 0.0F) && (saplingsPerHectare < 200.0F));
            Assert.IsTrue((Single.IsNaN(treesPerHectare) == false) && (treesPerHectare >= 0.0F) && (treesPerHectare < 2000.0F));

            Assert.IsTrue((Single.IsNaN(branchCarbonInKgPerHa) == false) && (branchCarbonInKgPerHa >= 0.0F) && (branchCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(branchNitrogenInKgPerHa) == false) && (branchNitrogenInKgPerHa >= 0.0F) && (branchNitrogenInKgPerHa < 200000.0F));
            Assert.IsTrue((Single.IsNaN(coarseRootCarbonInKgPerHa) == false) && (coarseRootCarbonInKgPerHa >= 0.0F) && (coarseRootCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(coarseRootNitrogenInKgPerHa) == false) && (coarseRootNitrogenInKgPerHa >= 0.0F) && (coarseRootNitrogenInKgPerHa < 200000.0F));
            Assert.IsTrue((Single.IsNaN(fineRootCarbonInKgPerHa) == false) && (fineRootCarbonInKgPerHa >= 0.0F) && (fineRootCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(fineRootNitrogenInKgPerHa) == false) && (fineRootNitrogenInKgPerHa >= 0.0F) && (fineRootNitrogenInKgPerHa < 200000.0F));
            Assert.IsTrue((Single.IsNaN(foliageCarbonInKgPerHa) == false) && (foliageCarbonInKgPerHa >= 0.0F) && (foliageCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(foliageNitrogenInKgPerHa) == false) && (foliageNitrogenInKgPerHa >= 0.0F) && (foliageNitrogenInKgPerHa < 200000.0F));
            Assert.IsTrue((Single.IsNaN(regenerationCarbonInKgPerHa) == false) && (regenerationCarbonInKgPerHa >= 0.0F) && (regenerationCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(regenerationNitrogenInKgPerHa) == false) && (regenerationNitrogenInKgPerHa >= 0.0F) && (regenerationNitrogenInKgPerHa < 200000.0F));
            Assert.IsTrue((Single.IsNaN(stemCarbonInKgPerHa) == false) && (stemCarbonInKgPerHa >= 0.0F) && (stemCarbonInKgPerHa < 2000000.0F));
            Assert.IsTrue((Single.IsNaN(stemNitrogenInKgPerHa) == false) && (stemNitrogenInKgPerHa >= 0.0F) && (stemNitrogenInKgPerHa < 200000.0F));

            Assert.IsTrue(trajectory.LengthInYears == simulationYear + 1);

            float abovegroundTreeMassFromComponents = branchCarbonInKgPerHa + branchNitrogenInKgPerHa + foliageCarbonInKgPerHa + foliageNitrogenInKgPerHa + stemCarbonInKgPerHa + stemNitrogenInKgPerHa; // kg/ha
            float belowgroundTreeMassFromComponents = coarseRootCarbonInKgPerHa + coarseRootNitrogenInKgPerHa + fineRootCarbonInKgPerHa + fineRootNitrogenInKgPerHa; // kg/ha
            float totalTreeMass = abovegroundTreeMassFromComponents + belowgroundTreeMassFromComponents; // kg/ha

            Assert.IsTrue(treeAbovegroundNppInKgPerHa < 1.2F * abovegroundTreeMassFromComponents);
            Assert.IsTrue(treeNppInKgPerHa < 1.2F * totalTreeMass);
        }
    }
}
