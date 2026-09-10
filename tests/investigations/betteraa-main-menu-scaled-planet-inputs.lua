Test.name("Redux Better AA main-menu scaled-planet input ownership")

Test.assert.equal(
    Test.game.state(),
    "MainMenu",
    "the harness should start this fixture-free test at the main menu")
Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.render.wait_stable(45)

better_aa.select_camera("Skybox")
better_aa.set_buffer_view("raw")
Test.report.value(
    "skyboxMotionCapture",
    better_aa.capture_motion_at_event("BeforeImageEffects"))
Test.wait.frames(8)
better_aa.select_camera("Camera.Scaled")
Test.wait.frames(4)
Test.capture.screenshot(
    "main-menu-skybox-motion-before-image-effects",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.restore_motion_capture(),
    "the Skybox motion capture should be restored")

better_aa.select_camera("Skybox")
better_aa.set_buffer_view("LinearDepth")
Test.report.value(
    "skyboxDepthCapture",
    better_aa.capture_depth_at_event("BeforeImageEffects"))
Test.wait.frames(8)
better_aa.select_camera("Camera.Scaled")
Test.wait.frames(4)
Test.capture.screenshot(
    "main-menu-skybox-depth-before-image-effects",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.restore_depth_capture(),
    "the Skybox depth capture should be restored")

better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("main-menu-dlaa-frame-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.assert.true_(
    better_aa.request_capture(),
    "the Better AA capability capture should be queued")
Test.wait.frames(8)
Test.report.note(
    "Captured immutable Skybox motion/depth before Camera.Scaled clears depth, then an eight-frame DLAA sequence for rectangular scaled-planet flicker analysis")
