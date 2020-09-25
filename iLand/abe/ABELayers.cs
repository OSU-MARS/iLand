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
        private readonly Dictionary<Agent, int> mAgentIndex;
        private List<LayerElement> mNames;
        private readonly Dictionary<int, int> mStandIndex;
        private readonly Dictionary<string, int> mStpIndex;
        private readonly Dictionary<string, int> mUnitIndex;

        public void setGrid(Grid<FMStand> grid) { Grid = grid; }

        public ABELayers()
        {
            this.mAgentIndex = new Dictionary<Agent, int>();
            this.mNames = null; // lazy init
            this.mStandIndex = new Dictionary<int, int>();
            this.mStpIndex = new Dictionary<string, int>();
            this.mUnitIndex = new Dictionary<string, int>();
        }

        ~ABELayers()
        {
            GlobalSettings.Instance.ModelController.RemoveLayers(this);
        }

        public double Value(FMStand data, int index)
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
                case 11: return data.unit().constScheduler() != null ? data.unit().constScheduler().ScoreOf(data.id()) : -1.0; // scheduler score
                case 12:
                    if (!mStpIndex.ContainsKey(data.stp().name())) // stand treatment program
                    {
                        mStpIndex[data.stp().name()] = mStpIndex.Count();
                    }
                    return mStpIndex[data.stp().name()];

                default: throw new NotSupportedException("ABELayers:value(): Invalid index");
            }
        }

        public override List<LayerElement> Names()
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

        // unused in C++
        //public override string LabelValue(int value, int index)
        //{
        //    return index switch
        //    {
        //        // stand id
        //        0 => mStandIndex.Single(keyValuePair => keyValuePair.Value == value).Key.ToString(),
        //        // unit
        //        1 => mUnitIndex.Single(keyValuePair => keyValuePair.Value == value).Key,
        //        // agent
        //        2 => mAgentIndex.Single(keyValuePair => keyValuePair.Value == value).Key.name(),
        //        // stp
        //        12 => mStpIndex.Single(keyValuePair => keyValuePair.Value == value).Key,
        //        _ => value.ToString(),
        //    };
        //}

        public void RegisterLayers()
        {
            GlobalSettings.Instance.ModelController.AddLayers(this, "ABE");
        }

        public void ClearClasses()
        {
            mAgentIndex.Clear();
            mStandIndex.Clear();
            mUnitIndex.Clear();
        }
    }
}
