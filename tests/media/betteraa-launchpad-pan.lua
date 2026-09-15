Test.name("Better AA launchpad comparison")
local aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(aa, nil, "Better AA adapter is available")
local modes = {
    {"off", "Off", "Off"},
    {"taa", "CustomTaa", "Custom TAA"},
    {"dlaa-k", "NvidiaDlaa", "NVIDIA DLAA"},
    {"fsr31", "AmdFsr2", "FSR 3.1 Native AA"}
}
Test.game.wait_for_state("MainMenu", 60)
for i, mode in ipairs(modes) do
    if not capture_preview or i == 1 then
        aa.set_backend("Off")
        Test.game.load_save("launchpad")
        Test.game.wait_for_state("Flight", 90)
        Test.game.pause()
        Test.camera.target_vessel()
        Test.camera.orbit { distance = capture_distance, yaw = capture_start_yaw,
            pitch = 35, fov = 60 }
        aa.select_camera("FlightCameraPhysics_Main")
        aa.set_dlaa_preset("K")
        aa.set_backend(mode[2])
        Test.wait.frames(120)
        local before = aa.release_status()
        Test.assert.equal(before.selectedBackend, mode[3], "Requested AA is active")
        if mode[1] == "dlaa-k" then
            Test.assert.equal(before.dlaaPreset, "K", "DLAA uses preset K")
        end
        Test.report.value(mode[1] .. "-before", before)
        Test.game.unpause()
        local path = Test.capture.pan(mode[1], {
            fps = 60, frames = capture_preview and 3 or 480, warmup = 360,
            width = 2560, height = 1440, startYaw = capture_start_yaw,
            endYaw = capture_start_yaw - 80, pitch = 35,
            distance = capture_distance, fov = 60
        })
        Test.report.value(mode[1] .. "-frames", path)
        local after = aa.release_status()
        Test.assert.equal(after.selectedBackend, mode[3], "AA stayed active through capture")
        if mode[1] == "fsr31" then
            Test.assert.near(after.fsrFrameTimeMs, 1000 / 60, 0.001,
                "FSR received the fixed capture timestep")
        end
        Test.report.value(mode[1] .. "-after", after)
        Test.game.pause()
        Test.assert.true_(aa.request_capture(), "Record production AA diagnostics")
        Test.wait.frames(8)
        Test.wait.until_(function() return not aa.release_status().captureBusy end, 60)
    end
end
aa.set_backend("Off")
Test.report.note("Full flight UI; 35-degree downward pitch; 80-degree right-to-left sweep. Offline frames at a fixed 1/60-second timestep are for image comparison, not performance measurement. Each mode reloads the same save; procedural animation may differ.")
