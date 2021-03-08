#nullable disable
using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Model = iLand.Simulation.Model;

namespace iLand.Output
{
    internal class Snapshot : AnnualOutput
    {
        private readonly Dictionary<int, ResourceUnit> mResourceUnits;

        public Snapshot()
        {
            this.mResourceUnits = new Dictionary<int, ResourceUnit>();
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            throw new NotImplementedException();
        }

        private static SqliteConnection OpenDatabase(string fileName, bool openReadOnly)
        {
            SqliteConnection snapshotDatabase = Landscape.GetDatabaseConnection(fileName, openReadOnly);
            if (openReadOnly == false)
            {
                using SqliteTransaction transaction = snapshotDatabase.BeginTransaction();
                // create tables
                // trees
                using SqliteCommand dropTrees = new("drop table trees", snapshotDatabase, transaction);
                dropTrees.ExecuteNonQuery();
                using SqliteCommand createTrees = new("create table trees (ID integer, RUindex integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)", snapshotDatabase, transaction);
                createTrees.ExecuteNonQuery();
                // soil
                using SqliteCommand dropSoil = new("drop table soil", snapshotDatabase, transaction);
                dropSoil.ExecuteNonQuery();
                using SqliteCommand createSoil = new("create table soil (RUindex integer, kyl real, kyr real, inLabC real, inLabN real, inLabP real, inRefC real, inRefN real, inRefP real, YLC real, YLN real, YLP real, YRC real, YRN real, YRP real, SOMC real, SOMN real, WaterContent, SnowPack real)", snapshotDatabase, transaction);
                createSoil.ExecuteNonQuery();
                // snag
                using SqliteCommand dropSnag = new("drop table snag", snapshotDatabase, transaction);
                dropSoil.ExecuteNonQuery();
                using SqliteCommand createSnag = new("create table snag(RUIndex integer, climateFactor real, SWD1C real, SWD1N real, SWD2C real, SWD2N real, SWD3C real, SWD3N real, " +
                                                     "totalSWDC real, totalSWDN real, NSnags1 real, NSnags2 real, NSnags3 real, dbh1 real, dbh2 real, dbh3 real, height1 real, height2 real, height3 real, " +
                                                     "volume1 real, volume2 real, volume3 real, tsd1 real, tsd2 real, tsd3 real, ksw1 real, ksw2 real, ksw3 real, halflife1 real, halflife2 real, halflife3 real, " +
                                                     "branch1C real, branch1N real, branch2C real, branch2N real, branch3C real, branch3N real, branch4C real, branch4N real, branch5C real, branch5N real, branchIndex integer)", snapshotDatabase, transaction);
                createSnag.ExecuteNonQuery();
                // saplings/regeneration
                using SqliteCommand dropSaplings = new("drop table saplings", snapshotDatabase, transaction);
                dropSoil.ExecuteNonQuery();
                using SqliteCommand createSaplings = new("create table saplings (RUindex integer, species text, posx integer, posy integer, age integer, height float, stress_years integer)", snapshotDatabase, transaction);
                createSnag.ExecuteNonQuery();
                transaction.Commit();
                Debug.WriteLine("Snapshot - tables created. Database " + fileName);
            }
            return snapshotDatabase;
        }

        public static bool CreateSnapshot(Model model, string snapshotDatabaseFileName)
        {
            using SqliteConnection snapshotDatabase = Snapshot.OpenDatabase(snapshotDatabaseFileName, false);
            // save the trees
            Snapshot.SaveTrees(model, snapshotDatabase);
            // save soil pools
            Snapshot.SaveSoil(model, snapshotDatabase);
            // save snags / deadwood pools
            Snapshot.SaveSnags(model, snapshotDatabase);
            // save saplings
            Snapshot.SaveSaplings(snapshotDatabase);

            // save a grid of the indices
            FileInfo fi = new(snapshotDatabaseFileName);
            string gridFile = Path.Combine(Path.GetDirectoryName(fi.FullName), Path.GetFileNameWithoutExtension(fi.FullName) + ".asc");

            Grid<ResourceUnit> ruGrid = model.Landscape.ResourceUnitGrid;
            Grid<double> ruIndexGrid = new(ruGrid.SizeX, ruGrid.SizeY, ruGrid.CellSize);
            ruIndexGrid.Setup(model.Landscape.ResourceUnitGrid.PhysicalExtent, model.Landscape.ResourceUnitGrid.CellSize);
            for (int index = 0; index < ruGrid.Count; ++index)
            {
                ResourceUnit ru = ruGrid[index];
                if (ru != null)
                {
                    ruIndexGrid[index] = ru.ResourceUnitGridIndex;
                }
                else
                {
                    ruIndexGrid[index] = -1.0;
                }
            }
            string gridText = Grid.ToEsriRaster(model.Landscape, ruIndexGrid);
            File.WriteAllText(gridFile, gridText);
            Debug.WriteLine("saved grid to " + gridFile);

            return true;
        }

        public bool Load(Model model, string snapshotDatabaseFilePath)
        {
            //using DebugTimer t = model.DebugTimers.Create("Snapshot.Load()");
            string gridFile = Path.Combine(Path.GetDirectoryName(snapshotDatabaseFilePath), Path.GetFileNameWithoutExtension(snapshotDatabaseFilePath) + ".asc");
            GisGrid grid = new();
            mResourceUnits.Clear();

            if (!grid.LoadFromFile(gridFile))
            {
                Debug.WriteLine("loading of snapshot: not a valid grid file (containing resource unit inidices) expected at: " + gridFile);
                Grid<ResourceUnit> ruGrid = model.Landscape.ResourceUnitGrid;
                for (int index = 0; index < ruGrid.Count; ++index)
                {
                    ResourceUnit ru = ruGrid[index];
                    if (ru != null)
                    {
                        mResourceUnits.Add(index, ru);
                    }
                }
            }
            else
            {
                // setup link between resource unit index and index grid:
                // store for each resource unit *in the snapshot database* the corresponding
                // resource unit index of the *current* simulation.
                PointF to = model.Landscape.Environment.GisGrid.GisToModel(grid.GisOrigin);
                if ((to.X % Constant.RUSize) != 0.0 || (to.Y % Constant.RUSize) != 0.0)
                {
                    PointF world_offset = model.Landscape.Environment.GisGrid.ModelToGis(new PointF(0.0F, 0.0F));
                    throw new NotSupportedException(String.Format("Loading of the snapshot '{0}' failed: The offset from the current location of the project ({3}/{4}) " +
                                             "is not a multiple of the resource unit size (100m) relative to grid of the snapshot (origin-x: {1}, origin-y: {2}).", snapshotDatabaseFilePath,
                                     grid.GisOrigin.X, grid.GisOrigin.Y, world_offset.X, world_offset.Y));
                }

                Grid<ResourceUnit> ruGrid = model.Landscape.ResourceUnitGrid;
                for (int ruIndex = 0; ruIndex < ruGrid.Count; ++ruIndex)
                {
                    ResourceUnit ru = ruGrid[ruIndex];
                    if (ru != null && ru.ResourceUnitGridIndex > -1)
                    {
                        int value = (int)grid.GetValue(ruGrid.GetCellCenterPosition(ruIndex));
                        if (value > -1)
                        {
                            mResourceUnits[value] = ru;
                        }
                    }
                }
            }

            using SqliteConnection snapshotDatabase = Snapshot.OpenDatabase(snapshotDatabaseFilePath, openReadOnly: true);
            this.LoadTrees(model, snapshotDatabase);
            this.LoadSoil(snapshotDatabase);
            this.LoadSnags(snapshotDatabase);
            // load saplings only when regeneration is enabled (this can save a lot of time)
            if (model.ModelSettings.RegenerationEnabled)
            {
                this.LoadSaplings(model, snapshotDatabase);
                //loadSaplingsOld();
            }

            // after changing the trees, do a complete apply/read pattern cycle over the landscape...
            // TODO: Why is this needed?  It occurs early in a model's annual timestep. Also, order here is not consistent with Model.Setup().
            model.ApplyAndReadLightPattern();
            Debug.WriteLine("applied light pattern...");

            // refresh the stand statistics
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                ru.Trees.RecalculateStatistics(true); // true: recalculate statistics
            }

            Debug.WriteLine("created stand statistics...");
            Debug.WriteLine("loading of snapshot completed.");
            return true;
        }

        public static bool SaveStandSnapshot(Model model, int standID, string fileName)
        {
            // Check database
            using SqliteConnection standSnapshotDatabase = Landscape.GetDatabaseConnection(model.Project.GetFilePath(ProjectDirectory.Home, fileName), openReadOnly: false);

            List<string> tableNames = new();
            SqliteCommand selectTableNames = new("SELECT name FROM sqlite_schema WHERE type='table'", standSnapshotDatabase);
            SqliteDataReader tableNameReader = selectTableNames.ExecuteReader();
            while (tableNameReader.Read())
            {
                tableNames.Add(tableNameReader.GetString(0));
            }

            // check if tree/sapling tables are already present
            if (tableNames.Contains("trees_stand") == false || tableNames.Contains("saplings_stand") == false)
            {
                // create tables
                using SqliteTransaction tablesTransaction = standSnapshotDatabase.BeginTransaction();
                using SqliteCommand dropTrees = new("drop table trees_stand", standSnapshotDatabase, tablesTransaction);
                dropTrees.ExecuteNonQuery();
                // trees
                using SqliteCommand createTrees = new("create table trees_stand (standID integer, ID integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)", standSnapshotDatabase, tablesTransaction);
                createTrees.ExecuteNonQuery();
                // saplings/regeneration
                using SqliteCommand dropSaplings = new("drop table saplings_stand", standSnapshotDatabase, tablesTransaction);
                dropSaplings.ExecuteNonQuery();
                using SqliteCommand createSaplings = new("create table saplings_stand (standID integer, posx integer, posy integer, species_index integer, age integer, height float, stress_years integer, flags integer)", standSnapshotDatabase, tablesTransaction);
                createSaplings.ExecuteNonQuery();
                tablesTransaction.Commit();
            }

            // save trees
            using (SqliteTransaction treesTransaction = standSnapshotDatabase.BeginTransaction())
            {
                using SqliteCommand deleteStand = new(String.Format("delete from trees_stand where standID={0}", standID), standSnapshotDatabase, treesTransaction);
                deleteStand.ExecuteNonQuery();

                using SqliteCommand insertTree = new("insert into trees_stand (standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " +
                                                     "values (:standid, :id, :x, :y, :spec, :age, :h, :d, :la, :opa, :mfol, :mwood, :mfr, :mcr, :npp, :si)");
                insertTree.Parameters.Add(":standID", SqliteType.Integer);
                insertTree.Parameters.Add(":id", SqliteType.Integer);
                insertTree.Parameters.Add(":x", SqliteType.Real);
                insertTree.Parameters.Add(":y", SqliteType.Real);
                insertTree.Parameters.Add(":spec", SqliteType.Integer);
                insertTree.Parameters.Add(":age", SqliteType.Integer);
                insertTree.Parameters.Add(":h", SqliteType.Real);
                insertTree.Parameters.Add(":d", SqliteType.Real);
                insertTree.Parameters.Add(":la", SqliteType.Real);
                insertTree.Parameters.Add(":opa", SqliteType.Real);
                insertTree.Parameters.Add(":mfol", SqliteType.Real);
                insertTree.Parameters.Add(":wood", SqliteType.Real);
                insertTree.Parameters.Add(":mfr", SqliteType.Real);
                insertTree.Parameters.Add(":mcr", SqliteType.Real);
                insertTree.Parameters.Add(":npp", SqliteType.Real);
                insertTree.Parameters.Add(":si", SqliteType.Real);

                PointF offset = model.Landscape.Environment.GisGrid.ModelToGis(new PointF(0.0F, 0.0F));
                List<MutableTuple<Trees, List<int>>> livingTreesInStand = model.Landscape.StandGrid.GetLivingTreesInStand(standID);
                for (int speciesIndex = 0; speciesIndex < livingTreesInStand.Count; ++speciesIndex)
                {
                    Trees trees = livingTreesInStand[speciesIndex].Item1;
                    foreach (int treeIndex in livingTreesInStand[speciesIndex].Item2)
                    {
                        insertTree.Parameters[0].Value = standID;
                        insertTree.Parameters[1].Value = trees.Tag[treeIndex];
                        insertTree.Parameters[2].Value = trees.GetCellCenterPoint(treeIndex).X + offset.X;
                        insertTree.Parameters[3].Value = trees.GetCellCenterPoint(treeIndex).Y + offset.Y;
                        insertTree.Parameters[4].Value = trees.Species.ID;
                        insertTree.Parameters[5].Value = trees.Age[treeIndex];
                        insertTree.Parameters[6].Value = trees.Height[treeIndex];
                        insertTree.Parameters[7].Value = trees.Dbh[treeIndex];
                        insertTree.Parameters[8].Value = trees.LeafArea[treeIndex];
                        insertTree.Parameters[10].Value = trees.Opacity[treeIndex];
                        insertTree.Parameters[11].Value = trees.FoliageMass[treeIndex];
                        insertTree.Parameters[12].Value = trees.StemMass[treeIndex];
                        insertTree.Parameters[13].Value = trees.FineRootMass[treeIndex];
                        insertTree.Parameters[14].Value = trees.CoarseRootMass[treeIndex];
                        insertTree.Parameters[15].Value = trees.NppReserve[treeIndex];
                        insertTree.Parameters[16].Value = trees.StressIndex[treeIndex];
                        insertTree.ExecuteNonQuery();
                    }
                }
                treesTransaction.Commit();
            }

            // save saplings
            // loop over all pixels, only when regeneration is enabled
            if (model.ModelSettings.RegenerationEnabled)
            {
                using SqliteTransaction saplingTransaction = standSnapshotDatabase.BeginTransaction();
                using SqliteCommand deleteSaplings = new(String.Format("delete from saplings_stand where standID={0}", standID), standSnapshotDatabase, saplingTransaction);
                deleteSaplings.ExecuteNonQuery();

                using SqliteCommand insertSapling = new(String.Format("insert into saplings_stand (standID, posx, posy, species_index, age, height, stress_years, flags) " +
                                                                      "values (?,?,?,?,?,?,?,?)"), standSnapshotDatabase, saplingTransaction);
                insertSapling.Parameters.Add("standID", SqliteType.Integer);
                insertSapling.Parameters.Add("posx", SqliteType.Real);
                insertSapling.Parameters.Add("posy", SqliteType.Real);
                insertSapling.Parameters.Add("species_index", SqliteType.Integer);
                insertSapling.Parameters.Add("age", SqliteType.Integer);
                insertSapling.Parameters.Add("height", SqliteType.Real);
                insertSapling.Parameters.Add("stress_years", SqliteType.Integer);
                insertSapling.Parameters.Add("flags", SqliteType.Integer);

                PointF offset = model.Landscape.Environment.GisGrid.ModelToGis(new PointF(0.0F, 0.0F));
                SaplingCellRunner saplingRunner = new(model.Landscape, standID);
                for (SaplingCell saplingCell = saplingRunner.MoveNext(); saplingCell != null; saplingCell = saplingRunner.MoveNext())
                {
                    for (int index = 0; index < saplingCell.Saplings.Length; ++index)
                    {
                        if (saplingCell.Saplings[index].IsOccupied())
                        {
                            insertSapling.Parameters[0].Value = standID;
                            PointF t = saplingRunner.CurrentCoordinate();
                            insertSapling.Parameters[1].Value = t.X + offset.X;
                            insertSapling.Parameters[2].Value = t.Y + offset.Y;
                            insertSapling.Parameters[3].Value = saplingCell.Saplings[index].SpeciesIndex;
                            insertSapling.Parameters[4].Value = saplingCell.Saplings[index].Age;
                            insertSapling.Parameters[5].Value = saplingCell.Saplings[index].Height;
                            insertSapling.Parameters[6].Value = saplingCell.Saplings[index].StressYears;
                            insertSapling.Parameters[7].Value = saplingCell.Saplings[index].IsSprout;
                            insertSapling.ExecuteNonQuery();
                        }
                    }

                    saplingTransaction.Commit();
                }
            }

            return true;
        }

        public static bool LoadStandSnapshot(Model model, int standID, MapGrid standGrid, string fileName)
        {
            using SqliteConnection db = Landscape.GetDatabaseConnection(model.Project.GetFilePath(ProjectDirectory.Home, fileName), openReadOnly: false);

            // load trees
            // kill all living trees on the stand
            List<MutableTuple<Trees, List<int>>> livingTreesInStand = standGrid.GetLivingTreesInStand(standID);
            Debug.Assert(livingTreesInStand.Count == 0);

            // load from database
            RectangleF extent = model.Landscape.Extent;
            using SqliteCommand treeQuery = new(String.Format("select standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, " +
                                                              "foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex " +
                                                              "from trees_stand where standID={0}", standID), db);
            using SqliteDataReader treeReader = treeQuery.ExecuteReader();
            int treesAdded = 0;
            while (treeReader.Read())
            {
                ++treesAdded;

                PointF treeLocation = model.Landscape.Environment.GisGrid.GisToModel(new PointF(treeReader.GetInt32(2), treeReader.GetInt32(3)));
                if (!extent.Contains(treeLocation))
                {
                    continue;
                }
                ResourceUnit ru = model.Landscape.GetResourceUnit(treeLocation);
                if (ru == null)
                {
                    continue;
                }

                TreeSpecies species = ru.Trees.TreeSpeciesSet[treeReader.GetInt32(4)];
                int treeIndex = ru.Trees.AddTree(model.Landscape, species.ID);
                Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[species.ID];
                treesOfSpecies.Tag[treeIndex] = treeReader.GetInt32(1);
                treesOfSpecies.SetLightCellIndex(treeIndex, treeLocation);
                treesOfSpecies.Species = species ?? throw new NotSupportedException("loadTrees: Invalid species");
                treesOfSpecies.Age[treeIndex] = treeReader.GetInt32(5);
                treesOfSpecies.Height[treeIndex] = treeReader.GetFloat(6);
                treesOfSpecies.Dbh[treeIndex] = treeReader.GetFloat(7);
                treesOfSpecies.LeafArea[treeIndex] = treeReader.GetFloat(8);
                treesOfSpecies.Opacity[treeIndex] = treeReader.GetFloat(9);
                treesOfSpecies.FoliageMass[treeIndex] = treeReader.GetFloat(10);
                treesOfSpecies.StemMass[treeIndex] = treeReader.GetFloat(11);
                treesOfSpecies.FineRootMass[treeIndex] = treeReader.GetFloat(12);
                treesOfSpecies.CoarseRootMass[treeIndex] = treeReader.GetFloat(13);
                treesOfSpecies.NppReserve[treeIndex] = treeReader.GetFloat(14);
                treesOfSpecies.StressIndex[treeIndex] = treeReader.GetFloat(15);
                treesOfSpecies.Stamp[treeIndex] = species.GetStamp(treesOfSpecies.Dbh[treeIndex], treesOfSpecies.Height[treeIndex]);
            }

            // now the saplings
            int existingSaplingsRemoved = 0;
            int saplingsAdded = 0;
            if (model.ModelSettings.RegenerationEnabled)
            {
                // (1) remove all saplings:
                SaplingCellRunner saplingRunner = new(model.Landscape, standID);
                for (SaplingCell saplingCell = saplingRunner.MoveNext(); saplingCell != null; saplingCell = saplingRunner.MoveNext())
                {
                    existingSaplingsRemoved += saplingCell.GetOccupiedSlotCount();
                    saplingRunner.RU.ClearSaplings(saplingCell, true);
                }

                // (2) load saplings from database
                SqliteCommand saplingQuery = new(String.Format("select posx, posy, species_index, age, height, stress_years, flags " +
                                                               "from saplings_stand where standID={0}", standID), db);
                using SqliteDataReader saplingReader = saplingQuery.ExecuteReader();
                while (saplingReader.Read())
                {
                    PointF coord = model.Landscape.Environment.GisGrid.GisToModel(new PointF(saplingReader.GetInt32(0), saplingReader.GetInt32(1)));
                    if (!extent.Contains(coord))
                    {
                        continue;
                    }
                    SaplingCell saplingCell = model.Landscape.GetSaplingCell(model.Landscape.LightGrid.GetCellIndex(coord), true, out ResourceUnit _);
                    if (saplingCell == null)
                    {
                        continue;
                    }
                    Sapling sapling = saplingCell.AddSaplingIfSlotFree(saplingReader.GetFloat(4), saplingReader.GetInt32(3), saplingReader.GetInt32(2));
                    if (sapling != null)
                    {
                        sapling.StressYears = saplingReader.GetByte(5);
                        sapling.IsSprout = saplingReader.GetBoolean(6);
                    }
                    saplingsAdded++;
                }

            }

            // clean up
            // model.RemoveDeadTreesAndRecalculateStandStatistics(true); TODO: why was this present in C++; loading a snapshot shouldn't modify state?
            Debug.WriteLine("Load snapshot for stand " + standID + ": added " + treesAdded + " trees, saplings (removed/loaded): " + existingSaplingsRemoved + "/" + saplingsAdded);
            return true;
        }

        private static void SaveTrees(Model model, SqliteConnection db)
        {
            using SqliteTransaction treeTransaction = db.BeginTransaction();
            SqliteCommand treeInsert = new(String.Format("insert into trees (ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " +
                                                         "values (:id, :index, :x, :y, :spec, :age, :h, :d, :la, :opa, :mfol, :mwood, :mfr, :mcr, :npp, :si)"), db);
            treeInsert.Parameters.Add(":id", SqliteType.Integer);
            treeInsert.Parameters.Add(":index", SqliteType.Integer);
            treeInsert.Parameters.Add(":x", SqliteType.Integer);
            treeInsert.Parameters.Add(":y", SqliteType.Integer);
            treeInsert.Parameters.Add(":spec", SqliteType.Text);
            treeInsert.Parameters.Add(":age", SqliteType.Integer);
            treeInsert.Parameters.Add(":h", SqliteType.Integer);
            treeInsert.Parameters.Add(":d", SqliteType.Integer);
            treeInsert.Parameters.Add(":la", SqliteType.Integer);
            treeInsert.Parameters.Add(":opa", SqliteType.Integer);
            treeInsert.Parameters.Add(":mfol", SqliteType.Integer);
            treeInsert.Parameters.Add(":mwood", SqliteType.Integer);
            treeInsert.Parameters.Add(":mfr", SqliteType.Integer);
            treeInsert.Parameters.Add(":mcr", SqliteType.Integer);
            treeInsert.Parameters.Add(":npp", SqliteType.Integer);
            treeInsert.Parameters.Add(":si", SqliteType.Integer);

            AllTreesEnumerator allTreeEnumerator = new(model.Landscape);
            while (allTreeEnumerator.MoveNext())
            {
                Trees trees = allTreeEnumerator.CurrentTrees;
                int treeIndex = allTreeEnumerator.CurrentTreeIndex;
                treeInsert.Parameters[0].Value = trees.Tag[treeIndex];
                treeInsert.Parameters[1].Value = trees.RU.ResourceUnitGridIndex;
                treeInsert.Parameters[2].Value = trees.LightCellPosition[treeIndex].X;
                treeInsert.Parameters[3].Value = trees.LightCellPosition[treeIndex].Y;
                treeInsert.Parameters[4].Value = trees.Species.ID;
                treeInsert.Parameters[5].Value = trees.Age[treeIndex];
                treeInsert.Parameters[6].Value = trees.Height[treeIndex];
                treeInsert.Parameters[7].Value = trees.Dbh[treeIndex];
                treeInsert.Parameters[8].Value = trees.LeafArea[treeIndex];
                treeInsert.Parameters[9].Value = trees.Opacity[treeIndex];
                treeInsert.Parameters[10].Value = trees.FoliageMass[treeIndex];
                treeInsert.Parameters[11].Value = trees.StemMass[treeIndex];
                treeInsert.Parameters[12].Value = trees.FineRootMass[treeIndex];
                treeInsert.Parameters[13].Value = trees.CoarseRootMass[treeIndex];
                treeInsert.Parameters[14].Value = trees.NppReserve[treeIndex];
                treeInsert.Parameters[15].Value = trees.StressIndex[treeIndex];
                treeInsert.ExecuteNonQuery();
            }
            treeTransaction.Commit();
        }

        private void LoadTrees(Model model, SqliteConnection db)
        {
            #if DEBUG
            foreach (ResourceUnit ruInList in model.Landscape.ResourceUnits)
            {
                Debug.Assert(ruInList.Trees.TreesBySpeciesID.Count == 0);
            }
            #endif

            int ru_index = -1;
            int new_ru;
            int offsetX = 0, offsetY = 0;
            ResourceUnit ru = null;
            int n = 0, ntotal = 0;
            // load the trees from the database
            using SqliteCommand treeQuery = new("select ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex from trees", db);
            using SqliteDataReader treeReader = treeQuery.ExecuteReader();
            while (treeReader.Read())
            {
                new_ru = treeReader.GetInt32(1);
                ++ntotal;
                if (new_ru != ru_index)
                {
                    ru_index = new_ru;
                    ru = mResourceUnits[ru_index];
                    if (ru != null)
                    {
                        offsetX = ru.TopLeftLightPosition.X;
                        offsetY = ru.TopLeftLightPosition.Y;
                    }
                }
                if (ru == null)
                {
                    continue;
                }

                // add new tree to the tree list
                TreeSpecies species = ru.Trees.TreeSpeciesSet[treeReader.GetString(4)];
                int treeIndex = ru.Trees.AddTree(model.Landscape, species.ID);
                Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[species.ID];
                treesOfSpecies.Tag[treeIndex] = treeReader.GetInt32(0);
                treesOfSpecies.LightCellPosition[treeIndex] = new Point(offsetX + treeReader.GetInt32(2) % Constant.LightCellsPerRUsize, // TODO: why modulus?
                                                                        offsetY + treeReader.GetInt32(3) % Constant.LightCellsPerRUsize);
                treesOfSpecies.Species = species ?? throw new NotSupportedException("Invalid species.");
                treesOfSpecies.Age[treeIndex] = treeReader.GetInt32(5);
                treesOfSpecies.Height[treeIndex] = treeReader.GetFloat(6);
                treesOfSpecies.Dbh[treeIndex] = treeReader.GetFloat(7);
                treesOfSpecies.LeafArea[treeIndex] = treeReader.GetFloat(8);
                treesOfSpecies.Opacity[treeIndex] = treeReader.GetFloat(9);
                treesOfSpecies.FoliageMass[treeIndex] = treeReader.GetFloat(10);
                treesOfSpecies.StemMass[treeIndex] = treeReader.GetFloat(11);
                treesOfSpecies.FineRootMass[treeIndex] = treeReader.GetFloat(12);
                treesOfSpecies.CoarseRootMass[treeIndex] = treeReader.GetFloat(13);
                treesOfSpecies.NppReserve[treeIndex] = treeReader.GetFloat(14);
                treesOfSpecies.StressIndex[treeIndex] = treeReader.GetFloat(15);
                treesOfSpecies.Stamp[treeIndex] = species.GetStamp(treesOfSpecies.Dbh[treeIndex], treesOfSpecies.Height[treeIndex]);

                if (++n % 10000 == 0)
                {
                    Debug.WriteLine(n + " trees loaded...");
                }
            }

            Debug.WriteLine("Snapshot: finished trees. N=" + n + " from trees in snapshot: " + ntotal);
        }

        private static void SaveSoil(Model model, SqliteConnection db)
        {
            using SqliteTransaction soilTransaction = db.BeginTransaction();
            using SqliteCommand soilInsert = new(String.Format("insert into soil (RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack) " +
                                                               "values (@idx, @kyl, @kyr, @inLabC, @iLN, @iLP, @iRC, @iRN, @iRP, @ylc, @yln, @ylp, @yrc, @yrn, @yrp, @somc, @somn, @wc, @swe)"), db, soilTransaction);
            soilInsert.Parameters.Add("@idx", SqliteType.Integer);
            soilInsert.Parameters.Add("@kyl", SqliteType.Real);
            soilInsert.Parameters.Add("@kyr", SqliteType.Real);
            soilInsert.Parameters.Add("@inLabC", SqliteType.Real);
            soilInsert.Parameters.Add("@iLN", SqliteType.Real);
            soilInsert.Parameters.Add("@iLP", SqliteType.Real);
            soilInsert.Parameters.Add("@iRC", SqliteType.Real);
            soilInsert.Parameters.Add("@iRN", SqliteType.Real);
            soilInsert.Parameters.Add("@iRP", SqliteType.Real);
            soilInsert.Parameters.Add("@ylc", SqliteType.Real);
            soilInsert.Parameters.Add("@yln", SqliteType.Real);
            soilInsert.Parameters.Add("@ylp", SqliteType.Real);
            soilInsert.Parameters.Add("@yrc", SqliteType.Real);
            soilInsert.Parameters.Add("@yrn", SqliteType.Real);
            soilInsert.Parameters.Add("@yrp", SqliteType.Real);
            soilInsert.Parameters.Add("@somc", SqliteType.Real);
            soilInsert.Parameters.Add("@somn", SqliteType.Real);
            soilInsert.Parameters.Add("@wc", SqliteType.Real);
            soilInsert.Parameters.Add("@swe", SqliteType.Real);

            //int n = 0;
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                World.ResourceUnitSoil soil = ru.Soil;
                if (soil != null)
                {
                    soilInsert.Parameters[0].Value = ru.ResourceUnitGridIndex;
                    soilInsert.Parameters[1].Value = soil.Parameters.Kyl;
                    soilInsert.Parameters[2].Value = soil.Parameters.Kyr;
                    soilInsert.Parameters[3].Value = soil.InputLabile.C;
                    soilInsert.Parameters[4].Value = soil.InputLabile.N;
                    soilInsert.Parameters[5].Value = soil.InputLabile.DecompositionRate;
                    soilInsert.Parameters[6].Value = soil.InputRefractory.C;
                    soilInsert.Parameters[7].Value = soil.InputRefractory.N;
                    soilInsert.Parameters[8].Value = soil.InputRefractory.DecompositionRate;
                    soilInsert.Parameters[9].Value = soil.YoungLabile.C;
                    soilInsert.Parameters[10].Value = soil.YoungLabile.N;
                    soilInsert.Parameters[11].Value = soil.YoungLabile.DecompositionRate;
                    soilInsert.Parameters[12].Value = soil.YoungRefractory.C;
                    soilInsert.Parameters[13].Value = soil.YoungRefractory.N;
                    soilInsert.Parameters[14].Value = soil.YoungRefractory.DecompositionRate;
                    soilInsert.Parameters[15].Value = soil.OrganicMatter.C;
                    soilInsert.Parameters[16].Value = soil.OrganicMatter.N;
                    soilInsert.Parameters[17].Value = ru.WaterCycle.CurrentSoilWaterContent;
                    soilInsert.Parameters[18].Value = ru.WaterCycle.CurrentSnowWaterEquivalent();
                    soilInsert.ExecuteNonQuery();

                    //if (++n % 1000 == 0)
                    //{
                    //    Debug.WriteLine(n + "soil resource units saved...");
                    //}
                }
            }

            soilTransaction.Commit();
            //Debug.WriteLine("Snapshot: finished Soil. N=" + n);
        }

        private void LoadSoil(SqliteConnection db)
        {
            using SqliteCommand soilQuery = new("select RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack from soil", db);
            int ru_index = -1;
            ResourceUnit ru = null;
            //int n = 0;
            using SqliteDataReader soilReader = soilQuery.ExecuteReader();
            while (soilReader.Read())
            {
                ru_index = soilReader.GetInt32(0);
                ru = mResourceUnits[ru_index];
                if (ru == null)
                {
                    continue;
                }
                World.ResourceUnitSoil soil = ru.Soil;
                if (soil == null)
                {
                    throw new NotSupportedException("loadSoil: trying to load soil data but soil module is disabled.");
                }
                soil.Parameters.Kyl = soilReader.GetFloat(1);
                soil.Parameters.Kyr = soilReader.GetFloat(2);
                soil.InputLabile.C = soilReader.GetFloat(3);
                soil.InputLabile.N = soilReader.GetFloat(4);
                soil.InputLabile.DecompositionRate = soilReader.GetFloat(5);
                soil.InputRefractory.C = soilReader.GetFloat(6);
                soil.InputRefractory.N = soilReader.GetFloat(7);
                soil.InputRefractory.DecompositionRate = soilReader.GetFloat(8);
                soil.YoungLabile.C = soilReader.GetFloat(9);
                soil.YoungLabile.N = soilReader.GetFloat(10);
                soil.YoungLabile.DecompositionRate = soilReader.GetFloat(11);
                soil.YoungRefractory.C = soilReader.GetFloat(12);
                soil.YoungRefractory.N = soilReader.GetFloat(13);
                soil.YoungRefractory.DecompositionRate = soilReader.GetFloat(14);
                soil.OrganicMatter.C = soilReader.GetFloat(15);
                soil.OrganicMatter.N = soilReader.GetFloat(16);
                ru.WaterCycle.SetContent(soilReader.GetFloat(17), soilReader.GetFloat(18));

                //if (++n % 1000 == 0)
                //{
                //    Debug.WriteLine(n + " soil units loaded...");
                //}
            }

            //Debug.WriteLine("Snapshot: finished soil. N=" + n);
        }

        private static void SaveSnags(Model model, SqliteConnection db)
        {
            using SqliteTransaction snagTransaction = db.BeginTransaction();
            SqliteCommand snagInsert = new(String.Format("insert into snag(RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, " +
                                                         "totalSWDC, totalSWDN, NSnags1, NSnags2, NSnags3, dbh1, dbh2, dbh3, height1, height2, height3, " +
                                                         "volume1, volume2, volume3, tsd1, tsd2, tsd3, ksw1, ksw2, ksw3, halflife1, halflife2, halflife3, " +
                                                         "branch1C, branch1N, branch2C, branch2N, branch3C, branch3N, branch4C, branch4N, branch5C, branch5N, branchIndex) " +
                                                         "values (?,?,?,?,?,?,?,?, " +
                                                         "?,?,?,?,?,?,?,?,?,?,?," +
                                                         "?,?,?,?,?,?,?,?,?,?,?,?," +
                                                         "?,?,?,?,?,?,?,?,?,?,?)"), db, snagTransaction);
            snagInsert.Parameters.Add("RUIndex", SqliteType.Integer);
            snagInsert.Parameters.Add("climateFactor", SqliteType.Real);
            snagInsert.Parameters.Add("SWD1C", SqliteType.Real);
            snagInsert.Parameters.Add("SWD1N", SqliteType.Real);
            snagInsert.Parameters.Add("SWD2C", SqliteType.Real);
            snagInsert.Parameters.Add("SWD2N", SqliteType.Real);
            snagInsert.Parameters.Add("SWD3C", SqliteType.Real);
            snagInsert.Parameters.Add("SWD3N", SqliteType.Real);
            snagInsert.Parameters.Add("totalSWDC", SqliteType.Real);
            snagInsert.Parameters.Add("totalSWDN", SqliteType.Real);
            snagInsert.Parameters.Add("NSnags1", SqliteType.Real);
            snagInsert.Parameters.Add("NSnags2", SqliteType.Real);
            snagInsert.Parameters.Add("NSnags3", SqliteType.Real);
            snagInsert.Parameters.Add("dbh1", SqliteType.Real);
            snagInsert.Parameters.Add("dbh2", SqliteType.Real);
            snagInsert.Parameters.Add("dbh3", SqliteType.Real);
            snagInsert.Parameters.Add("height1", SqliteType.Real);
            snagInsert.Parameters.Add("height2", SqliteType.Real);
            snagInsert.Parameters.Add("height3", SqliteType.Real);
            snagInsert.Parameters.Add("volume1", SqliteType.Real);
            snagInsert.Parameters.Add("volume2", SqliteType.Real);
            snagInsert.Parameters.Add("volume3", SqliteType.Real);
            snagInsert.Parameters.Add("tsd1", SqliteType.Real);
            snagInsert.Parameters.Add("tsd2", SqliteType.Real);
            snagInsert.Parameters.Add("tsd3", SqliteType.Real);
            snagInsert.Parameters.Add("ksw1", SqliteType.Real);
            snagInsert.Parameters.Add("ksw2", SqliteType.Real);
            snagInsert.Parameters.Add("ksw3", SqliteType.Real);
            snagInsert.Parameters.Add("halflife1", SqliteType.Real);
            snagInsert.Parameters.Add("halflife2", SqliteType.Real);
            snagInsert.Parameters.Add("halflife3", SqliteType.Real);
            snagInsert.Parameters.Add("branch1C", SqliteType.Real);
            snagInsert.Parameters.Add("branch1N", SqliteType.Real);
            snagInsert.Parameters.Add("branch2C", SqliteType.Real);
            snagInsert.Parameters.Add("branch2N", SqliteType.Real);
            snagInsert.Parameters.Add("branch3C", SqliteType.Real);
            snagInsert.Parameters.Add("branch3N", SqliteType.Real);
            snagInsert.Parameters.Add("branch4C", SqliteType.Real);
            snagInsert.Parameters.Add("branch4N", SqliteType.Real);
            snagInsert.Parameters.Add("branch5C", SqliteType.Real);
            snagInsert.Parameters.Add("branch5N", SqliteType.Real);
            snagInsert.Parameters.Add("branchIndex", SqliteType.Integer);

            int n = 0;
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                Tree.Snags snags = ru.Snags;
                if (snags == null)
                {
                    continue;
                }
                snagInsert.Parameters[0].Value = snags.RU.ResourceUnitGridIndex;
                snagInsert.Parameters[1].Value = snags.ClimateFactor;
                snagInsert.Parameters[2].Value = snags.StandingWoodyDebrisByClass[0].C;
                snagInsert.Parameters[3].Value = snags.StandingWoodyDebrisByClass[0].N;
                snagInsert.Parameters[4].Value = snags.StandingWoodyDebrisByClass[1].C;
                snagInsert.Parameters[5].Value = snags.StandingWoodyDebrisByClass[1].N;
                snagInsert.Parameters[7].Value = snags.StandingWoodyDebrisByClass[2].C;
                snagInsert.Parameters[8].Value = snags.StandingWoodyDebrisByClass[2].N;
                snagInsert.Parameters[9].Value = snags.TotalStanding.C;
                snagInsert.Parameters[10].Value = snags.TotalStanding.N;
                snagInsert.Parameters[11].Value = snags.NumberOfSnagsByClass[0];
                snagInsert.Parameters[12].Value = snags.NumberOfSnagsByClass[1];
                snagInsert.Parameters[13].Value = snags.NumberOfSnagsByClass[2];
                snagInsert.Parameters[14].Value = snags.AverageDbhByClass[0];
                snagInsert.Parameters[15].Value = snags.AverageDbhByClass[1];
                snagInsert.Parameters[16].Value = snags.AverageDbhByClass[2];
                snagInsert.Parameters[17].Value = snags.AverageHeightByClass[0];
                snagInsert.Parameters[18].Value = snags.AverageHeightByClass[1];
                snagInsert.Parameters[19].Value = snags.AverageHeightByClass[2];
                snagInsert.Parameters[20].Value = snags.AverageVolumeByClass[0];
                snagInsert.Parameters[21].Value = snags.AverageVolumeByClass[1];
                snagInsert.Parameters[22].Value = snags.AverageVolumeByClass[2];
                snagInsert.Parameters[23].Value = snags.TimeSinceDeathByClass[0];
                snagInsert.Parameters[24].Value = snags.TimeSinceDeathByClass[21];
                snagInsert.Parameters[25].Value = snags.TimeSinceDeathByClass[2];
                snagInsert.Parameters[26].Value = snags.StemDecompositionRateByClass[0];
                snagInsert.Parameters[27].Value = snags.StemDecompositionRateByClass[1];
                snagInsert.Parameters[28].Value = snags.StemDecompositionRateByClass[2];
                snagInsert.Parameters[29].Value = snags.HalfLifeByClass[0];
                snagInsert.Parameters[30].Value = snags.HalfLifeByClass[30];
                snagInsert.Parameters[31].Value = snags.HalfLifeByClass[2];
                snagInsert.Parameters[32].Value = snags.BranchesAndCoarseRootsByYear[0].C;
                snagInsert.Parameters[33].Value = snags.BranchesAndCoarseRootsByYear[0].N;
                snagInsert.Parameters[34].Value = snags.BranchesAndCoarseRootsByYear[1].C;
                snagInsert.Parameters[35].Value = snags.BranchesAndCoarseRootsByYear[1].N;
                snagInsert.Parameters[36].Value = snags.BranchesAndCoarseRootsByYear[2].C;
                snagInsert.Parameters[37].Value = snags.BranchesAndCoarseRootsByYear[2].N;
                snagInsert.Parameters[38].Value = snags.BranchesAndCoarseRootsByYear[3].C;
                snagInsert.Parameters[39].Value = snags.BranchesAndCoarseRootsByYear[3].N;
                snagInsert.Parameters[40].Value = snags.BranchesAndCoarseRootsByYear[4].C;
                snagInsert.Parameters[41].Value = snags.BranchesAndCoarseRootsByYear[4].N;
                snagInsert.Parameters[42].Value = snags.BranchCounter;
                snagInsert.ExecuteNonQuery();

                if (++n % 1000 == 0)
                {
                    Debug.WriteLine(n + " snags saved...");
                }
            }

            snagTransaction.Commit();
            Debug.WriteLine("Snapshot: finished Snags. N=" + n);
        }

        private void LoadSnags(SqliteConnection db)
        {
            //int n = 0;
            using SqliteCommand snagQuery = new("select RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, totalSWDC, totalSWDN, NSnags1, NSnags2, NSnags3, dbh1, dbh2, dbh3, height1, height2, height3, volume1, volume2, volume3, tsd1, tsd2, tsd3, ksw1, ksw2, ksw3, halflife1, halflife2, halflife3, branch1C, branch1N, branch2C, branch2N, branch3C, branch3N, branch4C, branch4N, branch5C, branch5N, branchIndex from snag", db);
            using SqliteDataReader snagReader = snagQuery.ExecuteReader();
            while (snagReader.Read())
            {
                //++n;

                int columnIndex = 0;
                int ruIndex = snagReader.GetInt32(columnIndex++);
                ResourceUnit ru = mResourceUnits[ruIndex];
                if (ru == null)
                {
                    continue;
                }
                Tree.Snags snags = ru.Snags;
                if (snags == null)
                {
                    continue;
                }
                snags.ClimateFactor = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[0].C = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[0].N = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[1].C = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[1].N = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[2].C = snagReader.GetFloat(columnIndex++);
                snags.StandingWoodyDebrisByClass[2].N = snagReader.GetFloat(columnIndex++);
                snags.TotalStanding.C = snagReader.GetFloat(columnIndex++);
                snags.TotalStanding.N = snagReader.GetFloat(columnIndex++);
                snags.NumberOfSnagsByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.NumberOfSnagsByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.NumberOfSnagsByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.AverageDbhByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.AverageDbhByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.AverageDbhByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.AverageHeightByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.AverageHeightByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.AverageHeightByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.AverageVolumeByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.AverageVolumeByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.AverageVolumeByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.TimeSinceDeathByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.TimeSinceDeathByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.TimeSinceDeathByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.StemDecompositionRateByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.StemDecompositionRateByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.StemDecompositionRateByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.HalfLifeByClass[0] = snagReader.GetFloat(columnIndex++);
                snags.HalfLifeByClass[1] = snagReader.GetFloat(columnIndex++);
                snags.HalfLifeByClass[2] = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[0].C = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[0].N = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[1].C = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[1].N = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[2].C = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[2].N = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[3].C = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[3].N = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[4].C = snagReader.GetFloat(columnIndex++);
                snags.BranchesAndCoarseRootsByYear[4].N = snagReader.GetFloat(columnIndex++);
                snags.BranchCounter = snagReader.GetInt32(columnIndex++);

                //if (++n % 1000 == 0)
                //{
                //    Debug.WriteLine(n + " snags loaded...");
                //}
            }

            //Debug.WriteLine("Snapshot: finished snags. N=" + n);
        }

        private static void SaveSaplings(SqliteConnection db)
        {
            using SqliteCommand q = new("insert into saplings (RUindex, species, posx, posy, age, height, stress_years) values (?,?,?,?,?,?,?)", db);
            // int n = 0;
            throw new NotSupportedException("saveSaplings() not implemented");
            //    foreach (ResourceUnit *ru, globalSettings().model().ruList()) {
            //        foreach (ResourceUnitSpecies *rus, ru.ruSpecies()) {
            //            Sapling &sap = rus.sapling();
            //            if (sap.saplings().isEmpty())
            //                continue;
            //            foreach (SaplingTreeOld &t, sap.saplings()) {
            //                if (!t.pixel)
            //                    continue;
            //                q.Parameters[1].Value = ru.index());
            //                q.Parameters[1].Value = rus.species().id());
            //                QPoint p=t.coords();
            //                q.Parameters[1].Value = p.x());
            //                q.Parameters[1].Value = p.y());
            //                q.Parameters[1].Value = t.age.age);
            //                q.Parameters[1].Value = t.height);
            //                q.Parameters[1].Value = t.age.stress_years);
            //                if (!q.exec()) {
            //                    throw new NotSupportedException(String.Format("saveSaplings: execute:") + q.lastError().text());
            //                }
            //                if (++n % 10000 == 0) {
            //                    Debug.WriteLine(n + "saplings saved...";
            //                    QCoreApplication::processEvents();
            //                }
            //            }
            //        }
            //    }
            //Debug.WriteLine("Snapshot: finished saplings. N=" + n);
        }

        private void LoadSaplings(Model model, SqliteConnection db)
        {
            SqliteCommand saplingQuery = new("select RUindex, species, posx, posy, age, height, stress_years from saplings", db);

            // clear all saplings in the whole project area: added for testing/debugging
            //    foreach( ResourceUnit *ru, globalSettings().model().ruList()) {
            //        foreach (ResourceUnitSpecies *rus, ru.ruSpecies()) {
            //            rus.changeSapling().clear();
            //            rus.changeSapling().clearStatistics();
            //        }
            //    }

            //int n = 0, ntotal = 0;
            SqliteDataReader saplingReader = saplingQuery.ExecuteReader();
            while (saplingReader.Read())
            {
                int columnIndex = 0;
                int ruIndex = saplingReader.GetInt32(columnIndex++);
                ResourceUnit ru = this.mResourceUnits[ruIndex];
                if (ru == null)
                {
                    throw new NotSupportedException("Resource unit grid index invalid.");
                }
                TreeSpecies species = ru.Trees.TreeSpeciesSet[saplingReader.GetString(columnIndex++)];
                if (species == null)
                {
                    throw new NotSupportedException("Species ID not found.");
                }

                int ruOriginX = ru.TopLeftLightPosition.X;
                int ruOriginY = ru.TopLeftLightPosition.Y;
                int lightIndexX = ruOriginX + saplingReader.GetInt32(columnIndex++) % Constant.LightCellsPerRUsize;
                int lightIndexY = ruOriginY + saplingReader.GetInt32(columnIndex++) % Constant.LightCellsPerRUsize;

                SaplingCell saplingCell = model.Landscape.GetSaplingCell(new Point(lightIndexX, lightIndexY), true, out _);
                if (saplingCell == null)
                {
                    continue;
                }

                int age = saplingReader.GetInt32(columnIndex++);
                Sapling sapling = saplingCell.AddSaplingIfSlotFree(saplingReader.GetFloat(columnIndex++), age, species.Index);
                if (sapling == null)
                {
                    continue;
                }
                sapling.StressYears = saplingReader.GetByte(columnIndex++);
                //++ntotal;

                //if (n < 10000000 && ++n % 10000 == 0)
                //{
                //    Debug.WriteLine(n + " saplings loaded...");
                //}
                //if (n >= 10000000 && ++n % 1000000 == 0)
                //{
                //    Debug.WriteLine(n + " saplings loaded...");
                //}
            }
            //Debug.WriteLine("Snapshot: finished loading saplings. N=" + n + "from N in snapshot:" + ntotal);
        }
    }
}
