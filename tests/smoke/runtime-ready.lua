Test.name("Runtime bridge and capture smoke test")

Test.assert.true_(Test.game.is_ready(), "KSP2 game services should be initialized")
Test.assert.true_(Test.mod.is_loaded("ReduxTestHarness"), "the harness mod should be active")

local harness = Test.mod.info("ReduxTestHarness")
Test.assert.equal(harness.version, "0.2.0", "the active harness metadata should match the build")

local extension = Test.mod.extension("ReduxTestHarness")
Test.assert.not_equal(extension, nil, "the harness should expose its registered extension")
Test.assert.equal(extension.protocol_version(), 1, "the extension should execute semantic callbacks")

Test.report.value("gameState", Test.game.state())
Test.report.value("activeModCount", #Test.mod.list())
Test.wait.frames(5)

Test.capture.screenshot("runtime-ready", {
    scale = 1,
    hideUI = false,
    waitFrames = 2
})

Test.report.attach("test.lua")
Test.report.note("Verified MoonSharp coroutine execution, artifacts, and screenshot capture")
