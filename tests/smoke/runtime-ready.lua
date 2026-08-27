Test.name("Runtime bridge and capture smoke test")

Test.assert.true_(Test.game.is_ready(), "KSP2 game services should be initialized")
Test.report.value("gameState", Test.game.state())
Test.wait.frames(5)

Test.capture.screenshot("runtime-ready", {
    scale = 1,
    hideUI = false,
    waitFrames = 2
})

Test.report.note("Verified MoonSharp coroutine execution, artifacts, and screenshot capture")
