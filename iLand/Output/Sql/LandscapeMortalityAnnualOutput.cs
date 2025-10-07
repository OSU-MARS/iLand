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
    // aggregated output for removed trees on the full landscape, all values are per hectare values
    public class LandscapeMortalityAnnualOutput : AnnualOutput // C++ LandscapeRemovedOut
    {
        private const int KeyDbhClassShiftInBits = 24;
        private const int KeyRemovalTypeShiftInBits = 8;
        private const int MaximumNumberOfDiameterClasses = 255; // currently eight bits reserved in key for diameter class
        private const int MaximumSpeciesIndex = 255; // currently eight bits reserved in key for species index

        private readonly List<UInt32> mDBHClass;
        private readonly List<int> diameterClassThresholds;
        private bool includeFelledTrees;
        private bool includeNaturalMortality;
        private readonly Dictionary<UInt32, LandscapeRemovalData> removalsByTypeAndSpeciesIndex;

        public LandscapeMortalityAnnualOutput()
        {
            this.mDBHClass = [];
            this.diameterClassThresholds = [];
            this.removalsByTypeAndSpeciesIndex = [];

            this.includeNaturalMortality = false;
            this.includeFelledTrees = true;

            this.Name = "Aggregates of removed trees due to death, harvest, and disturbances per species";
            this.TableName = "landscape_removed";
            this.Description = "Aggregates of all removed trees due to 'natural' death, harvest, or disturbance per species and reason. All values are totals for the whole landscape." +
                               "The user can select with options whether to include 'natural' death and harvested trees (which may slow down the processing). " +
                               "Set the setting in the XML project file 'includeNaturalMortality to 'true' to include trees that died due to natural mortality" +
                               "(stress, abiotic, and biotic disturbances) and the setting 'includeFelled' controls whether to include trees that were cut." + Environment.NewLine +
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
            if ((this.includeNaturalMortality == false) && (removalType == MortalityCause.Stress))
            {
                return;
            }
            if ((this.includeFelledTrees == false) && ((removalType & (MortalityCause.Harvest | MortalityCause.Salavaged | MortalityCause.CutAndDrop)) != MortalityCause.None))
            {
                return;
            }

            TreeSpecies species = trees.Species;
            Debug.Assert(species.Index < LandscapeMortalityAnnualOutput.MaximumSpeciesIndex);

            UInt32 removalKey = this.GetRemovalKey(trees.DbhInCm[treeIndex], removalType, species.Index);
            if (this.removalsByTypeAndSpeciesIndex.TryGetValue(removalKey, out LandscapeRemovalData? removalData) == false)
            {
                removalData = new(species);
                this.removalsByTypeAndSpeciesIndex.Add(removalKey, removalData);
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

        // current key format: 32 bits = 8 bit DBH class << 24 | 8-16 bit removal type << 8 | 8 bit species index
        // keep paired with UnpackRemovalKey()
        private UInt32 GetRemovalKey(float dbhInCm, MortalityCause removalType, int speciesIndex)
        {
            UInt32 dbhClass = 0;
            if (this.mDBHClass.Count > 0)
            {
                int dbhInCmAsInt = (int)MathF.Ceiling(dbhInCm);
                if (dbhInCmAsInt < this.mDBHClass.Count)
                {
                    dbhClass = this.mDBHClass[dbhInCmAsInt];
                }
                else
                {
                    dbhClass = this.mDBHClass[^1];
                }
            }

            return (dbhClass << LandscapeMortalityAnnualOutput.KeyDbhClassShiftInBits) | ((UInt32)removalType << LandscapeMortalityAnnualOutput.KeyRemovalTypeShiftInBits) | (UInt32)speciesIndex;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow) // C++: LandscapeRemovedOut::exec()
        {
            foreach ((UInt32 removalKey, LandscapeRemovalData removalData) in this.removalsByTypeAndSpeciesIndex)
            {
                if (removalData.Count > 0)
                {
                    (MortalityCause removalType, int diameterClass) = LandscapeMortalityAnnualOutput.UnpackRemovalKey(removalKey);
                    string removalCode;
                    if ((removalType & MortalityCause.Stress) == MortalityCause.Stress)
                    {
                        removalCode = "S";
                    }
                    else if ((removalType & MortalityCause.Harvest) == MortalityCause.Harvest)
                    {
                        removalCode = "H";
                    }
                    else if ((removalType & MortalityCause.Salavaged) == MortalityCause.Salavaged)
                    {
                        removalCode = "S";
                    }
                    else if ((removalType & MortalityCause.CutAndDrop) == MortalityCause.CutAndDrop)
                    {
                        removalCode = "C";
                    }
                    else if ((removalType & (MortalityCause.Disturbance | MortalityCause.BarkBeetles | MortalityCause.Fire | MortalityCause.Wind)) != MortalityCause.None)
                    {
                        removalCode = "D";
                    }
                    else
                    {
                        throw new NotSupportedException($"Unhandled tree removal type {removalType}.");
                    }

                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = removalData.TreeSpecies.WorldFloraID;
                    insertRow.Parameters[2].Value = removalKey / LandscapeMortalityAnnualOutput.KeyDbhClassShiftInBits;
                    insertRow.Parameters[3].Value = removalCode;
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
            this.includeFelledTrees = projectFile.Output.Sql.LandscapeMortality.IncludeFelled;
            this.includeNaturalMortality = projectFile.Output.Sql.LandscapeMortality.IncludeNatural;

            this.diameterClassThresholds.Clear();
            if (String.IsNullOrEmpty(projectFile.Output.Sql.LandscapeMortality.DbhClasses) == false)
            {
                string[] diameterClassesAsString = projectFile.Output.Sql.LandscapeMortality.DbhClasses.Split(',');
                int maxDbhThreshold = 0;
                int previousThreshold = 0;
                for (int index = 0; index < diameterClassesAsString.Length; ++index)
                {
                    int dbhThreshold = Int32.Parse(diameterClassesAsString[index], NumberStyles.Integer);
                    if (dbhThreshold <= previousThreshold)
                    {
                        throw new NotSupportedException($"DBH class threshold list '{projectFile.Output.Sql.LandscapeMortality.DbhClasses} does not contain unique, monotontically increasing integers. The threshold of {dbhThreshold} cm is duplicated or out of sequence.");
                    }

                    this.diameterClassThresholds.Add(dbhThreshold);
                    if (dbhThreshold > maxDbhThreshold)
                    {
                        maxDbhThreshold = dbhThreshold;
                    }

                    previousThreshold = dbhThreshold;
                }
                this.diameterClassThresholds.Add(Int32.MaxValue); // upper limit

                if (maxDbhThreshold >= LandscapeMortalityAnnualOutput.MaximumNumberOfDiameterClasses)
                {
                    throw new NotSupportedException($"DBH class threshold list '{projectFile.Output.Sql.LandscapeMortality.DbhClasses} exceeds the maximum DBH of {LandscapeMortalityAnnualOutput.MaximumNumberOfDiameterClasses} .");
                }

                int maxIntegerDbh = (int)maxDbhThreshold + 1;
                this.mDBHClass.Clear();
                if (this.mDBHClass.Capacity <= maxIntegerDbh)
                {
                    this.mDBHClass.Capacity = maxIntegerDbh + 1;
                }

                int currentDbhClass = 0;
                for (int dbhInCm = 0; dbhInCm <= maxIntegerDbh; ++dbhInCm)
                {
                    if (dbhInCm >= this.diameterClassThresholds[currentDbhClass])
                    {
                        ++currentDbhClass;
                    }
                    this.mDBHClass.Add((UInt32)currentDbhClass);
                }
            }
        }

        // keep paired with GetRemovalKey()
        private static (MortalityCause removalType, int diameterClass) UnpackRemovalKey(UInt32 removalKey)
        {
            int diameterClass = (int)((removalKey >> LandscapeMortalityAnnualOutput.KeyDbhClassShiftInBits) & 0xff);
            MortalityCause mortalityCause = (MortalityCause)((removalKey >> LandscapeMortalityAnnualOutput.KeyRemovalTypeShiftInBits) & 0xff);
            return (mortalityCause, diameterClass);
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
