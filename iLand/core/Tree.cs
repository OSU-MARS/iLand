using iLand.abe;
using iLand.output;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    /** @class Tree
        @ingroup core
        A tree is the basic simulation entity of iLand and represents a single tree.
        Trees in iLand are designed to be lightweight, thus the list of stored properties is limited. Basic properties
        are dimensions (dbh, height), biomass pools (stem, leaves, roots), the reserve NPP pool. Additionally, the location and species are stored.
        A Tree has a height of at least 4m; trees below this threshold are covered by the regeneration layer (see Sapling).
        Trees are stored in lists managed at the resource unit level.
      */
    internal class Tree
    {
        private static Grid<float> mGrid = null;
        private static Grid<HeightGridValue> mHeightGrid = null;
        private static TreeRemovedOut mRemovalOutput = null;
        private static LandscapeRemovedOut mLSRemovalOutput = null;
        private static int m_statPrint = 0;
        private static int m_statAboveZ = 0;
        private static int m_statCreated = 0;
        private static int m_nextId = 0;

        public static int statPrints() { return m_statPrint; }
        public static int statCreated() { return m_statCreated; }

        public static void setTreeRemovalOutput(TreeRemovedOut rout) { mRemovalOutput = rout; }
        public static void setLandscapeRemovalOutput(LandscapeRemovedOut rout) { mLSRemovalOutput = rout; }

        // state variables
        public int mId; ///< unique ID of tree
        public int mAge; ///< age of tree in years
        public float mDbh; ///< diameter at breast height [cm]
        public float mHeight; ///< tree height [m]
        public Point mPositionIndex; ///< index of the trees position on the basic LIF grid
        // biomass compartements
        public float mLeafArea; ///< m2 leaf area
        public float mOpacity; ///< multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public float mFoliageMass; ///< kg of foliage (dry)
        public float mWoodyMass; ///< kg biomass of aboveground stem biomass
        public float mFineRootMass; ///< kg biomass of fine roots (linked to foliage mass)
        public float mCoarseRootMass; ///< kg biomass of coarse roots (allometric equation)
        // production relevant
        public float mNPPReserve; ///< NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        private float mLRI; ///< resulting lightResourceIndex
        public float mLightResponse; ///< light response used for distribution of biomass on RU level
        // auxiliary
        public float mDbhDelta; ///< diameter growth [cm]
        public float mStressIndex; ///< stress index (used for mortality)

        // Stamp, Species, Resource Unit
        public Stamp mStamp;
        private Species mSpecies;
        private ResourceUnit mRU;

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

        /// set a Flag 'flag' to the value 'value'.
        private void setFlag(Flags flag, bool value)
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
        /// set a number of flags (need to be constructed by or'ing flags together) at the same time to the Boolean value 'value'.
        private void setFlag(int flag, bool value)
        {
            if (value)
            {
                mFlags |= flag;
            }
            else
            {
                mFlags &= (flag ^ 0xffffff);
            }
        }

        /// retrieve the value of the flag 'flag'.
        private bool flag(Flags flag)
        {
            return (mFlags & (int)flag) != 0;
        }

        // special functions
        private bool isDebugging()
        {
            return flag(Flags.TreeDebugging);
        }

        public Tree()
        {
            mDbh = mHeight = 0;
            mRU = null; 
            mSpecies = null;
            mFlags = mAge = 0;
            mOpacity = mFoliageMass = mWoodyMass = mCoarseRootMass = mFineRootMass = mLeafArea = 0.0F;
            mDbhDelta = mNPPReserve = mLRI = mStressIndex = 0.0F;
            mLightResponse = 0.0F;
            mStamp = null;
            mId = m_nextId++;
            m_statCreated++;
        }

        // access to properties
        public int id() { return mId; } ///< numerical unique ID of the tree
        public int age() { return mAge; } ///< the tree age (years)
        /// @property position The tree does not store the floating point coordinates but only the index of pixel on the LIF grid
        public PointF position() { Debug.Assert(mGrid != null); return mGrid.cellCenterPoint(mPositionIndex); }
        public Point positionIndex() { return mPositionIndex; }
        public Species species() { Debug.Assert(mRU != null); return mSpecies; } ///< pointer to the tree species of the tree.
        public ResourceUnit ru() { Debug.Assert(mRU != null); return mRU; } ///< pointer to the ressource unit the tree belongs to.

        // properties
        public float dbh() { return mDbh; } ///< dimater at breast height in cm
        public float height() { return mHeight; } ///< tree height in m
        public float lightResourceIndex() { return mLRI; } ///< LRI of the tree (updated during readStamp())
        public float leafArea() { return mLeafArea; } ///< leaf area (m2) of the tree
        public bool isDead() { return flag(Flags.TreeDead); } ///< returns true if the tree is already dead.
        // biomass properties
        public float biomassFoliage() { return mFoliageMass; } ///< mass (kg) of foliage
        public float biomassFineRoot() { return mFineRootMass; } ///< mass (kg) of fine roots
        public float biomassCoarseRoot() { return mCoarseRootMass; } ///< mass (kg) of coarse roots
        public float biomassStem() { return mWoodyMass; } ///< mass (kg) of stem
        public float stressIndex() { return mStressIndex; } ///< the scalar stress rating (0..1)

        // management flags (used by ABE management system)
        public void markForHarvest(bool do_mark) { setFlag(Flags.MarkForHarvest, do_mark); }
        public bool isMarkedForHarvest() { return flag(Flags.MarkForHarvest); }
        public void markForCut(bool do_mark) { setFlag(Flags.MarkForCut, do_mark); }
        public bool isMarkedForCut() { return flag(Flags.MarkForCut); }
        public void markCropTree(bool do_mark) { setFlag(Flags.MarkCropTree, do_mark); }
        public bool isMarkedAsCropTree() { return flag(Flags.MarkCropTree); }
        public void markCropCompetitor(bool do_mark) { setFlag(Flags.MarkCropCompetitor, do_mark); }
        public bool isMarkedAsCropCompetitor() { return flag(Flags.MarkCropCompetitor); }
        // death reasons
        public void setDeathReasonWind() { setFlag(Flags.TreeDeadWind, true); }
        public void setDeathReasonBarkBeetle() { setFlag(Flags.TreeDeadBarkBeetle, true); }
        public void setDeathReasonFire() { setFlag(Flags.TreeDeadFire, true); }
        public void setDeathCutdown() { setFlag(Flags.TreeDeadKillAndDrop, true); }
        public void setIsHarvested() { setFlag(Flags.TreeHarvested, true); }

        public bool isDeadWind() { return flag(Flags.TreeDeadWind); }
        public bool isDeadBarkBeetle() { return flag(Flags.TreeDeadBarkBeetle); }
        public bool isDeadFire() { return flag(Flags.TreeDeadFire); }
        public bool isCutdown() { return flag(Flags.TreeDeadKillAndDrop); }
        public bool isHarvested() { return flag(Flags.TreeHarvested); }

        public void setNewId() { mId = m_nextId++; } ///< force a new id for this object (after copying trees)
        public void setId(int id) { mId = id; } ///< set a spcific ID (if provided in stand init file).
        public void setPosition(PointF pos) { Debug.Assert(mGrid != null); mPositionIndex = mGrid.indexAt(pos); }
        public void setPosition(Point posIndex) { mPositionIndex = posIndex; }
        public void setDbh(float dbh) { mDbh = dbh; }
        public void setSpecies(Species ts) { mSpecies = ts; }
        public void setRU(ResourceUnit ru) { mRU = ru; }

        public Stamp stamp() { return mStamp; } ///< TODO: only for debugging purposes

        public void enableDebugging(bool enable = true) { setFlag(Flags.TreeDebugging, enable); }

        public float crownRadius()
        {
            Debug.Assert(mStamp != null);
            return mStamp.crownRadius();
        }

        public float biomassBranch()
        {
            return (float)mSpecies.biomassBranch(mDbh);
        }

        public static void setGrid(Grid<float> gridToStamp, Grid<HeightGridValue> dominanceGrid)
        {
            mGrid = gridToStamp; mHeightGrid = dominanceGrid;
        }

        // calculate the thickness of the bark of the tree
        private double barkThickness()
        {
            return mSpecies.barkThickness(mDbh);
        }

        /// dumps some core variables of a tree to a string.
        private string dump()
        {
            string result = String.Format("id {0} species {1} dbh {2} h {3} x/y {4}/{5} ru# {6} LRI {7}",
                                          mId, species().id(), mDbh, mHeight,
                                          position().X, position().Y,
                                          mRU.index(), mLRI);
            return result;
        }

        private void dumpList(List<object> rTargetList)
        {
            rTargetList.AddRange(new object[] { mId, species().id(), mDbh, mHeight, position().X, position().Y, mRU.index(), mLRI,
                                                mWoodyMass, mCoarseRootMass + mFoliageMass + mLeafArea } );
        }

        public void setup()
        {
            if (mDbh <= 0 || mHeight <= 0)
            {
                throw new NotSupportedException(String.Format("Error: trying to set up a tree with invalid dimensions: dbh: {0} height: {1} id: {2} RU-index: {3}", mDbh, mHeight, mId, mRU.index()));
            }
            // check stamp
            Debug.Assert(species() != null, "setup()", "species is NULL");
            mStamp = species().stamp(mDbh, mHeight);
            if (mStamp == null)
            {
                throw new NotSupportedException("setup() with invalid stamp!");
            }

            mFoliageMass = (float)(species().biomassFoliage(mDbh));
            mCoarseRootMass = (float)(species().biomassRoot(mDbh)); // coarse root (allometry)
            mFineRootMass = (float)(mFoliageMass * species().finerootFoliageRatio()); //  fine root (size defined  by finerootFoliageRatio)
            mWoodyMass = (float)(species().biomassWoody(mDbh));

            // LeafArea[m2] = LeafMass[kg] * specificLeafArea[m2/kg]
            mLeafArea = (float)(mFoliageMass * species().specificLeafArea());
            mOpacity = (float)(1.0F - MathF.Exp(-(float)GlobalSettings.instance().model().settings().lightExtinctionCoefficientOpacity * mLeafArea / mStamp.crownArea()));
            mNPPReserve = (float)((1 + species().finerootFoliageRatio()) * mFoliageMass); // initial value
            mDbhDelta = 0.1F; // initial value: used in growth() to estimate diameter increment
        }

        public void setAge(int age, float treeheight)
        {
            mAge = age;
            if (age == 0)
            {
                // estimate age using the tree height
                mAge = mSpecies.estimateAge(treeheight);
            }
        }

        //////////////////////////////////////////////////
        ////  Light functions (Pattern-stuff)
        //////////////////////////////////////////////////
        public void applyLIP()
        {
            if (mStamp == null)
            {
                return;
            }
            Debug.Assert(mGrid != null && mStamp != null && mRU != null);
            Point pos = mPositionIndex;
            int offset = mStamp.offset();
            pos.X -= offset;
            pos.Y -= offset;

            float local_dom; // height of Z* on the current position
            int x, y;
            float value, z, z_zstar;
            int gr_stamp = mStamp.size();

            if (!mGrid.isIndexValid(pos) || !mGrid.isIndexValid(new Point(pos.X + gr_stamp, pos.Y + gr_stamp)))
            {
                // this should not happen because of the buffer
                return;
            }
            int grid_y = pos.Y;
            for (y = 0; y < gr_stamp; ++y)
            {

                float grid_value_ptr = mGrid.ptr(pos.X, grid_y);
                int grid_x = pos.X;
                for (x = 0; x < gr_stamp; ++x, ++grid_x, ++grid_value_ptr)
                {
                    // suppose there is no stamping outside
                    value = mStamp[x, y]; // stampvalue
                                             //if (value>0.f) {
                    local_dom = mHeightGrid[grid_x / Constant.cPxPerHeight, grid_y / Constant.cPxPerHeight].height;
                    z = MathF.Max(mHeight - mStamp.distanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    value = 1.0F - value * mOpacity * z_zstar; // calculated value
                    value = MathF.Max(value, 0.02F); // limit value

                    grid_value_ptr *= value;
                }
                grid_y++;
            }

            m_statPrint++; // count # of stamp applications...
        }

        /// helper function for gluing the edges together
        /// index: index at grid
        /// count: number of pixels that are the simulation area (e.g. 100m and 2m pixel . 50)
        /// buffer: size of buffer around simulation area (in pixels)
        private int torusIndex(int index, int count, int buffer, int ru_index)
        {
            return buffer + ru_index + (index - buffer + count) % count;
        }

        /** Apply LIPs. This "Torus" functions wraps the influence at the edges of a 1ha simulation area.
          */
        public void applyLIP_torus()
        {
            if (mStamp == null)
            {
                return;
            }
            Debug.Assert(mGrid != null && mStamp != null && mRU != null);
            int bufferOffset = mGrid.indexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer
            Point pos = new Point((mPositionIndex.X - bufferOffset) % Constant.cPxPerRU + bufferOffset,
                                  (mPositionIndex.Y - bufferOffset) % Constant.cPxPerRU + bufferOffset); // offset within the ha
            Point ru_offset = new Point(mPositionIndex.X - pos.X, mPositionIndex.Y - pos.Y); // offset of the corner of the resource index

            int offset = mStamp.offset();
            pos.X -= offset;
            pos.Y -= offset;

            float local_dom; // height of Z* on the current position
            int x, y;
            float value;
            int gr_stamp = mStamp.size();
            int grid_x, grid_y;
            float grid_value;
            if (!mGrid.isIndexValid(pos) || !mGrid.isIndexValid(new Point(gr_stamp + pos.X, gr_stamp + pos.Y)))
            {
                // todo: in this case we should use another algorithm!!! necessary????
                return;
            }
            float z, z_zstar;
            int xt, yt; // wraparound coordinates
            for (y = 0; y < gr_stamp; ++y)
            {
                grid_y = pos.Y + y;
                yt = torusIndex(grid_y, Constant.cPxPerRU, bufferOffset, ru_offset.Y); // 50 cells per 100m
                for (x = 0; x < gr_stamp; ++x)
                {
                    // suppose there is no stamping outside
                    grid_x = pos.X + x;
                    xt = torusIndex(grid_x, Constant.cPxPerRU, bufferOffset, ru_offset.X);

                    local_dom = mHeightGrid.valueAtIndex(xt / Constant.cPxPerHeight, yt / Constant.cPxPerHeight).height;

                    z = MathF.Max(mHeight - mStamp.distanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    value = mStamp[x, y]; // stampvalue
                    value = 1.0F - value * mOpacity * z_zstar; // calculated value
                                                               // old: value = 1. - value*mOpacity / local_dom; // calculated value
                    value = MathF.Max(value, 0.02f); // limit value

                    grid_value = mGrid.ptr(xt, yt); // use wraparound coordinates
                    grid_value *= value;
                }
            }

            m_statPrint++; // count # of stamp applications...
        }

        /** heightGrid()
          This function calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
            */
        public void heightGrid()
        {
            Point p = new Point(mPositionIndex.X / Constant.cPxPerHeight, mPositionIndex.Y / Constant.cPxPerHeight); // pos of tree on height grid

            // count trees that are on height-grid cells (used for stockable area)
            mHeightGrid.valueAtIndex(p).increaseCount();
            if (mHeight > mHeightGrid.valueAtIndex(p).height)
            {
                mHeightGrid.valueAtIndex(p).height = mHeight;
            }

            int r = mStamp.reader().offset(); // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int index_eastwest = mPositionIndex.X % Constant.cPxPerHeight; // 4: very west, 0 east edge
            int index_northsouth = mPositionIndex.Y % Constant.cPxPerHeight; // 4: northern edge, 0: southern edge
            if (index_eastwest - r < 0)
            { // east
                mHeightGrid.valueAtIndex(p.X - 1, p.Y).height = MathF.Max(mHeightGrid.valueAtIndex(p.X - 1, p.Y).height, mHeight);
            }
            if (index_eastwest + r >= Constant.cPxPerHeight)
            {  // west
                mHeightGrid.valueAtIndex(p.X + 1, p.Y).height = MathF.Max(mHeightGrid.valueAtIndex(p.X + 1, p.Y).height, mHeight);
            }
            if (index_northsouth - r < 0)
            {  // south
                mHeightGrid.valueAtIndex(p.X, p.Y - 1).height = MathF.Max(mHeightGrid.valueAtIndex(p.X, p.Y - 1).height, mHeight);
            }
            if (index_northsouth + r >= Constant.cPxPerHeight)
            {  // north
                mHeightGrid.valueAtIndex(p.X, p.Y + 1).height = MathF.Max(mHeightGrid.valueAtIndex(p.X, p.Y + 1).height, mHeight);
            }

            // without spread of the height grid
            //    // height of Z*
            //    float cellsize = mHeightGrid.cellsize();
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
            //        if (mHeightGrid.isIndexValid(pos)) {
            //            float &rHGrid = mHeightGrid.valueAtIndex(pos).height;
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

        public void heightGrid_torus()
        {
            // height of Z*
            Point p = new Point(mPositionIndex.X / Constant.cPxPerHeight, mPositionIndex.Y / Constant.cPxPerHeight); // pos of tree on height grid
            int bufferOffset = mHeightGrid.indexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer (i.e.: size of buffer in height-pixels)
            p.X = ((p.X - bufferOffset) % 10 + bufferOffset); // 10: 10 x 10m pixeln in 100m
            p.Y = ((p.Y - bufferOffset) % 10 + bufferOffset);

            // torus coordinates: ru_offset = coords of lower left corner of 1ha patch
            Point ru_offset = new Point(mPositionIndex.X / Constant.cPxPerHeight - p.X, mPositionIndex.Y / Constant.cPxPerHeight - p.Y);

            // count trees that are on height-grid cells (used for stockable area)
            HeightGridValue v = mHeightGrid.valueAtIndex(torusIndex(p.X, 10, bufferOffset, ru_offset.X),
                                                         torusIndex(p.Y, 10, bufferOffset, ru_offset.Y));
            v.increaseCount();
            v.height = MathF.Max(v.height, mHeight);


            int r = mStamp.reader().offset(); // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int index_eastwest = mPositionIndex.X % Constant.cPxPerHeight; // 4: very west, 0 east edge
            int index_northsouth = mPositionIndex.Y % Constant.cPxPerHeight; // 4: northern edge, 0: southern edge
            if (index_eastwest - r < 0)
            { // east
                v = mHeightGrid.valueAtIndex(torusIndex(p.X - 1, 10, bufferOffset, ru_offset.X),
                                                        torusIndex(p.Y, 10, bufferOffset, ru_offset.Y));
                v.height = MathF.Max(v.height, mHeight);
            }
            if (index_eastwest + r >= Constant.cPxPerHeight)
            {  // west
                v = mHeightGrid.valueAtIndex(torusIndex(p.X + 1, 10, bufferOffset, ru_offset.X),
                                                        torusIndex(p.Y, 10, bufferOffset, ru_offset.Y));
                v.height = MathF.Max(v.height, mHeight);
            }
            if (index_northsouth - r < 0)
            {  // south
                v = mHeightGrid.valueAtIndex(torusIndex(p.X, 10, bufferOffset, ru_offset.X),
                                             torusIndex(p.Y - 1, 10, bufferOffset, ru_offset.Y));
                v.height = MathF.Max(v.height, mHeight);
            }
            if (index_northsouth + r >= Constant.cPxPerHeight)
            {  // north
                v = mHeightGrid.valueAtIndex(torusIndex(p.X, 10, bufferOffset, ru_offset.X),
                                             torusIndex(p.Y + 1, 10, bufferOffset, ru_offset.Y));
                v.height = MathF.Max(v.height, mHeight);
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
            //        if (mHeightGrid.isIndexValid(p_torus)) {
            //            float &rHGrid = mHeightGrid.valueAtIndex(p_torus.x(),p_torus.y()).height;
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
        public void readLIF()
        {
            if (mStamp == null)
            {
                return;
            }
            Stamp reader = mStamp.reader();
            if (reader == null)
            {
                return;
            }
            Point pos_reader = mPositionIndex;
            float outside_area_factor = 0.1F;

            int offset_reader = reader.offset();
            int offset_writer = mStamp.offset();
            int d_offset = offset_writer - offset_reader; // offset on the *stamp* to the crown-cells

            pos_reader.X -= offset_reader;
            pos_reader.Y -= offset_reader;

            float local_dom;

            int x, y;
            double sum = 0.0;
            double value, own_value;
            float grid_value;
            float z, z_zstar;
            int reader_size = reader.size();
            int rx = pos_reader.X;
            int ry = pos_reader.Y;
            for (y = 0; y < reader_size; ++y, ++ry)
            {
                grid_value = mGrid.ptr(rx, ry);
                for (x = 0; x < reader_size; ++x)
                {

                    HeightGridValue hgv = mHeightGrid.constValueAtIndex((rx + x) / Constant.cPxPerHeight, ry / Constant.cPxPerHeight); // the height grid value, ry: gets ++ed in outer loop, rx not
                    local_dom = hgv.height;
                    z = MathF.Max(mHeight - reader.distanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    own_value = 1.0 - mStamp.offsetValue(x, y, d_offset) * mOpacity * z_zstar;
                    own_value = Math.Max(own_value, 0.02);
                    value = grid_value++ / own_value; // remove impact of focal tree
                                                      // additional punishment if pixel is outside:
                    if (hgv.isForestOutside())
                    {
                        value *= outside_area_factor;
                    }
                    //Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                    //if (value>0.)
                    sum += value * reader[x, y];
                }
            }
            mLRI = (float)(sum);
            // LRI correction...
            double hrel = mHeight / mHeightGrid.valueAtIndex(mPositionIndex.X / Constant.cPxPerHeight, mPositionIndex.Y / Constant.cPxPerHeight).height;
            if (hrel < 1.0F)
            {
                mLRI = (float)(species().speciesSet().LRIcorrection(mLRI, hrel));
            }

            if (mLRI > 1.0F)
            {
                mLRI = 1.0F;
            }
            // Finally, add LRI of this Tree to the ResourceUnit!
            mRU.addWLA(mLeafArea, mLRI);

            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
        }

        /// Torus version of read stamp (glued edges)
        public void readLIF_torus()
        {
            if (mStamp == null)
            {
                return;
            }
            Stamp reader = mStamp.reader();
            if (reader == null)
            {
                return;
            }
            int bufferOffset = mGrid.indexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer

            Point pos_reader = new Point((mPositionIndex.X - bufferOffset) % Constant.cPxPerRU + bufferOffset,
                                         (mPositionIndex.Y - bufferOffset) % Constant.cPxPerRU + bufferOffset); // offset within the ha
            Point ru_offset = new Point(mPositionIndex.X - pos_reader.X, mPositionIndex.Y - pos_reader.Y); // offset of the corner of the resource index

            int offset_reader = reader.offset();
            int offset_writer = mStamp.offset();
            int d_offset = offset_writer - offset_reader; // offset on the *stamp* to the crown-cells

            pos_reader.X -= offset_reader;
            pos_reader.Y -= offset_reader;

            float local_dom;

            int x, y;
            double sum = 0.0;
            double value, own_value;
            float grid_value;
            float z, z_zstar;
            int reader_size = reader.size();
            int rx = pos_reader.X;
            int ry = pos_reader.Y;
            int xt, yt; // wrapped coords

            for (y = 0; y < reader_size; ++y)
            {
                yt = torusIndex(ry + y, Constant.cPxPerRU, bufferOffset, ru_offset.Y);
                for (x = 0; x < reader_size; ++x)
                {
                    xt = torusIndex(rx + x, Constant.cPxPerRU, bufferOffset, ru_offset.X);
                    grid_value = mGrid.ptr(xt, yt);

                    local_dom = mHeightGrid.valueAtIndex(xt / Constant.cPxPerHeight, yt / Constant.cPxPerHeight).height; // ry: gets ++ed in outer loop, rx not
                    z = MathF.Max(mHeight - reader.distanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    own_value = 1.0 - mStamp.offsetValue(x, y, d_offset) * mOpacity * z_zstar;
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
            mLRI = (float)(sum);

            // LRI correction...
            double hrel = mHeight / mHeightGrid.valueAtIndex(mPositionIndex.X / Constant.cPxPerHeight, mPositionIndex.Y / Constant.cPxPerHeight).height;
            if (hrel < 1.0)
            {
                mLRI = (float)(species().speciesSet().LRIcorrection(mLRI, hrel));
            }

            if (Double.IsNaN(mLRI))
            {
                Debug.WriteLine("LRI invalid (nan) " + id());
                mLRI = 0.0F;
                //Debug.WriteLine(reader.dump();
            }
            if (mLRI > 1.0F)
            {
                mLRI = 1.0F;
            }
            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;

            // Finally, add LRI of this Tree to the ResourceUnit!
            mRU.addWLA(mLeafArea, mLRI);
        }

        public static void resetStatistics()
        {
            m_statPrint = 0;
            m_statCreated = 0;
            m_statAboveZ = 0;
            m_nextId = 1;
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

        public void calcLightResponse()
        {
            // calculate a light response from lri:
            // http://iland.boku.ac.at/individual+tree+light+availability
            double lri = Global.limit(mLRI * mRU.LRImodifier(), 0.0, 1.0); // Eq. (3)
            mLightResponse = (float)(mSpecies.lightResponse(lri)); // Eq. (4)
            mRU.addLR(mLeafArea, mLightResponse);
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
        public void grow()
        {
            TreeGrowthData d = new TreeGrowthData();
            mAge++; // increase age
                    // step 1: get "interception area" of the tree individual [m2]
                    // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
            double effective_area = mRU.interceptedArea(mLeafArea, mLightResponse);

            // step 2: calculate GPP of the tree based
            // (1) get the amount of GPP for a "unit area" of the tree species
            double raw_gpp_per_area = mRU.resourceUnitSpecies(species()).prod3PG().GPPperArea();
            // (2) GPP (without aging-effect) in kg Biomass / year
            double raw_gpp = raw_gpp_per_area * effective_area;

            // apply aging according to the state of the individuum
            double aging_factor = mSpecies.aging(mHeight, mAge);
            mRU.addTreeAging(mLeafArea, aging_factor);
            double gpp = raw_gpp * aging_factor; //
            d.NPP = gpp * Constant.cAutotrophicRespiration; // respiration loss (0.47), cf. Waring et al 1998.

            //DBGMODE(
            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dTreeNPP) && isDebugging())
            {
                List<object> outList = GlobalSettings.instance().debugList(mId, DebugOutputs.dTreeNPP);
                dumpList(outList); // add tree headers
                outList.AddRange(new object[] { mLRI * mRU.LRImodifier(), mLightResponse, effective_area, raw_gpp, gpp, d.NPP, aging_factor });
            }
            //); // DBGMODE()
            if (GlobalSettings.instance().model().settings().growthEnabled)
            {
                if (d.NPP > 0.0)
                {
                    partitioning(d); // split npp to compartments and grow (diameter, height)
                }
            }

            // mortality
            //#ifdef ALT_TREE_MORTALITY
            //    // alternative variant of tree mortality (note: mStrssIndex used otherwise)
            //    altMortality(d);

            //#else
            if (GlobalSettings.instance().model().settings().mortalityEnabled)
            {
                mortality(d);
            }
            mStressIndex = (float)d.stress_index;
            //#endif

            if (!isDead())
            {
                mRU.resourceUnitSpecies(species()).statistics().add(this, d);
            }

            // regeneration
            mSpecies.seedProduction(this);
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on Duursma, 2007.
          @sa http://iland.boku.ac.at/allocation */
        private void partitioning(TreeGrowthData d)
        {
            double npp = d.NPP;
            // add content of reserve pool
            npp += mNPPReserve;
            double foliage_mass_allo = species().biomassFoliage(mDbh);
            double reserve_size = foliage_mass_allo * (1.0 + mSpecies.finerootFoliageRatio());
            double refill_reserve = Math.Min(reserve_size, (1.0 + mSpecies.finerootFoliageRatio()) * mFoliageMass); // not always try to refill reserve 100%

            double apct_wood, apct_root, apct_foliage; // allocation percentages (sum=1) (eta)
            ResourceUnitSpecies rus = mRU.resourceUnitSpecies(species());
            // turnover rates
            double to_fol = species().turnoverLeaf();
            double to_root = species().turnoverRoot();
            // the turnover rate of wood depends on the size of the reserve pool:


            double to_wood = refill_reserve / (mWoodyMass + refill_reserve);

            apct_root = rus.prod3PG().rootFraction();
            d.NPP_above = d.NPP * (1.0 - apct_root); // aboveground: total NPP - fraction to roots
            double b_wf = species().allometricRatio_wf(); // ratio of allometric exponents (b_woody / b_foliage)

            // Duursma 2007, Eq. (20)
            apct_wood = (foliage_mass_allo * to_wood / npp + b_wf * (1.0 - apct_root) - b_wf * foliage_mass_allo * to_fol / npp) / (foliage_mass_allo / mWoodyMass + b_wf);

            apct_wood = Global.limit(apct_wood, 0.0, 1.0 - apct_root);

            apct_foliage = 1.0 - apct_root - apct_wood;


#if DEBUG
            if (apct_foliage < 0 || apct_wood < 0)
            {
                Debug.WriteLine("transfer to foliage or wood < 0");
            }
            if (npp < 0)
            {
                Debug.WriteLine("NPP < 0");
            }
#endif

            // Change of biomass compartments
            double sen_root = mFineRootMass * to_root;
            double sen_foliage = mFoliageMass * to_fol;
            if (ru().snag() != null)
            {
                ru().snag().addTurnoverLitter(this.species(), sen_foliage, sen_root);
            }

            // Roots
            // http://iland.boku.ac.at/allocation#belowground_NPP
            mFineRootMass -= (float)sen_root; // reduce only fine root pool
            double delta_root = apct_root * npp;
            // 1st, refill the fine root pool
            double fineroot_miss = mFoliageMass * mSpecies.finerootFoliageRatio() - mFineRootMass;
            if (fineroot_miss > 0.0)
            {
                double delta_fineroot = Math.Min(fineroot_miss, delta_root);
                mFineRootMass += (float)delta_fineroot;
                delta_root -= delta_fineroot;
            }
            // 2nd, the rest of NPP allocated to roots go to coarse roots
            double max_coarse_root = species().biomassRoot(mDbh);
            mCoarseRootMass += (float)delta_root;
            if (mCoarseRootMass > max_coarse_root)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (ru().snag() != null)
                {
                    ru().snag().addTurnoverWood(species(), mCoarseRootMass - max_coarse_root);
                }
                mCoarseRootMass = (float)(max_coarse_root);
            }

            // Foliage
            double delta_foliage = apct_foliage * npp - sen_foliage;
            mFoliageMass += (float)delta_foliage;
            if (Single.IsNaN(mFoliageMass))
            {
                Debug.WriteLine("foliage mass invalid!");
            }
            if (mFoliageMass < 0.0F)
            {
                mFoliageMass = 0.0F; // limit to zero
            }

            mLeafArea = (float)(mFoliageMass * species().specificLeafArea()); // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            d.stress_index = MathF.Max((float)(1.0 - (npp) / (to_fol * foliage_mass_allo + to_root * foliage_mass_allo * species().finerootFoliageRatio() + reserve_size)), 0.0F);

            // Woody compartments
            // see also: http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            double gross_woody = apct_wood * npp;
            double to_reserve = Math.Min(reserve_size, gross_woody);
            mNPPReserve = (float)(to_reserve);
            double net_woody = gross_woody - to_reserve;
            double net_stem = 0.0;
            mDbhDelta = 0.0F;

            if (net_woody > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                net_stem = net_woody * species().allometricFractionStem(mDbh);
                d.NPP_stem = net_stem;
                mWoodyMass += (float)net_woody;
                //  (3) growth of diameter and height baseed on net stem increment
                grow_diameter(d);
            }

            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dTreePartition) && isDebugging())
            {
                List<object> outList = GlobalSettings.instance().debugList(mId, DebugOutputs.dTreePartition);
                dumpList(outList); // add tree headers
                outList.AddRange(new object[] { npp, apct_foliage, apct_wood, apct_root, delta_foliage, net_woody, delta_root, mNPPReserve, net_stem, d.stress_index });
            }

            #if DEBUG
            if (mWoodyMass < 0.0 || mWoodyMass > 50000 || mFoliageMass < 0.0 || mFoliageMass > 2000.0 || mCoarseRootMass < 0.0 || mCoarseRootMass > 30000 || mNPPReserve > 4000.0)
            {
                Debug.WriteLine("Tree:partitioning: invalid or unlikely pools.");
                Debug.WriteLine(GlobalSettings.instance().debugListCaptions((DebugOutputs)0));
                List<object> dbg = new List<object>();
                dumpList(dbg);
                Debug.WriteLine(dbg);
            }
            #endif
            /*Debug.WriteLineIf(mId == 1 , "partitioning", "dump", dump()
                     + String.Format("npp {0} npp_reserve %9 sen_fol {1} sen_stem {2} sen_root {3} net_fol {4} net_stem {5} net_root %7 to_reserve %8")
                       .arg(npp, senescence_foliage, senescence_stem, senescence_root)
                       .arg(net_foliage, net_stem, net_root, to_reserve, mNPPReserve) );*/
        }

        /** Determination of diamter and height growth based on increment of the stem mass (@p net_stem_npp).
            Refer to XXX for equations and variables.
            This function updates the dbh and height of the tree.
            The equations are based on dbh in meters! */
        private void grow_diameter(TreeGrowthData d)
        {
            // determine dh-ratio of increment
            // height increment is a function of light competition:
            double hd_growth = relative_height_growth(); // hd of height growth
            double d_m = mDbh / 100.0; // current diameter in [m]
            double net_stem_npp = d.NPP_stem;

            double d_delta_m = mDbhDelta / 100.0; // increment of last year in [m]

            double mass_factor = species().volumeFactor() * species().density();
            double stem_mass = mass_factor * d_m * d_m * mHeight; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            double factor_diameter = 1.0 / (mass_factor * (d_m + d_delta_m) * (d_m + d_delta_m) * (2.0 * mHeight / d_m + hd_growth));
            double delta_d_estimate = factor_diameter * net_stem_npp; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            double stem_estimate = mass_factor * (d_m + delta_d_estimate) * (d_m + delta_d_estimate) * (mHeight + delta_d_estimate * hd_growth);
            double stem_residual = stem_estimate - (stem_mass + net_stem_npp);

            // the final increment is then:
            double d_increment = factor_diameter * (net_stem_npp - stem_residual); // Eq. (11)
            double res_final = 0.0;
            if (Math.Abs(stem_residual) > Math.Min(1.0, stem_mass))
            {

                // calculate final residual in stem
                res_final = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (mHeight + d_increment * hd_growth) - ((stem_mass + net_stem_npp));
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
                        est_stem = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (mHeight + d_increment * hd_growth); // estimate with current increment
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
            Debug.WriteLineIf(d_increment < 0.0 || d_increment > 0.1, dump() +
                       String.Format("hdz {0} factor_diameter {1} stem_residual {2} delta_d_estimate {3} d_increment {4} final residual(kg) {5}",
                       hd_growth, factor_diameter, stem_residual, delta_d_estimate, d_increment, mass_factor * (mDbh + d_increment) * (mDbh + d_increment) * (mHeight + d_increment * hd_growth) - ((stem_mass + net_stem_npp))),
                       "grow_diameter increment out of range.");

            //DBGMODE(
            // do not calculate res_final twice if already done
            Debug.WriteLineIf((res_final == 0.0 ? Math.Abs(mass_factor * (d_m + d_increment) * (d_m + d_increment) * (mHeight + d_increment * hd_growth) - ((stem_mass + net_stem_npp))) : res_final) > 1, dump(),
                "grow_diameter: final residual stem estimate > 1kg");
            Debug.WriteLineIf(d_increment > 10.0 || d_increment * hd_growth > 10.0, String.Format("d-increment {0} h-increment {1} ", d_increment, d_increment * hd_growth / 100.0) + dump(),
                "grow_diameter growth out of bound");

            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dTreeGrowth) && isDebugging())
            {
                List<object> outList = GlobalSettings.instance().debugList(mId, DebugOutputs.dTreeGrowth);
                dumpList(outList); // add tree headers
                outList.AddRange(new object[] { net_stem_npp, stem_mass, hd_growth, factor_diameter, delta_d_estimate * 100, d_increment * 100 });
            }

            d_increment = Math.Max(d_increment, 0.0);

            // update state variables
            mDbh += (float)d_increment * 100.0F; // convert from [m] to [cm]
            mDbhDelta = (float)(d_increment * 100.0); // save for next year's growth
            mHeight += (float)(d_increment * hd_growth);

            // update state of LIP stamp and opacity
            mStamp = species().stamp(mDbh, mHeight); // get new stamp for updated dimensions
                                                     // calculate the CrownFactor which reflects the opacity of the crown
            float k = (float)GlobalSettings.instance().model().settings().lightExtinctionCoefficientOpacity;
            mOpacity = 1.0F - MathF.Exp(-k * mLeafArea / mStamp.crownArea());
        }

        /// return the HD ratio of this year's increment based on the light status.
        private double relative_height_growth()
        {
            mSpecies.hdRange(mDbh, out double hd_low, out double hd_high);

            Debug.WriteLineIf(hd_low > hd_high, dump(), "relative_height_growth: hd low higher than hd_high");
            Debug.WriteLineIf(hd_low < 10 || hd_high > 250, dump() + String.Format(" hd-low: {0} hd-high: {1}", hd_low, hd_high), "relative_height_growth: hd out of range ");

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            double lri = Global.limit(mLRI * mRU.LRImodifier(), 0.0, 1.0);
            double hd_ratio = hd_high - (hd_high - hd_low) * lri;
            return hd_ratio;
        }

        /** This function is called if a tree dies.
          @sa ResourceUnit::cleanTreeList(), remove() */
        public void die(TreeGrowthData d = null)
        {
            setFlag(Flags.TreeDead, true); // set flag that tree is dead
            mRU.treeDied();
            ResourceUnitSpecies rus = mRU.resourceUnitSpecies(species());
            rus.statisticsDead().add(this, d); // add tree to statistics
            notifyTreeRemoved(TreeRemovalType.TreeDeath);
            if (ru().snag() != null)
            {
                ru().snag().addMortality(this);
            }
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void remove(double removeFoliage = 0.0, double removeBranch = 0.0, double removeStem = 0.0)
        {
            setFlag(Flags.TreeDead, true); // set flag that tree is dead
            setIsHarvested();
            mRU.treeDied();
            ResourceUnitSpecies rus = mRU.resourceUnitSpecies(species());
            rus.statisticsMgmt().add(this, null);
            if (isCutdown())
            {
                notifyTreeRemoved(TreeRemovalType.TreeCutDown);
            }
            else
            {
                notifyTreeRemoved(TreeRemovalType.TreeHarvest);
            }
            if (GlobalSettings.instance().model().saplings() != null)
            {
                GlobalSettings.instance().model().saplings().addSprout(this);
            }
            if (ru().snag() != null)
            {
                ru().snag().addHarvest(this, removeStem, removeBranch, removeFoliage);
            }
        }

        /// remove the tree due to an special event (disturbance)
        /// this is +- the same as die().
        public void removeDisturbance(double stem_to_soil_fraction, double stem_to_snag_fraction, double branch_to_soil_fraction, double branch_to_snag_fraction, double foliage_to_soil_fraction)
        {
            setFlag(Flags.TreeDead, true); // set flag that tree is dead
            mRU.treeDied();
            ResourceUnitSpecies rus = mRU.resourceUnitSpecies(species());
            rus.statisticsDead().add(this, null);
            notifyTreeRemoved(TreeRemovalType.TreeDisturbance);

            if (GlobalSettings.instance().model().saplings() != null)
            {
                GlobalSettings.instance().model().saplings().addSprout(this);
            }
            if (ru().snag() != null)
            {
                if (isHarvested())
                { // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    ru().snag().addHarvest(this, 1.0, 0.0, 0.0);
                }
                else
                {
                    ru().snag().addDisturbance(this, stem_to_snag_fraction, stem_to_soil_fraction, branch_to_snag_fraction, branch_to_soil_fraction, foliage_to_soil_fraction);
                }
            }
        }

        /// remove a part of the biomass of the tree, e.g. due to fire.
        private void removeBiomassOfTree(double removeFoliageFraction, double removeBranchFraction, double removeStemFraction)
        {
            mFoliageMass *= 1.0F - (float)removeFoliageFraction;
            mWoodyMass *= (1.0F - (float)removeStemFraction);
            // we have a problem with the branches: this currently cannot be done properly!
            // (void)removeBranchFraction; // silence warning
        }

        public void setHeight(float height)
        {
            if (height <= 0.0 || height > 150.0)
            {
                Trace.TraceWarning("trying to set tree height to invalid value:" + height + " for tree on RU:" + (mRU != null ? mRU.boundingBox() : new Rectangle()));
            }
            mHeight = height;
        }

        private void mortality(TreeGrowthData d)
        {
            // death if leaf area is 0
            if (mFoliageMass < 0.00001)
            {
                die();
            }

            double p_death, p_stress, p_intrinsic;
            p_intrinsic = species().deathProb_intrinsic();
            p_stress = species().deathProb_stress(d.stress_index);
            p_death = p_intrinsic + p_stress;
            double p = RandomGenerator.drandom(); //0..1
            if (p < p_death)
            {
                // die...
                die();
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

        private void notifyTreeRemoved(TreeRemovalType reason)
        {
            // this information is used to track the removed volume for stands based on grids (and for salvaging operations)
            ForestManagementEngine abe = GlobalSettings.instance().model().ABEngine();
            if (abe != null)
            {
                abe.notifyTreeRemoval(this, (int)reason);
            }

            // tell disturbance modules that a tree died
            GlobalSettings.instance().model().modules().treeDeath(this, (int)reason);

            // update reason, if ABE handled the tree
            if (reason == TreeRemovalType.TreeDisturbance && isHarvested())
            {
                reason = TreeRemovalType.TreeSalavaged;
            }
            if (isCutdown())
            {
                reason = TreeRemovalType.TreeCutDown;
            }
            // create output for tree removals
            if (mRemovalOutput != null && mRemovalOutput.isEnabled())
            {
                mRemovalOutput.execRemovedTree(this, (int)reason);
            }
            if (mLSRemovalOutput != null && mLSRemovalOutput.isEnabled())
            {
                mLSRemovalOutput.execRemovedTree(this, (int)reason);
            }
        }

        public double volume()
        {
            /// @see Species::volumeFactor() for details
            double volume_factor = species().volumeFactor();
            double volume = volume_factor * mDbh * mDbh * mHeight * 0.0001; // dbh in cm: cm/100 * cm/100 = cm*cm * 0.0001 = m2
            return volume;
        }

        /// return the basal area in m2
        public double basalArea()
        {
            // A = r^2 * pi = d/2*pi; from cm.m: d/200
            double b = (mDbh / 200.0) * (mDbh / 200.0) * Math.PI;
            return b;
        }
    }
}
