using iLand.Output;
using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Core
{
    /** @class Tree
        @ingroup core
        A tree is the basic simulation entity of iLand and represents a single tree.
        Trees in iLand are designed to be lightweight, thus the list of stored properties is limited. Basic properties
        are dimensions (dbh, height), biomass pools (stem, leaves, roots), the reserve NPP pool. Additionally, the location and species are stored.
        A Tree has a height of at least 4m; trees below this threshold are covered by the regeneration layer (see Sapling).
        Trees are stored in lists managed at the resource unit level.
      */
    public class Tree
    {
        private static Grid<float> LightGrid = null; // BUGBUG: threading
        private static Grid<HeightGridValue> HeightGrid = null;  // BUGBUG: threading

        public static LandscapeRemovedOutput LandscapeRemovalOutput { get; set; }
        public static int StampApplications { get; set; }
        public static int TreesCreated { get; set; }
        public static TreeRemovedOutput TreeRemovalOutput { get; set; }

        // biomass compartements
        public float mOpacity; ///< multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        // production relevant
        public float mNPPReserve; ///< NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public float mLightResponse; ///< light response used for distribution of biomass on RU level
        // auxiliary
        public float mDbhDelta; ///< diameter growth [cm]

        // various flags
        private int mFlags;

        /// (binary coded) tree flags
        enum Flags
        {
            TreeDead = 1, TreeDebugging = 2,
            TreeDeadBarkBeetle = 16, TreeDeadWind = 32, TreeDeadFire = 64, TreeDeadKillAndDrop = 128, TreeHarvested = 256,
            MarkForCut = 512, // mark tree for being cut down
            MarkForHarvest = 1024, // mark tree for being harvested
            MarkCropTree = 2048, // mark as crop tree
            MarkCropCompetitor = 4096 // mark as competitor for a crop tree
        };

        public int Age { get; set; } ///< the tree age (years)
        public float Dbh { get; set; } ///< dimater at breast height in cm
        public float Height { get; set; } ///< tree height in m
        public int ID { get; set; } ///< numerical unique ID of the tree
        public float LeafArea { get; set; } ///< leaf area (m2) of the tree
        public Point LightCellIndex { get; set; }
        public float LightResourceIndex { get; private set; } ///< LRI of the tree (updated during readStamp())
        public Point PositionIndex { get; set; } ///< index of the trees position on the basic LIF grid
        public ResourceUnit RU { get; set; } ///< pointer to the ressource unit the tree belongs to.
        public Species Species { get; set; } ///< pointer to the tree species of the tree.
        public Stamp Stamp { get; set; } ///< TODO: only for debugging purposes

        // biomass properties
        public float CoarseRootMass { get; set; } ///< mass (kg) of coarse roots
        public float FineRootMass { get; set; } ///< mass (kg) of fine roots
        public float FoliageMass { get; set; } ///< mass (kg) of foliage
        public float StemMass { get; set; } ///< mass (kg) of stem
        public float StressIndex { get; set; } ///< the scalar stress rating (0..1), used for mortality

        public Tree()
        {
            Dbh = Height = 0;
            RU = null;
            Species = null;
            mFlags = Age = 0;
            mOpacity = FoliageMass = StemMass = CoarseRootMass = FineRootMass = LeafArea = 0.0F;
            mDbhDelta = mNPPReserve = LightResourceIndex = StressIndex = 0.0F;
            mLightResponse = 0.0F;
            Stamp = null;
            ID = 0; // set by ResourceUnit
            Tree.TreesCreated++; // BUGBUG: thread safety
        }

        /// @property position The tree does not store the floating point coordinates but only the index of pixel on the LIF grid
        /// BUGBUG?
        public PointF GetCellCenterPoint() { Debug.Assert(Tree.LightGrid != null); return Tree.LightGrid.GetCellCenterPoint(PositionIndex); }

        public void SetLightCellIndex(PointF pos) { Debug.Assert(Tree.LightGrid != null); PositionIndex = Tree.LightGrid.IndexAt(pos); }

        // death reasons
        public bool IsCutDown() { return IsFlagSet(Flags.TreeDeadKillAndDrop); }
        public bool IsDead() { return IsFlagSet(Flags.TreeDead); } ///< returns true if the tree is already dead.
        public bool IsDeadBarkBeetle() { return IsFlagSet(Flags.TreeDeadBarkBeetle); }
        public bool IsDeadFire() { return IsFlagSet(Flags.TreeDeadFire); }
        public bool IsDeadWind() { return IsFlagSet(Flags.TreeDeadWind); }
        public bool IsHarvested() { return IsFlagSet(Flags.TreeHarvested); }

        public void SetDeathReasonBarkBeetle() { SetFlag(Flags.TreeDeadBarkBeetle, true); }
        public void SetDeathReasonCutdown() { SetFlag(Flags.TreeDeadKillAndDrop, true); }
        public void SetDeathReasonFire() { SetFlag(Flags.TreeDeadFire, true); }
        public void SetDeathReasonHarvested() { SetFlag(Flags.TreeHarvested, true); }
        public void SetDeathReasonWind() { SetFlag(Flags.TreeDeadWind, true); }

        // management flags (used by ABE management system)
        public void MarkForHarvest(bool do_mark) { SetFlag(Flags.MarkForHarvest, do_mark); }
        public bool IsMarkedForHarvest() { return IsFlagSet(Flags.MarkForHarvest); }
        public void MarkForCut(bool do_mark) { SetFlag(Flags.MarkForCut, do_mark); }
        public bool IsMarkedForCut() { return IsFlagSet(Flags.MarkForCut); }
        public void MarkAsCropTree(bool do_mark) { SetFlag(Flags.MarkCropTree, do_mark); }
        public bool IsMarkedAsCropTree() { return IsFlagSet(Flags.MarkCropTree); }
        public void MarkAsCropCompetitor(bool do_mark) { SetFlag(Flags.MarkCropCompetitor, do_mark); }
        public bool IsMarkedAsCropCompetitor() { return IsFlagSet(Flags.MarkCropCompetitor); }

        /// set a Flag 'flag' to the value 'value'.
        private void SetFlag(Flags flag, bool value)
        {
            if (value)
            {
                mFlags |= (int)flag;
            }
            else
            {
                mFlags &= (int)flag ^ 0xffffff;
            }
        }

        /// retrieve the value of the flag 'flag'.
        private bool IsFlagSet(Flags flag)
        {
            return (mFlags & (int)flag) != 0;
        }

        // special functions
        private bool IsDebugging()
        {
            return IsFlagSet(Flags.TreeDebugging);
        }

        public void EnableDebugging(bool enable = true) { SetFlag(Flags.TreeDebugging, enable); }

        public float GetCrownRadius()
        {
            Debug.Assert(this.Stamp != null);
            return this.Stamp.CrownRadius;
        }

        public float GetBranchBiomass()
        {
            return (float)this.Species.GetBiomassBranch(Dbh);
        }

        public static void SetGrids(Grid<float> gridToStamp, Grid<HeightGridValue> dominanceGrid)
        {
            Tree.LightGrid = gridToStamp; 
            Tree.HeightGrid = dominanceGrid;
        }

        /// dumps some core variables of a tree to a string.
        private string Dump()
        {
            string result = String.Format("id {0} species {1} dbh {2} h {3} x/y {4}/{5} ru# {6} LRI {7}",
                                          ID, Species.ID, Dbh, Height,
                                          GetCellCenterPoint().X, GetCellCenterPoint().Y,
                                          RU.Index, LightResourceIndex);
            return result;
        }

        private void DumpList(List<object> rTargetList)
        {
            rTargetList.AddRange(new object[] { ID, Species.ID, Dbh, Height, GetCellCenterPoint().X, GetCellCenterPoint().Y, RU.Index, LightResourceIndex,
                                                StemMass, CoarseRootMass + FoliageMass + LeafArea } );
        }

        public void Setup(Model model)
        {
            if (Dbh <= 0 || Height <= 0)
            {
                throw new NotSupportedException(String.Format("Error: trying to set up a tree with invalid dimensions: dbh: {0} height: {1} id: {2} RU-index: {3}", Dbh, Height, ID, RU.Index));
            }
            // check stamp
            Debug.Assert(Species != null, "setup()", "species is NULL");
            Stamp = Species.GetStamp(Dbh, Height);
            if (Stamp == null)
            {
                throw new NotSupportedException("setup() with invalid stamp!");
            }

            FoliageMass = (float)Species.GetBiomassFoliage(Dbh);
            CoarseRootMass = (float)Species.GetBiomassRoot(Dbh); // coarse root (allometry)
            FineRootMass = (float)(FoliageMass * Species.FinerootFoliageRatio); //  fine root (size defined  by finerootFoliageRatio)
            StemMass = (float)Species.GetBiomassWoody(Dbh);

            // LeafArea[m2] = LeafMass[kg] * specificLeafArea[m2/kg]
            LeafArea = (float)(FoliageMass * Species.SpecificLeafArea);
            mOpacity = (float)(1.0F - MathF.Exp(-(float)model.ModelSettings.LightExtinctionCoefficientOpacity * LeafArea / Stamp.CrownArea));
            mNPPReserve = (float)((1 + Species.FinerootFoliageRatio) * FoliageMass); // initial value
            mDbhDelta = 0.1F; // initial value: used in growth() to estimate diameter increment
        }

        public void SetAge(int age, float treeheight)
        {
            Age = age;
            if (age == 0)
            {
                // estimate age using the tree height
                Age = Species.EstimateAge(treeheight);
            }
        }

        //////////////////////////////////////////////////
        ////  Light functions (Pattern-stuff)
        //////////////////////////////////////////////////
        public void ApplyLightIntensityPattern()
        {
            if (Stamp == null)
            {
                return;
            }
            Debug.Assert(Tree.LightGrid != null && Stamp != null && RU != null);
            Point pos = PositionIndex;
            int offset = Stamp.CenterCellPosition;
            pos.X -= offset;
            pos.Y -= offset;

            float local_dom; // height of Z* on the current position
            int x, y;
            float value, z, z_zstar;
            int gr_stamp = Stamp.Size();

            if (!Tree.LightGrid.Contains(pos) || !Tree.LightGrid.Contains(new Point(pos.X + gr_stamp, pos.Y + gr_stamp)))
            {
                // this should not happen because of the buffer
                return;
            }
            int grid_y = pos.Y;
            for (y = 0; y < gr_stamp; ++y)
            {

                float grid_value_ptr = Tree.LightGrid[pos.X, grid_y];
                int grid_x = pos.X;
                for (x = 0; x < gr_stamp; ++x, ++grid_x, ++grid_value_ptr)
                {
                    // suppose there is no stamping outside
                    value = Stamp[x, y]; // stampvalue
                                             //if (value>0.f) {
                    local_dom = Tree.HeightGrid[grid_x / Constant.LightPerHeightSize, grid_y / Constant.LightPerHeightSize].Height;
                    z = MathF.Max(Height - Stamp.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    value = 1.0F - value * mOpacity * z_zstar; // calculated value
                    value = MathF.Max(value, 0.02F); // limit value

                    grid_value_ptr *= value;
                }
                grid_y++;
            }

            ++Tree.StampApplications; // BUGBUG: not thread safe
        }

        /// helper function for gluing the edges together
        /// index: index at grid
        /// count: number of pixels that are the simulation area (e.g. 100m and 2m pixel . 50)
        /// buffer: size of buffer around simulation area (in pixels)
        private int TorusIndex(int index, int count, int buffer, int ru_index)
        {
            return buffer + ru_index + (index - buffer + count) % count;
        }

        /** Apply LIPs. This "Torus" functions wraps the influence at the edges of a 1ha simulation area.
          */
        public void ApplyLightIntensityPatternTorus()
        {
            if (this.Stamp == null)
            {
                return;
            }
            Debug.Assert(Tree.LightGrid != null && this.Stamp != null && this.RU != null);
            int bufferOffset = Tree.LightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer
            Point pos = new Point((this.PositionIndex.X - bufferOffset) % Constant.LightPerRUsize + bufferOffset,
                                  (this.PositionIndex.Y - bufferOffset) % Constant.LightPerRUsize + bufferOffset); // offset within the ha
            Point ruOffset = new Point(this.PositionIndex.X - pos.X, this.PositionIndex.Y - pos.Y); // offset of the corner of the resource index

            int offset = this.Stamp.CenterCellPosition;
            pos.X -= offset;
            pos.Y -= offset;

            int stampSize = this.Stamp.Size();
            if (!Tree.LightGrid.Contains(pos) || !Tree.LightGrid.Contains(new Point(stampSize + pos.X, stampSize + pos.Y)))
            {
                // todo: in this case we should use another algorithm!!! necessary????
                return;
            }

            float local_dom; // height of Z* on the current position
            for (int y = 0; y < stampSize; ++y)
            {
                int grid_y = pos.Y + y;
                int yt = TorusIndex(grid_y, Constant.LightPerRUsize, bufferOffset, ruOffset.Y); // 50 cells per 100m
                for (int x = 0; x < stampSize; ++x)
                {
                    // suppose there is no stamping outside
                    int grid_x = pos.X + x;
                    int xt = TorusIndex(grid_x, Constant.LightPerRUsize, bufferOffset, ruOffset.X);

                    local_dom = Tree.HeightGrid[xt / Constant.LightPerHeightSize, yt / Constant.LightPerHeightSize].Height;

                    float z = MathF.Max(Height - Stamp.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    float value = this.Stamp[x, y]; // stampvalue
                    value = 1.0F - value * mOpacity * z_zstar; // calculated value
                                                               // old: value = 1. - value*mOpacity / local_dom; // calculated value
                    value = MathF.Max(value, 0.02f); // limit value

                    Tree.LightGrid[xt, yt] *= value; // use wraparound coordinates
                }
            }

            ++Tree.StampApplications; // BUGBUG: not thread safe
        }

        /** heightGrid()
          This function calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
            */
        public void CalculateDominantHeightField()
        {
            Point p = new Point(PositionIndex.X / Constant.LightPerHeightSize, PositionIndex.Y / Constant.LightPerHeightSize); // pos of tree on height grid

            // count trees that are on height-grid cells (used for stockable area)
            Tree.HeightGrid[p].IncreaseCount();
            if (Height > Tree.HeightGrid[p].Height)
            {
                Tree.HeightGrid[p].Height = Height;
            }

            int r = Stamp.Reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int index_eastwest = PositionIndex.X % Constant.LightPerHeightSize; // 4: very west, 0 east edge
            int index_northsouth = PositionIndex.Y % Constant.LightPerHeightSize; // 4: northern edge, 0: southern edge
            if (index_eastwest - r < 0)
            { // east
                Tree.HeightGrid[p.X - 1, p.Y].Height = MathF.Max(Tree.HeightGrid[p.X - 1, p.Y].Height, Height);
            }
            if (index_eastwest + r >= Constant.LightPerHeightSize)
            {  // west
                Tree.HeightGrid[p.X + 1, p.Y].Height = MathF.Max(Tree.HeightGrid[p.X + 1, p.Y].Height, Height);
            }
            if (index_northsouth - r < 0)
            {  // south
                Tree.HeightGrid[p.X, p.Y - 1].Height = MathF.Max(Tree.HeightGrid[p.X, p.Y - 1].Height, Height);
            }
            if (index_northsouth + r >= Constant.LightPerHeightSize)
            {  // north
                Tree.HeightGrid[p.X, p.Y + 1].Height = MathF.Max(Tree.HeightGrid[p.X, p.Y + 1].Height, Height);
            }

            // without spread of the height grid
            //    // height of Z*
            //    float cellsize = Tree.mHeightGrid.cellsize();
            //
            //    int index_eastwest = mPositionIndex.X % cPxPerHeight; // 4: very west, 0 east edge
            //    int index_northsouth = mPositionIndex.Y % cPxPerHeight; // 4: northern edge, 0: southern edge
            //    int dist[9];
            //    dist[3] = index_northsouth * 2 + 1; // south
            //    dist[1] = index_eastwest * 2 + 1; // west
            //    dist[5] = 10 - dist[3]; // north
            //    dist[7] = 10 - dist[1]; // east
            //    dist[8] = MathF.Max(dist[5], dist[7]); // north-east
            //    dist[6] = MathF.Max(dist[3], dist[7]); // south-east
            //    dist[0] = MathF.Max(dist[3], dist[1]); // south-west
            //    dist[2] = MathF.Max(dist[5], dist[1]); // north-west
            //    dist[4] = 0; // center cell
            //    /* the scheme of indices is as follows:  if sign(ix)= -1, if ix<0, 0 for ix=0, 1 for ix>0 (detto iy), then:
            //       index = 4 + 3*sign(ix) + sign(iy) transforms combinations of directions to unique ids (0..8), which are used above.
            //        e.g.: sign(ix) = -1, sign(iy) = 1 (=north-west) . index = 4 + -3 + 1 = 2
            //    */
            //
            //
            //    int ringcount = int(floor(mHeight / cellsize)) + 1;
            //    int ix, iy;
            //    int ring;
            //    float hdom;
            //
            //    for (ix=-ringcount;ix<=ringcount;ix++)
            //        for (iy=-ringcount; iy<=+ringcount; iy++) {
            //        ring = MathF.Max(abs(ix), abs(iy));
            //        Point pos(ix+p.X, iy+p.Y);
            //        if (Tree.mHeightGrid.isIndexValid(pos)) {
            //            float &rHGrid = Tree.mHeightGrid[pos).height;
            //            if (rHGrid > mHeight) // skip calculation if grid is higher than tree
            //                continue;
            //            int direction = 4 + (ix?(ix<0?-3:3):0) + (iy?(iy<0?-1:1):0); // 4 + 3*sgn(x) + sgn(y)
            //            hdom = mHeight - dist[direction];
            //            if (ring>1)
            //                hdom -= (ring-1)*10;
            //
            //            rHGrid = MathF.Max(rHGrid, hdom); // write value
            //        } // is valid
            //    } // for (y)
        }

        public void CalculateDominantHeightFieldTorus()
        {
            // height of Z*
            Point p = new Point(PositionIndex.X / Constant.LightPerHeightSize, PositionIndex.Y / Constant.LightPerHeightSize); // pos of tree on height grid
            int bufferOffset = Tree.HeightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer (i.e.: size of buffer in height-pixels)
            p.X = ((p.X - bufferOffset) % Constant.HeightSize + bufferOffset); // 10: 10 x 10m pixeln in 100m
            p.Y = ((p.Y - bufferOffset) % Constant.HeightSize + bufferOffset);

            // torus coordinates: ru_offset = coords of lower left corner of 1ha patch
            Point ru_offset = new Point(PositionIndex.X / Constant.LightPerHeightSize - p.X, PositionIndex.Y / Constant.LightPerHeightSize - p.Y);

            // count trees that are on height-grid cells (used for stockable area)
            HeightGridValue v = Tree.HeightGrid[TorusIndex(p.X, 10, bufferOffset, ru_offset.X), TorusIndex(p.Y, 10, bufferOffset, ru_offset.Y)];
            v.IncreaseCount();
            v.Height = MathF.Max(v.Height, Height);


            int r = Stamp.Reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int index_eastwest = PositionIndex.X % Constant.LightPerHeightSize; // 4: very west, 0 east edge
            int index_northsouth = PositionIndex.Y % Constant.LightPerHeightSize; // 4: northern edge, 0: southern edge
            if (index_eastwest - r < 0)
            { // east
                v = Tree.HeightGrid[TorusIndex(p.X - 1, 10, bufferOffset, ru_offset.X), TorusIndex(p.Y, 10, bufferOffset, ru_offset.Y)];
                v.Height = MathF.Max(v.Height, Height);
            }
            if (index_eastwest + r >= Constant.LightPerHeightSize)
            {  // west
                v = Tree.HeightGrid[TorusIndex(p.X + 1, 10, bufferOffset, ru_offset.X), TorusIndex(p.Y, 10, bufferOffset, ru_offset.Y)];
                v.Height = MathF.Max(v.Height, Height);
            }
            if (index_northsouth - r < 0)
            {  // south
                v = Tree.HeightGrid[TorusIndex(p.X, 10, bufferOffset, ru_offset.X), TorusIndex(p.Y - 1, 10, bufferOffset, ru_offset.Y)];
                v.Height = MathF.Max(v.Height, Height);
            }
            if (index_northsouth + r >= Constant.LightPerHeightSize)
            {  // north
                v = Tree.HeightGrid[TorusIndex(p.X, 10, bufferOffset, ru_offset.X), TorusIndex(p.Y + 1, 10, bufferOffset, ru_offset.Y)];
                v.Height = MathF.Max(v.Height, Height);
            }

            //    int index_eastwest = mPositionIndex.X % cPxPerHeight; // 4: very west, 0 east edge
            //    int index_northsouth = mPositionIndex.Y % cPxPerHeight; // 4: northern edge, 0: southern edge
            //    int dist[9];
            //    dist[3] = index_northsouth * 2 + 1; // south
            //    dist[1] = index_eastwest * 2 + 1; // west
            //    dist[5] = 10 - dist[3]; // north
            //    dist[7] = 10 - dist[1]; // east
            //    dist[8] = MathF.Max(dist[5], dist[7]); // north-east
            //    dist[6] = MathF.Max(dist[3], dist[7]); // south-east
            //    dist[0] = MathF.Max(dist[3], dist[1]); // south-west
            //    dist[2] = MathF.Max(dist[5], dist[1]); // north-west
            //    dist[4] = 0; // center cell
            //    /* the scheme of indices is as follows:  if sign(ix)= -1, if ix<0, 0 for ix=0, 1 for ix>0 (detto iy), then:
            //       index = 4 + 3*sign(ix) + sign(iy) transforms combinations of directions to unique ids (0..8), which are used above.
            //        e.g.: sign(ix) = -1, sign(iy) = 1 (=north-west) . index = 4 + -3 + 1 = 2
            //    */
            //
            //
            //    int ringcount = int(floor(mHeight / cellsize)) + 1;
            //    int ix, iy;
            //    int ring;
            //    float hdom;
            //    for (ix=-ringcount;ix<=ringcount;ix++)
            //        for (iy=-ringcount; iy<=+ringcount; iy++) {
            //        ring = MathF.Max(abs(ix), abs(iy));
            //        Point pos(ix+p.X, iy+p.Y);
            //        Point p_torus(torusIndex(pos.x(),10,bufferOffset,ru_offset.x()),
            //                       torusIndex(pos.y(),10,bufferOffset,ru_offset.y()));
            //        if (Tree.mHeightGrid.isIndexValid(p_torus)) {
            //            float &rHGrid = Tree.mHeightGrid[p_torus.x(),p_torus.y()).height;
            //            if (rHGrid > mHeight) // skip calculation if grid is higher than tree
            //                continue;
            //            int direction = 4 + (ix?(ix<0?-3:3):0) + (iy?(iy<0?-1:1):0); // 4 + 3*sgn(x) + sgn(y)
            //            hdom = mHeight - dist[direction];
            //            if (ring>1)
            //                hdom -= (ring-1)*10;
            //
            //            rHGrid = MathF.Max(rHGrid, hdom); // write value
            //        } // is valid
            //    } // for (y)
        }


        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland.boku.ac.at/competition+for+light for details.
          */
        public void ReadLightInfluenceField(GlobalSettings globalSettings)
        {
            if (Stamp == null)
            {
                return;
            }
            Stamp reader = Stamp.Reader;
            if (reader == null)
            {
                return;
            }
            Point pos_reader = PositionIndex;
            float outside_area_factor = 0.1F;

            int offset_reader = reader.CenterCellPosition;
            int offset_writer = Stamp.CenterCellPosition;
            int d_offset = offset_writer - offset_reader; // offset on the *stamp* to the crown-cells

            pos_reader.X -= offset_reader;
            pos_reader.Y -= offset_reader;

            float local_dom;

            int x, y;
            double sum = 0.0;
            double value, own_value;
            float grid_value;
            float z, z_zstar;
            int reader_size = reader.Size();
            int rx = pos_reader.X;
            int ry = pos_reader.Y;
            for (y = 0; y < reader_size; ++y, ++ry)
            {
                grid_value = Tree.LightGrid[rx, ry];
                for (x = 0; x < reader_size; ++x)
                {

                    HeightGridValue hgv = Tree.HeightGrid[(rx + x) / Constant.LightPerHeightSize, ry / Constant.LightPerHeightSize]; // the height grid value, ry: gets ++ed in outer loop, rx not
                    local_dom = hgv.Height;
                    z = MathF.Max(Height - reader.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    own_value = 1.0 - Stamp[x, y, d_offset] * mOpacity * z_zstar;
                    own_value = Math.Max(own_value, 0.02);
                    value = grid_value++ / own_value; // remove impact of focal tree
                                                      // additional punishment if pixel is outside:
                    if (hgv.IsOutsideWorld())
                    {
                        value *= outside_area_factor;
                    }
                    //Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                    //if (value>0.)
                    sum += value * reader[x, y];
                }
            }
            LightResourceIndex = (float)(sum);
            // LRI correction...
            double hrel = Height / Tree.HeightGrid[PositionIndex.X / Constant.LightPerHeightSize, PositionIndex.Y / Constant.LightPerHeightSize].Height;
            if (hrel < 1.0F)
            {
                LightResourceIndex = (float)Species.SpeciesSet.GetLriCorrection(globalSettings, LightResourceIndex, hrel);
            }

            if (LightResourceIndex > 1.0F)
            {
                LightResourceIndex = 1.0F;
            }
            // Finally, add LRI of this Tree to the ResourceUnit!
            RU.AddWeightedLeafArea(LeafArea, LightResourceIndex);

            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightIntensityFieldTorus(GlobalSettings globalSettings)
        {
            if (this.Stamp == null)
            {
                return;
            }
            Stamp reader = this.Stamp.Reader;
            if (reader == null)
            {
                return;
            }
            int bufferOffset = Tree.LightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer

            Point pos_reader = new Point((PositionIndex.X - bufferOffset) % Constant.LightPerRUsize + bufferOffset,
                                         (PositionIndex.Y - bufferOffset) % Constant.LightPerRUsize + bufferOffset); // offset within the ha
            Point ru_offset = new Point(PositionIndex.X - pos_reader.X, PositionIndex.Y - pos_reader.Y); // offset of the corner of the resource index

            int offset_reader = reader.CenterCellPosition;
            int offset_writer = Stamp.CenterCellPosition;
            int d_offset = offset_writer - offset_reader; // offset on the *stamp* to the crown-cells

            pos_reader.X -= offset_reader;
            pos_reader.Y -= offset_reader;

            float local_dom;

            int x, y;
            double sum = 0.0;
            double value, own_value;
            float grid_value;
            float z, z_zstar;
            int reader_size = reader.Size();
            int rx = pos_reader.X;
            int ry = pos_reader.Y;
            int xt, yt; // wrapped coords

            for (y = 0; y < reader_size; ++y)
            {
                yt = TorusIndex(ry + y, Constant.LightPerRUsize, bufferOffset, ru_offset.Y);
                for (x = 0; x < reader_size; ++x)
                {
                    xt = TorusIndex(rx + x, Constant.LightPerRUsize, bufferOffset, ru_offset.X);
                    grid_value = Tree.LightGrid[xt, yt];

                    local_dom = Tree.HeightGrid[xt / Constant.LightPerHeightSize, yt / Constant.LightPerHeightSize].Height; // ry: gets ++ed in outer loop, rx not
                    z = MathF.Max(Height - reader.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    own_value = 1.0 - Stamp[x, y, d_offset] * mOpacity * z_zstar;
                    // old: own_value = 1. - mStamp.offsetValue(x,y,d_offset)*mOpacity / local_dom; // old: dom_height;
                    own_value = Math.Max(own_value, 0.02);
                    value = grid_value++ / own_value; // remove impact of focal tree

                    // debug for one tree in HJA
                    //if (id()==178020)
                    //    Debug.WriteLine(x + y + xt + yt + *grid_value + local_dom + own_value + value + (*reader)(x,y);
                    //if (_isnan(value))
                    //    Debug.WriteLine("isnan" + id();
                    if (value * reader[x, y] > 1.0)
                    {
                        Debug.WriteLine("LIFTorus: value > 1.0.");
                    }
                    sum += value * reader[x, y];

                    //} // isIndexValid
                }
            }
            LightResourceIndex = (float)(sum);

            // LRI correction...
            double hrel = Height / Tree.HeightGrid[PositionIndex.X / Constant.LightPerHeightSize, PositionIndex.Y / Constant.LightPerHeightSize].Height;
            if (hrel < 1.0)
            {
                LightResourceIndex = (float)Species.SpeciesSet.GetLriCorrection(globalSettings, LightResourceIndex, hrel);
            }

            if (Double.IsNaN(LightResourceIndex))
            {
                Debug.WriteLine("LRI invalid (nan) " + ID);
                LightResourceIndex = 0.0F;
                //Debug.WriteLine(reader.dump();
            }
            if (LightResourceIndex > 1.0F)
            {
                LightResourceIndex = 1.0F;
            }
            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;

            // Finally, add LRI of this Tree to the ResourceUnit!
            RU.AddWeightedLeafArea(LeafArea, LightResourceIndex);
        }

        public static void ResetStatistics()
        {
            Tree.StampApplications = 0;
            Tree.TreesCreated = 0;
        }

        //#ifdef ALT_TREE_MORTALITY
        //void mortalityParams(double dbh_inc_threshold, int stress_years, double stress_mort_prob)
        //{
        //    _stress_threshold = dbh_inc_threshold;
        //    _stress_years = stress_years;
        //    _stress_death_prob = stress_mort_prob;
        //    Debug.WriteLine("Alternative Mortality enabled: threshold" + dbh_inc_threshold + ", years:" + _stress_years + ", level:" + _stress_death_prob;
        //}
        //#endif

        public void CalcLightResponse(GlobalSettings globalSettings)
        {
            // calculate a light response from lri:
            // http://iland.boku.ac.at/individual+tree+light+availability
            double lri = Global.Limit(LightResourceIndex * RU.LriModifier, 0.0, 1.0); // Eq. (3)
            mLightResponse = (float)(Species.GetLightResponse(globalSettings, lri)); // Eq. (4)
            RU.AddLightResponse(LeafArea, mLightResponse);
        }

        //////////////////////////////////////////////////
        ////  Growth Functions
        //////////////////////////////////////////////////

        /** grow() is the main function of the yearly tree growth.
          The main steps are:
          - Production of GPP/NPP   @sa http://iland.boku.ac.at/primary+production http://iland.boku.ac.at/individual+tree+light+availability
          - Partitioning of NPP to biomass compartments of the tree @sa http://iland.boku.ac.at/allocation
          - Growth of the stem http://iland.boku.ac.at/stem+growth (???)
          Further activties: * the age of the tree is increased
                             * the mortality sub routine is executed
                             * seeds are produced */
        public void Grow(Model model)
        {
            TreeGrowthData d = new TreeGrowthData();
            Age++; // increase age
                    // step 1: get "interception area" of the tree individual [m2]
                    // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
            double effective_area = RU.InterceptedArea(this.LeafArea, mLightResponse);

            // step 2: calculate GPP of the tree based
            // (1) get the amount of GPP for a "unit area" of the tree species
            double raw_gpp_per_area = RU.ResourceUnitSpecies(Species).BiomassGrowth.GppPerArea;
            // (2) GPP (without aging-effect) in kg Biomass / year
            double raw_gpp = raw_gpp_per_area * effective_area;

            // apply aging according to the state of the individuum
            double agingFactor = this.Species.Aging(model.GlobalSettings, this.Height, this.Age);
            RU.AddTreeAging(this.LeafArea, agingFactor);
            double gpp = raw_gpp * agingFactor; //
            d.NppTotal = gpp * Constant.AutotrophicRespiration; // respiration loss (0.47), cf. Waring et al 1998.

            //DBGMODE(
            if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.TreeNpp) && IsDebugging())
            {
                List<object> outList = model.GlobalSettings.DebugList(ID, DebugOutputs.TreeNpp);
                DumpList(outList); // add tree headers
                outList.AddRange(new object[] { LightResourceIndex * RU.LriModifier, mLightResponse, effective_area, raw_gpp, gpp, d.NppTotal, agingFactor });
            }
            //); // DBGMODE()
            if (model.ModelSettings.GrowthEnabled)
            {
                if (d.NppTotal > 0.0)
                {
                    PartitionBiomass(d, model); // split npp to compartments and grow (diameter, height)
                }
            }

            // mortality
            //#ifdef ALT_TREE_MORTALITY
            //    // alternative variant of tree mortality (note: mStrssIndex used otherwise)
            //    altMortality(d);

            //#else
            if (model.ModelSettings.MortalityEnabled)
            {
                Mortality(model, d);
            }
            StressIndex = (float)d.StressIndex;
            //#endif

            if (!IsDead())
            {
                RU.ResourceUnitSpecies(Species).Statistics.Add(this, d);
            }

            // regeneration
            Species.SeedProduction(model.GlobalSettings, this);
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on Duursma, 2007.
          @sa http://iland.boku.ac.at/allocation */
        private void PartitionBiomass(TreeGrowthData d, Model model)
        {
            double npp = d.NppTotal;
            // add content of reserve pool
            npp += mNPPReserve;
            double foliage_mass_allo = Species.GetBiomassFoliage(Dbh);
            double reserve_size = foliage_mass_allo * (1.0 + Species.FinerootFoliageRatio);
            double refill_reserve = Math.Min(reserve_size, (1.0 + Species.FinerootFoliageRatio) * FoliageMass); // not always try to refill reserve 100%

            double apct_wood, apct_root, apct_foliage; // allocation percentages (sum=1) (eta)
            ResourceUnitSpecies rus = RU.ResourceUnitSpecies(Species);
            // turnover rates
            double to_fol = Species.TurnoverLeaf;
            double to_root = Species.TurnoverRoot;
            // the turnover rate of wood depends on the size of the reserve pool:


            double to_wood = refill_reserve / (StemMass + refill_reserve);

            apct_root = rus.BiomassGrowth.RootFraction;
            d.NppAboveground = d.NppTotal * (1.0 - apct_root); // aboveground: total NPP - fraction to roots
            double b_wf = Species.GetWoodFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

            // Duursma 2007, Eq. (20)
            apct_wood = (foliage_mass_allo * to_wood / npp + b_wf * (1.0 - apct_root) - b_wf * foliage_mass_allo * to_fol / npp) / (foliage_mass_allo / StemMass + b_wf);

            apct_wood = Global.Limit(apct_wood, 0.0, 1.0 - apct_root);

            apct_foliage = 1.0 - apct_root - apct_wood;

            //#if DEBUG
            //if (apct_foliage < 0 || apct_wood < 0)
            //{
            //    Debug.WriteLine("transfer to foliage or wood < 0");
            //}
            //if (npp < 0)
            //{
            //    Debug.WriteLine("NPP < 0");
            //}
            //#endif

            // Change of biomass compartments
            double sen_root = FineRootMass * to_root;
            double sen_foliage = FoliageMass * to_fol;
            if (RU.Snags != null)
            {
                RU.Snags.AddTurnoverLitter(this.Species, sen_foliage, sen_root);
            }

            // Roots
            // http://iland.boku.ac.at/allocation#belowground_NPP
            FineRootMass -= (float)sen_root; // reduce only fine root pool
            double delta_root = apct_root * npp;
            // 1st, refill the fine root pool
            double fineroot_miss = FoliageMass * Species.FinerootFoliageRatio - FineRootMass;
            if (fineroot_miss > 0.0)
            {
                double delta_fineroot = Math.Min(fineroot_miss, delta_root);
                FineRootMass += (float)delta_fineroot;
                delta_root -= delta_fineroot;
            }
            // 2nd, the rest of NPP allocated to roots go to coarse roots
            double max_coarse_root = Species.GetBiomassRoot(Dbh);
            CoarseRootMass += (float)delta_root;
            if (CoarseRootMass > max_coarse_root)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (RU.Snags != null)
                {
                    RU.Snags.AddTurnoverWood(Species, CoarseRootMass - max_coarse_root);
                }
                CoarseRootMass = (float)(max_coarse_root);
            }

            // Foliage
            double delta_foliage = apct_foliage * npp - sen_foliage;
            FoliageMass += (float)delta_foliage;
            if (Single.IsNaN(FoliageMass))
            {
                Debug.WriteLine("foliage mass invalid!");
            }
            if (FoliageMass < 0.0F)
            {
                FoliageMass = 0.0F; // limit to zero
            }

            LeafArea = (float)(FoliageMass * Species.SpecificLeafArea); // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            d.StressIndex = MathF.Max((float)(1.0 - (npp) / (to_fol * foliage_mass_allo + to_root * foliage_mass_allo * Species.FinerootFoliageRatio + reserve_size)), 0.0F);

            // Woody compartments
            // see also: http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            double gross_woody = apct_wood * npp;
            double to_reserve = Math.Min(reserve_size, gross_woody);
            mNPPReserve = (float)(to_reserve);
            double net_woody = gross_woody - to_reserve;
            mDbhDelta = 0.0F;

            if (net_woody > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                double net_stem = net_woody * Species.AllometricFractionStem(Dbh);
                d.NppStem = net_stem;
                StemMass += (float)net_woody;
                //  (3) growth of diameter and height baseed on net stem increment
                this.GrowHeightAndDiameter(d, model);
            }

            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.TreePartition) && IsDebugging())
            //{
            //    List<object> outList = GlobalSettings.Instance.DebugList(ID, DebugOutputs.TreePartition);
            //    DumpList(outList); // add tree headers
            //    outList.AddRange(new object[] { npp, apct_foliage, apct_wood, apct_root, delta_foliage, net_woody, delta_root, mNPPReserve, net_stem, d.StressIndex });
            //}

            //#if DEBUG
            //if (StemMass < 0.0 || StemMass > 50000 || FoliageMass < 0.0 || FoliageMass > 2000.0 || CoarseRootMass < 0.0 || CoarseRootMass > 30000 || mNPPReserve > 4000.0)
            //{
            //    Debug.WriteLine("Tree:partitioning: invalid or unlikely pools.");
            //    Debug.WriteLine(GlobalSettings.Instance.DebugListCaptions((DebugOutputs)0));
            //    List<object> dbg = new List<object>();
            //    DumpList(dbg);
            //    Debug.WriteLine(dbg);
            //}
            //#endif
            /*Debug.WriteLineIf(mId == 1 , "partitioning", "dump", dump()
                     + String.Format("npp {0} npp_reserve %9 sen_fol {1} sen_stem {2} sen_root {3} net_fol {4} net_stem {5} net_root %7 to_reserve %8")
                       .arg(npp, senescence_foliage, senescence_stem, senescence_root)
                       .arg(net_foliage, net_stem, net_root, to_reserve, mNPPReserve) );*/
        }

        /** Determination of diamter and height growth based on increment of the stem mass (@p net_stem_npp).
            Refer to XXX for equations and variables.
            This function updates the dbh and height of the tree.
            The equations are based on dbh in meters! */
        private void GrowHeightAndDiameter(TreeGrowthData d, Model model)
        {
            // determine dh-ratio of increment
            // height increment is a function of light competition:
            double hd_growth = this.RelativeHeightGrowth(model.GlobalSettings); // hd of height growth
            double d_m = Dbh / 100.0; // current diameter in [m]
            double net_stem_npp = d.NppStem;

            double d_delta_m = mDbhDelta / 100.0; // increment of last year in [m]

            double mass_factor = Species.VolumeFactor * Species.WoodDensity;
            double stem_mass = mass_factor * d_m * d_m * Height; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            double factor_diameter = 1.0 / (mass_factor * (d_m + d_delta_m) * (d_m + d_delta_m) * (2.0 * Height / d_m + hd_growth));
            double delta_d_estimate = factor_diameter * net_stem_npp; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            double stem_estimate = mass_factor * (d_m + delta_d_estimate) * (d_m + delta_d_estimate) * (Height + delta_d_estimate * hd_growth);
            double stem_residual = stem_estimate - (stem_mass + net_stem_npp);

            // the final increment is then:
            double d_increment = factor_diameter * (net_stem_npp - stem_residual); // Eq. (11)
            if (Math.Abs(stem_residual) > Math.Min(1.0, stem_mass))
            {
                // calculate final residual in stem
                double res_final = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (Height + d_increment * hd_growth) - ((stem_mass + net_stem_npp));
                if (Math.Abs(res_final) > Math.Min(1.0, stem_mass))
                {
                    // for large errors in stem biomass due to errors in diameter increment (> 1kg or >stem mass), we solve the increment iteratively.
                    // first, increase increment with constant step until we overestimate the first time
                    // then,
                    d_increment = 0.02; // start with 2cm increment
                    bool reached_error = false;
                    double step = 0.01; // step-width 1cm
                    double est_stem;
                    do
                    {
                        est_stem = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (Height + d_increment * hd_growth); // estimate with current increment
                        stem_residual = est_stem - (stem_mass + net_stem_npp);

                        if (Math.Abs(stem_residual) < 1.0) // finished, if stem residual below 1kg
                            break;
                        if (stem_residual > 0.0)
                        {
                            d_increment -= step;
                            reached_error = true;
                        }
                        else
                        {
                            d_increment += step;
                        }
                        if (reached_error)
                        {
                            step /= 2.0;
                        }
                    } while (step > 0.00001); // continue until diameter "accuracy" falls below 1/100mm
                }
            }

            if (d_increment < 0.0f)
            {
                Debug.WriteLine("grow_diameter: d_inc < 0.0");
            }
            Debug.WriteLineIf(d_increment < 0.0 || d_increment > 0.1, Dump() +
                       String.Format("hdz {0} factor_diameter {1} stem_residual {2} delta_d_estimate {3} d_increment {4} final residual(kg) {5}",
                       hd_growth, factor_diameter, stem_residual, delta_d_estimate, d_increment, mass_factor * (Dbh + d_increment) * (Dbh + d_increment) * (Height + d_increment * hd_growth) - ((stem_mass + net_stem_npp))),
                       "grow_diameter increment out of range.");

            //DBGMODE(
            // do not calculate res_final twice if already done
            //Debug.WriteLineIf((res_final == 0.0 ? Math.Abs(mass_factor * (d_m + d_increment) * (d_m + d_increment) * (Height + d_increment * hd_growth) - ((stem_mass + net_stem_npp))) : res_final) > 1, Dump(),
            //    "grow_diameter: final residual stem estimate > 1kg");
            //Debug.WriteLineIf(d_increment > 10.0 || d_increment * hd_growth > 10.0, String.Format("d-increment {0} h-increment {1} ", d_increment, d_increment * hd_growth / 100.0) + Dump(),
            //    "grow_diameter growth out of bound");

            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.TreeGrowth) && IsDebugging())
            //{
            //    List<object> outList = GlobalSettings.Instance.DebugList(ID, DebugOutputs.TreeGrowth);
            //    DumpList(outList); // add tree headers
            //    outList.AddRange(new object[] { net_stem_npp, stem_mass, hd_growth, factor_diameter, delta_d_estimate * 100, d_increment * 100 });
            //}

            d_increment = Math.Max(d_increment, 0.0);

            // update state variables
            Dbh += (float)d_increment * 100.0F; // convert from [m] to [cm]
            mDbhDelta = (float)(d_increment * 100.0); // save for next year's growth
            Height += (float)(d_increment * hd_growth);

            // update state of LIP stamp and opacity
            Stamp = Species.GetStamp(Dbh, Height); // get new stamp for updated dimensions
            // calculate the CrownFactor which reflects the opacity of the crown
            float k = (float)model.ModelSettings.LightExtinctionCoefficientOpacity;
            mOpacity = 1.0F - MathF.Exp(-k * LeafArea / Stamp.CrownArea);
        }

        /// return the HD ratio of this year's increment based on the light status.
        private double RelativeHeightGrowth(GlobalSettings globalSettings)
        {
            Species.GetHeightDiameterRatioLimits(globalSettings, this.Dbh, out double hd_low, out double hd_high);

            Debug.WriteLineIf(hd_low > hd_high, Dump(), "relative_height_growth: hd low higher than hd_high");
            Debug.WriteLineIf(hd_low < 10 || hd_high > 250, Dump() + String.Format(" hd-low: {0} hd-high: {1}", hd_low, hd_high), "relative_height_growth: hd out of range ");

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            double lri = Global.Limit(LightResourceIndex * RU.LriModifier, 0.0, 1.0);
            double hd_ratio = hd_high - (hd_high - hd_low) * lri;
            return hd_ratio;
        }

        /** This function is called if a tree dies.
          @sa ResourceUnit::cleanTreeList(), remove() */
        public void Die(Model model, TreeGrowthData d = null)
        {
            SetFlag(Flags.TreeDead, true); // set flag that tree is dead
            RU.TreeDied();
            ResourceUnitSpecies rus = RU.ResourceUnitSpecies(Species);
            rus.StatisticsDead.Add(this, d); // add tree to statistics
            NotifyTreeRemoved(model, MortalityCause.Stress);
            if (RU.Snags != null)
            {
                RU.Snags.AddMortality(this);
            }
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void Remove(Model model, double removeFoliage = 0.0, double removeBranch = 0.0, double removeStem = 0.0)
        {
            SetFlag(Flags.TreeDead, true); // set flag that tree is dead
            SetDeathReasonHarvested();
            RU.TreeDied();
            ResourceUnitSpecies rus = RU.ResourceUnitSpecies(Species);
            rus.StatisticsMgmt.Add(this, null);
            if (IsCutDown())
            {
                NotifyTreeRemoved(model, MortalityCause.CutDown);
            }
            else
            {
                NotifyTreeRemoved(model, MortalityCause.Harvest);
            }
            if (model.Saplings != null)
            {
                model.Saplings.AddSprout(this, model);
            }
            if (RU.Snags != null)
            {
                RU.Snags.AddHarvest(this, removeStem, removeBranch, removeFoliage);
            }
        }

        /// remove the tree due to an special event (disturbance)
        /// this is +- the same as die().
        public void RemoveDisturbance(Model model, double stem_to_soil_fraction, double stem_to_snag_fraction, double branch_to_soil_fraction, double branch_to_snag_fraction, double foliage_to_soil_fraction)
        {
            SetFlag(Flags.TreeDead, true); // set flag that tree is dead
            RU.TreeDied();
            ResourceUnitSpecies rus = RU.ResourceUnitSpecies(Species);
            rus.StatisticsDead.Add(this, null);
            NotifyTreeRemoved(model, MortalityCause.Disturbance);

            if (model.Saplings != null)
            {
                model.Saplings.AddSprout(this, model);
            }
            if (RU.Snags != null)
            {
                if (IsHarvested())
                { // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    RU.Snags.AddHarvest(this, 1.0, 0.0, 0.0);
                }
                else
                {
                    RU.Snags.AddDisturbance(this, stem_to_snag_fraction, stem_to_soil_fraction, branch_to_snag_fraction, branch_to_soil_fraction, foliage_to_soil_fraction);
                }
            }
        }

        public void SetHeight(float height)
        {
            if (height <= 0.0 || height > 150.0)
            {
                Trace.TraceWarning("trying to set tree height to invalid value:" + height + " for tree on RU:" + (RU != null ? RU.BoundingBox : new Rectangle()));
            }
            Height = height;
        }

        private void Mortality(Model model, TreeGrowthData d)
        {
            // death if leaf area is 0
            if (FoliageMass < 0.00001)
            {
                Die(model);
            }

            double p_death, p_stress, p_intrinsic;
            p_intrinsic = Species.DeathProbabilityIntrinsic;
            p_stress = Species.GetDeathProbabilityForStress(d.StressIndex);
            p_death = p_intrinsic + p_stress;
            double p = RandomGenerator.Random(); //0..1
            if (p < p_death)
            {
                // die...
                Die(model);
            }
        }

        //#ifdef ALT_TREE_MORTALITY
        //private void altMortality(TreeGrowthData d)
        //{
        //    // death if leaf area is 0
        //    if (mFoliageMass < 0.00001)
        //        die();

        //    double p_intrinsic, p_stress = 0.;
        //    p_intrinsic = species().deathProb_intrinsic();

        //    if (mDbhDelta < _stress_threshold)
        //    {
        //        mStressIndex++;
        //        if (mStressIndex > _stress_years)
        //            p_stress = _stress_death_prob;
        //    }
        //    else
        //        mStressIndex = 0;

        //    double p = drandom(); //0..1
        //    if (p < p_intrinsic + p_stress)
        //    {
        //        // die...
        //        die();
        //    }
        //}
        //#endif

        private void NotifyTreeRemoved(Model model, MortalityCause reason)
        {
            // tell disturbance modules that a tree died
            model.Modules.TreeDeath(this, (int)reason);

            // update reason, if ABE handled the tree
            if (reason == MortalityCause.Disturbance && IsHarvested())
            {
                reason = MortalityCause.Salavaged;
            }
            if (IsCutDown())
            {
                reason = MortalityCause.CutDown;
            }
            // create output for tree removals
            if (TreeRemovalOutput != null && TreeRemovalOutput.IsEnabled)
            {
                TreeRemovalOutput.LogTreeRemoval(model.GlobalSettings, this, (int)reason);
            }
            if (LandscapeRemovalOutput != null && LandscapeRemovalOutput.IsEnabled)
            {
                LandscapeRemovalOutput.AccumulateTreeRemoval(this, (int)reason);
            }
        }

        public double Volume()
        {
            /// @see Species::volumeFactor() for details
            double volume_factor = Species.VolumeFactor;
            double volume = volume_factor * Dbh * Dbh * Height * 0.0001; // dbh in cm: cm/100 * cm/100 = cm*cm * 0.0001 = m2
            return volume;
        }

        /// return the basal area in m2
        public double BasalArea()
        {
            // A = r^2 * pi = d/2*pi; from cm.m: d/200
            double b = (Dbh / 200.0) * (Dbh / 200.0) * Math.PI;
            return b;
        }
    }
}
