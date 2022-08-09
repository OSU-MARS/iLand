namespace iLand.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly int[] DaysInNonLeapYearMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private static readonly int[] MidmonthDayIndicesNonLeapYear = { 16, 45, 74, 105, 136, 166, 196, 228, 258, 288, 319, 350 }; // see iLand.R

        public static (int monthIndex, int dayOfMonthIndex) DayOfYearToDayOfMonth(int dayOfYearIndex)
        {
            // binary tree
            int dayOfMonthIndex;
            int monthIndex;
            if (dayOfYearIndex < 244) // January-August
            {
                if (dayOfYearIndex < 120) // January-April
                {
                    if (dayOfYearIndex < 59) // January, February
                    {
                        if (dayOfYearIndex < 31)
                        {
                            dayOfMonthIndex = dayOfYearIndex; // January
                            monthIndex = 0;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 31; // February
                            monthIndex = 1;
                        }
                    }
                    else // March, April
                    {
                        if (dayOfYearIndex < 90)
                        {
                            dayOfMonthIndex = dayOfYearIndex - 59; // March
                            monthIndex = 2;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 90; // April
                            monthIndex = 3;
                        }
                    }
                }
                else // May-August
                {
                    if (dayOfYearIndex < 181) // May, June
                    {
                        if (dayOfYearIndex < 151)
                        {
                            dayOfMonthIndex = dayOfYearIndex - 120; // May
                            monthIndex = 4;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 151; // June
                            monthIndex = 5;
                        }
                    }
                    else // July, August
                    {
                        if (dayOfYearIndex < 212)
                        {
                            dayOfMonthIndex = dayOfYearIndex - 181; // July
                            monthIndex = 6;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 212; // August
                            monthIndex = 7;
                        }
                    }
                }
            }
            else // September-December
            {
                if (dayOfYearIndex < 304) // September, October
                {
                    if (dayOfYearIndex < 273)
                    {
                        dayOfMonthIndex = dayOfYearIndex - 243; // September
                        monthIndex = 8;
                    }
                    else
                    {
                        dayOfMonthIndex = dayOfYearIndex - 273; // October
                        monthIndex = 9;
                    }
                }
                else // November, December
                {
                    if (dayOfYearIndex < 334)
                    {
                        dayOfMonthIndex = dayOfYearIndex - 304; // November
                        monthIndex = 10;
                    }
                    else
                    {
                        dayOfMonthIndex = dayOfYearIndex - 334; // December
                        monthIndex = 11;
                    }
                }
            }

            return (monthIndex, dayOfMonthIndex);
        }

        public static int GetDaysInMonth(int monthIndex, bool isLeapYear)
        {
            int daysInMonth = DateTimeExtensions.DaysInNonLeapYearMonth[monthIndex];
            if ((monthIndex == 1) && isLeapYear)
            {
                daysInMonth = 29;
            }
            return daysInMonth;
        }

        public static int GetDaysInYear(bool isLeapYear)
        {
            return isLeapYear ? Constant.DaysInLeapYear : Constant.DaysInYear;
        }

        public static int GetMidmonthDayIndex(int monthIndex, bool isLeapYear)
        {
            int midmonthDayIndex = DateTimeExtensions.MidmonthDayIndicesNonLeapYear[monthIndex];
            if (isLeapYear && (monthIndex > 0))
            {
                ++midmonthDayIndex;
            }
            return midmonthDayIndex;
        }
    }
}
