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
        public float LiveAndSnagStemVolume { get; private set; }
        public float LiveStemVolume { get; private set; }
        public float TotalCarbon { get; private set; }
        public float TreeCount { get; private set; }
        public float TreeNpp { get; private set; }
        public float TreeNppAboveground { get; private set; }

        public void AddResourceUnit(ResourceUnit resourceUnit, ResourceUnitTreeStatistics ruLiveTreeStatisticsForSpecies)
        {
            if (ruLiveTreeStatisticsForSpecies.IsPerHectare == false)
            {
                throw new ArgumentOutOfRangeException(nameof(ruLiveTreeStatisticsForSpecies), "Attempt to aggregate species statistics which are not per hectare.");
            }

            float ruAreaInLandscape = resourceUnit.AreaInLandscapeInM2;
            this.totalAreaInLandscape += ruAreaInLandscape;
            this.totalAreaWithTrees += resourceUnit.AreaWithTreesInM2;

            this.AverageDbh += ruLiveTreeStatisticsForSpecies.AverageDbh * ruAreaInLandscape;
            this.AverageHeight += ruLiveTreeStatisticsForSpecies.AverageHeight * ruAreaInLandscape;
            this.BasalArea += ruLiveTreeStatisticsForSpecies.BasalArea * ruAreaInLandscape;
            this.CohortCount += ruLiveTreeStatisticsForSpecies.CohortCount * ruAreaInLandscape;
            this.LeafAreaIndex += ruLiveTreeStatisticsForSpecies.LeafArea;
            this.LiveAndSnagStemVolume += ruLiveTreeStatisticsForSpecies.LiveAndSnagStemVolume * ruAreaInLandscape;
            this.LiveStemVolume += ruLiveTreeStatisticsForSpecies.StemVolume * ruAreaInLandscape;
            this.TreeCount += ruLiveTreeStatisticsForSpecies.TreeCount * ruAreaInLandscape;
            this.TotalCarbon += ruLiveTreeStatisticsForSpecies.GetTotalCarbon() * ruAreaInLandscape;
            this.TreeNpp += ruLiveTreeStatisticsForSpecies.TreeNpp * ruAreaInLandscape;
            this.TreeNppAboveground += ruLiveTreeStatisticsForSpecies.TreeNppAboveground * ruAreaInLandscape;
        }

        public void ConvertIncrementalSumsToAreaWeightedAverages()
        {
            Debug.Assert(this.totalAreaInLandscape > 0.0F);
            this.AverageDbh /= this.totalAreaInLandscape;
            this.AverageHeight /= this.totalAreaInLandscape;
            this.BasalArea /= this.totalAreaInLandscape;
            this.CohortCount /= this.totalAreaInLandscape;
            this.LeafAreaIndex /= this.totalAreaWithTrees;
            this.LiveAndSnagStemVolume /= this.totalAreaInLandscape;
            this.LiveStemVolume /= this.totalAreaInLandscape;
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
            this.LiveAndSnagStemVolume = 0.0F;
            this.LiveStemVolume = 0.0F;
            this.TreeCount = 0.0F;
            this.TotalCarbon = 0.0F;
            this.TreeNpp = 0.0F;
            this.TreeNppAboveground = 0.0F;
        }
    }
}
