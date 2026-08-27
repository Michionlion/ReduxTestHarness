# Testing other mods

The harness owns common game operations. A feature mod should own semantic
operations that are specific to that mod, rather than exposing arbitrary KSP2
objects or asking a test to find internal components by reflection.

## Require and inspect a mod from Lua

```lua
Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")

local mod = Test.mod.info("ReduxBetterAA")
Test.report.value("betterAaVersion", mod.version)

for _, active in ipairs(Test.mod.list()) do
    Test.report.log(active.id .. " " .. (active.version or "unknown"))
end
```

Every report automatically includes the same active-mod inventory under
`environment.mods`, which makes a failure reproducible without depending on
the live player later.

## Register a semantic API from C#

A mod that references `ReduxTestHarness.dll` and `MoonSharp.Interpreter.dll`
can register an extension when it initializes:

```csharp
private IDisposable _testApi;

public override void OnInitialized()
{
    _testApi = ReduxTestHarness.TestApiRegistry.Register(
        "MyRenderingMod",
        (script, api) =>
        {
            api.Set("set_haze", ReduxTestHarness.TestApiRegistry.Callback(
                "MyRenderingMod.set_haze",
                (context, arguments) =>
                {
                    bool enabled = arguments[0].CastToBool();
                    HazeController.Instance.Enabled = enabled;
                    return DynValue.Nil;
                }));

            api.Set("is_ready", ReduxTestHarness.TestApiRegistry.Callback(
                "MyRenderingMod.is_ready",
                (context, arguments) =>
                    DynValue.NewBoolean(HazeController.Instance.IsReady)));
        });
}

private void OnDestroy()
{
    if (_testApi != null)
    {
        _testApi.Dispose();
        _testApi = null;
    }
}
```

The Lua test can then remain semantic:

```lua
local haze = Test.mod.extension("MyRenderingMod")
Test.assert.not_equal(haze, nil, "MyRenderingMod test API is required")

haze.set_haze(true)
Test.wait["until"](haze.is_ready, 30)
Test.render.wait_stable(30)
Test.capture.screenshot("haze-enabled")
```

Extension builders execute on KSP2's main thread once per test and receive a
fresh table. They should expose small snapshots, settings, and commands. Avoid
returning live Unity/KSP2 objects. `TestApiRegistry.Callback` makes synchronous
CLR failures catchable by Lua `pcall`; an uncaught failure still fails the test.
For an optional harness dependency, discover
`TestApiRegistry.Register` by reflection and simply skip registration when the
harness assembly is absent.

Use `Test.report.fail_on_log(pattern, message)` for a feature-specific error
signature, or run the CLI with `--FailOnLogErrors` for the harness's general
Unity/KSP exception policy. Patterns are .NET regular expressions with a
one-second match timeout and a 20-match report cap.
