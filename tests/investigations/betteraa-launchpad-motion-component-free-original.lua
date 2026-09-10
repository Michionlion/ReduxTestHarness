Test.name("Redux Better AA launchpad component-free original camera")

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
    "original-components-active",
    { scale = 1, hideUI = false, waitFrames = 1 })

local suppressed = better_aa.suppress_camera_component("*")
Test.report.value("cameraBehavioursSuppressed", suppressed)
Test.wait.frames(18)
Test.report.value("componentFreeMatrices", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "all-non-camera-behaviours-disabled",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "cameraBehavioursRestored",
    better_aa.restore_camera_components())
Test.wait.frames(18)
Test.capture.screenshot(
    "original-components-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Disabled every non-Camera Behaviour attached to FlightCameraPhysics_Main at once, preserving only the Camera and TestHarness/BetterAA command buffers, to test component interaction versus camera-instance state")
