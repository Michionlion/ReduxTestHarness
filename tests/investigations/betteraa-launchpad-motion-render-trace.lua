Test.name("Redux Better AA launchpad physics-camera render trace")

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

Test.report.value(
    "tracedCamera",
    better_aa.start_camera_render_trace())
Test.wait.frames(12)
Test.report.value("renderTrace", better_aa.camera_render_trace())
Test.capture.screenshot(
    "traced-raw-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.stop_camera_render_trace(),
    "camera render trace should have been active")

Test.report.note(
    "Counted Unity camera callbacks and sampled current/public-previous VP at each callback while the reproducible raw launchpad field was visible")
