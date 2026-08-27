# Redux Test Harness

Developer-only automated in-game testing for KSP2 Redux. The prototype provides:

- `redux-test`, a PowerShell CLI with conventional exit codes;
- a localhost-only JSON-lines bridge with five commands;
- Lua execution in a fork of KSP2's existing MoonSharp environment;
- a semantic `Test` API for game state, flight, waits, camera, selected rendering settings, screenshots, assertions, and reporting;
- active-mod discovery and an extension registry for mod-owned semantic test APIs;
- isolated per-run `report.json`, `summary.md`, screenshots, attachments, bounded logs, and the executed Lua script.

The external protocol does not expose KSP2 objects or duplicate semantic operations. It only starts/cancels Lua tests and reports lifecycle state.

## Build and install

The repository does not require a separately installed .NET SDK. `build-mod.ps1` uses the Unity 6000.4.1f1 compiler and compiles against the installed Redux assemblies.

```powershell
pwsh .\scripts\build-mod.ps1
pwsh .\scripts\install-mod.ps1
```

`install-mod.ps1` copies the mod to `KSP2\mods\ReduxTestHarness` and creates `test-mode.enabled`. The bridge will not start unless that marker exists or KSP2 is launched with `REDUX_TEST_ENABLE=1`.

Override the default game location with `-GameRoot` or `KSP2_ROOT`. If Unity
6000.4.1f1 is not in its standard location, pass `-UnityRoot` or set
`UNITY_6000_4_1F1_ROOT`:

```powershell
$env:KSP2_ROOT = 'D:\SteamLibrary\steamapps\common\Kerbal Space Program 2'
$env:UNITY_6000_4_1F1_ROOT = 'D:\Unity\6000.4.1f1'
pwsh .\scripts\install-mod.ps1
```

KSP2 must be closed before installation because Windows cannot replace a
loaded mod DLL. The install script detects this and reports the player process
IDs rather than failing partway through a copy.

## Run

With KSP2 already running:

```powershell
.\redux-test status
.\redux-test run .\tests\smoke\orbit-render.lua --timeout 180
```

For a fixture-free runtime/bridge/capture check, use:

```powershell
.\redux-test run .\tests\smoke\runtime-ready.lua --launch
```

Or let the CLI launch and later close KSP2:

```powershell
.\redux-test run .\tests\smoke\orbit-render.lua --launch
```

Use `--keep-open` to leave a CLI-launched game running. Automated launches
dismiss KSP2's photosensitivity page after verifying that all EULA, privacy,
and terms versions were already accepted; the harness never accepts those
agreements. Pass `--KeepStartupWarning` to retain the photosensitivity page.
A fresh launch also waits for the unobstructed main menu to remain ready for
two seconds before Lua begins. Override that grace period with
`--StartupSettleSeconds`. `--results`, `--fixtures`, `--GameRoot`, and `--Port`
override their corresponding defaults. `--FailOnLogErrors` fails the test if a
new exception signature is written while that run is active.

Exit codes are:

- `0`: test passed;
- `1`: Lua test/assertion failed or was cancelled;
- `2`: launch, bridge, protocol, or overall-timeout failure.

## Fixtures

`Test.game.load_save("rendering/kerbin-orbit")` resolves, in order:

1. `fixtures/rendering/kerbin-orbit`
2. `fixtures/rendering/kerbin-orbit.json`
3. `fixtures/rendering/kerbin-orbit.json.gz`

Place a known KSP2 save at one of those paths. Save contents are deliberately not checked into this prototype.

## Lua API

The implemented MVP surface is:

```text
Test.name
Test.game.state / is_ready / load_save / wait_for_state / pause / unpause
Test.flight.start / active_vessel / find_vessel / set_throttle / stage / set_sas
Test.wait.frames / seconds / until_ (`Test.wait["until"]` is also supported)
Test.camera.mode / target_vessel / set / orbit
Test.render.set / get / wait_stable
Test.mod.is_loaded / info / list / extension
Test.capture.screenshot
Test.assert.true_ / false_ / equal / not_equal / near / greater / less
Test.report.note / log / metric / value / attach / fail_on_log / fail_on_log_errors
```

See [tests/smoke/orbit-render.lua](tests/smoke/orbit-render.lua) for a complete
vertical-slice test, [tests/smoke/launchpad-reload.lua](tests/smoke/launchpad-reload.lua)
for the same-process save-reload regression, [docs/architecture.md](docs/architecture.md)
for integration details and current seams, [docs/extensions.md](docs/extensions.md)
for mod-owned semantic API registration, [docs/review.md](docs/review.md) for the
full harness audit, and [docs/validation.md](docs/validation.md)
for the completed player smoke evidence.

The shipped Redux player currently lacks its documented `CliIntegration`
runtime. See [docs/cli-integration.md](docs/cli-integration.md) for the
version-matched investigation, a binary probe, and the recommended Redux-side
restoration.

## Verify without launching KSP2

```powershell
pwsh .\tests\run-tests.ps1
```

This parses the scripts, validates metadata/schema files, compiles the in-game DLL against the installed Redux build, and exercises CLI status/run behavior against a localhost mock bridge.
