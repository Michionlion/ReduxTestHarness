Test.name("Redux Better AA DLAA cloud distance reset sweep")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

local distances = { 30000, 60000, 90000, 120000, 150000 }

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
for _, distance in ipairs(distances) do
    Test.camera.direct_orbit {
        distance = distance,
        yaw = 303,
        pitch = 28,
        fov = 55
    }
    Test.wait.frames(30)
    better_aa.request_history_reset()
    Test.wait.frames(30)
    Test.capture.screenshot(
        string.format("10-dlaa-reset-%06dkm", distance / 1000),
        { scale = 1, hideUI = true, waitFrames = 2 })
    Test.report.value(
        string.format("cloudRenderer%06dkm", distance / 1000),
        better_aa.cloud_renderer_snapshot())
end

better_aa.set_backend("Off")
Test.report.note(
    "Forced one explicit DLAA history reset after every 30-150 km camera-distance transition to isolate stale vendor history from cloud producer changes.")
