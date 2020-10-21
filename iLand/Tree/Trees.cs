﻿using iLand.Simulation;
using iLand.Output;
using iLand.Tools;
using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;

namespace iLand.Tree
{
    /** @class Tree
        A tree is the basic simulation entity of iLand and represents a single tree.
        Trees in iLand are designed to be lightweight, thus the list of stored properties is limited. Basic properties
        are dimensions (dbh, height), biomass pools (stem, leaves, roots), the reserve NPP pool. Additionally, the location and species are stored.
        A Tree has a height of at least 4m; trees below this threshold are covered by the regeneration layer (see Sapling).
        Trees are stored in lists managed at the resource unit level.
      */
    public class Trees
    {
        private readonly Grid<float> lightGrid;
        private readonly Grid<HeightCell> heightGrid;

        //public static int StampApplications { get; set; }
        //public static int TreesCreated { get; set; }

        // various flags
        private readonly List<TreeFlags> flags;

        public List<int> Age { get; private set; } // the tree age (years)
        public List<float> Dbh { get; private set; } // diameter at breast height in cm
        public List<float> DbhDelta { get; private set; } // diameter growth [cm]
        public List<float> Height { get; private set; } // tree height in m
        public List<int> ID { get; private set; } // numerical unique ID of the tree
        public List<float> LeafArea { get; private set; } // leaf area (m2) of the tree
        public List<Point> LightCellPosition { get; private set; } // index of the trees position on the basic LIF grid
        public List<float> LightResourceIndex { get; private set; } // LRI of the tree (updated during readStamp())
        public List<float> LightResponse { get; private set; } // light response used for distribution of biomass on RU level
        public List<float> NppReserve { get; private set; } // NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public List<float> Opacity { get; private set; } // multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public ResourceUnit RU { get; private set; } // pointer to the ressource unit the tree belongs to.
        public Species Species { get; set; } // pointer to the tree species of the tree.
        public List<Stamp> Stamp { get; private set; }

        // biomass properties
        public List<float> CoarseRootMass { get; private set; } // mass (kg) of coarse roots
        public List<float> FineRootMass { get; private set; } // mass (kg) of fine roots
        public List<float> FoliageMass { get; private set; } // mass (kg) of foliage
        public List<float> StemMass { get; private set; } // mass (kg) of stem
        public List<float> StressIndex { get; private set; } // the scalar stress rating (0..1), used for mortality

        public Trees(Model model, ResourceUnit resourceUnit)
        {
            this.heightGrid = model.HeightGrid;
            this.lightGrid = model.LightGrid;
            this.flags = new List<TreeFlags>(Constant.Simd128x4.Width);
            
            this.Age = new List<int>(Constant.Simd128x4.Width);
            this.CoarseRootMass = new List<float>(Constant.Simd128x4.Width);
            this.Dbh = new List<float>(Constant.Simd128x4.Width);
            this.DbhDelta = new List<float>(Constant.Simd128x4.Width);
            this.FineRootMass = new List<float>(Constant.Simd128x4.Width);
            this.FoliageMass = new List<float>(Constant.Simd128x4.Width);
            this.Height = new List<float>(Constant.Simd128x4.Width);
            this.ID = new List<int>(Constant.Simd128x4.Width);
            this.LeafArea = new List<float>(Constant.Simd128x4.Width);
            this.LightCellPosition = new List<Point>(Constant.Simd128x4.Width);
            this.LightResourceIndex = new List<float>(Constant.Simd128x4.Width);
            this.LightResponse = new List<float>(Constant.Simd128x4.Width);
            this.NppReserve = new List<float>(Constant.Simd128x4.Width);
            this.Opacity = new List<float>(Constant.Simd128x4.Width);
            this.RU = resourceUnit;
            this.Species = null;
            this.Stamp = new List<Tree.Stamp>(Constant.Simd128x4.Width);
            this.StemMass = new List<float>(Constant.Simd128x4.Width);
            this.StressIndex = new List<float>(Constant.Simd128x4.Width);

            //Tree.TreesCreated++;
        }

        public int Capacity 
        { 
            get { return this.Height.Capacity; }
            set
            {
                if (value % Constant.Simd128x4.Width != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capacity is not an integer multiple of SIMD width.");
                }

                this.flags.Capacity = value;

                this.Age.Capacity = value;
                this.CoarseRootMass.Capacity = value;
                this.Dbh.Capacity = value;
                this.DbhDelta.Capacity = value;
                this.FineRootMass.Capacity = value;
                this.FoliageMass.Capacity = value;
                this.Height.Capacity = value;
                this.ID.Capacity = value;
                this.LeafArea.Capacity = value;
                this.LightCellPosition.Capacity = value;
                this.LightResourceIndex.Capacity = value;
                this.LightResponse.Capacity = value;
                this.NppReserve.Capacity = value;
                this.Opacity.Capacity = value;
                this.Stamp.Capacity = value;
                this.StemMass.Capacity = value;
                this.StressIndex.Capacity = value;
            }
        }
        
        public int Count 
        { 
            get { return this.Height.Count; }
        }

        /// @property position The tree does not store the floating point coordinates but only the index of pixel on the LIF grid
        /// BUGBUG?
        public PointF GetCellCenterPoint(int treeIndex) 
        { 
            Debug.Assert(this.lightGrid != null);
            return this.lightGrid.GetCellCenterPoint(this.LightCellPosition[treeIndex]); 
        }

        public void SetLightCellIndex(int treeIndex, PointF pos) 
        { 
            Debug.Assert(this.lightGrid != null); 
            this.LightCellPosition[treeIndex] = this.lightGrid.IndexAt(pos); 
        }

        // private bool IsDebugging() { return this.flags[treeIndex].HasFlag(TreeFlags.Debugging); }
        public void SetDebugging(int treeIndex, bool enable = true) { this.SetFlag(treeIndex, TreeFlags.Debugging, enable); }

        // death reasons
        public bool IsCutDown(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadCutAndDrop); }
        public bool IsDead(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Dead); } // returns true if the tree is already dead.
        public bool IsDeadBarkBeetle(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadBarkBeetle); }
        public bool IsDeadFire(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFire); }
        public bool IsDeadWind(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadWind); }
        public bool IsHarvested(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Harvested); }

        public void SetDeathReasonBarkBeetle(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadBarkBeetle, true); }
        public void SetDeathReasonCutAndDrop(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadCutAndDrop, true); }
        public void SetDeathReasonFire(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadFire, true); }
        public void SetDeathReasonHarvested(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.Harvested, true); }
        public void SetDeathReasonWind(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadWind, true); }

        // management flags (used by ABE management system)
        public bool IsMarkedForHarvest(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.MarkedForHarvest); }
        public bool IsMarkedForCut(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.MarkedForCut); }
        public bool IsMarkedAsCropTree(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.CropTree); }
        public bool IsMarkedAsCropCompetitor(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.CropCompetitor); }

        public void SetMarkForHarvest(int treeIndex, bool mark) { this.SetFlag(treeIndex, TreeFlags.MarkedForHarvest, mark); }
        public void SetMarkForCut(int treeIndex, bool mark) { this.SetFlag(treeIndex, TreeFlags.MarkedForCut, mark); }
        public void SetMarkAsCropTree(int treeIndex, bool mark) { this.SetFlag(treeIndex, TreeFlags.CropTree, mark); }
        public void SetMarkAsCropCompetitor(int treeIndex, bool mark) { this.SetFlag(treeIndex, TreeFlags.CropCompetitor, mark); }

        /// set a Flag 'flag' to the value 'value'.
        private void SetFlag(int treeIndex, TreeFlags flag, bool value)
        {
            if (value)
            {
                this.flags[treeIndex] |= flag;
            }
            else
            {
                this.flags[treeIndex] &= (TreeFlags)((int)flag ^ 0xffffff);
            }
        }

        public void Add()
        {
            this.flags.Add(0);

            this.Age.Add(0);
            this.CoarseRootMass.Add(0.0F);
            this.Dbh.Add(0.0F);
            this.DbhDelta.Add(0.0F);
            this.FineRootMass.Add(0.0F);
            this.FoliageMass.Add(0.0F);
            this.Height.Add(0.0F);
            this.ID.Add(0);
            this.LeafArea.Add(0.0F);
            this.LightCellPosition.Add(Point.Empty);
            this.LightResourceIndex.Add(0.0F);
            this.LightResponse.Add(0.0F);
            this.NppReserve.Add(0.0F);
            this.Opacity.Add(0.0F);
            this.Stamp.Add(null);
            this.StemMass.Add(0.0F);
            this.StressIndex.Add(0.0F);
        }

        public void Add(Trees other, int otherTreeIndex)
        {
            this.flags.Add(other.flags[otherTreeIndex]);

            this.Age.Add(other.Age[otherTreeIndex]);
            this.CoarseRootMass.Add(other.CoarseRootMass[otherTreeIndex]);
            this.Dbh.Add(other.Dbh[otherTreeIndex]);
            this.DbhDelta.Add(other.DbhDelta[otherTreeIndex]);
            this.FineRootMass.Add(other.FineRootMass[otherTreeIndex]);
            this.FoliageMass.Add(other.FoliageMass[otherTreeIndex]);
            this.Height.Add(other.Height[otherTreeIndex]);
            this.ID.Add(other.ID[otherTreeIndex]);
            this.LeafArea.Add(other.LeafArea[otherTreeIndex]);
            this.LightCellPosition.Add(other.LightCellPosition[otherTreeIndex]);
            this.LightResourceIndex.Add(other.LightResourceIndex[otherTreeIndex]);
            this.LightResponse.Add(other.LightResponse[otherTreeIndex]);
            this.NppReserve.Add(other.NppReserve[otherTreeIndex]);
            this.Opacity.Add(other.Opacity[otherTreeIndex]);
            this.Stamp.Add(other.Stamp[otherTreeIndex]);
            this.StemMass.Add(other.StemMass[otherTreeIndex]);
            this.StressIndex.Add(other.StressIndex[otherTreeIndex]);
        }

        public void Copy(int sourceIndex, int destinationIndex)
        {
            this.flags[destinationIndex] = this.flags[sourceIndex];

            this.Age[destinationIndex] = this.Age[sourceIndex];
            this.CoarseRootMass[destinationIndex] = this.CoarseRootMass[sourceIndex];
            this.Dbh[destinationIndex] = this.Dbh[sourceIndex];
            this.DbhDelta[destinationIndex] = this.DbhDelta[sourceIndex];
            this.FineRootMass[destinationIndex] = this.FineRootMass[sourceIndex];
            this.FoliageMass[destinationIndex] = this.FoliageMass[sourceIndex];
            this.Height[destinationIndex] = this.Height[sourceIndex];
            this.ID[destinationIndex] = this.ID[sourceIndex];
            this.LeafArea[destinationIndex] = this.LeafArea[sourceIndex];
            this.LightCellPosition[destinationIndex] = this.LightCellPosition[sourceIndex];
            this.LightResourceIndex[destinationIndex] = this.LightResourceIndex[sourceIndex];
            this.LightResponse[destinationIndex] = this.LightResponse[sourceIndex];
            this.NppReserve[destinationIndex] = this.NppReserve[sourceIndex];
            this.Opacity[destinationIndex] = this.Opacity[sourceIndex];
            this.Stamp[destinationIndex] = this.Stamp[sourceIndex];
            this.StemMass[destinationIndex] = this.StemMass[sourceIndex];
            this.StressIndex[destinationIndex] = this.StressIndex[sourceIndex];
        }

        public void ApplyLightIntensityPattern(int treeIndex)
        {
            if (Stamp == null)
            {
                return;
            }
            Debug.Assert(this.lightGrid != null && Stamp != null && RU != null);
            Point pos = this.LightCellPosition[treeIndex];
            int offset = this.Stamp[treeIndex].CenterCellPosition;
            pos.X -= offset;
            pos.Y -= offset;

            float local_dom; // height of Z* on the current position
            int x, y;
            float value, z, z_zstar;
            int gr_stamp = this.Stamp[treeIndex].Size();

            if (!this.lightGrid.Contains(pos) || !this.lightGrid.Contains(new Point(pos.X + gr_stamp, pos.Y + gr_stamp)))
            {
                // this should not happen because of the buffer
                return;
            }
            int grid_y = pos.Y;
            for (y = 0; y < gr_stamp; ++y)
            {

                float grid_value_ptr = this.lightGrid[pos.X, grid_y];
                int grid_x = pos.X;
                for (x = 0; x < gr_stamp; ++x, ++grid_x, ++grid_value_ptr)
                {
                    // suppose there is no stamping outside
                    value = this.Stamp[treeIndex][x, y]; // stampvalue
                    //if (value>0.f) {
                    local_dom = this.heightGrid[grid_x / Constant.LightPerHeightSize, grid_y / Constant.LightPerHeightSize].Height;
                    z = MathF.Max(this.Height[treeIndex] - this.Stamp[treeIndex].GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    value = 1.0F - value * this.Opacity[treeIndex] * z_zstar; // calculated value
                    value = MathF.Max(value, 0.02F); // limit value

                    grid_value_ptr *= value;
                }
                grid_y++;
            }

            //++Tree.StampApplications;
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
        public void ApplyLightIntensityPatternTorus(int treeIndex)
        {
            if (this.Stamp == null)
            {
                return;
            }
            Debug.Assert(this.lightGrid != null && this.Stamp != null && this.RU != null);
            int bufferOffset = this.lightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer
            Point pos = new Point((this.LightCellPosition[treeIndex].X - bufferOffset) % Constant.LightPerRUsize + bufferOffset,
                                  (this.LightCellPosition[treeIndex].Y - bufferOffset) % Constant.LightPerRUsize + bufferOffset); // offset within the ha
            Point ruOffset = new Point(this.LightCellPosition[treeIndex].X - pos.X, this.LightCellPosition[treeIndex].Y - pos.Y); // offset of the corner of the resource index

            int offset = this.Stamp[treeIndex].CenterCellPosition;
            pos.X -= offset;
            pos.Y -= offset;

            int stampSize = this.Stamp[treeIndex].Size();
            if (!this.lightGrid.Contains(pos) || !this.lightGrid.Contains(new Point(stampSize + pos.X, stampSize + pos.Y)))
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

                    local_dom = this.heightGrid[xt / Constant.LightPerHeightSize, yt / Constant.LightPerHeightSize].Height;

                    float z = MathF.Max(this.Height[treeIndex] - this.Stamp[treeIndex].GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;
                    float value = this.Stamp[treeIndex][x, y]; // stampvalue
                    value = 1.0F - value * this.Opacity[treeIndex] * z_zstar; // calculated value
                                                               // old: value = 1. - value*mOpacity / local_dom; // calculated value
                    value = MathF.Max(value, 0.02f); // limit value

                    this.lightGrid[xt, yt] *= value; // use wraparound coordinates
                }
            }

            //++Tree.StampApplications;
        }

        /** heightGrid()
          This function calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
            */
        public void CalculateDominantHeightField(int treeIndex)
        {
            Point heightCellPosition = new Point(this.LightCellPosition[treeIndex].X / Constant.LightPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightPerHeightSize); // pos of tree on height grid

            // count trees that are on height-grid cells (used for stockable area)
            this.heightGrid[heightCellPosition].AddTree(this.Height[treeIndex]);

            int r = this.Stamp[treeIndex].Reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int index_eastwest = this.LightCellPosition[treeIndex].X % Constant.LightPerHeightSize; // 4: very west, 0 east edge
            int index_northsouth = this.LightCellPosition[treeIndex].Y % Constant.LightPerHeightSize; // 4: northern edge, 0: southern edge
            if (index_eastwest - r < 0)
            { // east
                this.heightGrid[heightCellPosition.X - 1, heightCellPosition.Y].Height = MathF.Max(this.heightGrid[heightCellPosition.X - 1, heightCellPosition.Y].Height, this.Height[treeIndex]);
            }
            if (index_eastwest + r >= Constant.LightPerHeightSize)
            {  // west
                this.heightGrid[heightCellPosition.X + 1, heightCellPosition.Y].Height = MathF.Max(this.heightGrid[heightCellPosition.X + 1, heightCellPosition.Y].Height, this.Height[treeIndex]);
            }
            if (index_northsouth - r < 0)
            {  // south
                this.heightGrid[heightCellPosition.X, heightCellPosition.Y - 1].Height = MathF.Max(this.heightGrid[heightCellPosition.X, heightCellPosition.Y - 1].Height, this.Height[treeIndex]);
            }
            if (index_northsouth + r >= Constant.LightPerHeightSize)
            {  // north
                this.heightGrid[heightCellPosition.X, heightCellPosition.Y + 1].Height = MathF.Max(this.heightGrid[heightCellPosition.X, heightCellPosition.Y + 1].Height, this.Height[treeIndex]);
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
            //    dist[treeIndex] = MathF.Max(dist[3], dist[1]); // south-west
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

        public void CalculateDominantHeightFieldTorus(int treeIndex)
        {
            // height of Z*
            Point heightCellPosition = new Point(this.LightCellPosition[treeIndex].X / Constant.LightPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightPerHeightSize); // pos of tree on height grid
            int bufferOffset = this.heightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer (i.e.: size of buffer in height-pixels)
            heightCellPosition.X = (heightCellPosition.X - bufferOffset) % Constant.HeightSizePerRU + bufferOffset; // 10: 10 x 10m pixeln in 100m
            heightCellPosition.Y = (heightCellPosition.Y - bufferOffset) % Constant.HeightSizePerRU + bufferOffset;

            // torus coordinates: ru_offset = coords of lower left corner of 1ha patch
            Point ru_offset = new Point(this.LightCellPosition[treeIndex].X / Constant.LightPerHeightSize - heightCellPosition.X, this.LightCellPosition[treeIndex].Y / Constant.LightPerHeightSize - heightCellPosition.Y);

            // count trees that are on height-grid cells (used for stockable area)
            int torusX = TorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X);
            int torusY = TorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y);
            HeightCell heightCell = this.heightGrid[torusX, torusY];
            heightCell.AddTree(this.Height[treeIndex]);

            int readerCenter = this.Stamp[treeIndex].Reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int indexEastWest = this.LightCellPosition[treeIndex].X % Constant.LightPerHeightSize; // 4: very west, 0 east edge
            int indexNorthSouth = this.LightCellPosition[treeIndex].Y % Constant.LightPerHeightSize; // 4: northern edge, 0: southern edge
            if (indexEastWest - readerCenter < 0)
            { // east
                heightCell = this.heightGrid[TorusIndex(heightCellPosition.X - 1, 10, bufferOffset, ru_offset.X), TorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexEastWest + readerCenter >= Constant.LightPerHeightSize)
            {  // west
                heightCell = this.heightGrid[TorusIndex(heightCellPosition.X + 1, 10, bufferOffset, ru_offset.X), TorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth - readerCenter < 0)
            {  // south
                heightCell = this.heightGrid[TorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X), TorusIndex(heightCellPosition.Y - 1, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth + readerCenter >= Constant.LightPerHeightSize)
            {  // north
                heightCell = this.heightGrid[TorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X), TorusIndex(heightCellPosition.Y + 1, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
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
            //    dist[treeIndex] = MathF.Max(dist[3], dist[1]); // south-west
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

        /** This function is called if a tree dies.
            @sa ResourceUnit::cleanTreeList(), remove() */
        public void Die(Model model, int treeIndex, TreeGrowthData growthData = null)
        {
            this.SetFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.RU.TreeDied();
            ResourceUnitSpecies rus = RU.GetSpecies(Species);
            rus.StatisticsDead.Add(this, treeIndex, growthData); // add tree to statistics
            this.NotifyTreeRemoved(model, treeIndex, MortalityCause.Stress);
            if (this.RU.Snags != null)
            {
                this.RU.Snags.AddMortality(this, treeIndex);
            }
        }

        /// dumps some core variables of a tree to a string.
        private string Dump(int treeIndex)
        {
            string result = String.Format("id {0} species {1} dbh {2} h {3} x/y {4}/{5} ru# {6} LRI {7}",
                                          this.ID, this.Species.ID, this.Dbh, this.Height,
                                          this.GetCellCenterPoint(treeIndex).X, this.GetCellCenterPoint(treeIndex).Y,
                                          this.RU.Index, this.LightResourceIndex);
            return result;
        }

        //private void DumpList(List<object> rTargetList)
        //{
        //    rTargetList.AddRange(new object[] { ID, Species.ID, Dbh, Height, GetCellCenterPoint().X, GetCellCenterPoint().Y, RU.Index, LightResourceIndex,
        //                                        StemMass, CoarseRootMass + FoliageMass + LeafArea } );
        //}

        public float GetCrownRadius(int treeIndex)
        {
            Debug.Assert(this.Stamp != null);
            return this.Stamp[treeIndex].CrownRadius;
        }

        public float GetBranchBiomass(int treeIndex)
        {
            return (float)this.Species.GetBiomassBranch(this.Dbh[treeIndex]);
        }

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland.boku.ac.at/competition+for+light for details.
          */
        public void ReadLightInfluenceField(Model model, int treeIndex)
        {
            if (Stamp == null)
            {
                return;
            }
            Stamp reader = this.Stamp[treeIndex].Reader;
            if (reader == null)
            {
                return;
            }
            Point lightCellPosition = this.LightCellPosition[treeIndex];
            float outsideAreaFactor = 0.1F;

            int readerOffset = reader.CenterCellPosition;
            int writerOffset = this.Stamp[treeIndex].CenterCellPosition;
            int writerReaderOffset = writerOffset - readerOffset; // offset on the *stamp* to the crown-cells

            lightCellPosition.X -= readerOffset;
            lightCellPosition.Y -= readerOffset;

            double sum = 0.0;
            int readerSize = reader.Size();
            int rx = lightCellPosition.X;
            int ry = lightCellPosition.Y;
            for (int y = 0; y < readerSize; ++y, ++ry)
            {
                float lightValue = this.lightGrid[rx, ry];
                for (int x = 0; x < readerSize; ++x)
                {
                    HeightCell hgv = this.heightGrid[(rx + x) / Constant.LightPerHeightSize, ry / Constant.LightPerHeightSize]; // the height grid value, ry: gets ++ed in outer loop, rx not
                    float local_dom = hgv.Height;
                    float z = MathF.Max(this.Height[treeIndex] - reader.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    float treeValue = 1.0F - this.Stamp[treeIndex][x, y, writerReaderOffset] * this.Opacity[treeIndex] * z_zstar;
                    treeValue = MathF.Max(treeValue, 0.02F);
                    float value = lightValue / treeValue; // remove impact of focal tree
                    // additional punishment if pixel is outside
                    if (hgv.IsOutsideWorld())
                    {
                        value *= outsideAreaFactor;
                    }
                    //Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                    //if (value>0.)
                    sum += value * reader[x, y];
                }
            }
            this.LightResourceIndex[treeIndex] = (float)sum;
            // LRI correction...
            float hrel = this.Height[treeIndex] / this.heightGrid[this.LightCellPosition[treeIndex].X / Constant.LightPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightPerHeightSize].Height;
            if (hrel < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(model, this.LightResourceIndex[treeIndex], hrel);
            }

            if (this.LightResourceIndex[treeIndex] > 1.0F)
            {
                this.LightResourceIndex[treeIndex] = 1.0F;
            }
            // Finally, add LRI of this Tree to the ResourceUnit!
            RU.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);

            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightInfluenceFieldTorus(Model model, int treeIndex)
        {
            if (this.Stamp == null)
            {
                return;
            }
            Stamp reader = this.Stamp[treeIndex].Reader;
            if (reader == null)
            {
                return;
            }
            int bufferOffset = this.lightGrid.IndexAt(new PointF(0.0F, 0.0F)).X; // offset of buffer

            Point treePositionInRU = new Point((this.LightCellPosition[treeIndex].X - bufferOffset) % Constant.LightPerRUsize + bufferOffset,
                                               (this.LightCellPosition[treeIndex].Y - bufferOffset) % Constant.LightPerRUsize + bufferOffset); // offset within the ha
            Point ru_offset = new Point(this.LightCellPosition[treeIndex].X - treePositionInRU.X, this.LightCellPosition[treeIndex].Y - treePositionInRU.Y); // offset of the corner of the resource index

            float lightIndex = 0.0F;
            int readerSize = reader.Size();
            int readerOriginX = treePositionInRU.X - reader.CenterCellPosition;
            int readerOriginY = treePositionInRU.Y - reader.CenterCellPosition;
            int writerReaderOffset = this.Stamp[treeIndex].CenterCellPosition - reader.CenterCellPosition; // offset on the *stamp* to the crown (light?) cells
            for (int readerY = 0; readerY < readerSize; ++readerY)
            {
                int yTorus = TorusIndex(readerOriginY + readerY, Constant.LightPerRUsize, bufferOffset, ru_offset.Y);
                for (int readerX = 0; readerX < readerSize; ++readerX)
                {
                    // see http://iland.boku.ac.at/competition+for+light 
                    int xTorus = TorusIndex(readerOriginX + readerX, Constant.LightPerRUsize, bufferOffset, ru_offset.X);
                    float dominantHeightTorus = this.heightGrid[xTorus / Constant.LightPerHeightSize, yTorus / Constant.LightPerHeightSize].Height; // ry: gets ++ed in outer loop, rx not
                    float influenceZ = MathF.Max(this.Height[treeIndex] - reader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                    float influenceZstar = (influenceZ >= dominantHeightTorus) ? 1.0F : influenceZ / dominantHeightTorus;

                    // TODO: why a nonzero floor as opposed to skipping division?
                    float focalIntensity = MathF.Max(1.0F - this.Stamp[treeIndex][readerX, readerY, writerReaderOffset] * this.Opacity[treeIndex] * influenceZstar, 0.02F);
                    // C++ code is actually Tree.LightGrid[Tree.LightGrid.IndexOf(xTorus, yTorus) + 1], which appears to be an off by
                    // one error corrected by Qt build implementing precdence in *ptr++ incorrectly.
                    float cellIntensity = this.lightGrid[xTorus, yTorus];
                    float cellIndex = cellIntensity / focalIntensity; // remove impact of focal tree

                    // debug for one tree in HJA
                    //if (id()==178020)
                    //    Debug.WriteLine(x + y + xt + yt + *grid_value + local_dom + own_value + value + (*reader)(x,y);
                    //if (_isnan(value))
                    //    Debug.WriteLine("isnan" + id();
                    if (cellIndex * reader[readerX, readerY] > 1.0)
                    {
                        Debug.WriteLine("LIFTorus: value > 1.0.");
                    }
                    lightIndex += cellIndex * reader[readerX, readerY];
                    //} // isIndexValid
                }
            }
            this.LightResourceIndex[treeIndex] = lightIndex;

            // LRI correction...
            float hrel = this.Height[treeIndex] / this.heightGrid[this.LightCellPosition[treeIndex].X / Constant.LightPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightPerHeightSize].Height;
            if (hrel < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = (float)Species.SpeciesSet.GetLriCorrection(model, this.LightResourceIndex[treeIndex], hrel);
            }

            if (Double.IsNaN(this.LightResourceIndex[treeIndex]))
            {
                throw new InvalidOperationException("Light resource index unexpectedly NaN.");
                //Debug.WriteLine("LRI invalid (nan) " + ID);
                //this.LightResourceIndex[treeIndex] = 0.0F;
                //Debug.WriteLine(reader.dump();
            }

            Debug.Assert(this.LightResourceIndex[treeIndex] >= 0.0F && this.LightResourceIndex[treeIndex] < 50.0F); // sanity upper bound
            if (this.LightResourceIndex[treeIndex] > 1.0F)
            {
                this.LightResourceIndex[treeIndex] = 1.0F; // TODO: why clamp?
            }
            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;

            // Finally, add LRI of this Tree to the ResourceUnit!
            this.RU.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);
        }

        public void RemoveRange(int index, int count)
        {
            this.flags.RemoveRange(index, count);

            this.Age.RemoveRange(index, count);
            this.CoarseRootMass.RemoveRange(index, count);
            this.Dbh.RemoveRange(index, count);
            this.DbhDelta.RemoveRange(index, count);
            this.FineRootMass.RemoveRange(index, count);
            this.FoliageMass.RemoveRange(index, count);
            this.Height.RemoveRange(index, count);
            this.ID.RemoveRange(index, count);
            this.LeafArea.RemoveRange(index, count);
            this.LightCellPosition.RemoveRange(index, count);
            this.LightResourceIndex.RemoveRange(index, count);
            this.LightResponse.RemoveRange(index, count);
            this.NppReserve.RemoveRange(index, count);
            this.Opacity.RemoveRange(index, count);
            this.Stamp.RemoveRange(index, count);
            this.StemMass.RemoveRange(index, count);
            this.StressIndex.RemoveRange(index, count);
        }

        //public static void ResetStatistics()
        //{
        //    Tree.StampApplications = 0;
        //    Tree.TreesCreated = 0;
        //}

        //#ifdef ALT_TREE_MORTALITY
        //void mortalityParams(double dbh_inc_threshold, int stress_years, double stress_mort_prob)
        //{
        //    _stress_threshold = dbh_inc_threshold;
        //    _stress_years = stress_years;
        //    _stress_death_prob = stress_mort_prob;
        //    Debug.WriteLine("Alternative Mortality enabled: threshold" + dbh_inc_threshold + ", years:" + _stress_years + ", level:" + _stress_death_prob;
        //}
        //#endif

        public void CalcLightResponse(Model model, int treeIndex)
        {
            // calculate a light response from lri:
            // http://iland.boku.ac.at/individual+tree+light+availability
            float lri = Global.Limit(this.LightResourceIndex[treeIndex] * this.RU.LriModifier, 0.0F, 1.0F); // Eq. (3)
            this.LightResponse[treeIndex] = this.Species.GetLightResponse(model, lri); // Eq. (4)
            RU.AddLightResponse(this.LeafArea[treeIndex], this.LightResponse[treeIndex]);
        }

        /// return the basal area in m2
        public float GetBasalArea(int treeIndex)
        {
            float basalArea = MathF.PI * 0.0001F / 4.0F * this.Dbh[treeIndex] * this.Dbh[treeIndex];
            return basalArea;
        }

        public float GetStemVolume(int treeIndex)
        {
            /// @see Species::volumeFactor() for details
            float taperCoefficient = this.Species.VolumeFactor;
            float volume = taperCoefficient * 0.0001F * this.Dbh[treeIndex] * this.Dbh[treeIndex] * this.Height[treeIndex]; // dbh in cm: cm/100 * cm/100 = cm*cm * 0.0001 = m2
            return volume;
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
        public void Grow(Model model, int treeIndex)
        {
            this.Age[treeIndex]++; // increase age
            // step 1: get "interception area" of the tree individual [m2]
            // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
            double effectiveRUarea = RU.InterceptedArea(this.LeafArea[treeIndex], this.LightResponse[treeIndex]); // light response in [0...1] depending on suppression

            // step 2: calculate GPP of the tree based
            // (1) get the amount of GPP for a "unit area" of the tree species
            double raw_gpp_per_area = RU.GetSpecies(Species).BiomassGrowth.AnnualGpp;
            // (2) GPP (without aging-effect) in kg Biomass / year
            double raw_gpp = raw_gpp_per_area * effectiveRUarea;

            // apply aging according to the state of the individal
            float agingFactor = this.Species.Aging(model, this.Height[treeIndex], this.Age[treeIndex]);
            RU.AddTreeAging(this.LeafArea[treeIndex], agingFactor);
            double gpp = raw_gpp * agingFactor; //
            TreeGrowthData growthData = new TreeGrowthData()
            {
                NppTotal = gpp * Constant.AutotrophicRespiration // respiration loss (0.47), cf. Waring et al 1998.
            };

            //DBGMODE(
            //if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.TreeNpp) && IsDebugging())
            //{
            //    List<object> outList = model.GlobalSettings.DebugList(ID, DebugOutputs.TreeNpp);
            //    DumpList(outList); // add tree headers
            //    outList.AddRange(new object[] { this.LightResourceIndex[treeIndex] * RU.LriModifier, LightResponse, effective_area, raw_gpp, gpp, d.NppTotal, agingFactor });
            //}
            //); // DBGMODE()
            if (model.ModelSettings.GrowthEnabled)
            {
                if (growthData.NppTotal > 0.0)
                {
                    this.PartitionBiomass(growthData, model, treeIndex); // split npp to compartments and grow (diameter, height)
                }
            }

            // mortality
            //#ifdef ALT_TREE_MORTALITY
            //    // alternative variant of tree mortality (note: mStrssIndex used otherwise)
            //    altMortality(d);

            //#else
            if (model.ModelSettings.MortalityEnabled)
            {
                this.Mortality(model, treeIndex, growthData);
            }
            this.StressIndex[treeIndex] = (float)growthData.StressIndex;
            //#endif

            if (!this.IsDead(treeIndex))
            {
                RU.GetSpecies(Species).Statistics.Add(this, treeIndex, growthData);
            }

            // regeneration
            Species.SeedProduction(model, this, treeIndex);
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on Duursma, 2007.
          @sa http://iland.boku.ac.at/allocation */
        private void PartitionBiomass(TreeGrowthData growthData, Model model, int treeIndex)
        {
            float npp = (float)growthData.NppTotal;
            // add content of reserve pool
            npp += this.NppReserve[treeIndex];
            float foliage_mass_allo = Species.GetBiomassFoliage(this.Dbh[treeIndex]);
            float reserve_size = foliage_mass_allo * (1.0F + this.Species.FinerootFoliageRatio);
            float refill_reserve = MathF.Min(reserve_size, (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMass[treeIndex]); // not always try to refill reserve 100%

            ResourceUnitSpecies rus = RU.GetSpecies(Species);

            float apct_root = rus.BiomassGrowth.RootFraction;
            growthData.NppAboveground = growthData.NppTotal * (1.0F - apct_root); // aboveground: total NPP - fraction to roots
            float b_wf = Species.GetWoodFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

            // turnover rates
            float foliageTurnover = this.Species.TurnoverLeaf;
            float rootTurnover = this.Species.TurnoverRoot;
            // the turnover rate of wood depends on the size of the reserve pool:
            float woodTurnover = refill_reserve / (this.StemMass[treeIndex] + refill_reserve);

            // Duursma 2007, Eq. (20) allocation percentages (sum=1) (eta)
            float apct_wood = (foliage_mass_allo * woodTurnover / npp + b_wf * (1.0F - apct_root) - b_wf * foliage_mass_allo * foliageTurnover / npp) / (foliage_mass_allo / this.StemMass[treeIndex] + b_wf);
            apct_wood = Global.Limit(apct_wood, 0.0F, 1.0F - apct_root);
            float apct_foliage = 1.0F - apct_root - apct_wood;

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
            float sen_root = this.FineRootMass[treeIndex] * rootTurnover;
            float sen_foliage = this.FoliageMass[treeIndex] * foliageTurnover;
            if (RU.Snags != null)
            {
                RU.Snags.AddTurnoverLitter(this.Species, sen_foliage, sen_root);
            }

            // Roots
            // http://iland.boku.ac.at/allocation#belowground_NPP
            this.FineRootMass[treeIndex] -= sen_root; // reduce only fine root pool
            float delta_root = apct_root * npp;
            // 1st, refill the fine root pool
            float fineroot_miss = this.FoliageMass[treeIndex] * this.Species.FinerootFoliageRatio - this.FineRootMass[treeIndex];
            if (fineroot_miss > 0.0F)
            {
                float delta_fineroot = MathF.Min(fineroot_miss, delta_root);
                this.FineRootMass[treeIndex] += delta_fineroot;
                delta_root -= delta_fineroot;
            }
            // 2nd, the rest of NPP allocated to roots go to coarse roots
            float max_coarse_root = this.Species.GetBiomassRoot(this.Dbh[treeIndex]);
            this.CoarseRootMass[treeIndex] += delta_root;
            if (this.CoarseRootMass[treeIndex] > max_coarse_root)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (RU.Snags != null)
                {
                    RU.Snags.AddTurnoverWood(Species, this.CoarseRootMass[treeIndex] - max_coarse_root);
                }
                this.CoarseRootMass[treeIndex] = max_coarse_root;
            }

            // Foliage
            float delta_foliage = apct_foliage * npp - sen_foliage;
            this.FoliageMass[treeIndex] += (float)delta_foliage;
            if (Single.IsNaN(this.FoliageMass[treeIndex]))
            {
                Debug.WriteLine("foliage mass invalid!");
            }
            if (this.FoliageMass[treeIndex] < 0.0F)
            {
                this.FoliageMass[treeIndex] = 0.0F; // limit to zero
            }

            this.LeafArea[treeIndex] = this.FoliageMass[treeIndex] * this.Species.SpecificLeafArea; // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            growthData.StressIndex = MathF.Max((float)(1.0 - (npp) / (foliageTurnover * foliage_mass_allo + rootTurnover * foliage_mass_allo * Species.FinerootFoliageRatio + reserve_size)), 0.0F);

            // Woody compartments
            // see also: http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            float gross_woody = apct_wood * npp;
            float to_reserve = MathF.Min(reserve_size, gross_woody);
            this.NppReserve[treeIndex] = to_reserve;
            float net_woody = gross_woody - to_reserve;
            this.DbhDelta[treeIndex] = 0.0F;

            if (net_woody > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                float net_stem = net_woody * this.Species.AllometricFractionStem(this.Dbh[treeIndex]);
                growthData.NppStem = net_stem;
                this.StemMass[treeIndex] += net_woody;
                //  (3) growth of diameter and height baseed on net stem increment
                this.GrowHeightAndDiameter(growthData, model, treeIndex);
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
        private void GrowHeightAndDiameter(TreeGrowthData growthData, Model model, int treeIndex)
        {
            // determine dh-ratio of increment
            // height increment is a function of light competition:
            double hd_growth = this.RelativeHeightGrowth(model, treeIndex); // hd of height growth
            double d_m = this.Dbh[treeIndex] / 100.0; // current diameter in [m]
            double net_stem_npp = growthData.NppStem;

            double d_delta_m = this.DbhDelta[treeIndex] / 100.0; // increment of last year in [m]

            double mass_factor = Species.VolumeFactor * Species.WoodDensity;
            double stem_mass = mass_factor * d_m * d_m * this.Height[treeIndex]; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            double factor_diameter = 1.0 / (mass_factor * (d_m + d_delta_m) * (d_m + d_delta_m) * (2.0 * this.Height[treeIndex] / d_m + hd_growth));
            double delta_d_estimate = factor_diameter * net_stem_npp; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            double stem_estimate = mass_factor * (d_m + delta_d_estimate) * (d_m + delta_d_estimate) * (this.Height[treeIndex] + delta_d_estimate * hd_growth);
            double stem_residual = stem_estimate - (stem_mass + net_stem_npp);

            // the final increment is then:
            double d_increment = factor_diameter * (net_stem_npp - stem_residual); // Eq. (11)
            if (Math.Abs(stem_residual) > Math.Min(1.0, stem_mass))
            {
                // calculate final residual in stem
                double res_final = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (this.Height[treeIndex] + d_increment * hd_growth) - ((stem_mass + net_stem_npp));
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
                        est_stem = mass_factor * (d_m + d_increment) * (d_m + d_increment) * (this.Height[treeIndex] + d_increment * hd_growth); // estimate with current increment
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
            Debug.WriteLineIf(d_increment < 0.0 || d_increment > 0.1, this.Dump(treeIndex) +
                       String.Format("hdz {0} factor_diameter {1} stem_residual {2} delta_d_estimate {3} d_increment {4} final residual(kg) {5}",
                       hd_growth, factor_diameter, stem_residual, delta_d_estimate, d_increment, mass_factor * (this.Dbh[treeIndex] + d_increment) * (this.Dbh[treeIndex] + d_increment) * (this.Height[treeIndex] + d_increment * hd_growth) - ((stem_mass + net_stem_npp))),
                       "grow_diameter increment out of range.");

            //DBGMODE(
            // do not calculate res_final twice if already done
            //Debug.WriteLineIf((res_final == 0.0 ? Math.Abs(mass_factor * (d_m + d_increment) * (d_m + d_increment) * (this.height[treeIndex] + d_increment * hd_growth) - ((stem_mass + net_stem_npp))) : res_final) > 1, Dump(),
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
            this.Dbh[treeIndex] += (float)d_increment * 100.0F; // convert from [m] to [cm]
            this.DbhDelta[treeIndex] = (float)(d_increment * 100.0); // save for next year's growth
            this.Height[treeIndex] += (float)(d_increment * hd_growth);

            // update state of LIP stamp and opacity
            this.Stamp[treeIndex] = Species.GetStamp(this.Dbh[treeIndex], this.Height[treeIndex]); // get new stamp for updated dimensions
            // calculate the CrownFactor which reflects the opacity of the crown
            float k = (float)model.ModelSettings.LightExtinctionCoefficientOpacity;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-k * this.LeafArea[treeIndex] / this.Stamp[treeIndex].CrownArea);
        }

        /// return the HD ratio of this year's increment based on the light status.
        private double RelativeHeightGrowth(Model model, int treeIndex)
        {
            Species.GetHeightDiameterRatioLimits(model, this.Dbh[treeIndex], out double hd_low, out double hd_high);

            Debug.WriteLineIf(hd_low > hd_high, this.Dump(treeIndex), "relative_height_growth: hd low higher than hd_high");
            Debug.WriteLineIf(hd_low < 10 || hd_high > 250, this.Dump(treeIndex) + String.Format(" hd-low: {0} hd-high: {1}", hd_low, hd_high), "relative_height_growth: hd out of range ");

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            double lri = Global.Limit(this.LightResourceIndex[treeIndex] * RU.LriModifier, 0.0, 1.0);
            double hd_ratio = hd_high - (hd_high - hd_low) * lri;
            return hd_ratio;
        }


        public void SetAge(int treeIndex, int age, float height)
        {
            this.Age[treeIndex] = age;
            if (age == 0)
            {
                // estimate age using the tree height
                this.Age[treeIndex] = Species.EstimateAge(height);
            }
        }

        public void Setup(Model model, int treeIndex)
        {
            if (this.Dbh[treeIndex] <= 0.0F || this.Height[treeIndex] <= 0.0F)
            {
                throw new NotSupportedException(String.Format("Invalid dimensions: dbh: {0} height: {1} id: {2} RU-index: {3}", Dbh, Height, ID, RU.Index));
            }
            // check stamp
            Debug.Assert(this.Species != null, "setup()", "species is NULL");
            this.Stamp[treeIndex] = this.Species.GetStamp(this.Dbh[treeIndex], this.Height[treeIndex]);
            if (this.Stamp[treeIndex] == null)
            {
                throw new NotSupportedException("Stamp is null.");
            }

            this.FoliageMass[treeIndex] = this.Species.GetBiomassFoliage(this.Dbh[treeIndex]);
            this.CoarseRootMass[treeIndex] = this.Species.GetBiomassRoot(this.Dbh[treeIndex]); // coarse root (allometry)
            this.FineRootMass[treeIndex] = this.FoliageMass[treeIndex] * this.Species.FinerootFoliageRatio; //  fine root (size defined  by finerootFoliageRatio)
            this.StemMass[treeIndex] = this.Species.GetBiomassWoody(this.Dbh[treeIndex]);

            // LeafArea[m2] = LeafMass[kg] * specificLeafArea[m2/kg]
            this.LeafArea[treeIndex] = this.FoliageMass[treeIndex] * this.Species.SpecificLeafArea;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-model.ModelSettings.LightExtinctionCoefficientOpacity * this.LeafArea[treeIndex] / this.Stamp[treeIndex].CrownArea);
            this.NppReserve[treeIndex] = (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMass[treeIndex]; // initial value
            this.DbhDelta[treeIndex] = 0.1F; // initial value: used in growth() to estimate diameter increment
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void Remove(Model model, int treeIndex, float removeFoliage = 0.0F, float removeBranch = 0.0F, float removeStem = 0.0F)
        {
            this.SetFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.SetDeathReasonHarvested(treeIndex);
            this.RU.TreeDied();
            ResourceUnitSpecies ruSpecies = this.RU.GetSpecies(Species);
            ruSpecies.StatisticsMgmt.Add(this, treeIndex, null);
            this.NotifyTreeRemoved(model, treeIndex, this.IsCutDown(treeIndex) ?  MortalityCause.CutDown : MortalityCause.Harvest);

            if (model.Saplings != null)
            {
                model.Saplings.AddSprout(model, this, treeIndex);
            }
            if (this.RU.Snags != null)
            {
                this.RU.Snags.AddHarvest(this, treeIndex, removeStem, removeBranch, removeFoliage);
            }
        }

        /// remove the tree due to an special event (disturbance)
        /// this is +- the same as die().
        // TODO: when would branch to snag fraction be greater than zero?
        public void RemoveDisturbance(Model model, int treeIndex, float stemToSoilFraction, float stemToSnagFraction, float branchToSoilFraction, float branchToSnagFraction, float foliageToSoilFraction)
        {
            this.SetFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.RU.TreeDied();
            ResourceUnitSpecies ruSpecies = this.RU.GetSpecies(this.Species);
            ruSpecies.StatisticsDead.Add(this, treeIndex, null);
            this.NotifyTreeRemoved(model, treeIndex, MortalityCause.Disturbance);

            if (model.Saplings != null)
            {
                model.Saplings.AddSprout(model, this, treeIndex);
            }
            if (RU.Snags != null)
            {
                if (this.IsHarvested(treeIndex))
                { // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    RU.Snags.AddHarvest(this, treeIndex, 1.0F, 0.0F, 0.0F);
                }
                else
                {
                    RU.Snags.AddDisturbance(this, treeIndex, stemToSnagFraction, stemToSoilFraction, branchToSnagFraction, branchToSoilFraction, foliageToSoilFraction);
                }
            }
        }

        public void SetHeight(int treeIndex, float height)
        {
            if (height <= 0.0 || height > 150.0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Attempt to set invalid height " + height + " m for tree on RU " + (this.RU != null ? this.RU.BoundingBox : new Rectangle()));
            }
            this.Height[treeIndex] = height;
        }

        private void Mortality(Model model, int treeIndex, TreeGrowthData growthData)
        {
            // death if leaf area is near zero
            if (this.FoliageMass[treeIndex] < 0.00001)
            {
                this.Die(model, treeIndex);
            }

            double p_death, p_stress, p_intrinsic;
            p_intrinsic = Species.DeathProbabilityIntrinsic;
            p_stress = Species.GetDeathProbabilityForStress(growthData.StressIndex);
            p_death = p_intrinsic + p_stress;
            double p = model.RandomGenerator.Random(); //0..1
            if (p < p_death)
            {
                // die...
                this.Die(model, treeIndex);
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

        private void NotifyTreeRemoved(Model model, int treeIndex, MortalityCause reason)
        {
            Debug.Assert(treeIndex < this.Count);

            // tell disturbance modules that a tree died
            model.Modules.TreeDeath(this, reason);

            // update reason, if ABE handled the tree
            if (reason == MortalityCause.Disturbance && this.IsHarvested(treeIndex))
            {
                reason = MortalityCause.Salavaged;
            }
            if (this.IsCutDown(treeIndex))
            {
                reason = MortalityCause.CutDown;
            }
            // create output for tree removals
            TreeRemovedOutput treeRemovedOutput = model.Outputs.TreeRemoved;
            if (treeRemovedOutput != null && treeRemovedOutput.IsEnabled)
            {
                treeRemovedOutput.AddTree(model, this, treeIndex, reason);
            }

            LandscapeRemovedOutput landscapeRemovedOutput = model.Outputs.LandscapeRemoved;
            if (landscapeRemovedOutput != null && landscapeRemovedOutput.IsEnabled)
            {
                landscapeRemovedOutput.AddTree(this, treeIndex, reason);
            }
        }
    }
}
