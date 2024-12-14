using iLand.Tool;
using iLand.Tree;
using System;
using System.Drawing;

namespace iLand.World
{
    public class Microclimate
    {
        private readonly ResourceUnit resourceUnit;
        private readonly MicroclimateCell[] microclimateCells;
        private readonly (float min, float max)[] bufferingMinMaxByMonth; // save min/max buffering per month
        private bool isSetup;

        public Microclimate(ResourceUnit ru)
        {
            this.microclimateCells = new MicroclimateCell[Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth];
            this.isSetup = false;
            this.resourceUnit = ru;
            this.bufferingMinMaxByMonth = new (float, float)[12];
        }

        /// analyze vegetation on resource unit and calculate indices
        public void CalculateVegetationLaiShadeToleranceAndMeans(Landscape landscape) // C++: Microclimate::calculateVegetation()
        {
            if (this.isSetup == false)
            {
                // calculate (only once) northness and other factors that only depend on elevation model
                this.CalculateFixedFactors(landscape);
            }

            // loop over trees and calculate aggregate values
            Span<float> ba_total = stackalloc float[Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth];
            Span<float> lai_total = stackalloc float[Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth];
            Span<float> shade_tol = stackalloc float[Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth];
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                float lightResponseClass = treesOfSpecies.Species.LightResponseClass;
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    int microclimateCellIndex = this.LightIndexToMicroclimateCellIndex(treesOfSpecies.LightCellIndexXY[treeIndex]);
                    float basalAreaInM2 = treesOfSpecies.GetBasalArea(treeIndex);
                    ba_total[microclimateCellIndex] += basalAreaInM2;
                    lai_total[microclimateCellIndex] += treesOfSpecies.LeafAreaInM2[treeIndex];
                    // shade-tolerance uses species parameter light response class
                    shade_tol[microclimateCellIndex] += lightResponseClass * basalAreaInM2;
                }
            }

            // now write back to the microclimate store
            for (int microclimateCellIndex = 0; microclimateCellIndex < ba_total.Length; ++microclimateCellIndex)
            {
                float lai = Maths.Limit(lai_total[microclimateCellIndex] / Constant.Grid.HeightCellAreaInM2, 0.3F, 9.4F); // m2/m2
                float stol = Maths.Limit(ba_total[microclimateCellIndex] > 0.0F ? shade_tol[microclimateCellIndex] / ba_total[microclimateCellIndex] : 0.0F, 1.0F, 5.0F);
                this.GetCell(microclimateCellIndex).SetLeafAreaIndex(lai); // calculate m2/m2
                this.GetCell(microclimateCellIndex).SetShadeToleranceMean(stol);
            }

            // do additionally calculate and buffer values on RU resolution for performance reasons
            this.CalculateMonthlyBuffering();
        }

        private int LightIndexToMicroclimateCellIndex(Point lightIndexXY)
        {
            int microclimateIndexX = (lightIndexXY.X - this.resourceUnit.MinimumLightIndexXY.X) / Constant.Grid.LightCellsPerHeightCellWidth;
            int microclimateIndexY = (lightIndexXY.Y - this.resourceUnit.MinimumLightIndexXY.Y) / Constant.Grid.LightCellsPerHeightCellWidth;
            return Constant.Grid.HeightCellsPerRUWidth * microclimateIndexY + microclimateIndexX;
        }

        // get resource unit aggregates
        //void microclimateBuffering;
        /// average minimum buffering, i.e. actual min temperature = min_temp + buffering
        /// month is 0..11
        public float GetMinimumMicroclimateBuffering(int monthIndex) // C++: Microclimate::minimumMicroclimateBufferingRU()
        {
            return this.bufferingMinMaxByMonth[monthIndex].min;
        }

        /// average maximum buffering, i.e. actual max temperature = max_temp + buffering
        /// month is 0..11
        public float GetMaximumMicroclimateBuffering(int monthIndex) // C++: Microclimate::maximumMicroclimateBufferingRU()
        {
            return this.bufferingMinMaxByMonth[monthIndex].max;
        }

        /// mean buffering per RU for month [0..11]
        public float GetMeanMicroclimateBuffering(int monthIndex) // C++: Microclimate::meanMicroclimateBufferingRU()
        {
            // calculate mean value of min and max buffering
            float buffer = 0.5F * (this.GetMinimumMicroclimateBuffering(monthIndex) + this.GetMaximumMicroclimateBuffering(monthIndex));
            return buffer;
        }

        public MicroclimateCell GetCell(int index) // C++: Microclimate::getCell()
        { 
            return this.microclimateCells[index]; 
        }

        public PointF GetCellCentroid(int index) // C++: Microclimate::cellCoord()
        {
            float centroidX = Constant.Grid.HeightCellsPerRUWidth * (index % Constant.Grid.HeightCellsPerRUWidth + 0.5F);
            float centroidY = Constant.Grid.HeightCellsPerRUWidth * (index / Constant.Grid.HeightCellsPerRUWidth + 0.5F);
            return new PointF(this.resourceUnit.ProjectExtent.Left + centroidX, this.resourceUnit.ProjectExtent.Bottom + centroidY);
        }

        private void CalculateFixedFactors(Landscape landscape)
        {
            if (landscape.DigitalElevationModel == null)
            {
                throw new ArgumentOutOfRangeException(nameof(landscape), "The iLand microclimate module requires a digital elevation model.");
            }

            // extract fixed factors from DEM
            DigitalElevationModel dem = landscape.DigitalElevationModel;
            for (int microclimateCellIndex = 0; microclimateCellIndex < this.microclimateCells.Length; ++microclimateCellIndex)
            {
                PointF p = GetCellCentroid(microclimateCellIndex);
                float aspect = Single.Pi / 180.0F * dem.AspectGrid[p];
                float northness = MathF.Cos(aspect);

                // slope
                //float slope = dem.slopeGrid().constValueAt(p); // percentage of degrees, i.e. 1 = 45 degrees
                //slope = atan(slope) * 180.0F / Single.Pi; // convert degree, thanks Kristin for spotting the error in a previous version

                // topographic position
                const int radius = 500;
                float tpi = dem.GetTopographicPositionIndex(p, radius);

                // limit values to range of predictors in statistical model
                //northness = limit(northness, -1, 1)
                tpi = Maths.Limit(tpi, -105.0F, 67.0F);

                GetCell(microclimateCellIndex).SetNorthness(northness);
                GetCell(microclimateCellIndex).SetTopographicPositionIndex(tpi);

                // we only process cells that are stockable
                // TODO: restore once stockability mask is back in place
                //if (hg[p].isValid() = false)
                //{
                //    cell(microclimateCellIndex).setInvalid();
                //}
            }

            this.isSetup = true;
        }

        private void CalculateMonthlyBuffering() // C++: Microclimate::calculateRUMeanValues()
        {
            // calculate mean min / max temperature
            for (int monthIndex = 0; monthIndex < 12; ++monthIndex)
            {
                float tmin = Maths.Limit(this.resourceUnit.Weather.TimeSeries.GetMonthlyMeanDailyMinimumTemperature(monthIndex), -12.4F, 16.5F);
                float tmax = Maths.Limit(this.resourceUnit.Weather.TimeSeries.GetMonthlyMeanDailyMaximumTemperature(monthIndex), -5.4F, 44.9F);

                // run calculations
                // loop over all cells and calculate buffering
                float buffer_min = 0.0F;
                float buffer_max = 0.0F;
                int nValidCells = 0;
                for (int microclimateCellIndex = 0; microclimateCellIndex < this.microclimateCells.Length; ++microclimateCellIndex)
                {
                    MicroclimateCell cell = this.microclimateCells[microclimateCellIndex];
                    if (cell.IsValid())
                    {
                        buffer_min += cell.GetMinimumMicroclimateBuffering(tmin);
                        buffer_max += cell.GetMaximumMicroclimateBuffering(tmax);
                        ++nValidCells;
                    }
                }

                // calculate mean values for RU and save for later
                buffer_min = nValidCells > 0 ? buffer_min / nValidCells : 0.0F;
                buffer_max = nValidCells > 0 ? buffer_max / nValidCells : 0.0F;
                if (Single.Abs(buffer_min) > 15.0F || Single.Abs(buffer_max) > 15.0F)
                {
                    // qDebug() << "Microclimate: dubious buffering: RU: " << mRU.id() << ", buffer_min:" << buffer_min << ", buffermax:" << buffer_max;
                    buffer_min = 0.0F;
                    buffer_max = 0.0F; // just make sure nothing bad happens downstream
                }

                this.bufferingMinMaxByMonth[monthIndex] = (buffer_min, buffer_max);
            }
        }
    }
}
