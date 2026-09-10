Test.name("Redux Better AA launchpad raw-motion statistics")

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
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
better_aa.set_buffer_view("validity")
Test.wait.frames(24)
Test.report.value("matrix", better_aa.matrix_snapshot())
Test.assert.true_(
    better_aa.request_capture(),
    "BetterAA should queue the validity capture and statistics readback")
Test.wait.frames(30)
Test.report.note(
    "Queued BetterAA's full validity screenshot, statistics grid, and 4x4 anchor samples at the settled yaw 0 launchpad pose")
