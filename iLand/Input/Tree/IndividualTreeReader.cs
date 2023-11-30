using iLand.Extensions;
using iLand.Tree;
using System;

namespace iLand.Input.Tree
{
    public class IndividualTreeReader : TreeReader
    {
        public int Count { get; protected set; }

        public UInt16[] AgeInYears { get; private set; }
        public float[] DbhInCm { get; private set; }
        public float[] GisX { get; private set; }
        public float[] GisY { get; private set; }
        public float[] HeightInM { get; private set; }
        public WorldFloraID[] SpeciesID { get; private set; }
        public UInt32[] StandID { get; private set; }
        public UInt32[] TreeID { get; private set; }

        public IndividualTreeReader(string individualTreeFilePath)
            : base(individualTreeFilePath)
        {
            this.AgeInYears = [];
            this.DbhInCm = [];
            this.GisX = [];
            this.GisY = [];
            this.HeightInM = [];
            this.SpeciesID = [];
            this.StandID = [];
            this.TreeID = [];
        }

        public int Capacity
        {
            get { return this.AgeInYears.Length; }
        }

        protected void Resize(int newSize)
        {
            this.AgeInYears = this.AgeInYears.Resize(newSize);
            this.DbhInCm = this.DbhInCm.Resize(newSize);
            this.GisX = this.GisX.Resize(newSize);
            this.GisY = this.GisY.Resize(newSize);
            this.HeightInM = this.HeightInM.Resize(newSize);
            this.SpeciesID = this.SpeciesID.Resize(newSize);
            this.StandID = this.StandID.Resize(newSize);
            this.TreeID = this.TreeID.Resize(newSize);
        }
    }
}
