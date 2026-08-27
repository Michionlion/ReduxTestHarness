using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SpaceWarp2.API.Mods;
using SpaceWarp2.API.Mods.JSON;
using UnityEngine;

namespace ReduxTestHarness
{
    internal sealed class ArtifactWriter
    {
        private const long MaxBytesPerLog = 64L * 1024L * 1024L;
        private const int MaxForbiddenLogMatches = 20;
        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };

        private readonly string _gameRoot;
        private readonly string _scriptText;
        private readonly Stopwatch _stopwatch;
        private readonly List<LogSourceSnapshot> _logSources;
        private readonly List<ForbiddenLogPattern> _forbiddenLogPatterns =
            new List<ForbiddenLogPattern>();

        public ArtifactWriter(
            string runId,
            string scriptPath,
            string scriptText,
            string resultsRoot,
            bool includeStartupLogs)
        {
            _scriptText = scriptText;
            _gameRoot = ResolveGameRoot();
            _stopwatch = Stopwatch.StartNew();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss_fff");
            string scriptName = Path.GetFileNameWithoutExtension(scriptPath);
            string runSuffix = Slug(runId);
            if (runSuffix.Length > 12)
            {
                runSuffix = runSuffix.Substring(0, 12);
            }
            ArtifactDirectory = Path.Combine(
                Path.GetFullPath(resultsRoot),
                timestamp + "_" + runSuffix,
                Slug(scriptName));
            ScreenshotDirectory = Path.Combine(ArtifactDirectory, "screenshots");
            AttachmentDirectory = Path.Combine(ArtifactDirectory, "attachments");
            LogDirectory = Path.Combine(ArtifactDirectory, "logs");
            Directory.CreateDirectory(ScreenshotDirectory);
            Directory.CreateDirectory(AttachmentDirectory);
            Directory.CreateDirectory(LogDirectory);
            _logSources = SnapshotLogSources(includeStartupLogs);

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
        public string AttachmentDirectory { get; private set; }
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

        public string AddAttachment(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute) && !Directory.Exists(absolute))
            {
                throw new FileNotFoundException("Attachment does not exist.", absolute);
            }
            if ((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Attachments cannot be links or reparse points: " + absolute);
            }
            if (string.Equals(
                absolute.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(ArtifactDirectory).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The result directory itself cannot be attached; attach a file or child directory.");
            }

            if (IsWithin(ArtifactDirectory, absolute))
            {
                AddRelativeUnique(Report.Attachments, absolute);
                return absolute;
            }

            string copied;
            if (File.Exists(absolute))
            {
                string stem = Slug(Path.GetFileNameWithoutExtension(absolute));
                string extension = Path.GetExtension(absolute);
                copied = UniquePath(AttachmentDirectory, stem, extension);
                CopyFileShared(absolute, copied);
            }
            else
            {
                string stem = Slug(new DirectoryInfo(absolute).Name);
                copied = UniqueDirectory(AttachmentDirectory, stem);
                CopyDirectory(absolute, copied);
            }
            AddRelativeUnique(Report.Attachments, copied);
            return copied;
        }

        public void AddWarning(string kind, string message)
        {
            Report.Warnings.Add(new TestError
            {
                Kind = kind,
                Message = message,
                StackTrace = null
            });
        }

        public void AddForbiddenLogPattern(string pattern, string message)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("A non-empty log pattern is required.", "pattern");
            }
            var expression = new Regex(
                pattern,
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            _forbiddenLogPatterns.Add(new ForbiddenLogPattern
            {
                Pattern = pattern,
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Forbidden log pattern matched: " + pattern
                    : message,
                Expression = expression
            });
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
            ScanLogsForForbiddenPatterns();
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
            builder.Append("Warnings: ").AppendLine(Report.Warnings.Count.ToString());
            return builder.ToString();
        }

        private void CollectLogs()
        {
            for (int index = 0; index < _logSources.Count; index++)
            {
                LogSourceSnapshot snapshot = _logSources[index];
                string source = snapshot.Path;
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
                    CopyLogSlice(snapshot, destination);
                    AddRelativeUnique(Report.Logs, destination);
                }
                catch (Exception copyError)
                {
                    if (_forbiddenLogPatterns.Count > 0)
                    {
                        Report.Errors.Add(new TestError
                        {
                            Kind = "log_collection",
                            Message = "A required log could not be captured: " + copyError.Message,
                            StackTrace = null
                        });
                        Report.Status = "failed";
                    }
                    else
                    {
                        AddWarning("log_collection", copyError.Message);
                    }
                }
            }
        }

        private void ScanLogsForForbiddenPatterns()
        {
            if (_forbiddenLogPatterns.Count == 0)
            {
                return;
            }
            if (Report.Logs.Count == 0)
            {
                Report.Errors.Add(new TestError
                {
                    Kind = "log_collection",
                    Message = "No KSP2 logs were available for the requested failure policy.",
                    StackTrace = null
                });
                Report.Status = "failed";
                return;
            }

            int matches = 0;
            for (int logIndex = 0;
                logIndex < Report.Logs.Count && matches < MaxForbiddenLogMatches;
                logIndex++)
            {
                string relative = Report.Logs[logIndex].Replace('/', Path.DirectorySeparatorChar);
                string path = Path.Combine(ArtifactDirectory, relative);
                try
                {
                    using (var reader = new StreamReader(
                        new FileStream(
                            path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete),
                        new UTF8Encoding(false),
                        true,
                        65536))
                    {
                        string line;
                        int lineNumber = 0;
                        while (matches < MaxForbiddenLogMatches &&
                            (line = reader.ReadLine()) != null)
                        {
                            lineNumber++;
                            for (int patternIndex = 0;
                                patternIndex < _forbiddenLogPatterns.Count;
                                patternIndex++)
                            {
                                ForbiddenLogPattern pattern =
                                    _forbiddenLogPatterns[patternIndex];
                                if (!pattern.Expression.IsMatch(line))
                                {
                                    continue;
                                }
                                Report.Errors.Add(new TestError
                                {
                                    Kind = "forbidden_log_match",
                                    Message = pattern.Message + " (" +
                                        Report.Logs[logIndex] + ":" + lineNumber + ")",
                                    StackTrace = line
                                });
                                matches++;
                                break;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    Report.Errors.Add(new TestError
                    {
                        Kind = error is RegexMatchTimeoutException
                            ? "log_scan_timeout"
                            : "log_scan",
                        Message = "Could not scan '" + Report.Logs[logIndex] + "': " +
                            error.Message,
                        StackTrace = null
                    });
                    Report.Status = "failed";
                }
            }

            if (matches > 0 && Report.Status == "passed")
            {
                Report.Status = "failed";
            }
            if (matches == MaxForbiddenLogMatches)
            {
                AddWarning(
                    "log_scan_limit",
                    "Forbidden log matches were capped at " + MaxForbiddenLogMatches + ".");
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
            PopulateMods(environment);
        }

        private static void PopulateMods(TestEnvironment environment)
        {
            try
            {
                IReadOnlyList<SpaceWarpPluginDescriptor> plugins =
                    PluginList.AllEnabledAndActivePlugins;
                for (int index = 0; index < plugins.Count; index++)
                {
                    SpaceWarpPluginDescriptor descriptor = plugins[index];
                    if (descriptor == null)
                    {
                        continue;
                    }
                    ModInfo info = descriptor.SWInfo;
                    environment.Mods.Add(new ModEnvironmentRecord
                    {
                        Id = info == null ? descriptor.Guid : info.ModID,
                        Name = info == null ? descriptor.Name : info.Name,
                        Version = info == null ? null : info.Version,
                        Assembly = info == null ? null : info.MainAssembly
                    });
                }
                environment.Mods.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.Id, right.Id));
            }
            catch
            {
                // Environment collection must not prevent a test from starting.
            }
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
            if (!IsWithin(ArtifactDirectory, absolutePath))
            {
                throw new InvalidOperationException(
                    "Artifact paths must be contained by the current result directory: " +
                    absolutePath);
            }
            string relative = MakeRelative(ArtifactDirectory, absolutePath).Replace('\\', '/');
            if (!target.Contains(relative))
            {
                target.Add(relative);
            }
        }

        private List<LogSourceSnapshot> SnapshotLogSources(bool includeStartupLogs)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(_gameRoot))
            {
                candidates.Add(Path.Combine(_gameRoot, "Ksp2.log"));
                candidates.Add(Path.Combine(_gameRoot, "BepInEx", "LogOutput.log"));
            }
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Intercept Games",
                "Kerbal Space Program 2", "Player.log"));

            var snapshots = new List<LogSourceSnapshot>();
            for (int index = 0; index < candidates.Count; index++)
            {
                string path = candidates[index];
                long offset = 0;
                if (!includeStartupLogs && File.Exists(path))
                {
                    try
                    {
                        offset = new FileInfo(path).Length;
                    }
                    catch
                    {
                        offset = 0;
                    }
                }
                snapshots.Add(new LogSourceSnapshot { Path = path, Offset = offset });
            }
            return snapshots;
        }

        private void CopyLogSlice(LogSourceSnapshot snapshot, string destination)
        {
            using (var source = new FileStream(
                snapshot.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                long end = source.Length;
                long requestedStart = snapshot.Offset <= end ? snapshot.Offset : 0;
                long start = requestedStart;
                bool truncated = end - start > MaxBytesPerLog;
                if (truncated)
                {
                    start = end - MaxBytesPerLog;
                }

                using (var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    if (truncated)
                    {
                        string notice =
                            "[ReduxTestHarness] Earlier bytes from this test log were " +
                            "truncated; retained the final " + MaxBytesPerLog + " bytes.\r\n";
                        byte[] noticeBytes = new UTF8Encoding(false).GetBytes(notice);
                        output.Write(noticeBytes, 0, noticeBytes.Length);
                        AddWarning(
                            "log_truncated",
                            Path.GetFileName(snapshot.Path) + " exceeded the " +
                            MaxBytesPerLog + " byte per-run capture limit.");
                    }

                    source.Position = start;
                    CopyRange(source, output, end - start);
                }
            }
        }

        private static void CopyFileShared(string sourcePath, string destinationPath)
        {
            using (var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                source.CopyTo(destination);
            }
        }

        private static void CopyRange(Stream source, Stream destination, long count)
        {
            var buffer = new byte[65536];
            long remaining = count;
            while (remaining > 0)
            {
                int read = source.Read(
                    buffer,
                    0,
                    (int)Math.Min(buffer.Length, remaining));
                if (read <= 0)
                {
                    break;
                }
                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            var pending = new Stack<KeyValuePair<string, string>>();
            pending.Push(new KeyValuePair<string, string>(sourceRoot, destinationRoot));
            while (pending.Count > 0)
            {
                KeyValuePair<string, string> current = pending.Pop();
                Directory.CreateDirectory(current.Value);

                string[] directories = Directory.GetDirectories(current.Key);
                for (int index = 0; index < directories.Length; index++)
                {
                    if ((File.GetAttributes(directories[index]) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "Attachment directories cannot contain links or reparse points: " +
                            directories[index]);
                    }
                    pending.Push(new KeyValuePair<string, string>(
                        directories[index],
                        Path.Combine(current.Value, Path.GetFileName(directories[index]))));
                }

                string[] files = Directory.GetFiles(current.Key);
                for (int index = 0; index < files.Length; index++)
                {
                    if ((File.GetAttributes(files[index]) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "Attachment directories cannot contain links or reparse points: " +
                            files[index]);
                    }
                    CopyFileShared(
                        files[index],
                        Path.Combine(current.Value, Path.GetFileName(files[index])));
                }
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

        private static string UniqueDirectory(string directory, string stem)
        {
            string candidate = Path.Combine(directory, stem);
            int suffix = 2;
            while (Directory.Exists(candidate) || File.Exists(candidate))
            {
                candidate = Path.Combine(directory, stem + "-" + suffix);
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
            string slug = builder.ToString().Trim('-');
            return slug.Length == 0 ? "test" : slug;
        }

        private static bool IsWithin(string root, string path)
        {
            string rootPrefix = AppendSeparator(Path.GetFullPath(root));
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    rootPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
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

        private sealed class LogSourceSnapshot
        {
            public string Path;
            public long Offset;
        }

        private sealed class ForbiddenLogPattern
        {
            public string Pattern;
            public string Message;
            public Regex Expression;
        }
    }
}
