using System;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit weather, soil properties, and other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See http://iland-model.org/simulation+extent.
    /// </remarks>
    public class ResourceUnitReaderCsv : ResourceUnitReader
    {
        public ResourceUnitReaderCsv(string resourceUnitFilePath, ResourceUnitEnvironment defaultEnvironment)
        {
            using CsvFile resourceUnitEnvironmentFile = new(resourceUnitFilePath);
            ResourceUnitHeaderCsv environmentHeader = new(resourceUnitEnvironmentFile);

            if (String.IsNullOrEmpty(defaultEnvironment.WeatherID) && (environmentHeader.WeatherID < 0))
            {
                throw new NotSupportedException("Environment file must have a weather ID column if /project/model/world/weather/defaultDatabaseTable is not specified in the project file.");
            }
            if (String.IsNullOrEmpty(defaultEnvironment.SpeciesTableName) && (environmentHeader.SpeciesTableName < 0))
            {
                throw new NotSupportedException("Environment file must have a species table column if /project/model/world/species/databaseTable is not specified in the project file.");
            }

            resourceUnitEnvironmentFile.Parse((string[] row) =>
            {
                ResourceUnitEnvironment resourceUnitEnvironment = new(environmentHeader, row, defaultEnvironment);

                this.Environments.Add(resourceUnitEnvironment);

                if (resourceUnitEnvironment.GisCenterX > this.MaximumCenterCoordinateX)
                {
                    this.MaximumCenterCoordinateX = resourceUnitEnvironment.GisCenterX;
                }
                if (resourceUnitEnvironment.GisCenterY > this.MaximumCenterCoordinateY)
                {
                    this.MaximumCenterCoordinateY = resourceUnitEnvironment.GisCenterY;
                }
                if (resourceUnitEnvironment.GisCenterX < this.MinimumCenterCoordinateX)
                {
                    this.MinimumCenterCoordinateX = resourceUnitEnvironment.GisCenterX;
                }
                if (resourceUnitEnvironment.GisCenterY < this.MinimumCenterCoordinateY)
                {
                    this.MinimumCenterCoordinateY = resourceUnitEnvironment.GisCenterY;
                }
            });

            if (this.Environments.Count < 1)
            {
                throw new NotSupportedException("Resource unit environment file '" + resourceUnitFilePath + "' is empty or has only headers.");
            }
        }
    }
}
