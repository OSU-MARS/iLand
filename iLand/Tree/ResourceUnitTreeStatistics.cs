using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /** Collects information on stand level for each tree species.
        Call clear() to clear the statistics, then call add() for each tree and finally calculate().
        To aggregate on a higher level, use add() for each StandStatistics object to include, and then
        calculate() on the higher level.
        Todo-List for new items:
        - add a member variable and a getter
        - add to "add(Tree)" and "calculate()"
        - add to "add(StandStatistics)" as well!
      */
    public class ResourceUnitTreeStatistics
    {
        private readonly bool requiresPerHectareConversion;
        private readonly ResourceUnit ru;
        private float sumDbh;
        private float sumHeight;
        private float sumSaplingAge;

        public bool IsPerHectare { get; init; } // if true, metrics are per hectare, if false metrics are aggregated across trees but not per area

        // trees
        public float AverageDbh { get; private set; } // average DBH, cm
        public float AverageHeight { get; private set; } // average tree height, m
        public float BasalArea { get; private set; } // sum of basal area of all trees, m² on resource unit or m²/ha
        public float LeafArea { get; private set; } // m² on resource unit
        public float LeafAreaIndex { get; private set; } // [m²/m²]/ha of height cells containing trees
        public float StemVolume { get; private set; } // sum of trees' stem volume, m³ or m³/ha, may be live, dead, or removed depending on what this statistics object represents
        public float LiveAndSnagStemVolume { get; private set; } // total increment (gesamtwuchsleistung), m³ or m³/ha
        public float TreeCount { get; private set; } // trees on resource unit or TPH
        public float TreeNpp { get; private set; } // sum of above + belowground NPP of trees >4m tall, (kg biomass increment) or (kg biomass increment)/ha
        public float TreeNppAboveground { get; private set; } // (kg biomass increment) or (kg biomass increment)/ha
        public ResourceUnitTreeSpecies? ResourceUnitSpecies { get; private init; } // species if statistics are species-specific, otherwise null

        // saplings
        public int CohortCount { get; private set; } // number of cohorts of saplings or cohorts/ha
        public float MeanSaplingAge { get; private set; } // average age of sapling (not currently weighted by represented sapling numbers)
        public float SaplingNpp { get; private set; } // carbon gain of saplings (kg biomass increment) or (kg biomass increment)/ha
        public int SaplingCount { get; private set; } // number of individuals in regeneration layer (represented by CohortCount cohorts), saplings or saplings/ha
        
        // carbon/nitrogen cycle
        public float BranchCarbon { get; private set; } // kg or kg/ha
        public float BranchNitrogen { get; private set; } // kg or kg/ha
        public float CoarseRootCarbon { get; private set; } // kg or kg/ha
        public float CoarseRootNitrogen { get; private set; } // kg or kg/ha
        public float FineRootCarbon { get; private set; } // kg or kg/ha
        public float FineRootNitrogen { get; private set; } // kg or kg/ha
        public float FoliageCarbon { get; private set; } // kg or kg/ha
        public float FoliageNitrogen { get; private set; } // kg or kg/ha
        public float RegenerationCarbon { get; private set; } // kg or kg/ha
        public float RegenerationNitrogen { get; private set; } // kg or kg/ha
        public float StemCarbon { get; private set; } // kg or kg/ha
        public float StemNitrogen { get; private set; } // kg or kg/ha

        public ResourceUnitTreeStatistics(ResourceUnit ru)
        {
            this.ru = ru;
            this.sumDbh = 0.0F;
            this.sumHeight = 0.0F;
            this.sumSaplingAge = 0.0F;

            this.IsPerHectare = false;
            this.requiresPerHectareConversion = false;
            this.ResourceUnitSpecies = null;

            this.AverageDbh = 0.0F;
            this.AverageHeight = 0.0F;
            this.BasalArea = 0.0F;
            this.LeafArea = 0.0F;
            this.LeafAreaIndex = 0.0F;
            this.StemVolume = 0.0F;
            this.LiveAndSnagStemVolume = 0.0F;
            this.TreeNpp = 0.0F;
            this.TreeNppAboveground = 0.0F;
            this.TreeCount = 0.0F;

            this.CohortCount = 0;
            this.MeanSaplingAge = 0.0F;
            this.SaplingNpp = 0.0F;
            this.SaplingCount = 0;

            this.BranchCarbon = 0.0F;
            this.BranchNitrogen = 0.0F;
            this.CoarseRootCarbon = 0.0F;
            this.CoarseRootNitrogen = 0.0F;
            this.FineRootCarbon = 0.0F;
            this.FineRootNitrogen = 0.0F;
            this.FoliageCarbon = 0.0F;
            this.FoliageNitrogen = 0.0F;
            this.RegenerationCarbon = 0.0F;
            this.RegenerationNitrogen = 0.0F;
            this.StemCarbon = 0.0F;
            this.StemNitrogen = 0.0F;
        }

        public ResourceUnitTreeStatistics(ResourceUnit ru, ResourceUnitTreeSpecies ruSpecies)
            : this(ru)
        {
            this.requiresPerHectareConversion = true;

            this.IsPerHectare = true;
            this.ResourceUnitSpecies = ruSpecies;
        }

        /// total carbon stock: sum of carbon of all living trees + regeneration layer
        public float GetTotalCarbon()
        {
            return this.StemCarbon + this.BranchCarbon + this.FoliageCarbon + this.FineRootCarbon + this.CoarseRootCarbon + this.RegenerationCarbon;
        }

        public void Zero()
        {
            // reset all values
            this.ZeroTreeStatistics();

            this.sumSaplingAge = 0.0F;

            this.TreeNpp = 0.0F;
            this.TreeNppAboveground = 0.0F;
            this.SaplingNpp = 0.0F;
            this.CohortCount = 0;
            this.SaplingCount = 0;
            this.MeanSaplingAge = 0.0F;
            this.RegenerationCarbon = 0.0F;
            this.RegenerationNitrogen = 0.0F;
        }

        public void ZeroTreeStatistics()
        {
            // reset only those values that are directly accumulated from trees
            // TODO: why aren't non-sapling NPP fields cleared here?
            this.sumDbh = 0.0F;
            this.sumHeight = 0.0F;

            this.TreeCount = 0.0F;
            this.AverageDbh = 0.0F;
            this.AverageHeight = 0.0f;
            this.BasalArea = 0.0F;
            this.StemVolume = 0.0F;
            this.LiveAndSnagStemVolume = 0.0F;
            this.LeafArea = 0.0F;
            this.LeafAreaIndex = 0.0F;
            /*mNPP = mNPPabove = 0.0F;
            mNPPsaplings = 0.0F;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0F;
            mSumSaplingAge = 0.0F;*/
            this.StemCarbon = 0.0F;
            this.FoliageCarbon = 0.0F;
            this.BranchCarbon = 0.0F;
            this.CoarseRootCarbon = 0.0F;
            this.FineRootCarbon = 0.0F;
            this.StemNitrogen = 0.0F;
            this.FoliageNitrogen = 0.0F;
            this.BranchNitrogen = 0.0F;
            this.CoarseRootNitrogen = 0.0F;
            this.FineRootNitrogen = 0.0F;
            //mCRegeneration=0.0F; 
            //mNRegeneration=0.0F;
        }

        public void AddToCurrentYear(SaplingProperties sapling)
        {
            this.CohortCount += sapling.LivingCohorts;
            this.SaplingCount += (int)sapling.LivingSaplings; // saplings with height >1.3m

            this.sumSaplingAge += sapling.AverageAge * sapling.LivingCohorts;

            this.RegenerationCarbon += sapling.CarbonLiving.C;
            this.RegenerationNitrogen += sapling.CarbonLiving.N;

            this.SaplingNpp += sapling.CarbonGain.C / Constant.BiomassCFraction;
        }

        public void AddToCurrentYear(Trees trees, int treeIndex, TreeGrowthData? treeGrowth, bool skipDead)
        {
            if (skipDead && trees.IsDead(treeIndex))
            {
                return;
            }

            // trees
            this.sumDbh += trees.Dbh[treeIndex];
            this.sumHeight += trees.Height[treeIndex];
            this.BasalArea += trees.GetBasalArea(treeIndex);
            this.LeafArea += trees.LeafArea[treeIndex];
            this.StemVolume += trees.GetStemVolume(treeIndex);
            ++this.TreeCount;
            if (treeGrowth != null)
            {
                this.TreeNpp += treeGrowth.NppTotal;
                this.TreeNppAboveground += treeGrowth.NppAboveground;
            }

            // carbon and nitrogen pools
            this.BranchCarbon += Constant.BiomassCFraction * trees.GetBranchBiomass(treeIndex);
            this.BranchNitrogen += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.GetBranchBiomass(treeIndex);
            this.CoarseRootCarbon += Constant.BiomassCFraction * trees.CoarseRootMass[treeIndex];
            this.CoarseRootNitrogen += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.CoarseRootMass[treeIndex];
            this.FineRootCarbon += Constant.BiomassCFraction * trees.FineRootMass[treeIndex];
            this.FineRootNitrogen += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FineRootMass[treeIndex];
            this.FoliageCarbon += Constant.BiomassCFraction * trees.FoliageMass[treeIndex];
            this.FoliageNitrogen += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FoliageMass[treeIndex];
            this.StemCarbon += Constant.BiomassCFraction * trees.StemMass[treeIndex];
            this.StemNitrogen += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.StemMass[treeIndex];
        }

        public virtual void OnEndYear()
        {
            float treesPerHectare = this.TreeCount;
            if (treesPerHectare > 0.0F)
            {
                this.AverageDbh = this.sumDbh / treesPerHectare;
                this.AverageHeight = this.sumHeight / treesPerHectare;
            }
            if (this.CohortCount != 0)
            {
                this.MeanSaplingAge = this.sumSaplingAge / this.CohortCount; // else leave mean sapling age as zero
            }
            Debug.Assert(this.ru.AreaInLandscape > 0.0F);
            this.LeafAreaIndex = this.LeafArea / this.ru.AreaInLandscape; // this.ru.AreaWithTrees;
            this.LiveAndSnagStemVolume = this.StemVolume; // initialization, removed volume may be added below

            // scale values to per hectare if resource unit <> 1ha
            // note: do this only on species-level (avoid double scaling)
            if (this.requiresPerHectareConversion)
            {              
                float ruExpansionFactor = Constant.ResourceUnitAreaInM2 / this.ru.AreaInLandscape;
                if (ruExpansionFactor != 1.0F)
                {
                    this.sumDbh *= ruExpansionFactor; // probably not strictly necessary
                    this.BasalArea *= ruExpansionFactor;
                    this.StemVolume *= ruExpansionFactor;
                    this.LiveAndSnagStemVolume *= ruExpansionFactor;
                    this.TreeCount *= ruExpansionFactor;
                    this.TreeNpp *= ruExpansionFactor;
                    this.TreeNppAboveground *= ruExpansionFactor;

                    //mGWL *= area_factor;
                    this.CohortCount = (int)(ruExpansionFactor * this.CohortCount); // TODO: change to float to avoid quantization?
                    this.SaplingCount = (int)(ruExpansionFactor * this.SaplingCount); // TODO: change to float to avoid quantization?
                    this.SaplingNpp *= ruExpansionFactor;

                    this.BranchCarbon *= ruExpansionFactor;
                    this.BranchNitrogen *= ruExpansionFactor;
                    this.CoarseRootCarbon *= ruExpansionFactor;
                    this.CoarseRootNitrogen *= ruExpansionFactor;
                    this.FineRootCarbon *= ruExpansionFactor;
                    this.FineRootNitrogen *= ruExpansionFactor;
                    this.FoliageCarbon *= ruExpansionFactor;
                    this.FoliageNitrogen *= ruExpansionFactor;
                    this.RegenerationCarbon *= ruExpansionFactor;
                    this.RegenerationNitrogen *= ruExpansionFactor;
                    this.StemCarbon *= ruExpansionFactor;
                    this.StemNitrogen *= ruExpansionFactor;
                }

                if (this.ResourceUnitSpecies != null)
                {
                    this.LiveAndSnagStemVolume += this.ResourceUnitSpecies.RemovedStemVolume; // RemovedStemVolume assumed to be in m³/ha
                }
            }
        }

        public void AddCurrentYears(ResourceUnitTreeStatistics other)
        {
            if (Object.ReferenceEquals(this.ru, other.ru) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(other), "Attempt to add statistics from different resource units (grid indices " + this.ru.ResourceUnitGridIndex + " and " + other.ru.ResourceUnitGridIndex + ".");
            }
            if (this.IsPerHectare != other.IsPerHectare)
            {
                throw new ArgumentOutOfRangeException(nameof(other), "Attempt to add statistics with mismatched units. Per hectare settings: " + this.IsPerHectare + " and " + other.IsPerHectare + ".");
            }
            Debug.Assert(this.ResourceUnitSpecies == null && other.ResourceUnitSpecies != null);

            // trees
            this.sumDbh += other.sumDbh;
            this.sumHeight += other.sumHeight;

            this.BasalArea += other.BasalArea;
            this.LeafArea += other.LeafArea;
            this.LeafAreaIndex += other.LeafAreaIndex; // can add directly due to same resource unit constraint
            this.LiveAndSnagStemVolume += other.LiveAndSnagStemVolume;
            this.StemVolume += other.StemVolume;
            this.TreeCount += other.TreeCount;
            this.TreeNpp += other.TreeNpp;
            this.TreeNppAboveground += other.TreeNppAboveground;

            // regeneration
            this.sumSaplingAge += other.sumSaplingAge;

            this.CohortCount += other.CohortCount;
            this.SaplingCount += other.SaplingCount;
            this.SaplingNpp += other.SaplingNpp;

            // carbon/nitrogen pools
            this.BranchCarbon += other.BranchCarbon;
            this.BranchNitrogen += other.BranchNitrogen;
            this.CoarseRootCarbon += other.CoarseRootCarbon;
            this.FineRootCarbon += other.FineRootCarbon;
            this.FineRootNitrogen += other.FineRootNitrogen;
            this.CoarseRootNitrogen += other.CoarseRootNitrogen;
            this.FoliageCarbon += other.FoliageCarbon;
            this.FoliageNitrogen += other.FoliageNitrogen;
            this.RegenerationCarbon += other.RegenerationCarbon;
            this.RegenerationNitrogen += other.RegenerationNitrogen;
            this.StemCarbon += other.StemCarbon;
            this.StemNitrogen += other.StemNitrogen;
        }
    }
}
