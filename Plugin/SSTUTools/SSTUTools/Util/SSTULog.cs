using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public static class SSTULog
    {

        public static readonly bool debugMode = true;

        public static void stacktrace()
        {
            MonoBehaviour.print(System.Environment.StackTrace);
        }

        public static void log(string line)
        {
            MonoBehaviour.print("SSTU-LOG  : " + line);
        }

        public static void log(System.Object obj)
        {
            MonoBehaviour.print("SSTU-LOG  : " + obj);
        }

        public static void error(string line)
        {
            MonoBehaviour.print("SSTU-ERROR: " + line);
        }

        public static void debug(string line)
        {
            if (!debugMode) { return; }
            MonoBehaviour.print("SSTU-DEBUG: " + line);
        }

        public static void debug(object obj)
        {
            if (!debugMode) { return; }
            MonoBehaviour.print(obj);
        }

    }
}
