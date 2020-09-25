using System;

namespace iLand.output
{
    internal class OutputColumn
    {
        public OutputDatatype mDatatype;
        public string Name { get; private set; }
        public string Description { get; private set; }

        public OutputColumn(string name, string description, OutputDatatype datatype)
        {
            Name = name;
            Description = description;
            mDatatype = datatype;
        }

        public static OutputColumn CreateID()
        {
            return new OutputColumn("rid", "id of ressource unit (-1: no ids set)", OutputDatatype.OutInteger);
        }

        public static OutputColumn CreateResourceUnit()
        {
            return new OutputColumn("ru", "index of ressource unit", OutputDatatype.OutInteger);
        }

        public static OutputColumn CreateSpecies()
        {
            return new OutputColumn("species", "tree species", OutputDatatype.OutString);
        }

        public static OutputColumn CreateYear() 
        { 
            return new OutputColumn("year", "simulation year", OutputDatatype.OutInteger); 
        }

        public string Datatype() 
        {
            return mDatatype switch
            {
                OutputDatatype.OutInteger => "integer",
                OutputDatatype.OutDouble => "double",
                OutputDatatype.OutString => "string",
                _ => throw new NotSupportedException(),
            };
        }
    }
}
