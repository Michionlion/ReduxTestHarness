# Prototype architecture

## Runtime boundary

The prototype keeps the requested boundary:

```text
redux-test CLI
  -> localhost JSON-lines lifecycle bridge
  -> Lua script
  -> semantic Test API implemented in C#
  -> KSP2 / Redux services
```

The bridge accepts only `ping`, `run_script`, `get_status`, `cancel_test`, and `shutdown`. It binds `IPAddress.Loopback`, has no remote binding option, and starts only when `mods/ReduxTestHarness/test-mode.enabled` exists or `REDUX_TEST_ENABLE=1` is present.

## Existing runtime reuse

The installed Redux build contains:

- `KSP.ScriptInterop.impl.moonsharp.ScriptEnvironment`;
- `IScriptEnvironment.Fork`;
- coroutine-capable MoonSharp execution;
- `LuaPipePlugin64.dll`, queried by KSP2's `ScriptInteroperability` once per fixed update;
- a SpaceWarp console bridge that looks for an optional `Redux.CliIntegration` runtime.

The native Lua pipe is a synchronous context/script injector and its standalone client/protocol is not present in this installation. The documented `Redux.CliIntegration` types are absent from the shipped player assembly even though SpaceWarp contains a reflection bridge for them. The prototype therefore uses the allowed thin endpoint but does not create another scripting engine: every run forks `GameInstance.ScriptEnvironment` and executes through its existing MoonSharp `Script` and globals. See [cli-integration.md](cli-integration.md) for the version-matched binary investigation and restoration path.

If Redux later ships a supported external client for its CLI integration, `JsonLineBridgeServer` is the replaceable layer. `LuaTestRunner`, the semantic API, and artifacts do not depend on the transport.

## Coroutine scheduling

Lua callbacks that need time return a MoonSharp yield request. `LuaTestRunner.Tick` resumes the coroutine only when the semantic condition completes or fails:

- frame/time deadline reached;
- game state matches;
- save-load callback completes;
- requested vessel is active and controllable;
- screenshot file is present and non-empty;
- Lua predicate returns true.

An overall run deadline is independent of individual waits. Assertions are flushed to `report.json` immediately so partial evidence survives most test failures.

## Game operations

`KspGameAdapter` uses the current public Redux/KSP2 services:

- `SaveLoadManager.LoadGameFromFile` for fixtures;
- `GameStateMachine.GetGameState` for state;
- `UniverseModel.GetAllVessels` and `ViewController.SetActiveVehicle` for flight selection;
- `FlightCtrlState`, `ActivateNextStage`, and the SAS action group for controls;
- `UniverseCameraManager` plus a late-applied Unity camera transform for deterministic vessel-relative cameras;
- `GraphicsSettings` for render scale, anti-aliasing, cloud settings, and boolean graphics switches;
- `ScreenCapture.CaptureScreenshot` for PNG capture.

Camera overrides are released when the test completes or is cancelled.

## Artifacts

The game writes the canonical report because it owns assertion, screenshot, fixture, and Lua-error state. Each run produces:

```text
.test-results/<timestamp>/<script-slug>/
  report.json
  summary.md
  test.lua
  screenshots/*.png
  logs/Ksp2.log
  logs/Player.log
  logs/LogOutput.log
```

Only logs that exist are copied. The report schema is [schemas/report.schema.json](../schemas/report.schema.json).

## Known prototype seams

- A real known save is required for the included orbit test and is not committed.
- The camera override targets KSP2's current Unity camera. Multi-camera-stack-specific composition may need a Redux camera service after representative in-game validation.
- `taa` currently maps to KSP2 anti-aliasing level `3`; Redux feature mods should add explicit semantic render setting adapters rather than depend on this generic mapping.
- `vfxQuality` maps the requested quality label to Unity's quality level. A Redux-owned VFX setting should replace this when one is exposed.
- A hard process crash can leave a partial `running` report. The CLI still returns infrastructure exit code `2`; a future watchdog can finalize the partial report out-of-process.
- Automated image-diff approval and UI automation remain intentionally out of scope.
