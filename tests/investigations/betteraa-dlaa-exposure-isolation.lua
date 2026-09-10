Test.name("Redux Better AA DLAA exposure isolation")

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
Test.camera.absolute {
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}
Test.render.wait_stable(90)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.093069315)

local function burst(prefix)
    Test.wait.frames(75)
    for index = 1, 6 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 2 })
    end
end

better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0, true)
better_aa.set_backend("NvidiaDlaa")
burst("10-ppv2-exposure")

better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0, false)
burst("20-ngx-auto-exposure")

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
burst("30-manual-pre-exposure-1")

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 2.0, false)
burst("40-manual-pre-exposure-2")

better_aa.set_backend("Off")
Test.report.note(
    "Compared the exact dotted-ray pose under PPv2 pre-exposure, NGX auto exposure, and manual pre-exposure 1 and 2.")
