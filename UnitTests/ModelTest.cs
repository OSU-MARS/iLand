using iLand.Core;
using iLand.Tools;
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
        public void Kalkalpen()
        {
            using Model kalkalpen = this.LoadProject(this.GetKalkalpenProjectPath(this.TestContext));

            this.VerifyKalkalpenModel(kalkalpen);
            this.VerifyNorwaySpruce(kalkalpen);

            Dictionary<int, float> initialDiameters = new Dictionary<int, float>();
            Dictionary<int, float> initialHeights = new Dictionary<int, float>();
            Dictionary<int, float> finalDiameters = new Dictionary<int, float>();
            Dictionary<int, float> finalHeights = new Dictionary<int, float>();
            for (int year = 0; year < 3; ++year)
            {
                initialDiameters.Clear();
                initialHeights.Clear();
                foreach (Tree tree in kalkalpen.ResourceUnits[0].Trees)
                {
                    initialDiameters.Add(tree.ID, tree.Dbh);
                    initialHeights.Add(tree.ID, tree.Height);
                }

                kalkalpen.RunYear();

                foreach (ResourceUnit ru in kalkalpen.ResourceUnits)
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
                    Assert.IsTrue(kalkalpen.ResourceUnitGrid.PhysicalExtent.Contains(ru.BoundingBox));
                    Assert.IsTrue((ru.AverageAging > 0.0) && (ru.AverageAging < 1.0));
                    Assert.IsTrue((ru.EffectiveAreaPerWla > 0.0) && (ru.EffectiveAreaPerWla <= 1.0));
                    Assert.IsTrue(ru.ID >= 0);
                    Assert.IsTrue(ru.Index >= 0);
                    Assert.IsTrue((ru.LriModifier > 0.0) && (ru.LriModifier <= 1.0));
                    Assert.IsTrue((ru.PhotosyntheticallyActiveArea > 0.0) && (ru.PhotosyntheticallyActiveArea <= Constant.RUArea));
                    Assert.IsTrue(ru.StockableArea == Constant.RUArea);
                    Assert.IsTrue((ru.StockedArea > 0.0) && (ru.StockedArea <= Constant.RUArea));
                    Assert.IsTrue((ru.TotalLeafArea > 0.0) && (ru.TotalLeafArea < 20.0 * Constant.RUArea));
                }

                finalDiameters.Clear();
                finalHeights.Clear();
                foreach (Tree tree in kalkalpen.ResourceUnits[0].Trees)
                {
                    finalDiameters.Add(tree.ID, tree.Dbh);
                    finalHeights.Add(tree.ID, tree.Height);
                }

                int minimumTreeCount = 31 - 5 * year;
                int resourceUnit0treeCount = kalkalpen.ResourceUnits[0].Trees.Count;
                Assert.IsTrue(resourceUnit0treeCount >= minimumTreeCount);
                Assert.IsTrue(kalkalpen.ResourceUnits[1].Trees.Count >= minimumTreeCount);
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
                for (int lightIndex = 0; lightIndex < kalkalpen.LightGrid.Count; ++lightIndex)
                {
                    float light = kalkalpen.LightGrid[lightIndex];
                    maxLight = MathF.Max(light, maxLight);
                    meanLight += light;
                    minLight = MathF.Min(light, minLight);
                }
                meanLight /= kalkalpen.LightGrid.Count;

                float maxGridHeight = Single.MinValue;
                float meanGridHeight = 0.0F;
                float minGridHeight = Single.MaxValue;
                for (int heightIndex = 0; heightIndex < kalkalpen.HeightGrid.Count; ++heightIndex)
                {
                    float height = kalkalpen.HeightGrid[heightIndex].Height;
                    maxGridHeight = MathF.Max(height, maxGridHeight);
                    meanGridHeight += height;
                    minGridHeight = MathF.Min(height, minGridHeight);
                }
                meanGridHeight /= kalkalpen.HeightGrid.Count;

                Assert.IsTrue(averageDiameterGrowth > MathF.Max(0.2F - 0.01F * year, 0.0F));
                Assert.IsTrue(averageHeightGrowth > MathF.Max(0.2F - 0.01F * year, 0.0F));
                Assert.IsTrue(minGridHeight >= 0.0F);
                Assert.IsTrue((meanGridHeight > minGridHeight) && (meanGridHeight < maxGridHeight));
                Assert.IsTrue(maxGridHeight < 45.0F + 0.1F * year);
                Assert.IsTrue(minLight >= 0.0F && minLight < 1.0F);
                Assert.IsTrue((meanLight > minLight) && (meanLight < maxLight));
                Assert.IsTrue(maxLight == 1.0F);
            }

            kalkalpen.DebugTimers.WriteTimers();

            RumpleIndex rumpleIndex = new RumpleIndex();
            rumpleIndex.Calculate(kalkalpen);
            double index = rumpleIndex.Value(kalkalpen);
            Assert.IsTrue(Math.Abs(index - 0.0) < 0.001);

            // check calculation: numbers for Jenness paper
            //float[] hs = new float[] { 165, 170, 145, 160, 183, 155, 122, 175, 190 };
            //double area = rumpleIndex.CalculateSurfaceArea(hs, 100);
        }

        [TestMethod]
        public void MalcolmKnapp14()
        {
            string projectDirectory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp");
            // spacing trials
            using Model plot14 = this.LoadProject(Path.Combine(projectDirectory, "plot 14.xml"));

            // check soil properties at initial load
            this.VerifyMalcolmKnappResourceUnit(plot14);

            for (int year = 0; year < 28; ++year)
            {
                plot14.RunYear();
            }

            this.VerifyMalcolmKnappClimate(plot14);
            this.VerifyMalcolmKnappModel(plot14);
            this.VerifyMalcolmKnappDouglasFir(plot14);
        }

        [TestMethod]
        public void MalcolmKnapp16()
        {
            string projectDirectory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp");
            using Model plot16 = this.LoadProject(Path.Combine(projectDirectory, "plot 16.xml"));

            // check soil properties at initial load
            this.VerifyMalcolmKnappResourceUnit(plot16);

            // 2019 - 1985 + 1 = 35 years of data available
            for (int year = 0; year < 35; ++year)
            {
                plot16.RunYear();
            }

            this.VerifyMalcolmKnappClimate(plot16);
            this.VerifyMalcolmKnappModel(plot16);
            this.VerifyMalcolmKnappDouglasFir(plot16);
        }

        [TestMethod]
        public void MalcolmKnappNelder()
        {
            string projectDirectory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp");

            using Model nelder1 = this.LoadProject(Path.Combine(projectDirectory, "Nelder 1.xml"));
            this.VerifyMalcolmKnappResourceUnit(nelder1);
            for (int year = 0; year < 26; ++year) // age 25 to 51
            {
                nelder1.RunYear();
            }

            this.VerifyMalcolmKnappClimate(nelder1);
            this.VerifyMalcolmKnappModel(nelder1);
            this.VerifyMalcolmKnappDouglasFir(nelder1);
        }

        private void VerifyKalkalpenModel(Model model)
        {
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
        }

        private void VerifyMalcolmKnappClimate(Model model)
        {
            Assert.IsTrue(model.Climates.Count == 1);
            foreach (Climate climate in model.Climates)
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
        }

        private void VerifyMalcolmKnappDouglasFir(Model model)
        {
            Assert.IsTrue(model.SpeciesSet().SpeciesCount() == 1);

            Species douglasFir = model.SpeciesSet().Species(0);
            Assert.IsTrue(douglasFir.Active);
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
            Assert.IsTrue(Math.Abs(douglasFir.DeathProbabilityIntrinsic - 0.00080063445425371249) < 0.000001); // transformed from 0.67
            // probStress  6.9
            // displayColor D6F288
            Assert.IsTrue(douglasFir.EstablishmentParameters.ChillRequirement == 56);
            Assert.IsTrue(Math.Abs(douglasFir.EstablishmentParameters.FrostTolerance - 0.5) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.EstablishmentParameters.GddBaseTemperature - 3.4) < 0.001);
            Assert.IsTrue(douglasFir.EstablishmentParameters.GddBudBurst == 255);
            Assert.IsTrue(douglasFir.EstablishmentParameters.GddMax == 3261);
            Assert.IsTrue(douglasFir.EstablishmentParameters.GddMin == 177);
            Assert.IsTrue(douglasFir.EstablishmentParameters.MinFrostFree == 65);
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
            Assert.IsTrue(douglasFir.IsTreeSerotinous(model, 40) == false);
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
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.HdSapling - 89.0F) < 0.001);
            Assert.IsTrue(String.Equals(douglasFir.SaplingGrowthParameters.HeightGrowthPotential.ExpressionString, "41.4*(1-(1-(h/41.4)^(1/3))*exp(-0.0408))^3", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.MaxStressYears == 3);
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.ReferenceRatio - 0.506) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.ReinekesR - 159.0) < 0.001);
            Assert.IsTrue(douglasFir.SaplingGrowthParameters.RepresentedClasses.Count == 41);
            Assert.IsTrue(Double.IsNaN(douglasFir.SaplingGrowthParameters.SproutGrowth));
            Assert.IsTrue(Math.Abs(douglasFir.SaplingGrowthParameters.StressThreshold - 0.1) < 0.001);
            Assert.IsTrue(douglasFir.SeedDispersal == null);
            // Assert.IsTrue(String.Equals(douglasFir.SeedDispersal.DumpNextYearFileName, null, StringComparison.OrdinalIgnoreCase));
            // Assert.IsTrue(douglasFir.SeedDispersal.SeedMap == null);
            // Assert.IsTrue(Object.ReferenceEquals(douglasFir.SeedDispersal.Species, douglasFir));
            Assert.IsTrue(douglasFir.SnagHalflife == 40);
            Assert.IsTrue(Math.Abs(douglasFir.SnagKsw - 0.08) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SnagKyl - 0.322) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.SnagKyr - 0.1791) < 0.001);
            Assert.IsTrue(Object.ReferenceEquals(douglasFir.SpeciesSet, model.SpeciesSet()));
            Assert.IsTrue(Math.Abs(douglasFir.SpecificLeafArea - 5.84) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.TurnoverLeaf - 0.18) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.TurnoverRoot - 0.617284) < 0.001);
            Assert.IsTrue(Math.Abs(douglasFir.VolumeFactor - 0.423492) < 0.001); // 0.539208 * pi/4
            Assert.IsTrue(Math.Abs(douglasFir.WoodDensity - 450.0) < 0.001);
        }

        private void VerifyMalcolmKnappModel(Model model)
        {
            Assert.IsTrue(model.ModelSettings.RegenerationEnabled == false);
            Assert.IsTrue(model.ModelSettings.MortalityEnabled == true);
            Assert.IsTrue(model.ModelSettings.GrowthEnabled == true);
            Assert.IsTrue(model.ModelSettings.CarbonCycleEnabled == true);
            Assert.IsTrue(model.ModelSettings.Epsilon == 2.7);
            Assert.IsTrue(model.ModelSettings.LightExtinctionCoefficient == 0.6);
            Assert.IsTrue(model.ModelSettings.LightExtinctionCoefficientOpacity == 0.6);
            Assert.IsTrue(model.ModelSettings.TemperatureTau == 6.0);
            Assert.IsTrue(model.ModelSettings.AirDensity == 1.204);
            Assert.IsTrue(model.ModelSettings.LaiThresholdForClosedStands == 3.0);
            Assert.IsTrue(model.ModelSettings.BoundaryLayerConductance == 0.2);
            Assert.IsTrue(model.ModelSettings.UseDynamicAvailableNitrogen == false);
            Assert.IsTrue(model.ModelSettings.UseParFractionBelowGroundAllocation == true);
            Assert.IsTrue(model.ModelSettings.TorusMode == true);
            Assert.IsTrue(Math.Abs(model.ModelSettings.Latitude - Global.ToRadians(49.259)) < 0.0001);
        }

        private void VerifyMalcolmKnappResourceUnit(Model model)
        {
            foreach (ResourceUnit ru in model.ResourceUnits)
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
                Assert.IsTrue(ru.WaterCycle.CanopyConductance == 0.0); // initially zero
                Assert.IsTrue((ru.WaterCycle.CurrentSoilWaterContent >= 0.0) && (ru.WaterCycle.CurrentSoilWaterContent <= ru.WaterCycle.FieldCapacity));
                Assert.IsTrue(Math.Abs(ru.WaterCycle.FieldCapacity - 20.006) < 0.001);
                Assert.IsTrue(ru.WaterCycle.Psi.Length == Constant.DaysInLeapYear);
                foreach (double psi in ru.WaterCycle.Psi)
                {
                    Assert.IsTrue((psi <= 0.0) && (psi > -6000.0));
                }
                Assert.IsTrue((ru.WaterCycle.SnowDayRad >= 0.0) && (ru.WaterCycle.SnowDayRad < 5000.0)); // TODO: linkt to snow days?
                Assert.IsTrue((ru.WaterCycle.SnowDays >= 0.0) && (ru.WaterCycle.SnowDays <= Constant.DaysInLeapYear));
                Assert.IsTrue(Math.Abs(ru.WaterCycle.SoilDepth - 112.8) < 0.001);
                Assert.IsTrue(ru.WaterCycle.TotalEvapotranspiration == 0.0); // zero at initialization
                Assert.IsTrue(ru.WaterCycle.TotalWaterLoss == 0.0); // zero at initialization
            }
        }

        private void VerifyNorwaySpruce(Model model)
        {
            Species species = model.FirstResourceUnit().SpeciesSet.GetSpecies("piab");
            Assert.IsTrue(species != null);

            // PIAB: 1/(1 + (x/0.55)^2)
            double youngAgingFactor = species.Aging(model, 10.0F, 10);
            double middleAgingFactor = species.Aging(model, 40.0F, 80);
            double oldAgingFactor = species.Aging(model, 55.5F, 575);

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
            species.GetHeightDiameterRatioLimits(model, 3.3, out double lowLimitSmall, out double highLimitSmall);
            species.GetHeightDiameterRatioLimits(model, 10.0, out double lowLimitMedium, out double highLimitMedium);
            species.GetHeightDiameterRatioLimits(model, 33, out double lowLimitLarge, out double highLimitLarge);

            Assert.IsTrue(Math.Abs(lowLimitSmall - 93.58) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitMedium - 53.76) < 0.01);
            Assert.IsTrue(Math.Abs(lowLimitLarge - 29.59) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitSmall - 112.15) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitMedium - 85.99) < 0.01);
            Assert.IsTrue(Math.Abs(highLimitLarge - 64.59) < 0.01);

            // PIAB: 44.7*(1-(1-(h/44.7)^(1/3))*exp(-0.044))^3
            // round(44.7*(1-(1-(c(0.25, 1, 4.5)/44.7)^(1/3))*exp(-0.044))^3, 3)
            double shortPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model, 0.25);
            double mediumPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model, 1);
            double tallPotential = species.SaplingGrowthParameters.HeightGrowthPotential.Calculate(model, 4.5);

            Assert.IsTrue(Math.Abs(shortPotential - 0.431) < 0.01);
            Assert.IsTrue(Math.Abs(mediumPotential - 1.367) < 0.01);
            Assert.IsTrue(Math.Abs(tallPotential - 5.202) < 0.01);
        }
    }
}
