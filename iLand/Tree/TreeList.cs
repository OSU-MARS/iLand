using iLand.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Tree
{
    public class TreeList
    {
        public int Count { get; set; }

        public UInt16[] AgeInYears { get; private set; }
        public float[] DbhInCm { get; private set; }
        public float[] HeightInM { get; private set; }
        public int[] StandID { get; private set; }
        public int[] TreeID { get; private set; }

        public TreeList(int capacity)
        {
            if ((capacity < 0) || (capacity % Simd128.Width32 != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity of " + capacity + " is not a positive integer multiple of SIMD width.");
            }

            this.Count = 0;
            this.Allocate(capacity);
        }

        public TreeList(TreeList other)
        {
            this.Allocate(other.Capacity);
            this.Count = other.Count;

            Array.Copy(other.AgeInYears, this.AgeInYears, this.Count);
            Array.Copy(other.DbhInCm, this.DbhInCm, this.Count);
            Array.Copy(other.HeightInM, this.HeightInM, this.Count);
            Array.Copy(other.StandID, this.StandID, this.Count);
            Array.Copy(other.TreeID, this.TreeID, this.Count);

        }

        public int Capacity
        {
            get { return this.DbhInCm.Length; }
        }

        [MemberNotNull(nameof(TreeList.AgeInYears), nameof(TreeList.DbhInCm), nameof(TreeList.HeightInM), nameof(TreeList.StandID), nameof(TreeList.TreeID))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.AgeInYears = Array.Empty<UInt16>();
                this.DbhInCm = Array.Empty<float>();
                this.HeightInM = Array.Empty<float>();
                this.StandID = Array.Empty<int>();
                this.TreeID = Array.Empty<int>();
            }
            else
            {
                this.AgeInYears = new UInt16[capacity];
                this.DbhInCm = new float[capacity];
                this.HeightInM = new float[capacity];
                this.StandID = new int[capacity];
                this.TreeID = new int[capacity];
            }
        }

        public virtual void Resize(int newSize)
        {
            if ((newSize < this.Count) || (newSize % Simd128.Width32 != 0)) // enforces positive size (unless a bug allows Count to become negative)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), "New size of " + newSize + " is smaller than the current number of live trees (" + this.Count + ") or is not an integer multiple of SIMD width.");
            }

            this.AgeInYears = this.AgeInYears.Resize(newSize);
            this.DbhInCm = this.DbhInCm.Resize(newSize);
            this.HeightInM = this.HeightInM.Resize(newSize); // updates this.Capacity
            this.StandID = this.StandID.Resize(newSize);
            this.TreeID = this.TreeID.Resize(newSize);
        }
    }
}
