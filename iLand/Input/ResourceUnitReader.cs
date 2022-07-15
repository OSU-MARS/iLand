using iLand.Input.ProjectFile;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit weather, soil properties, and other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See http://iland-model.org/simulation+extent.
    /// </remarks>
    public class ResourceUnitReader
    {
        private readonly Dictionary<string, int> environmentIndexByCoordinate;
        private PointF maximumCenterCoordinate;
        private PointF minimumCenterCoordinate;

        public List<ResourceUnitEnvironment> Environments { get; private init; }

        public ResourceUnitReader(Project projectFile)
        {
            this.environmentIndexByCoordinate = new Dictionary<string, int>();
            this.maximumCenterCoordinate = new(Single.MinValue, Single.MinValue);
            this.minimumCenterCoordinate = new(Single.MaxValue, Single.MaxValue);

            this.Environments = new();

            string resourceUnitFilePath = projectFile.GetFilePath(ProjectDirectory.Gis, projectFile.World.Initialization.ResourceUnitFile);
            using CsvFile resourceUnitEnvironmentFile = new(resourceUnitFilePath);
            ResourceUnitHeader environmentHeader = new(resourceUnitEnvironmentFile);

            ResourceUnitEnvironment defaultEnvironment = new(projectFile.World);
            if (String.IsNullOrEmpty(defaultEnvironment.WeatherID) && (environmentHeader.WeatherID < 0))
            {
                throw new NotSupportedException("Environment file must have a weather ID column if model.world.weather.defaultDatabaseTable is not specified in the project file.");
            }
            if (String.IsNullOrEmpty(defaultEnvironment.SpeciesTableName) && (environmentHeader.SpeciesTableName < 0))
            {
                throw new NotSupportedException("Environment file must have a species table column if model.world.species.databaseTable is not specified in the project file.");
            }

            resourceUnitEnvironmentFile.Parse((string[] row) =>
            {
                ResourceUnitEnvironment resourceUnitEnvironment = new(environmentHeader, row, defaultEnvironment);

                environmentIndexByCoordinate[resourceUnitEnvironment.GetCentroidKey()] = this.Environments.Count;
                this.Environments.Add(resourceUnitEnvironment);

                if (resourceUnitEnvironment.GisCenterX > this.maximumCenterCoordinate.X)
                {
                    this.maximumCenterCoordinate.X = resourceUnitEnvironment.GisCenterX;
                }
                if (resourceUnitEnvironment.GisCenterY > this.maximumCenterCoordinate.Y)
                {
                    this.maximumCenterCoordinate.Y = resourceUnitEnvironment.GisCenterY;
                }
                if (resourceUnitEnvironment.GisCenterX < this.minimumCenterCoordinate.X)
                {
                    this.minimumCenterCoordinate.X = resourceUnitEnvironment.GisCenterX;
                }
                if (resourceUnitEnvironment.GisCenterY < this.minimumCenterCoordinate.Y)
                {
                    this.minimumCenterCoordinate.Y = resourceUnitEnvironment.GisCenterY;
                }
            });

            if (this.Environments.Count < 1)
            {
                throw new NotSupportedException("Resource unit environment file '" + resourceUnitFilePath + "' is empty or has only headers.");
            }
        }

        public RectangleF GetBoundingBox()
        {
            float resourceUnitSize = Constant.ResourceUnitSizeInM;
            float x = this.minimumCenterCoordinate.X - 0.5F * resourceUnitSize;
            float y = this.minimumCenterCoordinate.Y - 0.5F * resourceUnitSize;
            float width = this.maximumCenterCoordinate.X - this.minimumCenterCoordinate.X + resourceUnitSize;
            float height = this.maximumCenterCoordinate.Y - this.minimumCenterCoordinate.Y + resourceUnitSize;
            return new RectangleF(x, y, width, height);
        }
    }
}
