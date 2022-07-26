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
                npp += treeSpecies.StatisticsLive.TreeNpp;
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

        public void Verify(ResourceUnit resourceUnit, float maximumTreeCount, List<float> expectedGppByYear, List<float> expectedNppByYear, List<float> expectedVolumeByYear, int standID)
        {
            float growthMultiplier = 1.0F - this.NonmonotonicGrowthTolerance;
            float treeNppMultiplier = 1.0F - this.TreeNppTolerance;

            ResourceUnitTrajectory resourceUnitTrajectory = (ResourceUnitTrajectory)resourceUnit.Trees.TreeStatisticsByStandID[standID];
            for (int year = 0; year < expectedVolumeByYear.Count; ++year)
            {
                float observedGpp = this.ObservedGppByYear[year];
                float expectedGpp = expectedGppByYear[year];
                float relativeGppError = MathF.Abs(1.0F - observedGpp / expectedGpp);

                float observedNpp = this.ObservedNppByYear[year];
                float expectedNpp = expectedNppByYear[year];
                float recordedNpp = resourceUnitTrajectory.TreeNppByYear[year];
                float relativeNppError = MathF.Abs(1.0F - observedNpp / expectedNpp);

                float observedStemVolume = this.ObservedStemVolumeByYear[year];
                float expectedStemVolume = expectedVolumeByYear[year];
                float recordedVolume = resourceUnitTrajectory.LiveStemVolumeByYear[year];
                float relativeVolumeError = MathF.Abs(1.0F - observedStemVolume / expectedStemVolume);

                Assert.IsTrue(relativeGppError < this.GppTolerance, "Expected GPP of {0:0.000} kg/m² in simulation year {1} but the GPP recorded was {2:0.000} kg/m², a {3:0.0%} difference.", expectedGpp, year, observedGpp, relativeGppError);
                Assert.IsTrue(relativeNppError < this.NppTolerance, "Expected NPP of {0:0.000} kg/ha in simulation year {1} but the NPP recorded was {2:0.000} kg/ha, a {3:0.0%} difference.", expectedNpp, year, observedNpp, relativeNppError);
                Assert.IsTrue(relativeVolumeError < this.StemVolumeTolerance, "Expected stem volume of {0:0.000} m³ in simulation year {1} but the stem volume recorded was {2:0.000} m³, a {3:0.0%} difference.", expectedStemVolume, year, observedStemVolume, relativeVolumeError);
                Assert.IsTrue(observedNpp == recordedNpp);
                Assert.IsTrue(MathF.Abs(observedStemVolume - recordedVolume) < 0.001F);

                // plot statistics are multiplied by expansion factor obtained from portion of resource unit occupied to obtain per hectare values
                if (year == 0)
                {
                    // sanity checks on initial state
                    Assert.IsTrue(resourceUnitTrajectory.AverageDbhByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.AverageHeightByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.BasalAreaByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.LeafAreaIndexByYear[year] >= 1.0F);
                    Assert.IsTrue(resourceUnitTrajectory.LiveStemVolumeByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.LiveAndSnagStemVolumeByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.TreeNppAbovegroundByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.TreeNppByYear[year] > 0.0F);
                    float treeCount = resourceUnitTrajectory.TreeCountByYear[year];
                    Assert.IsTrue((treeCount >= 0.0F) && (treeCount <= maximumTreeCount));
                    Assert.IsTrue(resourceUnitTrajectory.CohortCountByYear[year] == 0);
                    Assert.IsTrue(resourceUnitTrajectory.MeanSaplingAgeByYear[year] == 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.SaplingNppByYear[year] == 0.0F);

                    Assert.IsTrue(resourceUnitTrajectory.BranchCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.BranchNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.CoarseRootCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.CoarseRootNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.FineRootCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.FineRootNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.FoliageCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.FoliageNitrogenByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.RegenerationCarbonByYear[year] == 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.RegenerationNitrogenByYear[year] == 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.SaplingCountByYear[year] == 0);
                    Assert.IsTrue(resourceUnitTrajectory.StemCarbonByYear[year] > 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.StemNitrogenByYear[year] > 0.0F);
                }
                else
                {
                    // sanity checks on growth and mortality
                    int previousYear = year - 1;
                    Assert.IsTrue(resourceUnitTrajectory.AverageDbhByYear[year] > growthMultiplier * resourceUnitTrajectory.AverageDbhByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.AverageHeightByYear[year] > growthMultiplier * resourceUnitTrajectory.AverageHeightByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.BasalAreaByYear[year] > growthMultiplier * resourceUnitTrajectory.BasalAreaByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.LeafAreaIndexByYear[year] > growthMultiplier * resourceUnitTrajectory.LeafAreaIndexByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.LiveStemVolumeByYear[year] > growthMultiplier * resourceUnitTrajectory.LiveStemVolumeByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.LiveAndSnagStemVolumeByYear[year] >= growthMultiplier * resourceUnitTrajectory.LiveAndSnagStemVolumeByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.StemCarbonByYear[year] > growthMultiplier * resourceUnitTrajectory.StemCarbonByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.StemNitrogenByYear[year] > growthMultiplier * resourceUnitTrajectory.StemNitrogenByYear[previousYear]);
                    float treeCount = resourceUnitTrajectory.TreeCountByYear[year];
                    Assert.IsTrue((treeCount >= 0.0F) && (treeCount <= maximumTreeCount));
                    Assert.IsTrue(resourceUnitTrajectory.TreeNppAbovegroundByYear[year] > treeNppMultiplier * resourceUnitTrajectory.TreeNppAbovegroundByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.TreeNppByYear[year] > treeNppMultiplier * resourceUnitTrajectory.TreeNppByYear[previousYear]);

                    Assert.IsTrue(resourceUnitTrajectory.MeanSaplingAgeByYear[year] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(resourceUnitTrajectory.RegenerationCarbonByYear[year] == 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.RegenerationNitrogenByYear[year] == 0.0F);
                    Assert.IsTrue(resourceUnitTrajectory.SaplingNppByYear[year] == 0.0F); // regeneration not enabled
                    Assert.IsTrue(resourceUnitTrajectory.SaplingCountByYear[year] == 0);

                    Assert.IsTrue(resourceUnitTrajectory.BranchCarbonByYear[year] > growthMultiplier * resourceUnitTrajectory.BranchCarbonByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.BranchNitrogenByYear[year] > growthMultiplier * resourceUnitTrajectory.BranchNitrogenByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.CoarseRootCarbonByYear[year] > growthMultiplier * resourceUnitTrajectory.CoarseRootCarbonByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.CoarseRootNitrogenByYear[year] > growthMultiplier * resourceUnitTrajectory.CoarseRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.CohortCountByYear[year] == 0); // no saplings at initialization and regeneration not enabled
                    Assert.IsTrue(resourceUnitTrajectory.FineRootCarbonByYear[year] > growthMultiplier * resourceUnitTrajectory.FineRootCarbonByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.FineRootNitrogenByYear[year] > growthMultiplier * resourceUnitTrajectory.FineRootNitrogenByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.FoliageCarbonByYear[year] > growthMultiplier * resourceUnitTrajectory.FoliageCarbonByYear[previousYear]);
                    Assert.IsTrue(resourceUnitTrajectory.FoliageNitrogenByYear[year] > growthMultiplier * resourceUnitTrajectory.FoliageNitrogenByYear[previousYear]);

                    // sanity checks on ranges
                    Assert.IsTrue(resourceUnitTrajectory.LiveStemVolumeByYear[year] >= resourceUnitTrajectory.LiveAndSnagStemVolumeByYear[year]);
                    Assert.IsTrue((resourceUnitTrajectory.LeafAreaIndexByYear[year] > 0.3F) && (resourceUnitTrajectory.LeafAreaIndexByYear[year] < 20.0F));
                }
            }
        }
    }
}
