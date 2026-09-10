Test.name("Redux Better AA cloud private texture audit")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

local fields = {
    { "_finalSceneColor", "rgb" },
    { "_finalSceneColor", "alpha" },
    { "_preUpsample", "rgb" },
    { "_preUpsample", "alpha" },
    { "_upsampledColorBuffer", "rgb" },
    { "_upsampledColorBuffer", "alpha" },
    { "_newCloudRaysColorBuffer", "rgb" },
    { "_newCloudRaysColorBuffer", "alpha" },
    { "_historyUpsampledColorBuffer", "rgb" },
    { "_historyUpsampledColorBuffer", "alpha" },
    { "_upsampledDepthBuffer", "r" },
    { "_newCloudRaysDepthBuffer", "r" },
    { "_preDownsampledDepth", "r" }
}

local function attach_cloud_textures(prefix, distance)
    Test.camera.direct_orbit {
        distance = distance,
        yaw = 303,
        pitch = 28,
        fov = 55
    }
    Test.wait.frames(90)
    Test.capture.screenshot(
        prefix .. "-scene",
        { scale = 1, hideUI = true, waitFrames = 2 })
    for _, request in ipairs(fields) do
        local path = better_aa.capture_cloud_texture(request[1], request[2])
        Test.report.attach(path)
    end
    Test.report.value(prefix .. "CloudRenderer", better_aa.cloud_renderer_snapshot())
end

attach_cloud_textures("10-off-030km", 30000)
attach_cloud_textures("20-off-120km", 120000)

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
attach_cloud_textures("30-dlaa-120km", 120000)

better_aa.set_dlaa_execution_bypass(true)
Test.wait.frames(90)
attach_cloud_textures("40-dlaa-bypass-120km", 120000)

better_aa.set_dlaa_execution_bypass(false)
better_aa.set_backend("Off")
Test.report.note(
    "Captured cloud renderer color, alpha, and depth targets at 30 km and 120 km for direct inspection.")
