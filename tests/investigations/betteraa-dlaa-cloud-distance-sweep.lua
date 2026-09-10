Test.name("Redux Better AA DLAA cloud distance sweep")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

local distances = { 30000, 60000, 90000, 120000, 150000 }

local function capture_sweep(prefix)
    for _, distance in ipairs(distances) do
        Test.camera.direct_orbit {
            distance = distance,
            yaw = 303,
            pitch = 28,
            fov = 55
        }
        Test.wait.frames(60)
        Test.capture.screenshot(
            string.format("%s-%06dkm", prefix, distance / 1000),
            { scale = 1, hideUI = true, waitFrames = 2 })
        Test.report.value(
            string.format("%sCloudRenderer%06dkm", prefix, distance / 1000),
            better_aa.cloud_renderer_snapshot())
    end
end

capture_sweep("10-off")

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
capture_sweep("20-dlaa")

better_aa.set_dlaa_execution_bypass(true)
Test.wait.frames(90)
capture_sweep("30-dlaa-state-ngx-bypassed")

better_aa.set_dlaa_execution_bypass(false)
better_aa.set_backend("Off")
Test.report.note(
    "Compared stock Off, normal DLAA, and DLAA camera state with only NGX evaluation bypassed over 30-150 km camera distance.")
