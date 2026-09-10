Test.name("Redux Better AA launchpad motion camera-source capture")

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
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

better_aa.select_camera("FlightCameraScaled_Main")
better_aa.set_buffer_view("raw")
Test.wait.frames(6)
Test.report.value(
    "scaledCapture",
    better_aa.capture_motion_at_event("BeforeImageEffects"))
Test.wait.frames(8)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.wait.frames(4)
Test.capture.screenshot(
    "scaled-camera-motion-shown-by-physics-camera",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.restore_motion_capture(),
    "the scaled-camera capture should be restored")

Test.wait.frames(4)
Test.capture.screenshot(
    "physics-camera-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Copied each camera's BuiltinRenderTextureType.MotionVectors at BeforeImageEffects and presented the scaled-camera copy through the later physics camera")
