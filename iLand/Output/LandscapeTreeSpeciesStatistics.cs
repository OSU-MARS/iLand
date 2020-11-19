using iLand.Tree;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Output
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

        public void AddResourceUnit(ResourceUnit ru, ResourceUnitTreeStatistics ruSpeciesStats)
        {
            if (ruSpeciesStats.IsPerHectare == false)
            {
                throw new ArgumentOutOfRangeException(nameof(ruSpeciesStats), "Attempt to aggregate species statistics which are not per hectare.");
            }
            this.totalAreaInLandscape += ru.AreaInLandscape;
            this.totalAreaWithTrees += ru.AreaWithTrees;

            this.AverageDbh += ruSpeciesStats.AverageDbh * ru.AreaInLandscape;
            this.AverageHeight += ruSpeciesStats.AverageHeight * ru.AreaInLandscape;
            this.BasalArea += ruSpeciesStats.BasalArea * ru.AreaInLandscape;
            this.CohortCount += ruSpeciesStats.CohortCount * ru.AreaInLandscape;
            this.LeafAreaIndex += ruSpeciesStats.LeafArea;
            this.LiveAndSnagStemVolume += ruSpeciesStats.LiveAndSnagStemVolume * ru.AreaInLandscape;
            this.LiveStemVolume += ruSpeciesStats.StemVolume * ru.AreaInLandscape;
            this.TreeCount += ruSpeciesStats.TreeCount * ru.AreaInLandscape;
            this.TotalCarbon += ruSpeciesStats.GetTotalCarbon() * ru.AreaInLandscape;
            this.TreeNpp += ruSpeciesStats.TreeNpp * ru.AreaInLandscape;
            this.TreeNppAboveground += ruSpeciesStats.TreeNppAboveground * ru.AreaInLandscape;
        }

        public void ConvertSumsToAreaWeightedAverages()
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
