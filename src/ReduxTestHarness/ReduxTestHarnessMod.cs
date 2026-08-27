using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using SpaceWarp2.API.Mods;
using UnityEngine;

namespace ReduxTestHarness
{
    [DefaultExecutionOrder(32000)]
    public sealed class ReduxTestHarnessMod : MonoBehaviourMod
    {
        private const int DefaultPort = 28542;
        private JsonLineBridgeServer _server;
        private KspGameAdapter _game;
        private LuaTestRunner _runner;
        private bool _dismissPhotosensitivityWarning;
        private bool _startupWarningDismissed;
        private float _nextStartupWarningProbe;
        private float _startupWarningProbeDeadline;
        private bool _includeStartupLogs;
        private IDisposable _testApiRegistration;

        public override void OnInitialized()
        {
            _game = new KspGameAdapter();
            string gameRoot = Directory.GetParent(Application.dataPath).FullName;
            string marker = Path.Combine(
                gameRoot, "mods", "ReduxTestHarness", "test-mode.enabled");
            bool enabled = File.Exists(marker) ||
                string.Equals(
                    Environment.GetEnvironmentVariable("REDUX_TEST_ENABLE"),
                    "1",
                    StringComparison.Ordinal);
            if (!enabled)
            {
                SWLogger.LogWarning(
                    "[ReduxTestHarness] Test endpoint is disabled. Create " + marker +
                    " or launch with REDUX_TEST_ENABLE=1.");
                return;
            }

            _dismissPhotosensitivityWarning = string.Equals(
                Environment.GetEnvironmentVariable("REDUX_TEST_DISMISS_PHOTOSENSITIVITY"),
                "1",
                StringComparison.Ordinal);
            _includeStartupLogs = string.Equals(
                Environment.GetEnvironmentVariable("REDUX_TEST_INCLUDE_STARTUP_LOGS"),
                "1",
                StringComparison.Ordinal);
            _startupWarningProbeDeadline = Time.realtimeSinceStartup + 30f;
            _testApiRegistration = TestApiRegistry.Register(
                "ReduxTestHarness",
                ConfigureHarnessTestApi);

            int port = DefaultPort;
            string configuredPort = Environment.GetEnvironmentVariable("REDUX_TEST_PORT");
            int parsedPort;
            if (!string.IsNullOrWhiteSpace(configuredPort) &&
                int.TryParse(configuredPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedPort) &&
                parsedPort > 0 && parsedPort <= ushort.MaxValue)
            {
                port = parsedPort;
            }

            _server = new JsonLineBridgeServer(message =>
                SWLogger.LogInfo("[ReduxTestHarness/Bridge] " + message));
            JObject nativeCli = CliIntegrationProbe.Snapshot();
            if ((bool)nativeCli["available"])
            {
                SWLogger.LogInfo(
                    "[ReduxTestHarness] Redux CliIntegration detected in " +
                    (string)nativeCli["assembly"] + ".");
            }
            else
            {
                SWLogger.LogWarning(
                    "[ReduxTestHarness] Redux CliIntegration is not present in the " +
                    "loaded player assemblies; using the test-only lifecycle bridge.");
            }
            try
            {
                _server.Start(port);
            }
            catch (Exception error)
            {
                SWLogger.LogError("[ReduxTestHarness/Bridge] Failed to start: " + error);
                _server.Dispose();
                _server = null;
            }
        }

        private void Update()
        {
            TryDismissStartupWarning();

            if (_runner != null)
            {
                _runner.Tick();
            }

            if (_server == null)
            {
                return;
            }

            BridgeCommand command;
            int budget = 16;
            while (budget-- > 0 && _server.TryDequeue(out command))
            {
                if (command.IsAbandoned != null && command.IsAbandoned())
                {
                    continue;
                }
                ProcessCommand(command);
            }
        }

        private void TryDismissStartupWarning()
        {
            if (!_dismissPhotosensitivityWarning || _startupWarningDismissed ||
                Time.realtimeSinceStartup > _startupWarningProbeDeadline ||
                Time.realtimeSinceStartup < _nextStartupWarningProbe)
            {
                return;
            }

            _nextStartupWarningProbe = Time.realtimeSinceStartup + 0.25f;
            string error;
            if (StartupDialogAutomation.TryDismissPhotosensitivityWarning(out error))
            {
                _startupWarningDismissed = true;
                SWLogger.LogInfo(
                    "[ReduxTestHarness] Dismissed KSP2's photosensitivity warning for this automated launch.");
            }
            else if (!string.IsNullOrEmpty(error))
            {
                _dismissPhotosensitivityWarning = false;
                SWLogger.LogWarning("[ReduxTestHarness] " + error);
            }
        }

        private void LateUpdate()
        {
            if (_game != null)
            {
                _game.ApplyCameraOverride();
            }
        }

        private void ProcessCommand(BridgeCommand command)
        {
            try
            {
                switch (command.Command)
                {
                    case "ping":
                        command.Complete(PingResponse());
                        break;
                    case "run_script":
                        command.Complete(StartRun(command.Payload));
                        break;
                    case "get_status":
                        command.Complete(StatusResponse(command.Payload));
                        break;
                    case "cancel_test":
                        command.Complete(CancelRun(command.Payload));
                        break;
                    case "shutdown":
                        command.Complete(JsonLineBridgeServer.Success());
                        bool quit = (bool?)command.Payload["quitGame"] ?? false;
                        if (quit)
                        {
                            Application.Quit();
                        }
                        else
                        {
                            _server.Dispose();
                            _server = null;
                        }
                        break;
                    default:
                        command.Complete(JsonLineBridgeServer.Error(
                            "unknown_command",
                            "Unknown command '" + command.Command + "'."));
                        break;
                }
            }
            catch (Exception error)
            {
                SWLogger.LogError("[ReduxTestHarness/Bridge] Command failed: " + error);
                command.Complete(JsonLineBridgeServer.Error("command_failed", error.Message));
            }
        }

        private JObject PingResponse()
        {
            JObject response = JsonLineBridgeServer.Success();
            response["ready"] = _game != null && _game.IsReady;
            response["gameState"] = _game == null ? "Unavailable" : _game.State;
            response["testStatus"] = _runner == null ? "idle" : _runner.Status;
            response["protocolVersion"] = 1;
            response["harnessVersion"] =
                Assembly.GetExecutingAssembly().GetName().Version.ToString();
            response["activeModCount"] =
                SpaceWarp2.API.Mods.PluginList.AllEnabledAndActivePlugins.Count;
            response["reduxCliIntegration"] = CliIntegrationProbe.Snapshot();
            response["startupWarningVisible"] =
                StartupDialogAutomation.IsPhotosensitivityWarningVisible();
            return response;
        }

        private JObject StartRun(JObject payload)
        {
            if (_game == null || !_game.IsReady)
            {
                return JsonLineBridgeServer.Error(
                    "game_not_ready",
                    "KSP2 game services are not ready (state: " +
                    (_game == null ? "Unavailable" : _game.State) + ").");
            }
            if (_runner != null && !_runner.IsFinished)
            {
                return JsonLineBridgeServer.Error(
                    "test_in_progress",
                    "Another test is already running: " + _runner.RunId);
            }

            string runId = RequiredString(payload, "runId");
            string script = RequiredString(payload, "script");
            string scriptPath = RequiredString(payload, "scriptPath");
            string resultsRoot = RequiredString(payload, "resultsRoot");
            string fixturesRoot = RequiredString(payload, "fixturesRoot");
            int timeout = (int?)payload["timeoutSeconds"] ?? 180;
            timeout = Mathf.Clamp(timeout, 1, 86400);
            bool failOnLogErrors = (bool?)payload["failOnLogErrors"] ?? false;

            _runner = new LuaTestRunner(
                this,
                _game,
                runId,
                scriptPath,
                script,
                resultsRoot,
                fixturesRoot,
                timeout,
                _includeStartupLogs,
                failOnLogErrors,
                message => SWLogger.LogInfo("[ReduxTestHarness/Runner] " + message),
                message => SWLogger.LogError("[ReduxTestHarness/Runner] " + message));
            _includeStartupLogs = false;

            JObject response = JsonLineBridgeServer.Success();
            response["accepted"] = true;
            response["runId"] = runId;
            response["artifactDirectory"] = _runner.Artifacts.ArtifactDirectory;
            return response;
        }

        private JObject StatusResponse(JObject payload)
        {
            string requestedRun = (string)payload["runId"];
            if (_runner == null)
            {
                JObject idle = JsonLineBridgeServer.Success();
                idle["status"] = "idle";
                return idle;
            }
            if (!string.IsNullOrEmpty(requestedRun) && requestedRun != _runner.RunId)
            {
                return JsonLineBridgeServer.Error(
                    "run_not_found",
                    "Run is not current or retained: " + requestedRun);
            }

            TestStatusSnapshot snapshot = _runner.Snapshot();
            JObject response = JsonLineBridgeServer.Success();
            response["runId"] = snapshot.RunId;
            response["name"] = snapshot.Name;
            response["status"] = snapshot.Status;
            response["reportPath"] = snapshot.ReportPath;
            response["error"] = snapshot.Error;
            response["screenshots"] = JArray.FromObject(snapshot.Screenshots);
            return response;
        }

        private JObject CancelRun(JObject payload)
        {
            string requestedRun = (string)payload["runId"];
            if (_runner == null ||
                (!string.IsNullOrEmpty(requestedRun) && requestedRun != _runner.RunId))
            {
                return JsonLineBridgeServer.Error("run_not_found", "No matching test is running.");
            }
            _runner.Cancel("Cancelled by redux-test.");
            JObject response = JsonLineBridgeServer.Success();
            response["status"] = _runner.Status;
            response["reportPath"] = _runner.Artifacts.ReportPath;
            return response;
        }

        private static string RequiredString(JObject payload, string name)
        {
            string value = (string)payload[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Request field '" + name + "' is required.");
            }
            return value;
        }

        private static void ConfigureHarnessTestApi(Script script, Table table)
        {
            table.Set(
                "protocol_version",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxTestHarness.protocol_version",
                    (context, arguments) => DynValue.NewNumber(1)));
            table.Set(
                "version",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxTestHarness.version",
                    (context, arguments) => DynValue.NewString(
                        Assembly.GetExecutingAssembly().GetName().Version.ToString())));
        }

        private void OnDestroy()
        {
            if (_runner != null && !_runner.IsFinished)
            {
                _runner.Cancel("ReduxTestHarness was unloaded.");
            }
            _runner = null;
            if (_testApiRegistration != null)
            {
                _testApiRegistration.Dispose();
                _testApiRegistration = null;
            }
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
        }
    }
}
