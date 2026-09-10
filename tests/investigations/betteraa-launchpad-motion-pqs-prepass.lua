Test.name("Redux Better AA launchpad PQS-prepass isolation")

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

Test.report.value("pqsRenderers", better_aa.pqs_snapshot())
Test.capture.screenshot(
    "pqs-prepass-active",
    { scale = 1, hideUI = false, waitFrames = 1 })

local suppressed = better_aa.suppress_pqs_prepass()
Test.assert.true_(
    suppressed > 0,
    "at least one active PQS renderer should be found")
Test.report.value("suppressedPqsRenderers", suppressed)
Test.wait.frames(24)
Test.capture.screenshot(
    "pqs-prepass-suppressed",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.assert.equal(
    better_aa.restore_pqs_prepass(),
    suppressed,
    "every suppressed PQS renderer should be restored")
Test.wait.frames(24)
Test.capture.screenshot(
    "pqs-prepass-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Temporarily disabled only active PQSRenderer behaviours after reproducing the raw field, then restored them; no AA backend or motion sanitizer was active")
