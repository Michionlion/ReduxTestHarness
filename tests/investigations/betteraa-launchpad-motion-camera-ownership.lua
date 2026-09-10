Test.name("Redux Better AA launchpad motion camera ownership")

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

for _, camera in ipairs({
    "FlightCameraScaled_Main",
    "FlightCameraPhysics_Main"
}) do
    better_aa.select_camera(camera)
    better_aa.set_buffer_view("raw")
    Test.wait.frames(8)
    Test.report.value(camera .. "Matrices", better_aa.matrix_snapshot())
    Test.capture.screenshot(
        camera .. "-raw",
        { scale = 1, hideUI = false, waitFrames = 1 })

    better_aa.set_buffer_view("LinearDepth")
    Test.wait.frames(4)
    Test.capture.screenshot(
        camera .. "-depth",
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.report.note(
    "Compared the scaled-space and physics-space cameras at the same corrupt north-facing launchpad pose")
