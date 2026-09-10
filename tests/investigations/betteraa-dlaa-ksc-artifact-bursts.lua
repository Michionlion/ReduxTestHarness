Test.name("Redux Better AA DLAA KSC artifact bursts")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
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

local function pose(distance, pitch)
    Test.camera.direct_orbit {
        distance = distance,
        yaw = 303,
        pitch = pitch,
        fov = 60
    }
    Test.wait.frames(90)
end

local function burst(prefix, count)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

-- Low-horizon view for the preset-sensitive dark-dot ray.
pose(200, 10)
better_aa.set_backend("Off")
Test.wait.frames(90)
burst("10-horizon-off", 4)

better_aa.set_vendor_sharpness("NvidiaDlaa", 0.10)
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(120)
burst("20-horizon-dlaa", 6)

better_aa.set_temporal_jitter_suppressed(true)
Test.wait.frames(120)
burst("30-horizon-dlaa-zero-jitter", 4)
better_aa.set_temporal_jitter_suppressed(false)

better_aa.set_dlaa_execution_bypass(true)
Test.wait.frames(120)
burst("40-horizon-dlaa-ngx-bypass", 4)
better_aa.set_dlaa_execution_bypass(false)

-- Mid-distance KSC/terrain view for stationary lighting and z-like flicker.
pose(500, 15)
better_aa.set_backend("Off")
Test.wait.frames(90)
burst("50-terrain-off", 6)

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(120)
burst("60-terrain-dlaa", 10)

better_aa.set_dlaa_execution_bypass(true)
Test.wait.frames(120)
burst("70-terrain-dlaa-ngx-bypass", 6)

better_aa.set_dlaa_execution_bypass(false)
better_aa.set_backend("Off")
Test.report.note(
    "Captured hidden-UI stationary bursts for Off, DLAA, zero-jitter DLAA, and DLAA state with NGX bypassed.")
