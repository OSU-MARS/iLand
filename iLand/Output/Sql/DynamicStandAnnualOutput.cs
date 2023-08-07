using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public partial class DynamicStandAnnualOutput : AnnualOutput
    {
        private static readonly ReadOnlyCollection<string> WellKnownAggregations;

        private readonly List<DynamicOutputField> fieldList;
        private readonly Expression resourceUnitFilter;
        private readonly Expression treeFilter;
        private readonly Expression yearFilter;

        private struct DynamicOutputField
        {
            public int AggregationIndex { get; set; }
            public int VariableIndex { get; set; }
            public string Expression { get; set; }
        };

        static DynamicStandAnnualOutput()
        {
            DynamicStandAnnualOutput.WellKnownAggregations = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95", "sd" }.AsReadOnly();
        }

        public DynamicStandAnnualOutput()
        {
            this.yearFilter = new();
            this.fieldList = new();
            this.resourceUnitFilter = new();
            this.treeFilter = new();

            this.Name = "dynamic stand output by species/RU";
            this.TableName = "dynamicstand";
            this.Description = "User defined outputs for tree aggregates for each stand or species." + Environment.NewLine +
                               "Technically, each field is calculated 'live', i.e. it is looped over all trees, and eventually the statistics (percentiles) " +
                               "are calculated. The aggregated values are not scaled to any area unit." + Environment.NewLine +
                               "!!!Specifying the aggregation" + Environment.NewLine +
                               "The ''by_species'' and ''by_ru'' option allow to define the aggregation level. When ''by_species'' is set to ''true'', " +
                               "a row for each species will be created, otherwise all trees of all species are aggregated to one row. " +
                               "Similarly, ''by_ru''=''true'' means outputs for each resource unit, while a value of ''false'' aggregates over the full project area." + Environment.NewLine +
                               "!!!Specifying filters" + Environment.NewLine +
                               "You can use the 'rufilter' and 'treefilter' XML settings to reduce the limit the output to a subset of resource units / trees. " +
                               "Both filters are valid expressions (for resource unit level and tree level, respectively). For example, a ''treefilter'' of 'speciesindex=0' reduces the output to just one species.\n" +
                               "The ''condition'' filter is (when present) evaluated and the output is only executed when ''condition'' is true (variable='year') This can be used to constrain the output to specific years (e.g. 'in(year,100,200,300)' produces output only for the given year." + Environment.NewLine +
                               "!!!Specifying data columns" + Environment.NewLine +
                               "Each field is defined as: ''field.aggregatio''n (separated by a dot). A ''field'' is a valid [Expression]. ''Aggregation'' is one of the following:  " +
                               "mean, sum, min, max, p25, p50, p75, p5, 10, p90, p95 (pXX=XXth percentile), sd (std.dev.)." + Environment.NewLine +
                               "Complex expression are allowed, e.g: if(dbh>50,1,0).sum (-> counts trees with dbh>50)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            // other colums are added during setup...
        }

        [GeneratedRegex("([^\\.]+).(\\w+)[,\\s]*")] // two parts: before dot and after dot, and , + whitespace at the end
        private static partial Regex GetColumnVariableAndAggregationRegex();

        [GeneratedRegex("[\\[\\]\\,\\(\\)<>=!\\s]")]
        private static partial Regex GetSqlColumnNameRegex();

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            string? columnString = projectFile.Output.Sql.DynamicStand.Columns;
            if (String.IsNullOrEmpty(columnString))
            {
                return;
            }

            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.DynamicStand.ResourceUnitFilter);
            this.treeFilter.SetExpression(projectFile.Output.Sql.DynamicStand.TreeFilter);
            this.yearFilter.SetExpression(projectFile.Output.Sql.DynamicStand.Condition);
            // remove any columns following the three columns added in the constructor
            this.Columns.RemoveRange(3, this.Columns.Count - 3);
            this.fieldList.Clear();

            // setup fields
            TreeVariableAccessor treeWrapper = new(simulationState);
            MatchCollection columnDefinitions = DynamicStandAnnualOutput.GetColumnVariableAndAggregationRegex().Matches(columnString);
            foreach (Match columnVariableAndAggregation in columnDefinitions)
            {
                string columnVariable = columnVariableAndAggregation.Groups[1].Value; // field / expresssion
                string columnVariableAggregation = columnVariableAndAggregation.Groups[2].Value;
                DynamicOutputField fieldForColumn = new();
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

                fieldForColumn.AggregationIndex = DynamicStandAnnualOutput.WellKnownAggregations.IndexOf(columnVariableAggregation);
                if (fieldForColumn.AggregationIndex == -1)
                {
                    throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed:{1}",
                                                                  columnVariableAggregation, String.Join(" ", DynamicStandAnnualOutput.WellKnownAggregations), System.Environment.NewLine));
                }
                this.fieldList.Add(fieldForColumn);

                string sqlColumnName = String.Format("{0}_{1}", columnVariable, columnVariableAggregation);
                sqlColumnName = DynamicStandAnnualOutput.GetSqlColumnNameRegex().Replace(sqlColumnName, "_");
                sqlColumnName = sqlColumnName.Replace("__", "_");
                this.Columns.Add(new(sqlColumnName, columnVariable, SqliteType.Real));
            }
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.fieldList.Count == 0)
            {
                return;
            }
            if (this.yearFilter.IsEmpty == false)
            {
                int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
                if (this.yearFilter.Evaluate(currentCalendarYear) != 0.0F)
                {
                    return;
                }
            }

            bool logBySpecies = model.Project.Output.Sql.DynamicStand.BySpecies;
            bool logByResourceUnit = model.Project.Output.Sql.DynamicStand.ByResourceUnit;
            if (logByResourceUnit)
            {
                // when looping over resource units, do it differently (old way)
                this.ExtractByResourceUnit(model, logBySpecies, insertRow);
                return;
            }

            // grouping
            if (model.Landscape.SpeciesSetsByTableName.Count != 1)
            {
                throw new NotImplementedException("Generation of a unique list of species from multiple species sets is not currently supported.");
            }
            List<float> fieldData = new(); // statistics data
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            Expression customExpression = new();

            TreeSpeciesSet treeSpeciesSet = model.Landscape.SpeciesSetsByTableName.First().Value;
            List<TreeListSpatial> liveTreesOfSpecies = new();
            for (int speciesSet = 0; speciesSet < treeSpeciesSet.ActiveSpecies.Count; ++speciesSet)
            {
                liveTreesOfSpecies.Clear();

                TreeSpecies species = treeSpeciesSet.ActiveSpecies[speciesSet];
                AllTreesEnumerator allTreeEnumerator = new(model.Landscape);
                while (allTreeEnumerator.MoveNextLiving())
                {
                    if (logBySpecies && allTreeEnumerator.CurrentTrees.Species != species)
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
                SummaryStatistics fieldStatistics = new(); // statistcs helper class
                foreach (DynamicOutputField field in this.fieldList)
                {
                    if (String.IsNullOrEmpty(field.Expression) == false)
                    {
                        // setup dynamic dynamic expression if present
                        customExpression.SetExpression(field.Expression);
                        customExpression.Wrapper = treeWrapper;
                    }

                    // fetch data values from the trees
                    fieldData.Clear();
                    foreach (TreeListSpatial trees in liveTreesOfSpecies)
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
                        insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                        insertRow.Parameters[1].Value = -1;
                        insertRow.Parameters[2].Value = -1;
                        if (logBySpecies)
                        {
                            insertRow.Parameters[3].Value = species.WorldFloraID;
                        }
                        else
                        {
                            insertRow.Parameters[3].Value = String.Empty;
                        }
                        columnIndex = 3;
                    }

                    // calculate statistics
                    fieldStatistics.SetData(fieldData);
                    float value = field.AggregationIndex switch
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
                        _ => 0.0F,
                    };
                    // add current value to output
                    insertRow.Parameters[++columnIndex].Value = value;
                }

                if (columnIndex > 0)
                {
                    insertRow.ExecuteNonQuery();
                }

                if (logBySpecies == false)
                {
                    break;
                }
            }
        }

        private void ExtractByResourceUnit(Model model, bool bySpecies, SqliteCommand insertRow)
        {
            if (this.fieldList.Count == 0)
            {
                return; // nothing to do if no fields to log
            }

            List<float> fieldData = new(); //statistics data
            SummaryStatistics fieldStatistics = new(); // statistcs helper class
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            ResourceUnitVariableAccessor ruWrapper = new(model.SimulationState);
            this.resourceUnitFilter.Wrapper = ruWrapper;

            Expression fieldExpression = new();
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                // test filter
                if (this.resourceUnitFilter.IsEmpty == false)
                {
                    ruWrapper.ResourceUnit = resourceUnit;
                    if (this.resourceUnitFilter.Execute() == 0.0F)
                    {
                        continue;
                    }
                }

                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    if (bySpecies && ruSpecies.StatisticsLive.TreesPerHa == 0)
                    {
                        continue;
                    }

                    // dynamic calculations
                    int columnIndex = 0;
                    foreach (DynamicOutputField field in this.fieldList)
                    {
                        if (String.IsNullOrEmpty(field.Expression) == false)
                        {
                            // setup dynamic dynamic expression if present
                            fieldExpression.SetExpression(field.Expression);
                            fieldExpression.Wrapper = treeWrapper;
                        }
                        fieldData.Clear();
                        bool hasTrees = false;
                        TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID[ruSpecies.Species.WorldFloraID];
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
                            if (!this.treeFilter.IsEmpty)
                            {
                                this.treeFilter.Wrapper = treeWrapper;
                                if (this.treeFilter.Execute() == 0.0F)
                                {
                                    continue;
                                }
                            }
                            hasTrees = true;

                            if (field.VariableIndex >= 0)
                            {
                                fieldData.Add(treeWrapper.GetValue(field.VariableIndex));
                            }
                            else
                            {
                                fieldData.Add(fieldExpression.Execute());
                            }
                        }

                        // do nothing if no trees are avaiable
                        if (hasTrees == false)
                        {
                            continue;
                        }

                        if (columnIndex == 0)
                        {
                            insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                            insertRow.Parameters[1].Value = resourceUnit.ID;
                            if (bySpecies)
                            {
                                insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID;
                            }
                            else
                            {
                                insertRow.Parameters[2].Value = String.Empty;
                            }
                            columnIndex = 2;
                        }

                        // calculate statistics
                        fieldStatistics.SetData(fieldData);
                        float value = field.AggregationIndex switch
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
                            _ => 0.0F,
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
