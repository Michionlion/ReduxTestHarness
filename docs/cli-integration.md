# What happened to `CliIntegration`?

## Finding

The installed Redux player is `0.2.8.5.103184-beta`, commit `ffc94930`.
Its `Assembly-CSharp.dll` does **not** define any `Redux.CliIntegration` types.
However, three independent pieces of the same installation expect that feature:

1. The API documentation generated for the exact `0.2.8.5.103184` Beta build
   documents [`Redux.CliIntegration.CliIntegrationServer`](https://api.ksp2redux.org/beta/AssemblyCSharp/Redux.CliIntegration.CliIntegrationServer.html) in
   `Assembly-CSharp.dll`, describes it as a localhost-only JSON-RPC server, and
   gives its default port as `51693`.
2. `SpaceWarp2.UI.dll` contains `SpaceWarpConsoleCliIntegrationBridge`, which
   reflectively looks for `CliIntegrationActivityLog` and
   `CliIntegrationCSharpRepl` and reports that the runtime is not loaded when
   they cannot be found.
3. The shipped `Assembly-CSharp.dll` still contains the unused constant
   `KSP2Redux.CliIntegration.StartupSavePath`, but no code references it and no
   code calls `CliIntegrationServer.EnsureStarted`.

The API appeared in Beta documentation as early as Redux `0.2.3.0.102311` and
remains in later Beta/Develop documentation. This is not a simple rename.

## Most likely cause

The documentation is generated from an assembly before the final Unity player
build, while the installed assembly is the player output. The CLI types are
reached through reflection and the server has no direct startup call in the
player. That combination is consistent with Unity managed stripping removing
the implementation, or with a player-only build define excluding it. The
evidence cannot distinguish those two without access to the private Redux
source/build pipeline, but it does rule out the idea that an optional DLL is
merely missing from the install.

Run the local binary probe with:

```powershell
pwsh .\scripts\inspect-cli-integration.ps1
```

## Correct Redux-side restoration

The durable fix belongs in the Redux player build:

1. Call `CliIntegrationServer.EnsureStarted(GameInstance)` from a guaranteed
   developer/test startup path.
2. Preserve every reflectively reached `Redux.CliIntegration` type with a
   `link.xml` rule or `UnityEngine.Scripting.Preserve`.
3. Keep the listener loopback-only and gated behind a developer/test setting.
4. Add a post-build check that the player `Assembly-CSharp.dll` still contains
   `CliIntegrationServer`, `CliIntegrationActivityLog`, and
   `CliIntegrationCSharpRepl`.
5. Launch the built player and verify a real request on port `51693`; checking
   only the pre-player API assembly will reproduce the current false positive.

The original implementation cannot be recovered from documentation alone: the
public pages expose its type surface, not the JSON-RPC method set or method
bodies. Copying a reference assembly over the player DLL would also be unsafe.

## Prototype fallback

`ReduxTestHarness` provides only the small transport required for automated Lua
tests. It deliberately does not recreate the missing arbitrary C# REPL. The
runtime `ping` response reports whether native `CliIntegration` is available,
so a future build can detect the restoration and migrate transports without
changing the Lua semantic API or report format.
