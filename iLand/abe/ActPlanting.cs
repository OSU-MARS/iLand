using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class ActPlanting
        @ingroup abe
        The ActPlanting class implements artificial regeneration (i.e., planting of trees).
        */
    internal class ActPlanting : Activity
    {
        // Planting patterns
        public static List<MutableTuple<string, int>> planting_patterns = new List<MutableTuple<string, int>>() {
            new MutableTuple<string, int>("11" +
                                          "11", 2),
            new MutableTuple<string, int>("11111" +
                                          "11111" +
                                          "11111" +
                                          "11111" +
                                          "11111", 5),
            new MutableTuple<string, int>("1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111" +
                                          "1111111111", 10),
            new MutableTuple<string, int>("00110" +
                                          "11110" +
                                          "11111" +
                                          "01111" +
                                          "00110", 5),
            new MutableTuple<string, int>("0000110000" +
                                          "0011111100" +
                                          "0111111110" +
                                          "0111111110" +
                                          "1111111111" +
                                          "1111111111" +
                                          "0111111110" +
                                          "0011111110" +
                                          "0011111100" +
                                          "0000110000", 10) };
        public static List<string> planting_pattern_names = new List<string>() { "rect2", "rect10", "rect20", "circle5", "circle10" };
        private new static List<string> mAllowedProperties;

        private List<SPlantingItem> mItems;
        private bool mRequireLoading;

        public string type() { return "planting"; }

        public ActPlanting(FMSTP parent)
            : base(parent)
        {
            //mBaseActivity.setIsScheduled(true); // use the scheduler
            //mBaseActivity.setDoSimulate(true); // simulate per default
            if (mAllowedProperties.Count == 0)
            {
                mAllowedProperties = new List<string>(Activity.mAllowedProperties);
                mAllowedProperties.AddRange(new string[] { "species", "fraction", "height", "age", "clear",
                                                           "pattern", "spacing", "offset", "random", "n" });
            }
        }

        public new void setup(QJSValue value)
        {
            // check if regeneration is enabled
            if (GlobalSettings.instance().model().settings().regenerationEnabled == false)
            {
                throw new NotSupportedException("Cannot set up planting acitivities when iLand regeneration module is disabled.");
            }
            base.setup(value); // setup base events

            QJSValue items = FMSTP.valueFromJs(value, "items");
            mItems.Clear();
            // iterate over array or over single object
            if ((items.isArray() || items.isObject()) && !items.isCallable())
            {
                QJSValueIterator it = new QJSValueIterator(items);
                while (it.hasNext())
                {
                    it.next();
                    if (it.name() == "length")
                    {
                        continue;
                    }
                    SPlantingItem item = new SPlantingItem();
                    mItems.Add(item);
                    Debug.WriteLine(it.name() + ": " + FomeScript.JStoString(it.value()));
                    FMSTP.checkObjectProperties(it.value(), mAllowedProperties, "setup of planting activity:" + name() + "; " + it.name());

                    item.setup(it.value());
                }
            }
            else
            {
                SPlantingItem item = new SPlantingItem();
                mItems.Add(item);
                FMSTP.checkObjectProperties(items, mAllowedProperties, "setup of planting activity:" + name());

                item.setup(items);
            }
            mRequireLoading = false;
            foreach (SPlantingItem it in mItems)
            {
                if (it.clear == true)
                {
                    mRequireLoading = true;
                    break;
                }
            }
        }

        public override bool execute(FMStand stand)
        {
            Debug.WriteLine(stand.context() + " execute of planting activity....");
            using DebugTimer time = new DebugTimer("ABE:ActPlanting:execute");

            for (int s = 0; s < mItems.Count; ++s)
            {
                mItems[s].run(stand);
            }

            return true;
        }

        public override List<string> info()
        {
            List<string> lines = base.info();
            foreach (SPlantingItem item in mItems) 
            {
                lines.Add("-");
                lines.Add(String.Format("species: {0}", item.species.id()));
                lines.Add(String.Format("fraction: {0}", item.fraction));
                lines.Add(String.Format("clear: {0}", item.clear));
                lines.Add(String.Format("pattern: {0}", item.group_type > -1 ? planting_pattern_names[item.group_type] : ""));
                lines.Add(String.Format("spacing: {0}", item.spacing));
                lines.Add(String.Format("offset: {0}", item.offset));
                lines.Add(String.Format("random: {0}", item.group_random_count > 0));
                lines.Add("/-");
            }
            return lines;
        }

        public static void runSinglePlantingItem(FMStand stand, QJSValue value)
        {
            if (stand == null)
            {
                return;
            }
            if (FMSTP.verbose())
            {
                Debug.WriteLine("run Single Planting Item for Stand " + stand.id());
            }
            using DebugTimer time = new DebugTimer("ABE:runSinglePlantingItem");
            SPlantingItem item = new SPlantingItem();
            item.setup(value);
            item.run(stand);
        }
    }
}
