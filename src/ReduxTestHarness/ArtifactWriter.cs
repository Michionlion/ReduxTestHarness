using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace ReduxTestHarness
{
    internal sealed class ArtifactWriter
    {
        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };

        private readonly string _gameRoot;
        private readonly string _scriptText;
        private readonly Stopwatch _stopwatch;

        public ArtifactWriter(
            string runId,
            string scriptPath,
            string scriptText,
            string resultsRoot)
        {
            _scriptText = scriptText;
            _gameRoot = ResolveGameRoot();
            _stopwatch = Stopwatch.StartNew();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss_fff");
            string scriptName = Path.GetFileNameWithoutExtension(scriptPath);
            ArtifactDirectory = Path.Combine(
                Path.GetFullPath(resultsRoot),
                timestamp,
                Slug(scriptName));
            ScreenshotDirectory = Path.Combine(ArtifactDirectory, "screenshots");
            LogDirectory = Path.Combine(ArtifactDirectory, "logs");
            Directory.CreateDirectory(ScreenshotDirectory);
            Directory.CreateDirectory(LogDirectory);

            Report = new TestReport
            {
                RunId = runId,
                Name = scriptName,
                Script = scriptPath,
                StartedUtc = DateTime.UtcNow
            };
            PopulateEnvironment(Report.Environment);
            Report.Process.ProcessId = Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(ArtifactDirectory, "test.lua"), scriptText, new UTF8Encoding(false));
            Flush();
        }

        public string ArtifactDirectory { get; private set; }
        public string ScreenshotDirectory { get; private set; }
        public string LogDirectory { get; private set; }
        public string ReportPath { get { return Path.Combine(ArtifactDirectory, "report.json"); } }
        public TestReport Report { get; private set; }

        public string NewScreenshotPath(string requestedName)
        {
            return UniquePath(ScreenshotDirectory, Slug(requestedName), ".png");
        }

        public void AddScreenshot(string absolutePath)
        {
            AddRelativeUnique(Report.Screenshots, absolutePath);
        }

        public void AddAttachment(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute) && !Directory.Exists(absolute))
            {
                throw new FileNotFoundException("Attachment does not exist.", absolute);
            }
            AddRelativeUnique(Report.Attachments, absolute);
        }

        public void Complete(string status, Exception error)
        {
            _stopwatch.Stop();
            Report.Status = status;
            Report.EndedUtc = DateTime.UtcNow;
            Report.DurationSeconds = Math.Round(_stopwatch.Elapsed.TotalSeconds, 3);
            if (error != null)
            {
                Report.Errors.Add(new TestError
                {
                    Kind = error.GetType().FullName,
                    Message = error.Message,
                    StackTrace = error.ToString()
                });
            }
            CollectLogs();
            Flush();
        }

        public void Flush()
        {
            Directory.CreateDirectory(ArtifactDirectory);
            string json = JsonConvert.SerializeObject(Report, JsonSettings);
            WriteUtf8Atomic(ReportPath, json);
            WriteUtf8Atomic(
                Path.Combine(ArtifactDirectory, "summary.md"),
                BuildSummary());
        }

        private string BuildSummary()
        {
            int passed = 0;
            for (int index = 0; index < Report.Assertions.Count; index++)
            {
                if (Report.Assertions[index].Status == "passed")
                {
                    passed++;
                }
            }

            var builder = new StringBuilder();
            builder.Append(Report.Status == "passed" ? "PASS" :
                Report.Status == "running" ? "RUNNING" : "FAIL");
            builder.Append(" — ").AppendLine(Report.Name);
            builder.AppendLine();
            builder.Append("Duration: ").Append(Report.DurationSeconds.ToString("0.###")).AppendLine(" s");
            builder.Append("Fixture: ").AppendLine(Report.Fixture ?? "none");
            builder.AppendLine();
            builder.Append("Assertions: ").Append(passed).Append('/').Append(Report.Assertions.Count).AppendLine(" passed");
            builder.Append("Screenshots: ").AppendLine(Report.Screenshots.Count.ToString());
            builder.Append("Errors: ").AppendLine(Report.Errors.Count.ToString());
            return builder.ToString();
        }

        private void CollectLogs()
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(_gameRoot))
            {
                candidates.Add(Path.Combine(_gameRoot, "Ksp2.log"));
                candidates.Add(Path.Combine(_gameRoot, "BepInEx", "LogOutput.log"));
            }

            string localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Intercept Games", "Kerbal Space Program 2", "Player.log");
            candidates.Add(localLow);

            for (int index = 0; index < candidates.Count; index++)
            {
                string source = candidates[index];
                if (!File.Exists(source))
                {
                    continue;
                }
                try
                {
                    string destination = UniquePath(
                        LogDirectory,
                        Path.GetFileNameWithoutExtension(source),
                        Path.GetExtension(source));
                    File.Copy(source, destination, true);
                    AddRelativeUnique(Report.Logs, destination);
                }
                catch (Exception copyError)
                {
                    Report.Errors.Add(new TestError
                    {
                        Kind = "log_collection",
                        Message = copyError.Message,
                        StackTrace = null
                    });
                }
            }
        }

        private void PopulateEnvironment(TestEnvironment environment)
        {
            environment.KspVersion = Application.version;
            environment.UnityVersion = Application.unityVersion;
            environment.Platform = Application.platform.ToString();
            environment.GraphicsDevice = SystemInfo.graphicsDeviceName;
            environment.HarnessVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Assembly gameAssembly = typeof(KSP.Game.GameManager).Assembly;
            string reduxDisplayVersion = GetReduxDisplayVersion(gameAssembly);
            environment.ReduxVersion = reduxDisplayVersion ??
                gameAssembly.GetName().Version.ToString();
            environment.ReduxCommit = GetReduxCommit(reduxDisplayVersion) ??
                GetInformationalVersion(gameAssembly);
        }

        private static string GetReduxDisplayVersion(Assembly gameAssembly)
        {
            try
            {
                Type version = gameAssembly.GetType("Redux.Version", false, false);
                PropertyInfo property = version == null ? null : version.GetProperty(
                    "DisplayVersionSingleLine",
                    BindingFlags.Public | BindingFlags.Static);
                return property == null ? null : property.GetValue(null, null) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string GetReduxCommit(string displayVersion)
        {
            if (string.IsNullOrWhiteSpace(displayVersion))
            {
                return null;
            }
            Match match = Regex.Match(
                displayVersion,
                @"(?:commit\s+|\+)([0-9a-f]{7,40})\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string GetInformationalVersion(Assembly assembly)
        {
            object[] attributes = assembly.GetCustomAttributes(
                typeof(AssemblyInformationalVersionAttribute), false);
            if (attributes.Length == 0)
            {
                return null;
            }
            string value = ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;
            int plus = value.IndexOf('+');
            return plus >= 0 && plus + 1 < value.Length ? value.Substring(plus + 1) : null;
        }

        private void AddRelativeUnique(List<string> target, string absolutePath)
        {
            string relative = MakeRelative(ArtifactDirectory, absolutePath).Replace('\\', '/');
            if (!target.Contains(relative))
            {
                target.Add(relative);
            }
        }

        private static string ResolveGameRoot()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent == null ? null : parent.FullName;
        }

        private static string UniquePath(string directory, string stem, string extension)
        {
            string candidate = Path.Combine(directory, stem + extension);
            int suffix = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, stem + "-" + suffix + extension);
                suffix++;
            }
            return candidate;
        }

        internal static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "test";
            }
            var builder = new StringBuilder(value.Length);
            bool dash = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = char.ToLowerInvariant(value[index]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    dash = false;
                }
                else if (!dash && builder.Length > 0)
                {
                    builder.Append('-');
                    dash = true;
                }
            }
            return builder.ToString().Trim('-');
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(AppendSeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
        }

        private static string AppendSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static void WriteUtf8Atomic(string path, string contents)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
    }
}
