namespace iLand.Tree
{
    public class StandOrResourceUnitTreeStatistics
    {
        // accumulators for calculating averages
        protected float TotalDbhInCm { get; set; }
        protected float TotalHeightInM { get; set; }
        protected float TotalSaplingCohortAgeInYears { get; set; }

        public float TotalLeafAreaInM2 { get; protected set; } // m² on resource unit

        // trees
        public float AverageDbhInCm { get; protected set; } // average DBH, cm
        public float AverageHeightInM { get; protected set; } // average tree height, m
        public float BasalAreaInM2PerHa { get; protected set; } // sum of basal area of all trees, accumulated as m² on resource unit and converted to m²/ha
        public float LeafAreaIndex { get; protected set; } // [m²/m²]/ha of height cells containing trees
        public float StemVolumeInM3PerHa { get; protected set; } // sum of trees' stem volume, m³ or m³/ha, may be live, dead, or removed depending on what this statistics object represents
        public float TreesPerHa { get; protected set; } // trees on resource unit or TPH
        public float TreeNppPerHa { get; protected set; } // sum of above + belowground NPP of trees >4m tall, (kg biomass increment) or (kg biomass increment)/ha
        public float TreeNppPerHaAboveground { get; protected set; } // (kg biomass increment) or (kg biomass increment)/ha

        // saplings
        public float SaplingCohortsPerHa { get; protected set; } // number of cohorts of saplings or cohorts/ha
        public float SaplingMeanAgeInYears { get; protected set; } // average age of sapling (not currently weighted by represented sapling numbers)
        public float SaplingNppPerHa { get; protected set; } // carbon gain of saplings (kg biomass increment) or (kg biomass increment)/ha
        public float SaplingsPerHa { get; protected set; } // number of individuals in regeneration layer (represented by CohortCount cohorts), saplings or saplings/ha

        // carbon/nitrogen cycle
        public float BranchCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float BranchNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float CoarseRootCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float CoarseRootNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FineRootCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FineRootNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FoliageCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FoliageNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float RegenerationCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float RegenerationNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float StemCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float StemNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha

        public StandOrResourceUnitTreeStatistics()
        {
            this.Zero();
        }

        protected void AddAreaWeighted(StandOrResourceUnitTreeStatistics completedTreeStatistics, float areaInHectares)
        {
            // accumulators: area weight is included automatically since these are totals over the resource unit
            this.TotalDbhInCm += completedTreeStatistics.TotalDbhInCm;
            this.TotalHeightInM += completedTreeStatistics.TotalHeightInM;
            this.TotalLeafAreaInM2 += completedTreeStatistics.TotalLeafAreaInM2;
            this.TotalSaplingCohortAgeInYears += completedTreeStatistics.TotalSaplingCohortAgeInYears;

            // trees
            this.BasalAreaInM2PerHa += areaInHectares * completedTreeStatistics.BasalAreaInM2PerHa;
            this.LeafAreaIndex += areaInHectares * completedTreeStatistics.LeafAreaIndex; // can add directly due to same resource unit constraint
            this.StemVolumeInM3PerHa += areaInHectares * completedTreeStatistics.StemVolumeInM3PerHa;
            this.TreesPerHa += areaInHectares * completedTreeStatistics.TreesPerHa;
            this.TreeNppPerHa += areaInHectares * completedTreeStatistics.TreeNppPerHa;
            this.TreeNppPerHaAboveground += areaInHectares * completedTreeStatistics.TreeNppPerHaAboveground;

            // regeneration
            this.SaplingCohortsPerHa += areaInHectares * completedTreeStatistics.SaplingCohortsPerHa;
            this.SaplingsPerHa += areaInHectares * completedTreeStatistics.SaplingsPerHa;
            this.SaplingNppPerHa += areaInHectares * completedTreeStatistics.SaplingNppPerHa;

            // carbon/nitrogen pools
            this.BranchCarbonInKgPerHa += areaInHectares * completedTreeStatistics.BranchCarbonInKgPerHa;
            this.BranchNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.BranchNitrogenInKgPerHa;
            this.CoarseRootCarbonInKgPerHa += areaInHectares * completedTreeStatistics.CoarseRootCarbonInKgPerHa;
            this.FineRootCarbonInKgPerHa += areaInHectares * completedTreeStatistics.FineRootCarbonInKgPerHa;
            this.FineRootNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.FineRootNitrogenInKgPerHa;
            this.CoarseRootNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.CoarseRootNitrogenInKgPerHa;
            this.FoliageCarbonInKgPerHa += areaInHectares * completedTreeStatistics.FoliageCarbonInKgPerHa;
            this.FoliageNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.FoliageNitrogenInKgPerHa;
            this.RegenerationCarbonInKgPerHa += areaInHectares * completedTreeStatistics.RegenerationCarbonInKgPerHa;
            this.RegenerationNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.RegenerationNitrogenInKgPerHa;
            this.StemCarbonInKgPerHa += areaInHectares * completedTreeStatistics.StemCarbonInKgPerHa;
            this.StemNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.StemNitrogenInKgPerHa;
        }

        protected void AddUnweighted(StandOrResourceUnitTreeStatistics completedTreeStatistics)
        {
            // accumulators
            this.TotalDbhInCm += completedTreeStatistics.TotalDbhInCm;
            this.TotalHeightInM += completedTreeStatistics.TotalHeightInM;
            this.TotalLeafAreaInM2 += completedTreeStatistics.TotalLeafAreaInM2;
            this.TotalSaplingCohortAgeInYears += completedTreeStatistics.TotalSaplingCohortAgeInYears;

            // trees
            this.BasalAreaInM2PerHa += completedTreeStatistics.BasalAreaInM2PerHa;
            this.LeafAreaIndex += completedTreeStatistics.LeafAreaIndex; // can add directly due to same resource unit constraint
            this.StemVolumeInM3PerHa += completedTreeStatistics.StemVolumeInM3PerHa;
            this.TreesPerHa += completedTreeStatistics.TreesPerHa;
            this.TreeNppPerHa += completedTreeStatistics.TreeNppPerHa;
            this.TreeNppPerHaAboveground += completedTreeStatistics.TreeNppPerHaAboveground;

            // regeneration
            this.SaplingCohortsPerHa += completedTreeStatistics.SaplingCohortsPerHa;
            this.SaplingsPerHa += completedTreeStatistics.SaplingsPerHa;
            this.SaplingNppPerHa += completedTreeStatistics.SaplingNppPerHa;

            // carbon/nitrogen pools
            this.BranchCarbonInKgPerHa += completedTreeStatistics.BranchCarbonInKgPerHa;
            this.BranchNitrogenInKgPerHa += completedTreeStatistics.BranchNitrogenInKgPerHa;
            this.CoarseRootCarbonInKgPerHa += completedTreeStatistics.CoarseRootCarbonInKgPerHa;
            this.FineRootCarbonInKgPerHa += completedTreeStatistics.FineRootCarbonInKgPerHa;
            this.FineRootNitrogenInKgPerHa += completedTreeStatistics.FineRootNitrogenInKgPerHa;
            this.CoarseRootNitrogenInKgPerHa += completedTreeStatistics.CoarseRootNitrogenInKgPerHa;
            this.FoliageCarbonInKgPerHa += completedTreeStatistics.FoliageCarbonInKgPerHa;
            this.FoliageNitrogenInKgPerHa += completedTreeStatistics.FoliageNitrogenInKgPerHa;
            this.RegenerationCarbonInKgPerHa += completedTreeStatistics.RegenerationCarbonInKgPerHa;
            this.RegenerationNitrogenInKgPerHa += completedTreeStatistics.RegenerationNitrogenInKgPerHa;
            this.StemCarbonInKgPerHa += completedTreeStatistics.StemCarbonInKgPerHa;
            this.StemNitrogenInKgPerHa += completedTreeStatistics.StemNitrogenInKgPerHa;
        }

        // reset all values for accumulation of a year's statistics
        public virtual void Zero()
        {
            this.ZeroStandingTreeStatisticsForRecalculation();

            this.TotalSaplingCohortAgeInYears = 0.0F;

            this.RegenerationCarbonInKgPerHa = 0.0F;
            this.RegenerationNitrogenInKgPerHa = 0.0F;
            this.SaplingCohortsPerHa = 0;
            this.SaplingMeanAgeInYears = 0.0F;
            this.SaplingNppPerHa = 0.0F;
            this.SaplingsPerHa = 0;

            this.TreeNppPerHa = 0.0F;
            this.TreeNppPerHaAboveground = 0.0F;
        }

        // reset only those values that are directly accumulated from *trees* - seedling and saplings are zeroed in Zero()
        // NPP is 
        public void ZeroStandingTreeStatisticsForRecalculation()
        {
            this.TotalDbhInCm = 0.0F;
            this.TotalHeightInM = 0.0F;

            this.AverageDbhInCm = 0.0F;
            this.AverageHeightInM = 0.0f;
            this.BasalAreaInM2PerHa = 0.0F;

            this.BranchCarbonInKgPerHa = 0.0F;
            this.BranchNitrogenInKgPerHa = 0.0F;
            this.CoarseRootCarbonInKgPerHa = 0.0F;
            this.CoarseRootNitrogenInKgPerHa = 0.0F;
            this.FineRootCarbonInKgPerHa = 0.0F;
            this.FineRootNitrogenInKgPerHa = 0.0F;
            this.FoliageCarbonInKgPerHa = 0.0F;
            this.FoliageNitrogenInKgPerHa = 0.0F;
            this.TotalLeafAreaInM2 = 0.0F;
            this.LeafAreaIndex = 0.0F;
            this.StemCarbonInKgPerHa = 0.0F;
            this.StemNitrogenInKgPerHa = 0.0F;
            this.StemVolumeInM3PerHa = 0.0F;
            this.TreesPerHa = 0.0F;
        }
    }
}
