using System;

namespace iLand.output
{
    internal class OutputColumn
    {
        private string mName;
        private string mDescription;
        public OutputDatatype mDatatype;

        public static OutputColumn year() { return new OutputColumn("year", "simulation year", OutputDatatype.OutInteger); }
        public static OutputColumn species() { return new OutputColumn("species", "tree species", OutputDatatype.OutString); }
        public static OutputColumn ru() { return new OutputColumn("ru", "index of ressource unit", OutputDatatype.OutInteger); }
        public static OutputColumn id() { return new OutputColumn("rid", "id of ressource unit (-1: no ids set)", OutputDatatype.OutInteger); }
        public string name() { return mName; }
        public string description() { return mDescription; }

        public OutputColumn(string name, string description, OutputDatatype datatype)
        {
            mName = name;
            mDescription = description;
            mDatatype = datatype;
        }

        public string datatype() 
        { 
            switch (mDatatype) 
            { 
                case OutputDatatype.OutInteger: return "integer"; 
                case OutputDatatype.OutDouble: return "double"; 
                case OutputDatatype.OutString: return "string";
                default: throw new NotSupportedException();
            } 
        }
    }
}
