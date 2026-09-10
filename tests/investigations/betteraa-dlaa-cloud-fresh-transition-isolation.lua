Test.name("Redux Better AA DLAA fresh cloud-target transition isolation")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

local distances = { 30000, 60000, 90000, 120000, 150000 }

local function load_fresh()
    better_aa.set_backend("Off")
    Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
    Test.game.wait_for_state("Flight", 45)
    Test.flight.start("Fly Safe-15")
    Test.camera.mode("Flight")
    Test.camera.target_vessel()
    Test.wait.frames(45)
    better_aa.select_camera("FlightCameraPhysics_Main")
end

local function sweep(prefix)
    for _, distance in ipairs(distances) do
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

better_aa.set_temporal_jitter_suppressed(false)
load_fresh()
sweep("10-off")

load_fresh()
better_aa.set_dlaa_preset("K")
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_backend("NvidiaDlaa")
sweep("20-k")

load_fresh()
better_aa.set_temporal_jitter_suppressed(true)
better_aa.set_dlaa_preset("K")
better_aa.set_backend("NvidiaDlaa")
sweep("30-k-zero-jitter")

load_fresh()
better_aa.set_temporal_jitter_suppressed(false)
better_aa.set_dlaa_preset("M")
better_aa.set_backend("NvidiaDlaa")
sweep("40-m")

better_aa.set_backend("Off")
Test.report.note(
    "Reloaded the flight scene before every condition so the cloud renderer's 0.25-to-0.5 target transition occurs independently under Off, K, zero-jitter K, and M.")
