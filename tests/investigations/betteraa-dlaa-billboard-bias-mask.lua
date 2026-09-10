Test.name("Redux Better AA DLAA billboard bias-mask capture")

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

better_aa.set_vegetation_motion_repair(true)
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(120)
Test.capture.screenshot("10-dlaa-final", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_buffer_view("VegetationBillboardCoverage")
Test.wait.frames(5)
Test.capture.screenshot("20-billboard-coverage", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_buffer_view("VendorHistoryBiasMask")
Test.wait.frames(5)
Test.capture.screenshot("30-vendor-bias-mask", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_buffer_view("Off")
better_aa.set_backend("Off")
Test.report.note("Captured the exact dotted-ray pose, its producer-specific billboard coverage, and the final vendor history-bias mask.")
