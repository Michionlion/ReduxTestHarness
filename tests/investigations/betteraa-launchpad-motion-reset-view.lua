Test.name("Redux Better AA launchpad reset view-matrix state")

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

Test.report.value("beforeReset", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "before-reset-view",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "override",
    better_aa.override_camera_state("resetWorldToCamera"))
Test.wait.frames(24)
Test.report.value("afterReset", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "after-reset-view",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Reset only Unity's world-to-camera-matrix override state after reproducing the raw launchpad motion field; no sanitizer or AA backend was active")
