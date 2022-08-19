using iLand.World;
using OSGeo.GDAL;
using System.Drawing;

namespace iLand.Extensions
{
    internal static class GridExtensions
    {
        public static void ExportToGeoTiff(this Grid<float> grid, string geoTiffFilePath, string projection, PointF gisOrigin)
        {
            // https://gdal.org/api/csharp/csharp_raster.html - out of date as of August 2022, but potentially useful
            // https://gdal.org/tutorials/geotransforms_tut.html
            // Unclear if any useful return code checking can be done on SetTransform(), SetProjection(), and WriteRaster().
            Driver geoTiff = Gdal.GetDriverByName("GTiff");
            string[] options = new string[] { "COMPRESS=DEFLATE", "PREDICTOR=2", "ZLEVEL=9" };
            using Dataset raster = geoTiff.Create(geoTiffFilePath, grid.SizeX, grid.SizeY, 1, DataType.GDT_Float32, options);
            // GDAL transform parameters: lower left corner x, cell size east-west, row rotation, lower left corner y, column rotation, cell size north-south
            // GDAL documentation indicates the upper left corner but this is incorrect
            raster.SetGeoTransform(new double[] { gisOrigin.X + grid.ProjectExtent.X, grid.CellSizeInM, 0.0, gisOrigin.Y + grid.ProjectExtent.Y, 0.0, grid.CellSizeInM });
            raster.SetProjection(projection);
            raster.WriteRaster(xOff: 0, yOff: 0, grid.SizeX, grid.SizeY, grid.Data, grid.SizeX, grid.SizeY, bandCount: 1, null, pixelSpace: 0, lineSpace: 0, bandSpace: 0);
        }
    }
}