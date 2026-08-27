using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Game;
using KSP.ScriptInterop;
using MoonSharp.Interpreter;
using UnityEngine;

namespace ReduxTestHarness
{
    internal sealed class LuaTestRunner
    {
        private static readonly DynValue[] NoValues = new DynValue[0];

        private readonly MonoBehaviour _owner;
        private readonly KspGameAdapter _game;
        private readonly string _fixturesRoot;
        private readonly DateTime _deadlineUtc;
        private readonly ArtifactWriter _artifacts;
        private readonly Action<string> _log;
        private Script _script;
        private MoonSharp.Interpreter.Coroutine _coroutine;
        private Func<bool> _pendingCondition;
        private Func<DynValue> _pendingResult;
        private string _pendingDescription;
        private float _pendingDeadline;
        private bool _finished;
        private Exception _terminalError;

        public LuaTestRunner(
            MonoBehaviour owner,
            KspGameAdapter game,
            string runId,
            string scriptPath,
            string scriptText,
            string resultsRoot,
            string fixturesRoot,
            int timeoutSeconds,
            Action<string> log)
        {
            _owner = owner;
            _game = game;
            _fixturesRoot = Path.GetFullPath(fixturesRoot);
            _deadlineUtc = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            _artifacts = new ArtifactWriter(runId, scriptPath, scriptText, resultsRoot);
            _log = log;

            try
            {
                InitializeScript(scriptText, scriptPath);
            }
            catch (Exception error)
            {
                _terminalError = error;
                Finish("failed");
            }
        }

        public string RunId { get { return _artifacts.Report.RunId; } }
        public bool IsFinished { get { return _finished; } }
        public string Status { get { return _artifacts.Report.Status; } }
        public ArtifactWriter Artifacts { get { return _artifacts; } }

        public void Tick()
        {
            if (_finished)
            {
                return;
            }
            if (DateTime.UtcNow >= _deadlineUtc)
            {
                Fail(new TimeoutException("Test exceeded its overall timeout."));
                return;
            }

            if (_pendingCondition != null)
            {
                bool complete;
                try
                {
                    complete = _pendingCondition();
                }
                catch (Exception error)
                {
                    Fail(error);
                    return;
                }

                if (!complete)
                {
                    if (Time.realtimeSinceStartup >= _pendingDeadline)
                    {
                        Fail(new TimeoutException("Timed out waiting for " + _pendingDescription + "."));
                    }
                    return;
                }

                Func<DynValue> resultFactory = _pendingResult;
                ClearPending();
                DynValue result = DynValue.Nil;
                try
                {
                    if (resultFactory != null)
                    {
                        result = resultFactory() ?? DynValue.Nil;
                    }
                }
                catch (Exception error)
                {
                    Fail(error);
                    return;
                }
                Resume(result);
                return;
            }

            if (_coroutine != null && _coroutine.State == CoroutineState.Suspended)
            {
                Resume();
            }
        }

        public void Cancel(string reason)
        {
            if (_finished)
            {
                return;
            }
            _terminalError = new OperationCanceledException(reason ?? "Test cancelled.");
            Finish("cancelled");
        }

        public TestStatusSnapshot Snapshot()
        {
            var snapshot = new TestStatusSnapshot
            {
                RunId = RunId,
                Name = _artifacts.Report.Name,
                Status = Status,
                ReportPath = _artifacts.ReportPath,
                Error = _terminalError == null ? null : _terminalError.Message
            };
            for (int index = 0; index < _artifacts.Report.Screenshots.Count; index++)
            {
                snapshot.Screenshots.Add(Path.GetFullPath(Path.Combine(
                    _artifacts.ArtifactDirectory,
                    _artifacts.Report.Screenshots[index].Replace('/', Path.DirectorySeparatorChar))));
            }
            return snapshot;
        }

        private void InitializeScript(string scriptText, string scriptPath)
        {
            GameInstance game = GameManager.Instance == null ? null : GameManager.Instance.Game;
            if (game == null || game.ScriptEnvironment == null)
            {
                throw new InvalidOperationException("KSP2 MoonSharp script environment is not initialized.");
            }

            IScriptEnvironment fork = game.ScriptEnvironment.Fork(
                "ReduxTestHarness-" + RunId,
                false);
            var environment = fork as KSP.ScriptInterop.impl.moonsharp.ScriptEnvironment;
            if (environment == null)
            {
                throw new InvalidOperationException(
                    "The active KSP2 script environment is not MoonSharp-backed.");
            }

            _script = environment.script;
            Table globals = environment.globals;
            globals.Set("Test", DynValue.NewTable(CreateTestApi()));
            DynValue function = _script.LoadString(scriptText, globals, scriptPath);
            DynValue coroutineValue = _script.CreateCoroutine(function);
            _coroutine = coroutineValue.Coroutine;
            _coroutine.AutoYieldCounter = 20000;
            Resume();
        }

        private Table CreateTestApi()
        {
            var test = new Table(_script);
            SetCallback(test, "name", (context, args) =>
            {
                _artifacts.Report.Name = RequiredString(args, 0, "Test.name");
                _artifacts.Flush();
                return DynValue.Nil;
            });

            test.Set("game", DynValue.NewTable(CreateGameApi()));
            test.Set("flight", DynValue.NewTable(CreateFlightApi()));
            test.Set("wait", DynValue.NewTable(CreateWaitApi()));
            test.Set("camera", DynValue.NewTable(CreateCameraApi()));
            test.Set("render", DynValue.NewTable(CreateRenderApi()));
            test.Set("capture", DynValue.NewTable(CreateCaptureApi()));
            test.Set("assert", DynValue.NewTable(CreateAssertApi()));
            test.Set("report", DynValue.NewTable(CreateReportApi()));
            return test;
        }

        private Table CreateGameApi()
        {
            var api = new Table(_script);
            SetCallback(api, "state", (context, args) => DynValue.NewString(_game.State));
            SetCallback(api, "is_ready", (context, args) => DynValue.NewBoolean(_game.IsReady));
            SetCallback(api, "load_save", (context, args) =>
            {
                string fixture = RequiredString(args, 0, "Test.game.load_save");
                bool completed = false;
                bool success = false;
                string error = null;
                _artifacts.Report.Fixture = fixture;
                _artifacts.Flush();
                _game.LoadSave(fixture, _fixturesRoot, (loaded, message) =>
                {
                    success = loaded;
                    error = message;
                    completed = true;
                });
                return YieldUntil(
                    () => completed,
                    90.0f,
                    "save fixture '" + fixture + "'",
                    () =>
                    {
                        if (!success)
                        {
                            throw new InvalidOperationException(error ?? "Save load failed.");
                        }
                        return DynValue.True;
                    });
            });
            SetCallback(api, "wait_for_state", (context, args) =>
            {
                string state = RequiredString(args, 0, "Test.game.wait_for_state");
                float timeout = OptionalNumber(args, 1, 30.0f);
                return YieldUntil(
                    () => string.Equals(_game.State, state, StringComparison.OrdinalIgnoreCase),
                    timeout,
                    "game state " + state,
                    () => DynValue.NewString(_game.State));
            });
            SetCallback(api, "pause", (context, args) =>
            {
                _game.SetPaused(true);
                return DynValue.Nil;
            });
            SetCallback(api, "unpause", (context, args) =>
            {
                _game.SetPaused(false);
                return DynValue.Nil;
            });
            return api;
        }

        private Table CreateFlightApi()
        {
            var api = new Table(_script);
            SetCallback(api, "start", (context, args) =>
            {
                string name = RequiredString(args, 0, "Test.flight.start");
                KSP.Sim.impl.VesselComponent vessel = _game.FindVessel(name);
                if (vessel == null)
                {
                    throw new ScriptRuntimeException("Vessel was not found: " + name);
                }
                if (!ReferenceEquals(_game.ActiveVessel(), vessel) &&
                    !_game.ActivateVessel(vessel))
                {
                    throw new ScriptRuntimeException("KSP2 rejected activation of vessel: " + name);
                }
                return YieldUntil(
                    () => _game.IsActiveAndUsable(vessel),
                    30.0f,
                    "active flight vessel '" + name + "'",
                    () => DynValue.NewTable(_game.VesselSnapshot(_script, vessel)));
            });
            SetCallback(api, "active_vessel", (context, args) =>
            {
                Table snapshot = _game.VesselSnapshot(_script, _game.ActiveVessel());
                return snapshot == null ? DynValue.Nil : DynValue.NewTable(snapshot);
            });
            SetCallback(api, "find_vessel", (context, args) =>
            {
                Table snapshot = _game.VesselSnapshot(
                    _script,
                    _game.FindVessel(RequiredString(args, 0, "Test.flight.find_vessel")));
                return snapshot == null ? DynValue.Nil : DynValue.NewTable(snapshot);
            });
            SetCallback(api, "set_throttle", (context, args) =>
            {
                _game.SetThrottle(RequiredNumber(args, 0, "Test.flight.set_throttle"));
                return DynValue.Nil;
            });
            SetCallback(api, "stage", (context, args) =>
            {
                _game.Stage();
                return DynValue.Nil;
            });
            SetCallback(api, "set_sas", (context, args) =>
            {
                _game.SetSas(RequiredBoolean(args, 0, "Test.flight.set_sas"));
                return DynValue.Nil;
            });
            return api;
        }

        private Table CreateWaitApi()
        {
            var api = new Table(_script);
            SetCallback(api, "frames", (context, args) =>
            {
                int frames = (int)Math.Max(0, RequiredNumber(args, 0, "Test.wait.frames"));
                int target = Time.frameCount + frames;
                return YieldUntil(
                    () => Time.frameCount >= target,
                    Math.Max(5.0f, frames / 5.0f),
                    frames + " frames",
                    () => DynValue.Nil);
            });
            SetCallback(api, "seconds", (context, args) =>
            {
                float seconds = (float)Math.Max(0.0, RequiredNumber(args, 0, "Test.wait.seconds"));
                float target = Time.realtimeSinceStartup + seconds;
                return YieldUntil(
                    () => Time.realtimeSinceStartup >= target,
                    seconds + 5.0f,
                    seconds.ToString("0.###", CultureInfo.InvariantCulture) + " seconds",
                    () => DynValue.Nil);
            });
            Func<ScriptExecutionContext, CallbackArguments, DynValue> waitUntil = (context, args) =>
            {
                DynValue predicate = RequiredFunction(args, 0, "Test.wait.until");
                float timeout = OptionalNumber(args, 1, 30.0f);
                return YieldUntil(
                    () => _script.Call(predicate).CastToBool(),
                    timeout,
                    "Lua predicate",
                    () => DynValue.True);
            };
            SetCallback(api, "until", waitUntil);
            SetCallback(api, "until_", waitUntil);
            return api;
        }

        private Table CreateCameraApi()
        {
            var api = new Table(_script);
            SetCallback(api, "mode", (context, args) =>
            {
                _game.SetCameraMode(RequiredString(args, 0, "Test.camera.mode"));
                return DynValue.Nil;
            });
            SetCallback(api, "target_vessel", (context, args) =>
            {
                _game.TargetActiveVessel();
                return DynValue.Nil;
            });
            SetCallback(api, "orbit", (context, args) =>
            {
                Table options = RequiredTable(args, 0, "Test.camera.orbit");
                _game.SetOrbitCamera(
                    RequiredFieldNumber(options, "distance", "Test.camera.orbit"),
                    RequiredFieldNumber(options, "yaw", "Test.camera.orbit"),
                    RequiredFieldNumber(options, "pitch", "Test.camera.orbit"),
                    OptionalFieldNumber(options, "fov", 60.0));
                return DynValue.Nil;
            });
            SetCallback(api, "set", (context, args) =>
            {
                Table options = RequiredTable(args, 0, "Test.camera.set");
                Vector3 position = VectorField(options, "position");
                Vector3 rotation = VectorField(options, "rotation");
                _game.SetCamera(position, rotation, (float)OptionalFieldNumber(options, "fov", 60.0));
                return DynValue.Nil;
            });
            return api;
        }

        private Table CreateRenderApi()
        {
            var api = new Table(_script);
            SetCallback(api, "set", (context, args) =>
            {
                string name = RequiredString(args, 0, "Test.render.set");
                _game.SetRenderSetting(name, ToPlainValue(RequiredArgument(args, 1, "Test.render.set")));
                return DynValue.Nil;
            });
            SetCallback(api, "get", (context, args) =>
            {
                object value = _game.GetRenderSetting(RequiredString(args, 0, "Test.render.get"));
                return value == null ? DynValue.Nil : DynValue.FromObject(_script, value);
            });
            SetCallback(api, "wait_stable", (context, args) =>
            {
                int frames = args.Count == 0 ? 30 : (int)RequiredNumber(args, 0, "Test.render.wait_stable");
                int target = Time.frameCount + Math.Max(0, frames);
                return YieldUntil(
                    () => Time.frameCount >= target,
                    Math.Max(10.0f, frames / 5.0f),
                    "render stabilization",
                    () => DynValue.Nil);
            });
            return api;
        }

        private Table CreateCaptureApi()
        {
            var api = new Table(_script);
            SetCallback(api, "screenshot", (context, args) =>
            {
                string name = RequiredString(args, 0, "Test.capture.screenshot");
                int scale = 1;
                bool hideUi = true;
                int waitFrames = 0;
                if (args.Count > 1 && args[1].Type != DataType.Nil && args[1].Type != DataType.Void)
                {
                    Table options = RequiredTable(args, 1, "Test.capture.screenshot");
                    scale = (int)OptionalFieldNumber(options, "scale", 1.0);
                    hideUi = OptionalFieldBoolean(options, "hideUI", true);
                    waitFrames = (int)OptionalFieldNumber(options, "waitFrames", 0.0);
                }

                var capture = new CaptureState
                {
                    Path = _artifacts.NewScreenshotPath(name)
                };
                _owner.StartCoroutine(CaptureScreenshot(capture, scale, hideUi, waitFrames));
                return YieldUntil(
                    () => capture.Complete,
                    30.0f + Math.Max(0, waitFrames),
                    "screenshot '" + name + "'",
                    () =>
                    {
                        if (capture.Error != null)
                        {
                            throw capture.Error;
                        }
                        return DynValue.NewString(capture.Path);
                    });
            });
            return api;
        }

        private Table CreateAssertApi()
        {
            var api = new Table(_script);
            SetCallback(api, "true_", (context, args) => Assert(
                RequiredArgument(args, 0, "Test.assert.true_").CastToBool(),
                "condition is true", args, 1,
                ToPlainValue(args[0]), true));
            SetCallback(api, "false_", (context, args) => Assert(
                !RequiredArgument(args, 0, "Test.assert.false_").CastToBool(),
                "condition is false", args, 1,
                ToPlainValue(args[0]), false));
            SetCallback(api, "equal", (context, args) =>
            {
                DynValue actual = RequiredArgument(args, 0, "Test.assert.equal");
                DynValue expected = RequiredArgument(args, 1, "Test.assert.equal");
                return Assert(DynEquals(actual, expected), "values are equal", args, 2,
                    ToPlainValue(actual), ToPlainValue(expected));
            });
            SetCallback(api, "not_equal", (context, args) =>
            {
                DynValue actual = RequiredArgument(args, 0, "Test.assert.not_equal");
                DynValue expected = RequiredArgument(args, 1, "Test.assert.not_equal");
                return Assert(!DynEquals(actual, expected), "values are not equal", args, 2,
                    ToPlainValue(actual), ToPlainValue(expected));
            });
            SetCallback(api, "near", (context, args) =>
            {
                double actual = RequiredNumber(args, 0, "Test.assert.near");
                double expected = RequiredNumber(args, 1, "Test.assert.near");
                double tolerance = RequiredNumber(args, 2, "Test.assert.near");
                return Assert(Math.Abs(actual - expected) <= tolerance,
                    "values are within " + tolerance.ToString(CultureInfo.InvariantCulture),
                    args, 3, actual, expected);
            });
            SetCallback(api, "greater", (context, args) =>
            {
                double actual = RequiredNumber(args, 0, "Test.assert.greater");
                double expected = RequiredNumber(args, 1, "Test.assert.greater");
                return Assert(actual > expected, "actual > expected", args, 2, actual, expected);
            });
            SetCallback(api, "less", (context, args) =>
            {
                double actual = RequiredNumber(args, 0, "Test.assert.less");
                double expected = RequiredNumber(args, 1, "Test.assert.less");
                return Assert(actual < expected, "actual < expected", args, 2, actual, expected);
            });
            return api;
        }

        private Table CreateReportApi()
        {
            var api = new Table(_script);
            SetCallback(api, "note", (context, args) =>
            {
                _artifacts.Report.Notes.Add(RequiredString(args, 0, "Test.report.note"));
                _artifacts.Flush();
                return DynValue.Nil;
            });
            SetCallback(api, "metric", (context, args) =>
            {
                _artifacts.Report.Metrics[RequiredString(args, 0, "Test.report.metric")] =
                    RequiredNumber(args, 1, "Test.report.metric");
                _artifacts.Flush();
                return DynValue.Nil;
            });
            SetCallback(api, "value", (context, args) =>
            {
                _artifacts.Report.Values[RequiredString(args, 0, "Test.report.value")] =
                    ToPlainValue(RequiredArgument(args, 1, "Test.report.value"));
                _artifacts.Flush();
                return DynValue.Nil;
            });
            SetCallback(api, "attach", (context, args) =>
            {
                string path = RequiredString(args, 0, "Test.report.attach");
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(_artifacts.ArtifactDirectory, path);
                }
                _artifacts.AddAttachment(path);
                _artifacts.Flush();
                return DynValue.NewString(Path.GetFullPath(path));
            });
            return api;
        }

        private IEnumerator CaptureScreenshot(
            CaptureState capture,
            int scale,
            bool hideUi,
            int waitFrames)
        {
            var enabled = new List<Canvas>();
            try
            {
                for (int frame = 0; frame < Math.Max(0, waitFrames); frame++)
                {
                    yield return new WaitForEndOfFrame();
                }

                if (hideUi)
                {
                    Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include);
                    for (int index = 0; index < canvases.Length; index++)
                    {
                        if (canvases[index] != null && canvases[index].enabled)
                        {
                            enabled.Add(canvases[index]);
                            canvases[index].enabled = false;
                        }
                    }
                }

                yield return new WaitForEndOfFrame();
                try
                {
                    _game.ApplyCameraOverride();
                    ScreenCapture.CaptureScreenshot(capture.Path, Math.Max(1, scale));
                }
                catch (Exception error)
                {
                    capture.Error = error;
                    capture.Complete = true;
                    yield break;
                }
                for (int frame = 0; frame < 120; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    bool ready = false;
                    try
                    {
                        ready = File.Exists(capture.Path) && new FileInfo(capture.Path).Length > 0;
                    }
                    catch (Exception error)
                    {
                        capture.Error = error;
                        capture.Complete = true;
                        yield break;
                    }
                    if (ready)
                    {
                        _artifacts.AddScreenshot(capture.Path);
                        _artifacts.Flush();
                        capture.Complete = true;
                        yield break;
                    }
                }
                capture.Error = new IOException("KSP2 did not write screenshot: " + capture.Path);
                capture.Complete = true;
            }
            finally
            {
                for (int index = 0; index < enabled.Count; index++)
                {
                    if (enabled[index] != null)
                    {
                        enabled[index].enabled = true;
                    }
                }
            }
        }

        private DynValue Assert(
            bool passed,
            string expression,
            CallbackArguments args,
            int messageIndex,
            object actual,
            object expected)
        {
            string message = OptionalString(args, messageIndex, null);
            _artifacts.Report.Assertions.Add(new AssertionRecord
            {
                Status = passed ? "passed" : "failed",
                Expression = expression,
                Message = message,
                Actual = actual,
                Expected = expected
            });
            _artifacts.Flush();
            if (!passed)
            {
                throw new ScriptRuntimeException(message ?? "Assertion failed: " + expression);
            }
            return DynValue.True;
        }

        private DynValue YieldUntil(
            Func<bool> condition,
            float timeoutSeconds,
            string description,
            Func<DynValue> result)
        {
            if (_pendingCondition != null)
            {
                throw new ScriptRuntimeException("Only one asynchronous Test operation may be active at a time.");
            }
            _pendingCondition = condition;
            _pendingResult = result;
            _pendingDescription = description;
            _pendingDeadline = Time.realtimeSinceStartup + Math.Max(0.1f, timeoutSeconds);
            return DynValue.NewYieldReq(NoValues);
        }

        private void Resume(params DynValue[] values)
        {
            if (_finished || _coroutine == null)
            {
                return;
            }
            try
            {
                _coroutine.Resume(values ?? NoValues);
                if (_coroutine.State == CoroutineState.Dead)
                {
                    Finish("passed");
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        private void Fail(Exception error)
        {
            _terminalError = error;
            _log("Test failed: " + error);
            Finish("failed");
        }

        private void Finish(string status)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            ClearPending();
            _game.ClearCameraOverride();
            _artifacts.Complete(status, _terminalError);
        }

        private void ClearPending()
        {
            _pendingCondition = null;
            _pendingResult = null;
            _pendingDescription = null;
            _pendingDeadline = 0.0f;
        }

        private static void SetCallback(Table table, string name, Func<ScriptExecutionContext, CallbackArguments, DynValue> callback)
        {
            table.Set(name, DynValue.NewCallback(callback, "Test." + name));
        }

        private static DynValue RequiredArgument(CallbackArguments args, int index, string function)
        {
            if (args.Count <= index || args[index].Type == DataType.Void)
            {
                throw new ScriptRuntimeException(function + " requires argument " + (index + 1) + ".");
            }
            return args[index];
        }

        private static string RequiredString(CallbackArguments args, int index, string function)
        {
            DynValue value = RequiredArgument(args, index, function);
            if (value.Type != DataType.String)
            {
                throw new ScriptRuntimeException(function + " argument " + (index + 1) + " must be a string.");
            }
            return value.String;
        }

        private static string OptionalString(CallbackArguments args, int index, string fallback)
        {
            if (args.Count <= index || args[index].IsNil())
            {
                return fallback;
            }
            return args[index].CastToString();
        }

        private static double RequiredNumber(CallbackArguments args, int index, string function)
        {
            DynValue value = RequiredArgument(args, index, function);
            if (value.Type != DataType.Number)
            {
                throw new ScriptRuntimeException(function + " argument " + (index + 1) + " must be a number.");
            }
            return value.Number;
        }

        private static bool RequiredBoolean(CallbackArguments args, int index, string function)
        {
            DynValue value = RequiredArgument(args, index, function);
            if (value.Type != DataType.Boolean)
            {
                throw new ScriptRuntimeException(function + " argument " + (index + 1) + " must be a boolean.");
            }
            return value.Boolean;
        }

        private static float OptionalNumber(CallbackArguments args, int index, float fallback)
        {
            return args.Count <= index || args[index].IsNil()
                ? fallback
                : (float)args[index].Number;
        }

        private static Table RequiredTable(CallbackArguments args, int index, string function)
        {
            DynValue value = RequiredArgument(args, index, function);
            if (value.Type != DataType.Table)
            {
                throw new ScriptRuntimeException(function + " argument " + (index + 1) + " must be a table.");
            }
            return value.Table;
        }

        private static DynValue RequiredFunction(CallbackArguments args, int index, string function)
        {
            DynValue value = RequiredArgument(args, index, function);
            if (value.Type != DataType.Function && value.Type != DataType.ClrFunction)
            {
                throw new ScriptRuntimeException(function + " argument " + (index + 1) + " must be a function.");
            }
            return value;
        }

        private static double RequiredFieldNumber(Table table, string field, string function)
        {
            DynValue value = table.Get(field);
            if (value.Type != DataType.Number)
            {
                throw new ScriptRuntimeException(function + " requires numeric field '" + field + "'.");
            }
            return value.Number;
        }

        private static double OptionalFieldNumber(Table table, string field, double fallback)
        {
            DynValue value = table.Get(field);
            return value.IsNil() ? fallback : value.Number;
        }

        private static bool OptionalFieldBoolean(Table table, string field, bool fallback)
        {
            DynValue value = table.Get(field);
            return value.IsNil() ? fallback : value.Boolean;
        }

        private static Vector3 VectorField(Table table, string field)
        {
            DynValue value = table.Get(field);
            if (value.Type != DataType.Table)
            {
                throw new ScriptRuntimeException("Camera field '" + field + "' must be a three-number array.");
            }
            return new Vector3(
                (float)value.Table.Get(1).Number,
                (float)value.Table.Get(2).Number,
                (float)value.Table.Get(3).Number);
        }

        private static bool DynEquals(DynValue left, DynValue right)
        {
            if (left.Type == DataType.Number && right.Type == DataType.Number)
            {
                return left.Number.Equals(right.Number);
            }
            if (left.Type == DataType.String && right.Type == DataType.String)
            {
                return string.Equals(left.String, right.String, StringComparison.Ordinal);
            }
            if (left.Type == DataType.Boolean && right.Type == DataType.Boolean)
            {
                return left.Boolean == right.Boolean;
            }
            if (left.IsNil() && right.IsNil())
            {
                return true;
            }
            return ReferenceEquals(left.ToObject(), right.ToObject());
        }

        private static object ToPlainValue(DynValue value)
        {
            switch (value.Type)
            {
                case DataType.Nil:
                case DataType.Void:
                    return null;
                case DataType.Boolean:
                    return value.Boolean;
                case DataType.Number:
                    return value.Number;
                case DataType.String:
                    return value.String;
                case DataType.Table:
                    var dictionary = new Dictionary<string, object>();
                    foreach (TablePair pair in value.Table.Pairs)
                    {
                        dictionary[pair.Key.ToPrintString()] = ToPlainValue(pair.Value);
                    }
                    return dictionary;
                default:
                    return value.ToPrintString();
            }
        }

        private sealed class CaptureState
        {
            public string Path;
            public bool Complete;
            public Exception Error;
        }
    }
}
