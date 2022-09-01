namespace iLand.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly int[] DaysInNonLeapYearMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private static readonly int[] MidmonthDayIndicesNonLeapYear = { 16, 45, 74, 105, 136, 166, 196, 228, 258, 288, 319, 350 }; // see iLand.R

        public static (int monthIndex, int dayOfMonthIndex) DayOfYearToDayOfMonth(int dayOfYearIndex, bool isLeapYear)
        {
            int dayOfMonthIndex;
            int monthIndex;
            if (isLeapYear)
            {
                // binary tree for leap years
                if (dayOfYearIndex < 245) // January-August
                {
                    if (dayOfYearIndex < 121) // January-April
                    {
                        if (dayOfYearIndex < 60) // January, February
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
                            if (dayOfYearIndex < 91)
                            {
                                dayOfMonthIndex = dayOfYearIndex - 60; // March
                                monthIndex = 2;
                            }
                            else
                            {
                                dayOfMonthIndex = dayOfYearIndex - 91; // April
                                monthIndex = 3;
                            }
                        }
                    }
                    else // May-August
                    {
                        if (dayOfYearIndex < 182) // May, June
                        {
                            if (dayOfYearIndex < 152)
                            {
                                dayOfMonthIndex = dayOfYearIndex - 121; // May
                                monthIndex = 4;
                            }
                            else
                            {
                                dayOfMonthIndex = dayOfYearIndex - 152; // June
                                monthIndex = 5;
                            }
                        }
                        else // July, August
                        {
                            if (dayOfYearIndex < 213)
                            {
                                dayOfMonthIndex = dayOfYearIndex - 182; // July
                                monthIndex = 6;
                            }
                            else
                            {
                                dayOfMonthIndex = dayOfYearIndex - 213; // August
                                monthIndex = 7;
                            }
                        }
                    }
                }
                else // September-December
                {
                    if (dayOfYearIndex < 305) // September, October
                    {
                        if (dayOfYearIndex < 274)
                        {
                            dayOfMonthIndex = dayOfYearIndex - 244; // September
                            monthIndex = 8;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 274; // October
                            monthIndex = 9;
                        }
                    }
                    else // November, December
                    {
                        if (dayOfYearIndex < 335)
                        {
                            dayOfMonthIndex = dayOfYearIndex - 305; // November
                            monthIndex = 10;
                        }
                        else
                        {
                            dayOfMonthIndex = dayOfYearIndex - 335; // December
                            monthIndex = 11;
                        }
                    }
                }
            }
            else
            {
                // binary tree for non-leap years
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
            }

            return (monthIndex, dayOfMonthIndex);
        }

        // same binary trees as DayOfYearToDayOfMonth()
        public static int DayOfYearToMonthIndex(int dayOfYearIndex, bool isLeapYear)
        {
            if (isLeapYear)
            {
                // binary tree for leap years
                if (dayOfYearIndex < 245) // January-August
                {
                    if (dayOfYearIndex < 121) // January-April
                    {
                        if (dayOfYearIndex < 60) // January, February
                        {
                            if (dayOfYearIndex < 31)
                            {
                                return 0; // January
                            }
                            else
                            {
                                return 1; // February
                            }
                        }
                        else // March, April
                        {
                            if (dayOfYearIndex < 91)
                            {
                                return 2; // March
                            }
                            else
                            {
                                return 3; // April
                            }
                        }
                    }
                    else // May-August
                    {
                        if (dayOfYearIndex < 182) // May, June
                        {
                            if (dayOfYearIndex < 152)
                            {
                                return 4; // May
                            }
                            else
                            {
                                return 5; // June
                            }
                        }
                        else // July, August
                        {
                            if (dayOfYearIndex < 213)
                            {
                                return 6; // July
                            }
                            else
                            {
                                return 7; // August
                            }
                        }
                    }
                }
                else // September-December
                {
                    if (dayOfYearIndex < 305) // September, October
                    {
                        if (dayOfYearIndex < 274)
                        {
                            return 8; // September
                        }
                        else
                        {
                            return 9; // October
                        }
                    }
                    else // November, December
                    {
                        if (dayOfYearIndex < 335)
                        {
                            return 10; // November
                        }
                        else
                        {
                            return 11; // December
                        }
                    }
                }
            }
            else
            {
                // binary tree for non-leap years
                if (dayOfYearIndex < 244) // January-August
                {
                    if (dayOfYearIndex < 120) // January-April
                    {
                        if (dayOfYearIndex < 59) // January, February
                        {
                            if (dayOfYearIndex < 31)
                            {
                                return 0; // January
                            }
                            else
                            {
                                return 1; // February
                            }
                        }
                        else // March, April
                        {
                            if (dayOfYearIndex < 90)
                            {
                                return 2; // March
                            }
                            else
                            {
                                return 3; // April
                            }
                        }
                    }
                    else // May-August
                    {
                        if (dayOfYearIndex < 181) // May, June
                        {
                            if (dayOfYearIndex < 151)
                            {
                                return 4; // May
                            }
                            else
                            {
                                return 5; // June
                            }
                        }
                        else // July, August
                        {
                            if (dayOfYearIndex < 212)
                            {
                                return 6; // July
                            }
                            else
                            {
                                return 7; // August
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
                            return 8; // September
                        }
                        else
                        {
                            return 9; // October
                        }
                    }
                    else // November, December
                    {
                        if (dayOfYearIndex < 334)
                        {
                            return 10; // November
                        }
                        else
                        {
                            return 11; // December
                        }
                    }
                }
            }
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
            return isLeapYear ? Constant.Time.DaysInLeapYear : Constant.Time.DaysInYear;
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
