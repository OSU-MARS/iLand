using iLand.core;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class RUWrapper : ExpressionWrapper
    {
        private static readonly List<string> ruVarList;

        private ResourceUnit mRU;
        public void setResourceUnit(ResourceUnit resourceUnit) { mRU = resourceUnit; }

        static RUWrapper()
        {
            ruVarList = new List<string>(baseVarList);
            ruVarList.AddRange(new string[] { "id", "totalEffectiveArea", "nitrogenAvailable", "soilDepth", "stockedArea", "stockableArea",
                         "count", "volume", "avgDbh", "avgHeight", "basalArea", "leafAreaIndex", "aging", "cohortCount", "saplingCount", "saplingAge",
                         "canopyConductance", "soilC", "soilN", "snagC", "index", "meanTemp", "annualPrecip", "annualRad" });
        }

        public RUWrapper()
        {
            mRU = null;
        }

        public RUWrapper(ResourceUnit resourceUnit)
        {
            mRU = resourceUnit;
        }

        public override List<string> getVariablesList()
        {
            return ruVarList;
        }

        public virtual double value(int variableIndex)
        {
            Debug.Assert(mRU != null);

            switch (variableIndex - baseVarList.Count)
            {
                case 0: return mRU.id(); // id from grid
                case 1: return mRU.mEffectiveArea_perWLA;
                case 2: return mRU.mUnitVariables.nitrogenAvailable;
                case 3: return mRU.waterCycle().soilDepth();
                case 4: return mRU.stockedArea();
                case 5: return mRU.stockableArea();
                case 6: return mRU.mStatistics.count();
                case 7: return mRU.mStatistics.volume();
                case 8: return mRU.mStatistics.dbh_avg();
                case 9: return mRU.mStatistics.height_avg();
                case 10: return mRU.mStatistics.basalArea();
                case 11: return mRU.mStatistics.leafAreaIndex();
                case 12: return mRU.mAverageAging;
                case 13: return mRU.statistics().cohortCount();
                case 14: return mRU.statistics().saplingCount();
                case 15: return mRU.statistics().saplingAge();
                case 16: return mRU.waterCycle().canopyConductance();
                // soil C + soil N
                case 17:
                    if (mRU.soil() != null)
                    {
                        return mRU.soil().youngLabile().C + mRU.soil().youngRefractory().C + mRU.soil().oldOrganicMatter().C;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 18:
                    if (mRU.soil() != null)
                    {
                        return mRU.soil().youngLabile().N + mRU.soil().youngRefractory().N + mRU.soil().oldOrganicMatter().N;
                    }
                    else
                    {
                        return 0.0;
                    }
                // snags
                case 19:
                    if (mRU.snag() != null)
                    {
                        return mRU.snag().totalCarbon();
                    }
                    else
                    {
                        return 0.0;
                    }
                case 20: return mRU.index(); // numeric index
                case 21: return mRU.climate().meanAnnualTemperature(); // mean temperature
                case 22:
                    {
                        double psum = 0;
                        for (int i = 0; i < 12; ++i)
                        {
                            psum += mRU.climate().precipitationMonth()[i];
                        }
                        return psum;
                    }
                case 23: 
                    return mRU.climate().totalRadiation();
                default:
                    return base.value(variableIndex);
            }
        }
    }
}
