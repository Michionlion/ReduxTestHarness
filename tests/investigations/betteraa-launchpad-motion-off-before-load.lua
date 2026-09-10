Test.name("Redux Better AA launchpad motion with backend off before flight load")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.wait.frames(12)
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
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

Test.report.value("matrices", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "raw-motion-off-before-camera-creation",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.set_buffer_view("validity")
Test.wait.frames(6)
Test.assert.true_(
    better_aa.request_capture(),
    "BetterAA should queue a raw motion statistics capture")
Test.wait.frames(30)

Test.report.note(
    "Selected BetterAA Off at the main menu before the flight scene and its cameras were created, then reproduced the same launchpad pose")
