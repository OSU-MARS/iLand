using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Climate = iLand.World.Climate;
using Model = iLand.Simulation.Model;

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

                Dictionary<int, float> initialDiameters = new();
                Dictionary<int, float> initialHeights = new();
                Dictionary<int, float> finalDiameters = new();
                Dictionary<int, float> finalHeights = new();
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
                        Assert.IsTrue(ru.Trees.TreeStatisticsByStandID.Count == 0);
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

            List<float> gppByYear = new();
            List<float> nppByYear = new();
            List<float> stemVolumeByYear = new();
            for (int year = 0; year < 28; ++year)
            {
                plot14.RunYear();

                Assert.IsTrue(plot14.Landscape.ResourceUnits.Count == 1);
                float gpp = 0.0F;
                float npp = 0.0F;
                foreach (ResourceUnitTreeSpecies treeSpecies in plot14.Landscape.ResourceUnits[0].Trees.SpeciesAvailableOnResourceUnit)
                {
                    gpp += treeSpecies.BiomassGrowth.AnnualGpp;
                    npp += treeSpecies.Statistics.TreeNpp;
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
                stemVolumeByYear.Add(volume);

                Assert.IsTrue(plot14.Landscape.ResourceUnits[0].Trees.TreeStatisticsByStandID.Count == 1);
                Assert.IsTrue(plot14.Landscape.ResourceUnits[0].Trees.TreeStatisticsByStandID.ContainsKey(14));
            }

            ModelTest.VerifyMalcolmKnappClimate(plot14);
            ModelTest.VerifyMalcolmKnappModel(plot14);
            ModelTest.VerifyMalcolmKnappDouglasFir(plot14);

            // regex for reformatting copy/paste of values from watch window: "\s+\[\d+]\s+(\d+.\d{1,3})\d*\s+float\r?\n" -> "$1F, "
            List<float> nominalGppByYear = new()
            {
                10.331F, 11.133F, 14.020F, 11.316F, 13.527F, // 0...4
                10.526F, 12.332F, 12.791F, 12.987F, 11.235F, // 5...9
                11.608F, 10.062F, 11.081F,  9.992F, 12.681F, // 10...14
                11.237F, 11.539F, 10.230F,  8.268F,  9.310F, // 15...19
                11.888F,  8.964F, 11.145F, 10.091F, 13.221F, // 20...24
                10.973F, 12.179F, 12.647F                    // 25...27
            };
            
            List<float> nominalNppByYear = new()
            {
                13305.625F, 14514.859F, 18456.353F, 15041.932F, 18053.941F,
                14110.686F, 16558.890F, 17210.685F, 17502.668F, 15159.530F,
                15669.442F, 13588.600F, 14967.475F, 13497.672F, 17130.728F,
                15179.787F, 15582.680F, 13804.854F, 11158.202F, 12561.510F,
                16028.833F, 12079.922F, 15009.008F, 13583.033F, 17776.400F,
                14749.142F, 16356.83F, 16963.591F
            };
            List<float> nominalVolumeByYear = new()
            {
                118.143F, 130.357F, 148.674F, 161.134F, 178.615F,
                189.416F, 204.427F, 220.182F, 236.591F, 248.959F,
                261.886F, 271.750F, 283.939F, 293.349F, 309.284F,
                320.494F, 333.093F, 340.758F, 346.781F, 355.119F,
                368.386F, 374.821F, 386.607F, 395.546F, 410.205F,
                422.316F, 437.456F, 452.937F
            };

            ResourceUnitTreeStatisticsWithPreviousYears plotStatistics = (ResourceUnitTreeStatisticsWithPreviousYears)plot14.Landscape.ResourceUnits[0].Trees.TreeStatisticsByStandID[14];
            for (int year = 0; year < nominalVolumeByYear.Count; ++year)
            {
                float gpp = gppByYear[year];
                float nominalGpp = nominalGppByYear[year];
                float relativeGppError = MathF.Abs(1.0F - gpp / nominalGpp);

                float npp = nppByYear[year];
                float nominalNpp = nominalNppByYear[year];
                float plotNpp = plotStatistics.TreeNppByYear[year];
                float relativeNppError = MathF.Abs(1.0F - npp / nominalNpp);

                float stemVolume = stemVolumeByYear[year];
                float nominalVolume = nominalVolumeByYear[year];
                float plotVolume = plotStatistics.LiveStemVolumeByYear[year];
                float relativeVolumeError = MathF.Abs(1.0F - stemVolume / nominalVolume);

                Assert.IsTrue(relativeGppError < 0.02F, "Expected plot 14 to have a GPP of {0:0.000} kg/m² in simulation year {1} but the projected GPP was {2:0.000} kg/m², a {3:0.0%} difference.", nominalGpp, year, gpp, relativeGppError);
                Assert.IsTrue(relativeNppError < 0.02F, "Expected plot 14 to have an NPP of {0:0.000} kg/ha in simulation year {1} but the projected NPP was {2:0.000} kg/ha, a {3:0.0%} difference.", nominalNpp, year, npp, relativeNppError);
                Assert.IsTrue(relativeVolumeError < 0.02F, "Expected plot 14 to carry a standing volume of {0:0.000} m³ in simulation year {1} but the projected volume was {2:0.000} m³, a {3:0.0%} difference.", nominalVolume, year, stemVolume, relativeVolumeError);
                Assert.IsTrue(npp == plotNpp);
                Assert.IsTrue(MathF.Abs(stemVolume - plotVolume) < 0.0001F);

                // plot statistics are multiplied by expansion factor obtained from portion of resource unit occupied to obtain per hectare values
                if (year == 0)
                {
                    // sanity checks on initial state
                    Assert.IsTrue(plotStatistics.AverageDbhByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.AverageHeightByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.BasalAreaByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.LeafAreaIndexByYear[year] >= 1.0F);
                    Assert.IsTrue(plotStatistics.LiveStemVolumeByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.LiveAndSnagStemVolumeByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.TreeNppAbovegroundByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.TreeNppByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.TreeCountByYear[year] == 222.0F);

                    Assert.IsTrue(plotStatistics.CohortCountByYear[year] == 0);
                    Assert.IsTrue(plotStatistics.MeanSaplingAgeByYear[year] == 0.0F);
                    Assert.IsTrue(plotStatistics.SaplingNppByYear[year] == 0.0F);

                    Assert.IsTrue(plotStatistics.BranchCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.BranchNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.CoarseRootCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.CoarseRootNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.FineRootCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.FineRootNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.FoliageCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.FoliageNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.RegenerationCarbonByYear[year] == 0.0F);
                    Assert.IsTrue(plotStatistics.RegenerationNitrogenByYear[year] == 0.0F);
                    Assert.IsTrue(plotStatistics.SaplingCountByYear[year] == 0);
                    Assert.IsTrue(plotStatistics.StemCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(plotStatistics.StemNitrogenByYear[year] > 0.0F);
                }
                else
                {
                    // sanity checks on growth
                    int previousYear = year - 1;
                    Assert.IsTrue(plotStatistics.AverageDbhByYear[year] > plotStatistics.AverageDbhByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.AverageHeightByYear[year] > plotStatistics.AverageHeightByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.BasalAreaByYear[year] > 0.95F * plotStatistics.BasalAreaByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.LeafAreaIndexByYear[year] > 0.95F * plotStatistics.LeafAreaIndexByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.LiveStemVolumeByYear[year] > 0.95F * plotStatistics.LiveStemVolumeByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.LiveAndSnagStemVolumeByYear[year] >= 0.95F * plotStatistics.LiveAndSnagStemVolumeByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.StemCarbonByYear[year] > 0.99F * plotStatistics.StemCarbonByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.StemNitrogenByYear[year] > 0.99F * plotStatistics.StemNitrogenByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.TreeCountByYear[year] <= plotStatistics.TreeCountByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.TreeNppAbovegroundByYear[year] > 0.67F * plotStatistics.TreeNppAbovegroundByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.TreeNppByYear[year] > 0.75F * plotStatistics.TreeNppByYear[previousYear]);

                    Assert.IsTrue(plotStatistics.MeanSaplingAgeByYear[year] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(plotStatistics.RegenerationCarbonByYear[year] == 0.0F);
                    Assert.IsTrue(plotStatistics.RegenerationNitrogenByYear[year] == 0.0F);
                    Assert.IsTrue(plotStatistics.SaplingNppByYear[year] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(plotStatistics.SaplingCountByYear[year] == 0);

                    Assert.IsTrue(plotStatistics.BranchCarbonByYear[year] > 0.95F * plotStatistics.BranchCarbonByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.BranchNitrogenByYear[year] > 0.95F * plotStatistics.BranchNitrogenByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.CoarseRootCarbonByYear[year] > 0.95F * plotStatistics.CoarseRootCarbonByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.CoarseRootNitrogenByYear[year] > 0.95F * plotStatistics.CoarseRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.CohortCountByYear[year] == 0); // no saplings at initialization and regeneration not enabled
                    Assert.IsTrue(plotStatistics.FineRootCarbonByYear[year] > 0.95F * plotStatistics.FineRootCarbonByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.FineRootNitrogenByYear[year] > 0.95F * plotStatistics.FineRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.FoliageCarbonByYear[year] > 0.95F * plotStatistics.FoliageCarbonByYear[previousYear]);
                    Assert.IsTrue(plotStatistics.FoliageNitrogenByYear[year] > 0.95F * plotStatistics.FoliageNitrogenByYear[previousYear]);

                    // sanity checks on ranges
                    Assert.IsTrue(plotStatistics.LiveStemVolumeByYear[year] >= plotStatistics.LiveAndSnagStemVolumeByYear[year]);
                    Assert.IsTrue((plotStatistics.LeafAreaIndexByYear[year] > 1.0F) && (plotStatistics.LeafAreaIndexByYear[year] < 20.0F));
                }
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

        [TestMethod]
        public void PacificNorthwestSpecies()
        {
            Project pnwProject = new(LandTest.GetPacificNorthwestProjectPath(this.TestContext!));

            TreeSpeciesSet pnwSpecies = new(Constant.Database.DefaultSpeciesTable);
            pnwSpecies.Setup(pnwProject);

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
            Assert.IsTrue((abam != null) && (abgr != null) && (abpr != null) && (acma != null) && (alru != null) && (pisi != null) &&
                          (pipo != null) && (psme != null) && (tshe != null) && (tsme != null) && (thpl != null));
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
            Assert.IsTrue(MathF.Abs(douglasFir.CNRatioFineRoot - 9.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.CNRatioFoliage - 60.3F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.CNRatioWood - 452.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.DeathProbabilityFixed - 0.00355005264F) < 0.000001F); // transformed from 0.67
            // probStress  6.9
            // displayColor D6F288
            Assert.IsTrue(douglasFir.EstablishmentParameters.ChillRequirement == 30);
            Assert.IsTrue(MathF.Abs(douglasFir.EstablishmentParameters.FrostTolerance - 0.5F) < 0.001);
            Assert.IsTrue(MathF.Abs(douglasFir.EstablishmentParameters.GrowingDegreeDaysBaseTemperature - 3.4F) < 0.001F);
            Assert.IsTrue(douglasFir.EstablishmentParameters.GddBudBurst == 255);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MaximumGrowingDegreeDays == 3261);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MinimumGrowingDegreeDays == 177);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MinimumFrostFreeDays == 65);
            Assert.IsTrue(MathF.Abs(douglasFir.EstablishmentParameters.MinTemp + 37.0F) < 0.001F);
            Assert.IsTrue(Single.IsNaN(douglasFir.EstablishmentParameters.PsiMin));
            Assert.IsTrue(MathF.Abs(douglasFir.FecundityM2 - 20.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.FecunditySerotiny - 0.0F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.FinerootFoliageRatio - 1.0F) < 0.001F);
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
            Assert.IsTrue(String.Equals(douglasFir.Name, "Pseudotsuga menziesii", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(Math.Abs(douglasFir.NonSeedYearFraction - 0.25) < 0.001);
            Assert.IsTrue(douglasFir.PhenologyClass == 0);
            Assert.IsTrue(Math.Abs(douglasFir.MinimumSoilWaterPotential + 1.234) < 0.001);
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
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowthParameters.BrowsingProbability - 0.5F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowthParameters.HeightDiameterRatio - 112.0F) < 0.001F);
            Assert.IsTrue(String.Equals(douglasFir.SaplingGrowthParameters.HeightGrowthPotential.ExpressionString, "1.2*72.2*(1-(1-(h/72.2)^(1/3))*exp(-0.0427))^3", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.MaxStressYears == 2);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowthParameters.ReferenceRatio - 0.503F) < 0.001F);
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowthParameters.ReinekeR - 164.0F) < 0.001F);
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.RepresentedClasses.Count == 41);
            Assert.IsTrue(Single.IsNaN(douglasFir.SaplingGrowthParameters.SproutGrowth));
            Assert.IsTrue(MathF.Abs(douglasFir.SaplingGrowthParameters.StressThreshold - 0.1F) < 0.001F);
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

            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                Assert.IsTrue(Object.ReferenceEquals(douglasFir.SpeciesSet, ru.Trees.TreeSpeciesSet));
                Assert.IsTrue(ru.Trees.TreeSpeciesSet.Count == 11);
                Assert.IsTrue(Object.ReferenceEquals(douglasFir, ru.Trees.TreeSpeciesSet.ActiveSpecies[0]));
            }
        }

        private static void VerifyMalcolmKnappModel(Model model)
        {
            Assert.IsTrue(model.Landscape.Environment.UseDynamicAvailableNitrogen == false);

            Assert.IsTrue(model.Project.Model.Ecosystem.AirDensity == 1.204F);
            Assert.IsTrue(model.Project.Model.Ecosystem.BoundaryLayerConductance == 0.2F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LightUseEpsilon == 2.7F);
            Assert.IsTrue(model.Project.Model.Ecosystem.LaiThresholdForConstantStandConductance == 3.0F);
            Assert.IsTrue(model.Project.Model.Ecosystem.ResourceUnitLightExtinctionCoefficient == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient == 0.6F);
            Assert.IsTrue(model.Project.Model.Ecosystem.TemperatureAveragingTau == 6.0F);
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
                Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.C - 161.086F) < 0.001F, "Soil: organic carbon");
                Assert.IsTrue(MathF.Abs(ru.Soil.OrganicMatter.N - 17.73954F) < 0.00001F, "Soil: organic nitrogen");
                Assert.IsTrue(MathF.Abs(ru.Soil.PlantAvailableNitrogen - 56.186F) < 0.001F, "Soil: plant available nitrogen");
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.C - 4.8414983F) < 0.001F, "Soil: young labile carbon");
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungLabile.N - 0.2554353F) < 0.0001F, "Soil: young labile nitrogen");
                Assert.IsTrue(ru.Soil.YoungLabile.DecompositionRate == 0.322F, "Soil: young labile decomposition rate");
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.C - 45.97414F) < 0.001F, "Soil: young refractory carbon");
                Assert.IsTrue(MathF.Abs(ru.Soil.YoungRefractory.N - 0.261731F) < 0.0001F, "Soil: young refractory nitrogen");
                Assert.IsTrue(ru.Soil.YoungRefractory.DecompositionRate == 0.1790625F, "Soil: young refractory decomposition rate");
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;
                Assert.IsTrue(ru.WaterCycle.CanopyConductance == 0.0F, "Water cycle: canopy conductance"); // initially zero
                Assert.IsTrue((ru.WaterCycle.CurrentSoilWaterContent >= 0.0) && (ru.WaterCycle.CurrentSoilWaterContent <= ru.WaterCycle.FieldCapacity), "Soil: current water content");
                Assert.IsTrue(MathF.Abs(ru.WaterCycle.FieldCapacity - 29.2064552F) < 0.001F, "Soil: field capacity");
                Assert.IsTrue(ru.WaterCycle.SoilWaterPsi.Length == Constant.DaysInLeapYear, "Water cycle: water potential length");
                foreach (float psi in ru.WaterCycle.SoilWaterPsi)
                {
                    Assert.IsTrue((psi <= 0.0F) && (psi > -6000.0F), "Water cycle: water potential");
                }
                Assert.IsTrue((ru.WaterCycle.SnowDayRadiation >= 0.0F) && (ru.WaterCycle.SnowDayRadiation < 5000.0F), "Water cycle: snow radiation"); // TODO: linkt to snow days?
                Assert.IsTrue((ru.WaterCycle.SnowDays >= 0.0F) && (ru.WaterCycle.SnowDays <= Constant.DaysInLeapYear), "Water cycle: snow days");
                Assert.IsTrue(Math.Abs(ru.WaterCycle.SoilDepth - 1340.0F) < 0.001F, "Soil: depth");
                Assert.IsTrue(ru.WaterCycle.TotalEvapotranspiration == 0.0F, "Soil: evapotranspiration"); // zero at initialization
                Assert.IsTrue(ru.WaterCycle.TotalRunoff == 0.0F, "Soil: runoff"); // zero at initialization
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
