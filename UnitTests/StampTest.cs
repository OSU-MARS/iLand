using iLand.Tree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class StampTest : LandTest
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void ExportStampsToCsv()
        {
            // reader stamps
            TreeSpeciesStamps readerStamps = new();
            readerStamps.Load(Path.Combine(this.TestContext!.TestDeploymentDir, "readerstamp.bin"));
            //string stampExportPath = Path.Combine(this.TestContext!.TestDeploymentDir, "..", "..", "..", "..", "..", "R", "stamps");
            //readerStamps.Write(Path.Combine(stampExportPath, "readerstamp.csv"));

            // Europe
            string kalkalpenDirectoryPath = Path.Combine(LandTest.GetUnitTestDirectoryPath(this.TestContext!), "Kalkalpen", "lip");
            List<string> europeFileNames = new() { "abal", "acca", "acpl", "acps", "algl", "alin", "alvi", "bepe", "cabe", "casa", "coav", "fasy", "frex", "lade", "piab", "pice", "pini", "pisy", "poni", "potr", "psme", "qupe", "qupu", "quro", "rops", "saca", "soar", "soau", "tico", "tipl", "ulgl" };
            foreach (string baseName in europeFileNames)
            {
                TreeSpeciesStamps stamps = new();
                stamps.Load(Path.Combine(kalkalpenDirectoryPath, baseName + ".bin"));
                //stamps.Write(Path.Combine(stampExportPath, baseName + ".csv"));
            }

            // Pacific Northwest
            string elliottStampDirectoryPath = Path.Combine(LandTest.GetUnitTestDirectoryPath(this.TestContext!), "Elliott", "lip");
            List<string> pacificNorthwestFileNames = new() { "abam", "abgr", "abpr", "acma", "alru", "pipo", "pisi", "psme", "thpl", "tshe", "tsme" };
            foreach (string baseName in pacificNorthwestFileNames)
            {
                TreeSpeciesStamps stamps = new();
                stamps.Load(Path.Combine(elliottStampDirectoryPath, baseName + ".bin"));
                //if (String.Equals(baseName, "psme", StringComparison.OrdinalIgnoreCase))
                //{
                //    stamps.Write(Path.Combine(stampExportPath, baseName + "Europe.csv"));
                //}
                //else
                //{
                //    stamps.Write(Path.Combine(stampExportPath, baseName + ".csv"));
                //}
            }
        }
    }
}
