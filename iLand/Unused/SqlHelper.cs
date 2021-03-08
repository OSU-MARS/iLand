using Microsoft.Data.Sqlite;

namespace iLand.Tools
{
    /** @class SqlHelper
        A helper class for simple execution of database commands.
        */
    internal class SqlHelper
    {
        /** execute 'query' against database 'database'. The first column of the first row are returned.
          A Null-Variant is returned, if the query has no results. */
        public static object QueryValue(string query, SqliteConnection database)
        {
            SqliteCommand q = new(query, database);
            return q.ExecuteScalar();
        }

        /** execute 'query' against database 'database'.
            Use for insert, update, ... statements without return values. */
        public static bool ExecuteSql(string query, SqliteConnection database)
        {
            SqliteCommand q = new(query, database);
            q.ExecuteNonQuery();
            return true;
        }
    }
}
