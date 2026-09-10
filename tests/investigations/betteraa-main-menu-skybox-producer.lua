Test.name("Redux Better AA main-menu skybox producer")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_backend("Off")
Test.render.wait_stable(60)
better_aa.select_camera("Skybox")
Test.report.value("skyboxA", better_aa.skybox_snapshot())
Test.wait.frames(30)
Test.report.value("skyboxB", better_aa.skybox_snapshot())

better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("menu-dlaa-live-cubemap-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

local suppressed = better_aa.suppress_observer_cubemap()
Test.report.value("suppressedCubemapSystems", suppressed)
Test.wait.frames(60)
Test.report.value("skyboxSuppressed", better_aa.skybox_snapshot())
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("menu-dlaa-frozen-cubemap-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.restore_observer_cubemap()
Test.report.note(
    "Inventoried the main-menu Skybox material and compared DLAA while its cubemap producer was active versus disabled")
