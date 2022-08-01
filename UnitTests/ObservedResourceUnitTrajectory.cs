using iLand.Output;
using iLand.Tree;
using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace iLand.Test
{
    internal class ObservedResourceUnitTrajectory
    {
        public float GppTolerance { get; init; }
        public float NonmonotonicGrowthTolerance { get; init; }
        public float NppTolerance { get; init; }
        public float StemVolumeTolerance { get; init; }
        public float TreeNppTolerance { get; init; }

        public List<float> ObservedGppByYear { get; private init; }
        public List<float> ObservedNppByYear { get; private init; }
        public List<float> ObservedStemVolumeByYear { get; private init; }

        public ObservedResourceUnitTrajectory()
        {
            this.GppTolerance = 0.02F;
            this.NonmonotonicGrowthTolerance = 0.05F;
            this.NppTolerance = 0.02F;
            this.StemVolumeTolerance = 0.02F;
            this.TreeNppTolerance = 0.33F;

            this.ObservedGppByYear = new();
            this.ObservedNppByYear = new();
            this.ObservedStemVolumeByYear = new();
        }

        public void AddYear(ResourceUnit resourceUnit)
        {
            float gpp = 0.0F;
            float npp = 0.0F;
            foreach (ResourceUnitTreeSpecies treeSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
            {
                gpp += treeSpecies.TreeGrowth.AnnualGpp;
                npp += treeSpecies.StatisticsLive.TreeNppPerHa;
            }
            this.ObservedGppByYear.Add(gpp);
            this.ObservedNppByYear.Add(npp);

            float stemVolume = 0.0F;
            foreach (Trees treesOfSpecies in resourceUnit.Trees.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Assert.IsTrue(treesOfSpecies.IsDead(treeIndex) == false);
                    stemVolume += treesOfSpecies.GetStemVolume(treeIndex);
                }
            }
            this.ObservedStemVolumeByYear.Add(stemVolume);
        }

        public void Verify(StandOrResourceUnitTrajectory actualTrajectory, float maximumTreeCount, List<float> expectedGppByYear, List<float> expectedNppByYear, List<float> expectedVolumeByYear)
        {
            float growthMultiplier = 1.0F - this.NonmonotonicGrowthTolerance;
            float treeNppMultiplier = 1.0F - this.TreeNppTolerance;

            for (int simulationYear = 0; simulationYear < expectedVolumeByYear.Count; ++simulationYear)
            {
                float observedGpp = this.ObservedGppByYear[simulationYear];
                float expectedGpp = expectedGppByYear[simulationYear];
                float relativeGppError;
                if (expectedGpp != 0.0F)
                {
                    relativeGppError = MathF.Abs(1.0F - observedGpp / expectedGpp);
                }
                else
                {
                    relativeGppError = observedGpp; // fall back to absolute error from zero since relative error is not well defined 
                }

                float observedNpp = this.ObservedNppByYear[simulationYear];
                float expectedNpp = expectedNppByYear[simulationYear];
                float recordedNpp = actualTrajectory.TreeNppByYear[simulationYear];
                float relativeNppError;
                if (expectedNpp != 0.0F)
                {
                    relativeNppError = MathF.Abs(1.0F - observedNpp / expectedNpp);
                }
                else
                {
                    relativeNppError = observedNpp;
                }

                float observedStemVolume = this.ObservedStemVolumeByYear[simulationYear];
                float expectedStemVolume = expectedVolumeByYear[simulationYear];
                float recordedVolume = actualTrajectory.LiveStemVolumeByYear[simulationYear];
                float relativeVolumeError = MathF.Abs(1.0F - observedStemVolume / expectedStemVolume);

                Assert.IsTrue(relativeGppError < this.GppTolerance, "Expected GPP of {0:0.000} kg/m² in simulation year {1} but the GPP recorded was {2:0.000} kg/m², a {3:0.0%} difference.", expectedGpp, simulationYear, observedGpp, relativeGppError);
                Assert.IsTrue(relativeNppError < this.NppTolerance, "Expected NPP of {0:0.000} kg/ha in simulation year {1} but the NPP recorded was {2:0.000} kg/ha, a {3:0.0%} difference.", expectedNpp, simulationYear, observedNpp, relativeNppError);
                Assert.IsTrue(relativeVolumeError < this.StemVolumeTolerance, "Expected stem volume of {0:0.000} m³ in simulation year {1} but the stem volume recorded was {2:0.000} m³, a {3:0.0%} difference.", expectedStemVolume, simulationYear, observedStemVolume, relativeVolumeError);
                Assert.IsTrue(observedNpp == recordedNpp);
                Assert.IsTrue(MathF.Abs(observedStemVolume - recordedVolume) < 0.001F);

                // plot statistics are multiplied by expansion factor obtained from portion of resource unit occupied to obtain per hectare values
                if (simulationYear == 0)
                {
                    // sanity checks on initial state
                    Assert.IsTrue(actualTrajectory.AverageDbhByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.AverageHeightByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.BasalAreaByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.LeafAreaIndexByYear[simulationYear] >= 1.0F);
                    Assert.IsTrue(actualTrajectory.LiveStemVolumeByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.TreeNppAbovegroundByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.TreeNppByYear[simulationYear] == 0.0F);
                    float treeCount = actualTrajectory.TreesPerHectareByYear[simulationYear];
                    Assert.IsTrue((treeCount >= 0.0F) && (treeCount <= maximumTreeCount));
                    Assert.IsTrue(actualTrajectory.CohortsPerHectareByYear[simulationYear] == 0);
                    Assert.IsTrue(actualTrajectory.MeanSaplingAgeByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.SaplingNppByYear[simulationYear] == 0.0F);

                    Assert.IsTrue(actualTrajectory.BranchCarbonByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.BranchNitrogenByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.CoarseRootCarbonByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.CoarseRootNitrogenByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.FineRootCarbonByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.FineRootNitrogenByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.FoliageCarbonByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.FoliageNitrogenByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.RegenerationCarbonByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.RegenerationNitrogenByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.SaplingsPerHectareByYear[simulationYear] == 0);
                    Assert.IsTrue(actualTrajectory.StemCarbonByYear[simulationYear] > 0.0F);
                    Assert.IsTrue(actualTrajectory.StemNitrogenByYear[simulationYear] > 0.0F);
                }
                else
                {
                    // sanity checks on growth and mortality
                    int previousYear = simulationYear - 1;
                    Assert.IsTrue(actualTrajectory.AverageDbhByYear[simulationYear] > growthMultiplier * actualTrajectory.AverageDbhByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.AverageHeightByYear[simulationYear] > growthMultiplier * actualTrajectory.AverageHeightByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.BasalAreaByYear[simulationYear] > growthMultiplier * actualTrajectory.BasalAreaByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.LeafAreaIndexByYear[simulationYear] > growthMultiplier * actualTrajectory.LeafAreaIndexByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.LiveStemVolumeByYear[simulationYear] > growthMultiplier * actualTrajectory.LiveStemVolumeByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.StemCarbonByYear[simulationYear] > growthMultiplier * actualTrajectory.StemCarbonByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.StemNitrogenByYear[simulationYear] > growthMultiplier * actualTrajectory.StemNitrogenByYear[previousYear]);
                    float treeCount = actualTrajectory.TreesPerHectareByYear[simulationYear];
                    Assert.IsTrue((treeCount >= 0.0F) && (treeCount <= maximumTreeCount));
                    Assert.IsTrue(actualTrajectory.TreeNppAbovegroundByYear[simulationYear] > treeNppMultiplier * actualTrajectory.TreeNppAbovegroundByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.TreeNppByYear[simulationYear] > treeNppMultiplier * actualTrajectory.TreeNppByYear[previousYear]);

                    Assert.IsTrue(actualTrajectory.MeanSaplingAgeByYear[simulationYear] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(actualTrajectory.RegenerationCarbonByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.RegenerationNitrogenByYear[simulationYear] == 0.0F);
                    Assert.IsTrue(actualTrajectory.SaplingNppByYear[simulationYear] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(actualTrajectory.SaplingsPerHectareByYear[simulationYear] == 0);

                    Assert.IsTrue(actualTrajectory.BranchCarbonByYear[simulationYear] > growthMultiplier * actualTrajectory.BranchCarbonByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.BranchNitrogenByYear[simulationYear] > growthMultiplier * actualTrajectory.BranchNitrogenByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.CoarseRootCarbonByYear[simulationYear] > growthMultiplier * actualTrajectory.CoarseRootCarbonByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.CoarseRootNitrogenByYear[simulationYear] > growthMultiplier * actualTrajectory.CoarseRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.CohortsPerHectareByYear[simulationYear] == 0); // no saplings at initialization and regeneration not enabled
                    Assert.IsTrue(actualTrajectory.FineRootCarbonByYear[simulationYear] > growthMultiplier * actualTrajectory.FineRootCarbonByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.FineRootNitrogenByYear[simulationYear] > growthMultiplier * actualTrajectory.FineRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.FoliageCarbonByYear[simulationYear] > growthMultiplier * actualTrajectory.FoliageCarbonByYear[previousYear]);
                    Assert.IsTrue(actualTrajectory.FoliageNitrogenByYear[simulationYear] > growthMultiplier * actualTrajectory.FoliageNitrogenByYear[previousYear]);

                    // sanity checks on ranges
                    Assert.IsTrue((actualTrajectory.LeafAreaIndexByYear[simulationYear] > 0.3F) && (actualTrajectory.LeafAreaIndexByYear[simulationYear] < 20.0F));
                }
            }
        }
    }
}
