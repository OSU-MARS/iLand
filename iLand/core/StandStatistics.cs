namespace iLand.core
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
    internal class StandStatistics
    {
        private ResourceUnitSpecies mRUS; ///< link to the resource unit species
        private double mCount;
        private double mSumDbh;
        private double mSumHeight;
        private double mSumBasalArea;
        private double mSumVolume;
        private double mGWL;
        private double mAverageDbh;
        private double mAverageHeight;
        private double mLeafAreaIndex;
        private double mNPP;
        private double mNPPabove;
        private double mNPPsaplings; // carbon gain of saplings
                                     // regeneration layer
        private int mCohortCount; ///< number of cohrots
        private int mSaplingCount; ///< number of sapling (Reinekes Law)
        private double mSumSaplingAge;
        private double mAverageSaplingAge;
        // carbon and nitrogen pools
        private double mCStem, mCFoliage, mCBranch, mCCoarseRoot, mCFineRoot;
        private double mNStem, mNFoliage, mNBranch, mNCoarseRoot, mNFineRoot;
        private double mCRegeneration, mNRegeneration;

        // getters
        public double count() { return mCount; }
        public double dbh_avg() { return mAverageDbh; } ///< average dbh (cm)
        public double height_avg() { return mAverageHeight; } ///< average tree height (m)
        public double volume() { return mSumVolume; } ///< sum of tree volume (m3/ha)
        public double gwl() { return mGWL; } ///< total increment (m3/ha)
        public double basalArea() { return mSumBasalArea; } ///< sum of basal area of all trees (m2/ha)
        public double leafAreaIndex() { return mLeafAreaIndex; } ///< [m2/m2]/ha stocked area.
        public double npp() { return mNPP; } ///< sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public double nppAbove() { return mNPPabove; } ///< above ground NPP (kg Biomass increment)/ha
        public double nppSaplings() { return mNPPsaplings; } ///< carbon gain of saplings (kg Biomass increment)/ha
        public int cohortCount() { return mCohortCount; } ///< number of cohorts of saplings / ha
        public int saplingCount() { return mSaplingCount; } ///< number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha
        public double saplingAge() { return mAverageSaplingAge; } ///< average age of sapling (currenty not weighted with represented sapling numbers...)
        // carbon/nitrogen cycle
        public double cStem() { return mCStem; }
        public double nStem() { return mNStem; }
        public double cBranch() { return mCBranch; }
        public double nBranch() { return mNBranch; }
        public double cFoliage() { return mCFoliage; }
        public double nFoliage() { return mNFoliage; }
        public double cCoarseRoot() { return mCCoarseRoot; }
        public double nCoarseRoot() { return mNCoarseRoot; }
        public double cFineRoot() { return mCFineRoot; }
        public double nFineRoot() { return mNFineRoot; }
        public double cRegeneration() { return mCRegeneration; }
        public double nRegeneration() { return mNRegeneration; }
        /// total carbon stock: sum of carbon of all living trees + regeneration layer
        public double totalCarbon() { return mCStem + mCBranch + mCFoliage + mCFineRoot + mCCoarseRoot + mCRegeneration; }

        public StandStatistics() { mRUS = null; clear(); }
        public void setResourceUnitSpecies(ResourceUnitSpecies rus) { mRUS = rus; }

        public void clear()
        {
            // reset all values
            mCount = 0;
            mSumDbh = mSumHeight = mAverageDbh = mAverageHeight = 0.0;
            mSumBasalArea = mSumVolume = mGWL = 0.0;
            mLeafAreaIndex = 0.0;
            mNPP = mNPPabove = 0.0;
            mNPPsaplings = 0.0;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0;
            mSumSaplingAge = 0.0;
            mCStem = 0.0;
            mCFoliage = 0.0; mCBranch = 0.0; mCCoarseRoot = 0.0; mCFineRoot = 0.0;
            mNStem = 0.0; mNFoliage = 0.0; mNBranch = 0.0; mNCoarseRoot = 0.0; mNFineRoot = 0.0;
            mCRegeneration = 0.0; mNRegeneration = 0.0;
        }

        public void clearOnlyTrees()
        {
            // reset only those values that are directly accumulated from trees
            mCount = 0;
            mSumDbh = mSumHeight = mAverageDbh = mAverageHeight = 0.0;
            mSumBasalArea = mSumVolume = mGWL = 0.0;
            mLeafAreaIndex = 0.0;
            /*mNPP = mNPPabove = 0.0;
            mNPPsaplings = 0.0;
            mCohortCount = mSaplingCount = 0;
            mAverageSaplingAge = 0.0;
            mSumSaplingAge = 0.0;*/
            mCStem = 0.0; mCFoliage = 0.0; mCBranch = 0.0; mCCoarseRoot = 0.0; mCFineRoot = 0.0;
            mNStem = 0.0; mNFoliage = 0.0; mNBranch = 0.0; mNCoarseRoot = 0.0; mNFineRoot = 0.0;
            /*mCRegeneration=0.0; mNRegeneration=0.0;*/
        }

        public void addBiomass(double biomass, double CNRatio, ref double C, ref double N)
        {
            C += biomass * Constant.biomassCFraction;
            N += biomass * Constant.biomassCFraction / CNRatio;
        }

        public void add(Tree tree, TreeGrowthData tgd)
        {
            mCount++;
            mSumDbh += tree.dbh();
            mSumHeight += tree.height();
            mSumBasalArea += tree.basalArea();
            mSumVolume += tree.volume();
            mLeafAreaIndex += tree.leafArea(); // warning: sum of leafarea!
            if (tgd != null)
            {
                mNPP += tgd.NPP;
                mNPPabove += tgd.NPP_above;
            }
            // carbon and nitrogen pools
            addBiomass(tree.biomassStem(), tree.species().cnWood(), ref mCStem, ref mNStem);
            addBiomass(tree.biomassBranch(), tree.species().cnWood(), ref mCBranch, ref mNBranch);
            addBiomass(tree.biomassFoliage(), tree.species().cnFoliage(), ref mCFoliage, ref mNFoliage);
            addBiomass(tree.biomassFineRoot(), tree.species().cnFineroot(), ref mCFineRoot, ref mNFineRoot);
            addBiomass(tree.biomassCoarseRoot(), tree.species().cnWood(), ref mCCoarseRoot, ref mNCoarseRoot);
        }

        // note: mRUS = 0 for aggregated statistics
        public void calculate()
        {
            if (mCount > 0.0)
            {
                mAverageDbh = mSumDbh / mCount;
                mAverageHeight = mSumHeight / mCount;
                if (mRUS != null && mRUS.ru().stockableArea() > 0.0)
                {
                    mLeafAreaIndex /= mRUS.ru().stockableArea(); // convert from leafarea to LAI
                }
            }
            if (mCohortCount != 0)
            {
                mAverageSaplingAge = mSumSaplingAge / (double)mCohortCount;
            }

            // scale values to per hectare if resource unit <> 1ha
            // note: do this only on species-level (avoid double scaling)
            if (mRUS != null)
            {
                double area_factor = Constant.cRUArea / mRUS.ru().stockableArea();
                if (area_factor != 1.0)
                {
                    mCount = mCount * area_factor;
                    mSumBasalArea *= area_factor;
                    mSumVolume *= area_factor;
                    mSumDbh *= area_factor;
                    mNPP *= area_factor;
                    mNPPabove *= area_factor;
                    mNPPsaplings *= area_factor;
                    //mGWL *= area_factor;
                    mCohortCount = (int)(area_factor * mCohortCount); // BUGBUG: quantization?
                    mSaplingCount = (int)(area_factor * mSaplingCount); // BUGBUG: quantization?
                    //double mCStem, mCFoliage, mCBranch, mCCoarseRoot, mCFineRoot;
                    //double mNStem, mNFoliage, mNBranch, mNCoarseRoot, mNFineRoot;
                    //double mCRegeneration, mNRegeneration;
                    mCStem *= area_factor; 
                    mNStem *= area_factor;
                    mCFoliage *= area_factor; 
                    mNFoliage *= area_factor;
                    mCBranch *= area_factor; 
                    mNBranch *= area_factor;
                    mCCoarseRoot *= area_factor; 
                    mNCoarseRoot *= area_factor;
                    mCFineRoot *= area_factor; 
                    mNFineRoot *= area_factor;
                    mCRegeneration *= area_factor; 
                    mNRegeneration *= area_factor;
                }
                mGWL = mSumVolume + mRUS.removedVolume(); // removedVolume: per ha, SumVolume now too
            }
        }

        public void add(StandStatistics stat)
        {
            mCount += stat.mCount;
            mSumBasalArea += stat.mSumBasalArea;
            mSumDbh += stat.mSumDbh;
            mSumHeight += stat.mSumHeight;
            mSumVolume += stat.mSumVolume;
            mLeafAreaIndex += stat.mLeafAreaIndex;
            mNPP += stat.mNPP;
            mNPPabove += stat.mNPPabove;
            mNPPsaplings += stat.mNPPsaplings;
            mGWL += stat.mGWL;
            // regeneration
            mCohortCount += stat.mCohortCount;
            mSaplingCount += stat.mSaplingCount;
            mSumSaplingAge += stat.mSumSaplingAge;
            // carbon/nitrogen pools
            mCStem += stat.mCStem; mNStem += stat.mNStem;
            mCBranch += stat.mCBranch; mNBranch += stat.mNBranch;
            mCFoliage += stat.mCFoliage; mNFoliage += stat.mNFoliage;
            mCFineRoot += stat.mCFineRoot; mNFineRoot += stat.mNFineRoot;
            mCCoarseRoot += stat.mCCoarseRoot; mNCoarseRoot += stat.mNCoarseRoot;
            mCRegeneration += stat.mCRegeneration; mNRegeneration += stat.mNRegeneration;
        }

        public void addAreaWeighted(StandStatistics stat, double weight)
        {
            // aggregates that are not scaled to hectares
            mCount += stat.mCount * weight;
            mSumBasalArea += stat.mSumBasalArea * weight;
            mSumDbh += stat.mSumDbh * weight;
            mSumHeight += stat.mSumHeight * weight;
            mSumVolume += stat.mSumVolume * weight;
            // averages that are scaled to per hectare need to be scaled
            mAverageDbh += stat.mAverageDbh * weight;
            mAverageHeight += stat.mAverageHeight * weight;
            mAverageSaplingAge += stat.mAverageSaplingAge * weight;
            mLeafAreaIndex += stat.mLeafAreaIndex * weight;

            mNPP += stat.mNPP * weight;
            mNPPabove += stat.mNPPabove * weight;
            mNPPsaplings += stat.mNPPsaplings * weight;
            mGWL += stat.mGWL * weight;
            // regeneration
            mCohortCount += (int)(stat.mCohortCount * weight); // BUGBUG: quantization?
            mSaplingCount += (int)(stat.mSaplingCount * weight); // BUGBUG: quantization?
            mSumSaplingAge += stat.mSumSaplingAge * weight;
            // carbon/nitrogen pools
            mCStem += stat.mCStem * weight; mNStem += stat.mNStem * weight;
            mCBranch += stat.mCBranch * weight; mNBranch += stat.mNBranch * weight;
            mCFoliage += stat.mCFoliage * weight; mNFoliage += stat.mNFoliage * weight;
            mCFineRoot += stat.mCFineRoot * weight; mNFineRoot += stat.mNFineRoot * weight;
            mCCoarseRoot += stat.mCCoarseRoot * weight; mNCoarseRoot += stat.mNCoarseRoot * weight;
            mCRegeneration += stat.mCRegeneration * weight; mNRegeneration += stat.mNRegeneration * weight;
        }

        public void add(SaplingStat sapling)
        {
            mCohortCount += sapling.livingCohorts();
            mSaplingCount += (int)sapling.livingSaplings(); // saplings with height >1.3m

            mSumSaplingAge += sapling.averageAge() * sapling.livingCohorts();

            mCRegeneration += sapling.carbonLiving().C;
            mNRegeneration += sapling.carbonLiving().N;

            mNPPsaplings += sapling.carbonGain().C / Constant.biomassCFraction;
        }
    }
}
