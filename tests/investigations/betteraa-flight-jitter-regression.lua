Test.name("Redux Better AA 0.5.25 flight jitter regression")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(60)

better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_backend("AmdFsr2")
Test.render.wait_stable(60)
Test.capture.screenshot(
    "flight-fsr2-jitter-regression",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.request_capture()
Test.wait.frames(4)

Test.report.note(
    "Captured installed 0.5.25 FSR2 in Flight so schema 20 can verify that the full projection and dispatch jitter sequence remains active")
