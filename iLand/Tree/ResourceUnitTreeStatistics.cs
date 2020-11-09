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
        private double mSumDbh;
        private double mSumHeight;
        private double sumSaplingAge;

        public double AverageDbh { get; private set; } // average dbh (cm)
        public double AverageHeight { get; private set; } // average tree height (m)
        public double BasalArea { get; private set; } // sum of basal area of all trees (m2/ha)
        public int CohortCount { get; private set; } // number of cohorts of saplings / ha
        public double TreesPerHectare { get; private set; }
        public double TotalStemVolumeGrowth { get; private set; } // total increment (gesamtwuchsleistung, m3/ha)
        public float LeafAreaIndex { get; private set; } // [m2/m2]/ha stocked area.
        public double MeanSaplingAge { get; private set; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public double Npp { get; private set; } // sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public double NppAbove { get; private set; } // above ground NPP (kg Biomass increment)/ha
        public double NppSaplings { get; private set; } // carbon gain of saplings (kg Biomass increment)/ha
        public ResourceUnitTreeSpecies ResourceUnitSpecies { get; set; }
        // number of sapling (Reinekes Law)
        public int SaplingCount { get; private set; } // number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha
        public double StemVolume { get; private set; } // sum of tree volume (m3/ha)
        // carbon/nitrogen cycle
        public double BranchC { get; private set; }
        public double BranchN { get; private set; }
        public double CoarseRootC { get; private set; }
        public double CoarseRootN { get; private set; }
        public double FineRootC { get; private set; }
        public double FineRootN { get; private set; }
        public double FoliageC { get; private set; }
        public double FoliageN { get; private set; }
        public double RegenerationC { get; private set; }
        public double RegenerationN { get; private set; }
        public double StemC { get; private set; }
        public double StemN { get; private set; }

        public ResourceUnitTreeStatistics() 
        { 
            this.ResourceUnitSpecies = null;
            this.Zero(); 
        }

        /// total carbon stock: sum of carbon of all living trees + regeneration layer
        public double GetTotalCarbon() 
        { 
            return this.StemC + this.BranchC + this.FoliageC + this.FineRootC + this.CoarseRootC + this.RegenerationC; 
        }

        public void Zero()
        {
            // reset all values
            this.ZeroTreeStatistics();

            this.sumSaplingAge = 0.0;

            this.Npp = 0.0;
            this.NppAbove = 0.0;
            this.NppSaplings = 0.0;
            this.CohortCount = 0;
            this.SaplingCount = 0;
            this.MeanSaplingAge = 0.0;
            this.RegenerationC = 0.0;
            this.RegenerationN = 0.0;
        }

        public void ZeroTreeStatistics()
        {
            // reset only those values that are directly accumulated from trees
            // TODO: why aren't non-sapling NPP fields clieared here?
            this.TreesPerHectare = 0;
            this.mSumDbh = 0.0;
            this.mSumHeight = 0.0;
            this.AverageDbh = 0.0;
            this.AverageHeight = 0.0;
            this.BasalArea = 0.0;
            this.StemVolume = 0.0;
            this.TotalStemVolumeGrowth = 0.0;
            this.LeafAreaIndex = 0.0F;
            /*mNPP = mNPPabove = 0.0;
            mNPPsaplings = 0.0;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0;
            mSumSaplingAge = 0.0;*/
            this.StemC = 0.0;
            this.FoliageC = 0.0;
            this.BranchC = 0.0;
            this.CoarseRootC = 0.0;
            this.FineRootC = 0.0;
            this.StemN = 0.0;
            this.FoliageN = 0.0;
            this.BranchN = 0.0;
            this.CoarseRootN = 0.0;
            this.FineRootN = 0.0;
            /*mCRegeneration=0.0; mNRegeneration=0.0;*/
        }

        public void Add(SaplingProperties sapling)
        {
            this.CohortCount += sapling.LivingCohorts;
            this.SaplingCount += (int)sapling.LivingSaplings; // saplings with height >1.3m

            this.sumSaplingAge += sapling.AverageAge * sapling.LivingCohorts;

            this.RegenerationC += sapling.CarbonLiving.C;
            this.RegenerationN += sapling.CarbonLiving.N;

            this.NppSaplings += sapling.CarbonGain.C / Constant.BiomassCFraction;
        }

        public void Add(Trees trees, int treeIndex, TreeGrowthData treeGrowth, bool skipDead = false)
        {
            if (skipDead && trees.IsDead(treeIndex))
            {
                return;
            }

            ++this.TreesPerHectare;
            this.mSumDbh += trees.Dbh[treeIndex];
            this.mSumHeight += trees.Height[treeIndex];
            this.BasalArea += trees.GetBasalArea(treeIndex);
            this.StemVolume += trees.GetStemVolume(treeIndex);
            this.LeafAreaIndex += trees.LeafArea[treeIndex]; // warning: sum of leafarea!
            if (treeGrowth != null)
            {
                this.Npp += treeGrowth.NppTotal;
                this.NppAbove += treeGrowth.NppAboveground;
            }
            // carbon and nitrogen pools
            this.BranchC += Constant.BiomassCFraction * trees.GetBranchBiomass(treeIndex);
            this.BranchN += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.GetBranchBiomass(treeIndex);
            this.CoarseRootC += Constant.BiomassCFraction * trees.CoarseRootMass[treeIndex];
            this.CoarseRootN += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.CoarseRootMass[treeIndex];
            this.FineRootC += Constant.BiomassCFraction * trees.FineRootMass[treeIndex];
            this.FineRootN += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FineRootMass[treeIndex];
            this.FoliageC += Constant.BiomassCFraction * trees.FoliageMass[treeIndex];
            this.FoliageN += Constant.BiomassCFraction / trees.Species.CNRatioFineRoot * trees.FoliageMass[treeIndex];
            this.StemC += Constant.BiomassCFraction * trees.StemMass[treeIndex];
            this.StemN += Constant.BiomassCFraction / trees.Species.CNRatioWood * trees.StemMass[treeIndex];
        }

        // note: mRUS = 0 for aggregated statistics
        public void OnEndYear()
        {
            if (this.TreesPerHectare > 0.0)
            {
                this.AverageDbh = mSumDbh / TreesPerHectare;
                this.AverageHeight = mSumHeight / TreesPerHectare;
                if (this.ResourceUnitSpecies != null && ResourceUnitSpecies.RU.AreaInLandscape > 0.0)
                {
                    this.LeafAreaIndex /= ResourceUnitSpecies.RU.AreaInLandscape; // convert from leafarea to LAI
                }
            }
            if (this.CohortCount != 0)
            {
                this.MeanSaplingAge = sumSaplingAge / (double)CohortCount;
            }

            // scale values to per hectare if resource unit <> 1ha
            // note: do this only on species-level (avoid double scaling)
            if (this.ResourceUnitSpecies != null)
            {
                float areaFactor = Constant.RUArea / this.ResourceUnitSpecies.RU.AreaInLandscape;
                if (areaFactor != 1.0F)
                {
                    this.TreesPerHectare *= areaFactor;
                    this.BasalArea *= areaFactor;
                    this.StemVolume *= areaFactor;
                    this.mSumDbh *= areaFactor;
                    this.Npp *= areaFactor;
                    this.NppAbove *= areaFactor;
                    this.NppSaplings *= areaFactor;
                    //mGWL *= area_factor;
                    this.CohortCount = (int)(areaFactor * this.CohortCount); // TODO: quantization?
                    this.SaplingCount = (int)(areaFactor * this.SaplingCount); // TODO: quantization?
                    //double mCStem, mCFoliage, mCBranch, mCCoarseRoot, mCFineRoot;
                    //double mNStem, mNFoliage, mNBranch, mNCoarseRoot, mNFineRoot;
                    //double mCRegeneration, mNRegeneration;
                    this.StemC *= areaFactor;
                    this.StemN *= areaFactor;
                    this.FoliageC *= areaFactor;
                    this.FoliageN *= areaFactor;
                    this.BranchC *= areaFactor;
                    this.BranchN *= areaFactor;
                    this.CoarseRootC *= areaFactor;
                    this.CoarseRootN *= areaFactor;
                    this.FineRootC *= areaFactor;
                    this.FineRootN *= areaFactor;
                    this.RegenerationC *= areaFactor;
                    this.RegenerationN *= areaFactor;
                }
                this.TotalStemVolumeGrowth = this.StemVolume + this.ResourceUnitSpecies.RemovedStemVolume; // removedVolume: per ha, SumVolume now too
            }
        }

        public void Add(ResourceUnitTreeStatistics other)
        {
            this.TreesPerHectare += other.TreesPerHectare;
            this.BasalArea += other.BasalArea;
            this.mSumDbh += other.mSumDbh;
            this.mSumHeight += other.mSumHeight;
            this.StemVolume += other.StemVolume;
            this.LeafAreaIndex += other.LeafAreaIndex;
            this.Npp += other.Npp;
            this.NppAbove += other.NppAbove;
            this.NppSaplings += other.NppSaplings;
            this.TotalStemVolumeGrowth += other.TotalStemVolumeGrowth;
            // regeneration
            this.CohortCount += other.CohortCount;
            this.SaplingCount += other.SaplingCount;
            this.sumSaplingAge += other.sumSaplingAge;
            // carbon/nitrogen pools
            this.StemC += other.StemC;
            this.StemN += other.StemN;
            this.BranchC += other.BranchC;
            this.BranchN += other.BranchN;
            this.FoliageC += other.FoliageC;
            this.FoliageN += other.FoliageN;
            this.FineRootC += other.FineRootC;
            this.FineRootN += other.FineRootN;
            this.CoarseRootC += other.CoarseRootC;
            this.CoarseRootN += other.CoarseRootN;
            this.RegenerationC += other.RegenerationC;
            this.RegenerationN += other.RegenerationN;
        }

        public void AddWeighted(ResourceUnitTreeStatistics other, double weight)
        {
            // aggregates that are not scaled to hectares
            this.TreesPerHectare += other.TreesPerHectare * weight;
            this.BasalArea += other.BasalArea * weight;
            this.mSumDbh += other.mSumDbh * weight;
            this.mSumHeight += other.mSumHeight * weight;
            this.StemVolume += other.StemVolume * weight;
            // averages that are scaled to per hectare need to be scaled
            this.AverageDbh += other.AverageDbh * weight;
            this.AverageHeight += other.AverageHeight * weight;
            this.MeanSaplingAge += other.MeanSaplingAge * weight;
            this.LeafAreaIndex += other.LeafAreaIndex * (float)weight;

            this.Npp += other.Npp * weight;
            this.NppAbove += other.NppAbove * weight;
            this.NppSaplings += other.NppSaplings * weight;
            this.TotalStemVolumeGrowth += other.TotalStemVolumeGrowth * weight;
            // regeneration
            this.CohortCount += (int)(other.CohortCount * weight); // BUGBUG: quantization?
            this.SaplingCount += (int)(other.SaplingCount * weight); // BUGBUG: quantization?
            this.sumSaplingAge += other.sumSaplingAge * weight;
            // carbon/nitrogen pools
            this.StemC += other.StemC * weight;
            this.StemN += other.StemN * weight;
            this.BranchC += other.BranchC * weight;
            this.BranchN += other.BranchN * weight;
            this.FoliageC += other.FoliageC * weight;
            this.FoliageN += other.FoliageN * weight;
            this.FineRootC += other.FineRootC * weight;
            this.FineRootN += other.FineRootN * weight;
            this.CoarseRootC += other.CoarseRootC * weight;
            this.CoarseRootN += other.CoarseRootN * weight;
            this.RegenerationC += other.RegenerationC * weight;
            this.RegenerationN += other.RegenerationN * weight;
        }
    }
}
