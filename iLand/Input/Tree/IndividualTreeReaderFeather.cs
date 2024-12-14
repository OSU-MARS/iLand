using Apache.Arrow;
using Apache.Arrow.Compression;
using Apache.Arrow.Ipc;
using iLand.Extensions;
using iLand.Tree;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Array = System.Array;

namespace iLand.Input.Tree
{
    public class IndividualTreeReaderFeather : IndividualTreeReader
    {
        public IndividualTreeReaderFeather(string individualTreeFilePath)
            : base(individualTreeFilePath)
        {
            using FileStream individualTreeStream = new(individualTreeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader individualTreeFile = new(individualTreeStream, new CompressionCodecFactory()); // ArrowFileReader.IsFileValid is false until a batch is read

            // no clear advantage to reading batches asynchronously at 9.2 Mtrees (Apache 9.0.0, .NET 6.0, AMD Zen 3 @ 4.8 GHz, PCIe 3.0 x4 SSD)
            for (RecordBatch? batch = individualTreeFile.ReadNextRecordBatch(); batch != null; batch = individualTreeFile.ReadNextRecordBatch())
            {
                IndividualTreeArrowBatch fields = new(batch);
                ReadOnlySpan<float> dbhField = fields.DbhInCm.Values;
                ReadOnlySpan<UInt16> fiaCodeField = [];
                if (fields.FiaCode != null)
                {
                    fiaCodeField = fields.FiaCode.Values;
                }
                ReadOnlySpan<float> heightField = fields.HeightInM.Values;
                ReadOnlySpan<UInt32> wfoIDfield = [];
                if (fields.WorldFloraID != null)
                {
                    wfoIDfield = fields.WorldFloraID.Values;
                }
                ReadOnlySpan<float> gisXfield = fields.GisX.Values;
                ReadOnlySpan<float> gisYfield = fields.GisY.Values;

                if (this.Capacity - this.Count < batch.Length)
                {
                    int estimatedNewCapacity = this.Count + batch.Length; // minimal capacity
                    // estimate capacity from file size, assuming uncompressed feather since compression is not supported in Apache 9.0.0 C#
                    double uncompressedBatchSizeInBytes = batch.Length * fields.GetBytesPerRecord();
                    double uncompressedBytesPerTree = uncompressedBatchSizeInBytes / batch.Length;
                    int estimatedCapacityFromFileSize = (int)Math.Ceiling(individualTreeStream.Length / uncompressedBytesPerTree); // should be slightly high as ~2.1 kB of file is identifiers, schema, and batch markers
                    if (estimatedCapacityFromFileSize > estimatedNewCapacity)
                    {
                        estimatedNewCapacity = estimatedCapacityFromFileSize;
                    }
                    this.Resize(estimatedNewCapacity);
                }

                if (fields.FiaCode != null)
                {
                    FiaCode previousFiaCode = FiaCode.Unknown;
                    WorldFloraID treeSpeciesID = WorldFloraID.Unknown;
                    for (int destinationIndex = this.Count, sourceIndex = 0; sourceIndex < batch.Length; ++destinationIndex, ++sourceIndex)
                    {
                        FiaCode fiaCode = (FiaCode)fiaCodeField[sourceIndex];
                        if (fiaCode != previousFiaCode)
                        {
                            treeSpeciesID = WorldFloraIDExtensions.Convert(fiaCode);
                        }
                        this.SpeciesID[destinationIndex] = treeSpeciesID;
                    }
                }
                else
                {
                    wfoIDfield.CopyTo(MemoryMarshal.Cast<WorldFloraID, UInt32>(this.SpeciesID.AsSpan())[this.Count..]);
                }
                dbhField.CopyTo(this.DbhInCm.AsSpan()[this.Count..]);
                heightField.CopyTo(this.HeightInM.AsSpan()[this.Count..]);
                gisXfield.CopyTo(this.GisX.AsSpan()[this.Count..]);
                gisYfield.CopyTo(this.GisY.AsSpan()[this.Count..]);

                if (fields.AgeInYears != null)
                {
                    fields.AgeInYears.Values.CopyTo(this.AgeInYears.AsSpan()[this.Count..]);
                }
                // else { leave this.AgeInYears as zero}

                if (fields.StandID != null)
                {
                    fields.StandID.Values.CopyTo(this.StandID.AsSpan()[this.Count..]);
                }
                else
                {
                    Debug.Assert(Constant.DefaultStandID == 0);
                    // this.StandID.AsSpan()[this.Count..].Fill(Constant.DefaultStandID);
                }

                if (fields.TreeID != null)
                {
                    fields.TreeID.Values.CopyTo(this.TreeID.AsSpan()[this.Count..]);
                }
                else
                {
                    for (int destinationIndex = this.Count, sourceIndex = 0; sourceIndex < batch.Length; ++destinationIndex, ++sourceIndex)
                    {
                        this.TreeID[destinationIndex] = (UInt32)destinationIndex; // default "unique" tree ID is its sequential number in the tree file
                    }
                }

                // no read time validation as it's done when trees are added to resource units
                this.Count += batch.Length;
            }
        }
    }
}
