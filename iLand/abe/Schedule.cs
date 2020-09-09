using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.abe
{
    internal class Schedule
    {
        // some stuffs
        public int tmin; int tmax; int topt;
        public double tminrel; double tmaxrel; double toptrel;
        public bool force_execution;
        // repeating
        public int repeat_interval;
        public bool repeat;
        public bool absolute;

        public Schedule(QJSValue js_value)
        {
            clear();
            setup(js_value);
        }

        public void clear()
        {
            tmin = tmax = topt = -1;
            tminrel = tmaxrel = toptrel = -1.0;
            force_execution = false;
            repeat_interval = -1;
            repeat = false;
            absolute = false;
        }

        public void setup(QJSValue js_value)
        {
            clear();
            if (js_value.isObject())
            {
                tmin = FMSTP.valueFromJs(js_value, "min", "-1").toInt();
                tmax = FMSTP.valueFromJs(js_value, "max", "-1").toInt();
                topt = FMSTP.valueFromJs(js_value, "opt", "-1").toInt();
                tminrel = FMSTP.valueFromJs(js_value, "minRel", "-1").toNumber();
                tmaxrel = FMSTP.valueFromJs(js_value, "maxRel", "-1").toNumber();
                toptrel = FMSTP.valueFromJs(js_value, "optRel", "-1").toNumber();
                repeat_interval = FMSTP.valueFromJs(js_value, "repeatInterval", "1").toInt();
                // switches
                force_execution = FMSTP.boolValueFromJs(js_value, "force", false);
                repeat = FMSTP.boolValueFromJs(js_value, "repeat", false);
                absolute = FMSTP.boolValueFromJs(js_value, "absolute", false);
                if (!repeat)
                {
                    if (tmin > -1 && tmax > -1 && topt == -1)
                    {
                        topt = (tmax + tmin) / 2;
                    }
                    if (tmin > -1 && tmax > -1 && topt > -1 && (topt < tmin || topt > tmax))
                    {
                        throw new NotSupportedException(String.Format("Error in setting up schedule: 'opt' either missing or out of range: %1", FomeScript.JStoString(js_value)));
                    }
                    if (tminrel > -1 && tmaxrel > -1 && toptrel > -1 && (toptrel < tminrel || toptrel > tmaxrel))
                    {
                        throw new NotSupportedException(String.Format("Error in setting up schedule: 'opt' either missing or out of range: %1", FomeScript.JStoString(js_value)));
                    }
                    if (tminrel * tmaxrel < 0.0 || tmin * tmax < 0.0)
                    {
                        throw new NotSupportedException(String.Format("Error in setting up schedule: min and max required: %1", FomeScript.JStoString(js_value)));
                    }

                    if (topt == -1 && toptrel == -1.0)
                    {
                        throw new NotSupportedException(String.Format("Error in setting up schedule: neither 'opt' nor 'optRel' point can be derived in: %1", FomeScript.JStoString(js_value)));
                    }
                }

            }
            else if (js_value.isNumber())
            {
                topt = (int)js_value.toNumber();
            }
            else
            {
                throw new NotSupportedException(String.Format("Error in setting up schedule/timing. Invalid javascript object: %1", FomeScript.JStoString(js_value)));
            }
        }

        public string dump()
        {
            if (repeat)
            {
                return String.Format("schedule: Repeating every %1 years.", repeat_interval);
            }
            else
            {
                return String.Format("schedule: tmin/topt/tmax %1/%2/%3\nrelative: min/opt/max %4/%5/%6\nforce: %7", tmin, topt, tmax,
                        tminrel, toptrel, tmaxrel, force_execution);
            }
        }

        public double value(FMStand stand, int specific_year)
        {
            double U = stand.U();
            double current;
            double current_year = ForestManagementEngine.instance().currentYear();
            if (specific_year >= 0)
            {
                current_year = specific_year;
            }
            // absolute age: years since the start of the rotation
            current = stand.absoluteAge();

            if (absolute)
            {
                current = current_year;
            }

            double current_rel = current / U;
            if (repeat)
            {
                // handle special case of repeating activities.
                // we execute the repeating activity if repeatInterval years elapsed
                // since the last execution.
                if ((int)current_year % repeat_interval == 0)
                {
                    return 1.0; // yes, execute this year
                }
                else
                {
                    return 0.0; // do not execute this year.
                }

            }
            // force execution: if age already higher than max, then always evaluate to 1.
            if (tmax > -1.0 && current >= tmax && force_execution)
            {
                return 1;
            }
            if (tmaxrel > -1.0 && current_rel >= tmaxrel && force_execution)
            {
                return 1.0;
            }

            if (tmin > -1.0 && current < tmin) return 0.0;
            if (tmax > -1.0 && current > tmax) return -1.0; // expired
            if (tminrel > -1.0 && current_rel < tminrel) return 0.0;
            if (tmaxrel > -1.0 && current_rel > tmaxrel) return -1.0; // expired

            // optimal time
            if (topt > -1.0 && Math.Abs(current - topt) <= 0.5)
            {
                return 1.0;
            }
            if (topt > -1.0 && current > topt)
            {
                if (force_execution)
                {
                    return 1.0;
                }
                else
                {
                    return -1.0; // expired
                }
            }

            if (tmin > -1.0 && tmax > -1.0)
            {
                if (topt > -1.0)
                {
                    // linear interpolation
                    if (current <= topt)
                    {
                        return topt == tmin ? 1.0 : (current - tmin) / (topt - tmin);
                    }
                    if (force_execution)
                    {
                        return 1.0; // keep the high probability.
                    }
                    else
                    {
                        return topt == tmax ? 1.0 : (tmax - current) / (tmax - topt); // decreasing probabilitiy again
                    }
                }
                else
                {
                    return 1.0; // no optimal time: everything between min and max is fine!
                }
            }
            // there is an optimal absoulte point in time defined, but not reached
            if (topt > -1)
            {
                return 0.0;
            }

            // optimal time
            if (toptrel > -1.0 && Math.Abs(current_rel - toptrel) * U <= 0.5)
            {
                return 1.0;
            }

            // min/max relative time
            if (tminrel > -1.0 && tmaxrel > -1.0)
            {
                if (toptrel > -1.0)
                {
                    // linear interpolation
                    if (current_rel <= toptrel)
                    {
                        return toptrel == tminrel ? 1.0 : (current_rel - tminrel) / (toptrel - tminrel);
                    }
                    else
                    {
                        return toptrel == tmaxrel ? 1.0 : (tmaxrel - current_rel) / (tmaxrel - toptrel);
                    }
                }
                else
                {
                    return 1.0; // no optimal time: everything between min and max is fine!
                }
            }
            // there is an optimal relative point in time defined, but not reached yet.
            if (toptrel > -1.0)
            {
                return 0.0;
            }

            Debug.WriteLine("value: unexpected combination. U " + U + " age " + current + ", schedule:" + this.dump());
            return 0.0;
        }

        public double minValue(double U)
        {
            if (absolute) return tmin;
            if (repeat) return 100000000.0;
            if (tmin > -1) return tmin;
            if (tminrel > -1.0) return tminrel * U; // assume a fixed U of 100yrs
            if (repeat) return -1.0; // repeating executions are treated specially
            if (topt > -1) return topt;
            return toptrel * U;
        }

        public double maxValue(double U)
        {
            if (absolute) return tmax;
            if (tmax > -1) return tmax;
            if (tmaxrel > -1.0) return tmaxrel * U; // assume a fixed U of 100yrs
            if (repeat) return -1.0; // repeating executions are treated specially
            if (topt > -1) return topt;
            return toptrel * U;
        }

        public double optimalValue(double U)
        {
            if (topt > -1) return topt;
            if (toptrel > -1) return toptrel * U;
            if (tmin > -1 && tmax > -1) return (tmax + tmin) / 2.0;
            if (tminrel > -1 && tmaxrel > -1) return (tmaxrel + tminrel) / 2.0 * U;
            if (force_execution) return tmax;
            return toptrel * U;
        }
    }
}
