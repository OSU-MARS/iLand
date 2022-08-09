using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using iLand.Extensions;
using iLand.Output;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using Model = iLand.Simulation.Model;
namespace iLand.Cmdlets
{
    [Cmdlet(VerbsCommunications.Write, "Trajectory")]
    public class WriteTrajectory : Cmdlet
    {
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResourceUnitFile { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? StandFile { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public Model? Trajectory { get; set; }

        public WriteTrajectory()
        {
            this.ResourceUnitFile = null;
            this.StandFile = null;
            this.Trajectory = null;
        }

        private static RecordBatch CreateArrowBatch<TTrajectory>(Schema schema, IList<TTrajectory> trajectories, int calendarYearBeforeFirstSimulationTimestep) where TTrajectory : StandOrResourceUnitTrajectory
        {
            // allocate arrays
            int trajectoryLengthInYears = trajectories[0].Years;
            int batchLength = trajectories.Count * trajectoryLengthInYears;
            Memory<byte> year = new(new byte[batchLength * sizeof(Int32)]);
            Memory<byte> resourceUnitIDs = new(new byte[batchLength * sizeof(Int32)]);
            Memory<byte> averageDbh = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> averageHeight = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> basalArea = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> lai = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> liveStemVolume = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> treeNpp = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> treeAbovegroundNpp = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> treesPerHectare = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> saplingCohorts = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> saplingMeanAge = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> saplingNpp = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> saplingsPerHectare = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> branchCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> branchNitrogen = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> coarseRootCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> coarseRootNitrogen = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> fineRootCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> fineRootNitrogen = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> foliageCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> foliageNitrogen = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> regenerationCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> regenerationNitrogen = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> stemCarbon = new(new byte[batchLength * sizeof(float)]);
            Memory<byte> stemNitrogen = new(new byte[batchLength * sizeof(float)]);

            Span<int> yearDestination = year.Span.CastTo<int>();
            Span<int> resourceUnitIDdestination = resourceUnitIDs.Span.CastTo<int>();
            Span<float> averageDbhDestination = averageDbh.Span.CastTo<float>();
            Span<float> averageHeightDestination = averageHeight.Span.CastTo<float>();
            Span<float> basalAreaDestination = basalArea.Span.CastTo<float>();
            Span<float> laiDestination = lai.Span.CastTo<float>();
            Span<float> liveStemVolumeDestination = liveStemVolume.Span.CastTo<float>();
            Span<float> treeNppDestination = treeNpp.Span.CastTo<float>();
            Span<float> treeAbovegroundNppDestination = treeAbovegroundNpp.Span.CastTo<float>();
            Span<float> treesPerHectareDestination = treesPerHectare.Span.CastTo<float>();
            Span<float> saplingCohortsDestination = saplingCohorts.Span.CastTo<float>();
            Span<float> saplingMeanAgeDestination = saplingMeanAge.Span.CastTo<float>();
            Span<float> saplingNppDestination = saplingNpp.Span.CastTo<float>();
            Span<float> saplingsPerHectareDestination = saplingsPerHectare.Span.CastTo<float>();
            Span<float> branchCarbonDestination = branchCarbon.Span.CastTo<float>();
            Span<float> branchNitrogenDestination = branchNitrogen.Span.CastTo<float>();
            Span<float> coarseRootCarbonDestination = coarseRootCarbon.Span.CastTo<float>();
            Span<float> coarseRootNitrogenDestination = coarseRootNitrogen.Span.CastTo<float>();
            Span<float> fineRootCarbonDestination = fineRootCarbon.Span.CastTo<float>();
            Span<float> fineRootNitrogenDestination = fineRootNitrogen.Span.CastTo<float>();
            Span<float> foliageCarbonDestination = foliageCarbon.Span.CastTo<float>();
            Span<float> foliageNitrogenDestination = foliageNitrogen.Span.CastTo<float>();
            Span<float> regenerationCarbonDestination = regenerationCarbon.Span.CastTo<float>();
            Span<float> regenerationNitrogenDestination = regenerationNitrogen.Span.CastTo<float>();
            Span<float> stemCarbonDestination = stemCarbon.Span.CastTo<float>();
            Span<float> stemNitrogenDestination = stemNitrogen.Span.CastTo<float>();

            // copy data from resource units
            Span<int> yearSource = stackalloc int[trajectoryLengthInYears];
            for (int simulationYear = 0; simulationYear < trajectoryLengthInYears; ++simulationYear)
            {
                yearSource[simulationYear] = simulationYear + calendarYearBeforeFirstSimulationTimestep;
            }
            for (int batchIndex = 0, resourceUnitIndex = 0; resourceUnitIndex < trajectories.Count; ++resourceUnitIndex)
            {
                TTrajectory trajectory = trajectories[resourceUnitIndex];
                if (trajectory.Years != trajectoryLengthInYears)
                {
                    string trajectoryType = trajectory is ResourceUnitTrajectory ? "resource unit" : "stand";
                    throw new NotSupportedException("Trajectory for " + trajectoryType + " " + trajectory.GetID() + " is " + trajectory.Years + " years long, which departs from the expected trajectory length of " + trajectoryLengthInYears + " years.");
                }

                resourceUnitIDdestination.Slice(batchIndex, trajectoryLengthInYears).Fill(trajectory.GetID());
                yearSource.CopyTo(yearDestination.Slice(batchIndex, trajectoryLengthInYears));

                Span<float> averageDbhSource = CollectionsMarshal.AsSpan(trajectory.AverageDbhByYear);
                averageDbhSource.CopyTo(averageDbhDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> averageHeightSource = CollectionsMarshal.AsSpan(trajectory.AverageHeightByYear);
                averageHeightSource.CopyTo(averageHeightDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> basalAreaSource = CollectionsMarshal.AsSpan(trajectory.BasalAreaByYear);
                basalAreaSource.CopyTo(basalAreaDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> leafAreaIndexSource = CollectionsMarshal.AsSpan(trajectory.LeafAreaIndexByYear);
                leafAreaIndexSource.CopyTo(laiDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> liveStemVolumeSource = CollectionsMarshal.AsSpan(trajectory.LiveStemVolumeByYear);
                liveStemVolumeSource.CopyTo(liveStemVolumeDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> treeNppSource = CollectionsMarshal.AsSpan(trajectory.TreeNppByYear);
                treeNppSource.CopyTo(treeNppDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> treeNppAbovegroundSource = CollectionsMarshal.AsSpan(trajectory.TreeNppAbovegroundByYear);
                treeNppAbovegroundSource.CopyTo(treeAbovegroundNppDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> treesPerHectareSource = CollectionsMarshal.AsSpan(trajectory.TreesPerHectareByYear);
                treesPerHectareSource.CopyTo(treesPerHectareDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> saplingCohortsSource = CollectionsMarshal.AsSpan(trajectory.SaplingCohortsPerHectareByYear);
                saplingCohortsSource.CopyTo(saplingCohortsDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> saplingMeanAgeSource = CollectionsMarshal.AsSpan(trajectory.SaplingMeanAgeByYear);
                saplingMeanAgeSource.CopyTo(saplingMeanAgeDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> saplingNppSource = CollectionsMarshal.AsSpan(trajectory.SaplingNppByYear);
                saplingNppSource.CopyTo(saplingNppDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> saplingsPerHectareSource = CollectionsMarshal.AsSpan(trajectory.SaplingsPerHectareByYear);
                saplingsPerHectareSource.CopyTo(saplingsPerHectareDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> branchCarbonSource = CollectionsMarshal.AsSpan(trajectory.BranchCarbonByYear);
                branchCarbonSource.CopyTo(branchCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> branchNitrogenSource = CollectionsMarshal.AsSpan(trajectory.BranchNitrogenByYear);
                branchNitrogenSource.CopyTo(branchNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> coarseRootCarbonSource = CollectionsMarshal.AsSpan(trajectory.CoarseRootCarbonByYear);
                coarseRootCarbonSource.CopyTo(coarseRootCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> coarseRootNitrogenSource = CollectionsMarshal.AsSpan(trajectory.CoarseRootNitrogenByYear);
                coarseRootNitrogenSource.CopyTo(coarseRootNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> fineRootCarbonSource = CollectionsMarshal.AsSpan(trajectory.FineRootCarbonByYear);
                fineRootCarbonSource.CopyTo(fineRootCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> fineRootNitrogenSource = CollectionsMarshal.AsSpan(trajectory.FineRootNitrogenByYear);
                fineRootNitrogenSource.CopyTo(fineRootNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> foliageCarbonSource = CollectionsMarshal.AsSpan(trajectory.FoliageCarbonByYear);
                foliageCarbonSource.CopyTo(foliageCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> foliageNitrogenSource = CollectionsMarshal.AsSpan(trajectory.FoliageNitrogenByYear);
                foliageNitrogenSource.CopyTo(foliageNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> regenerationCarbonSource = CollectionsMarshal.AsSpan(trajectory.RegenerationCarbonByYear);
                regenerationCarbonSource.CopyTo(regenerationCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> regeneratioNitrogenSource = CollectionsMarshal.AsSpan(trajectory.RegenerationNitrogenByYear);
                regeneratioNitrogenSource.CopyTo(regenerationNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> stemCarbonSource = CollectionsMarshal.AsSpan(trajectory.StemCarbonByYear);
                stemCarbonSource.CopyTo(stemCarbonDestination.Slice(batchIndex, trajectoryLengthInYears));
                Span<float> stemNitrogenSource = CollectionsMarshal.AsSpan(trajectory.StemNitrogenByYear);
                stemNitrogenSource.CopyTo(stemNitrogenDestination.Slice(batchIndex, trajectoryLengthInYears));

                batchIndex += trajectoryLengthInYears;
            }

            // repackage arrays into Arrow record batch
            IArrowArray[] data = new IArrowArray[]
            {
                ArrowArrayExtensions.WrapInInt32(resourceUnitIDs),
                ArrowArrayExtensions.WrapInInt32(year),
                ArrowArrayExtensions.WrapInFloat(averageDbh),
                ArrowArrayExtensions.WrapInFloat(averageHeight),
                ArrowArrayExtensions.WrapInFloat(basalArea),
                ArrowArrayExtensions.WrapInFloat(lai),
                ArrowArrayExtensions.WrapInFloat(liveStemVolume),
                ArrowArrayExtensions.WrapInFloat(treeNpp),
                ArrowArrayExtensions.WrapInFloat(treeAbovegroundNpp),
                ArrowArrayExtensions.WrapInFloat(treesPerHectare),
                ArrowArrayExtensions.WrapInFloat(saplingCohorts),
                ArrowArrayExtensions.WrapInFloat(saplingMeanAge),
                ArrowArrayExtensions.WrapInFloat(saplingNpp),
                ArrowArrayExtensions.WrapInFloat(saplingsPerHectare),
                ArrowArrayExtensions.WrapInFloat(branchCarbon),
                ArrowArrayExtensions.WrapInFloat(branchNitrogen),
                ArrowArrayExtensions.WrapInFloat(coarseRootCarbon),
                ArrowArrayExtensions.WrapInFloat(coarseRootNitrogen),
                ArrowArrayExtensions.WrapInFloat(fineRootCarbon),
                ArrowArrayExtensions.WrapInFloat(fineRootNitrogen),
                ArrowArrayExtensions.WrapInFloat(foliageCarbon),
                ArrowArrayExtensions.WrapInFloat(foliageNitrogen),
                ArrowArrayExtensions.WrapInFloat(regenerationCarbon),
                ArrowArrayExtensions.WrapInFloat(regenerationNitrogen),
                ArrowArrayExtensions.WrapInFloat(stemCarbon),
                ArrowArrayExtensions.WrapInFloat(stemNitrogen),
            };

            return new RecordBatch(schema, data, batchLength);
        }

        private static Schema CreateArrowSchema(string idFieldName)
        {
            List<Field> fields = new()
            {
                new(idFieldName, Int32Type.Default, false),
                new("year", Int32Type.Default, false),
                new("averageDbh", FloatType.Default, false),
                new("averageHeight", FloatType.Default, false),
                new("basalArea", FloatType.Default, false),
                new("lai", FloatType.Default, false),
                new("liveStemVolume", FloatType.Default, false),
                new("treeNpp", FloatType.Default, false),
                new("treeAbovegroundNpp", FloatType.Default, false),
                new("treesPerHectare", FloatType.Default, false),
                new("saplingCohorts", FloatType.Default, false),
                new("saplingMeanAge", FloatType.Default, false),
                new("saplingNpp", FloatType.Default, false),
                new("saplingsPerHectare", FloatType.Default, false),
                new("branchCarbon", FloatType.Default, false),
                new("branchNitrogen", FloatType.Default, false),
                new("coarseRootCarbon", FloatType.Default, false),
                new("coarseRootNitrogen", FloatType.Default, false),
                new("fineRootCarbon", FloatType.Default, false),
                new("fineRootNitrogen", FloatType.Default, false),
                new("foliageCarbon", FloatType.Default, false),
                new("foliageNitrogen", FloatType.Default, false),
                new("regenerationCarbon", FloatType.Default, false),
                new("regenerationNitrogen", FloatType.Default, false),
                new("stemCarbon", FloatType.Default, false),
                new("stemNitrogen", FloatType.Default, false)
            };

            Dictionary<string, string> metadata = new();
            return new Schema(fields, metadata);
        }

        protected override void ProcessRecord()
        {
            // there's no requirement to log resource unit and stand trajectories just because they're present
            Debug.Assert(this.Trajectory != null);
            if (String.IsNullOrWhiteSpace(this.ResourceUnitFile) == false)
            {
                IList<ResourceUnitTrajectory> trajectories = this.Trajectory.Output.ResourceUnitTrajectories;
                if (trajectories.Count == 0)
                {
                    throw new ParameterOutOfRangeException(nameof(this.ResourceUnitFile), "A resource unit file was specified but no resource unit trajectories were logged.");
                }
                this.WriteTrajectories(this.ResourceUnitFile, trajectories, "resourceUnit");
            }

            if (String.IsNullOrWhiteSpace(this.StandFile) == false)
            {
                IList<StandTrajectory> trajectories = this.Trajectory.Output.StandTrajectoriesByID.Values;
                if (trajectories.Count == 0)
                {
                    throw new ParameterOutOfRangeException(nameof(this.StandFile), "A stand file was specified but no stand trajectories were logged.");
                }
                this.WriteTrajectories(this.StandFile, trajectories, "stand");
            }
        }

        private void WriteTrajectories<TTrajectory>(string trajectoryFilePath, IList<TTrajectory> trajectories, string idFieldName) where TTrajectory : StandOrResourceUnitTrajectory
        {
            // for now, all weather time series should start in January of the first simulation year
            int calendarYearBeforeFirstSimulationTimestep = this.Trajectory!.Landscape.WeatherFirstCalendarYear - 1;
            Schema schema = WriteTrajectory.CreateArrowSchema(idFieldName);
            RecordBatch batch = WriteTrajectory.CreateArrowBatch<TTrajectory>(schema, trajectories, calendarYearBeforeFirstSimulationTimestep);

            using FileStream stream = new(trajectoryFilePath, FileMode.Create, FileAccess.Write, FileShare.None, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using ArrowFileWriter writer = new(stream, schema);
            writer.WriteStart();
            writer.WriteRecordBatch(batch);
            writer.WriteEnd();
        }
    }
}
