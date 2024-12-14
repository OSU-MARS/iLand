namespace iLand.Tree
{
    public class LiveTreeAndSaplingStatistics : LiveTreeStatistics
    {
        private float totalSaplingCohortAgeInYears;

        public float RegenerationCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float RegenerationNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha

        public float SaplingBasalArea { get; private set; } // basal area (m²) of saplings
        public float SaplingCohortsPerHa { get; protected set; } // number of cohorts of saplings or cohorts/ha
        public float SaplingLeafAreaIndex { get; protected set; } // leaf area index (m²/m²)
        public float SaplingMeanAgeInYears { get; protected set; } // average age of sapling (not currently weighted by represented sapling numbers)
        public float SaplingNppPerHa { get; protected set; } // carbon gain of saplings (kg biomass increment) or (kg biomass increment)/ha
        public float SaplingsPerHa { get; protected set; } // number of individuals in regeneration layer (represented by CohortCount cohorts), saplings or saplings/ha

        public void Add(SaplingStatistics saplingStatistics)
        {
            this.totalSaplingCohortAgeInYears += saplingStatistics.AverageAgeInYears * saplingStatistics.LivingCohorts;

            this.RegenerationCarbonInKgPerHa += saplingStatistics.CarbonNitrogenLiving.C;
            this.RegenerationNitrogenInKgPerHa += saplingStatistics.CarbonNitrogenLiving.N;

            this.SaplingBasalArea += saplingStatistics.BasalArea;
            this.SaplingCohortsPerHa += saplingStatistics.LivingCohorts;
            this.SaplingLeafAreaIndex += saplingStatistics.LeafAreaIndex;
            this.SaplingNppPerHa += saplingStatistics.CarbonNitrogenGain.C / Constant.DryBiomassCarbonFraction;
            this.SaplingsPerHa += saplingStatistics.LivingSaplings; // saplings with height >1.3m
        }

        public void AddAreaWeighted(float areaInHectares, LiveTreeAndSaplingStatistics completedTreeStatistics)
        {
            // accumulators: area weight is included automatically since these are totals over the resource unit
            this.totalSaplingCohortAgeInYears += completedTreeStatistics.totalSaplingCohortAgeInYears;

            this.TotalDbhInCm += completedTreeStatistics.TotalDbhInCm;
            this.TotalHeightInM += completedTreeStatistics.TotalHeightInM;
            this.TotalLeafAreaInM2 += completedTreeStatistics.TotalLeafAreaInM2;

            // trees
            this.BasalAreaInM2PerHa += areaInHectares * completedTreeStatistics.BasalAreaInM2PerHa;
            this.LeafAreaIndex += areaInHectares * completedTreeStatistics.LeafAreaIndex; // can add directly due to same resource unit constraint
            this.StemVolumeInM3PerHa += areaInHectares * completedTreeStatistics.StemVolumeInM3PerHa;
            this.TreesPerHa += areaInHectares * completedTreeStatistics.TreesPerHa;
            this.TreeNppPerHa += areaInHectares * completedTreeStatistics.TreeNppPerHa;
            this.TreeNppPerHaAboveground += areaInHectares * completedTreeStatistics.TreeNppPerHaAboveground;

            // regeneration
            this.SaplingBasalArea += areaInHectares * completedTreeStatistics.SaplingBasalArea;
            this.SaplingCohortsPerHa += areaInHectares * completedTreeStatistics.SaplingCohortsPerHa;
            this.SaplingLeafAreaIndex += areaInHectares * completedTreeStatistics.SaplingLeafAreaIndex;
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
            this.StemAndReserveCarbonInKgPerHa += areaInHectares * completedTreeStatistics.StemAndReserveCarbonInKgPerHa;
            this.StemAndReserveNitrogenInKgPerHa += areaInHectares * completedTreeStatistics.StemAndReserveNitrogenInKgPerHa;
        }

        public void AddNppOfTreeBeforeDeath(TreeGrowthData tgd) // StandStatistics::addNPP()
        {
            // add NPP of trees that died due to mortality
            this.TreeNppPerHa += tgd.NppTotal;
            this.TreeNppPerHaAboveground += tgd.NppAboveground;
        }

        // this member function is only expected to be called when accumulating statistics for resource unit tree species
        // into values for the whole resource unit
        public void AddUnweighted(LiveTreeAndSaplingStatistics completedTreeStatistics)
        {
            // since addition is within the same resource unit and completed species statistics are being added all area-based
            // statistics have the same expansion factors and unweighted addition can be used

            // accumulators
            this.totalSaplingCohortAgeInYears += completedTreeStatistics.totalSaplingCohortAgeInYears;

            this.TotalDbhInCm += completedTreeStatistics.TotalDbhInCm;
            this.TotalHeightInM += completedTreeStatistics.TotalHeightInM;
            this.TotalLeafAreaInM2 += completedTreeStatistics.TotalLeafAreaInM2;

            // trees
            this.BasalAreaInM2PerHa += completedTreeStatistics.BasalAreaInM2PerHa;
            this.LeafAreaIndex += completedTreeStatistics.LeafAreaIndex; // can add directly due to same resource unit constraint
            this.StemVolumeInM3PerHa += completedTreeStatistics.StemVolumeInM3PerHa;
            this.TreesPerHa += completedTreeStatistics.TreesPerHa;
            this.TreeNppPerHa += completedTreeStatistics.TreeNppPerHa;
            this.TreeNppPerHaAboveground += completedTreeStatistics.TreeNppPerHaAboveground;

            // regeneration
            this.SaplingBasalArea += completedTreeStatistics.SaplingBasalArea;
            this.SaplingCohortsPerHa += completedTreeStatistics.SaplingCohortsPerHa;
            this.SaplingLeafAreaIndex += completedTreeStatistics.SaplingLeafAreaIndex;
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
            this.StemAndReserveCarbonInKgPerHa += completedTreeStatistics.StemAndReserveCarbonInKgPerHa;
            this.StemAndReserveNitrogenInKgPerHa += completedTreeStatistics.StemAndReserveNitrogenInKgPerHa;
        }

        // total carbon stock: sum of carbon of all living trees + regeneration layer
        public float GetTotalCarbon()
        {
            return this.StemAndReserveCarbonInKgPerHa + this.BranchCarbonInKgPerHa + this.FoliageCarbonInKgPerHa + this.FineRootCarbonInKgPerHa + this.CoarseRootCarbonInKgPerHa + this.RegenerationCarbonInKgPerHa;
        }

        public override void OnAdditionsComplete(float resourceUnitAreaInLandscapeInM2)
        {
            base.OnAdditionsComplete(resourceUnitAreaInLandscapeInM2);

            if (this.SaplingCohortsPerHa != 0.0F)
            {
                this.SaplingMeanAgeInYears = this.totalSaplingCohortAgeInYears / this.SaplingCohortsPerHa; // else leave mean sapling age as zero
            }

            if (resourceUnitAreaInLandscapeInM2 != Constant.SquareMetersPerHectare)
            {
                float ruExpansionFactor = Constant.SquareMetersPerHectare / resourceUnitAreaInLandscapeInM2;
                this.RegenerationCarbonInKgPerHa *= ruExpansionFactor;
                this.RegenerationNitrogenInKgPerHa *= ruExpansionFactor;

                this.SaplingCohortsPerHa *= ruExpansionFactor;
                this.SaplingsPerHa *= ruExpansionFactor;
                this.SaplingNppPerHa *= ruExpansionFactor;
            }
        }

        public override void Zero()
        {
            base.Zero();
            
            this.totalSaplingCohortAgeInYears = 0.0F;

            this.RegenerationCarbonInKgPerHa = 0.0F;
            this.RegenerationNitrogenInKgPerHa = 0.0F;
            this.SaplingCohortsPerHa = 0;
            this.SaplingMeanAgeInYears = 0.0F;
            this.SaplingNppPerHa = 0.0F;
            this.SaplingsPerHa = 0;
        }
    }
}