using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUBuildTools
{
    public class Build
    {
        private string buildFilePath;

        internal BuildConfig config;

        public Build(string buildConfig, string execPath)
        {
            buildFilePath = buildConfig;
            config = new BuildConfig(buildFilePath, execPath);
        }

        public void execute()
        {
            int len = config.buildActions.Length;
            for (int i = 0; i < len; i++)
            {
                config.buildActions[i].execute();
                SSTUBuildTools.pause();
            }
        }
    }

    public class BuildConfig
    {
        private string[] configLines;
        private string versionFilePath;
        internal VersionFile versionFile;
        internal BuildAction[] buildActions;
        internal string buildPath;
        internal bool incrementVersions = false;

        public BuildConfig(string path, string execPath)
        {
            buildPath = execPath;
            SSTUBuildTools.print("exec path: " + buildPath);
            configLines = File.ReadAllLines(path);
            int len = configLines.Length;
            string line;
            List<string> cmdLines = new List<string>();
            List<BuildAction> actions = new List<BuildAction>();
            for (int i = 0; i < len; i++)
            {
                line = configLines[i];
                if (line.StartsWith("versionFile"))
                {
                    versionFilePath = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("INC:") && incrementVersions)
                {
                    actions.Add(new VersionAction(this, new string[] { line }));
                }
                else if (line.StartsWith("CMD:"))
                {
                    actions.Add(new CommandAction(this, new string[] { line }));
                }
                else if (line.StartsWith("ZIP:"))
                {
                    cmdLines.Add(line);
                    while (line.Trim() != "}")
                    {
                        i++;
                        line = configLines[i];
                        cmdLines.Add(line);
                    }
                    actions.Add(new ZipAction(this, cmdLines.ToArray()));
                }
                cmdLines.Clear();
            }
            buildActions = actions.ToArray();
            versionFile = new VersionFile(versionFilePath);
        }

    }

    public class VersionFile
    {
        private string filePath;
        public string version;
        public VersionFile(string path)
        {
            this.filePath = path;
            SSTUBuildTools.print("Loading version file from: " + path);
            string[] lines = File.ReadAllLines(path);
            int len = lines.Length;
            string line;
            for (int i = 0; i < len; i++)
            {
                line = lines[i].Trim();
                if (line.StartsWith("\"VERSION\""))
                {
                    string major = lines[i + 2].Split(':')[1].Replace(",", "");
                    string minor = lines[i + 3].Split(':')[1].Replace(",", "");
                    string patch = lines[i + 4].Split(':')[1].Replace(",", "");
                    string build = lines[i + 5].Split(':')[1].Replace(",", "");
                    version = major + "." + minor + "." + patch + "." + build;
                    break;
                }
            }
            SSTUBuildTools.print("Parsed version string: " + version);
        }

        public void incrementMajor()
        {
            SSTUBuildTools.print("Version increment - major");
        }

        public void incrementMinor()
        {
            SSTUBuildTools.print("Version increment - minor");
        }

        public void incrementPatch()
        {
            SSTUBuildTools.print("Version increment - patch");
        }

        public void incrementBuild()
        {
            SSTUBuildTools.print("Version increment - build");
        }

        public void saveFile()
        {
            SSTUBuildTools.print("Version File Save out");
        }
    }

    public class BuildAction
    {
        protected BuildConfig config;
        protected string[] actionLines;

        public BuildAction(BuildConfig config, string[] cmdLines)
        {
            this.config = config;
            actionLines = cmdLines;
        }

        public virtual void execute()
        {

        }
    }

    public class VersionAction : BuildAction
    {
        public string command;

        public VersionAction(BuildConfig config, string[] cmdLines) : base(config, cmdLines)
        {
            command = actionLines[0].Split(':')[1].Trim();
        }

        public override void execute()
        {
            base.execute();
            if (command == "major")
            {
                config.versionFile.incrementMajor();
            }
            else if (command == "minor")
            {
                config.versionFile.incrementMinor();
            }
            else if (command == "patch")
            {
                config.versionFile.incrementPatch();
            }
            else if (command == "build")
            {
                config.versionFile.incrementBuild();
            }
            config.versionFile.saveFile();
        }
    }

    public class CommandAction : BuildAction
    {
        public string command;

        public CommandAction(BuildConfig config, string[] cmdLines) : base(config, cmdLines)
        {
            command = actionLines[0].Split(':')[1].Trim();
        }

        public override void execute()
        {
            base.execute();
            SSTUBuildTools.print("Executing command: " + command);
            Process process = new Process();
            ProcessStartInfo si = new ProcessStartInfo();
            string[] sp = command.Split(' ');
            si.FileName = "cmd.exe";
            si.Arguments = "/C " + command;
            si.CreateNoWindow = true;
            si.UseShellExecute = false;
            si.WindowStyle = ProcessWindowStyle.Hidden;            
            try
            {
                process.StartInfo = si;
                process.Start();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                SSTUBuildTools.print("Caught exception while running command: " + e.Message);
                SSTUBuildTools.pause();
                Environment.Exit(1);
            }
        }
    }

    public class ZipAction : BuildAction
    {
        private string destinationPath;

        public ZipAction(BuildConfig config, string[] cmdLines) : base(config, cmdLines)
        {
            destinationPath = actionLines[0].Split(':')[1].Trim();
        }

        public override void execute()
        {
            base.execute();            
            string outPath = destinationPath.Replace("%version%", config.versionFile.version);
            SSTUBuildTools.print("Zipping -- dest file: " + config.buildPath+ "/" +outPath);
            if (File.Exists(outPath)) { File.Delete(outPath); }
            ZipArchive archive = ZipFile.Open(outPath, ZipArchiveMode.Update);
            string line;
            int len = actionLines.Length;
            for (int i = 0; i < len; i++)
            {
                line = actionLines[i].Trim();
                if (line.StartsWith("+d"))//dir add
                {
                    string src = line.Substring(3).Split(':')[0];
                    string dest = line.Substring(3).Split(':')[1];
                    string[] files = Directory.GetFiles(src, "*.*", SearchOption.AllDirectories);
                    string file;
                    string destFile;
                    int len2 = files.Length;
                    SSTUBuildTools.print("Adding directory to archive: " + config.buildPath + "/" + src);
                    for (int k = 0; k < len2; k++)
                    {
                        file = files[k].Replace('\\', '/');
                        destFile = file.Replace(src, dest);
                        if (!File.Exists(file)) { continue; }
                        //SSTUBuildTools.print("Adding file to archive: " + file + " : " + destFile);
                        archive.CreateEntryFromFile(file, destFile);
                    }
                }
                else if (line.StartsWith("+f"))//file add
                {
                    string src = line.Substring(3).Split(':')[0];
                    string dest = line.Substring(3).Split(':')[1];
                    SSTUBuildTools.print("Adding file to archive: "+ src + " : "+ dest);
                    archive.CreateEntryFromFile(src, dest);
                }
                else if (line.StartsWith("-d"))//dir remove
                {
                    archive.GetEntry(line.Split(' ')[1]).Delete();
                }
                else if (line.StartsWith("-f"))//file remove
                {
                    archive.GetEntry(line.Split(' ')[1]).Delete();
                }
            }
            SSTUBuildTools.print("Building .zip file, please wait, this may take a few minutes...");
            archive.Dispose();
            SSTUBuildTools.print("Zip file built.");
        }
    }

}
