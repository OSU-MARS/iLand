using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace iLand.Test
{
    [TestClass]
    public class ModelTest : LandTest
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void Kalkalpen()
        {
            for (int reliabilityIteration = 0; reliabilityIteration < 1 /* 100 */; ++reliabilityIteration)
            {
                using Model kalkalpen = LandTest.LoadProject(LandTest.GetKalkalpenProjectPath(this.TestContext!));

                ModelTest.VerifyKalkalpenModel(kalkalpen);
                ModelTest.VerifyNorwaySpruce(kalkalpen);

                Dictionary<int, float> initialDiameters = new Dictionary<int, float>();
                Dictionary<int, float> initialHeights = new Dictionary<int, float>();
                Dictionary<int, float> finalDiameters = new Dictionary<int, float>();
                Dictionary<int, float> finalHeights = new Dictionary<int, float>();
                for (int year = 0; year < 3; ++year)
                {
                    initialDiameters.Clear();
                    initialHeights.Clear();
                    foreach (Trees treesOfSpecies in kalkalpen.Landscape.ResourceUnits[0].Trees.TreesBySpeciesID.Values)
                    {
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            initialDiameters.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Dbh[treeIndex]);
                            initialHeights.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Height[treeIndex]);
                        }
                    }

                    kalkalpen.RunYear();

                    foreach (ResourceUnit ru in kalkalpen.Landscape.ResourceUnits)
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
                        Assert.IsTrue(kalkalpen.Landscape.ResourceUnitGrid.PhysicalExtent.Contains(ru.BoundingBox));
                        Assert.IsTrue((ru.BoundingBox.Height == Constant.RUSize) && (ru.BoundingBox.Width == Constant.RUSize) &&
                                      (ru.BoundingBox.X == 0.0F) && (MathF.Abs(ru.BoundingBox.Y % 100.0F) < 0.001F));
                        Assert.IsTrue(ru.EnvironmentID >= 0);
                        Assert.IsTrue(ru.ResourceUnitGridIndex >= 0);
                        Assert.IsTrue(ru.AreaInLandscape == Constant.RUArea);
                        Assert.IsTrue((ru.AreaWithTrees > 0.0) && (ru.AreaWithTrees <= Constant.RUArea));
                        Assert.IsTrue((ru.Trees.AverageLeafAreaWeightedAgingFactor > 0.0) && (ru.Trees.AverageLeafAreaWeightedAgingFactor < 1.0));
                        Assert.IsTrue((ru.Trees.AverageLightRelativeIntensity > 0.0) && (ru.Trees.AverageLightRelativeIntensity <= 1.0));
                        Assert.IsTrue((ru.Trees.PhotosyntheticallyActiveArea > 0.0) && (ru.Trees.PhotosyntheticallyActiveArea <= Constant.RUArea));
                        Assert.IsTrue((ru.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea > 0.0) && (ru.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea <= 1.0));
                        Assert.IsTrue((ru.Trees.TotalLeafArea > 0.0) && (ru.Trees.TotalLeafArea < 20.0F * Constant.RUArea));
                    }

                    Assert.IsTrue(kalkalpen.Landscape.ResourceUnits.Count == 2);
                    Assert.IsTrue(kalkalpen.Landscape.ResourceUnitGrid.Count == 2);

                    finalDiameters.Clear();
                    finalHeights.Clear();

                    foreach (Trees treesOfSpecies in kalkalpen.Landscape.ResourceUnits[0].Trees.TreesBySpeciesID.Values)
                    {
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            finalDiameters.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Dbh[treeIndex]);
                            finalHeights.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Height[treeIndex]);

                            Assert.IsTrue((treesOfSpecies.Age[treeIndex] > 0 + year) && (treesOfSpecies.Age[treeIndex] < 100 + year));
                            Assert.IsTrue(treesOfSpecies.GetBasalArea(treeIndex) > 0.0);
                            Assert.IsTrue((treesOfSpecies.CoarseRootMass[treeIndex] >= 0.0F) && (treesOfSpecies.CoarseRootMass[treeIndex] < 1E6F));
                            Assert.IsTrue((treesOfSpecies.Dbh[treeIndex] > 0.0F) && (treesOfSpecies.Dbh[treeIndex] < 200.0F));
                            Assert.IsTrue((treesOfSpecies.DbhDelta[treeIndex] >= 0.0F) && (treesOfSpecies.DbhDelta[treeIndex] < 10.0F));
                            Assert.IsTrue((treesOfSpecies.FineRootMass[treeIndex] > 0.0F) && (treesOfSpecies.FineRootMass[treeIndex] < 1E6F));
                            Assert.IsTrue((treesOfSpecies.FoliageMass[treeIndex] > 0.0F) && (treesOfSpecies.FoliageMass[treeIndex] < 1000.0F));
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
                            Assert.IsTrue((treesOfSpecies.Height[treeIndex] > 0.0F) && (treesOfSpecies.Height[treeIndex] < 100.0F));
                            Assert.IsTrue((treesOfSpecies.Tag[treeIndex] > 0) && (treesOfSpecies.Tag[treeIndex] < 40));
                            Assert.IsTrue((treesOfSpecies.LeafArea[treeIndex] > 0.0F) && (treesOfSpecies.LeafArea[treeIndex] < 1000.0F));
                            // Assert.IsTrue((tree.LightCellPosition);
                            Assert.IsTrue((treesOfSpecies.LightResourceIndex[treeIndex] > 0.0F) && (treesOfSpecies.LightResourceIndex[treeIndex] <= 1.0F));
                            Assert.IsTrue((treesOfSpecies.LightResponse[treeIndex] > -0.5F) && (treesOfSpecies.LightResponse[treeIndex] <= 1.0F));
                            Assert.IsTrue((treesOfSpecies.NppReserve[treeIndex] > 0.0F) && (treesOfSpecies.NppReserve[treeIndex] < 1E4F));
                            Assert.IsTrue((treesOfSpecies.Opacity[treeIndex] > 0.0F) && (treesOfSpecies.Opacity[treeIndex] <= 1.0F));
                            Assert.IsTrue(object.ReferenceEquals(treesOfSpecies.RU, kalkalpen.Landscape.ResourceUnits[0]));
                            // Assert.IsTrue(tree.Species.ID);
                            // Assert.IsTrue(tree.Stamp);
                            Assert.IsTrue((treesOfSpecies.StemMass[treeIndex] > 0.0) && (treesOfSpecies.CoarseRootMass[treeIndex] < 1E6));
                            Assert.IsTrue((treesOfSpecies.StressIndex[treeIndex] >= 0.0) && (treesOfSpecies.CoarseRootMass[treeIndex] < 1E6));
                        }

                        Assert.IsTrue(treesOfSpecies.Capacity == 4);
                        Assert.IsTrue(treesOfSpecies.Count == (treesOfSpecies.Species.ID == "psme" ? 2 : 1));
                    }

                    int minimumTreeCount = 30 - 2 * year - 3; // TODO: wide tolerance required due to stochastic mortality
                    int resourceUnit0treeSpeciesCount = kalkalpen.Landscape.ResourceUnits[0].Trees.TreesBySpeciesID.Count;
                    Assert.IsTrue(resourceUnit0treeSpeciesCount >= minimumTreeCount);
                    Assert.IsTrue(kalkalpen.Landscape.ResourceUnits[1].Trees.TreesBySpeciesID.Count >= minimumTreeCount);
                    Assert.IsTrue(initialDiameters.Count >= minimumTreeCount);
                    Assert.IsTrue(initialHeights.Count >= minimumTreeCount);
                    Assert.IsTrue(finalDiameters.Count >= minimumTreeCount);
                    Assert.IsTrue(finalHeights.Count >= minimumTreeCount);

                    float averageDiameterGrowth = 0.0F;
                    float averageHeightGrowth = 0.0F;
                    foreach (KeyValuePair<int, float> tree in finalHeights)
                    {
                        float initialDiameter = initialDiameters[tree.Key];
                        float initialHeight = initialHeights[tree.Key];
                        float finalDiameter = finalDiameters[tree.Key];
                        float finalHeight = tree.Value;
                        averageDiameterGrowth += finalDiameter - initialDiameter;
                        averageHeightGrowth += finalHeight - initialHeight;
                        Assert.IsTrue(finalDiameter >= initialDiameter);
                        Assert.IsTrue(finalDiameter < 1.1F * initialDiameter);
                        Assert.IsTrue(finalHeight >= initialHeight);
                        Assert.IsTrue(finalHeight < 1.1F * initialHeight);
                    }

                    averageDiameterGrowth /= resourceUnit0treeSpeciesCount;
                    averageHeightGrowth /= resourceUnit0treeSpeciesCount;

                    float maxLight = Single.MinValue;
                    float meanLight = 0.0F;
                    float minLight = Single.MaxValue;
                    for (int lightIndex = 0; lightIndex < kalkalpen.Landscape.LightGrid.Count; ++lightIndex)
                    {
                        float light = kalkalpen.Landscape.LightGrid[lightIndex];
                        maxLight = MathF.Max(light, maxLight);
                        meanLight += light;
                        minLight = MathF.Min(light, minLight);
                    }
                    meanLight /= kalkalpen.Landscape.LightGrid.Count;

                    float maxGridHeight = Single.MinValue;
                    float meanGridHeight = 0.0F;
                    float minGridHeight = Single.MaxValue;
                    for (int heightIndex = 0; heightIndex < kalkalpen.Landscape.HeightGrid.Count; ++heightIndex)
                    {
                        float height = kalkalpen.Landscape.HeightGrid[heightIndex].Height;
                        maxGridHeight = MathF.Max(height, maxGridHeight);
                        meanGridHeight += height;
                        minGridHeight = MathF.Min(height, minGridHeight);
                    }
                    meanGridHeight /= kalkalpen.Landscape.HeightGrid.Count;

                    Assert.IsTrue(averageDiameterGrowth > MathF.Max(0.2F - 0.01F * year, 0.0F));
                    Assert.IsTrue(averageHeightGrowth > MathF.Max(0.2F - 0.01F * year, 0.0F));
                    Assert.IsTrue(minGridHeight >= 0.0F);
                    Assert.IsTrue((meanGridHeight > minGridHeight) && (meanGridHeight < maxGridHeight));
                    Assert.IsTrue(maxGridHeight < 45.0F + 0.1F * year);
                    Assert.IsTrue(minLight >= 0.0F && minLight < 1.0F);
                    Assert.IsTrue((meanLight > minLight) && (meanLight < maxLight));
                    Assert.IsTrue(maxLight == 1.0F);
                }

                //kalkalpen.DebugTimers.WriteTimers();
            }

            //RumpleIndex rumpleIndex = new RumpleIndex();
            //rumpleIndex.Calculate(kalkalpen);
            //float index = rumpleIndex.Value(kalkalpen);
            //Assert.IsTrue(Math.Abs(index - 0.0) < 0.001);

            // check calculation: numbers for Jenness paper
            //float[] hs = new float[] { 165, 170, 145, 160, 183, 155, 122, 175, 190 };
            //float area = rumpleIndex.CalculateSurfaceArea(hs, 100);
        }

        [TestMethod]
        public void MalcolmKnapp14()
        {
            // spacing trials
            using Model plot14 = LandTest.LoadProject(LandTest.GetMalcolmKnappProjectPath(TestConstant.MalcolmKnapp.Plot14));

            // check soil properties at initial load
            ModelTest.VerifyMalcolmKnappResourceUnit(plot14);

            List<float> gppByYear = new List<float>();
            List<float> nppByYear = new List<float>();
            List<float> volumeByYear = new List<float>();
            for (int year = 0; year < 28; ++year)
            {
                plot14.RunYear();

                Assert.IsTrue(plot14.Landscape.ResourceUnits.Count == 1);
                float gpp = 0.0F;
                float npp = 0.0F;
                foreach (ResourceUnitTreeSpecies treeSpecies in plot14.Landscape.ResourceUnits[0].Trees.SpeciesAvailableOnResourceUnit)
                {
                    gpp += treeSpecies.BiomassGrowth.AnnualGpp;
                    npp += treeSpecies.Statistics.Npp[^1];
                }
                gppByYear.Add(gpp);
                nppByYear.Add(npp);

                float volume = 0.0F;
                foreach (Trees treesOfSpecies in plot14.Landscape.ResourceUnits[0].Trees.TreesBySpeciesID.Values)
                {
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        volume += treesOfSpecies.GetStemVolume(treeIndex);
                    }
                }
                volumeByYear.Add(volume);
            }

            ModelTest.VerifyMalcolmKnappClimate(plot14);
            ModelTest.VerifyMalcolmKnappModel(plot14);
            ModelTest.VerifyMalcolmKnappDouglasFir(plot14);

            List<float> nominalGppByYear = new List<float>()
            {
                12.088F, 12.704F, 14.728F, 13.042F, 14.137F, // 0...4
                12.213F, 13.230F, 14.942F, 13.980F, 13.482F, // 5...9
                14.701F, 12.654F, 12.992F, 12.971F, 13.054F, // 10...14
                13.479F, 13.384F, 12.628F, 11.413F, 13.063F, // 15...19
                13.830F, 11.426F, 13.380F, 12.314F, 14.363F, // 20...24
                13.567F, 12.582F, 13.153F                    // 25...27
            };
            List<float> nominalNppByYear = new List<float>()
            {
                14793.009F, 15600.609F, 18059.067F, 15911.426F, 17124.919F, // 0...4
                14653.807F, 15758.883F, 17664.319F, 16369.269F, 15572.519F, // 5...9
                16836.781F, 14400.212F, 14684.704F, 14532.705F, 14449.115F, // 10...14
                14835.988F, 14600.618F, 13617.208F, 12186.767F, 13798.192F, // 15...19
                14573.193F, 11968.184F, 13903.084F, 12706.724F, 14668.152F, // 20...24
                13717.823F, 12599.708F, 13039.824F                          // 25...27
            };
            List<float> nominalVolumeByYear = new List<float>()
            {
                143.076F, 157.751F, 175.994F, 190.599F, 207.294F, // 0...4
                216.857F, 229.379F, 248.292F, 263.868F, 277.212F, // 5...9
                290.818F, 302.361F, 310.121F, 321.509F, 332.719F, // 10...14
                345.902F, 356.460F, 368.301F, 373.557F, 383.531F, // 15...19
                397.316F, 404.170F, 414.817F, 421.084F, 433.938F, // 20...24
                446.297F, 456.167F, 465.235F                      // 25...27
            };
            for (int year = 0; year < nominalVolumeByYear.Count; ++year)
            {
                float gpp = gppByYear[year];
                float nominalGpp = nominalGppByYear[year];
                float relativeGppError = MathF.Abs(1.0F - gpp / nominalGpp);

                float npp = nppByYear[year];
                float nominalNpp = nominalNppByYear[year];
                float relativeNppError = MathF.Abs(1.0F - npp / nominalNpp);

                float volume = volumeByYear[year];
                float nominalVolume = nominalVolumeByYear[year];
                float relativeVolumeError = MathF.Abs(1.0F - volume / nominalVolume);

                Assert.IsTrue(relativeGppError < 0.02F, "Expected plot 14 to have a GPP of {0:0.000} kg/m² in simulation year {1} but the projected NPP {2:0.000} kg/m², a {3:0.0%} difference.", nominalGpp, year, gpp, relativeGppError);
                Assert.IsTrue(relativeNppError < 0.02F, "Expected plot 14 to have an NPP of {0:0.000} kg/ha in simulation year {1} but the projected NPP {2:0.000} kg/ha, a {3:0.0%} difference.", nominalNpp, year, npp, relativeNppError);
                Assert.IsTrue(relativeVolumeError < 0.02F, "Expected plot 14 to carry a standing volume of {0:0.000} m³ in simulation year {1} but the projected volume was {2:0.000} m³, a {3:0.0%} difference.", nominalVolume, year, volume, relativeVolumeError);
            }
        }

        [TestMethod]
        public void MalcolmKnapp16()
        {
            using Model plot16 = LandTest.LoadProject(LandTest.GetMalcolmKnappProjectPath(TestConstant.MalcolmKnapp.Plot16));

            // check soil properties at initial load
            ModelTest.VerifyMalcolmKnappResourceUnit(plot16);

            // 2019 - 1985 + 1 = 35 years of data available
            for (int year = 0; year < 35; ++year)
            {
                plot16.RunYear();
            }

            ModelTest.VerifyMalcolmKnappClimate(plot16);
            ModelTest.VerifyMalcolmKnappModel(plot16);
            ModelTest.VerifyMalcolmKnappDouglasFir(plot16);
        }

        [TestMethod]
        public void MalcolmKnappNelder()
        {
            using Model nelder1 = LandTest.LoadProject(LandTest.GetMalcolmKnappProjectPath(TestConstant.MalcolmKnapp.Nelder1));
            ModelTest.VerifyMalcolmKnappResourceUnit(nelder1);
            for (int year = 0; year < 26; ++year) // age 25 to 51
            {
                nelder1.RunYear();
            }

            ModelTest.VerifyMalcolmKnappClimate(nelder1);
            ModelTest.VerifyMalcolmKnappModel(nelder1);
            ModelTest.VerifyMalcolmKnappDouglasFir(nelder1);
        }

        private static void VerifyKalkalpenModel(Model model)
        {
            Assert.IsTrue(model.Landscape.Environment.ClimatesByName.Count == 1);
            Assert.IsTrue(model.Landscape.Dem == null);
            Assert.IsTrue(model.Landscape.HeightGrid.PhysicalExtent.Height == 200.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.Landscape.HeightGrid.PhysicalExtent.Width == 100.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.Landscape.HeightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(model.Landscape.HeightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(model.Landscape.HeightGrid.SizeX == 22);
            Assert.IsTrue(model.Landscape.HeightGrid.SizeY == 32);
            Assert.IsTrue(model.Landscape.LightGrid.PhysicalExtent.Height == 200.0F + 2.0F * 60.0F); // 100 x 200 m world + 60 m buffering = 220 x 320 m
            Assert.IsTrue(model.Landscape.LightGrid.PhysicalExtent.Width == 100.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.Landscape.LightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(model.Landscape.LightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(model.Landscape.LightGrid.SizeX == 110);
            Assert.IsTrue(model.Landscape.LightGrid.SizeY == 160);
            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 2);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.PhysicalExtent.Height == 200.0);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.PhysicalExtent.Width == 100.0);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.PhysicalExtent.X == 0.0);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.PhysicalExtent.Y == 0.0);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.SizeX == 1);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.SizeY == 2);
            Assert.IsTrue(model.Landscape.StandGrid == null);
            Assert.IsTrue(model.Project.Model.Settings.Multithreading == false);
        }

        private static void VerifyMalcolmKnappClimate(Model model)
        {
            Assert.IsTrue(model.Landscape.Environment.ClimatesByName.Count == 1);
            foreach (Climate climate in model.Landscape.Environment.ClimatesByName.Values)
            {
                Phenology conifer = climate.GetPhenology(0);
                // private phenology variables read from the project file
                //   vpdMin, vpdMax, dayLengthMin, dayLengthMax, tempMintempMax
                //conifer.ChillingDaysLastYear;
                //conifer.ID;
                //conifer.LeafOnEnd;
                //conifer.LeafOnFraction;
                //conifer.LeafOnStart;
                Phenology broadleaf = climate.GetPhenology(1);
                Phenology deciduousConifer = climate.GetPhenology(2);

                // private climate variables
                //   tableName, batchYears, temperatureShift, precipitationShift, randomSamplingEnabled, randomSamplingList, filter
                Assert.IsTrue(climate.CarbonDioxidePpm == 360.0);
                Assert.IsTrue((climate.MeanAnnualTemperature > 0.0) && (climate.MeanAnnualTemperature < 30.0));
                Assert.IsTrue(String.Equals(climate.Name, "HaneyUBC", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(conifer.LeafType == 0);
                Assert.IsTrue(broadleaf.LeafType == 1);
                Assert.IsTrue(deciduousConifer.LeafType == 2);
                // climate.PrecipitationMonth;
                Assert.IsTrue((climate.Sun.LastDayLongerThan10_5Hours > 0) && (climate.Sun.LastDayLongerThan10_5Hours < 365));
                Assert.IsTrue((climate.Sun.LastDayLongerThan14_5Hours > 0) && (climate.Sun.LastDayLongerThan14_5Hours < 365));
                Assert.IsTrue(climate.Sun.LongestDay == 172);
                Assert.IsTrue(climate.Sun.IsNorthernHemisphere());
                // climate.TemperatureMonth;
                Assert.IsTrue((climate.TotalAnnualRadiation > 4000.0) && (climate.TotalAnnualRadiation < 5000.0));
            }
        }

        private static void VerifyMalcolmKnappDouglasFir(Model model)
        {
            TreeSpecies douglasFir = model.Landscape.ResourceUnits[0].Trees.TreeSpeciesSet[0];
            Assert.IsTrue(douglasFir.Active);
            Assert.IsTrue(String.Equals(douglasFir.ID, "psme", StringComparison.Ordinal));
            // maximumAge  500
            // maximumHeight   90
            // aging   1 / (1 + (x / 0.7) ^ 2)
            // douglasFir.Aging();
            // barkThickness   0.065
            // bmWoody_a   0.10568
            // bmWoody_b   2.4247
            // bmFoliage_a 0.058067
            // bmFoliage_b 1.7009
            // bmRoot_a    0.03575375
            // bmRoot_b    2.1641
            // bmBranch_a  0.024871
            // bmBranch_b  2.1382
            Assert.IsTrue(Math.Abs(douglasFir.CNRatioFineRoot - 42.11) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.CNRatioFoliage - 72.0) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.CNRatioWood - 880.33) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.DeathProbabilityFixed - 0.00080063445425371249) < 0.000001); // transformed from 0.67
            // probStress  6.9
            // displayColor D6F288
            Assert.IsTrue(douglasFir.EstablishmentParameters.ChillRequirement == 56);
            Assert.IsTrue(Math.Abs(douglasFir.EstablishmentParameters.FrostTolerance - 0.5) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.EstablishmentParameters.GrowingDegreeDaysBaseTemperature - 3.4) < 0.001);
            Assert.IsTrue(douglasFir.EstablishmentParameters.GddBudBurst == 255);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MaximumGrowingDegreeDays == 3261);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MinimumGrowingDegreeDays == 177);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MinimumFrostFreeDays == 65);
            Assert.IsTrue(Math.Abs(douglasFir.EstablishmentParameters.MinTemp + 37.0) < 0.001);
            Assert.IsTrue(Double.IsNaN(douglasFir.EstablishmentParameters.PsiMin));
            Assert.IsTrue(Math.Abs(douglasFir.FecundityM2 - 114.0) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.FecunditySerotiny - 0.0) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.FinerootFoliageRatio - 0.75) < 0.001);
            // HDlow   145.0998 * 1 * 0.8 * (1 - 0.28932) * d ^ -0.28932
            // HDhigh  100 / d + 25 + 100 * exp(-0.3 * (0.08 * d) ^ 1.5) + 120 * exp(-0.01 * d)
            Assert.IsTrue(String.Equals(douglasFir.ID, "psme", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.Index == 0);
            Assert.IsTrue(douglasFir.IsConiferous == true);
            Assert.IsTrue(douglasFir.IsEvergreen == true);
            Assert.IsTrue(douglasFir.IsSeedYear == false);
            Assert.IsTrue(douglasFir.IsTreeSerotinousRandom(model.RandomGenerator, 40) == false);
            // lightResponseClass  2.78
            Assert.IsTrue(Math.Abs(douglasFir.MaxCanopyConductance - 0.017) < 0.001);
            Assert.IsTrue(String.Equals(douglasFir.Name, "Pseudotsuga menzisii", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(Math.Abs(douglasFir.NonSeedYearFraction - 0.25) < 0.001);
            Assert.IsTrue(douglasFir.PhenologyClass == 0);
            Assert.IsTrue(Math.Abs(douglasFir.PsiMin + 1.234) < 0.001);
            // respNitrogenClass   2
            // respTempMax 20
            // respTempMin 0
            // respVpdExponent - 0.6
            // maturityYears   14
            // seedYearInterval    5
            // nonSeedYearFraction 0.25
            // seedKernel_as1  30
            // seedKernel_as2  200
            // seedKernel_ks0  0.2
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.BrowsingProbability - 0.3509615) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.HeightDiameterRatio - 89.0F) < 0.001);
            Assert.IsTrue(String.Equals(douglasFir.SaplingGrowthParameters.HeightGrowthPotential.ExpressionString, "41.4*(1-(1-(h/41.4)^(1/3))*exp(-0.0408))^3", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.MaxStressYears == 3);
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.ReferenceRatio - 0.506) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.ReinekeR - 159.0) < 0.001);
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.RepresentedClasses.Count == 41);
            Assert.IsTrue(Double.IsNaN(douglasFir.SaplingGrowthParameters.SproutGrowth));
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.StressThreshold - 0.1) < 0.001);
            Assert.IsTrue(douglasFir.SeedDispersal == null);
            // Assert.IsTrue(String.Equals(douglasFir.SeedDispersal.DumpNextYearFileName, null, StringComparison.OrdinalIgnoreCase));
            // Assert.IsTrue(douglasFir.SeedDispersal.SeedMap == null);
            // Assert.IsTrue(Object.ReferenceEquals(douglasFir.SeedDispersal.Species, douglasFir));
            Assert.IsTrue(douglasFir.SnagHalflife == 40);
            Assert.IsTrue(Math.Abs(douglasFir.SnagDecompositionRate - 0.08) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.LitterDecompositionRate - 0.322) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.CoarseWoodyDebrisDecompositionRate - 0.1791) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SpecificLeafArea - 5.84) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.TurnoverLeaf - 0.18) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.TurnoverFineRoot - 0.617284) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.VolumeFactor - 0.423492) < 0.001); // 0.539208 * pi/4
            Assert.IsTrue(Math.Abs(douglasFir.WoodDensity - 450.0) < 0.001);

            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                Assert.IsTrue(Object.ReferenceEquals(douglasFir.SpeciesSet, ru.Trees.TreeSpeciesSet));
                Assert.IsTrue(ru.Trees.TreeSpeciesSet.Count == 1);
                Assert.IsTrue(Object.ReferenceEquals(douglasFir, ru.Trees.TreeSpeciesSet.ActiveSpecies[0]));
            }
        }

        private static void VerifyMalcolmKnappModel(Model model)
        {
            Assert.IsTrue(model.Landscape.Environment.UseDynamicAvailableNitrogen == false);

            Assert.IsTrue(model.Project.Model.Ecosystem.AirDensity == 1.204F);
            Assert.IsTrue(model.Project.Model.Ecosystem.BoundaryLayerConductance == 0.2F);
            Assert.IsTrue(model.Project.Model.Ecosystem.Epsilon == 2.7F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LaiThresholdForClosedStands == 3.0F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LightExtinctionCoefficient == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LightExtinctionCoefficientOpacity == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TemperatureTau == 6.0F);
            Assert.IsTrue(model.Project.Model.Settings.RegenerationEnabled == false);
            Assert.IsTrue(model.Project.Model.Settings.MortalityEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.GrowthEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.CarbonCycleEnabled == true);
            Assert.IsTrue(model.Project.Model.Settings.UseParFractionBelowGroundAllocation == true);
            Assert.IsTrue(Math.Abs(model.Project.World.Geometry.Latitude - 49.261F) < 0.003);
            Assert.IsTrue(model.Project.World.Geometry.IsTorus == true);
        }

        private static void VerifyMalcolmKnappResourceUnit(Model model)
        {
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                // resource unit variables read from climate file which are aren't currently test accessible
                //   ru.Snags: swdC, swdCount, swdCN, swdHalfLife, swdDecomRate, otherC, other CN
                // resource unit variables read from project file which are aren't currently test accessible
                //   ru.Soil: qb, qh, el, er, leaching, nitrogenDeposition, soilDepth,
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
                AssertNullable.IsNotNull(ru.Soil);
                Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.C - 161.086F) < 0.001F);
                Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.N - 17.73954F) < 0.00001F);
                Assert.IsTrue(MathF.Abs(ru.Soil.PlantAvailableNitrogen - 56.186F) < 0.001F);
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.C - 4.8414983F) < 0.001F);
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.N - 0.2554353F) < 0.0001F);
                Assert.IsTrue(ru.Soil.YoungLabile.DecompositionRate == 0.322F);
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.C - 45.97414F) < 0.001F);
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.N - 0.261731F) < 0.0001F);
                Assert.IsTrue(ru.Soil.YoungRefractory.DecompositionRate == 0.1790625F);
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;
                Assert.IsTrue(ru.WaterCycle.CanopyConductance == 0.0); // initially zero
                Assert.IsTrue((ru.WaterCycle.CurrentSoilWaterContent >= 0.0) && (ru.WaterCycle.CurrentSoilWaterContent <= ru.WaterCycle.FieldCapacity));
                Assert.IsTrue(Math.Abs(ru.WaterCycle.FieldCapacity - 66.304010358336242) < 0.001);
                Assert.IsTrue(ru.WaterCycle.SoilWaterPsi.Length == Constant.DaysInLeapYear);
                foreach (float psi in ru.WaterCycle.SoilWaterPsi)
                {
                    Assert.IsTrue((psi <= 0.0F) && (psi > -6000.0F));
                }
                Assert.IsTrue((ru.WaterCycle.SnowDayRadiation >= 0.0) && (ru.WaterCycle.SnowDayRadiation < 5000.0)); // TODO: linkt to snow days?
                Assert.IsTrue((ru.WaterCycle.SnowDays >= 0.0) && (ru.WaterCycle.SnowDays <= Constant.DaysInLeapYear));
                Assert.IsTrue(Math.Abs(ru.WaterCycle.SoilDepth - 1340) < 0.001);
                Assert.IsTrue(ru.WaterCycle.TotalEvapotranspiration == 0.0); // zero at initialization
                Assert.IsTrue(ru.WaterCycle.TotalRunoff == 0.0); // zero at initialization
            }

            Assert.IsTrue(model.Landscape.ResourceUnits.Count == 1);
            Assert.IsTrue(model.Landscape.ResourceUnitGrid.Count == 1);
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
            Assert.IsTrue(Math.Abs(species.GetBiomassFoliage(2) - 0.281777) < 0.001);
            Assert.IsTrue(Math.Abs(species.GetBiomassFoliage(20) - 10.23070) < 0.001);
            Assert.IsTrue(Math.Abs(species.GetBiomassFoliage(50) - 42.72598) < 0.001);
            Assert.IsTrue(Math.Abs(species.GetBiomassFoliage(100) - 125.97920) < 0.001);

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
            double shortPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Evaluate(0.25);
            double mediumPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Evaluate(1);
            double tallPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Evaluate(4.5);

            Assert.IsTrue(Math.Abs(shortPotential - 0.431) < 0.01);
            Assert.IsTrue(Math.Abs(mediumPotential - 1.367) < 0.01);
            Assert.IsTrue(Math.Abs(tallPotential - 5.202) < 0.01);
        }
    }
}
