using Microsoft.Data.Sqlite;
using System;

namespace iLand.Output
{
    public class SqlColumn
    {
        public SqliteType Datatype { get; init; }
        public string Description { get; init; }
        public string Name { get; init; }

        public SqlColumn(string name, string description, OutputDatatype datatype)
        {
            this.Name = name;
            this.Description = description;
            this.Datatype = datatype switch
            {
                OutputDatatype.Integer => SqliteType.Integer,
                OutputDatatype.Double => SqliteType.Real,
                OutputDatatype.String => SqliteType.Text,
                _ => throw new NotSupportedException() // blob
            };
        }

        public static SqlColumn CreateID()
        {
            return new SqlColumn("rid", "id of ressource unit (-1: no ids set)", OutputDatatype.Integer);
        }

        public static SqlColumn CreateResourceUnit()
        {
            return new SqlColumn("ru", "index of ressource unit", OutputDatatype.Integer);
        }

        public static SqlColumn CreateSpecies()
        {
            return new SqlColumn("species", "tree species", OutputDatatype.String);
        }

        public static SqlColumn CreateYear() 
        { 
            return new SqlColumn("year", "simulation year", OutputDatatype.Integer); 
        }
    }
}
