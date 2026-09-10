Test.name("Redux Better AA launchpad raw-motion pose sweep")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")
Test.assert.true_(better_aa.available(), "ReduxBetterAA assembly should be loaded")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)

local vessel = Test.flight.start("Fly Safe-15")
Test.assert.equal(vessel.situation, "PreLaunch", "fixture should begin on the launchpad")

Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.wait.frames(15)

local yaws = { 0, 45, 90, 135, 180, 225, 270, 315 }
for index, yaw in ipairs(yaws) do
    Test.camera.orbit {
        distance = 45,
        yaw = yaw,
        pitch = 35,
        fov = 55
    }
    Test.wait.frames(12)
    Test.capture.screenshot(
        string.format("raw-pitch35-yaw%03d-a", yaw),
        { scale = 1, hideUI = false, waitFrames = 1 })
    Test.wait.frames(4)
    Test.capture.screenshot(
        string.format("raw-pitch35-yaw%03d-b", yaw),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.report.value("bufferView", better_aa.current_view())
Test.report.value("bufferCamera", better_aa.selected_camera())
Test.report.value("vessel", vessel.name)
Test.report.note(
    "Captured two raw motion-vector frames at each 45-degree yaw with a fixed 35-degree downward launchpad view")
