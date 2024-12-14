// C++/output/{ landscapeout.h, landscapeout.cpp }
using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    /** LandscapeRemovedOut is aggregated output for removed trees on the full landscape. All values are per hectare values. */
    public class LandscapeRemovedAnnualOutput : AnnualOutput
    {
        private const int KeyDbhClassMultiplier = 100000; // space for Int32.MaxValue / KeyDbhClassMultiplier = 21474 DBH classes
        private const int KeyRemovalTypeMultiplier = 10000; // space for 10 removal classes (values 0..9)
        private const int MaxDbhInCm = Int32.MaxValue / LandscapeRemovedAnnualOutput.KeyDbhClassMultiplier;

        private readonly List<int> mDBHClass;
        private readonly List<int> mDBHThreshold;
        private bool includeDeadTrees;
        private bool includeHarvestTrees;
        private readonly Dictionary<int, LandscapeRemovalData> removalsByTypeAndSpeciesIndex;

        public LandscapeRemovedAnnualOutput()
        {
            this.mDBHClass = [];
            this.mDBHThreshold = [];
            this.removalsByTypeAndSpeciesIndex = [];

            this.includeDeadTrees = false;
            this.includeHarvestTrees = true;

            this.Name = "Aggregates of removed trees due to death, harvest, and disturbances per species";
            this.TableName = "landscape_removed";
            this.Description = "Aggregates of all removed trees due to 'natural' death, harvest, or disturbance per species and reason. All values are totals for the whole landscape." +
                               "The user can select with options whether to include 'natural' death and harvested trees (which may slow down the processing). " +
                               "Set the setting in the XML project file 'includeNatural' to 'true' to include trees that died due to natural mortality, " +
                               "the setting 'includeHarvest' controls whether to include ('true') or exclude ('false') harvested trees." + Environment.NewLine +
                               "To enable output per DBH class, set the 'dbhClasses' setting to a comma delimited list of DBH class upper bounds (e.g., '10,20,30,40,50'). The value in the output column " +
                               "'dbh_class' refers to the class (e.g.: 0: 0-10, 1: 10-20, 2: 20-30, 3: 30-40, 4: 40-50, 5: >=50). Class bounds must be monotonically increasing integers.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("dbh_class", "dbh class (see above). 0 if dbh classes are off.", SqliteType.Integer));
            this.Columns.Add(new("reason", "Reason for tree death: 'N': Natural mortality, 'H': Harvest (removed from the forest), 'D': Disturbance (not salvage-harvested), 'S': Salvage harvesting (i.e. disturbed trees which are harvested), 'C': killed/cut down by management", SqliteType.Text));
            this.Columns.Add(new("count", "Number of trees died (living, >4m height).", SqliteType.Integer));
            this.Columns.Add(new("volume_m3", "Sum of stem volume (geomery, taper factor), m³.", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "Total basal area at breast height, m².", SqliteType.Real));
            this.Columns.Add(new("total_carbon", "Total carbon (sum of stem, branch, foliage, coarse and fine roots, and NPP reserve), kg C.", SqliteType.Real));
            this.Columns.Add(new("stem_c", "Carbon in stems, kg C.", SqliteType.Real));
            this.Columns.Add(new("branch_c", "Carbon on branch compartment, kg C.", SqliteType.Real));
            this.Columns.Add(new("foliage_c", "Carbon in foliage, kg C.", SqliteType.Real));
        }

        public void AddRemovedTree(TreeListSpatial trees, int treeIndex, MortalityCause removalType)
        {
            if ((this.includeDeadTrees == false) && (removalType == MortalityCause.Stress))
            {
                return;
            }
            if ((this.includeHarvestTrees == false) && (removalType == MortalityCause.Harvest || removalType == MortalityCause.Salavaged || removalType == MortalityCause.CutDown))
            {
                return;
            }

            TreeSpecies species = trees.Species;
            Debug.Assert(species.Index < LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier);

            int dbhClass = 0;
            if (this.mDBHClass.Count > 0)
            {
                int dbhInCmAsInt = (int)MathF.Ceiling(trees.DbhInCm[treeIndex]);
                if (dbhInCmAsInt < this.mDBHClass.Count)
                {
                    dbhClass = this.mDBHClass[dbhInCmAsInt];
                }
                else
                {
                    dbhClass = this.mDBHClass[^1];
                }
            }

            int key = LandscapeRemovedAnnualOutput.KeyDbhClassMultiplier * dbhClass + LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier * (int)removalType + species.Index;
            if (this.removalsByTypeAndSpeciesIndex.TryGetValue(key, out LandscapeRemovalData? removalData) == false)
            {
                removalData = new(species);
                this.removalsByTypeAndSpeciesIndex.Add(key, removalData);
            }

            float stemMass = trees.StemMassInKg[treeIndex];
            float branchMass = species.GetBiomassBranch(trees.DbhInCm[treeIndex]);
            float foliageMass = trees.FoliageMassInKg[treeIndex];
            removalData.BasalArea += trees.GetBasalArea(treeIndex);
            removalData.Volume += trees.GetStemVolume(treeIndex);
            removalData.CarbonTotal += Constant.DryBiomassCarbonFraction * (branchMass + trees.CoarseRootMassInKg[treeIndex] + trees.FineRootMassInKg[treeIndex] + foliageMass + stemMass + trees.NppReserveInKg[treeIndex]);
            removalData.CarbonStem += Constant.DryBiomassCarbonFraction * stemMass;
            removalData.CarbonBranch += Constant.DryBiomassCarbonFraction * branchMass;
            removalData.CarbonFoliage += Constant.DryBiomassCarbonFraction * foliageMass;
            ++removalData.Count;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow) // C++: LandscapeRemovedOut::exec()
        {
            foreach ((int removalKey, LandscapeRemovalData removalData) in this.removalsByTypeAndSpeciesIndex)
            {
                if (removalData.Count > 0)
                {
                    MortalityCause removalType = (MortalityCause)(removalKey / LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier);
                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = removalData.TreeSpecies.WorldFloraID;
                    insertRow.Parameters[2].Value = removalKey / LandscapeRemovedAnnualOutput.KeyDbhClassMultiplier;
                    insertRow.Parameters[3].Value = removalType switch
                    {
                        MortalityCause.CutDown => "C",
                        MortalityCause.Stress => "N",
                        MortalityCause.Disturbance => "D",
                        MortalityCause.Harvest => "H",
                        MortalityCause.Salavaged => "S",
                        _ => throw new NotSupportedException("Unhandled tree removal type " + removalType + ".")
                    };
                    insertRow.Parameters[4].Value = removalData.Count;
                    insertRow.Parameters[5].Value = removalData.Volume;
                    insertRow.Parameters[6].Value = removalData.BasalArea;
                    insertRow.Parameters[7].Value = removalData.CarbonTotal;
                    insertRow.Parameters[8].Value = removalData.CarbonStem;
                    insertRow.Parameters[9].Value = removalData.CarbonBranch;
                    insertRow.Parameters[10].Value = removalData.CarbonFoliage;
                    insertRow.ExecuteNonQuery();
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LandscapeRemovalData removal in this.removalsByTypeAndSpeciesIndex.Values)
            {
                removal.Zero();
            }
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.includeHarvestTrees = projectFile.Output.Sql.LandscapeRemoved.IncludeHarvest;
            this.includeDeadTrees = projectFile.Output.Sql.LandscapeRemoved.IncludeNatural;

            this.mDBHThreshold.Clear();
            if (String.IsNullOrEmpty(projectFile.Output.Sql.LandscapeRemoved.DbhClasses) == false)
            {
                string[] cls_list = projectFile.Output.Sql.LandscapeRemoved.DbhClasses.Split(',');
                int maxThreshold = 0;
                int previousThreshold = 0;
                for (int i = 0; i < cls_list.Length; ++i)
                {
                    int dbhThreshold = Int32.Parse(cls_list[i], NumberStyles.Integer);
                    if (dbhThreshold <= previousThreshold)
                    {
                        throw new NotSupportedException("DBH class threshold list '" + projectFile.Output.Sql.LandscapeRemoved.DbhClasses + " does not contain unique, monotontically increasing integers. The threshold of " + dbhThreshold + " cm is duplicated or out of sequence.");
                    }

                    this.mDBHThreshold.Add(dbhThreshold);
                    if (dbhThreshold > maxThreshold)
                    {
                        maxThreshold = dbhThreshold;
                    }

                    previousThreshold = dbhThreshold;
                }
                this.mDBHThreshold.Add(Int32.MaxValue); // upper limit

                if (maxThreshold >= LandscapeRemovedAnnualOutput.MaxDbhInCm)
                {
                    throw new NotSupportedException("DBH class threshold list '" + projectFile.Output.Sql.LandscapeRemoved.DbhClasses + " exceeds the maximum DBH of " + LandscapeRemovedAnnualOutput.MaxDbhInCm + " .");
                }

                int maxIntegerDbh = (int)maxThreshold + 1;
                this.mDBHClass.Clear();
                if (this.mDBHClass.Capacity <= maxIntegerDbh)
                {
                    this.mDBHClass.Capacity = maxIntegerDbh + 1;
                }
                int currentDbhClass = 0;
                for (int dbhInCm = 0; dbhInCm <= maxIntegerDbh; ++dbhInCm)
                {
                    if (dbhInCm >= this.mDBHThreshold[currentDbhClass])
                    {
                        ++currentDbhClass;
                    }
                    this.mDBHClass.Add(currentDbhClass);
                }
            }
        }

        private class LandscapeRemovalData
        {
            public float BasalArea { get; set; }
            public float CarbonTotal { get; set; }
            public float CarbonStem { get; set; }
            public float CarbonBranch { get; set; }
            public float CarbonFoliage { get; set; }
            public float Count { get; set; }
            public TreeSpecies TreeSpecies { get; private init; }
            public float Volume { get; set; }

            public LandscapeRemovalData(TreeSpecies treeSpecies)
            {
                this.TreeSpecies = treeSpecies;
                this.Zero();
            }

            public void Zero()
            {
                this.BasalArea = 0.0F;
                this.Count = 0.0F;
                this.Volume = 0.0F;
            }
        }
    }
}
