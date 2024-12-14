using System.Diagnostics;

namespace iLand.Tree
{
    public class LiveTreeStatistics
    {
        // accumulators for calculating averages
        protected float TotalDbhInCm { get; set; }
        protected float TotalHeightInM { get; set; }

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

        // carbon/nitrogen cycle
        public float BranchCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float BranchNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float CoarseRootCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float CoarseRootNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FineRootCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FineRootNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FoliageCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float FoliageNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float StemAndReserveCarbonInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha
        public float StemAndReserveNitrogenInKgPerHa { get; protected set; } // accumulated as kg and converted to kg/ha

        public LiveTreeStatistics()
        {
            this.Zero();
        }

        public void Add(TreeListSpatial trees, int treeIndex)
        {
            // trees
            this.TotalDbhInCm += trees.DbhInCm[treeIndex];
            this.TotalHeightInM += trees.HeightInM[treeIndex];
            this.TotalLeafAreaInM2 += trees.LeafAreaInM2[treeIndex];

            this.BasalAreaInM2PerHa += trees.GetBasalArea(treeIndex);
            this.StemVolumeInM3PerHa += trees.GetStemVolume(treeIndex);
            ++this.TreesPerHa;

            // carbon and nitrogen pools
            float branchBiomass = trees.GetBranchBiomass(treeIndex);
            this.BranchCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * branchBiomass;
            this.BranchNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * branchBiomass;
            float coarseRootMass = trees.CoarseRootMassInKg[treeIndex];
            this.CoarseRootCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * coarseRootMass;
            this.CoarseRootNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * coarseRootMass;
            float fineRootMass = trees.FineRootMassInKg[treeIndex];
            this.FineRootCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * fineRootMass;
            this.FineRootNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioFineRoot * fineRootMass;
            float foliageMass = trees.FoliageMassInKg[treeIndex];
            this.FoliageCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * foliageMass;
            this.FoliageNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioFineRoot * foliageMass;
            float stemAndReserveMass = trees.StemMassInKg[treeIndex] + trees.NppReserveInKg[treeIndex];
            this.StemAndReserveCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * stemAndReserveMass;
            this.StemAndReserveNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * stemAndReserveMass;
        }

        public void Add(TreeListSpatial trees, int treeIndex, float npp, float abovegroundNpp)
        {
            this.Add(trees, treeIndex);

            this.TreeNppPerHa += npp;
            this.TreeNppPerHaAboveground += abovegroundNpp;
        }

        public virtual void OnAdditionsComplete(float resourceUnitAreaInLandscapeInM2)
        {
            Debug.Assert(resourceUnitAreaInLandscapeInM2 > 0.0F);

            // since expansion factors have not yet been applied TreesPerHa and CohortsPerHa are tree counts
            float treeCount = this.TreesPerHa;
            if (treeCount > 0.0F) // if no trees, average DBH and height are left as zero
            {
                this.AverageDbhInCm = this.TotalDbhInCm / treeCount;
                this.AverageHeightInM = this.TotalHeightInM / treeCount;
            }

            this.LeafAreaIndex = this.TotalLeafAreaInM2 / resourceUnitAreaInLandscapeInM2; // arguably could be this.ResourceUnit.AreaWithTrees, this calculation is redundant for all species statistics as LAIs have already been added

            // if resource unit is 1 ha then values are already per hectare, if not expansion factor multiplication is needed
            // For resource unit tree statistics, expansion factors are applied only at the species level and then species' per hectare
            // properties are added together to find resource unit-level statistics.
            if (resourceUnitAreaInLandscapeInM2 != Constant.Grid.ResourceUnitAreaInM2)
            {
                // expansion factor does not apply to
                // this.AverageDbhInCm
                // this.AverageHeightInM
                // this.LeafAreaIndex
                // this.Total*
                float ruExpansionFactor = Constant.SquareMetersPerHectare / resourceUnitAreaInLandscapeInM2;
                this.BasalAreaInM2PerHa *= ruExpansionFactor;

                this.BranchCarbonInKgPerHa *= ruExpansionFactor;
                this.BranchNitrogenInKgPerHa *= ruExpansionFactor;
                this.CoarseRootCarbonInKgPerHa *= ruExpansionFactor;
                this.CoarseRootNitrogenInKgPerHa *= ruExpansionFactor;
                this.FineRootCarbonInKgPerHa *= ruExpansionFactor;
                this.FineRootNitrogenInKgPerHa *= ruExpansionFactor;
                this.FoliageCarbonInKgPerHa *= ruExpansionFactor;
                this.FoliageNitrogenInKgPerHa *= ruExpansionFactor;
                this.StemAndReserveCarbonInKgPerHa *= ruExpansionFactor;
                this.StemAndReserveNitrogenInKgPerHa *= ruExpansionFactor;

                this.StemVolumeInM3PerHa *= ruExpansionFactor;
                this.TreesPerHa *= ruExpansionFactor;
                this.TreeNppPerHa *= ruExpansionFactor;
                this.TreeNppPerHaAboveground *= ruExpansionFactor;
            }
        }

        // reset all values for accumulation of a year's statistics
        public virtual void Zero()
        {
            this.ZeroStandingTreeStatisticsForRecalculation();

            this.TreeNppPerHa = 0.0F;
            this.TreeNppPerHaAboveground = 0.0F;
        }

        // reset only those values that are directly accumulated from *trees* - seedlings and saplings are zeroed in overrides of Zero()
        // TODO: why is NPP cleared only in Zero()?
        public void ZeroStandingTreeStatisticsForRecalculation()
        {
            this.TotalDbhInCm = 0.0F;
            this.TotalHeightInM = 0.0F;
            this.TotalLeafAreaInM2 = 0.0F;

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
            this.LeafAreaIndex = 0.0F;
            this.StemAndReserveCarbonInKgPerHa = 0.0F;
            this.StemAndReserveNitrogenInKgPerHa = 0.0F;
            this.StemVolumeInM3PerHa = 0.0F;
            this.TreesPerHa = 0.0F;
        }
    }
}
