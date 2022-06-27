#nullable disable
using iLand.Input;
using Microsoft.Data.Sqlite;
using System;
using System.Globalization;

namespace iLand.Tool
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
            if (String.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new NotSupportedException(nameof(this.ConnectionString) + " is empty.");
            }
            if (String.IsNullOrWhiteSpace(this.FileName))
            {
                throw new NotSupportedException(nameof(this.FileName) + " is empty.");
            }
            if (String.IsNullOrWhiteSpace(this.TableName))
            {
                throw new NotSupportedException(nameof(this.TableName) + " is empty.");
            }

            this.mExpYear.SetExpression(this.Year);
            this.mExpMonth.SetExpression(this.Month);
            this.mExpDay.SetExpression(this.Day);

            this.mExpTemp.SetExpression(this.Temp);
            this.mExpMinTemp.SetExpression(this.MinTemp);
            this.mExpPrec.SetExpression(this.Prec);
            this.mExpRad.SetExpression(this.Rad);
            this.mExpVpd.SetExpression(this.Vpd);

            // load file
            // TODO: validate column order is year, month, day, temp, min_temp, prec, rad, vpd
            using CsvFile climateFile = new(this.FileName);

            SqliteConnectionStringBuilder connectionString = new()
            {
                DataSource = this.ConnectionString
            };
            using SqliteConnection sqlConnection = new(connectionString.ConnectionString);
            sqlConnection.Open();
            using SqliteTransaction transaction = sqlConnection.BeginTransaction();

            // prepare output database
            using SqliteCommand dropIfExists = new(String.Format("drop table if exists {0}", TableName), sqlConnection, transaction);
            dropIfExists.ExecuteNonQuery();
            using SqliteCommand create = new(String.Format("CREATE TABLE {0} (year INTEGER, month INTEGER, day INTEGER, " +
                                                           "temp REAL, min_temp REAL, prec REAL, rad REAL, vpd REAL)", TableName),
                                             sqlConnection, transaction);
            create.ExecuteNonQuery();

            // prepare insert statement
            using SqliteCommand insert = new(String.Format("insert into {0} (year, month, day, temp, min_temp, prec, rad, vpd) values (?,?,?, ?,?,?,?,?)", TableName),
                                             sqlConnection, transaction);
            climateFile.Parse((string[] row) =>
            {
                // fetch values from input file
                for (int columnIndex = 0; columnIndex < climateFile.ColumnCount; ++columnIndex)
                {
                    double value = Double.Parse(row[columnIndex]);
                    // store value in each of the expression variables
                    for (int j = 0; j < 8; j++)
                    {
                        this.mVars[10 * j + columnIndex] = value;
                    }
                }

                // calculate new values....
                int year = (int)this.mExpYear.Execute();
                int month = (int)this.mExpMonth.Execute();
                int day = (int)this.mExpDay.Execute();
                double temp = this.mExpTemp.Execute();
                double min_temp = this.mExpMinTemp.Execute();
                double prec = this.mExpPrec.Execute();
                double rad = this.mExpRad.Execute();
                double vpd = this.mExpVpd.Execute();

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
            });

            transaction.Commit();
            // Debug.WriteLine("run: processing complete. " + file.RowCount + " rows inserted.");
        }
    }
}
