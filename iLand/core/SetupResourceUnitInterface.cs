namespace iLand.core
{
    internal interface SetupResourceUnitInterface
    {
        /// setup of parameters specific for resource unit.
        /// this allows using spatially explicit parmater values.
        void setupResourceUnit(ResourceUnit ru);
    }
}
