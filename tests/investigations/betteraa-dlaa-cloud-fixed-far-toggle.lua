Test.name("Redux Better AA DLAA fixed-far cloud toggle isolation")

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
Test.wait.frames(45)
better_aa.select_camera("FlightCameraPhysics_Main")

Test.camera.direct_orbit {
    distance = 120000,
    yaw = 303,
    pitch = 28,
    fov = 55
}
Test.wait.frames(180)

local function capture(prefix)
    Test.capture.screenshot(
        prefix .. "-scene",
        { scale = 1, hideUI = true, waitFrames = 2 })
    Test.report.attach(better_aa.capture_cloud_texture("_finalSceneColor", "rgb"))
    Test.report.attach(better_aa.capture_cloud_texture("_finalSceneColor", "alpha"))
    Test.report.attach(better_aa.capture_cloud_texture("_preUpsample", "rgb"))
    Test.report.value(prefix .. "CloudRenderer", better_aa.cloud_renderer_snapshot())
end

capture("10-off-initial")

better_aa.set_dlaa_preset("K")
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.15)
better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0, true)
for cycle = 1, 3 do
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(120)
    capture(string.format("%02d-k-cycle-%d", 10 + cycle * 10, cycle))
    better_aa.set_backend("Off")
    Test.wait.frames(120)
    capture(string.format("%02d-off-cycle-%d", 15 + cycle * 10, cycle))
end

better_aa.set_dlaa_preset("M")
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(120)
capture("80-m")

better_aa.set_backend("Off")
Test.report.note(
    "Held KSP's flight-camera controller at 120 km while alternating three fresh K contexts with Off, then M; captured both the presented scene and the cloud renderer's private final color/alpha/pre-upsample targets.")
