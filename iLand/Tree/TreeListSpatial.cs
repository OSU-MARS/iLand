using iLand.Extensions;
using iLand.Tool;
using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** A tree is the basic simulation entity of iLand and represents a single tree.
        Trees in iLand are designed to be lightweight, thus the list of stored properties is limited. Basic properties
        are dimensions (dbh, height), biomass pools (stem, leaves, roots), the reserve NPP pool. Additionally, the location and species are stored.
        A Tree has a height of at least 4m; trees below this threshold are covered by the regeneration layer (see Sapling).
        Trees are stored in lists managed at the resource unit level.
      */
    public class TreeListSpatial : TreeList
    {
        private readonly Grid<float> lightGrid;

        private TreeFlags[] flags; // mortality and harvest flags

        public float[] DbhDeltaInCm { get; private set; } // diameter growth [cm]
        public Point[] LightCellIndexXY { get; private set; } // index of the trees position on the basic LIF grid
        public LightStamp[] LightStamp { get; private set; }
        public ResourceUnit ResourceUnit { get; private set; } // pointer to the ressource unit the tree belongs to.

        public TreeListSpatial(Landscape landscape, ResourceUnit resourceUnit, TreeSpecies species)
            : base(species)
        {
            this.lightGrid = landscape.LightGrid;
            this.flags = new TreeFlags[Constant.Simd128.Width32];

            this.DbhDeltaInCm = new float[Constant.Simd128.Width32];
            this.LightCellIndexXY = new Point[Constant.Simd128.Width32];
            this.ResourceUnit = resourceUnit;
            this.LightStamp = new LightStamp[Constant.Simd128.Width32];
        }

        // the tree list does not store the tree's exact GIS coordinates, only the index of pixel on the light grid
        public PointF GetCellCenterPoint(int treeIndex) 
        { 
            Debug.Assert(this.lightGrid != null);
            return this.lightGrid.GetCellProjectCentroid(this.LightCellIndexXY[treeIndex]); 
        }

        public override void Resize(int newSize)
        {
            // does argument checking
            base.Resize(newSize);

            this.flags = this.flags.Resize(newSize);

            this.DbhDeltaInCm = this.DbhDeltaInCm.Resize(newSize);
            this.LightCellIndexXY = this.LightCellIndexXY.Resize(newSize);
            // this.RU is scalar
            this.LightStamp = this.LightStamp.Resize(newSize);
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

        public void Add(float dbhInCm, float heightInM, UInt16 ageInYears, Point lightCellIndexXY, float lightStampBeerLambertK)
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
            this.CoarseRootMassInKg[this.Count] = this.Species.GetBiomassCoarseRoot(dbhInCm);
            this.DbhInCm[this.Count] = dbhInCm;
            this.DbhDeltaInCm[this.Count] = 0.1F; // initial value: used in growth() to estimate diameter increment

            float foliageBiomass = this.Species.GetBiomassFoliage(dbhInCm);
            this.FineRootMassInKg[this.Count] = this.Species.FinerootFoliageRatio * foliageBiomass;
            this.FoliageMassInKg[this.Count] = foliageBiomass;
            this.HeightInM[this.Count] = heightInM;

            float leafAreaInM2 = this.Species.SpecificLeafArea * foliageBiomass; // leafArea [m²] = specificLeafArea [m²/kg] * leafMass [kg]
            this.LeafAreaInM2[this.Count] = leafAreaInM2;

            this.LightCellIndexXY[this.Count] = lightCellIndexXY;
            this.LightResourceIndex[this.Count] = 0.0F;
            this.LightResponse[this.Count] = 0.0F;

            float nppReserve = (1.0F + this.Species.FinerootFoliageRatio) * foliageBiomass; // initial value
            this.NppReserveInKg[this.Count] = nppReserve;

            LightStamp stamp = this.Species.GetStamp(dbhInCm, heightInM);
            float opacity = 1.0F - MathF.Exp(-lightStampBeerLambertK * leafAreaInM2 / stamp.CrownAreaInM2);
            this.Opacity[this.Count] = opacity;
            
            this.LightStamp[this.Count] = stamp;
            this.StandID[this.Count] = Constant.DefaultStandID; // TODO: how not to add all regeneration to the default stand?
            this.StemMassInKg[this.Count] = this.Species.GetBiomassStem(dbhInCm);
            this.StressIndex[this.Count] = 0.0F;

            // best effort default: doesn't guarantee unique tree ID when tree lists are combined with regeneration or if tags are
            // partially specified in individual tree input but does at least provide unique IDs during initial resource unit
            // population
            this.TreeID[this.Count] = this.Count;

            ++this.Count;
        }

        public void Add(TreeListSpatial other, int otherTreeIndex)
        {
            if (this.Count == this.Capacity)
            {
                this.Resize(2 * this.Capacity); // for now, default to same size doubling as List<T>
            }

            this.flags[this.Count] = other.flags[otherTreeIndex];

            this.Age[this.Count] = other.Age[otherTreeIndex];
            this.CoarseRootMassInKg[this.Count] = other.CoarseRootMassInKg[otherTreeIndex];
            this.DbhInCm[this.Count] = other.DbhInCm[otherTreeIndex];
            this.DbhDeltaInCm[this.Count] = other.DbhDeltaInCm[otherTreeIndex];
            this.FineRootMassInKg[this.Count] = other.FineRootMassInKg[otherTreeIndex];
            this.FoliageMassInKg[this.Count] = other.FoliageMassInKg[otherTreeIndex];
            this.HeightInM[this.Count] = other.HeightInM[otherTreeIndex];
            this.LeafAreaInM2[this.Count] = other.LeafAreaInM2[otherTreeIndex];
            this.LightCellIndexXY[this.Count] = other.LightCellIndexXY[otherTreeIndex];
            this.LightResourceIndex[this.Count] = other.LightResourceIndex[otherTreeIndex];
            this.LightResponse[this.Count] = other.LightResponse[otherTreeIndex];
            this.NppReserveInKg[this.Count] = other.NppReserveInKg[otherTreeIndex];
            this.Opacity[this.Count] = other.Opacity[otherTreeIndex];
            this.LightStamp[this.Count] = other.LightStamp[otherTreeIndex];
            this.StandID[this.Count] = other.StandID[otherTreeIndex];
            this.StemMassInKg[this.Count] = other.StemMassInKg[otherTreeIndex];
            this.StressIndex[this.Count] = other.StressIndex[otherTreeIndex];
            this.TreeID[this.Count] = other.TreeID[otherTreeIndex];

            ++this.Count;
        }

        public void Copy(int sourceIndex, int destinationIndex)
        {
            this.flags[destinationIndex] = this.flags[sourceIndex];

            this.Age[destinationIndex] = this.Age[sourceIndex];
            this.CoarseRootMassInKg[destinationIndex] = this.CoarseRootMassInKg[sourceIndex];
            this.DbhInCm[destinationIndex] = this.DbhInCm[sourceIndex];
            this.DbhDeltaInCm[destinationIndex] = this.DbhDeltaInCm[sourceIndex];
            this.FineRootMassInKg[destinationIndex] = this.FineRootMassInKg[sourceIndex];
            this.FoliageMassInKg[destinationIndex] = this.FoliageMassInKg[sourceIndex];
            this.HeightInM[destinationIndex] = this.HeightInM[sourceIndex];
            this.TreeID[destinationIndex] = this.TreeID[sourceIndex];
            this.LeafAreaInM2[destinationIndex] = this.LeafAreaInM2[sourceIndex];
            this.LightCellIndexXY[destinationIndex] = this.LightCellIndexXY[sourceIndex];
            this.LightResourceIndex[destinationIndex] = this.LightResourceIndex[sourceIndex];
            this.LightResponse[destinationIndex] = this.LightResponse[sourceIndex];
            this.NppReserveInKg[destinationIndex] = this.NppReserveInKg[sourceIndex];
            this.Opacity[destinationIndex] = this.Opacity[sourceIndex];
            this.LightStamp[destinationIndex] = this.LightStamp[sourceIndex];
            this.StandID[destinationIndex] = this.StandID[sourceIndex];
            this.StemMassInKg[destinationIndex] = this.StemMassInKg[sourceIndex];
            this.StressIndex[destinationIndex] = this.StressIndex[sourceIndex];
        }

        public void ApplyLightIntensityPattern(Landscape landscape, TreeListSpatial treesOfSpecies)
        {
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                LightStamp stamp = this.LightStamp[treeIndex]!;
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
                            float dominantHeightInM = vegetationHeightGrid[lightX, lightY, Constant.LightCellsPerHeightCellWidth]; // height of z*u,v on the current position
                            float zStarXYJ = MathF.Max(this.HeightInM[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY), 0.0F); // distance to center = height (45 degree line)
                            float zStarMin = (zStarXYJ >= dominantHeightInM) ? 1.0F : zStarXYJ / dominantHeightInM; // tree influence height
                            float iStarXYJ = 1.0F - this.Opacity[treeIndex] * iXYJ * zStarMin; // this tree's Beer-Lambert contribution to shading of light grid cell
                            iStarXYJ = MathF.Max(iStarXYJ, 0.02F); // limit minimum value

                            this.lightGrid[lightIndex] *= iStarXYJ; // compound LIF intensity, Eq. 4
                        }
                    }
                }
            }
        }

        /// helper function for gluing model area edges together to form a torus
        /// index: index at light grid
        /// count: number of pixels that are the model area (e.g. 100 m area with 2 m light pixel = 50)
        /// buffer: size of buffer around simulation area (in light pixels)
        private static int GetTorusIndex(int index, int count, int gridCellsPerResourceUnitWidth, int ruOffset)
        {
            return gridCellsPerResourceUnitWidth + ruOffset + (index - gridCellsPerResourceUnitWidth + count) % count;
        }

        // Apply LIPs. This "Torus" functions wraps the influence at the edges of a 1 ha simulation area.
        // TODO: is this really restricted to a single resource unit?
        public void ApplyLightIntensityPatternTorus(Landscape landscape, TreeListSpatial treesOfSpecies, int lightBufferTranslationInCells)
        {
            Debug.Assert(this.lightGrid != null && this.LightStamp != null && this.ResourceUnit != null);
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                Point treePositionWithinRU = new((this.LightCellIndexXY[treeIndex].X - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells,
                                                 (this.LightCellIndexXY[treeIndex].Y - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells); // offset within the ha
                Point ruOffset = new(this.LightCellIndexXY[treeIndex].X - treePositionWithinRU.X, this.LightCellIndexXY[treeIndex].Y - treePositionWithinRU.Y); // offset of the corner of the resource index

                LightStamp stamp = this.LightStamp[treeIndex]!;
                Point stampOrigin = new(treePositionWithinRU.X - stamp.CenterCellIndex, treePositionWithinRU.Y - stamp.CenterCellIndex);

                int stampSize = stamp.GetSizeInLightCells();
                if ((this.lightGrid.Contains(stampOrigin) == false) || (this.lightGrid.Contains(stampOrigin.X + stampSize, stampOrigin.Y + stampSize) == false))
                {
                    // TODO: in this case we should use another algorithm!!! necessary????
                    throw new NotSupportedException("Light grid's buffer width is not large enough to stamp tree.");
                }

                for (int stampY = 0; stampY < stampSize; ++stampY)
                {
                    int lightY = stampOrigin.Y + stampY;
                    int torusY = TreeListSpatial.GetTorusIndex(lightY, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffset.Y); // 50 cells per 100m
                    for (int stampX = 0; stampX < stampSize; ++stampX)
                    {
                        // suppose there is no stamping outside
                        int lightX = stampOrigin.X + stampX;
                        int torusX = TreeListSpatial.GetTorusIndex(lightX, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffset.X);

                        float dominantHeightInM = vegetationHeightGrid[torusX, torusY, Constant.LightCellsPerHeightCellWidth]; // height of Z* on the current position
                        float z = MathF.Max(this.HeightInM[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY), 0.0F); // distance to center = height (45 degree line)
                        float z_zstar = (z >= dominantHeightInM) ? 1.0F : z / dominantHeightInM;
                        float value = stamp[stampX, stampY]; // stampvalue
                        value = 1.0F - value * this.Opacity[treeIndex] * z_zstar; // calculated value
                        // old: value = 1. - value*mOpacity / local_dom; // calculated value
                        value = MathF.Max(value, 0.02f); // limit value

                        this.lightGrid[torusX, torusY] *= value; // use wraparound coordinates
                    }
                }
            }
        }

        // calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
        public void CalculateDominantHeightField(Landscape landscape, TreeListSpatial treesOfSpecies)
        {
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                LightStamp? readerStamp = this.LightStamp[treeIndex]!.ReaderStamp;
                Debug.Assert(readerStamp != null);

                Point treeLightCellIndexXY = this.LightCellIndexXY[treeIndex];
                int treeHeightCellIndexX = treeLightCellIndexXY.X / Constant.LightCellsPerHeightCellWidth;
                int treeHeightCellIndexY = treeLightCellIndexXY.Y / Constant.LightCellsPerHeightCellWidth;

                // count trees that are on height-grid cells (used for stockable area)
                int heightCellIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY);
                float currentVegetationHeightInM = vegetationHeightGrid[heightCellIndex];
                float treeHeightInM = this.HeightInM[treeIndex];
                if (treeHeightInM > currentVegetationHeightInM)
                {
                    vegetationHeightGrid[heightCellIndex] = treeHeightInM;
                }

                int readerCenter = readerStamp.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                int indexEastWest = this.LightCellIndexXY[treeIndex].X % Constant.LightCellsPerHeightCellWidth; // 4: very west, 0 east edge
                int indexNorthSouth = this.LightCellIndexXY[treeIndex].Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
                if (indexEastWest - readerCenter < 0)
                { // west
                    int westNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX - 1, treeHeightCellIndexY);
                    currentVegetationHeightInM = vegetationHeightGrid[westNeighborIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        vegetationHeightGrid[westNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                {  // east
                    int eastNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX + 1, treeHeightCellIndexY);
                    currentVegetationHeightInM = vegetationHeightGrid[eastNeighborIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        vegetationHeightGrid[eastNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexNorthSouth - readerCenter < 0)
                {  // south
                    int southNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY - 1);
                    currentVegetationHeightInM = vegetationHeightGrid[southNeighborIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        vegetationHeightGrid[southNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                {  // north
                    int northNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY + 1);
                    currentVegetationHeightInM = vegetationHeightGrid[northNeighborIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        vegetationHeightGrid[northNeighborIndex] = treeHeightInM;
                    }
                }
            }
        }

        public void CalculateDominantHeightFieldTorus(Landscape landscape, TreeListSpatial treesOfSpecies, int heightBufferTranslationInCells)
        {
            Grid<float> heightGrid = landscape.VegetationHeightGrid;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                // height of Z*
                Point heightCellIndexXY = new(this.LightCellIndexXY[treeIndex].X / Constant.LightCellsPerHeightCellWidth, this.LightCellIndexXY[treeIndex].Y / Constant.LightCellsPerHeightCellWidth); // pos of tree on height grid
                heightCellIndexXY.X = (heightCellIndexXY.X - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells; // 10: 10 x 10m pixeln in 100m
                heightCellIndexXY.Y = (heightCellIndexXY.Y - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells;

                // torus coordinates: ruOffset = coords of lower left corner of 1 ha patch
                Point ruOffset = new(this.LightCellIndexXY[treeIndex].X / Constant.LightCellsPerHeightCellWidth - heightCellIndexXY.X, this.LightCellIndexXY[treeIndex].Y / Constant.LightCellsPerHeightCellWidth - heightCellIndexXY.Y);

                // count trees that are on height-grid cells (used for stockable area)
                int torusX = TreeListSpatial.GetTorusIndex(heightCellIndexXY.X, 10, heightBufferTranslationInCells, ruOffset.X);
                int torusY = TreeListSpatial.GetTorusIndex(heightCellIndexXY.Y, 10, heightBufferTranslationInCells, ruOffset.Y);
                int heightCellIndex = heightGrid.IndexXYToIndex(torusX, torusY);
                float currentVegetationHeightInM = heightGrid[heightCellIndex];
                float treeHeightInM = this.HeightInM[treeIndex];
                if (treeHeightInM > currentVegetationHeightInM)
                {
                    heightGrid[heightCellIndex] = treeHeightInM;
                }

                LightStamp reader = this.LightStamp[treeIndex]!.ReaderStamp!;
                int readerCenter = reader.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                int indexEastWest = this.LightCellIndexXY[treeIndex].X % Constant.LightCellsPerHeightCellWidth; // 4: very west, 0 east edge
                int indexNorthSouth = this.LightCellIndexXY[treeIndex].Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
                if (indexEastWest - readerCenter < 0)
                { // west
                    int westNeighborIndex = heightGrid.IndexXYToIndex(TreeListSpatial.GetTorusIndex(heightCellIndexXY.X - 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X), 
                                                                      TreeListSpatial.GetTorusIndex(heightCellIndexXY.Y, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                    currentVegetationHeightInM = heightGrid[westNeighborIndex];
                    treeHeightInM = this.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        heightGrid[westNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                {  // east
                    int eastNeighborIndex = heightGrid.IndexXYToIndex(TreeListSpatial.GetTorusIndex(heightCellIndexXY.X + 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                      TreeListSpatial.GetTorusIndex(heightCellIndexXY.Y, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                    currentVegetationHeightInM = heightGrid[eastNeighborIndex];
                    treeHeightInM = this.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        heightGrid[eastNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexNorthSouth - readerCenter < 0)
                {  // south
                    int southNeighborIndex = heightGrid.IndexXYToIndex(TreeListSpatial.GetTorusIndex(heightCellIndexXY.X, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                       TreeListSpatial.GetTorusIndex(heightCellIndexXY.Y - 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                    currentVegetationHeightInM = heightGrid[southNeighborIndex];
                    treeHeightInM = this.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        heightGrid[southNeighborIndex] = treeHeightInM;
                    }
                }
                if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                {  // north
                    int northNeighborIndex = heightGrid.IndexXYToIndex(TreeListSpatial.GetTorusIndex(heightCellIndexXY.X, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                       TreeListSpatial.GetTorusIndex(heightCellIndexXY.Y + 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                    currentVegetationHeightInM = heightGrid[northNeighborIndex];
                    treeHeightInM = this.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        heightGrid[northNeighborIndex] = treeHeightInM;
                    }
                }
            }
        }

        public void DropLastNTrees(int n)
        {
            if ((n < 1) || (n > this.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }

            this.Count -= n;
        }

        public float GetCrownRadius(int treeIndex)
        {
            Debug.Assert(this.LightStamp != null);
            return this.LightStamp[treeIndex]!.CrownRadiusInM;
        }

        public float GetBranchBiomass(int treeIndex)
        {
            return this.Species.GetBiomassBranch(this.DbhInCm[treeIndex]);
        }

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland-model.org/competition+for+light for details.
          */
        public void ReadLightInfluenceField(Landscape landscape, TreeListSpatial treesOfSpecies)
        {
            const float outOfLandscapeInfluenceReduction = 0.1F;

            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            Grid<HeightCellFlags> heightFlags = landscape.VegetationHeightFlags;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                LightStamp reader = this.LightStamp[treeIndex]!.ReaderStamp!;
                Point lightCellPosition = this.LightCellIndexXY[treeIndex];

                int readerOffset = reader.CenterCellIndex;
                int writerOffset = this.LightStamp[treeIndex]!.CenterCellIndex;
                int writerReaderOffset = writerOffset - readerOffset; // offset on the *stamp* to the crown-cells

                lightCellPosition.X -= readerOffset;
                lightCellPosition.Y -= readerOffset;

                int lightIndexX = lightCellPosition.X;
                int lightIndexY = lightCellPosition.Y;

                int readerSize = reader.GetSizeInLightCells();
                float sum = 0.0F;
                for (int readerY = 0; readerY < readerSize; ++readerY, ++lightIndexY)
                {
                    float lightValue = this.lightGrid[lightIndexX, lightIndexY];
                    for (int readerX = 0; readerX < readerSize; ++readerX)
                    {
                        float vegetationHeightInM = vegetationHeightGrid[lightIndexX + readerX, lightIndexY, Constant.LightCellsPerHeightCellWidth];
                        float z = MathF.Max(this.HeightInM[treeIndex] - reader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                        float z_zstar = (z >= vegetationHeightInM) ? 1.0F : z / vegetationHeightInM;

                        float treeValue = 1.0F - this.LightStamp[treeIndex]![readerX, readerY, writerReaderOffset] * this.Opacity[treeIndex] * z_zstar;
                        treeValue = MathF.Max(treeValue, 0.02F);
                        float value = lightValue / treeValue; // remove impact of focal tree
                        
                        // additional punishment if pixel is outside
                        if (heightFlags[lightIndexX + readerX, lightIndexY, Constant.LightCellsPerHeightCellWidth].IsInResourceUnit() == false)
                        {
                            value *= outOfLandscapeInfluenceReduction;
                        }
                        // Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                        // if (value>0.)
                        sum += value * reader[readerX, readerY];
                    }
                }
                this.LightResourceIndex[treeIndex] = sum;
                // LRI correction...
                float relativeHeight = this.HeightInM[treeIndex] / vegetationHeightGrid[this.LightCellIndexXY[treeIndex].X, this.LightCellIndexXY[treeIndex].Y, Constant.LightCellsPerHeightCellWidth];
                if (relativeHeight < 1.0F)
                {
                    this.LightResourceIndex[treeIndex] = this.Species.SpeciesSet.GetLriCorrection(this.LightResourceIndex[treeIndex], relativeHeight);
                }

                if (this.LightResourceIndex[treeIndex] > 1.0F)
                {
                    this.LightResourceIndex[treeIndex] = 1.0F;
                }
                // Finally, add LRI of this Tree to the ResourceUnit!
                this.ResourceUnit.Trees.AddWeightedLeafArea(this.LeafAreaInM2[treeIndex], this.LightResourceIndex[treeIndex]);
            }
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightInfluenceFieldTorus(Landscape landscape, TreeListSpatial treesOfSpecies, int lightBufferWidthInCells)
        {
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                LightStamp stampReader = this.LightStamp[treeIndex]!.ReaderStamp!;
                Point treePositionInRU = new((this.LightCellIndexXY[treeIndex].X - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells,
                                             (this.LightCellIndexXY[treeIndex].Y - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells); // offset within the ha
                Point ruOffset = new(this.LightCellIndexXY[treeIndex].X - treePositionInRU.X, this.LightCellIndexXY[treeIndex].Y - treePositionInRU.Y); // offset of the corner of the resource index

                float lightIndex = 0.0F;
                int readerSize = stampReader.GetSizeInLightCells();
                int readerOriginX = treePositionInRU.X - stampReader.CenterCellIndex;
                int readerOriginY = treePositionInRU.Y - stampReader.CenterCellIndex;
                int writerReaderOffset = this.LightStamp[treeIndex]!.CenterCellIndex - stampReader.CenterCellIndex; // offset on the *stamp* to the crown (light?) cells
                for (int readerY = 0; readerY < readerSize; ++readerY)
                {
                    int yTorus = TreeListSpatial.GetTorusIndex(readerOriginY + readerY, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffset.Y);
                    for (int readerX = 0; readerX < readerSize; ++readerX)
                    {
                        // see http://iland-model.org/competition+for+light 
                        int xTorus = TreeListSpatial.GetTorusIndex(readerOriginX + readerX, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffset.X);
                        float vegetationHeightInM = vegetationHeightGrid[xTorus, yTorus, Constant.LightCellsPerHeightCellWidth];
                        float influenceZ = MathF.Max(this.HeightInM[treeIndex] - stampReader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                        float influenceZstar = (influenceZ >= vegetationHeightInM) ? 1.0F : influenceZ / vegetationHeightInM;

                        // TODO: why a nonzero floor as opposed to skipping division?
                        float focalIntensity = MathF.Max(1.0F - this.LightStamp[treeIndex]![readerX, readerY, writerReaderOffset] * this.Opacity[treeIndex] * influenceZstar, 0.02F);
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
                Point treeLightCellIndexXY = this.LightCellIndexXY[treeIndex];
                float hrel = this.HeightInM[treeIndex] / vegetationHeightGrid[treeLightCellIndexXY.X, treeLightCellIndexXY.Y, Constant.LightCellsPerHeightCellWidth];
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
                this.ResourceUnit.Trees.AddWeightedLeafArea(this.LeafAreaInM2[treeIndex], this.LightResourceIndex[treeIndex]);
            }
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
            this.ResourceUnit.Trees.AddLightResponse(this.LeafAreaInM2[treeIndex], this.LightResponse[treeIndex]);
        }

        /// return the basal area in m2
        public float GetBasalArea(int treeIndex)
        {
            float basalArea = MathF.PI * 0.0001F / 4.0F * this.DbhInCm[treeIndex] * this.DbhInCm[treeIndex];
            return basalArea;
        }

        public float GetStemVolume(int treeIndex)
        {
            /// @see Species::volumeFactor() for details
            float taperCoefficient = this.Species.VolumeFactor;
            float volume = taperCoefficient * 0.0001F * this.DbhInCm[treeIndex] * this.DbhInCm[treeIndex] * this.HeightInM[treeIndex]; // dbh in cm: cm/100 * cm/100 = cm*cm * 0.0001 = m2
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
                float agingFactor = this.Species.GetAgingFactor(this.HeightInM[treeIndex], this.Age[treeIndex]);
                this.ResourceUnit.Trees.AddAging(this.LeafAreaInM2[treeIndex], agingFactor);

                // step 1: get "interception area" of the tree individual [m2]
                // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
                float effectiveTreeArea = this.ResourceUnit.Trees.GetPhotosyntheticallyActiveArea(this.LeafAreaInM2[treeIndex], this.LightResponse[treeIndex]); // light response in [0...1] depending on suppression

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
            float nppAvailable = growthData.NppTotal + this.NppReserveInKg[treeIndex];
            float foliageBiomass = this.Species.GetBiomassFoliage(this.DbhInCm[treeIndex]);
            float reserveSize = foliageBiomass * (1.0F + this.Species.FinerootFoliageRatio);
            float reserveAllocation = MathF.Min(reserveSize, (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMassInKg[treeIndex]); // not always try to refill reserve 100%

            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            float rootFraction = ruSpecies.TreeGrowth.RootFraction;
            growthData.NppAboveground = growthData.NppTotal * (1.0F - rootFraction); // aboveground: total NPP - fraction to roots
            float woodFoliageRatio = this.Species.GetStemFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

            // turnover rates
            float foliageTurnover = this.Species.TurnoverLeaf;
            float rootTurnover = this.Species.TurnoverFineRoot;
            // the turnover rate of wood depends on the size of the reserve pool:
            float woodTurnover = reserveAllocation / (this.StemMassInKg[treeIndex] + reserveAllocation);

            // Duursma 2007, Eq. (20) allocation percentages (sum=1) (eta)
            float woodFraction = (foliageBiomass * woodTurnover / nppAvailable + woodFoliageRatio * (1.0F - rootFraction) - woodFoliageRatio * foliageBiomass * foliageTurnover / nppAvailable) / (foliageBiomass / this.StemMassInKg[treeIndex] + woodFoliageRatio);
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
            float rootSenescence = this.FineRootMassInKg[treeIndex] * rootTurnover;
            float foliageSenescence = this.FoliageMassInKg[treeIndex] * foliageTurnover;
            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddTurnoverLitter(this.Species, foliageSenescence, rootSenescence);
            }

            // Roots
            // http://iland-model.org/allocation#belowground_NPP
            this.FineRootMassInKg[treeIndex] -= rootSenescence; // reduce only fine root pool
            float rootAllocation = rootFraction * nppAvailable;
            // 1st, refill the fine root pool
            float finerootMass = this.FoliageMassInKg[treeIndex] * this.Species.FinerootFoliageRatio - this.FineRootMassInKg[treeIndex];
            if (finerootMass > 0.0F)
            {
                float finerootAllocaton = MathF.Min(finerootMass, rootAllocation);
                this.FineRootMassInKg[treeIndex] += finerootAllocaton;
                rootAllocation -= finerootAllocaton;
            }
            // 2nd, the rest of NPP allocated to roots go to coarse roots
            float maxCoarseRootBiomass = this.Species.GetBiomassCoarseRoot(this.DbhInCm[treeIndex]);
            this.CoarseRootMassInKg[treeIndex] += rootAllocation;
            if (this.CoarseRootMassInKg[treeIndex] > maxCoarseRootBiomass)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the
                // surplus is accounted as turnover
                if (this.ResourceUnit.Snags != null)
                {
                    this.ResourceUnit.Snags.AddTurnoverWood(this.CoarseRootMassInKg[treeIndex] - maxCoarseRootBiomass, this.Species);
                }
                this.CoarseRootMassInKg[treeIndex] = maxCoarseRootBiomass;
            }

            // foliage
            float foliageAllocation = foliageFraction * nppAvailable - foliageSenescence;
            if (Single.IsNaN(foliageAllocation))
            {
                throw new ArithmeticException("Foliage mass is NaN.");
            }

            this.FoliageMassInKg[treeIndex] += foliageAllocation;
            if (this.FoliageMassInKg[treeIndex] < 0.0F)
            {
                this.FoliageMassInKg[treeIndex] = 0.0F; // limit to zero
            }

            this.LeafAreaInM2[treeIndex] = this.FoliageMassInKg[treeIndex] * this.Species.SpecificLeafArea; // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            growthData.StressIndex = MathF.Max(1.0F - nppAvailable / (foliageTurnover * foliageBiomass + rootTurnover * foliageBiomass * this.Species.FinerootFoliageRatio + reserveSize), 0.0F);

            // Woody compartments
            // see also: http://iland-model.org/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            float woodyAllocation = woodFraction * nppAvailable;
            float toReserve = MathF.Min(reserveSize, woodyAllocation);
            this.NppReserveInKg[treeIndex] = toReserve;
            float netWoodyAllocation = woodyAllocation - toReserve;

            this.DbhDeltaInCm[treeIndex] = 0.0F; // zeroing this here causes height and diameter growth to start with an estimate of only height growth
            if (netWoodyAllocation > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                float stemAllocation = netWoodyAllocation * this.Species.GetStemFraction(this.DbhInCm[treeIndex]);
                growthData.NppStem = stemAllocation;
                this.StemMassInKg[treeIndex] += netWoodyAllocation;
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
            float dbhInM = 0.01F * this.DbhInCm[treeIndex]; // current diameter in [m]
            float previousYearDbhIncrementInM = 0.01F * this.DbhDeltaInCm[treeIndex]; // increment of last year in [m]

            float massFactor = this.Species.VolumeFactor * this.Species.WoodDensity;
            float stemMass = massFactor * dbhInM * dbhInM * this.HeightInM[treeIndex]; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            float factorDiameter = 1.0F / (massFactor * (dbhInM + previousYearDbhIncrementInM) * (dbhInM + previousYearDbhIncrementInM) * (2.0F * this.HeightInM[treeIndex] / dbhInM + hdRatioNewGrowth));
            float nppStem = growthData.NppStem;
            float deltaDbhEstimate = factorDiameter * nppStem; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            float stemEstimate = massFactor * (dbhInM + deltaDbhEstimate) * (dbhInM + deltaDbhEstimate) * (this.HeightInM[treeIndex] + deltaDbhEstimate * hdRatioNewGrowth);
            float stemResidual = stemEstimate - (stemMass + nppStem);

            // the final increment is then:
            float dbhIncrementInM = factorDiameter * (nppStem - stemResidual); // Eq. (11)
            if (MathF.Abs(stemResidual) > MathF.Min(1.0F, stemMass))
            {
                // calculate final residual in stem
                float res_final = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - ((stemMass + nppStem));
                if (MathF.Abs(res_final) > MathF.Min(1.0F, stemMass))
                {
                    // for large errors in stem biomass due to errors in diameter increment (> 1kg or >stem mass), we solve the increment iteratively.
                    // first, increase increment with constant step until we overestimate the first time
                    // then,
                    dbhIncrementInM = 0.02F; // start with 2cm increment
                    bool stepTooLarge = false;
                    float dbhIncrementStepInM = 0.01F; // step-width 1cm
                    do
                    {
                        float est_stem = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth); // estimate with current increment
                        stemResidual = est_stem - (stemMass + nppStem);

                        if (MathF.Abs(stemResidual) < 1.0F) // finished, if stem residual below 1kg
                        {
                            break;
                        }
                        if (stemResidual > 0.0F)
                        {
                            dbhIncrementInM -= dbhIncrementStepInM;
                            stepTooLarge = true;
                        }
                        else
                        {
                            dbhIncrementInM += dbhIncrementStepInM;
                        }
                        if (stepTooLarge)
                        {
                            dbhIncrementStepInM /= 2.0F;
                        }
                    }
                    while (dbhIncrementStepInM > 0.00001F); // continue until diameter "accuracy" falls below 1/100mm
                }
            }

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
            Debug.Assert(dbhIncrementInM <= 0.1, String.Format("Diameter increment out of range: HD {0}, factor_diameter {1}, stem_residual {2}, delta_d_estimate {3}, d_increment {4}, final residual {5} kg.",
                                                               hdRatioNewGrowth,
                                                               factorDiameter,
                                                               stemResidual,
                                                               deltaDbhEstimate,
                                                               dbhIncrementInM,
                                                               massFactor * (this.DbhInCm[treeIndex] + dbhIncrementInM) * (this.DbhInCm[treeIndex] + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - stemMass + nppStem));

            // update state variables
            this.DbhInCm[treeIndex] += 100.0F * dbhIncrementInM; // convert from [m] to [cm]
            this.DbhDeltaInCm[treeIndex] = 100.0F * dbhIncrementInM; // save for next year's growth
            this.HeightInM[treeIndex] += dbhIncrementInM * hdRatioNewGrowth;

            // update state of LIP stamp and opacity
            this.LightStamp[treeIndex] = this.Species.GetStamp(this.DbhInCm[treeIndex], this.HeightInM[treeIndex]); // get new stamp for updated dimensions
            // calculate the CrownFactor which reflects the opacity of the crown
            float treeK = model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-treeK * this.LeafAreaInM2[treeIndex] / this.LightStamp[treeIndex]!.CrownAreaInM2);
        }

        /// return the HD ratio of this year's increment based on the light status.
        private float GetRelativeHeightGrowth(int treeIndex)
        {
            this.Species.GetHeightDiameterRatioLimits(this.DbhInCm[treeIndex], out float hdRatioLow, out float hdRatioHigh);
            Debug.Assert(hdRatioLow < hdRatioHigh, "HD low higher than HD high.");
            Debug.Assert((hdRatioLow > 15.0F - 0.02F * this.DbhInCm[treeIndex]) && (hdRatioHigh < 250.0F), "HD ratio out of range. Low: " + hdRatioLow + ", high: " + hdRatioHigh);

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
            if (this.FoliageMassInKg[treeIndex] < 0.00001F)
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
