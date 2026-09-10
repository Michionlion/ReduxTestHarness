Test.name("Redux Better AA launchpad motion render-event isolation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

Test.capture.screenshot(
    "motion-after-everything",
    { scale = 1, hideUI = false, waitFrames = 1 })

local capture = better_aa.capture_motion_at_event("BeforeImageEffects")
Test.report.value("earlyCapture", capture)
Test.wait.frames(12)
Test.capture.screenshot(
    "motion-before-image-effects-copy",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.assert.true_(
    better_aa.restore_motion_capture(),
    "the early motion capture should be restored")
Test.wait.frames(8)
Test.capture.screenshot(
    "motion-after-everything-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Copied BuiltinRenderTextureType.MotionVectors at BeforeImageEffects into an owned RGHalf texture and made the existing raw visualizer sample that immutable copy")
