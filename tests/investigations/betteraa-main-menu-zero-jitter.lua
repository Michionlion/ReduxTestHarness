Test.name("Redux Better AA main-menu zero-jitter isolation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function capture_sequence(prefix, count)
    for frame = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, frame),
            { scale = 1, hideUI = false, waitFrames = 1 })
    end
end

local function restart_dlaa()
    better_aa.set_backend("Off")
    Test.wait.frames(8)
    better_aa.set_backend("NvidiaDlaa")
    Test.render.wait_stable(60)
end

Test.game.wait_for_state("MainMenu", 45)
better_aa.select_camera("Camera.Scaled")
restart_dlaa()
capture_sequence("main-menu-dlaa-normal-jitter", 32)

Test.assert.true_(
    better_aa.set_temporal_jitter_suppressed(true),
    "test-only temporal jitter suppression should enable")
restart_dlaa()
capture_sequence("main-menu-dlaa-zero-jitter", 32)

better_aa.set_backend("Off")
Test.assert.false_(
    better_aa.set_temporal_jitter_suppressed(false),
    "test-only temporal jitter suppression should disable")
Test.report.note(
    "Compared the deterministic main-menu camera stack under normal and zero projection/dispatch jitter")
