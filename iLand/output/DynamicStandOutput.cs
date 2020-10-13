﻿using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace iLand.Output
{
    public class DynamicStandOutput : Output
    {
        private static readonly ReadOnlyCollection<string> Aggregations = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95", "sd" }.AsReadOnly();

        private readonly List<SDynamicField> mFieldList;
        private readonly Expression mRUFilter;
        private readonly Expression mTreeFilter;
        private readonly Expression mFilter;

        private struct SDynamicField
        {
            public int aggregationIndex;
            public int var_index;
            public string expression;
        };

        public DynamicStandOutput()
        {
            this.mFilter = new Expression();
            this.mFieldList = new List<SDynamicField>();
            this.mRUFilter = new Expression();
            this.mTreeFilter = new Expression();

            Name = "dynamic stand output by species/RU";
            TableName = "dynamicstand";
            Description = "Userdefined outputs for tree aggregates for each stand or species." + System.Environment.NewLine +
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
                          "Complex expression are allowed, e.g: if(dbh>50,1,0).sum (-> counts trees with dbh>50)";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            // other colums are added during setup...
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            string filter = globalSettings.Settings.GetString(".rufilter", "");
            string tree_filter = globalSettings.Settings.GetString(".treefilter", "");
            string fieldList = globalSettings.Settings.GetString(".columns", "");
            string condition = globalSettings.Settings.GetString(".condition", "");
            if (String.IsNullOrEmpty(fieldList))
            {
                return;
            }
            mRUFilter.SetExpression(filter);
            mTreeFilter.SetExpression(tree_filter);
            mFilter.SetExpression(condition);
            // clear columns
            Columns.RemoveRange(4, Columns.Count - 4);
            mFieldList.Clear();

            // setup fields
            // int pos = 0;
            TreeWrapper tw = new TreeWrapper();
            Regex rx = new Regex("([^\\.]+).(\\w+)[,\\s]*"); // two parts: before dot and after dot, and , + whitespace at the end
            MatchCollection fields = rx.Matches(fieldList);
            foreach (Match match in fields)
            {
                string field = match.Groups[1].Value; // field / expresssion
                string aggregation = match.Groups[2].Value;
                SDynamicField newField = new SDynamicField();
                mFieldList.Add(newField);
                // parse field
                if (field.Length > 0 && !field.Contains('('))
                {
                    // simple expression
                    newField.var_index = tw.GetVariableIndex(field);
                }
                else
                {
                    // complex expression
                    newField.var_index = -1;
                    newField.expression = field;
                }

                newField.aggregationIndex = DynamicStandOutput.Aggregations.IndexOf(aggregation);
                if (newField.aggregationIndex == -1)
                {
                    throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed:{1}",
                                                                  aggregation, String.Join(" ", Aggregations), System.Environment.NewLine));
                }

                string stripped_field = String.Format("{0}_{1}", field, aggregation);
                stripped_field = Regex.Replace(stripped_field, "[\\[\\]\\,\\(\\)<>=!\\s]", "_");
                stripped_field.Replace("__", "_");
                Columns.Add(new SqlColumn(stripped_field, field, OutputDatatype.Double));
            }
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (mFieldList.Count == 0)
            {
                return;
            }
            if (!mFilter.IsEmpty)
            {
                if (mFilter.Calculate(model, model.GlobalSettings.CurrentYear) != 0.0)
                {
                    return;
                }
            }

            using DebugTimer dt = model.DebugTimers.Create("DynamicStandOutput.LogYear()");

            bool perSpecies = model.GlobalSettings.Settings.GetBool("output.dynamicstand.by_species", true);
            bool per_ru = model.GlobalSettings.Settings.GetBool("output.dynamicstand.by_ru", true);

            if (per_ru)
            {
                // when looping over resource units, do it differently (old way)
                this.ExtractByResourceUnit(model, perSpecies, insertRow);
                return;
            }

            List<double> data = new List<double>(); //statistics data
            TreeWrapper tw = new TreeWrapper();
            Expression custom_expr = new Expression();

            StatData stat = new StatData(); // statistcs helper class
            // grouping
            List<Tree> trees = new List<Tree>();
            for (int index = 0; index < model.SpeciesSet().ActiveSpecies.Count; ++index)
            {
                Species species = model.SpeciesSet().ActiveSpecies[index];
                trees.Clear();
                AllTreeIterator all_trees = new AllTreeIterator(model);
                for (Tree t = all_trees.MoveNextLiving(); t != null; t = all_trees.MoveNextLiving())
                {
                    if (perSpecies && t.Species != species)
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
                        custom_expr.SetExpression(field.expression);
                        custom_expr.Wrapper = tw;
                    }

                    // fetch data values from the trees
                    data.Clear();
                    foreach (Tree t in trees)
                    {
                        tw.Tree = t;
                    }
                    if (field.var_index >= 0)
                    {
                        data.Add(tw.Value(field.var_index, model.GlobalSettings));
                    }
                    else
                    {
                        data.Add(custom_expr.Execute(model));
                    }
                    // constant values (if not already present)
                    if (IsRowEmpty())
                    {
                        this.Add(model.GlobalSettings.CurrentYear);
                        this.Add(-1);
                        this.Add(-1);
                        if (perSpecies)
                        {
                            this.Add(species.ID);
                        }
                        else
                        {
                            this.Add("");
                        }
                    }

                    // calculate statistics
                    stat.SetData(data);
                    double value = field.aggregationIndex switch
                    {
                        0 => stat.Mean,
                        1 => stat.Sum,
                        2 => stat.Min,
                        3 => stat.Max,
                        4 => stat.Percentile25(),
                        5 => stat.Median(),
                        6 => stat.Percentile75(),
                        7 => stat.Percentile(5),
                        8 => stat.Percentile(10),
                        9 => stat.Percentile(90),
                        10 => stat.Percentile(95),
                        11 => stat.StandardDev(),
                        _ => 0.0,
                    };
                    // add current value to output
                    this.Add(value);
                }

                if (!IsRowEmpty())
                {
                    this.WriteRow(insertRow);
                }

                if (!perSpecies)
                {
                    break;
                }
            }
        }

        private void ExtractByResourceUnit(Model model, bool bySpecies, SqliteCommand insertRow)
        {
            if (mFieldList.Count == 0)
            {
                return;
            }

            List<double> data = new List<double>(); //statistics data
            StatData stat = new StatData(); // statistcs helper class
            TreeWrapper tw = new TreeWrapper();
            RUWrapper ruwrapper = new RUWrapper();
            mRUFilter.Wrapper = ruwrapper;

            Expression custom_expr = new Expression();
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }

                // test filter
                if (!mRUFilter.IsEmpty)
                {
                    ruwrapper.ResourceUnit = ru;
                    if (mRUFilter.Execute(model) == 0.0)
                    {
                        continue;
                    }
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    if (bySpecies && rus.Statistics.Count == 0)
                    {
                        continue;
                    }

                    // dynamic calculations
                    foreach (SDynamicField field in mFieldList)
                    {

                        if (String.IsNullOrEmpty(field.expression) == false)
                        {
                            // setup dynamic dynamic expression if present
                            custom_expr.SetExpression(field.expression);
                            custom_expr.Wrapper = tw;
                        }
                        data.Clear();
                        bool has_trees = false;
                        foreach (Tree tree in ru.Trees)
                        {
                            if (bySpecies && tree.Species.Index != rus.Species.Index)
                            {
                                continue;
                            }
                            if (tree.IsDead())
                            {
                                continue;
                            }
                            tw.Tree = tree;

                            // apply treefilter
                            if (!mTreeFilter.IsEmpty)
                            {
                                mTreeFilter.Wrapper = tw;
                                if (mTreeFilter.Execute(model) == 0.0)
                                {
                                    continue;
                                }
                            }
                            has_trees = true;

                            if (field.var_index >= 0)
                            {
                                data.Add(tw.Value(field.var_index, model.GlobalSettings));
                            }
                            else
                            {
                                data.Add(custom_expr.Execute(model));
                            }
                        }

                        // do nothing if no trees are avaiable
                        if (!has_trees)
                        {
                            continue;
                        }


                        if (IsRowEmpty())
                        {
                            this.Add(model.GlobalSettings.CurrentYear);
                            this.Add(ru.Index);
                            this.Add(ru.ID);
                            if (bySpecies)
                            {
                                this.Add(rus.Species.ID);
                            }
                            else
                            {
                                this.Add("");
                            }
                        }

                        // calculate statistics
                        stat.SetData(data);
                        double value = field.aggregationIndex switch
                        {
                            0 => stat.Mean,
                            1 => stat.Sum,
                            2 => stat.Min,
                            3 => stat.Max,
                            4 => stat.Percentile25(),
                            5 => stat.Median(),
                            6 => stat.Percentile75(),
                            7 => stat.Percentile(5),
                            8 => stat.Percentile(10),
                            9 => stat.Percentile(90),
                            10 => stat.Percentile(95),
                            11 => stat.StandardDev(),
                            _ => 0.0,
                        };
                        // add current value to output
                        this.Add(value);

                    } // foreach (field)
                    if (!IsRowEmpty())
                    {
                        this.WriteRow(insertRow);
                    }
                    if (!bySpecies)
                    {
                        break;
                    }
                } //foreach species
            } // foreach resource unit
        }
    }
}
