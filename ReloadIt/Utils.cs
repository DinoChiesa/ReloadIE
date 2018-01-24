using System;
// using System.IO;
// using System.ComponentModel;
// using System.Runtime.InteropServices;
// using System.Runtime.InteropServices.ComTypes;
// using System.Security;
// using System.Collections;

namespace ReloadIt
{
    public static class Extensions
    {
        public static string AbbrevUrl(this string s)
        {
            if (s.StartsWith("http://"))
                return s.Substring(7,s.Length-7);
            return s;
        }
    }
}
