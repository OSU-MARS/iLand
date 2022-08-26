using System;
using System.Diagnostics;

namespace iLand.Test
{
    internal class ExpectedResourceUnitTrajectory
    {
        public float[] GppByYear { get; init; }
        public float[] NppByYear { get; init; }
        public float[] StemVolumeByYear { get; init; }

        public ExpectedResourceUnitTrajectory()
        {
            this.GppByYear = Array.Empty<float>();
            this.NppByYear = Array.Empty<float>();
            this.StemVolumeByYear = Array.Empty<float>();
        }

        public int LengthInYears
        {
            get
            {
                Debug.Assert((this.GppByYear.Length == this.NppByYear.Length) && (this.GppByYear.Length == this.StemVolumeByYear.Length));
                return this.GppByYear.Length;
            }
        }
    }
}
