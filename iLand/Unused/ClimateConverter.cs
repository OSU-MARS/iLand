#nullable disable
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;

namespace iLand.Tools
{
    /** @class ClimateConverter
        Converts text-file-based data into the iLand climate data format.
        For the iLand format see the wiki (ClimateFormat). For each column (i.e. year,month, day,
        temp, prec, rad, vpd), an expression providing access to the columns of the input file calculates
        the respective output value. Propertes tableName and fileName define the input file and the
        name of the output table (located in the "climate"-database of iLand) respectively.
        */
    /** @group script
        @class ClimateConverter
        This is the Scripting related documentation for the ClimateConverter tool.
        */
    internal class ClimateConverter
    {
        private readonly double[] mVars;

        private readonly Expression mExpYear;
        private readonly Expression mExpMonth;
        private readonly Expression mExpDay;
        private readonly Expression mExpTemp;
        private readonly Expression mExpMinTemp;
        private readonly Expression mExpPrec;
        private readonly Expression mExpRad;
        private readonly Expression mExpVpd;

        // getters
        public string ConnectionString { get; set; }
        public string FileName { get; set; }
        public string TableName { get; set; }
        public string Year { get; set; }
        public string Month { get; set; }
        public string Day { get; set; }
        public string Temp { get; set; }
        public string MinTemp { get; set; }
        public string Prec { get; set; }
        public string Rad { get; set; }
        public string Vpd { get; set; }

        public ClimateConverter()
        {
            this.mExpYear = new Expression();
            this.mExpMonth = new Expression();
            this.mExpDay = new Expression();
            this.mExpTemp = new Expression();
            this.mExpMinTemp = new Expression();
            this.mExpPrec = new Expression();
            this.mExpRad = new Expression();
            this.mExpVpd = new Expression();

            this.mVars = new double[100];

            this.BindExpression(mExpYear, 0);
            this.BindExpression(mExpMonth, 1);
            this.BindExpression(mExpDay, 2);

            this.BindExpression(mExpTemp, 3);
            this.BindExpression(mExpMinTemp, 4);

            this.BindExpression(mExpPrec, 5);
            this.BindExpression(mExpRad, 6);
            this.BindExpression(mExpVpd, 7);
        }

        private void BindExpression(Expression expr, int index)
        {
            expr.SetExpression(index.ToString(CultureInfo.InvariantCulture)); // "cX" is the default expression
            for (int i = 0; i < 10; i++)
            {
                mVars[index * 10 + i] = expr.AddVariable(i.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void ConvertFileToDatabase()
        {
            mExpYear.SetExpression(this.Year);
            mExpMonth.SetExpression(this.Month);
            mExpDay.SetExpression(this.Day);

            mExpTemp.SetExpression(this.Temp);
            mExpMinTemp.SetExpression(this.MinTemp);
            mExpPrec.SetExpression(this.Prec);
            mExpRad.SetExpression(this.Rad);
            mExpVpd.SetExpression(this.Vpd);

            if (String.IsNullOrEmpty(this.ConnectionString))
            {
                throw new NotSupportedException("Database is empty.");
            }
            if (String.IsNullOrWhiteSpace(this.TableName))
            {
                throw new NotSupportedException("Invalid table name.");
            }
            if (String.IsNullOrEmpty(this.FileName))
            {
                throw new NotSupportedException("Empty filename.");
            }

            // load file
            CsvFile file = new CsvFile();
            file.LoadFile(this.FileName);
            if (file.RowCount < 2)
            {
                throw new NotSupportedException("File '" + this.FileName + "' is empty.");
            }

            SqliteConnectionStringBuilder connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = this.ConnectionString
            };
            using SqliteConnection db = new SqliteConnection(connectionString.ConnectionString);
            db.Open();
            using (SqliteTransaction transaction = db.BeginTransaction())
            {
                // prepare output database
                using SqliteCommand dropIfExists = new SqliteCommand(String.Format("drop table if exists {0}", TableName), db, transaction);
                dropIfExists.ExecuteNonQuery();
                using SqliteCommand create = new SqliteCommand(String.Format("CREATE TABLE {0} ( year INTEGER, month INTEGER, day INTEGER, " +
                                                                             "temp REAL, min_temp REAL, prec REAL, rad REAL, vpd REAL)", TableName),
                                                               db, transaction);
                create.ExecuteNonQuery();

                // prepare insert statement
                using SqliteCommand insert = new SqliteCommand(String.Format("insert into {0} (year, month, day, temp, min_temp, prec, rad, vpd) values (?,?,?, ?,?,?,?,?)", TableName),
                                                               db, transaction);
                // do this for each row
                for (int row = 0; row < file.RowCount; row++)
                {
                    // fetch values from input file
                    for (int columnIndex = 0; columnIndex < file.ColumnCount; ++columnIndex)
                    {
                        double value = Double.Parse(file.GetValue(columnIndex, row));
                        // store value in each of the expression variables
                        for (int j = 0; j < 8; j++)
                        {
                            mVars[j * 10 + columnIndex] = value; // store in the locataion mVars[x] points to.
                        }
                    }

                    // calculate new values....
                    int year = (int)mExpYear.Execute();
                    int month = (int)mExpMonth.Execute();
                    int day = (int)mExpDay.Execute();
                    double temp = mExpTemp.Execute();
                    double min_temp = mExpMinTemp.Execute();
                    double prec = mExpPrec.Execute();
                    double rad = mExpRad.Execute();
                    double vpd = mExpVpd.Execute();

                    // bind values
                    insert.Parameters[0].Value = year;
                    insert.Parameters[1].Value = month;
                    insert.Parameters[2].Value = day;
                    insert.Parameters[3].Value = temp;
                    insert.Parameters[4].Value = min_temp;
                    insert.Parameters[5].Value = prec;
                    insert.Parameters[6].Value = rad;
                    insert.Parameters[7].Value = vpd;
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            Debug.WriteLine("run: processing complete. " + file.RowCount + " rows inserted.");
        }
    }
}
