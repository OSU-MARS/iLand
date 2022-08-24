using iLand.Tree;
using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace iLand.Cmdlets
{
    [Cmdlet(VerbsData.Export, "LightStamps")]
    public class ExportLightStamps : Cmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string CsvDirectory { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ProjectLip { get; set; }

        [Parameter]
        public SwitchParameter ReaderStamps { get; set; }

        public ExportLightStamps()
        {
            this.CsvDirectory = String.Empty;
            this.ProjectLip = String.Empty;
            this.ReaderStamps = false;
        }

        protected override void ProcessRecord()
        {
            // species files
            string[] speciesLightStampFiles = Directory.GetFiles(this.ProjectLip, "*.bin");
            for (int speciesIndex = 0; speciesIndex < speciesLightStampFiles.Length; ++speciesIndex)
            {
                string? baseName = Path.GetFileNameWithoutExtension(speciesLightStampFiles[speciesIndex]);
                if (String.IsNullOrWhiteSpace(baseName))
                {
                    throw new NotSupportedException("Species light stamp file '" + speciesLightStampFiles[speciesIndex] + "' lacks a file name.");
                }

                TreeSpeciesStamps stamps = new(Path.Combine(this.ProjectLip, baseName + ".bin"));
                stamps.WriteToCsv(Path.Combine(this.CsvDirectory, baseName + ".csv"));
            }

            // reader stamps
            if (this.ReaderStamps)
            {
                string? iLandAssemblyFilePath = Path.GetDirectoryName(typeof(TreeSpeciesSet).Assembly.Location);
                Debug.Assert(iLandAssemblyFilePath != null);
                TreeSpeciesStamps readerStamps = new(Path.Combine(iLandAssemblyFilePath, Constant.File.ReaderStampFileName));
                readerStamps.WriteToCsv(Path.Combine(this.CsvDirectory, "readerstamp.csv"));
            }
        }
    }
}
