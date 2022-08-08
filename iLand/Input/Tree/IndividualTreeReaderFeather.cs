using Apache.Arrow;
using Apache.Arrow.Ipc;
using System.IO;

namespace iLand.Input.Tree
{
    public class IndividualTreeReaderFeather : IndividualTreeReader
    {
        public IndividualTreeReaderFeather(string individualTreeFilePath)
            : base(individualTreeFilePath)
        {
            using FileStream individualTreeStream = new(individualTreeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader individualTreeFile = new(individualTreeStream); // ArrowFileReader.IsFileValid is false until a batch is read

            for (RecordBatch? batch = individualTreeFile.ReadNextRecordBatch(); batch != null; batch = individualTreeFile.ReadNextRecordBatch())
            {
                IndividualTreeArrowBatch fields = new(batch);
                for (int treeIndex = 0; treeIndex < batch.Length; ++treeIndex)
                {
                    this.SpeciesID.Add(fields.Species.GetString(treeIndex));
                    this.DbhInCm.Add(fields.DbhInCm.Values[treeIndex]);
                    this.HeightInM.Add(fields.HeightInM.Values[treeIndex]);
                    this.GisX.Add(fields.GisX.Values[treeIndex]);
                    this.GisY.Add(fields.GisY.Values[treeIndex]);

                    int age = 0;
                    if (fields.AgeInYears != null)
                    {
                        age = fields.AgeInYears.Values[treeIndex];
                    }
                    this.AgeInYears.Add(age);

                    int standID = Constant.DefaultStandID;
                    if (fields.StandID != null)
                    {
                        standID = fields.StandID.Values[treeIndex];
                    }
                    this.StandID.Add(standID);

                    int tag = this.Tag.Count;
                    if (fields.Tag != null)
                    {
                        tag = fields.Tag.Values[treeIndex];
                    }
                    this.Tag.Add(tag);
                }
            }
        }
    }
}
