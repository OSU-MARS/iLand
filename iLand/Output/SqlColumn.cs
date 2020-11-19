using Microsoft.Data.Sqlite;

namespace iLand.Output
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

        public static SqlColumn CreateID()
        {
            return new SqlColumn("rid", "id of ressource unit (-1: no ids set)", SqliteType.Integer);
        }

        public static SqlColumn CreateResourceUnit()
        {
            return new SqlColumn("ru", "index of ressource unit", SqliteType.Integer);
        }

        public static SqlColumn CreateSpecies()
        {
            return new SqlColumn("species", "tree species", SqliteType.Text);
        }

        public static SqlColumn CreateYear() 
        { 
            return new SqlColumn("year", "simulation year", SqliteType.Integer); 
        }
    }
}
