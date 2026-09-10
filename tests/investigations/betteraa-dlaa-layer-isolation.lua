Test.name("Redux Better AA DLAA renderer layer isolation")

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

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
better_aa.override_camera_state("noCulling")

local function capture_layer(layer)
    better_aa.set_backend("Off")
    better_aa.set_camera_culling_mask(2 ^ layer)
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(90)
    for index = 1, 5 do
        Test.capture.screenshot(
            string.format("layer-%02d-%02d", layer, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

local active_layers = { 0, 1, 6, 15, 16, 17, 19 }
for _, layer in ipairs(active_layers) do
    capture_layer(layer)
end

better_aa.set_backend("Off")
better_aa.restore_camera_state()
Test.report.note(
    "Rendered each layer in FlightCameraPhysics_Main culling mask 0x000B8043 independently at the exact dotted-ray pose.")
