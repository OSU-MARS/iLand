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
    public class DynamicStandAnnualOutput : AnnualOutput
    {
        private static readonly ReadOnlyCollection<string> Aggregations = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95", "sd" }.AsReadOnly();

        private readonly List<DynamicOutputField> mFieldList;
        private readonly Expression mResourceUnitfilter;
        private readonly Expression mTreeFilter;
        private readonly Expression mYearFilter;

        private struct DynamicOutputField
        {
            public int AggregationIndex { get; set; }
            public int VariableIndex { get; set; }
            public string Expression { get; set; }
        };

        public DynamicStandAnnualOutput()
        {
            this.mYearFilter = new Expression();
            this.mFieldList = new List<DynamicOutputField>();
            this.mResourceUnitfilter = new Expression();
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
            string? columnString = model.Project.Output.Annual.DynamicStand.Columns;
            if (String.IsNullOrEmpty(columnString))
            {
                return;
            }

            this.mResourceUnitfilter.SetExpression(model.Project.Output.Annual.DynamicStand.ResourceUnitFilter);
            this.mTreeFilter.SetExpression(model.Project.Output.Annual.DynamicStand.TreeFilter);
            this.mYearFilter.SetExpression(model.Project.Output.Annual.DynamicStand.Condition);
            // clear columns
            this.Columns.RemoveRange(4, Columns.Count - 4);
            this.mFieldList.Clear();

            // setup fields
            // int pos = 0;
            TreeWrapper treeWrapper = new TreeWrapper(model);
            Regex regex = new Regex("([^\\.]+).(\\w+)[,\\s]*"); // two parts: before dot and after dot, and , + whitespace at the end
            MatchCollection columns = regex.Matches(columnString);
            foreach (Match column in columns)
            {
                string columnVariable = column.Groups[1].Value; // field / expresssion
                string columnVariableAggregation = column.Groups[2].Value;
                DynamicOutputField fieldForColumn = new DynamicOutputField();
                // parse field
                if (columnVariable.Length > 0 && !columnVariable.Contains('('))
                {
                    // simple expression
                    fieldForColumn.VariableIndex = treeWrapper.GetVariableIndex(columnVariable);
                }
                else
                {
                    // complex expression
                    fieldForColumn.VariableIndex = -1;
                    fieldForColumn.Expression = columnVariable;
                }

                fieldForColumn.AggregationIndex = DynamicStandAnnualOutput.Aggregations.IndexOf(columnVariableAggregation);
                if (fieldForColumn.AggregationIndex == -1)
                {
                    throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed:{1}",
                                                                  columnVariableAggregation, String.Join(" ", Aggregations), System.Environment.NewLine));
                }
                this.mFieldList.Add(fieldForColumn);

                string sqlColumnName = String.Format("{0}_{1}", columnVariable, columnVariableAggregation);
                sqlColumnName = Regex.Replace(sqlColumnName, "[\\[\\]\\,\\(\\)<>=!\\s]", "_");
                sqlColumnName = sqlColumnName.Replace("__", "_");
                this.Columns.Add(new SqlColumn(sqlColumnName, columnVariable, SqliteType.Real));
            }
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.mFieldList.Count == 0)
            {
                return;
            }
            if (this.mYearFilter.IsEmpty == false)
            {
                if (this.mYearFilter.Evaluate(model.CurrentYear) != 0.0)
                {
                    return;
                }
            }

            //using DebugTimer dt = model.DebugTimers.Create("DynamicStandOutput.LogYear()");
            bool perSpecies = model.Project.Output.Annual.DynamicStand.BySpecies;
            bool perRU = model.Project.Output.Annual.DynamicStand.ByResourceUnit;
            if (perRU)
            {
                // when looping over resource units, do it differently (old way)
                this.ExtractByResourceUnit(model, perSpecies, insertRow);
                return;
            }

            // grouping
            if (model.Landscape.Environment.SpeciesSetsByTableName.Count != 1)
            {
                throw new NotImplementedException("Generation of a unique list of species from multiple species sets is not currently supported.");
            }
            List<double> fieldData = new List<double>(); //statistics data
            TreeWrapper treeWrapper = new TreeWrapper(model);
            Expression customExpression = new Expression();

            TreeSpeciesSet treeSpeciesSet = model.Landscape.Environment.SpeciesSetsByTableName.First().Value;
            List<Trees> liveTreesOfSpecies = new List<Trees>();
            for (int speciesSet = 0; speciesSet < treeSpeciesSet.ActiveSpecies.Count; ++speciesSet)
            {
                liveTreesOfSpecies.Clear();

                TreeSpecies species = treeSpeciesSet.ActiveSpecies[speciesSet];
                AllTreesEnumerator allTreeEnumerator = new AllTreesEnumerator(model.Landscape);
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
                SummaryStatistics fieldStatistics = new SummaryStatistics(); // statistcs helper class
                foreach (DynamicOutputField field in this.mFieldList)
                {
                    if (String.IsNullOrEmpty(field.Expression) == false)
                    {
                        // setup dynamic dynamic expression if present
                        customExpression.SetExpression(field.Expression);
                        customExpression.Wrapper = treeWrapper;
                    }

                    // fetch data values from the trees
                    fieldData.Clear();
                    foreach (Trees trees in liveTreesOfSpecies)
                    {
                        treeWrapper.Trees = trees;
                        for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                        {
                            treeWrapper.TreeIndex = treeIndex;
                            if (field.VariableIndex >= 0)
                            {
                                fieldData.Add(treeWrapper.GetValue(field.VariableIndex));
                            }
                            else
                            {
                                fieldData.Add(customExpression.Execute());
                            }
                        }
                    }

                    // constant values (if not already present)
                    if (columnIndex == 0)
                    {
                        insertRow.Parameters[0].Value = model.CurrentYear;
                        insertRow.Parameters[1].Value = -1;
                        insertRow.Parameters[2].Value = -1;
                        if (perSpecies)
                        {
                            insertRow.Parameters[3].Value = species.ID;
                        }
                        else
                        {
                            insertRow.Parameters[3].Value = String.Empty;
                        }
                        columnIndex = 3;
                    }

                    // calculate statistics
                    fieldStatistics.SetData(fieldData);
                    double value = field.AggregationIndex switch
                    {
                        0 => fieldStatistics.Mean,
                        1 => fieldStatistics.Sum,
                        2 => fieldStatistics.Min,
                        3 => fieldStatistics.Max,
                        4 => fieldStatistics.GetPercentile25(),
                        5 => fieldStatistics.GetMedian(),
                        6 => fieldStatistics.GetPercentile75(),
                        7 => fieldStatistics.GetPercentile(5),
                        8 => fieldStatistics.GetPercentile(10),
                        9 => fieldStatistics.GetPercentile(90),
                        10 => fieldStatistics.GetPercentile(95),
                        11 => fieldStatistics.GetStandardDeviation(),
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
            if (this.mFieldList.Count == 0)
            {
                return; // nothing to do if no fields to log
            }

            List<double> data = new List<double>(); //statistics data
            SummaryStatistics fieldStatistics = new SummaryStatistics(); // statistcs helper class
            TreeWrapper treeWrapper = new TreeWrapper(model);
            ResourceUnitWrapper ruWrapper = new ResourceUnitWrapper(model);
            this.mResourceUnitfilter.Wrapper = ruWrapper;

            Expression fieldExpression = new Expression();
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }

                // test filter
                if (this.mResourceUnitfilter.IsEmpty == false)
                {
                    ruWrapper.ResourceUnit = ru;
                    if (this.mResourceUnitfilter.Execute() == 0.0)
                    {
                        continue;
                    }
                }

                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    if (bySpecies && ruSpecies.Statistics.TreeCount == 0)
                    {
                        continue;
                    }

                    // dynamic calculations
                    int columnIndex = 0;
                    foreach (DynamicOutputField field in this.mFieldList)
                    {
                        if (String.IsNullOrEmpty(field.Expression) == false)
                        {
                            // setup dynamic dynamic expression if present
                            fieldExpression.SetExpression(field.Expression);
                            fieldExpression.Wrapper = treeWrapper;
                        }
                        data.Clear();
                        bool hasTrees = false;
                        Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[ruSpecies.Species.ID];
                        treeWrapper.Trees = treesOfSpecies;
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
                            treeWrapper.TreeIndex = treeIndex;

                            // apply tree filter
                            if (!this.mTreeFilter.IsEmpty)
                            {
                                this.mTreeFilter.Wrapper = treeWrapper;
                                if (this.mTreeFilter.Execute() == 0.0)
                                {
                                    continue;
                                }
                            }
                            hasTrees = true;

                            if (field.VariableIndex >= 0)
                            {
                                data.Add(treeWrapper.GetValue(field.VariableIndex));
                            }
                            else
                            {
                                data.Add(fieldExpression.Execute());
                            }
                        }

                        // do nothing if no trees are avaiable
                        if (hasTrees == false)
                        {
                            continue;
                        }

                        if (columnIndex == 0)
                        {
                            insertRow.Parameters[0].Value = model.CurrentYear;
                            insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                            insertRow.Parameters[2].Value = ru.EnvironmentID;
                            if (bySpecies)
                            {
                                insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                            }
                            else
                            {
                                insertRow.Parameters[3].Value = String.Empty;
                            }
                            columnIndex = 3;
                        }

                        // calculate statistics
                        fieldStatistics.SetData(data);
                        double value = field.AggregationIndex switch
                        {
                            0 => fieldStatistics.Mean,
                            1 => fieldStatistics.Sum,
                            2 => fieldStatistics.Min,
                            3 => fieldStatistics.Max,
                            4 => fieldStatistics.GetPercentile25(),
                            5 => fieldStatistics.GetMedian(),
                            6 => fieldStatistics.GetPercentile75(),
                            7 => fieldStatistics.GetPercentile(5),
                            8 => fieldStatistics.GetPercentile(10),
                            9 => fieldStatistics.GetPercentile(90),
                            10 => fieldStatistics.GetPercentile(95),
                            11 => fieldStatistics.GetStandardDeviation(),
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
