using Apache.Arrow;

namespace iLand.Input
{
    public class ArrowBatch
    {
        protected static TArray GetArray<TArray>(string name, Schema schema, IArrowArray[] fields) where TArray : class, IArrowArray
        {
            return (TArray)fields[schema.GetFieldIndex(name)];
        }

        protected static TArray? MaybeGetArray<TArray>(string name, Schema schema, IArrowArray[] fields) where TArray : class, IArrowArray
        {
            if (schema.Fields.ContainsKey(name))
            {
                return (TArray)fields[schema.GetFieldIndex(name)];
            }

            return null;
        }
    }
}
