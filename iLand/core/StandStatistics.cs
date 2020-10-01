namespace iLand.Core
{
    /** @class StandStatistics
      @ingroup tools
      Collects information on stand level for each tree species.
      Call clear() to clear the statistics, then call add() for each tree and finally calculate().
      To aggregate on a higher level, use add() for each StandStatistics object to include, and then
      calculate() on the higher level.
      Todo-List for new items:
      - add a member variable and a getter
      - add to "add(Tree)" and "calculate()"
      - add to "add(StandStatistics)" as well!
      */
    public class StandStatistics
    {
        private double mSumDbh;
        private double mSumHeight;
        private double mSumBasalArea;
        private double mSumVolume;
        private double mSumSaplingAge;

        public double AverageDbh { get; private set; } ///< average dbh (cm)
        public double AverageHeight { get; private set; } ///< average tree height (m)
        public double BasalArea { get; private set; } ///< sum of basal area of all trees (m2/ha)
        public int CohortCount { get; private set; } ///< number of cohorts of saplings / ha
        public double Count { get; private set; }
        public double Gwl { get; private set; } ///< total increment (m3/ha)
        public double LeafAreaIndex { get; private set; } ///< [m2/m2]/ha stocked area.
        public double MeanSaplingAge { get; private set; } ///< average age of sapling (currenty not weighted with represented sapling numbers...)
        public double Npp { get; private set; } ///< sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public double NppAbove { get; private set; } ///< above ground NPP (kg Biomass increment)/ha
        public double NppSaplings { get; private set; } ///< carbon gain of saplings (kg Biomass increment)/ha
        public ResourceUnitSpecies ResourceUnitSpecies { get; set; }
        ///< number of sapling (Reinekes Law)
        public int SaplingCount { get; private set; } ///< number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha
        public double Volume { get; private set; } ///< sum of tree volume (m3/ha)
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

        public StandStatistics() 
        { 
            ResourceUnitSpecies = null; 
            Clear(); 
        }

        /// total carbon stock: sum of carbon of all living trees + regeneration layer
        public double TotalCarbon() { return StemC + BranchC + FoliageC + FineRootC + CoarseRootC + RegenerationC; }

        public void Clear()
        {
            // reset all values
            // TODO: call ClearOnlyTrees()
            Count = 0;
            mSumDbh = mSumHeight = AverageDbh = AverageHeight = 0.0;
            mSumBasalArea = mSumVolume = Gwl = 0.0;
            LeafAreaIndex = 0.0;
            Npp = NppAbove = 0.0;
            NppSaplings = 0.0;
            CohortCount = SaplingCount = 0;
            MeanSaplingAge = 0.0;
            mSumSaplingAge = 0.0;
            StemC = 0.0;
            FoliageC = 0.0; BranchC = 0.0; CoarseRootC = 0.0; FineRootC = 0.0;
            StemN = 0.0; FoliageN = 0.0; BranchN = 0.0; CoarseRootN = 0.0; FineRootN = 0.0;
            RegenerationC = 0.0; RegenerationN = 0.0;
        }

        public void ClearOnlyTrees()
        {
            // reset only those values that are directly accumulated from trees
            Count = 0;
            mSumDbh = mSumHeight = AverageDbh = AverageHeight = 0.0;
            mSumBasalArea = mSumVolume = Gwl = 0.0;
            LeafAreaIndex = 0.0;
            /*mNPP = mNPPabove = 0.0;
            mNPPsaplings = 0.0;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0;
            mSumSaplingAge = 0.0;*/
            StemC = 0.0; FoliageC = 0.0; BranchC = 0.0; CoarseRootC = 0.0; FineRootC = 0.0;
            StemN = 0.0; FoliageN = 0.0; BranchN = 0.0; CoarseRootN = 0.0; FineRootN = 0.0;
            /*mCRegeneration=0.0; mNRegeneration=0.0;*/
        }

        public void Add(Tree tree, TreeGrowthData tgd)
        {
            Count++;
            mSumDbh += tree.Dbh;
            mSumHeight += tree.Height;
            mSumBasalArea += tree.BasalArea();
            mSumVolume += tree.Volume();
            LeafAreaIndex += tree.LeafArea; // warning: sum of leafarea!
            if (tgd != null)
            {
                Npp += tgd.NppTotal;
                NppAbove += tgd.NppAboveground;
            }
            // carbon and nitrogen pools
            this.BranchC += Constant.BiomassCFraction * tree.GetBranchBiomass();
            this.BranchN += Constant.BiomassCFraction / tree.Species.CNRatioWood * tree.GetBranchBiomass();
            this.CoarseRootC += Constant.BiomassCFraction * tree.CoarseRootMass;
            this.CoarseRootN += Constant.BiomassCFraction / tree.Species.CNRatioWood * tree.CoarseRootMass;
            this.FineRootC += Constant.BiomassCFraction * tree.FineRootMass;
            this.FineRootN += Constant.BiomassCFraction / tree.Species.CNRatioFineRoot * tree.FineRootMass;
            this.FoliageC += Constant.BiomassCFraction * tree.FoliageMass;
            this.FoliageN += Constant.BiomassCFraction / tree.Species.CNRatioFineRoot * tree.FoliageMass;
            this.StemC += Constant.BiomassCFraction * tree.StemMass;
            this.StemN += Constant.BiomassCFraction / tree.Species.CNRatioWood * tree.StemMass;
        }

        // note: mRUS = 0 for aggregated statistics
        public void Calculate()
        {
            if (Count > 0.0)
            {
                AverageDbh = mSumDbh / Count;
                AverageHeight = mSumHeight / Count;
                if (ResourceUnitSpecies != null && ResourceUnitSpecies.RU.StockableArea > 0.0)
                {
                    LeafAreaIndex /= ResourceUnitSpecies.RU.StockableArea; // convert from leafarea to LAI
                }
            }
            if (CohortCount != 0)
            {
                MeanSaplingAge = mSumSaplingAge / (double)CohortCount;
            }

            // scale values to per hectare if resource unit <> 1ha
            // note: do this only on species-level (avoid double scaling)
            if (ResourceUnitSpecies != null)
            {
                double area_factor = Constant.RUArea / ResourceUnitSpecies.RU.StockableArea;
                if (area_factor != 1.0)
                {
                    Count *= area_factor;
                    mSumBasalArea *= area_factor;
                    mSumVolume *= area_factor;
                    mSumDbh *= area_factor;
                    Npp *= area_factor;
                    NppAbove *= area_factor;
                    NppSaplings *= area_factor;
                    //mGWL *= area_factor;
                    CohortCount = (int)(area_factor * CohortCount); // BUGBUG: quantization?
                    SaplingCount = (int)(area_factor * SaplingCount); // BUGBUG: quantization?
                    //double mCStem, mCFoliage, mCBranch, mCCoarseRoot, mCFineRoot;
                    //double mNStem, mNFoliage, mNBranch, mNCoarseRoot, mNFineRoot;
                    //double mCRegeneration, mNRegeneration;
                    StemC *= area_factor; 
                    StemN *= area_factor;
                    FoliageC *= area_factor; 
                    FoliageN *= area_factor;
                    BranchC *= area_factor; 
                    BranchN *= area_factor;
                    CoarseRootC *= area_factor; 
                    CoarseRootN *= area_factor;
                    FineRootC *= area_factor; 
                    FineRootN *= area_factor;
                    RegenerationC *= area_factor; 
                    RegenerationN *= area_factor;
                }
                Gwl = mSumVolume + ResourceUnitSpecies.RemovedVolume; // removedVolume: per ha, SumVolume now too
            }
        }

        public void Add(StandStatistics stat)
        {
            Count += stat.Count;
            mSumBasalArea += stat.mSumBasalArea;
            mSumDbh += stat.mSumDbh;
            mSumHeight += stat.mSumHeight;
            mSumVolume += stat.mSumVolume;
            LeafAreaIndex += stat.LeafAreaIndex;
            Npp += stat.Npp;
            NppAbove += stat.NppAbove;
            NppSaplings += stat.NppSaplings;
            Gwl += stat.Gwl;
            // regeneration
            CohortCount += stat.CohortCount;
            SaplingCount += stat.SaplingCount;
            mSumSaplingAge += stat.mSumSaplingAge;
            // carbon/nitrogen pools
            StemC += stat.StemC; StemN += stat.StemN;
            BranchC += stat.BranchC; BranchN += stat.BranchN;
            FoliageC += stat.FoliageC; FoliageN += stat.FoliageN;
            FineRootC += stat.FineRootC; FineRootN += stat.FineRootN;
            CoarseRootC += stat.CoarseRootC; CoarseRootN += stat.CoarseRootN;
            RegenerationC += stat.RegenerationC; RegenerationN += stat.RegenerationN;
        }

        public void AddAreaWeighted(StandStatistics stat, double weight)
        {
            // aggregates that are not scaled to hectares
            Count += stat.Count * weight;
            mSumBasalArea += stat.mSumBasalArea * weight;
            mSumDbh += stat.mSumDbh * weight;
            mSumHeight += stat.mSumHeight * weight;
            mSumVolume += stat.mSumVolume * weight;
            // averages that are scaled to per hectare need to be scaled
            AverageDbh += stat.AverageDbh * weight;
            AverageHeight += stat.AverageHeight * weight;
            MeanSaplingAge += stat.MeanSaplingAge * weight;
            LeafAreaIndex += stat.LeafAreaIndex * weight;

            Npp += stat.Npp * weight;
            NppAbove += stat.NppAbove * weight;
            NppSaplings += stat.NppSaplings * weight;
            Gwl += stat.Gwl * weight;
            // regeneration
            CohortCount += (int)(stat.CohortCount * weight); // BUGBUG: quantization?
            SaplingCount += (int)(stat.SaplingCount * weight); // BUGBUG: quantization?
            mSumSaplingAge += stat.mSumSaplingAge * weight;
            // carbon/nitrogen pools
            StemC += stat.StemC * weight; StemN += stat.StemN * weight;
            BranchC += stat.BranchC * weight; BranchN += stat.BranchN * weight;
            FoliageC += stat.FoliageC * weight; FoliageN += stat.FoliageN * weight;
            FineRootC += stat.FineRootC * weight; FineRootN += stat.FineRootN * weight;
            CoarseRootC += stat.CoarseRootC * weight; CoarseRootN += stat.CoarseRootN * weight;
            RegenerationC += stat.RegenerationC * weight; RegenerationN += stat.RegenerationN * weight;
        }

        public void Add(SaplingStat sapling)
        {
            CohortCount += sapling.LivingCohorts;
            SaplingCount += (int)sapling.LivingSaplings; // saplings with height >1.3m

            mSumSaplingAge += sapling.AverageAge * sapling.LivingCohorts;

            RegenerationC += sapling.CarbonLiving.C;
            RegenerationN += sapling.CarbonLiving.N;

            NppSaplings += sapling.CarbonGain.C / Constant.BiomassCFraction;
        }
    }
}
