using iLand.Extensions;
using iLand.Tool;
using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;
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
        private TreeFlags[] flags;

        public int Count { get; private set; }

        public int[] Age { get; private set; } // the tree age (years)
        public float[] Dbh { get; private set; } // diameter at breast height in cm
        public float[] DbhDelta { get; private set; } // diameter growth [cm]
        public float[] Height { get; private set; } // tree height in m
        public float[] LeafArea { get; private set; } // leaf area (m²) of the tree
        public Point[] LightCellIndexXY { get; private set; } // index of the trees position on the basic LIF grid
        public float[] LightResourceIndex { get; private set; } // LRI of the tree (updated during readStamp())
        public float[] LightResponse { get; private set; } // light response used for distribution of biomass on RU level
        public float[] NppReserve { get; private set; } // NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public float[] Opacity { get; private set; } // multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public ResourceUnit ResourceUnit { get; private set; } // pointer to the ressource unit the tree belongs to.
        public TreeSpecies Species { get; private set; } // pointer to the tree species of the tree.
        public LightStamp[] Stamp { get; private set; }
        public int[] StandID { get; private set; }
        public int[] Tag { get; private set; } // (usually) numerical unique ID of the tree

        // biomass properties
        public float[] CoarseRootMass { get; private set; } // mass (kg) of coarse roots
        public float[] FineRootMass { get; private set; } // mass (kg) of fine roots
        public float[] FoliageMass { get; private set; } // mass (kg) of foliage
        public float[] StemMass { get; private set; } // mass (kg) of stem
        public float[] StressIndex { get; private set; } // the scalar stress rating (0..1), used for mortality

        public Trees(Landscape landscape, ResourceUnit resourceUnit, TreeSpecies species)
        {
            this.heightGrid = landscape.HeightGrid;
            this.lightGrid = landscape.LightGrid;
            this.flags = new TreeFlags[Constant.Simd128x4.Width];

            this.Count = 0;

            this.Age = new int[Constant.Simd128x4.Width];
            this.CoarseRootMass = new float[Constant.Simd128x4.Width];
            this.Dbh = new float[Constant.Simd128x4.Width];
            this.DbhDelta = new float[Constant.Simd128x4.Width];
            this.FineRootMass = new float[Constant.Simd128x4.Width];
            this.FoliageMass = new float[Constant.Simd128x4.Width];
            this.Height = new float[Constant.Simd128x4.Width];
            this.LeafArea = new float[Constant.Simd128x4.Width];
            this.LightCellIndexXY = new Point[Constant.Simd128x4.Width];
            this.LightResourceIndex = new float[Constant.Simd128x4.Width];
            this.LightResponse = new float[Constant.Simd128x4.Width];
            this.NppReserve = new float[Constant.Simd128x4.Width];
            this.Opacity = new float[Constant.Simd128x4.Width];
            this.ResourceUnit = resourceUnit;
            this.Species = species;
            this.Stamp = new LightStamp[Constant.Simd128x4.Width];
            this.StandID = new int[Constant.Simd128x4.Width];
            this.StemMass = new float[Constant.Simd128x4.Width];
            this.StressIndex = new float[Constant.Simd128x4.Width];
            this.Tag = new int[Constant.Simd128x4.Width];
        }

        public int Capacity 
        { 
            get { return this.Height.Length; }
        }
        
        /// @property position The tree does not store the floating point coordinates but only the index of pixel on the LIF grid
        /// TODO: store input coordinates of tree
        public PointF GetCellCenterPoint(int treeIndex) 
        { 
            Debug.Assert(this.lightGrid != null);
            return this.lightGrid.GetCellProjectCentroid(this.LightCellIndexXY[treeIndex]); 
        }

        public void Resize(int newSize)
        {
            if ((newSize < this.Count) || (newSize % Constant.Simd128x4.Width != 0)) // enforces positive size (unless a bug allows Count to become negative)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), "New size of " + newSize + " is smaller than the current number of live trees (" + this.Count + ") or is not an integer multiple of SIMD width.");
            }

            this.flags = this.flags.Resize(newSize);

            this.Age = this.Age.Resize(newSize);
            this.CoarseRootMass = this.CoarseRootMass.Resize(newSize);
            this.Dbh = this.Dbh.Resize(newSize);
            this.DbhDelta = this.DbhDelta.Resize(newSize);
            this.FineRootMass = this.FineRootMass.Resize(newSize);
            this.FoliageMass = this.FoliageMass.Resize(newSize);
            this.Height = this.Height.Resize(newSize); // updates this.Capacity
            this.LeafArea = this.LeafArea.Resize(newSize);
            this.LightCellIndexXY = this.LightCellIndexXY.Resize(newSize);
            this.LightResourceIndex = this.LightResourceIndex.Resize(newSize);
            this.LightResponse = this.LightResponse.Resize(newSize);
            this.NppReserve = this.NppReserve.Resize(newSize);
            this.Opacity = this.Opacity.Resize(newSize);
            // this.RU is scalar
            // this.Species is scalar
            this.Stamp = this.Stamp.Resize(newSize);
            this.StandID = this.StandID.Resize(newSize);
            this.StemMass = this.StemMass.Resize(newSize);
            this.StressIndex = this.StressIndex.Resize(newSize);
            this.Tag = this.Tag.Resize(newSize);
        }

        public void SetLightCellIndex(int treeIndex, PointF pos) 
        { 
            Debug.Assert(this.lightGrid != null); 
            this.LightCellIndexXY[treeIndex] = this.lightGrid.GetCellXYIndex(pos); 
        }

        // private bool IsDebugging() { return this.flags[treeIndex].HasFlag(TreeFlags.Debugging); }
        public void SetDebugging(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.Debugging, true); }

        // death reasons
        public bool IsCutDown(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadCutAndDrop); }
        public bool IsDead(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Dead); } // returns true if the tree is already dead.
        public bool IsDeadBarkBeetle(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromBarkBeetles); }
        public bool IsDeadFire(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromFire); }
        public bool IsDeadWind(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.DeadFromWind); }
        public bool IsHarvested(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.Harvested); }

        public void SetDeathReasonBarkBeetle(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.DeadFromBarkBeetles, true); }
        public void SetDeathReasonCutAndDrop(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.DeadCutAndDrop, true); }
        public void SetDeathReasonFire(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.DeadFromFire, true); }
        public void SetDeathReasonHarvested(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.Harvested, true); }
        public void SetDeathReasonWind(int treeIndex) { this.SetOrClearFlag(treeIndex, TreeFlags.DeadFromWind, true); }

        // management flags (used by ABE management system)
        public bool IsMarkedForHarvest(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.MarkedForHarvest); }
        public bool IsMarkedForCut(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.MarkedForCut); }
        public bool IsMarkedAsCropTree(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.CropTree); }
        public bool IsMarkedAsCropCompetitor(int treeIndex) { return this.flags[treeIndex].HasFlag(TreeFlags.CropCompetitor); }

        public void SetOrClearCropCompetitor(int treeIndex, bool isCompetitor) { this.SetOrClearFlag(treeIndex, TreeFlags.CropCompetitor, isCompetitor); }
        public void SetOrClearCropTree(int treeIndex, bool isCropTree) { this.SetOrClearFlag(treeIndex, TreeFlags.CropTree, isCropTree); }
        public void SetOrClearForCut(int treeIndex, bool isForCut) { this.SetOrClearFlag(treeIndex, TreeFlags.MarkedForCut, isForCut); }
        public void SetOrClearForHarvest(int treeIndex, bool isForHarvest) { this.SetOrClearFlag(treeIndex, TreeFlags.MarkedForHarvest, isForHarvest); }

        /// set a Flag 'flag' to the value 'value'.
        private void SetOrClearFlag(int treeIndex, TreeFlags flag, bool value)
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

        public void Add(float dbhInCm, float heightInM, int ageInYears, Point lightCellIndexXY, float lightStampBeerLambertK)
        {
            if (ageInYears < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ageInYears), "Tree age is zero or negative rather than being a positive number of years.");
            }
            else if (ageInYears == 0)
            {
                // if it's not specified, estimate the tree's from its height
                ageInYears = this.Species.EstimateAgeFromHeight(heightInM);
            }
            if (dbhInCm <= 0.0F || dbhInCm > 500.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(dbhInCm), "Attempt to add tree of species " + this.Species.ID + " with invalid diameter of " + dbhInCm + " cm to resource unit " + this.ResourceUnit.ID + ".");
            }
            if (heightInM <= 0.0F || heightInM > 150.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(heightInM), "Attempt to add tree of species " + this.Species.ID + " with invalid height of " + heightInM + " m to resource unit " + this.ResourceUnit.ID + ".");
            }
            if ((lightCellIndexXY.X < 0) || (lightCellIndexXY.Y < 0))
            {
                // TODO: check light cell is on resource unit or at least sanity check an upper bound
                throw new ArgumentOutOfRangeException(nameof(lightCellIndexXY));
            }

            if (this.Count == this.Capacity)
            {
                this.Resize(2 * this.Capacity); // for now, default to same size doubling as List<T>
            }

            this.flags[this.Count] = TreeFlags.None;

            this.Age[this.Count] = ageInYears;
            this.CoarseRootMass[this.Count] = this.Species.GetBiomassCoarseRoot(dbhInCm);
            this.Dbh[this.Count] = dbhInCm;
            this.DbhDelta[this.Count] = 0.1F; // initial value: used in growth() to estimate diameter increment

            float foliageBiomass = this.Species.GetBiomassFoliage(dbhInCm);
            this.FineRootMass[this.Count] = this.Species.FinerootFoliageRatio * foliageBiomass;
            this.FoliageMass[this.Count] = foliageBiomass;
            this.Height[this.Count] = heightInM;

            float leafAreaInM2 = this.Species.SpecificLeafArea * foliageBiomass; // leafArea [m²] = specificLeafArea [m²/kg] * leafMass [kg]
            this.LeafArea[this.Count] = leafAreaInM2;

            this.LightCellIndexXY[this.Count] = lightCellIndexXY;
            this.LightResourceIndex[this.Count] = 0.0F;
            this.LightResponse[this.Count] = 0.0F;

            float nppReserve = (1.0F + this.Species.FinerootFoliageRatio) * foliageBiomass; // initial value
            this.NppReserve[this.Count] = nppReserve;

            LightStamp stamp = this.Species.GetStamp(dbhInCm, heightInM);
            float opacity = 1.0F - MathF.Exp(-lightStampBeerLambertK * leafAreaInM2 / stamp.CrownAreaInM2);
            this.Opacity[this.Count] = opacity;
            
            this.Stamp[this.Count] = stamp;
            this.StandID[this.Count] = Constant.DefaultStandID; // TODO: how not to add all regeneration to the default stand?
            this.StemMass[this.Count] = this.Species.GetBiomassStem(dbhInCm);
            this.StressIndex[this.Count] = 0.0F;

            // best effort default: doesn't guarantee unique tree ID when tree lists are combined with regeneration or if tags are
            // partially specified in individual tree input but does at least provide unique IDs during initial resource unit
            // population
            this.Tag[this.Count] = this.Count;

            ++this.Count;
        }

        public void Add(Trees other, int otherTreeIndex)
        {
            if (this.Count == this.Capacity)
            {
                this.Resize(2 * this.Capacity); // for now, default to same size doubling as List<T>
            }

            this.flags[this.Count] = other.flags[otherTreeIndex];

            this.Age[this.Count] = other.Age[otherTreeIndex];
            this.CoarseRootMass[this.Count] = other.CoarseRootMass[otherTreeIndex];
            this.Dbh[this.Count] = other.Dbh[otherTreeIndex];
            this.DbhDelta[this.Count] = other.DbhDelta[otherTreeIndex];
            this.FineRootMass[this.Count] = other.FineRootMass[otherTreeIndex];
            this.FoliageMass[this.Count] = other.FoliageMass[otherTreeIndex];
            this.Height[this.Count] = other.Height[otherTreeIndex];
            this.LeafArea[this.Count] = other.LeafArea[otherTreeIndex];
            this.LightCellIndexXY[this.Count] = other.LightCellIndexXY[otherTreeIndex];
            this.LightResourceIndex[this.Count] = other.LightResourceIndex[otherTreeIndex];
            this.LightResponse[this.Count] = other.LightResponse[otherTreeIndex];
            this.NppReserve[this.Count] = other.NppReserve[otherTreeIndex];
            this.Opacity[this.Count] = other.Opacity[otherTreeIndex];
            this.Stamp[this.Count] = other.Stamp[otherTreeIndex];
            this.StandID[this.Count] = other.StandID[otherTreeIndex];
            this.StemMass[this.Count] = other.StemMass[otherTreeIndex];
            this.StressIndex[this.Count] = other.StressIndex[otherTreeIndex];
            this.Tag[this.Count] = other.Tag[otherTreeIndex];

            ++this.Count;
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
            this.LightCellIndexXY[destinationIndex] = this.LightCellIndexXY[sourceIndex];
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
            Point stampOrigin = this.LightCellIndexXY[treeIndex];
            stampOrigin.X -= stamp.CenterCellIndex;
            stampOrigin.Y -= stamp.CenterCellIndex;
            int stampSize = stamp.GetSizeInLightCells();
            if ((this.lightGrid.Contains(stampOrigin) == false) || (this.lightGrid.Contains(new Point(stampOrigin.X + stampSize, stampOrigin.Y + stampSize)) == false))
            {
                throw new NotSupportedException("Light grid's buffer width is not large enough to stamp tree.");
            }

            for (int lightY = stampOrigin.Y, stampY = 0; stampY < stampSize; ++lightY, ++stampY)
            {
                int lightIndex = this.lightGrid.IndexXYToIndex(stampOrigin.X, lightY);
                for (int lightX = stampOrigin.X, stampX = 0; stampX < stampSize; ++lightX, ++lightIndex, ++stampX)
                {
                    // http://iland-model.org/competition+for+light
                    float iXYJ = stamp[stampX, stampY]; // tree's light stamp value
                    if (iXYJ != 0.0F) // zero = no tree shading => LIF intensity = 1 => no change in light grid
                    {
                        float dominantHeight = this.heightGrid[lightX, lightY, Constant.LightCellsPerHeightCellWidth].MaximumVegetationHeightInM; // height of z*u,v on the current position
                        float zStarXYJ = MathF.Max(this.Height[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY), 0.0F); // distance to center = height (45 degree line)
                        float zStarMin = (zStarXYJ >= dominantHeight) ? 1.0F : zStarXYJ / dominantHeight; // tree influence height
                        float iStarXYJ = 1.0F - this.Opacity[treeIndex] * iXYJ * zStarMin; // this tree's Beer-Lambert contribution to shading of light grid cell
                        iStarXYJ = MathF.Max(iStarXYJ, 0.02F); // limit minimum value

                        this.lightGrid[lightIndex] *= iStarXYJ; // compound LIF intensity, Eq. 4
                    }
                }
            }

            //++Tree.StampApplications;
        }

        /// helper function for gluing model area edges together to form a torus
        /// index: index at light grid
        /// count: number of pixels that are the model area (e.g. 100 m area with 2 m light pixel = 50)
        /// buffer: size of buffer around simulation area (in light pixels)
        private static int GetTorusIndex(int index, int count, int buffer, int ruOffset)
        {
            return buffer + ruOffset + (index - buffer + count) % count;
        }

        // Apply LIPs. This "Torus" functions wraps the influence at the edges of a 1 ha simulation area.
        // TODO: is this really restricted to a single resource unit?
        public void ApplyLightIntensityPatternTorus(int treeIndex, int lightBufferTranslationInCells)
        {
            Debug.Assert(this.lightGrid != null && this.Stamp != null && this.ResourceUnit != null);
            Point treePositionWithinRU = new((this.LightCellIndexXY[treeIndex].X - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells,
                                             (this.LightCellIndexXY[treeIndex].Y - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells); // offset within the ha
            Point ruOffset = new(this.LightCellIndexXY[treeIndex].X - treePositionWithinRU.X, this.LightCellIndexXY[treeIndex].Y - treePositionWithinRU.Y); // offset of the corner of the resource index

            LightStamp stamp = this.Stamp[treeIndex]!;
            Point stampOrigin = new(treePositionWithinRU.X - stamp.CenterCellIndex, treePositionWithinRU.Y - stamp.CenterCellIndex);

            int stampSize = stamp.GetSizeInLightCells();
            if (this.lightGrid.Contains(stampOrigin) == false || this.lightGrid.Contains(stampOrigin.X + stampSize, stampOrigin.Y + stampSize) == false)
            {
                // TODO: in this case we should use another algorithm!!! necessary????
                throw new NotSupportedException("Light grid's buffer width is not large enough to stamp tree.");
            }

            for (int stampY = 0; stampY < stampSize; ++stampY)
            {
                int lightY = stampOrigin.Y + stampY;
                int torusY = Trees.GetTorusIndex(lightY, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffset.Y); // 50 cells per 100m
                for (int stampX = 0; stampX < stampSize; ++stampX)
                {
                    // suppose there is no stamping outside
                    int lightX = stampOrigin.X + stampX;
                    int torusX = Trees.GetTorusIndex(lightX, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffset.X);

                    float dominantHeight = this.heightGrid[torusX, torusY, Constant.LightCellsPerHeightCellWidth].MaximumVegetationHeightInM; // height of Z* on the current position
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

        // calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
        public void CalculateDominantHeightField(int treeIndex)
        {
            Point treeLightCellIndexXY = this.LightCellIndexXY[treeIndex];
            Point treeHeightCellIndexXY = new(treeLightCellIndexXY.X / Constant.LightCellsPerHeightCellWidth, treeLightCellIndexXY.Y / Constant.LightCellsPerHeightCellWidth); // position of tree on height grid

            // count trees that are on height-grid cells (used for stockable area)
            this.heightGrid[treeHeightCellIndexXY].AddTree(this.Height[treeIndex]);

            LightStamp reader = this.Stamp[treeIndex]!.ReaderStamp!;
            int center = reader.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int indexEastWest = this.LightCellIndexXY[treeIndex].X % Constant.LightCellsPerHeightCellWidth; // 4: very west, 0 east edge
            int indexNorthSouth = this.LightCellIndexXY[treeIndex].Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
            if (indexEastWest - center < 0)
            { // east
                this.heightGrid[treeHeightCellIndexXY.X - 1, treeHeightCellIndexXY.Y].MaximumVegetationHeightInM = MathF.Max(this.heightGrid[treeHeightCellIndexXY.X - 1, treeHeightCellIndexXY.Y].MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexEastWest + center >= Constant.LightCellsPerHeightCellWidth)
            {  // west
                this.heightGrid[treeHeightCellIndexXY.X + 1, treeHeightCellIndexXY.Y].MaximumVegetationHeightInM = MathF.Max(this.heightGrid[treeHeightCellIndexXY.X + 1, treeHeightCellIndexXY.Y].MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexNorthSouth - center < 0)
            {  // south
                this.heightGrid[treeHeightCellIndexXY.X, treeHeightCellIndexXY.Y - 1].MaximumVegetationHeightInM = MathF.Max(this.heightGrid[treeHeightCellIndexXY.X, treeHeightCellIndexXY.Y - 1].MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexNorthSouth + center >= Constant.LightCellsPerHeightCellWidth)
            {  // north
                this.heightGrid[treeHeightCellIndexXY.X, treeHeightCellIndexXY.Y + 1].MaximumVegetationHeightInM = MathF.Max(this.heightGrid[treeHeightCellIndexXY.X, treeHeightCellIndexXY.Y + 1].MaximumVegetationHeightInM, this.Height[treeIndex]);
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

        public void CalculateDominantHeightFieldTorus(int treeIndex, int heightBufferTranslationInCells)
        {
            // height of Z*
            Point heightCellIndexXY = new(this.LightCellIndexXY[treeIndex].X / Constant.LightCellsPerHeightCellWidth, this.LightCellIndexXY[treeIndex].Y / Constant.LightCellsPerHeightCellWidth); // pos of tree on height grid
            heightCellIndexXY.X = (heightCellIndexXY.X - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells; // 10: 10 x 10m pixeln in 100m
            heightCellIndexXY.Y = (heightCellIndexXY.Y - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells;

            // torus coordinates: ruOffset = coords of lower left corner of 1 ha patch
            Point ruOffset = new(this.LightCellIndexXY[treeIndex].X / Constant.LightCellsPerHeightCellWidth - heightCellIndexXY.X, this.LightCellIndexXY[treeIndex].Y / Constant.LightCellsPerHeightCellWidth - heightCellIndexXY.Y);

            // count trees that are on height-grid cells (used for stockable area)
            int torusX = Trees.GetTorusIndex(heightCellIndexXY.X, 10, heightBufferTranslationInCells, ruOffset.X);
            int torusY = Trees.GetTorusIndex(heightCellIndexXY.Y, 10, heightBufferTranslationInCells, ruOffset.Y);
            HeightCell heightCell = this.heightGrid[torusX, torusY];
            heightCell.AddTree(this.Height[treeIndex]);

            LightStamp reader = this.Stamp[treeIndex]!.ReaderStamp!;
            int readerCenter = reader.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
            int indexEastWest = this.LightCellIndexXY[treeIndex].X % Constant.LightCellsPerHeightCellWidth; // 4: very west, 0 east edge
            int indexNorthSouth = this.LightCellIndexXY[treeIndex].Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
            if (indexEastWest - readerCenter < 0)
            { // east
                heightCell = this.heightGrid[Trees.GetTorusIndex(heightCellIndexXY.X - 1, 10, heightBufferTranslationInCells, ruOffset.X), 
                                             Trees.GetTorusIndex(heightCellIndexXY.Y, 10, heightBufferTranslationInCells, ruOffset.Y)];
                heightCell.MaximumVegetationHeightInM = MathF.Max(heightCell.MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightCellWidth)
            {  // west
                heightCell = this.heightGrid[Trees.GetTorusIndex(heightCellIndexXY.X + 1, 10, heightBufferTranslationInCells, ruOffset.X),
                                             Trees.GetTorusIndex(heightCellIndexXY.Y, 10, heightBufferTranslationInCells, ruOffset.Y)];
                heightCell.MaximumVegetationHeightInM = MathF.Max(heightCell.MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexNorthSouth - readerCenter < 0)
            {  // south
                heightCell = this.heightGrid[Trees.GetTorusIndex(heightCellIndexXY.X, 10, heightBufferTranslationInCells, ruOffset.X),
                                             Trees.GetTorusIndex(heightCellIndexXY.Y - 1, 10, heightBufferTranslationInCells, ruOffset.Y)];
                heightCell.MaximumVegetationHeightInM = MathF.Max(heightCell.MaximumVegetationHeightInM, this.Height[treeIndex]);
            }
            if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightCellWidth)
            {  // north
                heightCell = this.heightGrid[Trees.GetTorusIndex(heightCellIndexXY.X, 10, heightBufferTranslationInCells, ruOffset.X),
                                             Trees.GetTorusIndex(heightCellIndexXY.Y + 1, 10, heightBufferTranslationInCells, ruOffset.Y)];
                heightCell.MaximumVegetationHeightInM = MathF.Max(heightCell.MaximumVegetationHeightInM, this.Height[treeIndex]);
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

        public void DropLastNTrees(int n)
        {
            if ((n < 1) || (n > this.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }

            this.Count -= n;
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
            return this.Stamp[treeIndex]!.CrownRadiusInM;
        }

        public float GetBranchBiomass(int treeIndex)
        {
            return this.Species.GetBiomassBranch(this.Dbh[treeIndex]);
        }

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland-model.org/competition+for+light for details.
          */
        public void ReadLightInfluenceField(int treeIndex)
        {
            LightStamp reader = this.Stamp[treeIndex]!.ReaderStamp!;
            Point lightCellPosition = this.LightCellIndexXY[treeIndex];
            float outsideAreaFactor = 0.1F;

            int readerOffset = reader.CenterCellIndex;
            int writerOffset = this.Stamp[treeIndex]!.CenterCellIndex;
            int writerReaderOffset = writerOffset - readerOffset; // offset on the *stamp* to the crown-cells

            lightCellPosition.X -= readerOffset;
            lightCellPosition.Y -= readerOffset;

            float sum = 0.0F;
            int readerSize = reader.GetSizeInLightCells();
            int rx = lightCellPosition.X;
            int ry = lightCellPosition.Y;
            for (int y = 0; y < readerSize; ++y, ++ry)
            {
                float lightValue = this.lightGrid[rx, ry];
                for (int x = 0; x < readerSize; ++x)
                {
                    HeightCell heightCell = this.heightGrid[rx + x, ry, Constant.LightCellsPerHeightCellWidth]; // the height grid value, ry: gets ++ed in outer loop, rx not
                    float local_dom = heightCell.MaximumVegetationHeightInM;
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
                    // Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                    // if (value>0.)
                    sum += value * reader[x, y];
                }
            }
            this.LightResourceIndex[treeIndex] = sum;
            // LRI correction...
            float relativeHeight = this.Height[treeIndex] / this.heightGrid[this.LightCellIndexXY[treeIndex].X, this.LightCellIndexXY[treeIndex].Y, Constant.LightCellsPerHeightCellWidth].MaximumVegetationHeightInM;
            if (relativeHeight < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(this.LightResourceIndex[treeIndex], relativeHeight);
            }

            if (this.LightResourceIndex[treeIndex] > 1.0F)
            {
                this.LightResourceIndex[treeIndex] = 1.0F;
            }
            // Finally, add LRI of this Tree to the ResourceUnit!
            this.ResourceUnit.Trees.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);

            // Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightInfluenceFieldTorus(int treeIndex, int lightBufferWidthInCells)
        {
            LightStamp stampReader = this.Stamp[treeIndex]!.ReaderStamp!;
            Point treePositionInRU = new((this.LightCellIndexXY[treeIndex].X - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells,
                                         (this.LightCellIndexXY[treeIndex].Y - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells); // offset within the ha
            Point ruOffset = new(this.LightCellIndexXY[treeIndex].X - treePositionInRU.X, this.LightCellIndexXY[treeIndex].Y - treePositionInRU.Y); // offset of the corner of the resource index

            float lightIndex = 0.0F;
            int readerSize = stampReader.GetSizeInLightCells();
            int readerOriginX = treePositionInRU.X - stampReader.CenterCellIndex;
            int readerOriginY = treePositionInRU.Y - stampReader.CenterCellIndex;
            int writerReaderOffset = this.Stamp[treeIndex]!.CenterCellIndex - stampReader.CenterCellIndex; // offset on the *stamp* to the crown (light?) cells
            for (int readerY = 0; readerY < readerSize; ++readerY)
            {
                int yTorus = Trees.GetTorusIndex(readerOriginY + readerY, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffset.Y);
                for (int readerX = 0; readerX < readerSize; ++readerX)
                {
                    // see http://iland-model.org/competition+for+light 
                    int xTorus = Trees.GetTorusIndex(readerOriginX + readerX, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffset.X);
                    float dominantHeightTorus = this.heightGrid[xTorus, yTorus, Constant.LightCellsPerHeightCellWidth].MaximumVegetationHeightInM; // ry: gets ++ed in outer loop, rx not
                    float influenceZ = MathF.Max(this.Height[treeIndex] - stampReader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                    float influenceZstar = (influenceZ >= dominantHeightTorus) ? 1.0F : influenceZ / dominantHeightTorus;

                    // TODO: why a nonzero floor as opposed to skipping division?
                    float focalIntensity = MathF.Max(1.0F - this.Stamp[treeIndex]![readerX, readerY, writerReaderOffset] * this.Opacity[treeIndex] * influenceZstar, 0.02F);
                    // C++ code is actually Tree.LightGrid[Tree.LightGrid.IndexOf(xTorus, yTorus) + 1], which appears to be an off by
                    // one error corrected by Qt build implementing precdence in *ptr++ incorrectly.
                    float cellIntensity = this.lightGrid[xTorus, yTorus];
                    float cellIndex = cellIntensity / focalIntensity; // remove impact of focal tree

                    // debug for one tree in HJA
                    // if (id()==178020)
                    //     Debug.WriteLine(x + y + xt + yt + *grid_value + local_dom + own_value + value + (*reader)(x,y);
                    // if (_isnan(value))
                    //     Debug.WriteLine("isnan" + id();
                    // if (cellIndex * reader[readerX, readerY] > 1.0)
                    // {
                    //     Debug.WriteLine("LIFTorus: value > 1.0.");
                    // }
                    lightIndex += cellIndex * stampReader[readerX, readerY];
                    //} // isIndexValid
                }
            }
            this.LightResourceIndex[treeIndex] = lightIndex;

            // LRI correction...
            float hrel = this.Height[treeIndex] / this.heightGrid[this.LightCellIndexXY[treeIndex].X, this.LightCellIndexXY[treeIndex].Y, Constant.LightCellsPerHeightCellWidth].MaximumVegetationHeightInM;
            if (hrel < 1.0F)
            {
                this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(this.LightResourceIndex[treeIndex], hrel);
            }

            if (Single.IsNaN(this.LightResourceIndex[treeIndex]))
            {
                throw new InvalidOperationException("Light resource index unexpectedly NaN.");
                // Debug.WriteLine("LRI invalid (nan) " + ID);
                // this.LightResourceIndex[treeIndex] = 0.0F;
                // Debug.WriteLine(reader.dump();
            }

            Debug.Assert(this.LightResourceIndex[treeIndex] >= 0.0F && this.LightResourceIndex[treeIndex] < 50.0F); // sanity upper bound
            if (this.LightResourceIndex[treeIndex] > 1.0F)
            {
                this.LightResourceIndex[treeIndex] = 1.0F; // TODO: why clamp?
            }
            // Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;

            // Finally, add LRI of this Tree to the ResourceUnit!
            this.ResourceUnit.Trees.AddWeightedLeafArea(this.LeafArea[treeIndex], this.LightResourceIndex[treeIndex]);
        }

        //public static void ResetStatistics()
        //{
        //    Tree.StampApplications = 0;
        //    Tree.TreesCreated = 0;
        //}

        //#ifdef ALT_TREE_MORTALITY
        //void mortalityParams(float dbh_inc_threshold, int stress_years, float stress_mort_prob)
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
            // http://iland-model.org/individual+tree+light+availability
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.ResourceUnit.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F); // Eq. (3)
            this.LightResponse[treeIndex] = this.Species.GetLightResponse(lri); // Eq. (4)
            this.ResourceUnit.Trees.AddLightResponse(this.LeafArea[treeIndex], this.LightResponse[treeIndex]);
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
          - Production of GPP/NPP   @sa http://iland-model.org/primary+production http://iland-model.org/individual+tree+light+availability
          - Partitioning of NPP to biomass compartments of the tree @sa http://iland-model.org/allocation
          - Growth of the stem http://iland-model.org/stem+growth (???)
          Further activties: * the age of the tree is increased
                             * the mortality sub routine is executed
                             * seeds are produced */
        public void CalculateAnnualGrowth(Model model)
        {
            // get the GPP for a "unit area" of the tree species
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            TreeGrowthData treeGrowthData = new();
            for (int treeIndex = 0; treeIndex < this.Count; ++treeIndex)
            {
                // increase age
                ++this.Age[treeIndex];

                // apply aging according to the state of the individal
                float agingFactor = this.Species.GetAgingFactor(this.Height[treeIndex], this.Age[treeIndex]);
                this.ResourceUnit.Trees.AddAging(this.LeafArea[treeIndex], agingFactor);

                // step 1: get "interception area" of the tree individual [m2]
                // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
                float effectiveTreeArea = this.ResourceUnit.Trees.GetPhotosyntheticallyActiveArea(this.LeafArea[treeIndex], this.LightResponse[treeIndex]); // light response in [0...1] depending on suppression

                // step 2: calculate GPP of the tree based
                // (2) GPP (without aging-effect) in kg Biomass / year
                float treeGppBeforeAging = ruSpecies.TreeGrowth.AnnualGpp * effectiveTreeArea;
                float treeGpp = treeGppBeforeAging * agingFactor;
                Debug.Assert(treeGpp >= 0.0F);
                treeGrowthData.NppAboveground = 0.0F;
                treeGrowthData.NppStem = 0.0F;
                treeGrowthData.NppTotal = model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier * treeGpp; // respiration loss (0.47), cf. Waring et al 1998.
                treeGrowthData.StressIndex = 0.0F;

                //#ifdef DEBUG
                //if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.TreeNpp) && IsDebugging())
                //{
                //    List<object> outList = model.GlobalSettings.DebugList(ID, DebugOutputs.TreeNpp);
                //    DumpList(outList); // add tree headers
                //    outList.AddRange(new object[] { this.LightResourceIndex[treeIndex] * RU.LriModifier, LightResponse, effective_area, raw_gpp, gpp, d.NppTotal, agingFactor });
                //}
                //); 
                //#endif
                if (model.Project.Model.Settings.GrowthEnabled && (treeGrowthData.NppTotal > 0.0F))
                {
                    this.PartitionBiomass(treeGrowthData, model, treeIndex); // split npp to compartments and grow (diameter, height)
                }

                // mortality
                //#ifdef ALT_TREE_MORTALITY
                // alternative variant of tree mortality (note: mStrssIndex used otherwise)
                // altMortality(d);
                //#else
                if (model.Project.Model.Settings.MortalityEnabled)
                {
                    this.CheckIntrinsicAndStressMortality(model, treeIndex, treeGrowthData);
                }
                this.StressIndex[treeIndex] = treeGrowthData.StressIndex;
                //#endif

                if (this.IsDead(treeIndex) == false)
                {
                    float abovegroundNpp = treeGrowthData.NppAboveground;
                    float totalNpp = treeGrowthData.NppTotal;
                    ruSpecies.StatisticsLive.Add(this, treeIndex, totalNpp, abovegroundNpp);

                    int standID = this.StandID[treeIndex];
                    this.ResourceUnit.Trees.TreeStatisticsByStandID[standID].Add(this, treeIndex, totalNpp, abovegroundNpp);
                }

                // regeneration
                this.Species.DisperseSeeds(model.RandomGenerator, this, treeIndex);
            }
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on 
            Duursma RA, Marshall JD, Robinson AP, Pangle RE. 2007. Description and test of a simple process-based model of forest growth
              for mixed-species stands. Ecological Modelling 203(3–4):297-311. https://doi.org/10.1016/j.ecolmodel.2006.11.032
          @sa http://iland-model.org/allocation */
        private void PartitionBiomass(TreeGrowthData growthData, Model model, int treeIndex)
        {
            // available resources
            float nppAvailable = growthData.NppTotal + this.NppReserve[treeIndex];
            float foliageBiomass = this.Species.GetBiomassFoliage(this.Dbh[treeIndex]);
            float reserveSize = foliageBiomass * (1.0F + this.Species.FinerootFoliageRatio);
            float reserveAllocation = MathF.Min(reserveSize, (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMass[treeIndex]); // not always try to refill reserve 100%

            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            float rootFraction = ruSpecies.TreeGrowth.RootFraction;
            growthData.NppAboveground = growthData.NppTotal * (1.0F - rootFraction); // aboveground: total NPP - fraction to roots
            float woodFoliageRatio = this.Species.GetStemFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

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
            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddTurnoverLitter(this.Species, foliageSenescence, rootSenescence);
            }

            // Roots
            // http://iland-model.org/allocation#belowground_NPP
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
            float maxCoarseRootBiomass = this.Species.GetBiomassCoarseRoot(this.Dbh[treeIndex]);
            this.CoarseRootMass[treeIndex] += rootAllocation;
            if (this.CoarseRootMass[treeIndex] > maxCoarseRootBiomass)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (this.ResourceUnit.Snags != null)
                {
                    this.ResourceUnit.Snags.AddTurnoverWood(this.CoarseRootMass[treeIndex] - maxCoarseRootBiomass, this.Species);
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
            // see also: http://iland-model.org/allocation#reserve_and_allocation_to_stem_growth
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
            if (MathF.Abs(stemResidual) > MathF.Min(1.0F, stemMass))
            {
                // calculate final residual in stem
                float res_final = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.Height[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - ((stemMass + nppStem));
                if (MathF.Abs(res_final) > MathF.Min(1.0F, stemMass))
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

            Debug.Assert((dbhIncrementInM >= 0.0) && (dbhIncrementInM <= 0.1), String.Format("Diameter increment out of range: HD {0}, factor_diameter {1}, stem_residual {2}, delta_d_estimate {3}, d_increment {4}, final residual {5} kg.",
                                                                                             hdRatioNewGrowth, 
                                                                                             factorDiameter, 
                                                                                             stemResidual, 
                                                                                             deltaDbhEstimate, 
                                                                                             dbhIncrementInM, 
                                                                                             massFactor * (this.Dbh[treeIndex] + dbhIncrementInM) * (this.Dbh[treeIndex] + dbhIncrementInM) * (this.Height[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - stemMass + nppStem));

            //DBGMODE(
            // do not calculate res_final twice if already done
            // Debug.WriteLineIf((res_final == 0.0 ? MathF.Abs(mass_factor * (d_m + d_increment) * (d_m + d_increment) * (this.height[treeIndex] + d_increment * hd_growth) - ((stem_mass + net_stem_npp))) : res_final) > 1, Dump(),
            //     "grow_diameter: final residual stem estimate > 1kg");
            // Debug.WriteLineIf(d_increment > 10.0 || d_increment * hd_growth > 10.0, String.Format("d-increment {0} h-increment {1} ", d_increment, d_increment * hd_growth / 100.0) + Dump(),
            //     "grow_diameter growth out of bound");

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
            float treeK = model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-treeK * this.LeafArea[treeIndex] / this.Stamp[treeIndex]!.CrownAreaInM2);
        }

        /// return the HD ratio of this year's increment based on the light status.
        private float GetRelativeHeightGrowth(int treeIndex)
        {
            this.Species.GetHeightDiameterRatioLimits(this.Dbh[treeIndex], out float hdRatioLow, out float hdRatioHigh);
            Debug.Assert(hdRatioLow < hdRatioHigh, "HD low higher than HD high.");
            Debug.Assert((hdRatioLow > 10.0F) && (hdRatioHigh < 250.0F), "HD ratio out of range. Low: " + hdRatioLow + ", high: " + hdRatioHigh);

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.ResourceUnit.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F);
            float hdRatio = hdRatioHigh - (hdRatioHigh - hdRatioLow) * lri;
            return hdRatio;
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void Remove(Model model, int treeIndex, float removeFoliage = 0.0F, float removeBranch = 0.0F, float removeStem = 0.0F)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.SetDeathReasonHarvested(treeIndex);
            this.ResourceUnit.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsManagement.Add(this, treeIndex);
            this.OnTreeRemoved(model, treeIndex, this.IsCutDown(treeIndex) ?  MortalityCause.CutDown : MortalityCause.Harvest);

            this.ResourceUnit.AddSprout(model, this, treeIndex);
            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddHarvest(this, treeIndex, removeStem, removeBranch, removeFoliage);
            }
        }

        /// remove the tree due to an special event (disturbance)
        /// this is +- the same as die().
        // TODO: when would branch to snag fraction be greater than zero?
        public void RemoveDisturbance(Model model, int treeIndex, float stemToSoilFraction, float stemToSnagFraction, float branchToSoilFraction, float branchToSnagFraction, float foliageToSoilFraction)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.ResourceUnit.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsSnag.Add(this, treeIndex);
            this.OnTreeRemoved(model, treeIndex, MortalityCause.Disturbance);

            this.ResourceUnit.AddSprout(model, this, treeIndex);
            if (this.ResourceUnit.Snags != null)
            {
                if (this.IsHarvested(treeIndex))
                { // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    this.ResourceUnit.Snags.AddHarvest(this, treeIndex, 1.0F, 0.0F, 0.0F);
                }
                else
                {
                    this.ResourceUnit.Snags.AddDisturbance(this, treeIndex, stemToSnagFraction, stemToSoilFraction, branchToSnagFraction, branchToSoilFraction, foliageToSoilFraction);
                }
            }
        }

        private void CheckIntrinsicAndStressMortality(Model model, int treeIndex, TreeGrowthData growthData)
        {
            // death if leaf area is near zero
            if (this.FoliageMass[treeIndex] < 0.00001F)
            {
                this.MarkTreeAsDead(model, treeIndex);
                return;
            }

            float pFixed = this.Species.DeathProbabilityFixed;
            float pStress = this.Species.GetMortalityProbability(growthData.StressIndex);
            float pMortality = pFixed + pStress;
            float random = model.RandomGenerator.GetRandomProbability(); // 0..1
            if (random < pMortality)
            {
                // die...
                this.MarkTreeAsDead(model, treeIndex);
            }
        }

        /** called if a tree dies
            @sa ResourceUnit::cleanTreeList(), remove() */
        public void MarkTreeAsDead(Model model, int treeIndex)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.ResourceUnit.Trees.OnTreeDied();

            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsSnag.Add(this, treeIndex);

            this.OnTreeRemoved(model, treeIndex, MortalityCause.Stress);

            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddMortality(this, treeIndex);
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
            model.Modules.OnTreeDeath(this, reason);

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
            if (model.Output.TreeRemovedSql != null)
            {
                model.Output.TreeRemovedSql.TryAddTree(model, this, treeIndex, reason);
            }

            if (model.Output.LandscapeRemovedSql != null)
            {
                model.Output.LandscapeRemovedSql.AddTree(this, treeIndex, reason);
            }
        }
    }
}
