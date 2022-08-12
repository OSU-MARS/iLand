using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    // summary statistics for all trees on a resource unit
    public class ResourceUnitTreeStatistics : StandOrResourceUnitTreeStatistics
    {
        public ResourceUnit ResourceUnit { get; private init; }

        public ResourceUnitTreeStatistics(ResourceUnit resourceUnit)
        {
            this.ResourceUnit = resourceUnit;
        }

        public void Add(ResourceUnitTreeSpeciesStatistics completedTreeSpeciesStatistics)
        {
            // this member function is only expected to be called when accumulating statistics for resource unit tree species
            // into values for the whole resource unit
            if (Object.ReferenceEquals(this.ResourceUnit, completedTreeSpeciesStatistics.ResourceUnit) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(completedTreeSpeciesStatistics));
            }

            // since addition is within the same resource unit and completed species statistics are being added all area-based
            // statistics have the same expansion factors and unweighted addition can be used
            base.AddUnweighted(completedTreeSpeciesStatistics);
        }

        public void Add(Trees trees, int treeIndex)
        {
            // trees
            this.TotalDbhInCm += trees.Dbh[treeIndex];
            this.TotalHeightInM += trees.Height[treeIndex];
            this.TotalLeafAreaInM2 += trees.LeafArea[treeIndex];

            this.BasalAreaInM2PerHa += trees.GetBasalArea(treeIndex);
            this.StemVolumeInM3PerHa += trees.GetStemVolume(treeIndex);
            ++this.TreesPerHa;

            // carbon and nitrogen pools
            float branchBiomass = trees.GetBranchBiomass(treeIndex);
            this.BranchCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * branchBiomass;
            this.BranchNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * branchBiomass;
            float coarseRootMass = trees.CoarseRootMass[treeIndex];
            this.CoarseRootCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * coarseRootMass;
            this.CoarseRootNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * coarseRootMass;
            float fineRootMass = trees.FineRootMass[treeIndex];
            this.FineRootCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * fineRootMass;
            this.FineRootNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioFineRoot * fineRootMass;
            float foliageMass = trees.FoliageMass[treeIndex];
            this.FoliageCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * foliageMass;
            this.FoliageNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioFineRoot * foliageMass;
            float stemMass = trees.StemMass[treeIndex];
            this.StemCarbonInKgPerHa += Constant.DryBiomassCarbonFraction * stemMass;
            this.StemNitrogenInKgPerHa += Constant.DryBiomassCarbonFraction / trees.Species.CarbonNitrogenRatioWood * stemMass;
        }

        public void Add(Trees trees, int treeIndex, float npp, float abovegroundNpp)
        {
            this.Add(trees, treeIndex);

            this.TreeNppPerHa += npp;
            this.TreeNppPerHaAboveground += abovegroundNpp;
        }

        public void Add(SaplingStatistics sapling)
        {
            this.SaplingCohortsPerHa += sapling.LivingCohorts;
            this.SaplingsPerHa += sapling.LivingSaplings; // saplings with height >1.3m
            this.TotalSaplingCohortAgeInYears += sapling.AverageAgeInYears * sapling.LivingCohorts;

            this.RegenerationCarbonInKgPerHa += sapling.CarbonNitrogenLiving.C;
            this.RegenerationNitrogenInKgPerHa += sapling.CarbonNitrogenLiving.N;

            this.SaplingNppPerHa += sapling.CarbonNitrogenGain.C / Constant.DryBiomassCarbonFraction;
        }

        // total carbon stock: sum of carbon of all living trees + regeneration layer
        public float GetTotalCarbon()
        {
            return this.StemCarbonInKgPerHa + this.BranchCarbonInKgPerHa + this.FoliageCarbonInKgPerHa + this.FineRootCarbonInKgPerHa + this.CoarseRootCarbonInKgPerHa + this.RegenerationCarbonInKgPerHa;
        }

        public void OnAdditionsComplete()
        {
            // since expansion factors have not yet been applied TreesPerHa and CohortsPerHa are tree counts
            float treeCount = this.TreesPerHa;
            if (treeCount > 0.0F) // if no trees, average DBH and height are left as zero
            {
                this.AverageDbhInCm = this.TotalDbhInCm / treeCount;
                this.AverageHeightInM = this.TotalHeightInM / treeCount;
            }
            if (this.SaplingCohortsPerHa != 0.0F)
            {
                this.SaplingMeanAgeInYears = this.TotalSaplingCohortAgeInYears / this.SaplingCohortsPerHa; // else leave mean sapling age as zero
            }

            Debug.Assert(this.ResourceUnit.AreaInLandscapeInM2 > 0.0F);
            this.LeafAreaIndex = this.TotalLeafAreaInM2 / this.ResourceUnit.AreaInLandscapeInM2; // arguably could be this.ResourceUnit.AreaWithTrees, this calculation is redundant for all species statistics as LAIs have already been added

            // if resource unit is 1 ha then values are already per hectare, if not expansion factor multiplication is needed
            // For resource unit tree statistics, expansion factors are applied only at the species level and then species' per hectare
            // properties are added together to find resource unit-level statistics.
            float resourceUnitAreaInLandscape = this.ResourceUnit.AreaInLandscapeInM2;
            if (resourceUnitAreaInLandscape != Constant.SquareMetersPerHectare)
            {
                // expansion factor does not apply to
                // this.AverageDbhInCm
                // this.AverageHeightInM
                // this.LeafAreaIndex
                // this.Total*
                float ruExpansionFactor = Constant.SquareMetersPerHectare / resourceUnitAreaInLandscape;
                this.BasalAreaInM2PerHa *= ruExpansionFactor;

                this.SaplingCohortsPerHa *= ruExpansionFactor;
                this.SaplingsPerHa *= ruExpansionFactor;
                this.SaplingNppPerHa *= ruExpansionFactor;

                this.BranchCarbonInKgPerHa *= ruExpansionFactor;
                this.BranchNitrogenInKgPerHa *= ruExpansionFactor;
                this.CoarseRootCarbonInKgPerHa *= ruExpansionFactor;
                this.CoarseRootNitrogenInKgPerHa *= ruExpansionFactor;
                this.FineRootCarbonInKgPerHa *= ruExpansionFactor;
                this.FineRootNitrogenInKgPerHa *= ruExpansionFactor;
                this.FoliageCarbonInKgPerHa *= ruExpansionFactor;
                this.FoliageNitrogenInKgPerHa *= ruExpansionFactor;
                this.RegenerationCarbonInKgPerHa *= ruExpansionFactor;
                this.RegenerationNitrogenInKgPerHa *= ruExpansionFactor;
                this.StemCarbonInKgPerHa *= ruExpansionFactor;
                this.StemNitrogenInKgPerHa *= ruExpansionFactor;

                this.StemVolumeInM3PerHa *= ruExpansionFactor;
                this.TreesPerHa *= ruExpansionFactor;
                this.TreeNppPerHa *= ruExpansionFactor;
                this.TreeNppPerHaAboveground *= ruExpansionFactor;
            }
        }
    }
}
