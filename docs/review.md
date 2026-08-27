# Whole-harness review

This review covers the external CLI, localhost bridge, Lua runner, semantic
game adapter, artifact/report lifecycle, build/install utilities, fixtures,
schema, and live retained-process behavior.

## Resolved issues

- **Retained test contamination:** render scale, anti-aliasing, clouds, cloud
  quality, Unity quality, generic boolean graphics settings, pause state, flight
  camera mode, camera transform, rotation, and FOV are restored after every
  pass, failure, cancellation, or Lua initialization error.
- **Unsafe repeated save loading:** semantic loads reset the universe before
  deserialization and wait for both the load callback and ready game services.
- **Unbounded or stale artifacts:** logs are run-local, capped at 64 MiB per
  source, copied while the player holds them open, and truncated with warnings.
  External files/directories are copied under `attachments`; linked/reparse
  directory entries are rejected.
- **Silent log regressions:** tests can fail on a feature-specific regular
  expression or the CLI's standard exception policy. Lua `print()` and
  `Test.report.log()` are deliberately routed to the collected log stream.
- **Invalid Lua inputs:** waits, screenshot options, throttle, cameras, render
  scale, quality settings, assertion tolerances, and metrics reject invalid,
  non-finite, fractional, or dangerous values as appropriate. Report tables
  reject cycles, excessive depth, and excessive entries.
- **Bridge robustness:** requests are localhost-only, developer-gated, limited
  to 4 MiB, time-bounded, and abandoned commands are not later executed from
  the main-thread queue. The CLI checks protocol compatibility and refuses to
  launch a second KSP2 player when one is already running without a reachable
  harness.
- **Lost crash status:** after a run is accepted, CLI/game disconnects and
  unexpected process exits finalize the partial report as
  `infrastructure_failed` and return exit code `2`.
- **Missing target-mod context:** reports now include active mod IDs/versions,
  Lua can inspect the active set, and mods have a supported semantic extension
  registry.
- **Deployment portability:** game and Unity roots can be configured through
  parameters/environment variables. Installation detects a running player and
  a missing build before copying.

## Intentional limits

- The harness is developer-only and does not provide authentication beyond a
  loopback-only endpoint plus the explicit enable marker/environment variable.
- Tests are serialized within one player. Multiple player instances require
  distinct ports and result directories.
- Vessel/world mutations are not transactional. Tests should load a fixture
  before relying on universe state.
- The generic render adapter covers only proven KSP2 settings. Feature mods
  should register their own meaningful operations.
- The current deterministic camera controls the active Unity flight camera.
  Tests of additional render-camera stacks need a mod-owned extension.
- A crash before the game returns an artifact directory cannot have a canonical
  per-run report; a crash after acceptance may lack the final in-game log slice.
- Save fixtures remain local because they may contain user/game data. The public
  repository contains test scripts and fixture layout guidance only.
- Headless KSP2, UI automation, image-diff approval, parallel/multiplayer tests,
  cloud CI, and remote internet control remain outside the MVP.
