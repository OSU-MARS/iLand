// C++/core/{ tree.h }
namespace iLand.Tree
{
    public class TreeGrowthData
    {
        public float NppAboveground { get; set; } // NPP aboveground (kg) (NPP - fraction roots), no consideration of tree senescence
        public float NppStem { get; set; }  // NPP used for growth of stem (dbh,h)
        public float NppTotal { get; set; } // total NPP (kg)
        public float StressIndex { get; set; } // stress index used for mortality calculation

        public TreeGrowthData()
        {
            this.NppTotal = 0.0F;
            this.NppAboveground = 0.0F;
            this.NppStem = 0.0F;
            this.StressIndex = 0.0F;
        }
    }
}
