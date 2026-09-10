Test.name("Redux Better AA DLAA shadow isolation")

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

local function burst(prefix, count)
    Test.wait.frames(90)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)

Test.camera.absolute {
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}
better_aa.set_backend("NvidiaDlaa")
burst("10-dot-pose-normal-shadows", 6)

better_aa.set_backend("Off")
better_aa.set_quality_shadows(false)
better_aa.set_backend("NvidiaDlaa")
burst("20-dot-pose-shadows-disabled", 6)

Test.camera.absolute {
    position = { 1495.77783, -1563.25476, -477.4797 },
    rotation = { -0.5554914, -0.289271, 0.2178656, 0.7485227 },
    fov = 60
}
burst("30-terrain-pose-shadows-disabled", 8)

better_aa.set_backend("Off")
better_aa.set_quality_shadows(true)
better_aa.set_backend("NvidiaDlaa")
burst("40-terrain-pose-normal-shadows", 8)

better_aa.set_backend("Off")
Test.report.note(
    "Compared the exact dotted-ray and terrain-flicker poses with Unity realtime shadows enabled and disabled globally.")
