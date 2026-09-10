Test.name("Redux Better AA production motion-input options")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local defaults = better_aa.motion_input_snapshot()
Test.assert.true_(
    defaults.vegetationRepairEnabled,
    "Vegetation source repair should be enabled by default")
Test.assert.true_(
    defaults.vegetationRepairAvailable,
    "Vegetation source repair should match Redux 2.8.5")
Test.assert.false_(
    defaults.sanitizerEnabled,
    "Motion rejection and camera fallback should be disabled by default")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

better_aa.set_buffer_view("raw")
Test.capture.screenshot(
    "production-00-b-on-e-off-raw",
    { scale = 1, hideUI = false, waitFrames = 2 })

local repaired = better_aa.motion_input_snapshot()
Test.assert.greater(
    repaired.vegetationReroutedCalls,
    0,
    "Production repair should reroute vegetation draws")

better_aa.set_vegetation_motion_repair(false)
Test.wait.frames(12)
Test.capture.screenshot(
    "production-01-b-off-e-off-raw-control",
    { scale = 1, hideUI = false, waitFrames = 2 })

better_aa.set_vegetation_motion_repair(true)
better_aa.set_backend("CustomTaa")
Test.wait.frames(24)
better_aa.set_buffer_view("SanitizedVendorMotion")
Test.capture.screenshot(
    "production-02-b-on-e-off-pass-through",
    { scale = 1, hideUI = false, waitFrames = 2 })

better_aa.set_motion_sanitizer(true)
Test.wait.frames(24)
Test.capture.screenshot(
    "production-03-b-on-e-on-sanitized",
    { scale = 1, hideUI = false, waitFrames = 2 })

better_aa.set_motion_sanitizer(false)
better_aa.set_vegetation_motion_repair(true)
local restored = better_aa.motion_input_snapshot()
Test.assert.true_(restored.vegetationRepairEnabled)
Test.assert.false_(restored.sanitizerEnabled)
Test.report.value(
    "vegetationReroutedCalls",
    restored.vegetationReroutedCalls)
Test.report.note(
    "Production defaults restored: vegetation source repair ON, sanitizer/camera fallback OFF.")
