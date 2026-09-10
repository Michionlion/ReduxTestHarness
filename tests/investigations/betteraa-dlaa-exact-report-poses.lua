Test.name("Redux Better AA DLAA exact report poses")

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

-- phase1-20260828-155753-001 / 155803-002: preset-sensitive dot ray.
Test.camera.absolute {
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}
better_aa.set_backend("Off")
burst("10-dot-pose-off", 3)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.093069315)
better_aa.set_backend("NvidiaDlaa")
burst("20-dot-pose-dlaa", 6)
better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(true)
better_aa.set_backend("NvidiaDlaa")
burst("30-dot-pose-dlaa-zero-jitter", 4)
better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)

-- phase1-20260828-160801-002: KSC hills/terrain flicker report.
Test.camera.absolute {
    position = { 1495.77783, -1563.25476, -477.4797 },
    rotation = { -0.5554914, -0.289271, 0.2178656, 0.7485227 },
    fov = 60
}
better_aa.set_backend("Off")
burst("50-terrain-pose-off", 5)
better_aa.set_backend("NvidiaDlaa")
burst("60-terrain-pose-dlaa", 10)

better_aa.set_backend("Off")
Test.report.note(
    "Replayed the exact FlightCameraPhysics_Main positions and quaternions from the user reports; the zero-jitter condition starts with a fresh DLAA context and history.")
