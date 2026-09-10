Test.name("Redux Better AA DLAA exact vegetation ray isolation")

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
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}

local function burst(prefix, count)
    Test.wait.frames(90)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_backend("Off")
burst("10-off", 3)

better_aa.set_vegetation_motion_repair(true)
better_aa.set_backend("NvidiaDlaa")
burst("20-dlaa-repair-on", 5)

better_aa.set_backend("Off")
better_aa.set_vegetation_motion_repair(false)
better_aa.set_backend("NvidiaDlaa")
burst("30-dlaa-repair-off", 5)

better_aa.set_backend("Off")
better_aa.set_vegetation_motion_repair(true)
better_aa.suppress_render_producer("vegetation-lod")
better_aa.set_backend("NvidiaDlaa")
burst("40-dlaa-without-vegetation-lod", 5)

better_aa.set_backend("Off")
better_aa.restore_render_producers()
better_aa.suppress_render_producer("vegetation-billboards")
better_aa.set_backend("NvidiaDlaa")
burst("50-dlaa-without-vegetation-billboards", 5)

better_aa.set_backend("Off")
better_aa.restore_render_producers()
better_aa.suppress_render_producer("vegetation-lod")
better_aa.suppress_render_producer("vegetation-billboards")
better_aa.set_backend("NvidiaDlaa")
burst("60-dlaa-without-all-vegetation", 5)

better_aa.set_backend("Off")
better_aa.restore_render_producers()
better_aa.set_vegetation_motion_repair(true)
Test.report.note(
    "Compared the exact dotted-ray pose with AA off, foliage repair on/off, and vegetation LOD/billboard producers suppressed.")
