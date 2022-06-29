using iLand.Input.ProjectFile;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit climates and soil properties plus a few other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See http://iland-model.org/simulation+extent.
    /// </remarks>
    public class ResourceUnitReader
    {
        private ResourceUnitEnvironment? currentEnvironment;

        private readonly Dictionary<string, int> environmentIndexByCoordinate;
        private readonly List<ResourceUnitEnvironment> environments;
        private PointF maximumCenterCoordinate;
        private PointF minimumCenterCoordinate;

        public ResourceUnitReader(Project projectFile)
        {
            this.currentEnvironment = null;

            this.environmentIndexByCoordinate = new Dictionary<string, int>();
            this.environments = new();
            this.maximumCenterCoordinate = new(Single.MinValue, Single.MinValue);
            this.minimumCenterCoordinate = new(Single.MaxValue, Single.MaxValue);

            string resourceUnitFilePath = projectFile.GetFilePath(ProjectDirectory.Gis, projectFile.World.Initialization.ResourceUnitFile); // TODO: stop requiring gis\ prefix in project file
            using CsvFile resourceUnitEnvironmentFile = new(resourceUnitFilePath);
            ResourceUnitHeader environmentHeader = new(resourceUnitEnvironmentFile);

            ResourceUnitEnvironment defaultEnvironment = new(projectFile.World);
            if (String.IsNullOrEmpty(defaultEnvironment.ClimateID) && (environmentHeader.ClimateID < 0))
            {
                throw new NotSupportedException("Environment file must have a '" + Constant.Setting.Climate.Name + "' column if '" + Constant.Setting.Climate.Name + "' is not specified in the project file.");
            }
            if (String.IsNullOrEmpty(defaultEnvironment.SpeciesTableName) && (environmentHeader.SpeciesTableName < 0))
            {
                throw new NotSupportedException("Environment file must have a '" + Constant.Setting.SpeciesTable + "' column if '" + Constant.Setting.SpeciesTable + "' is not specified in the project file.");
            }

            resourceUnitEnvironmentFile.Parse((string[] row) =>
            {
                ResourceUnitEnvironment resourceUnitEnvironment = new(environmentHeader, row, defaultEnvironment);

                environmentIndexByCoordinate[resourceUnitEnvironment.GetCentroidKey()] = this.environments.Count;
                this.environments.Add(resourceUnitEnvironment);

                if (resourceUnitEnvironment.CenterX > this.maximumCenterCoordinate.X)
                {
                    this.maximumCenterCoordinate.X = resourceUnitEnvironment.CenterX;
                }
                if (resourceUnitEnvironment.CenterY > this.maximumCenterCoordinate.Y)
                {
                    this.maximumCenterCoordinate.Y = resourceUnitEnvironment.CenterY;
                }
                if (resourceUnitEnvironment.CenterX < this.minimumCenterCoordinate.X)
                {
                    this.minimumCenterCoordinate.X = resourceUnitEnvironment.CenterX;
                }
                if (resourceUnitEnvironment.CenterY < this.minimumCenterCoordinate.Y)
                {
                    this.minimumCenterCoordinate.Y = resourceUnitEnvironment.CenterY;
                }
            });

            if (this.environments.Count < 1)
            {
                throw new NotSupportedException("Resource unit environment file '" + resourceUnitFilePath + "' is empty or has only headers.");
            }
        }

        public ResourceUnitEnvironment CurrentEnvironment 
        {
            get 
            {
                Debug.Assert(this.currentEnvironment != null);
                return this.currentEnvironment; 
            }
        }

        public RectangleF GetBoundingBox()
        {
            float resourceUnitSize = Constant.ResourceUnitSize;
            float x = this.minimumCenterCoordinate.X - 0.5F * resourceUnitSize;
            float y = this.minimumCenterCoordinate.Y - 0.5F * resourceUnitSize;
            float width = this.maximumCenterCoordinate.X - this.minimumCenterCoordinate.X + resourceUnitSize;
            float height = this.maximumCenterCoordinate.Y - this.minimumCenterCoordinate.Y + resourceUnitSize;
            return new RectangleF(x, y, width, height);
        }

        /// <summary>
        /// Moves environment enumerator to specified resource unit.
        /// </summary>
        public void MoveTo(PointF ruCentroid)
        {
            string key = (int)ruCentroid.X + "_" + (int)ruCentroid.Y;
            if (environmentIndexByCoordinate.TryGetValue(key, out int environmentIndex) == false)
            {
                throw new FileLoadException("Resource unit not found at (" + (int)ruCentroid.X + ", " + (int)ruCentroid.Y + ") in environment file.");
            }

            this.currentEnvironment = this.environments[environmentIndex];
        }
    }
}
