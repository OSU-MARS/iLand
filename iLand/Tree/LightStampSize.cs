namespace iLand.Tree
{
    /// <summary>
    /// <see cref="LightStampSize"/>  defines different light grid sizes for stamps.
    /// </summary>
    /// <remarks>
    /// The numeric value is the dimension of the stamp in light grid cells.
    /// </remarks>
    public enum LightStampSize
    { 
        Grid4x4 = 4, 
        Grid8x8 = 8, 
        Grid12x12 = 12, 
        Grid16x16 = 16, 
        Grid24x24 = 24, 
        Grid32x32 = 32, 
        Grid48x48 = 48, 
        Grid64x64 = 64 // update Constant.Grid.MaxLightStampSize if stamps larger than 64x64 are added
    }
}
