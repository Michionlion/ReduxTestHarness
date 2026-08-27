using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReduxTestHarness
{
    internal sealed class TestReport
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion = 1;

        [JsonProperty("runId")]
        public string RunId;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("script")]
        public string Script;

        [JsonProperty("status")]
        public string Status = "running";

        [JsonProperty("startedUtc")]
        public DateTime StartedUtc;

        [JsonProperty("endedUtc")]
        public DateTime? EndedUtc;

        [JsonProperty("durationSeconds")]
        public double DurationSeconds;

        [JsonProperty("environment")]
        public TestEnvironment Environment = new TestEnvironment();

        [JsonProperty("fixture")]
        public string Fixture;

        [JsonProperty("assertions")]
        public readonly List<AssertionRecord> Assertions = new List<AssertionRecord>();

        [JsonProperty("notes")]
        public readonly List<string> Notes = new List<string>();

        [JsonProperty("metrics")]
        public readonly Dictionary<string, double> Metrics = new Dictionary<string, double>();

        [JsonProperty("values")]
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();

        [JsonProperty("screenshots")]
        public readonly List<string> Screenshots = new List<string>();

        [JsonProperty("attachments")]
        public readonly List<string> Attachments = new List<string>();

        [JsonProperty("logs")]
        public readonly List<string> Logs = new List<string>();

        [JsonProperty("errors")]
        public readonly List<TestError> Errors = new List<TestError>();

        [JsonProperty("process")]
        public ProcessRecord Process = new ProcessRecord();
    }

    internal sealed class TestEnvironment
    {
        [JsonProperty("kspVersion")]
        public string KspVersion;

        [JsonProperty("reduxVersion")]
        public string ReduxVersion;

        [JsonProperty("reduxCommit")]
        public string ReduxCommit;

        [JsonProperty("harnessVersion")]
        public string HarnessVersion;

        [JsonProperty("unityVersion")]
        public string UnityVersion;

        [JsonProperty("platform")]
        public string Platform;

        [JsonProperty("graphicsDevice")]
        public string GraphicsDevice;
    }

    internal sealed class AssertionRecord
    {
        [JsonProperty("status")]
        public string Status;

        [JsonProperty("expression")]
        public string Expression;

        [JsonProperty("message")]
        public string Message;

        [JsonProperty("actual")]
        public object Actual;

        [JsonProperty("expected")]
        public object Expected;
    }

    internal sealed class TestError
    {
        [JsonProperty("kind")]
        public string Kind;

        [JsonProperty("message")]
        public string Message;

        [JsonProperty("stackTrace")]
        public string StackTrace;
    }

    internal sealed class ProcessRecord
    {
        [JsonProperty("processId")]
        public int ProcessId;

        [JsonProperty("exitCode")]
        public int? ExitCode = null;

        [JsonProperty("crashed")]
        public bool Crashed = false;
    }

    internal sealed class BridgeCommand
    {
        public string Command;
        public Newtonsoft.Json.Linq.JObject Payload;
        public Action<Newtonsoft.Json.Linq.JObject> Complete;
    }

    internal sealed class TestStatusSnapshot
    {
        public string RunId;
        public string Name;
        public string Status = "idle";
        public string ReportPath;
        public string Error;
        public readonly List<string> Screenshots = new List<string>();
    }
}
