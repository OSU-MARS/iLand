using iLand.abe;
using iLand.tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace iLand.Test
{
    [TestClass]
    public class Scripting
    {
        [TestMethod]
        public void ForestManagementEngine()
        {
            // test code
            //Activity::setVerbose(true);
            // setup the activities and the javascript environment...
            GlobalSettings.Instance.ResetScriptEngine(); // clear the script
            ScriptGlobal.SetupGlobalScripting(); // general iLand scripting helper functions and such
            // TODO
            //mScriptBridge = new FomeScript();
            //mScriptBridge.setupScriptEnvironment();

            string file_name = "E:/Daten/iLand/modeling/abm/knowledge_base/test/test_stp.js";
            string code = Helper.LoadTextFile(file_name);
            QJSValue result = GlobalSettings.Instance.ScriptEngine.Evaluate(code, file_name);
            if (result.IsError())
            {
                int lineno = result.Property("lineNumber").ToInt();
                List<string> code_lines = code.Replace("\r", String.Empty).Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendLine(String.Format("{0}: {1} {2}\n", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : ""));
                }
                Debug.WriteLine("Javascript Error in file" + result.Property("fileName") + ":" + result.Property("lineNumber") + ":" + result + ":" + System.Environment.NewLine + code_part);
            }

            //    try {
            //        Debug.WriteLine("*** test 1 ***";
            //        FMSTP stp;
            //        stp.setVerbose(true);
            //        stp.setup(GlobalSettings.instance().scriptEngine().globalObject().property("stp"), "stp");
            //        stp.dumpInfo();

            //    } catch (IException &e) {
            //        Debug.WriteLine("An error occured:" + e.message();
            //    }
            //    try {
            //        Debug.WriteLine("*** test 2 ***";
            //        FMSTP stp2;
            //        stp2.setVerbose(true);
            //        stp2.setup(GlobalSettings.instance().scriptEngine().globalObject().property("degenerated"), "degenerated");
            //        stp2.dumpInfo();
            //    } catch (IException &e) {
            //        Debug.WriteLine("An error occured:" + e.message();
            //    }

            // dump all objects: TODO
            //foreach (FMSTP stp in mSTP)
            //{
            //    stp.DumpInfo();
            //}
            //setup();
            Debug.WriteLine("finished");
        }
    }
}
