using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using Model = iLand.Simulation.Model;

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

        public List<int> Age { get; private init; } // the tree age (years)
        public List<float> Dbh { get; private init; } // diameter at breast height in cm
        public List<float> DbhDelta { get; private init; } // diameter growth [cm]
        public List<float> Height { get; private init; } // tree height in m
        public List<float> LeafArea { get; private init; } // leaf area (m2) of the tree
        public List<Point> LightCellPosition { get; private init; } // index of the trees position on the basic LIF grid
        public List<float> LightResourceIndex { get; private init; } // LRI of the tree (updated during readStamp())
        public List<float> LightResponse { get; private init; } // light response used for distribution of biomass on RU level
        public List<float> NppReserve { get; private init; } // NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public List<float> Opacity { get; private init; } // multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public ResourceUnit RU { get; private init; } // pointer to the ressource unit the tree belongs to.
        public TreeSpecies Species { get; set; } // pointer to the tree species of the tree.
        public List<LightStamp?> Stamp { get; private init; }
        public List<int> StandID { get; private init; }
        public List<int> Tag { get; private init; } // (usually) numerical unique ID of the tree

        // biomass properties
        public List<float> CoarseRootMass { get; private init; } // mass (kg) of coarse roots
        public List<float> FineRootMass { get; private init; } // mass (kg) of fine roots
        public List<float> FoliageMass { get; private init; } // mass (kg) of foliage
        public List<float> StemMass { get; private init; } // mass (kg) of stem
        public List<float> StressIndex { get; private init; } // the scalar stress rating (0..1), used for mortality

        public Trees(Landscape landscape, ResourceUnit resourceUnit, TreeSpecies species)
        {
            this.heightGrid = landscape.HeightGrid;
            this.lightGrid = landscape.LightGrid;
            this.flags = new List<TreeFlags>(Constant.Simd128x4.Width);
            
            this.Age = new List<int>(Constant.Simd128x4.Width);
            this.CoarseRootMass = new List<float>(Constant.Simd128x4.Width);
            this.Dbh = new List<float>(Constant.Simd128x4.Width);
            this.DbhDelta = new List<float>(Constant.Simd128x4.Width);
            this.FineRootMass = new List<float>(Constant.Simd128x4.Width);
            this.FoliageMass = new List<float>(Constant.Simd128x4.Width);
            this.Height = new List<float>(Constant.Simd128x4.Width);
            this.LeafArea = new List<float>(Constant.Simd128x4.Width);
            this.LightCellPosition = new List<Point>(Constant.Simd128x4.Width);
            this.LightResourceIndex = new List<float>(Constant.Simd128x4.Width);
            this.LightResponse = new List<float>(Constant.Simd128x4.Width);
            this.NppReserve = new List<float>(Constant.Simd128x4.Width);
            this.Opacity = new List<float>(Constant.Simd128x4.Width);
            this.RU = resourceUnit;
            this.Species = species;
            this.Stamp = new List<Tree.LightStamp?>(Constant.Simd128x4.Width);
            this.StandID = new List<int>(Constant.Simd128x4.Width);
            this.StemMass = new List<float>(Constant.Simd128x4.Width);
            this.StressIndex = new List<float>(Constant.Simd128x4.Width);
            this.Tag = new List<int>(Constant.Simd128x4.Width);

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
                this.Tag.Capacity = value;
                this.LeafArea.Capacity = value;
                this.LightCellPosition.Capacity = value;
                this.LightResourceIndex.Capacity = value;
                this.LightResponse.Capacity = value;
                this.NppReserve.Capacity = value;
                this.Opacity.Capacity = value;
                this.Stamp.Capacity = value;
                this.StandID.Capacity = value;
                this.StemMass.Capacity = value;
                this.StressIndex.Capacity = value;
            }
        }
        
        public int Count 
        { 
            get { return this.Height.Count; }
        }

        /// @property position The tree does not store the floating point coordinates but only the index of pixel on the LIF grid
        /// TODO: store input coordinates of tree
        public PointF GetCellCenterPoint(int treeIndex) 
        { 
            Debug.Assert(this.lightGrid != null);
            return this.lightGrid.GetCellCenterPosition(this.LightCellPosition[treeIndex]); 
        }

        public void SetLightCellIndex(int treeIndex, PointF pos) 
        { 
            Debug.Assert(this.lightGrid != null); 
            this.LightCellPosition[treeIndex] = this.lightGrid.GetCellIndex(pos); 
        }

        // private bool IsDebugging() { return this.flags[treeIndex].HasFlag(TreeFlags.Debugging); }
        public void SetDebugging(int treeIndex, bool enable = true) { this.SetFlag(treeIndex, TreeFlags.Debugging, enable); }

        // death reasons
        public bool IsCutDown(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadCutAndDrop); }
        public bool IsDead(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Dead); } // returns true if the tree is already dead.
        public bool IsDeadBarkBeetle(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromBarkBeetles); }
        public bool IsDeadFire(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromFire); }
        public bool IsDeadWind(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromWind); }
        public bool IsHarvested(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Harvested); }

        public void SetDeathReasonBarkBeetle(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadFromBarkBeetles, true); }
        public void SetDeathReasonCutAndDrop(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadCutAndDrop, true); }
        public void SetDeathReasonFire(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadFromFire, true); }
        public void SetDeathReasonHarvested(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.Harvested, true); }
        public void SetDeathReasonWind(int treeIndex) { this.SetFlag(treeIndex, TreeFlags.DeadFromWind, true); }

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
            this.Tag.Add(0);
            this.LeafArea.Add(0.0F);
            this.LightCellPosition.Add(Point.Empty);
            this.LightResourceIndex.Add(0.0F);
            this.LightResponse.Add(0.0F);
            this.NppReserve.Add(0.0F);
            this.Opacity.Add(0.0F);
            this.Stamp.Add(null);
            this.StandID.Add(-1);
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
            this.Tag.Add(other.Tag[otherTreeIndex]);
            this.LeafArea.Add(other.LeafArea[otherTreeIndex]);
            this.LightCellPosition.Add(other.LightCellPosition[otherTreeIndex]);
            this.LightResourceIndex.Add(other.LightResourceIndex[otherTreeIndex]);
            this.LightResponse.Add(other.LightResponse[otherTreeIndex]);
            this.NppReserve.Add(other.NppReserve[otherTreeIndex]);
            this.Opacity.Add(other.Opacity[otherTreeIndex]);
            this.Stamp.Add(other.Stamp[otherTreeIndex]);
            this.StandID.Add(other.StandID[otherTreeIndex]);
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
            this.Tag[destinationIndex] = this.Tag[sourceIndex];
            this.LeafArea[destinationIndex] = this.LeafArea[sourceIndex];
            this.LightCellPosition[destinationIndex] = this.LightCellPosition[sourceIndex];
            this.LightResourceIndex[destinationIndex] = this.LightResourceIndex[sourceIndex];
            this.LightResponse[destinationIndex] = this.LightResponse[sourceIndex];
            this.NppReserve[destinationIndex] = this.NppReserve[sourceIndex];
            this.Opacity[destinationIndex] = this.Opacity[sourceIndex];
            this.Stamp[destinationIndex] = this.Stamp[sourceIndex];
            this.StandID[destinationIndex] = this.StandID[sourceIndex];
            this.StemMass[destinationIndex] = this.StemMass[sourceIndex];
            this.StressIndex[destinationIndex] = this.StressIndex[sourceIndex];
        }

        public void ApplyLightIntensityPattern(int treeIndex)
        {
            LightStamp stamp = this.Stamp[treeIndex]!;
            Point stampOrigin = this.LightCellPosition[treeIndex];
            stampOrigin.X -= stamp.CenterCellPosition;
            stampOrigin.Y -= stamp.CenterCellPosition;
            int stampSize = stamp.Size();
            if (this.lightGrid.Contains(stampOrigin) == false || this.lightGrid.Contains(new Point(stampOrigin.X + stampSize, stampOrigin.Y + stampSize)) == false)
            {
                throw new NotSupportedException("Light grid's buffer width is not large enough to stamp tree.");
            }

            for (int lightY = stampOrigin.Y, stampY = 0; stampY < stampSize; ++lightY, ++stampY)
            {
                int lightIndex = this.lightGrid.IndexOf(stampOrigin.X, lightY);
                for (int lightX = stampOrigin.X, stampX = 0; stampX < stampSize; ++lightX, ++lightIndex, ++stampX)
                {
                    // suppose there is no stamping outside
                    float value = stamp[stampX, stampY]; // stampvalue
                    //if (value>0.f) {
                    float dominantHeight = this.heightGrid[lightX, lightY, Constant.LightCellsPerHeightSize].Height; // height of Z* on the current position
                    float z = MathF.Max(this.Height[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= dominantHeight) ? 1.0F : z / dominantHeight;
                    value = 1.0F - value * this.Opacity[treeIndex] * z_zstar; // calculated value
                    value = MathF.Max(value, 0.02F); // limit value

                    this.lightGrid[lightIndex] *= value;
                }
            }

            //++Tree.StampApplications;
        }

        /// helper function for gluing the edges together
        /// index: index at grid
        /// count: number of pixels that are the simulation area (e.g. 100m and 2m pixel . 50)
        /// buffer: size of buffer around simulation area (in pixels)
        private static int GetTorusIndex(int index, int count, int buffer, int ruOffset)
        {
            return buffer + ruOffset + (index - buffer + count) % count;
        }

        /** Apply LIPs. This "Torus" functions wraps the influence at the edges of a 1ha simulation area.
          */
        public void ApplyLightIntensityPatternTorus(int treeIndex)
        {
            Debug.Assert(this.lightGrid != null && this.Stamp != null && this.RU != null);
            int lightBufferWidth = this.lightGrid.GetCellIndex(new PointF(0.0F, 0.0F)).X; // offset of buffer
            Point treePositionInRU = new Point((this.LightCellPosition[treeIndex].X - lightBufferWidth) % Constant.LightCellsPerRUsize + lightBufferWidth,
                                               (this.LightCellPosition[treeIndex].Y - lightBufferWidth) % Constant.LightCellsPerRUsize + lightBufferWidth); // offset within the ha
            Point ruOffset = new Point(this.LightCellPosition[treeIndex].X - treePositionInRU.X, this.LightCellPosition[treeIndex].Y - treePositionInRU.Y); // offset of the corner of the resource index

            LightStamp stamp = this.Stamp[treeIndex]!;
            Point stampOrigin = new Point(treePositionInRU.X - stamp.CenterCellPosition, treePositionInRU.Y - stamp.CenterCellPosition);

            int stampSize = stamp.Size();
            if (this.lightGrid.Contains(stampOrigin) == false || this.lightGrid.Contains(new Point(stampOrigin.X + stampSize, stampOrigin.Y + stampSize)) == false)
            {
                // TODO: in this case we should use another algorithm!!! necessary????
                throw new NotSupportedException("Light grid's buffer width is not large enough to stamp tree.");
            }

            for (int stampY = 0; stampY < stampSize; ++stampY)
            {
                int lightY = stampOrigin.Y + stampY;
                int torusY = Trees.GetTorusIndex(lightY, Constant.LightCellsPerRUsize, lightBufferWidth, ruOffset.Y); // 50 cells per 100m
                for (int stampX = 0; stampX < stampSize; ++stampX)
                {
                    // suppose there is no stamping outside
                    int lightX = stampOrigin.X + stampX;
                    int torusX = Trees.GetTorusIndex(lightX, Constant.LightCellsPerRUsize, lightBufferWidth, ruOffset.X);

                    float dominantHeight = this.heightGrid[torusX, torusY, Constant.LightCellsPerHeightSize].Height; // height of Z* on the current position
                    float z = MathF.Max(this.Height[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= dominantHeight) ? 1.0F : z / dominantHeight;
                    float value = stamp[stampX, stampY]; // stampvalue
                    value = 1.0F - value * this.Opacity[treeIndex] * z_zstar; // calculated value
                    // old: value = 1. - value*mOpacity / local_dom; // calculated value
                    value = MathF.Max(value, 0.02f); // limit value

                    this.lightGrid[torusX, torusY] *= value; // use wraparound coordinates
                }
            }

            //++Tree.StampApplications;
        }

        /** heightGrid()
          This function calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
            */
        public void CalculateDominantHeightField(int treeIndex)
        {
            Point heightCellPosition = new Point(this.LightCellPosition[treeIndex].X / Constant.LightCellsPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightCellsPerHeightSize); // pos of tree on height grid

            // count trees that are on height-grid cells (used for stockable area)
            this.heightGrid[heightCellPosition].AddTree(this.Height[treeIndex]);

            LightStamp reader = this.Stamp[treeIndex]!.Reader!;
            int center = reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int indexEastWest = this.LightCellPosition[treeIndex].X % Constant.LightCellsPerHeightSize; // 4: very west, 0 east edge
            int indexNorthSouth = this.LightCellPosition[treeIndex].Y % Constant.LightCellsPerHeightSize; // 4: northern edge, 0: southern edge
            if (indexEastWest - center < 0)
            { // east
                this.heightGrid[heightCellPosition.X - 1, heightCellPosition.Y].Height = MathF.Max(this.heightGrid[heightCellPosition.X - 1, heightCellPosition.Y].Height, this.Height[treeIndex]);
            }
            if (indexEastWest + center >= Constant.LightCellsPerHeightSize)
            {  // west
                this.heightGrid[heightCellPosition.X + 1, heightCellPosition.Y].Height = MathF.Max(this.heightGrid[heightCellPosition.X + 1, heightCellPosition.Y].Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth - center < 0)
            {  // south
                this.heightGrid[heightCellPosition.X, heightCellPosition.Y - 1].Height = MathF.Max(this.heightGrid[heightCellPosition.X, heightCellPosition.Y - 1].Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth + center >= Constant.LightCellsPerHeightSize)
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
            Point heightCellPosition = new Point(this.LightCellPosition[treeIndex].X / Constant.LightCellsPerHeightSize, this.LightCellPosition[treeIndex].Y / Constant.LightCellsPerHeightSize); // pos of tree on height grid
            int bufferOffset = this.heightGrid.GetCellIndex(new PointF(0.0F, 0.0F)).X; // offset of buffer (i.e.: size of buffer in height-pixels)
            heightCellPosition.X = (heightCellPosition.X - bufferOffset) % Constant.HeightSizePerRU + bufferOffset; // 10: 10 x 10m pixeln in 100m
            heightCellPosition.Y = (heightCellPosition.Y - bufferOffset) % Constant.HeightSizePerRU + bufferOffset;

            // torus coordinates: ru_offset = coords of lower left corner of 1ha patch
            Point ru_offset = new Point(this.LightCellPosition[treeIndex].X / Constant.LightCellsPerHeightSize - heightCellPosition.X, this.LightCellPosition[treeIndex].Y / Constant.LightCellsPerHeightSize - heightCellPosition.Y);

            // count trees that are on height-grid cells (used for stockable area)
            int torusX = GetTorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X);
            int torusY = GetTorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y);
            HeightCell heightCell = this.heightGrid[torusX, torusY];
            heightCell.AddTree(this.Height[treeIndex]);

            LightStamp reader = this.Stamp[treeIndex]!.Reader!;
            int readerCenter = reader.CenterCellPosition; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int indexEastWest = this.LightCellPosition[treeIndex].X % Constant.LightCellsPerHeightSize; // 4: very west, 0 east edge
            int indexNorthSouth = this.LightCellPosition[treeIndex].Y % Constant.LightCellsPerHeightSize; // 4: northern edge, 0: southern edge
            if (indexEastWest - readerCenter < 0)
            { // east
                heightCell = this.heightGrid[GetTorusIndex(heightCellPosition.X - 1, 10, bufferOffset, ru_offset.X), GetTorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightSize)
            {  // west
                heightCell = this.heightGrid[GetTorusIndex(heightCellPosition.X + 1, 10, bufferOffset, ru_offset.X), GetTorusIndex(heightCellPosition.Y, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth - readerCenter < 0)
            {  // south
                heightCell = this.heightGrid[GetTorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X), GetTorusIndex(heightCellPosition.Y - 1, 10, bufferOffset, ru_offset.Y)];
                heightCell.Height = MathF.Max(heightCell.Height, this.Height[treeIndex]);
            }
            if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightSize)
            {  // north
                heightCell = this.heightGrid[GetTorusIndex(heightCellPosition.X, 10, bufferOffset, ru_offset.X), GetTorusIndex(heightCellPosition.Y + 1, 10, bufferOffset, ru_offset.Y)];
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
        public void Die(Model model, int treeIndex)
        {
            this.SetFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.RU.Trees.OnTreeDied();
            
            ResourceUnitTreeSpecies ruSpecies = this.RU.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsDead.AddToCurrentYear(this, treeIndex, null, skipDead: false); // add tree to statistics
            
            this.OnTreeRemoved(model, treeIndex, MortalityCause.Stress);
            
            if (this.RU.Snags != null)
            {
                this.RU.Snags.AddMortality(this, treeIndex);
            }
        }

        /// dumps some core variables of a tree to a string.
        //private string Dump(int treeIndex)
        //{
        //    string result = String.Format("id {0} species {1} dbh {2} h {3} x/y {4}/{5} ru# {6} LRI {7}",
        //                                  this.Tag, this.Species.ID, this.Dbh, this.Height,
        //                                  this.GetCellCenterPoint(treeIndex).X, this.GetCellCenterPoint(treeIndex).Y,
        //                                  this.RU.ResourceUnitGridIndex, this.LightResourceIndex);
        //    return result;
        //}

        //private void DumpList(List<object> rTargetList)
        //{
        //    rTargetList.AddRange(new object[] { ID, Species.ID, Dbh, Height, GetCellCenterPoint().X, GetCellCenterPoint().Y, RU.Index, LightResourceIndex,
        //                                        StemMass, CoarseRootMass + FoliageMass + LeafArea } );
        //}

        public float GetCrownRadius(int treeIndex)
        {
            Debug.Assert(this.Stamp != null);
            return this.Stamp[treeIndex]!.CrownRadius;
        }

        public float GetBranchBiomass(int treeIndex)
        {
            return this.Species.GetBiomassBranch(this.Dbh[treeIndex]);
        }

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland.boku.ac.at/competition+for+light for details.
          */
        public void ReadLightInfluenceField(int treeIndex)
        {
            LightStamp reader = this.Stamp[treeIndex]!.Reader!;
            Point lightCellPosition = this.LightCellPosition[treeIndex];
            float outsideAreaFactor = 0.1F;

            int readerOffset = reader.CenterCellPosition;
            int writerOffset = this.Stamp[treeIndex]!.CenterCellPosition;
            int writerReaderOffset = writerOffset - readerOffset; // offset on the *stamp* to the crown-cells

            lightCellPosition.X -= readerOffset;
            lightCellPosition.Y -= readerOffset;

            float sum = 0.0F;
            int readerSize = reader.Size();
            int rx = lightCellPosition.X;
            int ry = lightCellPosition.Y;
            for (int y = 0; y < readerSize; ++y, ++ry)
            {
                float lightValue = this.lightGrid[rx, ry];
                for (int x = 0; x < readerSize; ++x)
                {
                    HeightCell heightCell = this.heightGrid[rx + x, ry, Constant.LightCellsPerHeightSize]; // the height grid value, ry: gets ++ed in outer loop, rx not
                    float local_dom = heightCell.Height;
                    float z = MathF.Max(this.Height[treeIndex] - reader.GetDistanceToCenter(x, y), 0.0F); // distance to center = height (45 degree line)
                    float z_zstar = (z >= local_dom) ? 1.0F : z / local_dom;

                    float treeValue = 1.0F - this.Stamp[treeIndex]![x, y, writerReaderOffset] * this.Opacity[treeIndex] * z_zstar;
                    treeValue = MathF.Max(treeValue, 0.02F);
                    float value = lightValue / treeValue; // remove impact of focal tree
                    // additional punishment if pixel is outside
                    if (heightCell.IsOnLandscape() == false)
                    {
                        value *= outsideAreaFactor;
                    }
                    //Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                    //if (value>0.)
                    sum += value * reader[x, y];
                }
            }
            this.LightResourceIndex[treeIndex] = sum;
            // LRI correction...
            float relativeHeight = this.Height[treeIndex] / this.heightGrid[this.LightCellPosition[treeIndex].X, this.LightCellPosition[treeIndex].Y, Constant.LightCellsPerHeightSize].Height;
            if (relativeHeight < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(this.LightResourceIndex[treeIndex], relativeHeight);
            }

            if (this.LightResourceIndex[treeIndex] > 1.0F)
            {
                this.LightResourceIndex[treeIndex] = 1.0F;
            }
            // Finally, add LRI of this Tree to the ResourceUnit!
            this.RU.Trees.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);

            //Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightInfluenceFieldTorus(int treeIndex)
        {
            LightStamp reader = this.Stamp[treeIndex]!.Reader!;
            int bufferOffset = this.lightGrid.GetCellIndex(new PointF(0.0F, 0.0F)).X; // offset of buffer

            Point treePositionInRU = new Point((this.LightCellPosition[treeIndex].X - bufferOffset) % Constant.LightCellsPerRUsize + bufferOffset,
                                               (this.LightCellPosition[treeIndex].Y - bufferOffset) % Constant.LightCellsPerRUsize + bufferOffset); // offset within the ha
            Point ruOffset = new Point(this.LightCellPosition[treeIndex].X - treePositionInRU.X, this.LightCellPosition[treeIndex].Y - treePositionInRU.Y); // offset of the corner of the resource index

            float lightIndex = 0.0F;
            int readerSize = reader.Size();
            int readerOriginX = treePositionInRU.X - reader.CenterCellPosition;
            int readerOriginY = treePositionInRU.Y - reader.CenterCellPosition;
            int writerReaderOffset = this.Stamp[treeIndex]!.CenterCellPosition - reader.CenterCellPosition; // offset on the *stamp* to the crown (light?) cells
            for (int readerY = 0; readerY < readerSize; ++readerY)
            {
                int yTorus = GetTorusIndex(readerOriginY + readerY, Constant.LightCellsPerRUsize, bufferOffset, ruOffset.Y);
                for (int readerX = 0; readerX < readerSize; ++readerX)
                {
                    // see http://iland.boku.ac.at/competition+for+light 
                    int xTorus = GetTorusIndex(readerOriginX + readerX, Constant.LightCellsPerRUsize, bufferOffset, ruOffset.X);
                    float dominantHeightTorus = this.heightGrid[xTorus, yTorus, Constant.LightCellsPerHeightSize].Height; // ry: gets ++ed in outer loop, rx not
                    float influenceZ = MathF.Max(this.Height[treeIndex] - reader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                    float influenceZstar = (influenceZ >= dominantHeightTorus) ? 1.0F : influenceZ / dominantHeightTorus;

                    // TODO: why a nonzero floor as opposed to skipping division?
                    float focalIntensity = MathF.Max(1.0F - this.Stamp[treeIndex]![readerX, readerY, writerReaderOffset] * this.Opacity[treeIndex] * influenceZstar, 0.02F);
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
            float hrel = this.Height[treeIndex] / this.heightGrid[this.LightCellPosition[treeIndex].X, this.LightCellPosition[treeIndex].Y, Constant.LightCellsPerHeightSize].Height;
            if (hrel < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(this.LightResourceIndex[treeIndex], hrel);
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
            this.RU.Trees.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);
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
            this.Tag.RemoveRange(index, count);
            this.LeafArea.RemoveRange(index, count);
            this.LightCellPosition.RemoveRange(index, count);
            this.LightResourceIndex.RemoveRange(index, count);
            this.LightResponse.RemoveRange(index, count);
            this.NppReserve.RemoveRange(index, count);
            this.Opacity.RemoveRange(index, count);
            this.Stamp.RemoveRange(index, count);
            this.StandID.RemoveRange(index, count);
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

        public void CalculateLightResponse(int treeIndex)
        {
            // calculate a light response from lri:
            // http://iland.boku.ac.at/individual+tree+light+availability
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.RU.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F); // Eq. (3)
            this.LightResponse[treeIndex] = this.Species.GetLightResponse(lri); // Eq. (4)
            this.RU.Trees.AddLightResponse(this.LeafArea[treeIndex], this.LightResponse[treeIndex]);
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
        public void CalculateAnnualGrowth(Model model)
        {
            // get the GPP for a "unit area" of the tree species
            ResourceUnitTreeSpecies ruSpecies = this.RU.Trees.GetResourceUnitSpecies(this.Species);
            TreeGrowthData treeGrowthData = new TreeGrowthData();
            for (int treeIndex = 0; treeIndex < this.Count; ++treeIndex)
            {
                // increase age
                ++this.Age[treeIndex];

                // apply aging according to the state of the individal
                float agingFactor = this.Species.GetAgingFactor(this.Height[treeIndex], this.Age[treeIndex]);
                this.RU.Trees.AddAging(this.LeafArea[treeIndex], agingFactor);

                // step 1: get "interception area" of the tree individual [m2]
                // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
                float effectiveTreeArea = this.RU.Trees.GetPhotosyntheticallyActiveArea(this.LeafArea[treeIndex], this.LightResponse[treeIndex]); // light response in [0...1] depending on suppression

                // step 2: calculate GPP of the tree based
                // (2) GPP (without aging-effect) in kg Biomass / year
                float treeGppBeforeAging = ruSpecies.BiomassGrowth.AnnualGpp * effectiveTreeArea;
                float treeGpp = treeGppBeforeAging * agingFactor;
                Debug.Assert(treeGpp >= 0.0F);
                treeGrowthData.NppAboveground = 0.0F;
                treeGrowthData.NppStem = 0.0F;
                treeGrowthData.NppTotal = model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier * treeGpp; // respiration loss (0.47), cf. Waring et al 1998.
                treeGrowthData.StressIndex = 0.0F;

                //DBGMODE(
                //if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.TreeNpp) && IsDebugging())
                //{
                //    List<object> outList = model.GlobalSettings.DebugList(ID, DebugOutputs.TreeNpp);
                //    DumpList(outList); // add tree headers
                //    outList.AddRange(new object[] { this.LightResourceIndex[treeIndex] * RU.LriModifier, LightResponse, effective_area, raw_gpp, gpp, d.NppTotal, agingFactor });
                //}
                //); // DBGMODE()
                if (model.Project.Model.Settings.GrowthEnabled && (treeGrowthData.NppTotal > 0.0))
                {
                    this.PartitionBiomass(treeGrowthData, model, treeIndex); // split npp to compartments and grow (diameter, height)
                }

                // mortality
                //#ifdef ALT_TREE_MORTALITY
                //    // alternative variant of tree mortality (note: mStrssIndex used otherwise)
                //    altMortality(d);
                //#else
                if (model.Project.Model.Settings.MortalityEnabled)
                {
                    this.CheckIntrinsicAndStressMortality(model, treeIndex, treeGrowthData);
                }
                this.StressIndex[treeIndex] = treeGrowthData.StressIndex;
                //#endif

                if (this.IsDead(treeIndex) == false)
                {
                    ruSpecies.Statistics.AddToCurrentYear(this, treeIndex, treeGrowthData, skipDead: true);

                    int standID = this.StandID[treeIndex];
                    if (standID >= 0)
                    {
                        this.RU.Trees.TreeStatisticsByStandID[standID].AddToCurrentYear(this, treeIndex, treeGrowthData, skipDead: true);
                    }
                }

                // regeneration
                this.Species.DisperseSeeds(model.RandomGenerator, this, treeIndex);
            }
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on Duursma, 2007.
          @sa http://iland.boku.ac.at/allocation */
        private void PartitionBiomass(TreeGrowthData growthData, Model model, int treeIndex)
        {
            // available resources
            float nppAvailable = growthData.NppTotal + this.NppReserve[treeIndex];
            float foliageBiomass = this.Species.GetBiomassFoliage(this.Dbh[treeIndex]);
            float reserveSize = foliageBiomass * (1.0F + this.Species.FinerootFoliageRatio);
            float reserveAllocation = MathF.Min(reserveSize, (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMass[treeIndex]); // not always try to refill reserve 100%

            ResourceUnitTreeSpecies ruSpecies = this.RU.Trees.GetResourceUnitSpecies(Species);
            float rootFraction = ruSpecies.BiomassGrowth.RootFraction;
            growthData.NppAboveground = growthData.NppTotal * (1.0F - rootFraction); // aboveground: total NPP - fraction to roots
            float woodFoliageRatio = Species.GetWoodFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

            // turnover rates
            float foliageTurnover = this.Species.TurnoverLeaf;
            float rootTurnover = this.Species.TurnoverFineRoot;
            // the turnover rate of wood depends on the size of the reserve pool:
            float woodTurnover = reserveAllocation / (this.StemMass[treeIndex] + reserveAllocation);

            // Duursma 2007, Eq. (20) allocation percentages (sum=1) (eta)
            float woodFraction = (foliageBiomass * woodTurnover / nppAvailable + woodFoliageRatio * (1.0F - rootFraction) - woodFoliageRatio * foliageBiomass * foliageTurnover / nppAvailable) / (foliageBiomass / this.StemMass[treeIndex] + woodFoliageRatio);
            woodFraction = Maths.Limit(woodFraction, 0.0F, 1.0F - rootFraction);
            float foliageFraction = 1.0F - rootFraction - woodFraction;

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
            float rootSenescence = this.FineRootMass[treeIndex] * rootTurnover;
            float foliageSenescence = this.FoliageMass[treeIndex] * foliageTurnover;
            if (this.RU.Snags != null)
            {
                this.RU.Snags.AddTurnoverLitter(this.Species, foliageSenescence, rootSenescence);
            }

            // Roots
            // http://iland.boku.ac.at/allocation#belowground_NPP
            this.FineRootMass[treeIndex] -= rootSenescence; // reduce only fine root pool
            float rootAllocation = rootFraction * nppAvailable;
            // 1st, refill the fine root pool
            float finerootMass = this.FoliageMass[treeIndex] * this.Species.FinerootFoliageRatio - this.FineRootMass[treeIndex];
            if (finerootMass > 0.0F)
            {
                float finerootAllocaton = MathF.Min(finerootMass, rootAllocation);
                this.FineRootMass[treeIndex] += finerootAllocaton;
                rootAllocation -= finerootAllocaton;
            }
            // 2nd, the rest of NPP allocated to roots go to coarse roots
            float maxCoarseRootBiomass = this.Species.GetBiomassRoot(this.Dbh[treeIndex]);
            this.CoarseRootMass[treeIndex] += rootAllocation;
            if (this.CoarseRootMass[treeIndex] > maxCoarseRootBiomass)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (this.RU.Snags != null)
                {
                    this.RU.Snags.AddTurnoverWood(this.CoarseRootMass[treeIndex] - maxCoarseRootBiomass, this.Species);
                }
                this.CoarseRootMass[treeIndex] = maxCoarseRootBiomass;
            }

            // foliage
            float foliageAllocation = foliageFraction * nppAvailable - foliageSenescence;
            if (Single.IsNaN(foliageAllocation))
            {
                throw new ArithmeticException("Foliage mass is NaN.");
            }

            this.FoliageMass[treeIndex] += foliageAllocation;
            if (this.FoliageMass[treeIndex] < 0.0F)
            {
                this.FoliageMass[treeIndex] = 0.0F; // limit to zero
            }

            this.LeafArea[treeIndex] = this.FoliageMass[treeIndex] * this.Species.SpecificLeafArea; // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            growthData.StressIndex = MathF.Max(1.0F - nppAvailable / (foliageTurnover * foliageBiomass + rootTurnover * foliageBiomass * this.Species.FinerootFoliageRatio + reserveSize), 0.0F);

            // Woody compartments
            // see also: http://iland.boku.ac.at/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            float woodyAllocation = woodFraction * nppAvailable;
            float toReserve = MathF.Min(reserveSize, woodyAllocation);
            this.NppReserve[treeIndex] = toReserve;
            float netWoodyAllocation = woodyAllocation - toReserve;

            this.DbhDelta[treeIndex] = 0.0F; // zeroing this here causes height and diameter growth to start with an estimate of only height growth
            if (netWoodyAllocation > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                float stemAllocation = netWoodyAllocation * this.Species.GetStemFraction(this.Dbh[treeIndex]);
                growthData.NppStem = stemAllocation;
                this.StemMass[treeIndex] += netWoodyAllocation;
                //  (3) growth of diameter and height baseed on net stem increment
                this.GrowHeightAndDiameter(model, treeIndex, growthData);
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
        private void GrowHeightAndDiameter(Model model, int treeIndex, TreeGrowthData growthData)
        {
            // determine dh-ratio of increment
            // height increment is a function of light competition:
            float hdRatioNewGrowth = this.GetRelativeHeightGrowth(treeIndex); // hd of height growth
            float dbhInM = 0.01F * this.Dbh[treeIndex]; // current diameter in [m]
            float previousYearDbhIncrementInM = 0.01F * this.DbhDelta[treeIndex]; // increment of last year in [m]

            float massFactor = this.Species.VolumeFactor * this.Species.WoodDensity;
            float stemMass = massFactor * dbhInM * dbhInM * this.Height[treeIndex]; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            float factorDiameter = 1.0F / (massFactor * (dbhInM + previousYearDbhIncrementInM) * (dbhInM + previousYearDbhIncrementInM) * (2.0F * this.Height[treeIndex] / dbhInM + hdRatioNewGrowth));
            float nppStem = growthData.NppStem;
            float deltaDbhEstimate = factorDiameter * nppStem; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            float stemEstimate = massFactor * (dbhInM + deltaDbhEstimate) * (dbhInM + deltaDbhEstimate) * (this.Height[treeIndex] + deltaDbhEstimate * hdRatioNewGrowth);
            float stemResidual = stemEstimate - (stemMass + nppStem);

            // the final increment is then:
            float dbhIncrementInM = factorDiameter * (nppStem - stemResidual); // Eq. (11)
            if (Math.Abs(stemResidual) > Math.Min(1.0, stemMass))
            {
                // calculate final residual in stem
                float res_final = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.Height[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - ((stemMass + nppStem));
                if (Math.Abs(res_final) > Math.Min(1.0, stemMass))
                {
                    // for large errors in stem biomass due to errors in diameter increment (> 1kg or >stem mass), we solve the increment iteratively.
                    // first, increase increment with constant step until we overestimate the first time
                    // then,
                    dbhIncrementInM = 0.02F; // start with 2cm increment
                    bool reached_error = false;
                    float step = 0.01F; // step-width 1cm
                    do
                    {
                        float est_stem = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.Height[treeIndex] + dbhIncrementInM * hdRatioNewGrowth); // estimate with current increment
                        stemResidual = est_stem - (stemMass + nppStem);

                        if (MathF.Abs(stemResidual) < 1.0F) // finished, if stem residual below 1kg
                        {
                            break;
                        }
                        if (stemResidual > 0.0F)
                        {
                            dbhIncrementInM -= step;
                            reached_error = true;
                        }
                        else
                        {
                            dbhIncrementInM += step;
                        }
                        if (reached_error)
                        {
                            step /= 2.0F;
                        }
                    }
                    while (step > 0.00001F); // continue until diameter "accuracy" falls below 1/100mm
                }
            }

            if (dbhIncrementInM < 0.0F)
            {
                Debug.WriteLine("grow_diameter: d_inc < 0.0");
            }
            Debug.Assert((dbhIncrementInM >= 0.0) && (dbhIncrementInM <= 0.1), String.Format("Diameter increment out of range: HD {0}, factor_diameter {1}, stem_residual {2}, delta_d_estimate {3}, d_increment {4}, final residual {5} kg.",
                                                                                             hdRatioNewGrowth, 
                                                                                             factorDiameter, 
                                                                                             stemResidual, 
                                                                                             deltaDbhEstimate, 
                                                                                             dbhIncrementInM, 
                                                                                             massFactor * (this.Dbh[treeIndex] + dbhIncrementInM) * (this.Dbh[treeIndex] + dbhIncrementInM) * (this.Height[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - stemMass + nppStem));

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

            dbhIncrementInM = MathF.Max(dbhIncrementInM, 0.0F);

            // update state variables
            this.Dbh[treeIndex] += 100.0F * dbhIncrementInM; // convert from [m] to [cm]
            this.DbhDelta[treeIndex] = 100.0F * dbhIncrementInM; // save for next year's growth
            this.Height[treeIndex] += dbhIncrementInM * hdRatioNewGrowth;

            // update state of LIP stamp and opacity
            this.Stamp[treeIndex] = this.Species.GetStamp(this.Dbh[treeIndex], this.Height[treeIndex]); // get new stamp for updated dimensions
            // calculate the CrownFactor which reflects the opacity of the crown
            float k = model.Project.Model.Ecosystem.LightExtinctionCoefficientOpacity;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-k * this.LeafArea[treeIndex] / this.Stamp[treeIndex]!.CrownArea);
        }

        /// return the HD ratio of this year's increment based on the light status.
        private float GetRelativeHeightGrowth(int treeIndex)
        {
            this.Species.GetHeightDiameterRatioLimits(this.Dbh[treeIndex], out float hdRatioLow, out float hdRatioHigh);
            Debug.Assert(hdRatioLow < hdRatioHigh, "HD low higher than HD high.");
            Debug.Assert((hdRatioLow > 10.0F) && (hdRatioHigh < 250.0F), "HD ratio out of range. Low: " + hdRatioLow + ", high: " + hdRatioHigh);

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.RU.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F);
            float hdRatio = hdRatioHigh - (hdRatioHigh - hdRatioLow) * lri;
            return hdRatio;
        }

        public void SetAge(int treeIndex, int age, float height)
        {
            this.Age[treeIndex] = age;
            if (age == 0)
            {
                // estimate age using the tree height
                this.Age[treeIndex] = this.Species.EstimateAgeFromHeight(height);
            }
        }

        public void Setup(Project projectFile, int treeIndex)
        {
            float dbh = this.Dbh[treeIndex];
            if (dbh <= 0.0F || this.Height[treeIndex] <= 0.0F)
            {
                throw new NotSupportedException(String.Format("Invalid dimensions: dbh: {0} height: {1} id: {2} RU-index: {3}", dbh, this.Height[treeIndex], this.Tag[treeIndex], this.RU.ResourceUnitGridIndex));
            }
            // check stamp
            Debug.Assert(this.Species != null, "Setup()", "species is NULL");
            this.Stamp[treeIndex] = this.Species.GetStamp(dbh, this.Height[treeIndex]);
            if (this.Stamp[treeIndex] == null)
            {
                throw new NotSupportedException("Stamp is null.");
            }

            this.FoliageMass[treeIndex] = this.Species.GetBiomassFoliage(dbh);
            this.CoarseRootMass[treeIndex] = this.Species.GetBiomassRoot(dbh); // coarse root (allometry)
            this.FineRootMass[treeIndex] = this.FoliageMass[treeIndex] * this.Species.FinerootFoliageRatio; //  fine root (size defined  by finerootFoliageRatio)
            this.StemMass[treeIndex] = this.Species.GetBiomassWoody(dbh);

            // LeafArea[m2] = LeafMass[kg] * specificLeafArea[m2/kg]
            this.LeafArea[treeIndex] = this.FoliageMass[treeIndex] * this.Species.SpecificLeafArea;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-projectFile.Model.Ecosystem.LightExtinctionCoefficientOpacity * this.LeafArea[treeIndex] / this.Stamp[treeIndex]!.CrownArea);
            this.NppReserve[treeIndex] = (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMass[treeIndex]; // initial value
            this.DbhDelta[treeIndex] = 0.1F; // initial value: used in growth() to estimate diameter increment
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void Remove(Model model, int treeIndex, float removeFoliage = 0.0F, float removeBranch = 0.0F, float removeStem = 0.0F)
        {
            this.SetFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.SetDeathReasonHarvested(treeIndex);
            this.RU.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.RU.Trees.GetResourceUnitSpecies(Species);
            ruSpecies.StatisticsManagement.AddToCurrentYear(this, treeIndex, null, skipDead: false);
            this.OnTreeRemoved(model, treeIndex, this.IsCutDown(treeIndex) ?  MortalityCause.CutDown : MortalityCause.Harvest);

            this.RU.AddSprout(model, this, treeIndex);
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
            this.RU.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.RU.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsDead.AddToCurrentYear(this, treeIndex, null, skipDead: false);
            this.OnTreeRemoved(model, treeIndex, MortalityCause.Disturbance);

            this.RU.AddSprout(model, this, treeIndex);
            if (this.RU.Snags != null)
            {
                if (this.IsHarvested(treeIndex))
                { // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    this.RU.Snags.AddHarvest(this, treeIndex, 1.0F, 0.0F, 0.0F);
                }
                else
                {
                    this.RU.Snags.AddDisturbance(this, treeIndex, stemToSnagFraction, stemToSoilFraction, branchToSnagFraction, branchToSoilFraction, foliageToSoilFraction);
                }
            }
        }

        public void SetHeight(int treeIndex, float height)
        {
            if (height <= 0.0F || height > 150.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Attempt to set invalid height " + height + " m for tree on RU " + (this.RU != null ? this.RU.BoundingBox : new Rectangle()));
            }
            this.Height[treeIndex] = height;
        }

        private void CheckIntrinsicAndStressMortality(Model model, int treeIndex, TreeGrowthData growthData)
        {
            // death if leaf area is near zero
            if (this.FoliageMass[treeIndex] < 0.00001F)
            {
                this.Die(model, treeIndex);
            }

            float pFixed = this.Species.DeathProbabilityFixed;
            float pStress = this.Species.GetDeathProbabilityForStress(growthData.StressIndex);
            float pMortality = pFixed + pStress;
            float random = model.RandomGenerator.GetRandomFloat(); // 0..1
            if (random < pMortality)
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

        //    float p_intrinsic, p_stress = 0.;
        //    p_intrinsic = species().deathProb_intrinsic();

        //    if (mDbhDelta < _stress_threshold)
        //    {
        //        mStressIndex++;
        //        if (mStressIndex > _stress_years)
        //            p_stress = _stress_death_prob;
        //    }
        //    else
        //        mStressIndex = 0;

        //    float p = drandom(); //0..1
        //    if (p < p_intrinsic + p_stress)
        //    {
        //        // die...
        //        die();
        //    }
        //}
        //#endif

        private void OnTreeRemoved(Model model, int treeIndex, MortalityCause reason)
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
            if (model.AnnualOutputs.TreeRemoved != null)
            {
                model.AnnualOutputs.TreeRemoved.TryAddTree(model, this, treeIndex, reason);
            }

            if (model.AnnualOutputs.LandscapeRemoved != null)
            {
                model.AnnualOutputs.LandscapeRemoved.AddTree(this, treeIndex, reason);
            }
        }
    }
}
