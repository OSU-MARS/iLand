using Microsoft.Data.Sqlite;
using System;

namespace iLand.Output
{
    public class SqlColumn
    {
        public SqliteType Datatype { get; private set; }
        public string Description { get; private set; }
        public string Name { get; private set; }

        public SqlColumn(string name, string description, OutputDatatype datatype)
        {
            this.Name = name;
            this.Description = description;
            this.Datatype = datatype switch
            {
                OutputDatatype.OutInteger => SqliteType.Integer,
                OutputDatatype.OutDouble => SqliteType.Real,
                OutputDatatype.OutString => SqliteType.Text,
                _ => throw new NotSupportedException() // blob
            };
        }

        public static SqlColumn CreateID()
        {
            return new SqlColumn("rid", "id of ressource unit (-1: no ids set)", OutputDatatype.OutInteger);
        }

        public static SqlColumn CreateResourceUnit()
        {
            return new SqlColumn("ru", "index of ressource unit", OutputDatatype.OutInteger);
        }

        public static SqlColumn CreateSpecies()
        {
            return new SqlColumn("species", "tree species", OutputDatatype.OutString);
        }

        public static SqlColumn CreateYear() 
        { 
            return new SqlColumn("year", "simulation year", OutputDatatype.OutInteger); 
        }
    }
}
