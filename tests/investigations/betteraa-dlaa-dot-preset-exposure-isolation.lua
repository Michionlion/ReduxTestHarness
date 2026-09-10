Test.name("Redux Better AA DLAA dot-ray preset and exposure isolation")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.camera.absolute {
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}
Test.render.wait_stable(90)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_temporal_jitter_suppressed(true)

local function burst(prefix)
    Test.wait.frames(90)
    for index = 1, 5 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 2 })
    end
end

local function condition(prefix, preset, sharpness, auto_exposure, pre_exposure, prefer_ppv2)
    better_aa.set_backend("Off")
    better_aa.set_dlaa_preset(preset)
    better_aa.set_vendor_sharpness("NvidiaDlaa", sharpness)
    better_aa.set_vendor_auto_exposure(
        "NvidiaDlaa", auto_exposure, pre_exposure, prefer_ppv2)
    better_aa.set_backend("NvidiaDlaa")
    burst(prefix)
end

better_aa.set_backend("Off")
burst("10-off")

-- Strict reconstruction comparison: same color, pose, zero jitter, no sharpening,
-- and manual pre-exposure 1. Only the DLSS preset changes.
condition("20-k-manual1-sharp0", "K", 0.0, false, 1.0, false)
condition("30-m-manual1-sharp0", "M", 0.0, false, 1.0, false)

-- Reproduce the shipped radiometry path while still keeping pose/jitter fixed.
condition("40-k-ppv2-sharp0", "K", 0.0, true, 1.0, true)
condition("50-m-ppv2-sharp0", "M", 0.0, true, 1.0, true)

-- Finally isolate the user-facing sharpness default from the preset itself.
condition("60-k-manual1-sharp015", "K", 0.15, false, 1.0, false)
condition("70-m-manual1-sharp015", "M", 0.15, false, 1.0, false)

better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)
Test.report.note(
    "Compared K and M in fresh DLAA contexts at the exact dot-ray pose while independently controlling pre-exposure and sharpening; temporal jitter was zero throughout.")
