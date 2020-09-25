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
        public static List<MutableTuple<string, int>> planting_patterns;
        public static List<string> planting_pattern_names;
        private new static readonly List<string> mAllowedProperties;

        private readonly List<SPlantingItem> mItems;

        public override string Type() { return "planting"; }

        static ActPlanting()
        {
            ActPlanting.mAllowedProperties = new List<string>(Activity.mAllowedProperties) { "species", "fraction", "height", "age", "clear",
                                                                                             "pattern", "spacing", "offset", "random", "n" };
            ActPlanting.planting_pattern_names = new List<string>() { "rect2", "rect10", "rect20", "circle5", "circle10" };
            ActPlanting.planting_patterns = new List<MutableTuple<string, int>>() {
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
                                                "0000110000", 10)
            };
        }

        public ActPlanting()
        {
            //mBaseActivity.setIsScheduled(true); // use the scheduler
            //mBaseActivity.setDoSimulate(true); // simulate per default
            this.mItems = new List<SPlantingItem>();
        }

        public override void Setup(QJSValue value)
        {
            // check if regeneration is enabled
            if (GlobalSettings.Instance.Model.Settings.RegenerationEnabled == false)
            {
                throw new NotSupportedException("Cannot set up planting acitivities when iLand regeneration module is disabled.");
            }
            base.Setup(value); // setup base events

            QJSValue items = FMSTP.ValueFromJS(value, "items");
            mItems.Clear();
            // iterate over array or over single object
            if ((items.IsArray() || items.IsObject()) && !items.IsCallable())
            {
                QJSValueIterator it = new QJSValueIterator(items);
                while (it.HasNext())
                {
                    it.Next();
                    if (it.Name() == "length")
                    {
                        continue;
                    }
                    SPlantingItem item = new SPlantingItem();
                    mItems.Add(item);
                    Debug.WriteLine(it.Name() + ": " + FomeScript.JStoString(it.Value()));
                    FMSTP.CheckObjectProperties(it.Value(), mAllowedProperties, "setup of planting activity:" + name() + "; " + it.Name());

                    item.Setup(it.Value());
                }
            }
            else
            {
                SPlantingItem item = new SPlantingItem();
                mItems.Add(item);
                FMSTP.CheckObjectProperties(items, mAllowedProperties, "setup of planting activity:" + name());

                item.Setup(items);
            }
        }

        public override bool Execute(FMStand stand)
        {
            Debug.WriteLine(stand.context() + " execute of planting activity....");
            using DebugTimer time = new DebugTimer("ABE:ActPlanting:execute");

            for (int s = 0; s < mItems.Count; ++s)
            {
                mItems[s].Run(stand);
            }

            return true;
        }

        public override List<string> Info()
        {
            List<string> lines = base.Info();
            foreach (SPlantingItem item in mItems) 
            {
                lines.Add("-");
                lines.Add(String.Format("species: {0}", item.species.ID));
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

        public static void RunSinglePlantingItem(FMStand stand, QJSValue value)
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
            item.Setup(value);
            item.Run(stand);
        }
    }
}
