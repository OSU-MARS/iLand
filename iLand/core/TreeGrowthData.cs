namespace iLand.core
{
    internal class TreeGrowthData
    {
        public double NppAboveground { get; set; } ///< NPP aboveground (kg) (NPP - fraction roots), no consideration of tree senescence
        public double NppStem { get; set; }  ///< NPP used for growth of stem (dbh,h)
        public double NppTotal { get; set; } ///< total NPP (kg)
        public double StressIndex { get; set; } ///< stress index used for mortality calculation

        public TreeGrowthData()
        {
            this.NppTotal = 0.0;
            this.NppAboveground = 0.0;
            this.NppStem = 0.0;
            this.StressIndex = 0.0;
        }
    }
}
