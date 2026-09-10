Test.name("Redux Better AA DLAA distant-cloud bias-mask capture")

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
Test.camera.direct_orbit { distance = 150000, yaw = 303, pitch = 28, fov = 55 }
Test.wait.frames(90)
Test.capture.screenshot("10-off", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(120)
Test.capture.screenshot("20-dlaa", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_buffer_view("VendorHistoryBiasMask")
Test.wait.frames(5)
Test.capture.screenshot("30-vendor-bias-mask", { scale = 1, hideUI = true, waitFrames = 1 })

better_aa.set_buffer_view("Off")
better_aa.set_backend("Off")
Test.report.value("cloudRenderer", better_aa.cloud_renderer_snapshot())
Test.report.note("Captured the 150 km source, DLAA result, and vendor history-bias mask with cloud coverage at full strength.")
