Test.name("Redux Better AA launchpad scaled-camera history isolation")

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
    "scaled-camera-active",
    { scale = 1, hideUI = false, waitFrames = 1 })

local suppressed = better_aa.suppress_camera("FlightCameraScaled_Main")
Test.report.value("scaledCameraSuppressed", suppressed)
Test.wait.frames(24)
Test.report.value("physicsMatrices", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "scaled-camera-disabled",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "scaledCameraRestored",
    better_aa.restore_suppressed_cameras())
Test.wait.frames(12)
Test.capture.screenshot(
    "scaled-camera-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Disabled the lower-depth scaled-space camera while leaving the selected physics camera and its scene intact to test cross-camera native history contamination")
