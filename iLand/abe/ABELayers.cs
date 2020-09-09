using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iLand.abe
{
    /** @class ABELayers
        @ingroup abe
        ABELayers is a helper class for spatial visualization of ABE data.

        */
    internal class ABELayers : LayeredGrid<FMStand>
    {
        private List<LayerElement> mNames;
        private Dictionary<Agent, int> mAgentIndex;
        private Dictionary<string, int> mUnitIndex;
        private Dictionary<int, int> mStandIndex;
        private Dictionary<string, int> mSTPIndex;

        public void setGrid(Grid<FMStand> grid) { mGrid = grid; }

        ~ABELayers()
        {
            GlobalSettings.instance().controller().removeLayers(this);
        }

        public double value(FMStand data, int index)
        {
            if (data == null && index < 2)
            {
                return -1; // for classes
            }
            if (data == null)
            {
                return 0;
            }
            switch (index)
            {
                case 0:
                    if (!mStandIndex.ContainsKey(data.id()))
                    {
                        mStandIndex[data.id()] = mStandIndex.Count;
                    }
                    return mStandIndex[data.id()]; // "id"
                case 1:
                    if (!mUnitIndex.ContainsKey(data.unit().id()))
                    {
                        mUnitIndex[data.unit().id()] = mUnitIndex.Count;
                    }
                    return mUnitIndex[data.unit().id()]; // unit
                case 2:
                    if (!mAgentIndex.ContainsKey(data.unit().agent()))
                    {
                        mAgentIndex[data.unit().agent()] = mAgentIndex.Count;
                    }
                    return mAgentIndex[data.unit().agent()]; // unit
                case 3: return data.volume(); // "volume"
                case 4: return data.meanAnnualIncrement(); // mean annual increment m3/ha
                case 5: return data.meanAnnualIncrementTotal(); // mean annual increment m3/ha
                case 6: return data.basalArea(); // "basalArea"
                case 7: return data.age(); // "age"
                case 8: return data.lastExecution(); // "last evaluation"
                case 9: return data.sleepYears(); // "next evaluation"
                case 10: return data.lastUpdate(); // last update
                case 11: return data.unit().constScheduler() != null ? data.unit().constScheduler().scoreOf(data.id()) : -1.0; // scheduler score
                case 12:
                    if (!mSTPIndex.ContainsKey(data.stp().name())) // stand treatment program
                    {
                        mSTPIndex[data.stp().name()] = mSTPIndex.Count();
                    }
                    return mSTPIndex[data.stp().name()];

                default: throw new NotSupportedException("ABELayers:value(): Invalid index");
            }
        }

        public override List<LayerElement> names()
        {
            if (mNames == null)
            {
                mNames = new List<LayerElement>()
                {
                    new LayerElement("id", "stand ID", GridViewType.GridViewBrewerDiv),
                    new LayerElement("unit", "ID of the management unit", GridViewType.GridViewBrewerDiv),
                    new LayerElement("agent", "managing agent", GridViewType.GridViewBrewerDiv),
                    new LayerElement("volume", "stocking volume (m3/ha)", GridViewType.GridViewRainbow),
                    new LayerElement("MAI", "mean annual increment (of the decade) (m3/ha)", GridViewType.GridViewRainbow),
                    new LayerElement("MAITotal", "mean annual increment (full rotation) (m3/ha)", GridViewType.GridViewRainbow),
                    new LayerElement("basalArea", "stocking basal area (m2/ha)", GridViewType.GridViewRainbow),
                    new LayerElement("age", "stand age", GridViewType.GridViewRainbow),
                    new LayerElement("last execution", "years since the last execution of an activity on the stand.", GridViewType.GridViewHeat),
                    new LayerElement("next evaluation", "year of the last execution", GridViewType.GridViewHeat),
                    new LayerElement("last update", "year of the last update of the forest state.", GridViewType.GridViewRainbowReverse),
                    new LayerElement("scheduler score", "score of a stand in the scheduler (higher scores: higher prob. to be executed).", GridViewType.GridViewRainbow),
                    new LayerElement("stp", "Stand treatment program currently active", GridViewType.GridViewBrewerDiv)
                };
            }
            return mNames;
        }

        public string labelvalue(int value, int index)
        {
            switch (index)
            {
                case 0: // stand id
                    return mStandIndex.Single(keyValuePair => keyValuePair.Value == value).Key.ToString();
                case 1: // unit
                    return mUnitIndex.Single(keyValuePair => keyValuePair.Value == value).Key;
                case 2: // agent
                    return mAgentIndex.Single(keyValuePair => keyValuePair.Value == value).Key.name();
                case 12: // stp
                    return mSTPIndex.Single(keyValuePair => keyValuePair.Value == value).Key;
                default:
                    return value.ToString();
            }
        }

        public void registerLayers()
        {
            GlobalSettings.instance().controller().addLayers(this, "ABE");
        }

        public void clearClasses()
        {
            mAgentIndex.Clear();
            mStandIndex.Clear();
            mUnitIndex.Clear();
        }
    }
}
