namespace iLand.core
{
    internal class TreeGrowthData
    {
        public double NPP; ///< total NPP (kg)
        public double NPP_above; ///< NPP aboveground (kg) (NPP - fraction roots), no consideration of tree senescence
        public double NPP_stem;  ///< NPP used for growth of stem (dbh,h)
        public double stress_index; ///< stress index used for mortality calculation

        public TreeGrowthData()
        {
            NPP = 0.0;
            NPP_above = 0.0;
            NPP_stem = 0.0;
            // TODO: stress_index not initialized in C++
        }
    }
}
