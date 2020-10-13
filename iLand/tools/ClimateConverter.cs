using iLand.Core;
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
        public string FileName { get; set; }
        public string TableName { get; set; }
        public string Database { get; set; }
        public bool Captions { get; set; }
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
            this.Captions = true;
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

        public void Run(Model model)
        {
            mExpYear.SetExpression(Year);
            mExpMonth.SetExpression(Month);
            mExpDay.SetExpression(Day);

            mExpTemp.SetExpression(Temp);
            mExpMinTemp.SetExpression(MinTemp);
            mExpPrec.SetExpression(Prec);
            mExpRad.SetExpression(Rad);
            mExpVpd.SetExpression(Vpd);

            if (String.IsNullOrEmpty(Database))
            {
                throw new NotSupportedException("ClimateConverter: database is empty!");
            }
            if (String.IsNullOrWhiteSpace(TableName))
            {
                throw new NotSupportedException("run: invalid table name.");
            }
            if (String.IsNullOrEmpty(FileName))
            {
                Debug.WriteLine("run: empty filename.");
                return;
            }

            // load file
            CsvFile file = new CsvFile()
            {
                HasCaptions = Captions
            };
            file.LoadFile(FileName);
            if (file.RowCount == 0)
            {
                Debug.WriteLine("run: cannot load file: " + FileName);
                return;
            }

            SqliteConnectionStringBuilder connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = Database
            };
            using SqliteConnection db = new SqliteConnection(connectionString.ConnectionString);
            db.Open();
            using (SqliteTransaction transaction = db.BeginTransaction())
            {
                // prepare output database
                SqliteCommand dropIfExists = new SqliteCommand(String.Format("drop table if exists {0}", TableName), db, transaction);
                dropIfExists.ExecuteNonQuery();
                SqliteCommand create = new SqliteCommand(String.Format("CREATE TABLE {0} ( year INTEGER, month INTEGER, day INTEGER, " +
                                                                        "temp REAL, min_temp REAL, prec REAL, rad REAL, vpd REAL)", TableName),
                                                         db, transaction);
                create.ExecuteNonQuery();

                // prepare insert statement
                SqliteCommand insert = new SqliteCommand(String.Format("insert into {0} (year, month, day, temp, min_temp, prec, rad, vpd) values (?,?,?, ?,?,?,?,?)", TableName),
                                                         db, transaction);
                // do this for each row
                for (int row = 0; row < file.RowCount; row++)
                {
                    // fetch values from input file
                    for (int col = 0; col < file.ColCount; col++)
                    {
                        double value = Double.Parse(file.Value(row, col));
                        // store value in each of the expression variables
                        for (int j = 0; j < 8; j++)
                        {
                            mVars[j * 10 + col] = value; // store in the locataion mVars[x] points to.
                        }
                    }

                    // calculate new values....
                    int year = (int)mExpYear.Execute(model);
                    int month = (int)mExpMonth.Execute(model);
                    int day = (int)mExpDay.Execute(model);
                    double temp = mExpTemp.Execute(model);
                    double min_temp = mExpMinTemp.Execute(model);
                    double prec = mExpPrec.Execute(model);
                    double rad = mExpRad.Execute(model);
                    double vpd = mExpVpd.Execute(model);

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
