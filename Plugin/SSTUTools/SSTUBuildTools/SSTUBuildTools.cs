using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Compression;
using System.IO;

namespace SSTUBuildTools
{
    class SSTUBuildTools
    {
        public static void Main(string[] args)
        {
            if (args[0] == "zip") { zip(args); }
            else if (args[0] == "build") { build(args); }
            else if (args[0] == "increment") { increment(args); }
            else if (args[0] == "cmd") { cmd(args); }
        }

        private static void build(string[] args)
        {
            string fullPath = Path.GetFullPath(args[1]);
            Build build = new Build(fullPath, Environment.CurrentDirectory.Replace('\\', '/'));
            print("Building application.  Build config: " + fullPath);
            if (args[2] == "noinc") { build.config.incrementVersions = false; }
            pause();
            build.execute();
        }

        private static void zip(string[] args)
        {
            int len = args.Length;
            for (int i = 0; i < len; i++)
            {
                print(args[i]);
            }
        }

        private static void increment(string[] args)
        {
            int len = args.Length;
            for (int i = 0; i < len; i++)
            {
                print(args[i]);
            }
        }

        private static void cmd(string[] args)
        {
            int len = args.Length;
            for (int i = 0; i < len; i++)
            {
                print(args[i]);
            }
        }

        public static void print(string s)
        {
            System.Console.Out.WriteLine(s);
            System.Console.Out.Flush();
        }

        public static void pause()
        {
            System.Console.WriteLine("Paused -- press any <ENTER> to continue.");
            System.Console.In.ReadLine();
        }

    }

}
