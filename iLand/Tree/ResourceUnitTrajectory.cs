using iLand.World;
using System.Collections.Generic;

namespace iLand.Tree
{
    public class ResourceUnitTrajectory : ResourceUnitTreeStatistics
    {
        public List<float> AverageDbhByYear { get; private init; } // average dbh (cm)
        public List<float> AverageHeightByYear { get; private init; } // average tree height (m)
        public List<float> BasalAreaByYear { get; private init; } // sum of basal area of all trees (m2/ha)
        public List<int> CohortCountByYear { get; private init; } // number of cohorts of saplings / ha
        public List<float> LiveAndSnagStemVolumeByYear { get; private init; } // total increment (gesamtwuchsleistung, m3/ha)
        public List<float> LeafAreaIndexByYear { get; private init; } // [m2/m2]/ha stocked area.
        public List<float> TreeCountByYear { get; private init; }
        public List<float> TreeNppByYear { get; private init; } // sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public List<float> TreeNppAbovegroundByYear { get; private init; } // above ground NPP (kg Biomass increment)/ha

        public List<float> MeanSaplingAgeByYear { get; private init; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public List<float> SaplingNppByYear { get; private init; } // carbon gain of saplings (kg Biomass increment)/ha
        // number of sapling (Reinekes Law)
        public List<int> SaplingCountByYear { get; private init; } // number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha
        public List<float> LiveStemVolumeByYear { get; private init; } // sum of tree volume (m3/ha)

        // carbon/nitrogen cycle
        public List<float> BranchCarbonByYear { get; private init; }
        public List<float> BranchNitrogenByYear { get; private init; }
        public List<float> CoarseRootCarbonByYear { get; private init; }
        public List<float> CoarseRootNitrogenByYear { get; private init; }
        public List<float> FineRootCarbonByYear { get; private init; }
        public List<float> FineRootNitrogenByYear { get; private init; }
        public List<float> FoliageCarbonByYear { get; private init; }
        public List<float> FoliageNitrogenByYear { get; private init; }
        public List<float> RegenerationCarbonByYear { get; private init; }
        public List<float> RegenerationNitrogenByYear { get; private init; }
        public List<float> StemCarbonByYear { get; private init; }
        public List<float> StemNitrogenByYear { get; private init; }

        public ResourceUnitTrajectory(ResourceUnit ru)
            : base(ru)
        {
            int defaultCapacityInYears = 20;
            this.CohortCountByYear = new(defaultCapacityInYears);
            this.SaplingCountByYear = new(defaultCapacityInYears);
            this.AverageDbhByYear = new(defaultCapacityInYears);
            this.AverageHeightByYear = new(defaultCapacityInYears);
            this.BasalAreaByYear = new(defaultCapacityInYears);
            this.TreeCountByYear = new(defaultCapacityInYears);
            this.LiveAndSnagStemVolumeByYear = new(defaultCapacityInYears);
            this.LeafAreaIndexByYear = new(defaultCapacityInYears);
            this.MeanSaplingAgeByYear = new(defaultCapacityInYears);
            this.TreeNppByYear = new(defaultCapacityInYears);
            this.TreeNppAbovegroundByYear = new(defaultCapacityInYears);
            this.SaplingNppByYear = new(defaultCapacityInYears);
            this.LiveStemVolumeByYear = new(defaultCapacityInYears);
            this.BranchCarbonByYear = new(defaultCapacityInYears);
            this.BranchNitrogenByYear = new(defaultCapacityInYears);
            this.CoarseRootCarbonByYear = new(defaultCapacityInYears);
            this.CoarseRootNitrogenByYear = new(defaultCapacityInYears);
            this.FineRootCarbonByYear = new(defaultCapacityInYears);
            this.FineRootNitrogenByYear = new(defaultCapacityInYears);
            this.FoliageCarbonByYear = new(defaultCapacityInYears);
            this.FoliageNitrogenByYear = new(defaultCapacityInYears);
            this.RegenerationCarbonByYear = new(defaultCapacityInYears);
            this.RegenerationNitrogenByYear = new(defaultCapacityInYears);
            this.StemCarbonByYear = new(defaultCapacityInYears);
            this.StemNitrogenByYear = new(defaultCapacityInYears);
        }

        public override void OnEndYear()
        {
            base.OnEndYear();

            this.AverageDbhByYear.Add(this.AverageDbh);
            this.AverageHeightByYear.Add(this.AverageHeight);
            this.BasalAreaByYear.Add(this.BasalArea);
            this.CohortCountByYear.Add(this.CohortCount);
            this.LeafAreaIndexByYear.Add(this.LeafAreaIndex);
            this.LiveStemVolumeByYear.Add(this.StemVolume);
            this.LiveAndSnagStemVolumeByYear.Add(this.LiveAndSnagStemVolume);
            this.MeanSaplingAgeByYear.Add(this.MeanSaplingAge);
            this.TreeNppByYear.Add(this.TreeNpp);
            this.TreeNppAbovegroundByYear.Add(this.TreeNppAboveground);
            this.TreeCountByYear.Add(this.TreeCount);

            this.RegenerationCarbonByYear.Add(this.RegenerationCarbon);
            this.RegenerationNitrogenByYear.Add(this.RegenerationNitrogen);
            this.SaplingCountByYear.Add(this.SaplingCount);
            this.SaplingNppByYear.Add(this.SaplingNpp);

            this.BranchCarbonByYear.Add(this.BranchCarbon);
            this.BranchNitrogenByYear.Add(this.BranchNitrogen);
            this.CoarseRootCarbonByYear.Add(this.CoarseRootCarbon);
            this.CoarseRootNitrogenByYear.Add(this.CoarseRootNitrogen);
            this.FineRootCarbonByYear.Add(this.FineRootCarbon);
            this.FineRootNitrogenByYear.Add(this.FineRootNitrogen);
            this.FoliageCarbonByYear.Add(this.FoliageCarbon);
            this.FoliageNitrogenByYear.Add(this.FoliageNitrogen);
            this.StemCarbonByYear.Add(this.StemCarbon);
            this.StemNitrogenByYear.Add(this.StemNitrogen);
        }
    }
}
