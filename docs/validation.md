# Prototype validation

Validated locally on 2026-08-27 with:

- KSP2 application version `0.2.3.0`;
- Redux `0.2.8.5.103184-beta`, commit `ffc94930`;
- Unity `6000.4.1f1` Windows player;
- Redux Test Harness `0.1.0.0`;
- NVIDIA GeForce RTX 5070 Ti.

The fixture-free runtime command passed:

```powershell
.\redux-test run .\tests\smoke\runtime-ready.lua --launch
```

Observed end-to-end behavior:

1. The CLI launched KSP2 and waited for the test bridge.
2. SpaceWarp registered and initialized `ReduxTestHarness`.
3. The bridge bound to `127.0.0.1:28542` and reported that native Redux
   `CliIntegration` was absent.
4. The existing KSP2 MoonSharp environment executed the Lua test as a
   coroutine.
5. One assertion passed and game state was recorded as `MainMenu`.
6. KSP2 wrote a 2560x1440 PNG screenshot.
7. The harness copied `Ksp2.log`, `Player.log`, and the legacy
   `BepInEx/LogOutput.log`.
8. `report.json` and `summary.md` were finalized with status `passed` and no
   harness/test errors.
9. The CLI printed the report and screenshot paths, returned exit code `0`,
   and shut down the KSP2 process it launched.

On a cold process launch, KSP2 displayed its photosensitivity page despite the
installed Redux config containing `"Disable photosensitivity warning": true`.
The CLI now opts automated launches into a narrowly scoped startup hook that
dismisses only that page, and only after KSP2 reports that the already-recorded
EULA, privacy-policy, and terms-of-service versions are current. The hook calls
the page's non-persisting finish transition and cannot record legal acceptance.
`--KeepStartupWarning` disables the hook. The resulting runtime screenshot was
an unobstructed main menu, and `Player.log` recorded the automatic dismissal.

The full launchpad command also passed from a fresh KSP2 process:

```powershell
.\redux-test run .\tests\smoke\launchpad-launch.lua --launch
```

Using the ignored local fixture `local/launchpad-fly-safe-15`, the Lua test
loaded directly into flight, selected `Fly Safe-15`, verified its initial
`PreLaunch` state on Kerbin, configured a vessel-relative camera, set SAS and
full throttle, staged, and waited for the vessel to become `Flying`. All five
assertions passed, the reported launch altitude was `125.38`, and the harness
captured before/after 2560x1440 screenshots. The run took `11.89` seconds after
the test was accepted and returned exit code `0`.

The static/mock suite also verifies PowerShell parsing, JSON parsing, C# player
assembly compilation, loopback status/run behavior, and CLI exit codes `0`,
`1`, and `2`:

```powershell
pwsh .\tests\run-tests.ps1
```

The captured player logs contain pre-existing `ReduxBetterAA` Harmony and
missing-addressable errors from that separately installed development mod.
They did not originate in `ReduxTestHarness`; automatic log collection made
them visible as intended.

Cold-process save load, vessel activation, deterministic flight camera,
throttle/SAS/stage control, state waiting, and capture are runtime-validated by
the launchpad test. An earlier direct second call to `LoadGameFromFile` failed
on a duplicate `Minmus` registration and then produced cascading science,
ambience, telemetry, and VFX null references. The harness now mirrors KSP2's
Campaign Load, Save/Load Dialog, and Quickload flows by calling
`GameInstance.ResetUniverse` before every semantic fixture load. The dedicated
`launchpad-reload.lua` regression test loads the same fixture twice in one game
process.
