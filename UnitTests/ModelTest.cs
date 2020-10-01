using iLand.Core;
using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace iLand.Test
{
    [TestClass]
    public class ModelTest : LandTest
    {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            LandTest.EnsureModel(testContext);
        }

        /// <summary>
        /// Samples expressions on Species for correctness: aging, foliage, height-diameter ratio, and sapling growth potential.
        /// </summary>
        [TestMethod]
        public void Species()
        {
            Species species = LandTest.Model.FirstResourceUnit().SpeciesSet.GetSpecies("piab");
            Assert.IsTrue(species != null);

            // PIAB: 1/(1 + (x/0.55)^2)
            double youngAgingFactor = species.Aging(10.0F, 10);
            double middleAgingFactor = species.Aging(40.0F, 80);
            double oldAgingFactor = species.Aging(55.5F, 575);

            Assert.IsTrue(Math.Abs(youngAgingFactor - 0.964912) < 0.001);
            Assert.IsTrue(Math.Abs(middleAgingFactor - 0.481931) < 0.001);
            Assert.IsTrue(Math.Abs(oldAgingFactor - 0.2375708) < 0.001);

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
            species.GetHeightDiameterRatioLimits(3.3, out double lowLimitSmall, out double highLimitSmall);
            species.GetHeightDiameterRatioLimits(10.0, out double lowLimitMedium, out double highLimitMedium);
            species.GetHeightDiameterRatioLimits(33, out double lowLimitLarge, out double highLimitLarge);
              
            Assert.IsTrue(Math.Abs(lowLimitSmall - 93.58) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitMedium - 53.76) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitLarge - 29.59) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitSmall - 112.15) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitMedium - 85.99) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitLarge - 64.59) < 0.01);

            // PIAB: 44.7*(1-(1-(h/44.7)^(1/3))*exp(-0.044))^3
            // round(44.7*(1-(1-(c(0.25, 1, 4.5)/44.7)^(1/3))*exp(-0.044))^3, 3)
            double shortPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(0.25);
            double mediumPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(1);
            double tallPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(4.5);
              
            Assert.IsTrue(Math.Abs(shortPotential - 0.431) < 0.01);
            Assert.IsTrue(Math.Abs(mediumPotential - 1.367) < 0.01);
            Assert.IsTrue(Math.Abs(tallPotential - 5.202) < 0.01);
        }

        [TestMethod]
        public void YearSteps()
        {
            Assert.IsTrue(LandTest.Model.Climates.Count == 1);
            Assert.IsTrue(LandTest.Model.Dem == null);
            Assert.IsTrue(LandTest.Model.HeightGrid.PhysicalExtent.Height == 260.0);
            Assert.IsTrue(LandTest.Model.HeightGrid.PhysicalExtent.Width == 160.0);
            Assert.IsTrue(LandTest.Model.HeightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(LandTest.Model.HeightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(LandTest.Model.HeightGrid.CellsX == 16);
            Assert.IsTrue(LandTest.Model.HeightGrid.CellsY == 26);
            Assert.IsTrue(LandTest.Model.IsSetup == true);
            Assert.IsTrue(LandTest.Model.LightGrid.PhysicalExtent.Height == 2.0 * 130); // 100 x 200 m world + 60 m buffering = 160 x 260 m
            Assert.IsTrue(LandTest.Model.LightGrid.PhysicalExtent.Width == 2.0 * 80);
            Assert.IsTrue(LandTest.Model.LightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(LandTest.Model.LightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(LandTest.Model.LightGrid.CellsX == 80);
            Assert.IsTrue(LandTest.Model.LightGrid.CellsY == 130);
            Assert.IsTrue(LandTest.Model.ResourceUnits.Count == 2);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.PhysicalExtent.Height == 200.0);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.PhysicalExtent.Width == 100.0);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.PhysicalExtent.X == 0.0);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.PhysicalExtent.Y == 0.0);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.CellsX == 1);
            Assert.IsTrue(LandTest.Model.ResourceUnitGrid.CellsY == 2);
            Assert.IsTrue(LandTest.Model.StandGrid == null);
            Assert.IsTrue(ThreadRunner.IsMultithreaded == false);

            Dictionary<int, float> initialDiameters = new Dictionary<int, float>();
            Dictionary<int, float> initialHeights = new Dictionary<int, float>();
            Dictionary<int, float> finalDiameters = new Dictionary<int, float>();
            Dictionary<int, float> finalHeights = new Dictionary<int, float>();
            for (int year = 0; year < 3; ++year)
            {
                initialDiameters.Clear();
                initialHeights.Clear();
                foreach (Tree tree in LandTest.Model.ResourceUnits[0].Trees)
                {
                    initialDiameters.Add(tree.ID, tree.Dbh);
                    initialHeights.Add(tree.ID, tree.Height);
                }

                LandTest.Model.RunYear();

                finalDiameters.Clear();
                finalHeights.Clear();
                foreach (Tree tree in LandTest.Model.ResourceUnits[0].Trees)
                {
                    finalDiameters.Add(tree.ID, tree.Dbh);
                    finalHeights.Add(tree.ID, tree.Height);
                }

                int minimumTreeCount = 31 - 4 * year;
                int resourceUnit0treeCount = LandTest.Model.ResourceUnits[0].Trees.Count;
                Assert.IsTrue(resourceUnit0treeCount >= minimumTreeCount);
                Assert.IsTrue(LandTest.Model.ResourceUnits[1].Trees.Count >= minimumTreeCount);
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

                averageDiameterGrowth /= resourceUnit0treeCount;
                averageHeightGrowth /= resourceUnit0treeCount;

                float maxLight = Single.MinValue;
                float meanLight = 0.0F;
                float minLight = Single.MaxValue;
                for (int lightIndex = 0; lightIndex < LandTest.Model.LightGrid.Count; ++lightIndex)
                {
                    float light = LandTest.Model.LightGrid[lightIndex];
                    maxLight = MathF.Max(light, maxLight);
                    meanLight += light;
                    minLight = MathF.Min(light, minLight);
                }
                meanLight /= LandTest.Model.LightGrid.Count;

                float maxGridHeight = Single.MinValue;
                float meanGridHeight = 0.0F;
                float minGridHeight = Single.MaxValue;
                for (int heightIndex = 0; heightIndex < LandTest.Model.HeightGrid.Count; ++heightIndex)
                {
                    float height = LandTest.Model.HeightGrid[heightIndex].Height;
                    maxGridHeight = MathF.Max(height, maxGridHeight);
                    meanGridHeight += height;
                    minGridHeight = MathF.Min(height, minGridHeight);
                }
                meanGridHeight /= LandTest.Model.HeightGrid.Count;

                Assert.IsTrue(averageDiameterGrowth > 0.2F);
                Assert.IsTrue(averageHeightGrowth > 0.2F);
                Assert.IsTrue(minGridHeight >= 0.0F);
                Assert.IsTrue((meanGridHeight > minGridHeight) && (meanGridHeight < maxGridHeight));
                Assert.IsTrue(maxGridHeight < 50.0F);
                Assert.IsTrue(minLight >= 0.0F && minLight < 1.0F);
                Assert.IsTrue((meanLight > minLight) && (meanLight < maxLight));
                Assert.IsTrue(maxLight == 1.0F);
            }

            DebugTimer.WriteTimers();
        }
    }
}
