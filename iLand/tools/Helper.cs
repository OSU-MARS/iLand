using System;
using System.IO;
using System.Text.RegularExpressions;

namespace iLand.tools
{
    /** Helper contains a bunch of (static) helper functions.
      * including simplifed functions to read/write plain text files (loadTextFile(), saveToTextFile()),
      * funcitons to show message dialogs (msg(), question()), and functions to control the amount of
      * debug outputs (quiet(), debugEnabled()).
      */
    internal class Helper
    {
        private static bool m_quiet = true;
        private static bool m_NoDebug = false;

        public Helper()
        {
        }

        public static string currentRevision()
        {
            return typeof(Helper).Assembly.GetName().Version.ToString(); //.section(" ",1,1);
        }

        public static string loadTextFile(string fileName)
        {
            return File.ReadAllText(fileName);
        }

        public static void saveToTextFile(string fileName, string text)
        {
            File.WriteAllText(fileName, text);
        }

        public static byte[] loadFile(string fileName)
        {
            return File.ReadAllBytes(fileName);
        }

        public static void saveToFile(string fileName, byte[] data)
        {
            File.WriteAllBytes(fileName, data);
        }

        /// ask the user for a input value
        public static string userValue(string message, string defaultValue, object parent = null)
        {
            return "not availabile in non-gui-mode";
        }

        public static void msg(string message, object parent = null)
        {
            // no op
        }

        public static bool question(string message, object parent = null)
        {
            // no op
            return false;
        }

        public static string fileDialog(string title, string start_directory, string filter, object parent = null)
        {
            string fileName = "undefined";
            return fileName;
        }

        public static void openHelp(string topic)
        {
            // no op
        }

        public static string stripHtml(string source)
        {
            string str = String.Join(' ', source.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            return Regex.Replace(str, "<[^>]+>", "");
        }
    }
}
