namespace iLand.Core
{
    internal interface ISetupResourceUnitInterface
    {
        /// setup of parameters specific for resource unit.
        /// this allows using spatially explicit parmater values.
        void SetupResourceUnit(ResourceUnit ru);
    }
}
