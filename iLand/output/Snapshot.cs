using iLand.core;
using iLand.tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.output
{
    internal class Snapshot : Output
    {
        private readonly Dictionary<int, ResourceUnit> mRUHash;

        public Snapshot()
        {
            this.mRUHash = new Dictionary<int, ResourceUnit>();
        }

        private bool OpenDatabase(string fileName, bool read)
        {
            if (!GlobalSettings.Instance.SetupDatabaseConnection("snapshot", fileName, read))
            {
                throw new NotSupportedException("Snapshot:createDatabase: database could not be created / opened");
            }
            if (!read)
            {
                SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
                using SqliteTransaction transaction = db.BeginTransaction();
                // create tables
                // trees
                SqliteCommand dropTrees = new SqliteCommand("drop table trees", db, transaction);
                dropTrees.ExecuteNonQuery();
                SqliteCommand createTrees = new SqliteCommand("create table trees (ID integer, RUindex integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)", db, transaction);
                createTrees.ExecuteNonQuery();
                // soil
                SqliteCommand dropSoil = new SqliteCommand("drop table soil", db, transaction);
                dropSoil.ExecuteNonQuery();
                SqliteCommand createSoil = new SqliteCommand("create table soil (RUindex integer, kyl real, kyr real, inLabC real, inLabN real, inLabP real, inRefC real, inRefN real, inRefP real, YLC real, YLN real, YLP real, YRC real, YRN real, YRP real, SOMC real, SOMN real, WaterContent, SnowPack real)", db, transaction);
                createSoil.ExecuteNonQuery();
                // snag
                SqliteCommand dropSnag = new SqliteCommand("drop table snag", db, transaction);
                dropSoil.ExecuteNonQuery();
                SqliteCommand createSnag = new SqliteCommand("create table snag(RUIndex integer, climateFactor real, SWD1C real, SWD1N real, SWD2C real, SWD2N real, SWD3C real, SWD3N real, " +
                       "totalSWDC real, totalSWDN real, NSnags1 real, NSnags2 real, NSnags3 real, dbh1 real, dbh2 real, dbh3 real, height1 real, height2 real, height3 real, " +
                       "volume1 real, volume2 real, volume3 real, tsd1 real, tsd2 real, tsd3 real, ksw1 real, ksw2 real, ksw3 real, halflife1 real, halflife2 real, halflife3 real, " +
                       "branch1C real, branch1N real, branch2C real, branch2N real, branch3C real, branch3N real, branch4C real, branch4N real, branch5C real, branch5N real, branchIndex integer)", db, transaction);
                createSnag.ExecuteNonQuery();
                // saplings/regeneration
                SqliteCommand dropSaplings = new SqliteCommand("drop table saplings", db, transaction);
                dropSoil.ExecuteNonQuery();
                SqliteCommand createSaplings = new SqliteCommand("create table saplings (RUindex integer, species text, posx integer, posy integer, age integer, height float, stress_years integer)", db, transaction);
                createSnag.ExecuteNonQuery();
                transaction.Commit();
                Debug.WriteLine("Snapshot - tables created. Database " + fileName);
            }
            return true;
        }

        private bool OpenStandDatabase(string file_name, bool read)
        {
            if (!GlobalSettings.Instance.SetupDatabaseConnection("snapshotstand", file_name, read))
            {
                throw new NotSupportedException("Snapshot:createDatabase: database could not be created / opened");
            }
            return true;
        }

        public bool CreateSnapshot(string file_name)
        {
            OpenDatabase(file_name, false);
            // save the trees
            SaveTrees();
            // save soil pools
            SaveSoil();
            // save snags / deadwood pools
            SaveSnags();
            // save saplings
            SaveSaplings();

            // save a grid of the indices
            FileInfo fi = new FileInfo(file_name);
            string grid_file = Path.Combine(Path.GetDirectoryName(fi.FullName), Path.GetFileNameWithoutExtension(fi.FullName) + ".asc");

            Grid<ResourceUnit> ruGrid = GlobalSettings.Instance.Model.ResourceUnitGrid;
            Grid<double> index_grid = new Grid<double>(ruGrid.CellSize, ruGrid.SizeX, ruGrid.SizeY);
            index_grid.Setup(GlobalSettings.Instance.Model.ResourceUnitGrid.PhysicalSize, GlobalSettings.Instance.Model.ResourceUnitGrid.CellSize);
            RUWrapper ru_wrap = new RUWrapper();
            Expression ru_value = new Expression("index", ru_wrap);
            for (int index = 0; index < ruGrid.Count; ++index)
            {
                ResourceUnit ru = ruGrid[index];
                if (ru != null)
                {
                    ru_wrap.ResourceUnit = ru;
                    index_grid[index] = ru_value.Execute();
                }
                else
                {
                    index_grid[index] = -1.0;
                }
            }
            string grid_text = Grid.ToEsriRaster(index_grid);
            Helper.SaveToTextFile(grid_file, grid_text);
            Debug.WriteLine("saved grid to " + grid_file);

            return true;
        }

        public bool LoadSnapshot(string file_name)
        {
            using DebugTimer t = new DebugTimer("loadSnapshot");
            OpenDatabase(file_name, true);

            FileInfo fi = new FileInfo(file_name);
            string grid_file = Path.Combine(Path.GetDirectoryName(fi.FullName), Path.GetFileNameWithoutExtension(fi.FullName) + ".asc");
            GisGrid grid = new GisGrid();
            mRUHash.Clear();

            if (!grid.LoadFromFile(grid_file))
            {
                Debug.WriteLine("loading of snapshot: not a valid grid file (containing resource unit inidices) expected at: " + grid_file);
                Grid<ResourceUnit> ruGrid = GlobalSettings.Instance.Model.ResourceUnitGrid;
                for (int index = 0; index < ruGrid.Count; ++index)
                {
                    ResourceUnit ru = ruGrid[index];
                    if (ru != null)
                    {
                        mRUHash.Add(index, ru);
                    }
                }
            }
            else
            {
                // setup link between resource unit index and index grid:
                // store for each resource unit *in the snapshot database* the corresponding
                // resource unit index of the *current* simulation.
                PointF to = GisGrid.WorldToModel(grid.Origin);
                if ((to.X % Constant.RUSize) != 0.0 || (to.Y % Constant.RUSize) != 0.0)
                {
                    PointF world_offset = GisGrid.ModelToWorld(new PointF(0.0F, 0.0F));
                    throw new NotSupportedException(String.Format("Loading of the snapshot '{0}' failed: The offset from the current location of the project ({3}/{4}) " +
                                             "is not a multiple of the resource unit size (100m) relative to grid of the snapshot (origin-x: {1}, origin-y: {2}).", file_name,
                                     grid.Origin.X, grid.Origin.Y, world_offset.X, world_offset.Y));
                }

                Grid<ResourceUnit> rugrid = GlobalSettings.Instance.Model.ResourceUnitGrid;
                for (int i = 0; i < rugrid.Count; ++i)
                {
                    ResourceUnit ru = rugrid[i];
                    if (ru != null && ru.Index > -1)
                    {
                        int value = (int)grid.GetValue(rugrid.GetCellCenterPoint(i));
                        if (value > -1)
                        {
                            mRUHash[value] = ru;
                        }
                    }
                }
            }

            LoadTrees();
            LoadSoil();
            LoadSnags();
            // load saplings only when regeneration is enabled (this can save a lot of time)
            if (GlobalSettings.Instance.Model.Settings.RegenerationEnabled)
            {
                LoadSaplings();
                //loadSaplingsOld();
            }

            // after changing the trees, do a complete apply/read pattern cycle over the landscape...
            GlobalSettings.Instance.Model.OnlyApplyLightPattern();
            Debug.WriteLine("applied light pattern...");

            // refresh the stand statistics
            foreach (ResourceUnit ru in GlobalSettings.Instance.Model.ResourceUnits)
            {
                ru.RecreateStandStatistics(true); // true: recalculate statistics
            }

            Debug.WriteLine("created stand statistics...");
            Debug.WriteLine("loading of snapshot completed.");

            return true;
        }

        public bool SaveStandSnapshot(int stand_id, MapGrid stand_grid, string file_name)
        {
            // Check database
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshotstand();
            if (db.State != ConnectionState.Open)
            {
                OpenStandDatabase(GlobalSettings.Instance.Path(file_name), false);

                List<string> tableNames = new List<string>();
                SqliteCommand selectTableNames = new SqliteCommand("SELECT name FROM sqlite_schema WHERE type='table'", db);
                for (SqliteDataReader tableNameReader = selectTableNames.ExecuteReader(); tableNameReader.HasRows; tableNameReader.Read())
                {
                    tableNames.Add(tableNameReader.GetString(0));
                }

                // check if tree/sapling tables are already present
                if (tableNames.Contains("trees_stand") == false || tableNames.Contains("saplings_stand") == false)
                {
                    // create tables
                    using SqliteTransaction tablesTransaction = db.BeginTransaction();
                    using SqliteCommand dropTrees = new SqliteCommand("drop table trees_stand", db, tablesTransaction);
                    dropTrees.ExecuteNonQuery();
                    // trees
                    using SqliteCommand createTrees = new SqliteCommand("create table trees_stand (standID integer, ID integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)", db, tablesTransaction);
                    createTrees.ExecuteNonQuery();
                    // saplings/regeneration
                    using SqliteCommand dropSaplings = new SqliteCommand("drop table saplings_stand", db, tablesTransaction);
                    dropSaplings.ExecuteNonQuery();
                    using SqliteCommand createSaplings = new SqliteCommand("create table saplings_stand (standID integer, posx integer, posy integer, species_index integer, age integer, height float, stress_years integer, flags integer)", db, tablesTransaction);
                    createSaplings.ExecuteNonQuery();
                    tablesTransaction.Commit();
                }
            }

            // save trees
            using (SqliteTransaction treesTransaction = db.BeginTransaction())
            {
                using SqliteCommand deleteStand = new SqliteCommand(String.Format("delete from trees_stand where standID={0}", stand_id), db, treesTransaction);
                deleteStand.ExecuteNonQuery();

                using SqliteCommand insertTree = new SqliteCommand("insert into trees_stand (standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " +
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

                PointF offset = GisGrid.ModelToWorld(new PointF(0.0F, 0.0F));
                List<Tree> tree_list = stand_grid.Trees(stand_id);
                for (int index = 0; index < tree_list.Count; ++index)
                {
                    Tree t = tree_list[index];
                    insertTree.Parameters[0].Value = (stand_id);
                    insertTree.Parameters[1].Value = t.ID;
                    insertTree.Parameters[2].Value = t.GetCellCenterPoint().X + offset.X;
                    insertTree.Parameters[3].Value = t.GetCellCenterPoint().Y + offset.Y;
                    insertTree.Parameters[4].Value = t.Species.ID;
                    insertTree.Parameters[5].Value = t.Age;
                    insertTree.Parameters[6].Value = t.Height;
                    insertTree.Parameters[7].Value = t.Dbh;
                    insertTree.Parameters[8].Value = t.LeafArea;
                    insertTree.Parameters[10].Value = t.mOpacity;
                    insertTree.Parameters[11].Value = t.FoliageMass;
                    insertTree.Parameters[12].Value = t.StemMass;
                    insertTree.Parameters[13].Value = t.FineRootMass;
                    insertTree.Parameters[14].Value = t.CoarseRootMass;
                    insertTree.Parameters[15].Value = t.mNPPReserve;
                    insertTree.Parameters[16].Value = t.StressIndex;
                    insertTree.ExecuteNonQuery();
                }
                treesTransaction.Commit();
            }

            // save saplings
            // loop over all pixels, only when regeneration is enabled
            if (GlobalSettings.Instance.Model.Settings.RegenerationEnabled)
            {
                using SqliteTransaction saplingTransaction = db.BeginTransaction();
                using SqliteCommand deleteSaplings = new SqliteCommand(String.Format("delete from saplings_stand where standID={0}", stand_id), db, saplingTransaction);
                deleteSaplings.ExecuteNonQuery();

                using SqliteCommand insertSapling = new SqliteCommand(String.Format("insert into saplings_stand (standID, posx, posy, species_index, age, height, stress_years, flags) " +
                                                                               "values (?,?,?,?,?,?,?,?)"), db, saplingTransaction);
                insertSapling.Parameters.Add("standID", SqliteType.Integer);
                insertSapling.Parameters.Add("posx", SqliteType.Real);
                insertSapling.Parameters.Add("posy", SqliteType.Real);
                insertSapling.Parameters.Add("species_index", SqliteType.Integer);
                insertSapling.Parameters.Add("age", SqliteType.Integer);
                insertSapling.Parameters.Add("height", SqliteType.Real);
                insertSapling.Parameters.Add("stress_years", SqliteType.Integer);
                insertSapling.Parameters.Add("flags", SqliteType.Integer);

                PointF offset = GisGrid.ModelToWorld(new PointF(0.0F, 0.0F));
                SaplingCellRunner scr = new SaplingCellRunner(stand_id, stand_grid);
                for (SaplingCell sc = scr.MoveNext(); sc != null; sc = scr.MoveNext())
                {
                    for (int i = 0; i < SaplingCell.SaplingCells; ++i)
                    {
                        if (sc.Saplings[i].IsOccupied())
                        {
                            insertSapling.Parameters[0].Value = stand_id;
                            PointF t = scr.CurrentCoordinate();
                            insertSapling.Parameters[1].Value = t.X + offset.X;
                            insertSapling.Parameters[2].Value = t.Y + offset.Y;
                            insertSapling.Parameters[3].Value = sc.Saplings[i].SpeciesIndex;
                            insertSapling.Parameters[4].Value = sc.Saplings[i].Age;
                            insertSapling.Parameters[5].Value = sc.Saplings[i].Height;
                            insertSapling.Parameters[6].Value = sc.Saplings[i].StressYears;
                            insertSapling.Parameters[7].Value = sc.Saplings[i].Flags;
                            insertSapling.ExecuteNonQuery();
                        }
                    }

                    saplingTransaction.Commit();
                }
            }

            return true;
        }

        public bool LoadStandSnapshot(int stand_id, MapGrid stand_grid, string file_name)
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshotstand();
            if (db.State != ConnectionState.Open)
            {
                OpenStandDatabase(GlobalSettings.Instance.Path(file_name), false);
            }

            // load trees
            // kill all living trees on the stand
            List<Tree> tree_list = stand_grid.Trees(stand_id);
            int n_removed = tree_list.Count;
            tree_list.Clear();

            // load from database
            RectangleF extent = GlobalSettings.Instance.Model.PhysicalExtent;
            using SqliteCommand treeQuery = new SqliteCommand(String.Format("select standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, " +
                           "foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex " +
                           "from trees_stand where standID={0}", stand_id), db);
            using SqliteDataReader treeReader = treeQuery.ExecuteReader();
            int n = 0;
            for (; treeReader.HasRows; treeReader.Read(), ++n)
            {
                PointF coord = GisGrid.WorldToModel(new PointF(treeReader.GetInt32(2), treeReader.GetInt32(3)));
                if (!extent.Contains(coord))
                {
                    continue;
                }
                ResourceUnit ru = GlobalSettings.Instance.Model.GetResourceUnit(coord);
                if (ru == null)
                {
                    continue;
                }
                Tree t = ru.NewTree();
                t.RU = ru;
                t.ID = treeReader.GetInt32(1);
                t.SetLightCellIndex(coord);
                Species s = GlobalSettings.Instance.Model.SpeciesSet().Species(treeReader.GetInt32(4));
                t.Species = s ?? throw new NotSupportedException("loadTrees: Invalid species");
                t.Age = treeReader.GetInt32(5);
                t.Height = treeReader.GetFloat(6);
                t.Dbh = treeReader.GetFloat(7);
                t.LeafArea = treeReader.GetFloat(8);
                t.mOpacity = treeReader.GetFloat(9);
                t.FoliageMass = treeReader.GetFloat(10);
                t.StemMass = treeReader.GetFloat(11);
                t.FineRootMass = treeReader.GetFloat(12);
                t.CoarseRootMass = treeReader.GetFloat(13);
                t.mNPPReserve = treeReader.GetFloat(14);
                t.StressIndex = treeReader.GetFloat(15);
                t.Stamp = s.GetStamp(t.Dbh, t.Height);
            }

            // now the saplings
            int n_sap_removed = 0;
            int sap_n = 0;
            if (GlobalSettings.Instance.Model.Settings.RegenerationEnabled)
            {
                // (1) remove all saplings:
                SaplingCellRunner scr = new SaplingCellRunner(stand_id, stand_grid);
                for (SaplingCell sc = scr.MoveNext(); sc != null; sc = scr.MoveNext())
                {
                    n_sap_removed += sc.GetOccupiedSlotCount();
                    GlobalSettings.Instance.Model.Saplings.ClearSaplings(sc, scr.RU, true);
                }

                // (2) load saplings from database
                SqliteCommand saplingQuery = new SqliteCommand(String.Format("select posx, posy, species_index, age, height, stress_years, flags " +
                               "from saplings_stand where standID={0}", stand_id), db);
                using SqliteDataReader saplingReader = saplingQuery.ExecuteReader();
                for (; saplingReader.HasRows; saplingReader.Read())
                {
                    PointF coord = GisGrid.WorldToModel(new PointF(saplingReader.GetInt32(0), saplingReader.GetInt32(1)));
                    if (!extent.Contains(coord))
                    {
                        continue;
                    }
                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.Instance.Model.Saplings.Cell(GlobalSettings.Instance.Model.LightGrid.IndexAt(coord), true, ref ru);
                    if (sc == null)
                    {
                        continue;
                    }
                    SaplingTree st = sc.AddSapling(saplingReader.GetFloat(4), saplingReader.GetInt32(3), saplingReader.GetInt32(2));
                    if (st != null)
                    {
                        st.StressYears = saplingReader.GetByte(5);
                        st.Flags = saplingReader.GetByte(6);
                    }
                    sap_n++;
                }

            }

            // clean up
            GlobalSettings.Instance.Model.CleanTreeLists(true);
            Debug.WriteLine("load stand snapshot for stand " + stand_id + ": trees (removed/loaded): " + n_removed + "/" + n + ", saplings (removed/loaded): " + n_sap_removed + "/" + sap_n);
            return true;
        }

        private void SaveTrees()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteTransaction treeTransaction = db.BeginTransaction();
            AllTreeIterator at = new AllTreeIterator(GlobalSettings.Instance.Model);
            SqliteCommand treeInsert = new SqliteCommand(String.Format("insert into trees (ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " +
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

            int n = 0;
            for (Tree t = at.MoveNext(); t != null; t = at.MoveNext())
            {
                treeInsert.Parameters[0].Value = t.ID;
                treeInsert.Parameters[1].Value = t.RU.Index;
                treeInsert.Parameters[2].Value = t.PositionIndex.X;
                treeInsert.Parameters[3].Value = t.PositionIndex.Y;
                treeInsert.Parameters[4].Value = t.Species.ID;
                treeInsert.Parameters[5].Value = t.Age;
                treeInsert.Parameters[6].Value = t.Height;
                treeInsert.Parameters[7].Value = t.Dbh;
                treeInsert.Parameters[8].Value = t.LeafArea;
                treeInsert.Parameters[9].Value = t.mOpacity;
                treeInsert.Parameters[10].Value = t.FoliageMass;
                treeInsert.Parameters[11].Value = t.StemMass;
                treeInsert.Parameters[12].Value = t.FineRootMass;
                treeInsert.Parameters[13].Value = t.CoarseRootMass;
                treeInsert.Parameters[14].Value = t.mNPPReserve;
                treeInsert.Parameters[15].Value = t.StressIndex;
                treeInsert.ExecuteNonQuery();

                if (++n % 10000 == 0)
                {
                    Debug.WriteLine(n + "trees saved...");
                }
            }
            treeTransaction.Commit();
            Debug.WriteLine("Snapshot: finished trees. N=" + n);
        }

        private void LoadTrees()
        {
            // clear all trees on the landscape
            foreach (ResourceUnit ruInList in GlobalSettings.Instance.Model.ResourceUnits)
            {
                ruInList.Trees.Clear();
            }

            int ru_index = -1;
            int new_ru;
            int offsetx = 0, offsety = 0;
            ResourceUnit ru = null;
            int n = 0, ntotal = 0;
            // load the trees from the database
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteCommand treeQuery = new SqliteCommand("select ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex from trees", db);
            using SqliteDataReader treeReader = treeQuery.ExecuteReader();
            for (; treeReader.HasRows; treeReader.Read())
            {
                new_ru = treeReader.GetInt32(1);
                ++ntotal;
                if (new_ru != ru_index)
                {
                    ru_index = new_ru;
                    ru = mRUHash[ru_index];
                    if (ru != null)
                    {
                        offsetx = ru.CornerPointOffset.X;
                        offsety = ru.CornerPointOffset.Y;
                    }
                }
                if (ru == null)
                {
                    continue;
                }
                // add a new tree to the tree list
                //ru.trees().Add(Tree());
                //Tree &t = ru.trees().back();
                Tree t = ru.NewTree();
                t.RU = ru;
                t.ID = treeReader.GetInt32(0);
                t.PositionIndex = new Point(offsetx + treeReader.GetInt32(2) % Constant.LightPerRUsize,
                                            offsety + treeReader.GetInt32(3) % Constant.LightPerRUsize);
                Species s = GlobalSettings.Instance.Model.SpeciesSet().GetSpecies(treeReader.GetString(4));
                t.Species = s ?? throw new NotSupportedException("loadTrees: Invalid species");
                t.Age = treeReader.GetInt32(5);
                t.Height = treeReader.GetFloat(6);
                t.Dbh = treeReader.GetFloat(7);
                t.LeafArea = treeReader.GetFloat(8);
                t.mOpacity = treeReader.GetFloat(9);
                t.FoliageMass = treeReader.GetFloat(10);
                t.StemMass = treeReader.GetFloat(11);
                t.FineRootMass = treeReader.GetFloat(12);
                t.CoarseRootMass = treeReader.GetFloat(13);
                t.mNPPReserve = treeReader.GetFloat(14);
                t.StressIndex = treeReader.GetFloat(15);
                t.Stamp = s.GetStamp(t.Dbh, t.Height);

                if (n < 10000000 && ++n % 10000 == 0)
                {
                    Debug.WriteLine(n + " trees loaded...");
                }
            }

            Debug.WriteLine("Snapshot: finished trees. N=" + n + " from trees in snapshot: " + ntotal);
        }

        private void SaveSoil()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteTransaction soilTransaction = db.BeginTransaction();
            using SqliteCommand soilInsert = new SqliteCommand(String.Format("insert into soil (RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack) " +
                                                                             "values (:idx, :kyl, :kyr, :inLabC, :iLN, :iLP, :iRC, :iRN, :iRP, :ylc, :yln, :ylp, :yrc, :yrn, :yrp, :somc, :somn, :wc, :snowpack)"), db, soilTransaction);
            soilInsert.Parameters.Add(":idx", SqliteType.Integer);
            soilInsert.Parameters.Add(":kyl", SqliteType.Integer);
            soilInsert.Parameters.Add(":kyr", SqliteType.Integer);
            soilInsert.Parameters.Add(":inLabC", SqliteType.Integer);
            soilInsert.Parameters.Add(":iLN", SqliteType.Integer);
            soilInsert.Parameters.Add(":iLP", SqliteType.Integer);
            soilInsert.Parameters.Add(":iRC", SqliteType.Integer);
            soilInsert.Parameters.Add(":iRN", SqliteType.Integer);
            soilInsert.Parameters.Add(":iRP", SqliteType.Integer);
            soilInsert.Parameters.Add(":ylc", SqliteType.Integer);
            soilInsert.Parameters.Add(":yln", SqliteType.Integer);
            soilInsert.Parameters.Add(":ylp", SqliteType.Integer);
            soilInsert.Parameters.Add(":yrc", SqliteType.Integer);
            soilInsert.Parameters.Add(":yrn", SqliteType.Integer);
            soilInsert.Parameters.Add(":yrp", SqliteType.Integer);
            soilInsert.Parameters.Add(":somc", SqliteType.Integer);
            soilInsert.Parameters.Add(":somn", SqliteType.Integer);
            soilInsert.Parameters.Add(":wc", SqliteType.Integer);
            soilInsert.Parameters.Add(":snowpack", SqliteType.Integer);

            int n = 0;
            foreach (ResourceUnit ru in GlobalSettings.Instance.Model.ResourceUnits)
            {
                Soil s = ru.Soil;
                if (s != null)
                {
                    soilInsert.Parameters[0].Value = s.mRU.Index;
                    soilInsert.Parameters[1].Value = s.mKyl;
                    soilInsert.Parameters[2].Value = s.mKyr;
                    soilInsert.Parameters[3].Value = s.mInputLab.C;
                    soilInsert.Parameters[4].Value = s.mInputLab.N;
                    soilInsert.Parameters[5].Value = s.mInputLab.Parameter;
                    soilInsert.Parameters[6].Value = s.mInputRef.C;
                    soilInsert.Parameters[7].Value = s.mInputRef.N;
                    soilInsert.Parameters[8].Value = s.mInputRef.Parameter;
                    soilInsert.Parameters[9].Value = s.YoungLabile.C;
                    soilInsert.Parameters[10].Value = s.YoungLabile.N;
                    soilInsert.Parameters[11].Value = s.YoungLabile.Parameter;
                    soilInsert.Parameters[12].Value = s.YoungRefractory.C;
                    soilInsert.Parameters[13].Value = s.YoungRefractory.N;
                    soilInsert.Parameters[14].Value = s.YoungRefractory.Parameter;
                    soilInsert.Parameters[15].Value = s.OrganicMatter.C;
                    soilInsert.Parameters[16].Value = s.OrganicMatter.N;
                    soilInsert.Parameters[17].Value = ru.WaterCycle.CurrentContent;
                    soilInsert.Parameters[18].Value = ru.WaterCycle.CurrentSnowWaterEquivalent();
                    soilInsert.ExecuteNonQuery();

                    if (++n % 1000 == 0)
                    {
                        Debug.WriteLine(n + "soil resource units saved...");
                    }
                }
            }

            soilTransaction.Commit();
            Debug.WriteLine("Snapshot: finished Soil. N=" + n);
        }

        private void LoadSoil()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteCommand soilQuery = new SqliteCommand("select RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack from soil", db);
            int ru_index = -1;
            ResourceUnit ru = null;
            int n = 0;
            using SqliteDataReader soilReader = soilQuery.ExecuteReader();
            for (; soilReader.HasRows; soilReader.Read())
            {
                ru_index = soilReader.GetInt32(0);
                ru = mRUHash[ru_index];
                if (ru == null)
                {
                    continue;
                }
                Soil s = ru.Soil;
                if (s == null)
                {
                    throw new NotSupportedException("loadSoil: trying to load soil data but soil module is disabled.");
                }
                s.mKyl = soilReader.GetDouble(1);
                s.mKyr = soilReader.GetDouble(2);
                s.mInputLab.C = soilReader.GetDouble(3);
                s.mInputLab.N = soilReader.GetDouble(4);
                s.mInputLab.Parameter = soilReader.GetDouble(5);
                s.mInputRef.C = soilReader.GetDouble(6);
                s.mInputRef.N = soilReader.GetDouble(7);
                s.mInputRef.Parameter = soilReader.GetDouble(8);
                s.YoungLabile.C = soilReader.GetDouble(9);
                s.YoungLabile.N = soilReader.GetDouble(10);
                s.YoungLabile.Parameter = soilReader.GetDouble(11);
                s.YoungRefractory.C = soilReader.GetDouble(12);
                s.YoungRefractory.N = soilReader.GetDouble(13);
                s.YoungRefractory.Parameter = soilReader.GetDouble(14);
                s.OrganicMatter.C = soilReader.GetDouble(15);
                s.OrganicMatter.N = soilReader.GetDouble(16);
                ru.WaterCycle.SetContent(soilReader.GetDouble(17), soilReader.GetDouble(18));

                if (++n % 1000 == 0)
                {
                    Debug.WriteLine(n + "soil units loaded...");
                }
            }

            Debug.WriteLine("Snapshot: finished soil. N=" + n);
        }

        private void SaveSnags()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteTransaction snagTransaction = db.BeginTransaction();
            SqliteCommand snagInsert = new SqliteCommand(String.Format("insert into snag(RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, " +
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
            foreach (ResourceUnit ru in GlobalSettings.Instance.Model.ResourceUnits)
            {
                Snag s = ru.Snags;
                if (s == null)
                {
                    continue;
                }
                snagInsert.Parameters[0].Value = s.mRU.Index;
                snagInsert.Parameters[1].Value = s.ClimateFactor;
                snagInsert.Parameters[2].Value = s.mSWD[0].C;
                snagInsert.Parameters[3].Value = s.mSWD[0].N;
                snagInsert.Parameters[4].Value = s.mSWD[1].C;
                snagInsert.Parameters[5].Value = s.mSWD[1].N;
                snagInsert.Parameters[7].Value = s.mSWD[2].C;
                snagInsert.Parameters[8].Value = s.mSWD[2].N;
                snagInsert.Parameters[9].Value = s.TotalSwd.C;
                snagInsert.Parameters[10].Value = s.TotalSwd.N;
                snagInsert.Parameters[11].Value = s.mNumberOfSnags[0];
                snagInsert.Parameters[12].Value = s.mNumberOfSnags[1];
                snagInsert.Parameters[13].Value = s.mNumberOfSnags[2];
                snagInsert.Parameters[14].Value = s.mAvgDbh[0];
                snagInsert.Parameters[15].Value = s.mAvgDbh[1];
                snagInsert.Parameters[16].Value = s.mAvgDbh[2];
                snagInsert.Parameters[17].Value = s.mAvgHeight[0];
                snagInsert.Parameters[18].Value = s.mAvgHeight[1];
                snagInsert.Parameters[19].Value = s.mAvgHeight[2];
                snagInsert.Parameters[20].Value = s.mAvgVolume[0];
                snagInsert.Parameters[21].Value = s.mAvgVolume[21];
                snagInsert.Parameters[22].Value = s.mAvgVolume[2];
                snagInsert.Parameters[23].Value = s.mTimeSinceDeath[0];
                snagInsert.Parameters[24].Value = s.mTimeSinceDeath[21];
                snagInsert.Parameters[25].Value = s.mTimeSinceDeath[2];
                snagInsert.Parameters[26].Value = s.mKSW[0];
                snagInsert.Parameters[27].Value = s.mKSW[21];
                snagInsert.Parameters[28].Value = s.mKSW[2];
                snagInsert.Parameters[29].Value = s.mHalfLife[0];
                snagInsert.Parameters[30].Value = s.mHalfLife[30];
                snagInsert.Parameters[31].Value = s.mHalfLife[2];
                snagInsert.Parameters[32].Value = s.mOtherWood[0].C;
                snagInsert.Parameters[33].Value = s.mOtherWood[0].N;
                snagInsert.Parameters[34].Value = s.mOtherWood[1].C;
                snagInsert.Parameters[35].Value = s.mOtherWood[30].N;
                snagInsert.Parameters[36].Value = s.mOtherWood[2].C;
                snagInsert.Parameters[37].Value = s.mOtherWood[2].N;
                snagInsert.Parameters[38].Value = s.mOtherWood[3].C;
                snagInsert.Parameters[39].Value = s.mOtherWood[3].N;
                snagInsert.Parameters[40].Value = s.mOtherWood[4].C;
                snagInsert.Parameters[41].Value = s.mOtherWood[4].N;
                snagInsert.Parameters[42].Value = s.mBranchCounter;
                snagInsert.ExecuteNonQuery();

                if (++n % 1000 == 0)
                {
                    Debug.WriteLine(n + " snags saved...");
                }
            }

            snagTransaction.Commit();
            Debug.WriteLine("Snapshot: finished Snags. N=" + n);
        }

        private void LoadSnags()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            int n = 0;
            using SqliteCommand snagQuery = new SqliteCommand("select RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, totalSWDC, totalSWDN, NSnags1, NSnags2, NSnags3, dbh1, dbh2, dbh3, height1, height2, height3, volume1, volume2, volume3, tsd1, tsd2, tsd3, ksw1, ksw2, ksw3, halflife1, halflife2, halflife3, branch1C, branch1N, branch2C, branch2N, branch3C, branch3N, branch4C, branch4N, branch5C, branch5N, branchIndex from snag", db);
            using SqliteDataReader snagReader = snagQuery.ExecuteReader();
            for (; snagReader.HasRows; snagReader.Read(), ++n)
            {
                int ci = 0;
                int ru_index = snagReader.GetInt32(ci++);
                ResourceUnit ru = mRUHash[ru_index];
                if (ru == null)
                {
                    continue;
                }
                Snag s = ru.Snags;
                if (s == null)
                {
                    continue;
                }
                s.ClimateFactor = snagReader.GetDouble(ci++);
                s.mSWD[0].C = snagReader.GetDouble(ci++);
                s.mSWD[0].N = snagReader.GetDouble(ci++);
                s.mSWD[1].C = snagReader.GetDouble(ci++);
                s.mSWD[1].N = snagReader.GetDouble(ci++);
                s.mSWD[2].C = snagReader.GetDouble(ci++);
                s.mSWD[2].N = snagReader.GetDouble(ci++);
                s.TotalSwd.C = snagReader.GetDouble(ci++);
                s.TotalSwd.N = snagReader.GetDouble(ci++);
                s.mNumberOfSnags[0] = snagReader.GetDouble(ci++);
                s.mNumberOfSnags[1] = snagReader.GetDouble(ci++);
                s.mNumberOfSnags[2] = snagReader.GetDouble(ci++);
                s.mAvgDbh[0] = snagReader.GetDouble(ci++);
                s.mAvgDbh[1] = snagReader.GetDouble(ci++);
                s.mAvgDbh[2] = snagReader.GetDouble(ci++);
                s.mAvgHeight[0] = snagReader.GetDouble(ci++);
                s.mAvgHeight[1] = snagReader.GetDouble(ci++);
                s.mAvgHeight[2] = snagReader.GetDouble(ci++);
                s.mAvgVolume[0] = snagReader.GetDouble(ci++);
                s.mAvgVolume[1] = snagReader.GetDouble(ci++);
                s.mAvgVolume[2] = snagReader.GetDouble(ci++);
                s.mTimeSinceDeath[0] = snagReader.GetDouble(ci++);
                s.mTimeSinceDeath[1] = snagReader.GetDouble(ci++);
                s.mTimeSinceDeath[2] = snagReader.GetDouble(ci++);
                s.mKSW[0] = snagReader.GetDouble(ci++);
                s.mKSW[1] = snagReader.GetDouble(ci++);
                s.mKSW[2] = snagReader.GetDouble(ci++);
                s.mHalfLife[0] = snagReader.GetDouble(ci++);
                s.mHalfLife[1] = snagReader.GetDouble(ci++);
                s.mHalfLife[2] = snagReader.GetDouble(ci++);
                s.mOtherWood[0].C = snagReader.GetDouble(ci++); s.mOtherWood[0].N = snagReader.GetDouble(ci++);
                s.mOtherWood[1].C = snagReader.GetDouble(ci++); s.mOtherWood[1].N = snagReader.GetDouble(ci++);
                s.mOtherWood[2].C = snagReader.GetDouble(ci++); s.mOtherWood[2].N = snagReader.GetDouble(ci++);
                s.mOtherWood[3].C = snagReader.GetDouble(ci++); s.mOtherWood[3].N = snagReader.GetDouble(ci++);
                s.mOtherWood[4].C = snagReader.GetDouble(ci++); s.mOtherWood[4].N = snagReader.GetDouble(ci++);
                s.mBranchCounter = snagReader.GetInt32(ci++);

                if (++n % 1000 == 0)
                {
                    Debug.WriteLine(n + " snags loaded...");
                }
            }

            Debug.WriteLine("Snapshot: finished snags. N=" + n);
        }

        private void SaveSaplings()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            using SqliteCommand q = new SqliteCommand("insert into saplings (RUindex, species, posx, posy, age, height, stress_years) " +
                                   "values (?,?,?,?,?,?,?)", db);
            // int n = 0;
            throw new NotSupportedException("saveSaplings() not implemented");
            //    foreach (ResourceUnit *ru, GlobalSettings.instance().model().ruList()) {
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

        private void LoadSaplings()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseSnapshot();
            SqliteCommand saplingQuery = new SqliteCommand("select RUindex, species, posx, posy, age, height, stress_years from saplings", db);

            // clear all saplings in the whole project area: added for testing/debugging
            //    foreach( ResourceUnit *ru, GlobalSettings.instance().model().ruList()) {
            //        foreach (ResourceUnitSpecies *rus, ru.ruSpecies()) {
            //            rus.changeSapling().clear();
            //            rus.changeSapling().clearStatistics();
            //        }
            //    }

            ResourceUnit ru = null;
            int n = 0, ntotal = 0;
            int ci;
            int posx, posy;
            Saplings saplings = GlobalSettings.Instance.Model.Saplings;
            SqliteDataReader saplingReader = saplingQuery.ExecuteReader();
            for (; saplingReader.HasRows; saplingReader.Read())
            {
                ci = 0;
                int ru_index = saplingReader.GetInt32(ci++);
                ru = mRUHash[ru_index];
                if (ru == null)
                {
                    continue;
                }
                Species species = ru.SpeciesSet.GetSpecies(saplingReader.GetString(ci++));
                if (species == null)
                {
                    throw new NotSupportedException("loadSaplings: Invalid species");
                }

                int offsetx = ru.CornerPointOffset.X;
                int offsety = ru.CornerPointOffset.Y;
                posx = offsetx + saplingReader.GetInt32(ci++) % Constant.LightPerRUsize;
                posy = offsety + saplingReader.GetInt32(ci++) % Constant.LightPerRUsize;

                SaplingCell sc = saplings.Cell(new Point(posx, posy), true, ref ru);
                if (sc == null)
                {
                    continue;
                }

                int age = saplingReader.GetInt32(ci++);
                SaplingTree st = sc.AddSapling(saplingReader.GetFloat(ci++), age, species.Index);
                if (st == null)
                {
                    continue;
                }
                st.StressYears = saplingReader.GetByte(ci++);
                ++ntotal;


                if (n < 10000000 && ++n % 10000 == 0)
                {
                    Debug.WriteLine(n + " saplings loaded...");
                }
                if (n >= 10000000 && ++n % 1000000 == 0)
                {
                    Debug.WriteLine(n + " saplings loaded...");
                }
            }
            Debug.WriteLine("Snapshot: finished loading saplings. N=" + n + "from N in snapshot:" + ntotal);
        }
    }
}
