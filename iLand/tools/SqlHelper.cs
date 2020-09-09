using Microsoft.Data.Sqlite;

namespace iLand.tools
{
    /** @class SqlHelper
        @ingroup tools
        A helper class for simple execution of database commands.
        */
    internal class SqlHelper
    {
        /** execute 'query' against database 'database'. The first column of the first row are returned.
          A Null-Variant is returned, if the query has no results. */
        public static object queryValue(string query, SqliteConnection database)
        {
            SqliteCommand q = new SqliteCommand(query, database);
            return q.ExecuteScalar();
        }

        /** execute 'query' against database 'database'.
            Use for insert, update, ... statements without return values. */
        public static bool executeSql(string query, SqliteConnection database)
        {
            SqliteCommand q = new SqliteCommand(query, database);
            q.ExecuteNonQuery();
            return true;
        }
    }
}
