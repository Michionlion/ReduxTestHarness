Test.name("Redux Better AA DLAA terrain flicker billboard isolation")

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
Test.camera.absolute {
    position = { 1495.77783, -1563.25476, -477.4797 },
    rotation = { -0.5554914, -0.289271, 0.2178656, 0.7485227 },
    fov = 60
}

local function burst(prefix)
    Test.wait.frames(120)
    for index = 1, 8 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_vegetation_motion_repair(true)
better_aa.set_depth_disocclusion_mask(false)
better_aa.set_backend("NvidiaDlaa")
burst("10-normal-dlaa")

better_aa.suppress_render_producer("vegetation-billboards")
burst("20-without-billboards")

better_aa.restore_render_producers()
better_aa.set_dlaa_execution_bypass(true)
burst("30-ngx-bypass")

better_aa.set_dlaa_execution_bypass(false)
better_aa.set_depth_disocclusion_mask(true)
better_aa.set_backend("Off")
Test.report.note(
    "Compared the exact terrain-flicker report pose under normal DLAA, billboard producer suppression, and NGX execution bypass with the experimental bias mask disabled.")
