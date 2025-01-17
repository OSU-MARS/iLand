﻿using Apache.Arrow;
using Apache.Arrow.Compression;
using Apache.Arrow.Ipc;
using System;
using System.IO;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit weather, soil properties, and other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See https://iland-model.org/simulation+extent.
    /// </remarks>
    public class ResourceUnitReaderFeather : ResourceUnitReader
    {
        public ResourceUnitReaderFeather(string resourceUnitFilePath, ResourceUnitEnvironment defaultEnvironment)
        {
            using FileStream resourceUnitStream = new(resourceUnitFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader resourceUnitFile = new(resourceUnitStream, new CompressionCodecFactory()); // ArrowFileReader.IsFileValid is false until a batch is read

            for (RecordBatch? batch = resourceUnitFile.ReadNextRecordBatch(); batch != null; batch = resourceUnitFile.ReadNextRecordBatch())
            {
                ResourceUnitArrowBatch fields = new(batch);
                if (String.IsNullOrEmpty(defaultEnvironment.WeatherID) && (fields.WeatherID == null))
                {
                    throw new NotSupportedException("Environment file must have a weather ID column if /project/model/world/weather/defaultDatabaseTable is not specified in the project file.");
                }
                if (String.IsNullOrEmpty(defaultEnvironment.SpeciesTableName) && (fields.SpeciesTableName == null))
                {
                    throw new NotSupportedException("Environment file must have a species table column if /project/model/world/species/databaseTable is not specified in the project file.");
                }

                if (this.Capacity - this.Count < batch.Length)
                {
                    this.Resize(this.Count + batch.Length);
                }

                for (int index = 0; index < batch.Length; ++index)
                {
                    ResourceUnitEnvironment resourceUnitEnvironment = new(fields, index, defaultEnvironment);
                    this.Environments[this.Count] = resourceUnitEnvironment;
                    ++this.Count;

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
                }
            }

            if (this.Count < 1)
            {
                throw new NotSupportedException("Resource unit environment file '" + resourceUnitFilePath + "' is empty or has only headers.");
            }
        }
    }
}
