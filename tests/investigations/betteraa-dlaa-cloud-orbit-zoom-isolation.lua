Test.name("Redux Better AA DLAA cloud orbit-zoom isolation")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)
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
        -- direct_orbit updates KSP's flight-camera distance/controller state,
        -- unlike assigning the final camera transform directly.
        Test.camera.direct_orbit {
            distance = distance,
            yaw = 303,
            pitch = 28,
            fov = 55
        }
        Test.wait.frames(120)
        Test.capture.screenshot(
            string.format("%s-%06dkm", prefix, distance / 1000),
            { scale = 1, hideUI = true, waitFrames = 2 })
        Test.report.value(
            string.format("%sCloudRenderer%06dkm", prefix, distance / 1000),
            better_aa.cloud_renderer_snapshot())
    end
end

sweep("10-off")

better_aa.set_dlaa_preset("K")
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_backend("NvidiaDlaa")
sweep("20-k")

better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(true)
better_aa.set_backend("NvidiaDlaa")
sweep("30-k-zero-jitter")

better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)
better_aa.set_dlaa_preset("M")
better_aa.set_backend("NvidiaDlaa")
sweep("40-m")

better_aa.set_backend("Off")
Test.report.note(
    "Compared Off, K, fresh zero-jitter K, and M through KSP's real 30-150 km flight-camera orbit-distance controller path; sharpening and exposure were fixed at zero and manual 1.")
