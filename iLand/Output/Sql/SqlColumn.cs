using Microsoft.Data.Sqlite;

namespace iLand.Output.Sql
{
    public class SqlColumn
    {
        public string Description { get; private init; }
        public string Name { get; private init; }
        public SqliteType SqlType { get; private init; }

        public SqlColumn(string name, string description, SqliteType sqlType)
        {
            this.Name = name;
            this.Description = description;
            this.SqlType = sqlType;
        }

        public static SqlColumn CreateResourceUnitID()
        {
            return new SqlColumn("resourceUnit", "ID of resource unit (-1: no ids set).", SqliteType.Integer);
        }

        public static SqlColumn CreateTreeSpeciesID()
        {
            return new SqlColumn("species", "Tree species' World Flora Online identifier.", SqliteType.Integer);
        }

        public static SqlColumn CreateYear() 
        { 
            return new SqlColumn("year", "Calendar year.", SqliteType.Integer); 
        }
    }
}
