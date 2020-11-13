using System;
using System.Collections.Generic;

namespace iLand.Tree
{
    /** @class StandStatistics
      Collects information on stand level for each tree species.
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
        private float mSumDbh;
        private float mSumHeight;
        private float sumSaplingAge;

        public List<float> AverageDbh { get; init; } // average dbh (cm)
        public List<float> AverageHeight { get; init; } // average tree height (m)
        public List<float> BasalArea { get; init; } // sum of basal area of all trees (m2/ha)
        public List<int> CohortCount { get; init; } // number of cohorts of saplings / ha
        public List<float> TreesPerHectare { get; init; }
        public List<float> TotalStemVolumeGrowth { get; init; } // total increment (gesamtwuchsleistung, m3/ha)
        public List<float> LeafAreaIndex { get; init; } // [m2/m2]/ha stocked area.
        public List<float> MeanSaplingAge { get; init; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public List<float> Npp { get; init; } // sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public List<float> NppAbove { get; init; } // above ground NPP (kg Biomass increment)/ha
        public List<float> NppSaplings { get; init; } // carbon gain of saplings (kg Biomass increment)/ha
        public ResourceUnitTreeSpecies? ResourceUnitSpecies { get; init; }
        // number of sapling (Reinekes Law)
        public List<int> SaplingCount { get; init; } // number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha
        public List<float> StemVolume { get; init; } // sum of tree volume (m3/ha)
        // carbon/nitrogen cycle
        public List<float> BranchC { get; init; }
        public List<float> BranchN { get; init; }
        public List<float> CoarseRootC { get; init; }
        public List<float> CoarseRootN { get; init; }
        public List<float> FineRootC { get; init; }
        public List<float> FineRootN { get; init; }
        public List<float> FoliageC { get; init; }
        public List<float> FoliageN { get; init; }
        public List<float> RegenerationC { get; init; }
        public List<float> RegenerationN { get; init; }
        public List<float> StemC { get; init; }
        public List<float> StemN { get; init; }

        public ResourceUnitTreeStatistics(ResourceUnitTreeSpecies? ruSpecies)
        {
            this.ResourceUnitSpecies = ruSpecies;

            int defaultCapacity = 20;
            this.CohortCount = new List<int>(defaultCapacity) { 0 };
            this.SaplingCount = new List<int>(defaultCapacity) { 0 };

            this.AverageDbh = new List<float>(defaultCapacity) { 0.0F };
            this.AverageHeight = new List<float>(defaultCapacity) { 0.0F };
            this.BasalArea = new List<float>(defaultCapacity) { 0.0F };
            this.TreesPerHectare = new List<float>(defaultCapacity) { 0.0F };
            this.TotalStemVolumeGrowth = new List<float>(defaultCapacity) { 0.0F };
            this.LeafAreaIndex = new List<float>(defaultCapacity) { 0.0F };
            this.MeanSaplingAge = new List<float>(defaultCapacity) { 0.0F };
            this.Npp = new List<float>(defaultCapacity) { 0.0F };
            this.NppAbove = new List<float>(defaultCapacity) { 0.0F };
            this.NppSaplings = new List<float>(defaultCapacity) { 0.0F };
            this.StemVolume = new List<float>(defaultCapacity) { 0.0F };
            this.BranchC = new List<float>(defaultCapacity) { 0.0F };
            this.BranchN = new List<float>(defaultCapacity) { 0.0F };
            this.CoarseRootC = new List<float>(defaultCapacity) { 0.0F };
            this.CoarseRootN = new List<float>(defaultCapacity) { 0.0F };
            this.FineRootC = new List<float>(defaultCapacity) { 0.0F };
            this.FineRootN = new List<float>(defaultCapacity) { 0.0F };
            this.FoliageC = new List<float>(defaultCapacity) { 0.0F };
            this.FoliageN = new List<float>(defaultCapacity) { 0.0F };
            this.RegenerationC = new List<float>(defaultCapacity) { 0.0F };
            this.RegenerationN = new List<float>(defaultCapacity) { 0.0F };
            this.StemC = new List<float>(defaultCapacity) { 0.0F };
            this.StemN = new List<float>(defaultCapacity) { 0.0F };
        }

        private int CurrentYear
        {
            get { return this.TreesPerHectare.Count - 1; }
        }

        /// total carbon stock: sum of carbon of all living trees + regeneration layer
        public float GetMostRecentTotalCarbon()
        {
            int currentYear = this.CurrentYear;
            return this.StemC[currentYear] + this.BranchC[currentYear] + this.FoliageC[currentYear] + this.FineRootC[currentYear] + this.CoarseRootC[currentYear] + this.RegenerationC[currentYear];
        }

        public void Zero()
        {
            // reset all values
            this.ZeroTreeStatistics();

            this.sumSaplingAge = 0.0F;

            int currentYear = this.CurrentYear;
            this.Npp[currentYear] = 0.0F;
            this.NppAbove[currentYear] = 0.0F;
            this.NppSaplings[currentYear] = 0.0F;
            this.CohortCount[currentYear] = 0;
            this.SaplingCount[currentYear] = 0;
            this.MeanSaplingAge[currentYear] = 0.0F;
            this.RegenerationC[currentYear] = 0.0F;
            this.RegenerationN[currentYear] = 0.0F;
        }

        public void ZeroTreeStatistics()
        {
            // reset only those values that are directly accumulated from trees
            // TODO: why aren't non-sapling NPP fields cleared here?
            this.mSumDbh = 0.0F;
            this.mSumHeight = 0.0F;

            int currentYear = this.CurrentYear;
            this.TreesPerHectare[currentYear] = 0.0F;
            this.AverageDbh[currentYear] = 0.0F;
            this.AverageHeight[currentYear] = 0.0f;
            this.BasalArea[currentYear] = 0.0F;
            this.StemVolume[currentYear] = 0.0F;
            this.TotalStemVolumeGrowth[currentYear] = 0.0F;
            this.LeafAreaIndex[currentYear] = 0.0F;
            /*mNPP = mNPPabove = 0.0F;
            mNPPsaplings = 0.0F;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0F;
            mSumSaplingAge = 0.0F;*/
            this.StemC[currentYear] = 0.0F;
            this.FoliageC[currentYear] = 0.0F;
            this.BranchC[currentYear] = 0.0F;
            this.CoarseRootC[currentYear] = 0.0F;
            this.FineRootC[currentYear] = 0.0F;
            this.StemN[currentYear] = 0.0F;
            this.FoliageN[currentYear] = 0.0F;
            this.BranchN[currentYear] = 0.0F;
            this.CoarseRootN[currentYear] = 0.0F;
            this.FineRootN[currentYear] = 0.0F;
            //mCRegeneration=0.0F; 
            //mNRegeneration=0.0F;
        }

        public void Add(SaplingProperties sapling)
        {
            int currentYear = this.RegenerationC.Count - 1;
            this.CohortCount[currentYear] += sapling.LivingCohorts;
            this.SaplingCount[currentYear] += (int)sapling.LivingSaplings; // saplings with height >1.3m

            this.sumSaplingAge += sapling.AverageAge * sapling.LivingCohorts;

            this.RegenerationC[currentYear] += sapling.CarbonLiving.C;
            this.RegenerationN[currentYear] += sapling.CarbonLiving.N;

            this.NppSaplings[currentYear] += sapling.CarbonGain.C / Constant.BiomassCFraction;
        }

        public void Add(Trees trees, int treeIndex, TreeGrowthData? treeGrowth, bool skipDead = false)
        {
            if (skipDead && trees.IsDead(treeIndex))
            {
                return;
            }

            int currentYear = this.CurrentYear;
            ++this.TreesPerHectare[currentYear];
            this.mSumDbh += trees.Dbh[treeIndex];
            this.mSumHeight += trees.Height[treeIndex];
            this.BasalArea[currentYear] += trees.GetBasalArea(treeIndex);
            this.StemVolume[currentYear] += trees.GetStemVolume(treeIndex);
            this.LeafAreaIndex[currentYear] += trees.LeafArea[treeIndex]; // warning: sum of leafarea!
            if (treeGrowth != null)
            {
                this.Npp[currentYear] += treeGrowth.NppTotal;
                this.NppAbove[currentYear] += treeGrowth.NppAboveground;
            }
            // carbon and nitrogen pools
            this.BranchC[currentYear] += Constant.BiomassCFraction * trees.GetBranchBiomass(treeIndex);
            this.BranchN[currentYear] += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.GetBranchBiomass(treeIndex);
            this.CoarseRootC[currentYear] += Constant.BiomassCFraction * trees.CoarseRootMass[treeIndex];
            this.CoarseRootN[currentYear] += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.CoarseRootMass[treeIndex];
            this.FineRootC[currentYear] += Constant.BiomassCFraction * trees.FineRootMass[treeIndex];
            this.FineRootN[currentYear] += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FineRootMass[treeIndex];
            this.FoliageC[currentYear] += Constant.BiomassCFraction * trees.FoliageMass[treeIndex];
            this.FoliageN[currentYear] += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FoliageMass[treeIndex];
            this.StemC[currentYear] += Constant.BiomassCFraction * trees.StemMass[treeIndex];
            this.StemN[currentYear] += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.StemMass[treeIndex];
        }

        // note: mRUS = 0 for aggregated statistics
        public void OnEndYear()
        {
            int currentYear = this.CurrentYear;
            float treesPerHectare = this.TreesPerHectare[currentYear];
            if (treesPerHectare > 0.0F)
            {
                this.AverageDbh[currentYear] = this.mSumDbh / treesPerHectare;
                this.AverageHeight[currentYear] = this.mSumHeight / treesPerHectare;
                if (this.ResourceUnitSpecies != null && this.ResourceUnitSpecies.RU.AreaInLandscape > 0.0F)
                {
                    this.LeafAreaIndex[currentYear] /= this.ResourceUnitSpecies.RU.AreaInLandscape; // convert from leafarea to LAI
                }
            }
            if (this.CohortCount[currentYear] != 0)
            {
                this.MeanSaplingAge[currentYear] = this.sumSaplingAge / this.CohortCount[currentYear]; // else leave mean sapling age as zero
            }

            // scale values to per hectare if resource unit <> 1ha
            // note: do this only on species-level (avoid double scaling)
            if (this.ResourceUnitSpecies != null)
            {
                float areaFactor = Constant.RUArea / this.ResourceUnitSpecies.RU.AreaInLandscape;
                if (areaFactor != 1.0F)
                {
                    this.TreesPerHectare[currentYear] *= areaFactor;
                    this.BasalArea[currentYear] *= areaFactor;
                    this.StemVolume[currentYear] *= areaFactor;
                    this.mSumDbh *= areaFactor;
                    this.Npp[currentYear] *= areaFactor;
                    this.NppAbove[currentYear] *= areaFactor;
                    this.NppSaplings[currentYear] *= areaFactor;
                    //mGWL *= area_factor;
                    this.CohortCount[currentYear] = (int)(areaFactor * this.CohortCount[currentYear]); // TODO: quantization?
                    this.SaplingCount[currentYear] = (int)(areaFactor * this.SaplingCount[currentYear]); // TODO: quantization?
                    //float mCStem, mCFoliage, mCBranch, mCCoarseRoot, mCFineRoot;
                    //float mNStem, mNFoliage, mNBranch, mNCoarseRoot, mNFineRoot;
                    //float mCRegeneration, mNRegeneration;
                    this.StemC[currentYear] *= areaFactor;
                    this.StemN[currentYear] *= areaFactor;
                    this.FoliageC[currentYear] *= areaFactor;
                    this.FoliageN[currentYear] *= areaFactor;
                    this.BranchC[currentYear] *= areaFactor;
                    this.BranchN[currentYear] *= areaFactor;
                    this.CoarseRootC[currentYear] *= areaFactor;
                    this.CoarseRootN[currentYear] *= areaFactor;
                    this.FineRootC[currentYear] *= areaFactor;
                    this.FineRootN[currentYear] *= areaFactor;
                    this.RegenerationC[currentYear] *= areaFactor;
                    this.RegenerationN[currentYear] *= areaFactor;
                }
                this.TotalStemVolumeGrowth[currentYear] = this.StemVolume[currentYear] + this.ResourceUnitSpecies.RemovedStemVolume; // removedVolume: per ha, SumVolume now too
            }
        }

        public void OnStartYear()
        {
            this.Npp.Add(0.0F);
            this.NppAbove.Add(0.0F);
            this.NppSaplings.Add(0.0F);
            this.CohortCount.Add(0);
            this.SaplingCount.Add(0);
            this.MeanSaplingAge.Add(0.0F);
            this.RegenerationC.Add(0.0F);
            this.RegenerationN.Add(0.0F);
            this.TreesPerHectare.Add(0.0F);
            this.AverageDbh.Add(0.0F);
            this.AverageHeight.Add(0.0F);
            this.BasalArea.Add(0.0F);
            this.StemVolume.Add(0.0F);
            this.TotalStemVolumeGrowth.Add(0.0F);
            this.LeafAreaIndex.Add(0.0F);
            this.StemC.Add(0.0F);
            this.FoliageC.Add(0.0F);
            this.BranchC.Add(0.0F);
            this.CoarseRootC.Add(0.0F);
            this.FineRootC.Add(0.0F);
            this.StemN.Add(0.0F);
            this.FoliageN.Add(0.0F);
            this.BranchN.Add(0.0F);
            this.CoarseRootN.Add(0.0F);
            this.FineRootN.Add(0.0F);
        }

        public void AddCurrentYears(ResourceUnitTreeStatistics other)
        {
            if (this.TreesPerHectare.Count != other.TreesPerHectare.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(other));
            }

            int currentYear = this.CurrentYear;
            this.TreesPerHectare[currentYear] += other.TreesPerHectare[currentYear];
            this.BasalArea[currentYear] += other.BasalArea[currentYear];
            this.mSumDbh += other.mSumDbh;
            this.mSumHeight += other.mSumHeight;
            this.StemVolume[currentYear] += other.StemVolume[currentYear];
            this.LeafAreaIndex[currentYear] += other.LeafAreaIndex[currentYear];
            this.Npp[currentYear] += other.Npp[currentYear];
            this.NppAbove[currentYear] += other.NppAbove[currentYear];
            this.NppSaplings[currentYear] += other.NppSaplings[currentYear];
            this.TotalStemVolumeGrowth[currentYear] += other.TotalStemVolumeGrowth[currentYear];
            // regeneration
            this.CohortCount[currentYear] += other.CohortCount[currentYear];
            this.SaplingCount[currentYear] += other.SaplingCount[currentYear];
            this.sumSaplingAge += other.sumSaplingAge;
            // carbon/nitrogen pools
            this.StemC[currentYear] += other.StemC[currentYear];
            this.StemN[currentYear] += other.StemN[currentYear];
            this.BranchC[currentYear] += other.BranchC[currentYear];
            this.BranchN[currentYear] += other.BranchN[currentYear];
            this.FoliageC[currentYear] += other.FoliageC[currentYear];
            this.FoliageN[currentYear] += other.FoliageN[currentYear];
            this.FineRootC[currentYear] += other.FineRootC[currentYear];
            this.FineRootN[currentYear] += other.FineRootN[currentYear];
            this.CoarseRootC[currentYear] += other.CoarseRootC[currentYear];
            this.CoarseRootN[currentYear] += other.CoarseRootN[currentYear];
            this.RegenerationC[currentYear] += other.RegenerationC[currentYear];
            this.RegenerationN[currentYear] += other.RegenerationN[currentYear];
        }

        public void AddCurrentYearsWeighted(ResourceUnitTreeStatistics other, float weight)
        {
            // aggregates that are not scaled to hectares
            int currentYear = this.CurrentYear;
            this.TreesPerHectare[currentYear] += other.TreesPerHectare[currentYear] * weight;
            this.BasalArea[currentYear] += other.BasalArea[currentYear] * weight;
            this.mSumDbh += other.mSumDbh * weight;
            this.mSumHeight += other.mSumHeight * weight;
            this.StemVolume[currentYear] += other.StemVolume[currentYear] * weight;
            // averages that are scaled to per hectare need to be scaled
            this.AverageDbh[currentYear] += other.AverageDbh[currentYear] * weight;
            this.AverageHeight[currentYear] += other.AverageHeight[currentYear] * weight;
            this.MeanSaplingAge[currentYear] += other.MeanSaplingAge[currentYear] * weight;
            this.LeafAreaIndex[currentYear] += other.LeafAreaIndex[currentYear] * weight;

            this.Npp[currentYear] += other.Npp[currentYear] * weight;
            this.NppAbove[currentYear] += other.NppAbove[currentYear] * weight;
            this.NppSaplings[currentYear] += other.NppSaplings[currentYear] * weight;
            this.TotalStemVolumeGrowth[currentYear] += other.TotalStemVolumeGrowth[currentYear] * weight;
            // regeneration
            this.CohortCount[currentYear] += (int)(other.CohortCount[currentYear] * weight); // BUGBUG: quantization?
            this.SaplingCount[currentYear] += (int)(other.SaplingCount[currentYear] * weight); // BUGBUG: quantization?
            this.sumSaplingAge += other.sumSaplingAge * weight;
            // carbon/nitrogen pools
            this.StemC[currentYear] += other.StemC[currentYear] * weight;
            this.StemN[currentYear] += other.StemN[currentYear] * weight;
            this.BranchC[currentYear] += other.BranchC[currentYear] * weight;
            this.BranchN[currentYear] += other.BranchN[currentYear] * weight;
            this.FoliageC[currentYear] += other.FoliageC[currentYear] * weight;
            this.FoliageN[currentYear] += other.FoliageN[currentYear] * weight;
            this.FineRootC[currentYear] += other.FineRootC[currentYear] * weight;
            this.FineRootN[currentYear] += other.FineRootN[currentYear] * weight;
            this.CoarseRootC[currentYear] += other.CoarseRootC[currentYear] * weight;
            this.CoarseRootN[currentYear] += other.CoarseRootN[currentYear] * weight;
            this.RegenerationC[currentYear] += other.RegenerationC[currentYear] * weight;
            this.RegenerationN[currentYear] += other.RegenerationN[currentYear] * weight;
        }
    }
}
