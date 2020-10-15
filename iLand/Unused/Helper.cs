//using System;
//using System.IO;
//using System.Text.RegularExpressions;

namespace iLand.Tools
{
    /** Helper contains a bunch of (static) helper functions.
      * including simplifed functions to read/write plain text files (loadTextFile(), saveToTextFile()),
      * funcitons to show message dialogs (msg(), question()), and functions to control the amount of
      * debug outputs (quiet(), debugEnabled()).
      */
    internal class Helper
    {
        //public static string CurrentRevision()
        //{
        //    return typeof(Helper).Assembly.GetName().Version.ToString(); //.section(" ",1,1);
        //}

        //public static string LoadTextFile(string fileName)
        //{
        //    return File.ReadAllText(fileName);
        //}

        //public static void SaveToTextFile(string fileName, string text)
        //{
        //    File.WriteAllText(fileName, text);
        //}

        //public static byte[] LoadFile(string fileName)
        //{
        //    return File.ReadAllBytes(fileName);
        //}

        //public static void SaveToFile(string fileName, byte[] data)
        //{
        //    File.WriteAllBytes(fileName, data);
        //}

        //public static void Message(string message)
        //{
        //    throw new NotImplementedException();
        //}

        //public static string StripHtml(string source)
        //{
        //    string str = String.Join(' ', source.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        //    return Regex.Replace(str, "<[^>]+>", "");
        //}
    }
}
