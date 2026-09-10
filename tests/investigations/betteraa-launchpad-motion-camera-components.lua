Test.name("Redux Better AA launchpad motion camera-component isolation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

Test.report.value("baselineMatrices", better_aa.matrix_snapshot())
Test.capture.screenshot(
    "baseline",
    { scale = 1, hideUI = false, waitFrames = 1 })

local components = {
    { type = "KSP.Rendering.CameraEffectsSystem", label = "camera-effects" },
    {
        type = "ThreeEyedGames.DecaliciousRenderer",
        label = "decalicious"
    },
    { type = "KSP.VolumeCloud.VolumeCloudRenderer", label = "clouds" },
    { type = "UnderWaterEffect", label = "underwater" },
    { type = "PSCopyDepthTexture", label = "copy-depth" },
    { type = "SetOceanData", label = "ocean-data" },
    { type = "KSP.OutlineEffect", label = "outline" },
    {
        type = "CreateTerrainBlendBuffers",
        label = "terrain-blend-buffers"
    }
}

for _, component in ipairs(components) do
    local count = better_aa.suppress_camera_component(component.type)
    Test.report.value(component.label .. "Suppressed", count)
    Test.wait.frames(12)
    Test.capture.screenshot(
        component.label .. "-disabled",
        { scale = 1, hideUI = false, waitFrames = 1 })
    Test.report.value(
        component.label .. "Restored",
        better_aa.restore_camera_components())
    Test.wait.frames(12)
end

Test.report.note(
    "Disabled each non-interaction rendering component on FlightCameraPhysics_Main independently to identify any camera extension that regenerates or corrupts the built-in motion target")
