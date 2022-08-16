using System;
using System.Collections.Generic;

namespace iLand.Input.Tree
{
    public class IndividualTreeReader : TreeReader
    {
        public List<UInt16> AgeInYears { get; private init; }
        public List<float> DbhInCm { get; private init; }
        public List<float> GisX { get; private init; }
        public List<float> GisY { get; private init; }
        public List<float> HeightInM { get; private init; }
        public List<string> SpeciesID { get; private init; }
        public List<int> StandID { get; private init; }
        public List<int> TreeID { get; private init; }

        public IndividualTreeReader(string individualTreeFilePath)
            : base(individualTreeFilePath)
        {
            this.AgeInYears = new();
            this.DbhInCm = new();
            this.GisX = new();
            this.GisY = new();
            this.HeightInM = new();
            this.SpeciesID = new();
            this.StandID = new();
            this.TreeID = new();
        }
    }
}
