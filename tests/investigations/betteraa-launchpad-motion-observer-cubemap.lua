Test.name("Redux Better AA launchpad observer-cubemap isolation")

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

Test.capture.screenshot(
    "observer-cubemap-active",
    { scale = 1, hideUI = false, waitFrames = 1 })

local suppressed = better_aa.suppress_observer_cubemap()
Test.assert.true_(
    suppressed > 0,
    "an active CubemapReflectionSystem should be found")
Test.report.value("suppressedCubemapSystems", suppressed)
Test.wait.frames(24)
Test.capture.screenshot(
    "observer-cubemap-suppressed",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.assert.equal(
    better_aa.restore_observer_cubemap(),
    suppressed,
    "every suppressed cubemap system should be restored")
Test.wait.frames(24)
Test.capture.screenshot(
    "observer-cubemap-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Temporarily paused only KSP's observer/celestial reflection cubemap updater after reproducing the raw field, then restored it; no AA backend or motion sanitizer was active")
