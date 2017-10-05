using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CS.Util;
using CS.Util.Cryptography;
using Newtonsoft.Json;
using PowerArgs;
using RT.Util;
using RT.Util.Consoles;

namespace NpmSymCache
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Args.InvokeAction<CmdArgs>(args);
                Util.WriteLineColor("Success - exiting...", "$");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Util.WriteLineColor("Error: " + ex.Message, "_");
                Console.WriteLine();
                ArgUsage.GenerateUsageFromTemplate<CmdArgs>().WriteLine();
                return 1;
            }
        }
    }

    public class CmdArgs
    {
        private string _packageJson;

        [HelpHook, ArgShortcut("-?"), ArgShortcut("--help"), ArgShortcut("-h"),
            ArgDescription("Shows this help")]
        public bool Help { get; set; }

        [ArgShortcut("-p"), ArgShortcut("--package"), ArgExistingFile, ArgDefaultValue("package.json"),
            ArgDescription("Where to look for the package.json file.")]
        public string PackageFile { get; set; }

        protected string PackageJson => _packageJson ?? (_packageJson = File.ReadAllText(PackageFile));

        protected string PackageName
        {
            get
            {
                dynamic packageObj = JsonConvert.DeserializeObject(PackageJson);
                return packageObj.name.ToString();
            }
        }

        protected string PackageHash => MD5.Compute(PackageJson);

        protected string PackageDirectory => Path.Combine(Environment.ExpandEnvironmentVariables(CacheDirectory), CacheKey ?? PackageName, PackageHash);

        [ArgShortcut("-d"), ArgDefaultValue("%AppData%\\NpmSymCache"),
            ArgDescription("Overrides where NpmSymCache stores npm packages")]
        public string CacheDirectory { get; set; }

        [ArgShortcut("--key"),
            ArgDescription("Overrides what name NpmSymCache uses to identify this package. (usually taken from package.json)")]
        public string CacheKey { get; set; }

        [ArgShortcut("--limit"), ArgDefaultValue(5),
            ArgDescription("Sets number of cache entries to keep for this package")]
        public int CacheLimit { get; set; }

        [ArgActionMethod,
            ArgDescription("Opens the root cache directory in explorer")]
        public void Open()
        {
            Util.RunCmd("explorer.exe " + Environment.ExpandEnvironmentVariables(CacheDirectory), ignoreErrors: true);
        }

        [ArgActionMethod,
            ArgDescription("Cleans the cache, only keeping the cache entries that satisfy the limit argument")]
        public void Clean()
        {
            var parent = Path.GetFullPath(Path.Combine(PackageDirectory, ".."));

            var entries = Directory.EnumerateDirectories(parent)
                .Select(d => new DirectoryInfo(d))
                .ToArray();

            var remove = entries.Except(entries
                .OrderByDescending(d => d.CreationTime)
                .Take(CacheLimit))
                .ToArray();

            if (remove.Length > 0)
            {
                Util.WriteLineColor($"Cleaning old cache items ...");
                Util.WriteLineColor($"Pruning {remove.Length} items...", "^");

                foreach (var dir in remove)
                {
                    Util.WriteLineColor($"Deleting '{dir.FullName}'. (created {PrettyTime.Format(dir.CreationTime)})");
                    dir.Delete(true);
                }
            }
        }

        [ArgActionMethod,
            ArgDescription("Deletes current cache entry (if any) and then installs normally.")]
        public void Reinstall()
        {
            Util.WriteLineColor("Deleting current cache directory...", "^");
            Directory.Delete(PackageDirectory, true);
            Install();
        }

        [ArgActionMethod,
            ArgDescription("Installs the npm packages to the cache or restores a symlink")]
        public void Install()
        {
            const string pkgFileName = ".npmsymv";
            string pkgFilePath = Path.Combine(PackageDirectory, pkgFileName);

            if (Directory.Exists("node_modules"))
            {
                Util.WriteLineColor("'node_modules' exists. verifying...", "^");

                var symTarget = SymbolicLink.GetTarget("node_modules");
                if (symTarget == null)
                    throw new Exception("Folder 'node_modules' exists and is not a symlink. Delete this folder before trying again.");

                if (symTarget != PackageDirectory)
                {
                    Util.WriteLineColor("'node_modules' is a symlink pointing to the incorrect cache location.\n", "^");
                    Util.WriteLineColor($"    Old: {symTarget}\n", "^");
                    Util.WriteLineColor($"    New: {PackageDirectory}\n", "^");
                }

                Directory.Delete("node_modules", true);
            }

            bool cacheValid = false;

            if (!Directory.Exists(PackageDirectory))
            {
                Util.WriteLineColor("cache not found, creating...");
            }
            else if (File.Exists(pkgFilePath) && MD5.Compute(File.ReadAllText(pkgFilePath)) == PackageHash)
            {
                Util.WriteLineColor("cache exists");
                cacheValid = true;
            }
            else
            {
                Util.WriteLineColor("cache exists but is corrupted, recreating...", "^");
                Directory.Delete(PackageDirectory, true);
            }

            PathUtil.CreatePathToFile(PackageDirectory);
            Directory.CreateDirectory(PackageDirectory);

            Util.RunCmd($@"mklink /D node_modules ""{PackageDirectory}""");

            if (cacheValid)
                return;

            Util.RunCmd("npm install");

            File.Copy(PackageFile, pkgFilePath);

            Clean();
        }
    }

    public static class Util
    {
        public static void RunCmd(string cmd, bool ignoreErrors = false)
        {
            WriteLineColor($"\nRunning '{cmd}'.\n");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $@"/C " + cmd,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var p = Process.Start(psi);

            do
            {
                Thread.Sleep(1000);
                Console.Write(p.StandardOutput.ReadToEnd());
            } while (!p.HasExited);

            var code = p.ExitCode;
            if (code > 0 && !ignoreErrors)
            {
                Util.WriteColor(p.StandardError.ReadToEnd(), "_");
                throw new Exception("'" + cmd + "' exited with code: " + code);
            }
        }

        public static void WriteColor(string text, string color = "")
        {
            ConsoleUtil.Write(ConsoleColoredString.FromEggsNode(EggsML.Parse("*" + color + EggsML.Escape(text) + color + "*")));
        }

        public static void WriteLineColor(string text, string color = "")
        {
            ConsoleUtil.WriteLine(ConsoleColoredString.FromEggsNode(EggsML.Parse("*" + color + EggsML.Escape(text) + color + "*")));
        }
    }
}
