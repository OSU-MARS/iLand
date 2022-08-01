using iLand.Tree;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Output.Sql
{
    internal class LandscapeTreeSpeciesStatistics
    {
        private float totalAreaInLandscape;
        private float totalAreaWithTrees;

        public float AverageDbh { get; private set; }
        public float AverageHeight { get; private set; }
        public float BasalArea { get; private set; }
        public float CohortCount { get; private set; }
        public float LeafAreaIndex { get; private set; }
        public float LiveStandingAndRemovedStemVolume { get; private set; }
        public float LiveStandingStemVolume { get; private set; }
        public float TotalCarbon { get; private set; }
        public float TreeCount { get; private set; }
        public float TreeNpp { get; private set; }
        public float TreeNppAboveground { get; private set; }

        public void AddResourceUnit(ResourceUnit resourceUnit, ResourceUnitTreeSpeciesStatistics ruLiveTreeStatisticsForSpecies, float totalStemVolumeInM3PerHa)
        {
            float ruAreaInLandscape = resourceUnit.AreaInLandscapeInM2;
            if (ruAreaInLandscape <= 0.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(resourceUnit));
            }

            this.totalAreaInLandscape += ruAreaInLandscape;
            this.totalAreaWithTrees += resourceUnit.AreaWithTreesInM2;

            this.AverageDbh += ruLiveTreeStatisticsForSpecies.AverageDbhInCm * ruAreaInLandscape;
            this.AverageHeight += ruLiveTreeStatisticsForSpecies.AverageHeightInM * ruAreaInLandscape;
            this.BasalArea += ruLiveTreeStatisticsForSpecies.BasalAreaInM2PerHa * ruAreaInLandscape;
            this.CohortCount += ruLiveTreeStatisticsForSpecies.CohortsPerHa * ruAreaInLandscape;
            this.LeafAreaIndex += ruLiveTreeStatisticsForSpecies.TotalLeafAreaInM2;
            this.LiveStandingAndRemovedStemVolume += totalStemVolumeInM3PerHa * ruAreaInLandscape;
            this.LiveStandingStemVolume += ruLiveTreeStatisticsForSpecies.StemVolumeInM3PerHa * ruAreaInLandscape;
            this.TreeCount += ruLiveTreeStatisticsForSpecies.TreesPerHa * ruAreaInLandscape;
            this.TotalCarbon += ruLiveTreeStatisticsForSpecies.GetTotalCarbon() * ruAreaInLandscape;
            this.TreeNpp += ruLiveTreeStatisticsForSpecies.TreeNppPerHa * ruAreaInLandscape;
            this.TreeNppAboveground += ruLiveTreeStatisticsForSpecies.TreeNppPerHaAboveground * ruAreaInLandscape;
        }

        public void ConvertIncrementalSumsToAreaWeightedAverages()
        {
            Debug.Assert(this.totalAreaInLandscape > 0.0F);
            this.AverageDbh /= this.totalAreaInLandscape;
            this.AverageHeight /= this.totalAreaInLandscape;
            this.BasalArea /= this.totalAreaInLandscape;
            this.CohortCount /= this.totalAreaInLandscape;
            this.LeafAreaIndex /= this.totalAreaWithTrees;
            this.LiveStandingAndRemovedStemVolume /= this.totalAreaInLandscape;
            this.LiveStandingStemVolume /= this.totalAreaInLandscape;
            this.TreeCount /= this.totalAreaInLandscape;
            this.TotalCarbon /= this.totalAreaInLandscape;
            this.TreeNpp /= this.totalAreaInLandscape;
            this.TreeNppAboveground /= this.totalAreaInLandscape;
        }

        public void Zero()
        {
            this.totalAreaInLandscape = 0.0F;
            this.totalAreaWithTrees = 0.0F;

            this.AverageDbh = 0.0F;
            this.AverageHeight = 0.0F;
            this.BasalArea = 0.0F;
            this.CohortCount = 0.0F;
            this.LeafAreaIndex = 0.0F;
            this.LiveStandingAndRemovedStemVolume = 0.0F;
            this.LiveStandingStemVolume = 0.0F;
            this.TreeCount = 0.0F;
            this.TotalCarbon = 0.0F;
            this.TreeNpp = 0.0F;
            this.TreeNppAboveground = 0.0F;
        }
    }
}
