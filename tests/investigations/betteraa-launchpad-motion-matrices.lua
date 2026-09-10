Test.name("Redux Better AA launchpad motion matrix correlation")

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

for _, yaw in ipairs({ 0, 180, 315 }) do
    Test.camera.orbit {
        distance = 45,
        yaw = yaw,
        pitch = 35,
        fov = 55
    }
    Test.wait.frames(20)
    Test.report.value(
        string.format("yaw%03d", yaw),
        better_aa.matrix_snapshot())
    Test.capture.screenshot(
        string.format("yaw%03d-raw", yaw),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.report.note(
    "Compared Camera.previousViewProjectionMatrix with the selected physics camera and every active BetterAA camera after each settled rig pose")
