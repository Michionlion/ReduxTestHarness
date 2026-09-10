Test.name("Redux Better AA launchpad physics-camera reinitialization")

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
    "before-camera-reinitialize",
    { scale = 1, hideUI = false, waitFrames = 1 })

local suppressed = better_aa.suppress_camera("FlightCameraPhysics_Main")
Test.report.value("physicsCameraSuppressed", suppressed)
Test.wait.frames(3)
Test.report.value(
    "physicsCameraRestored",
    better_aa.restore_suppressed_cameras())
Test.wait.frames(30)
Test.report.value("matricesAfterReinitialize", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "after-camera-reinitialize",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Disabled the original physics camera for three frames and re-enabled it at the unchanged corrupt pose to test whether Unity's native per-camera motion history is merely uninitialized")
