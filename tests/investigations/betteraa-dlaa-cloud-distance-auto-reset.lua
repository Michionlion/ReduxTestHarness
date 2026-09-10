Test.name("Redux Better AA DLAA automatic cloud target reset")

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
local function sweep(prefix)
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

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
sweep("10-dlaa")
better_aa.set_backend("Off")
sweep("20-off")
Test.report.note(
    "Swept DLAA first so KSP's private cloud target crosses its initial 30-60 km resolution transition while vendor history is active, then captured AA-off references.")
