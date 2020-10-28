using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace iLand.Output
{
    public class DynamicStandOutput : Output
    {
        private static readonly ReadOnlyCollection<string> Aggregations = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95", "sd" }.AsReadOnly();

        private readonly List<DynamicOutputField> mFieldList;
        private readonly Expression mRUfilter;
        private readonly Expression mTreeFilter;
        private readonly Expression mFilter;

        private struct DynamicOutputField
        {
            public int AggregationIndex { get; set; }
            public int VariableIndex { get; set; }
            public string Expression { get; set; }
        };

        public DynamicStandOutput()
        {
            this.mFilter = new Expression();
            this.mFieldList = new List<DynamicOutputField>();
            this.mRUfilter = new Expression();
            this.mTreeFilter = new Expression();

            this.Name = "dynamic stand output by species/RU";
            this.TableName = "dynamicstand";
            this.Description = "Userdefined outputs for tree aggregates for each stand or species." + System.Environment.NewLine +
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
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            // other colums are added during setup...
        }

        public override void Setup(Model model)
        {
            string fieldList = model.Project.Output.Dynamic.Columns;
            if (String.IsNullOrEmpty(fieldList))
            {
                return;
            }

            this.mRUfilter.SetExpression(model.Project.Output.Dynamic.ResourceUnitFilter);
            this.mTreeFilter.SetExpression(model.Project.Output.Dynamic.TreeFilter);
            this.mFilter.SetExpression(model.Project.Output.Dynamic.Condition);
            // clear columns
            this.Columns.RemoveRange(4, Columns.Count - 4);
            this.mFieldList.Clear();

            // setup fields
            // int pos = 0;
            TreeWrapper treeWrapper = new TreeWrapper();
            Regex regex = new Regex("([^\\.]+).(\\w+)[,\\s]*"); // two parts: before dot and after dot, and , + whitespace at the end
            MatchCollection fields = regex.Matches(fieldList);
            foreach (Match match in fields)
            {
                string field = match.Groups[1].Value; // field / expresssion
                string aggregation = match.Groups[2].Value;
                DynamicOutputField newField = new DynamicOutputField();
                mFieldList.Add(newField);
                // parse field
                if (field.Length > 0 && !field.Contains('('))
                {
                    // simple expression
                    newField.VariableIndex = treeWrapper.GetVariableIndex(field);
                }
                else
                {
                    // complex expression
                    newField.VariableIndex = -1;
                    newField.Expression = field;
                }

                newField.AggregationIndex = DynamicStandOutput.Aggregations.IndexOf(aggregation);
                if (newField.AggregationIndex == -1)
                {
                    throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed:{1}",
                                                                  aggregation, String.Join(" ", Aggregations), System.Environment.NewLine));
                }

                string stripped_field = String.Format("{0}_{1}", field, aggregation);
                stripped_field = Regex.Replace(stripped_field, "[\\[\\]\\,\\(\\)<>=!\\s]", "_");
                stripped_field.Replace("__", "_");
                this.Columns.Add(new SqlColumn(stripped_field, field, OutputDatatype.Double));
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
                if (mFilter.Evaluate(model, model.ModelSettings.CurrentYear) != 0.0)
                {
                    return;
                }
            }

            //using DebugTimer dt = model.DebugTimers.Create("DynamicStandOutput.LogYear()");

            bool perSpecies = model.Project.Output.DynamicStand.BySpecies;
            bool perRU = model.Project.Output.DynamicStand.ByResourceUnit;

            if (perRU)
            {
                // when looping over resource units, do it differently (old way)
                this.ExtractByResourceUnit(model, perSpecies, insertRow);
                return;
            }

            List<double> data = new List<double>(); //statistics data
            TreeWrapper treeWrapper = new TreeWrapper();
            Expression customExpression = new Expression();

            SummaryStatistics stat = new SummaryStatistics(); // statistcs helper class
            // grouping
            if (model.Environment.SpeciesSetsByTableName.Count != 1)
            {
                throw new NotImplementedException("Generation of a unique list of species from multiple species sets is not currently supported.");
            }
            TreeSpeciesSet treeSpeciesSet = model.Environment.SpeciesSetsByTableName.First().Value;
            List<Trees> liveTreesOfSpecies = new List<Trees>();
            for (int speciesSet = 0; speciesSet < treeSpeciesSet.ActiveSpecies.Count; ++speciesSet)
            {
                liveTreesOfSpecies.Clear();

                TreeSpecies species = treeSpeciesSet.ActiveSpecies[speciesSet];
                AllTreesEnumerator allTreeEnumerator = new AllTreesEnumerator(model);
                while (allTreeEnumerator.MoveNextLiving())
                {
                    if (perSpecies && allTreeEnumerator.CurrentTrees.Species != species)
                    {
                        continue;
                    }
                    liveTreesOfSpecies.Add(allTreeEnumerator.CurrentTrees);
                }
                if (liveTreesOfSpecies.Count == 0)
                {
                    continue;
                }

                // dynamic calculations
                int columnIndex = 0;
                foreach (DynamicOutputField field in mFieldList)
                {
                    if (String.IsNullOrEmpty(field.Expression) == false)
                    {
                        // setup dynamic dynamic expression if present
                        customExpression.SetExpression(field.Expression);
                        customExpression.Wrapper = treeWrapper;
                    }

                    // fetch data values from the trees
                    data.Clear();
                    foreach (Trees trees in liveTreesOfSpecies)
                    {
                        // TODO: this loop sets the wrapper to the last trees in the list without doing anything?
                        treeWrapper.Trees = trees;
                    }

                    if (field.VariableIndex >= 0)
                    {
                        data.Add(treeWrapper.GetValue(model, field.VariableIndex));
                    }
                    else
                    {
                        data.Add(customExpression.Execute(model));
                    }
                    // constant values (if not already present)
                    if (columnIndex == 0)
                    {
                        insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                        insertRow.Parameters[1].Value = -1;
                        insertRow.Parameters[2].Value = -1;
                        if (perSpecies)
                        {
                            insertRow.Parameters[3].Value = species.ID;
                        }
                        else
                        {
                            insertRow.Parameters[3].Value = "";
                        }
                        columnIndex = 3;
                    }

                    // calculate statistics
                    stat.SetData(data);
                    double value = field.AggregationIndex switch
                    {
                        0 => stat.Mean,
                        1 => stat.Sum,
                        2 => stat.Min,
                        3 => stat.Max,
                        4 => stat.GetPercentile25(),
                        5 => stat.GetMedian(),
                        6 => stat.GetPercentile75(),
                        7 => stat.GetPercentile(5),
                        8 => stat.GetPercentile(10),
                        9 => stat.GetPercentile(90),
                        10 => stat.GetPercentile(95),
                        11 => stat.GetStandardDeviation(),
                        _ => 0.0,
                    };
                    // add current value to output
                    insertRow.Parameters[++columnIndex].Value = value;
                }

                if (columnIndex > 0)
                {
                    insertRow.ExecuteNonQuery();
                }

                if (perSpecies == false)
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
            SummaryStatistics stat = new SummaryStatistics(); // statistcs helper class
            TreeWrapper treeWrapper = new TreeWrapper();
            ResourceUnitWrapper ruWrapper = new ResourceUnitWrapper();
            mRUfilter.Wrapper = ruWrapper;

            Expression fieldExpression = new Expression();
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }

                // test filter
                if (!mRUfilter.IsEmpty)
                {
                    ruWrapper.ResourceUnit = ru;
                    if (mRUfilter.Execute(model) == 0.0)
                    {
                        continue;
                    }
                }

                int columnIndex = 0;
                foreach (ResourceUnitSpecies ruSpecies in ru.TreeSpecies)
                {
                    if (bySpecies && ruSpecies.Statistics.TreesPerHectare == 0)
                    {
                        continue;
                    }

                    // dynamic calculations
                    foreach (DynamicOutputField field in mFieldList)
                    {
                        if (String.IsNullOrEmpty(field.Expression) == false)
                        {
                            // setup dynamic dynamic expression if present
                            fieldExpression.SetExpression(field.Expression);
                            fieldExpression.Wrapper = treeWrapper;
                        }
                        data.Clear();
                        bool hasTrees = false;
                        Trees treesOfSpecies = ru.TreesBySpeciesID[ruSpecies.Species.ID];
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            if (bySpecies && treesOfSpecies.Species.Index != ruSpecies.Species.Index)
                            {
                                continue;
                            }
                            if (treesOfSpecies.IsDead(treeIndex))
                            {
                                continue;
                            }
                            treeWrapper.Trees = treesOfSpecies;

                            // apply treefilter
                            if (!mTreeFilter.IsEmpty)
                            {
                                mTreeFilter.Wrapper = treeWrapper;
                                if (mTreeFilter.Execute(model) == 0.0)
                                {
                                    continue;
                                }
                            }
                            hasTrees = true;

                            if (field.VariableIndex >= 0)
                            {
                                data.Add(treeWrapper.GetValue(model, field.VariableIndex));
                            }
                            else
                            {
                                data.Add(fieldExpression.Execute(model));
                            }
                        }

                        // do nothing if no trees are avaiable
                        if (hasTrees == false)
                        {
                            continue;
                        }

                        if (columnIndex == 0)
                        {
                            insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                            insertRow.Parameters[1].Value = ru.GridIndex;
                            insertRow.Parameters[2].Value = ru.EnvironmentID;
                            if (bySpecies)
                            {
                                insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                            }
                            else
                            {
                                insertRow.Parameters[3].Value = "";
                            }
                            columnIndex = 3;
                        }

                        // calculate statistics
                        stat.SetData(data);
                        double value = field.AggregationIndex switch
                        {
                            0 => stat.Mean,
                            1 => stat.Sum,
                            2 => stat.Min,
                            3 => stat.Max,
                            4 => stat.GetPercentile25(),
                            5 => stat.GetMedian(),
                            6 => stat.GetPercentile75(),
                            7 => stat.GetPercentile(5),
                            8 => stat.GetPercentile(10),
                            9 => stat.GetPercentile(90),
                            10 => stat.GetPercentile(95),
                            11 => stat.GetStandardDeviation(),
                            _ => 0.0,
                        };
                        // add current value to output
                        insertRow.Parameters[++columnIndex].Value = value;
                    } // foreach field

                    if (columnIndex > 0)
                    {
                        insertRow.ExecuteNonQuery();
                    }
                    if (!bySpecies)
                    {
                        break;
                    }
                } // foreach tree species
            } // foreach resource unit
        }
    }
}
