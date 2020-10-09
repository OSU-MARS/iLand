using iLand.Core;
using iLand.Tools;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class ModelTest : LandTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void MalcolmKnapp()
        {
            string projectDirectory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp");
            // spacing trials
            using Model plot14 = this.LoadProject(Path.Combine(projectDirectory, "plot 14.xml"));
            using Model plot16 = this.LoadProject(Path.Combine(projectDirectory, "plot 16.xml"));

            // check soil properties at initial load
            foreach (ResourceUnit ru in plot16.ResourceUnits)
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
                //ru.Soil.ClimateFactor;
                //ru.Soil.FluxToAtmosphere;
                //ru.Soil.FluxToDisturbance;
                // settings from project file are ignored and values from climate file are used 
                //Assert.IsTrue(Math.Abs(ru.Soil.OrganicMatter.C - 128.666) < 0.001);
                //Assert.IsTrue(Math.Abs(ru.Soil.OrganicMatter.N - 0.08368) < 0.00001);
                //Assert.IsTrue(Math.Abs(ru.Soil.YoungLabile.C - 12.375) < 0.001);
                //Assert.IsTrue(Math.Abs(ru.Soil.YoungLabile.N - 0.6521) < 0.0001);
                //Assert.IsTrue(ru.Soil.YoungLabile.Weight == 0.227); // TODO: why is decomposition rate read as weight?
                //Assert.IsTrue(Math.Abs(ru.Soil.YoungRefractory.C - 33.832) < 0.001);
                //Assert.IsTrue(Math.Abs(ru.Soil.YoungRefractory.N - 0.1212) < 0.0001);
                //Assert.IsTrue(ru.Soil.YoungRefractory.Weight == 0.071); // decomposition rate
                // Assert.IsTrue(Math.Abs(ru.Soil.AvailableNitrogen - 56.18682579) < 0.001); // BUGBUG: ignored in climate file
                Assert.IsTrue(Math.Abs(ru.Soil.OrganicMatter.C - 161.086884) < 0.001);
                Assert.IsTrue(Math.Abs(ru.Soil.OrganicMatter.N - 17.73954044) < 0.00001);
                Assert.IsTrue(Math.Abs(ru.Soil.YoungLabile.C - 4.841498397) < 0.001);
                Assert.IsTrue(Math.Abs(ru.Soil.YoungLabile.N - 0.2554353319) < 0.0001);
                Assert.IsTrue(ru.Soil.YoungLabile.Weight == 0.322);
                Assert.IsTrue(Math.Abs(ru.Soil.YoungRefractory.C - 45.97414423) < 0.001);
                Assert.IsTrue(Math.Abs(ru.Soil.YoungRefractory.N - 0.261731921) < 0.0001);
                Assert.IsTrue(ru.Soil.YoungRefractory.Weight == 0.1790625);
                //ru.Variables.CarbonToAtm;
                //ru.Variables.CarbonUptake;
                //ru.Variables.CumCarbonToAtm;
                //ru.Variables.CumCarbonUptake;
                //ru.Variables.CumNep;
                //ru.Variables.Nep;
                Assert.IsTrue(Math.Abs(ru.Variables.NitrogenAvailable - 56.18682579) < 0.0001); // BUGBUG: read from climate file but never used, project file setting is silently ignored
            }

            for (int year = 0; year < 28; ++year)
            {
                plot14.RunYear();
                plot16.RunYear();
            }

            Assert.IsTrue(plot16.ModelSettings.RegenerationEnabled == false);
            Assert.IsTrue(plot16.ModelSettings.MortalityEnabled == true);
            Assert.IsTrue(plot16.ModelSettings.GrowthEnabled == true);
            Assert.IsTrue(plot16.ModelSettings.CarbonCycleEnabled == true);
            Assert.IsTrue(plot16.ModelSettings.Epsilon == 2.7);
            Assert.IsTrue(plot16.ModelSettings.LightExtinctionCoefficient == 0.6);
            Assert.IsTrue(plot16.ModelSettings.LightExtinctionCoefficientOpacity == 0.6);
            Assert.IsTrue(plot16.ModelSettings.TemperatureTau == 6.0);
            Assert.IsTrue(plot16.ModelSettings.AirDensity == 1.204);
            Assert.IsTrue(plot16.ModelSettings.LaiThresholdForClosedStands == 3.0);
            Assert.IsTrue(plot16.ModelSettings.BoundaryLayerConductance == 0.2);
            Assert.IsTrue(plot16.ModelSettings.UseDynamicAvailableNitrogen == false);
            Assert.IsTrue(plot16.ModelSettings.UseParFractionBelowGroundAllocation == true);
            Assert.IsTrue(plot16.ModelSettings.TorusMode == true);
            Assert.IsTrue(Math.Abs(plot16.ModelSettings.Latitude - Global.ToRadians(49.259)) < 0.0001);

            foreach (Climate climate in plot16.Climates)
            {
                Phenology conifer = climate.Phenology(0);
                // private phenology variables read from the project file
                //   vpdMin, vpdMax, dayLengthMin, dayLengthMax, tempMintempMax
                //conifer.ChillingDaysLastYear;
                //conifer.ID;
                //conifer.LeafOnEnd;
                //conifer.LeafOnFraction;
                //conifer.LeafOnStart;
                Phenology broadleaf = climate.Phenology(1);
                Phenology deciduousConifer = climate.Phenology(2);

                // private climate variables
                //   tableName, batchYears, temperatureShift, precipitationShift, randomSamplingEnabled, randomSamplingList, filter
                Assert.IsTrue(climate.CarbonDioxidePpm == 360.0);
                Assert.IsTrue((climate.MeanAnnualTemperature > 0.0) && (climate.MeanAnnualTemperature < 30.0));
                Assert.IsTrue(String.Equals(climate.Name, "HaneyUBC", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(conifer.ID == 0);
                Assert.IsTrue(broadleaf.ID == 1);
                Assert.IsTrue(deciduousConifer.ID == 2);
                // climate.PrecipitationMonth;
                Assert.IsTrue((climate.Sun.LastDayLongerThan10_5Hours > 0) && (climate.Sun.LastDayLongerThan10_5Hours < 365));
                Assert.IsTrue((climate.Sun.LastDayLongerThan14_5Hours > 0) && (climate.Sun.LastDayLongerThan14_5Hours < 365));
                Assert.IsTrue(climate.Sun.LongestDay == 172);
                Assert.IsTrue(climate.Sun.NorthernHemisphere());
                // climate.TemperatureMonth;
                Assert.IsTrue((climate.TotalAnnualRadiation > 4000.0) && (climate.TotalAnnualRadiation < 5000.0));
            }

            // Nelder plot
            using Model nelder1 = this.LoadProject(Path.Combine(projectDirectory, "Nelder 1.xml"));
            for (int year = 0; year < 26; ++year) // age 25 to 51
            {
                nelder1.RunYear();
            }
        }

        /// <summary>
        /// Samples expressions on Species for correctness: aging, foliage, height-diameter ratio, and sapling growth potential.
        /// </summary>
        [TestMethod]
        public void Species()
        {
            using Model model = this.LoadProject(this.GetDefaultProjectPath(this.TestContext));
            Species species = model.FirstResourceUnit().SpeciesSet.GetSpecies("piab");
            Assert.IsTrue(species != null);

            // PIAB: 1/(1 + (x/0.55)^2)
            double youngAgingFactor = species.Aging(model.GlobalSettings, 10.0F, 10);
            double middleAgingFactor = species.Aging(model.GlobalSettings, 40.0F, 80);
            double oldAgingFactor = species.Aging(model.GlobalSettings, 55.5F, 575);

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
            species.GetHeightDiameterRatioLimits(model.GlobalSettings, 3.3, out double lowLimitSmall, out double highLimitSmall);
            species.GetHeightDiameterRatioLimits(model.GlobalSettings, 10.0, out double lowLimitMedium, out double highLimitMedium);
            species.GetHeightDiameterRatioLimits(model.GlobalSettings, 33, out double lowLimitLarge, out double highLimitLarge);
              
            Assert.IsTrue(Math.Abs(lowLimitSmall - 93.58) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitMedium - 53.76) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitLarge - 29.59) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitSmall - 112.15) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitMedium - 85.99) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitLarge - 64.59) < 0.01);

            // PIAB: 44.7*(1-(1-(h/44.7)^(1/3))*exp(-0.044))^3
            // round(44.7*(1-(1-(c(0.25, 1, 4.5)/44.7)^(1/3))*exp(-0.044))^3, 3)
            double shortPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model.GlobalSettings, 0.25);
            double mediumPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model.GlobalSettings, 1);
            double tallPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model.GlobalSettings, 4.5);
              
            Assert.IsTrue(Math.Abs(shortPotential - 0.431) < 0.01);
            Assert.IsTrue(Math.Abs(mediumPotential - 1.367) < 0.01);
            Assert.IsTrue(Math.Abs(tallPotential - 5.202) < 0.01);
        }

        [TestMethod]
        public void YearSteps()
        {
            using Model model = this.LoadProject(this.GetDefaultProjectPath(this.TestContext));

            Assert.IsTrue(model.Climates.Count == 1);
            Assert.IsTrue(model.Dem == null);
            Assert.IsTrue(model.HeightGrid.PhysicalExtent.Height == 200.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.HeightGrid.PhysicalExtent.Width == 100.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.HeightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(model.HeightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(model.HeightGrid.CellsX == 22);
            Assert.IsTrue(model.HeightGrid.CellsY == 32);
            Assert.IsTrue(model.IsSetup == true);
            Assert.IsTrue(model.LightGrid.PhysicalExtent.Height == 200.0F + 2.0F * 60.0F); // 100 x 200 m world + 60 m buffering = 220 x 320 m
            Assert.IsTrue(model.LightGrid.PhysicalExtent.Width == 100.0F + 2.0F * 60.0F);
            Assert.IsTrue(model.LightGrid.PhysicalExtent.X == -60.0);
            Assert.IsTrue(model.LightGrid.PhysicalExtent.Y == -60.0);
            Assert.IsTrue(model.LightGrid.CellsX == 110);
            Assert.IsTrue(model.LightGrid.CellsY == 160);
            Assert.IsTrue(model.ResourceUnits.Count == 2);
            Assert.IsTrue(model.ResourceUnitGrid.PhysicalExtent.Height == 200.0);
            Assert.IsTrue(model.ResourceUnitGrid.PhysicalExtent.Width == 100.0);
            Assert.IsTrue(model.ResourceUnitGrid.PhysicalExtent.X == 0.0);
            Assert.IsTrue(model.ResourceUnitGrid.PhysicalExtent.Y == 0.0);
            Assert.IsTrue(model.ResourceUnitGrid.CellsX == 1);
            Assert.IsTrue(model.ResourceUnitGrid.CellsY == 2);
            Assert.IsTrue(model.StandGrid == null);
            Assert.IsTrue(model.ThreadRunner.IsMultithreaded == false);

            Dictionary<int, float> initialDiameters = new Dictionary<int, float>();
            Dictionary<int, float> initialHeights = new Dictionary<int, float>();
            Dictionary<int, float> finalDiameters = new Dictionary<int, float>();
            Dictionary<int, float> finalHeights = new Dictionary<int, float>();
            for (int year = 0; year < 3; ++year)
            {
                initialDiameters.Clear();
                initialHeights.Clear();
                foreach (Tree tree in model.ResourceUnits[0].Trees)
                {
                    initialDiameters.Add(tree.ID, tree.Dbh);
                    initialHeights.Add(tree.ID, tree.Height);
                }

                model.RunYear();

                foreach (ResourceUnit ru in model.ResourceUnits)
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
                    Assert.IsTrue(model.ResourceUnitGrid.PhysicalExtent.Contains(ru.BoundingBox));
                    Assert.IsTrue((ru.AverageAging > 0.0) && (ru.AverageAging < 1.0));
                    Assert.IsTrue((ru.EffectiveAreaPerWla > 0.0) && (ru.EffectiveAreaPerWla <= 1.0));
                    Assert.IsTrue(ru.ID >= 0);
                    Assert.IsTrue(ru.Index >= 0);
                    Assert.IsTrue((ru.LriModifier > 0.0) && (ru.LriModifier <= 1.0));
                    Assert.IsTrue((ru.ProductiveArea > 0.0) && (ru.ProductiveArea <= Constant.RUArea));
                    Assert.IsTrue(ru.StockableArea == Constant.RUArea);
                    Assert.IsTrue((ru.StockedArea > 0.0) && (ru.StockedArea <= Constant.RUArea));
                    Assert.IsTrue((ru.TotalLeafArea > 0.0) && (ru.TotalLeafArea < 20.0 * Constant.RUArea));
                }

                finalDiameters.Clear();
                finalHeights.Clear();
                foreach (Tree tree in model.ResourceUnits[0].Trees)
                {
                    finalDiameters.Add(tree.ID, tree.Dbh);
                    finalHeights.Add(tree.ID, tree.Height);
                }

                int minimumTreeCount = 31 - 4 * year;
                int resourceUnit0treeCount = model.ResourceUnits[0].Trees.Count;
                Assert.IsTrue(resourceUnit0treeCount >= minimumTreeCount);
                Assert.IsTrue(model.ResourceUnits[1].Trees.Count >= minimumTreeCount);
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
                for (int lightIndex = 0; lightIndex < model.LightGrid.Count; ++lightIndex)
                {
                    float light = model.LightGrid[lightIndex];
                    maxLight = MathF.Max(light, maxLight);
                    meanLight += light;
                    minLight = MathF.Min(light, minLight);
                }
                meanLight /= model.LightGrid.Count;

                float maxGridHeight = Single.MinValue;
                float meanGridHeight = 0.0F;
                float minGridHeight = Single.MaxValue;
                for (int heightIndex = 0; heightIndex < model.HeightGrid.Count; ++heightIndex)
                {
                    float height = model.HeightGrid[heightIndex].Height;
                    maxGridHeight = MathF.Max(height, maxGridHeight);
                    meanGridHeight += height;
                    minGridHeight = MathF.Min(height, minGridHeight);
                }
                meanGridHeight /= model.HeightGrid.Count;

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
