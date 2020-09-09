using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace iLand.output
{
    internal class DynamicStandOut : Output
    {
        private static List<string> aggList = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95", "sd" };

        private Expression mRUFilter;
        private Expression mTreeFilter;
        private Expression mCondition;
        private struct SDynamicField
        {
            public int agg_index;
            public int var_index;
            public string expression;
        };
        private List<SDynamicField> mFieldList;

        public DynamicStandOut()
        {
            setName("dynamic stand output by species/RU", "dynamicstand");
            setDescription("Userdefined outputs for tree aggregates for each stand or species." + System.Environment.NewLine +
                       "Technically, each field is calculated 'live', i.e. it is looped over all trees, and eventually the statistics (percentiles) " +
                       "are calculated. The aggregated values are not scaled to any area unit." + System.Environment.NewLine +
                       "!!!Specifying the aggregation" + System.Environment.NewLine +
                       "The ''by_species'' and ''by_ru'' option allow to define the aggregation level. When ''by_species'' is set to ''true'', " +
                       "a row for each species will be created, otherwise all trees of all species are aggregated to one row. " +
                       "Similarly, ''by_ru''=''true'' means outputs for each resource unit, while a value of ''false'' aggregates over the full project area." + System.Environment.NewLine +
                       "!!!Specifying filters" + System.Environment.NewLine +
                       "You can use the 'rufilter' and 'treefilter' XML settings to reduce the limit the output to a subset of resource units / trees. " +
                       "Both filters are valid expressions (for resource unit level and tree level, respectively). For example, a ''treefilter'' of 'speciesindex=0' reduces the output to just one species.\n" +
                       "The ''condition'' filter is (when present) evaluated and the output is only executed when ''condition'' is true (variable='year') This can be used to constrain the output to specific years (e.g. 'in(year,100,200,300)' produces output only for the given year." + System.Environment.NewLine +
                       "!!!Specifying data columns" + System.Environment.NewLine +
                       "Each field is defined as: ''field.aggregatio''n (separated by a dot). A ''field'' is a valid [Expression]. ''Aggregation'' is one of the following:  " +
                       "mean, sum, min, max, p25, p50, p75, p5, 10, p90, p95 (pXX=XXth percentile), sd (std.dev.)." + System.Environment.NewLine +
                       "Complex expression are allowed, e.g: if(dbh>50,1,0).sum (-> counts trees with dbh>50)");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            // other colums are added during setup...
        }

        public void setup()
        {
            string filter = settings().value(".rufilter", "");
            string tree_filter = settings().value(".treefilter", "");
            string fieldList = settings().value(".columns", "");
            string condition = settings().value(".condition", "");
            if (String.IsNullOrEmpty(fieldList))
            {
                return;
            }
            mRUFilter.setExpression(filter);
            mTreeFilter.setExpression(tree_filter);
            mCondition.setExpression(condition);
            // clear columns
            columns().RemoveRange(4, columns().Count - 4);
            mFieldList.Clear();

            // setup fields
            int pos = 0;
            string field, aggregation;
            TreeWrapper tw = new TreeWrapper();
            Regex rx = new Regex("([^\\.]+).(\\w+)[,\\s]*"); // two parts: before dot and after dot, and , + whitespace at the end
            MatchCollection fields = rx.Matches(fieldList);
            foreach(Match match in fields)
            {
                field = match.Captures[0].Value; // field / expresssion
                aggregation = match.Captures[1].Value;
                SDynamicField newField = new SDynamicField();
                mFieldList.Add(newField);
                // parse field
                if (field.Length > 0 && !field.Contains('('))
                {
                    // simple expression
                    newField.var_index = tw.variableIndex(field);
                }
                else
                {
                    // complex expression
                    newField.var_index = -1;
                    newField.expression = field;
                }

                newField.agg_index = aggList.IndexOf(aggregation);
                if (newField.agg_index == -1)
                {
                    throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed:{1}",
                                                 aggregation, String.Join(" ", aggList), System.Environment.NewLine));
                }

                string stripped_field = String.Format("{0}_{1}", field, aggregation);
                stripped_field = Regex.Replace(stripped_field, "[\\[\\]\\,\\(\\)<>=!\\s]", "_");
                stripped_field.Replace("__", "_");
                columns().Add(new OutputColumn(stripped_field, field, OutputDatatype.OutDouble));
            }
        }

        public override void exec()
        {
            if (mFieldList.Count == 0)
            {
                return;
            }
            if (!mCondition.isEmpty())
            {
                if (mCondition.calculate(GlobalSettings.instance().currentYear()) != 0.0)
                {
                    return;
                }
            }

            using DebugTimer dt = new DebugTimer("dynamic stand output");

            bool per_species = GlobalSettings.instance().settings().valueBool("output.dynamicstand.by_species", true);
            bool per_ru = GlobalSettings.instance().settings().valueBool("output.dynamicstand.by_ru", true);

            if (per_ru)
            {
                // when looping over resource units, do it differently (old way)
                extractByResourceUnit(per_species);
                return;
            }

            Model m = GlobalSettings.instance().model();
            List<double> data = new List<double>(); //statistics data
            TreeWrapper tw = new TreeWrapper();
            Expression custom_expr = new Expression();

            StatData stat = new StatData(); // statistcs helper class
            // grouping
            List<Tree> trees = new List<Tree>();
            for (int index = 0; index < m.speciesSet().activeSpecies().Count; ++index)
            {
                Species species = m.speciesSet().activeSpecies()[index];
                trees.Clear();
                AllTreeIterator all_trees = new AllTreeIterator(m);
                for (Tree t = all_trees.nextLiving(); t != null; t = all_trees.nextLiving())
                {
                    if (per_species && t.species() != species)
                    {
                        continue;
                    }
                    trees.Add(t);
                }
                if (trees.Count == 0)
                {
                    continue;
                }

                // dynamic calculations
                foreach (SDynamicField field in mFieldList)
                {
                    if (String.IsNullOrEmpty(field.expression) == false)
                    {
                        // setup dynamic dynamic expression if present
                        custom_expr.setExpression(field.expression);
                        custom_expr.setModelObject(tw);
                    }

                    // fetch data values from the trees
                    data.Clear();
                    foreach (Tree t in trees)
                    {
                        tw.setTree(t);
                    }
                    if (field.var_index >= 0)
                    {
                        data.Add(tw.value(field.var_index));
                    }
                    else
                    {
                        data.Add(custom_expr.execute());
                    }
                    // constant values (if not already present)
                    if (isRowEmpty())
                    {
                        this.add(currentYear());
                        this.add(-1);
                        this.add(-1);
                        if (per_species)
                        {
                            this.add(species.id());
                        }
                        else
                        {
                            this.add("");
                        }
                    }

                    // calculate statistics
                    stat.setData(data);
                    // aggregate
                    double value;
                    switch (field.agg_index)
                    {
                        case 0: value = stat.mean(); break;
                        case 1: value = stat.sum(); break;
                        case 2: value = stat.min(); break;
                        case 3: value = stat.max(); break;
                        case 4: value = stat.percentile25(); break;
                        case 5: value = stat.median(); break;
                        case 6: value = stat.percentile75(); break;
                        case 7: value = stat.percentile(5); break;
                        case 8: value = stat.percentile(10); break;
                        case 9: value = stat.percentile(90); break;
                        case 10: value = stat.percentile(95); break;
                        case 11: value = stat.standardDev(); break;

                        default: value = 0.0; break;
                    }
                    // add current value to output
                    this.add(value);
                }

                if (!isRowEmpty())
                {
                    writeRow();
                }

                if (!per_species)
                {
                    break;
                }
            }
        }


        private void extractByResourceUnit(bool by_species)
        {
            if (mFieldList.Count == 0)
            {
                return;
            }

            Model m = GlobalSettings.instance().model();
            List<double> data = new List<double>(); //statistics data
            StatData stat = new StatData(); // statistcs helper class
            TreeWrapper tw = new TreeWrapper();
            RUWrapper ruwrapper = new RUWrapper();
            mRUFilter.setModelObject(ruwrapper);

            Expression custom_expr = new Expression();
            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }

                // test filter
                if (!mRUFilter.isEmpty())
                {
                    ruwrapper.setResourceUnit(ru);
                    if (mRUFilter.execute() == 0.0)
                    {
                        continue;
                    }
                }
                foreach (ResourceUnitSpecies rus in ru.ruSpecies())
                {
                    if (by_species && rus.constStatistics().count() == 0)
                    {
                        continue;
                    }

                    // dynamic calculations
                    foreach (SDynamicField field in mFieldList)
                    {

                        if (String.IsNullOrEmpty(field.expression) == false)
                        {
                            // setup dynamic dynamic expression if present
                            custom_expr.setExpression(field.expression);
                            custom_expr.setModelObject(tw);
                        }
                        data.Clear();
                        bool has_trees = false;
                        foreach (Tree tree in ru.trees())
                        {
                            if (by_species && tree.species().index() != rus.species().index())
                            {
                                continue;
                            }
                            if (tree.isDead())
                            {
                                continue;
                            }
                            tw.setTree(tree);

                            // apply treefilter
                            if (!mTreeFilter.isEmpty())
                            {
                                mTreeFilter.setModelObject(tw);
                                if (mTreeFilter.execute() == 0.0)
                                {
                                    continue;
                                }
                            }
                            has_trees = true;

                            if (field.var_index >= 0)
                            {
                                data.Add(tw.value(field.var_index));
                            }
                            else
                            {
                                data.Add(custom_expr.execute());
                            }
                        }

                        // do nothing if no trees are avaiable
                        if (!has_trees)
                        {
                            continue;
                        }


                        if (isRowEmpty())
                        {
                            this.add(currentYear());
                            this.add(ru.index());
                            this.add(ru.id());
                            if (by_species)
                            {
                                this.add(rus.species().id());
                            }
                            else
                            {
                                this.add("");
                            }
                        }

                        // calculate statistics
                        stat.setData(data);
                        // aggregate
                        double value;
                        switch (field.agg_index)
                        {
                            case 0: value = stat.mean(); break;
                            case 1: value = stat.sum(); break;
                            case 2: value = stat.min(); break;
                            case 3: value = stat.max(); break;
                            case 4: value = stat.percentile25(); break;
                            case 5: value = stat.median(); break;
                            case 6: value = stat.percentile75(); break;
                            case 7: value = stat.percentile(5); break;
                            case 8: value = stat.percentile(10); break;
                            case 9: value = stat.percentile(90); break;
                            case 10: value = stat.percentile(95); break;
                            case 11: value = stat.standardDev(); break;

                            default: value = 0.0; break;
                        }
                        // add current value to output
                        this.add(value);

                    } // foreach (field)
                    if (!isRowEmpty())
                    {
                        writeRow();
                    }
                    if (!by_species)
                    {
                        break;
                    }
                } //foreach species
            } // foreach resource unit
        }
    }
}
