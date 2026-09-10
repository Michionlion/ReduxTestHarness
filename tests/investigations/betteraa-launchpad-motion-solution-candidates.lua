Test.name("Redux Better AA launchpad source-solution candidates")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_vegetation_motion_repair(false)

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
better_aa.set_motion_pass_probe(0)
Test.wait.frames(6)
Test.capture.screenshot(
    "candidate-00-native-baseline",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.set_motion_pass_probe(17)
Test.wait.frames(6)
Test.capture.screenshot(
    "candidate-01-camera-pass-only",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.set_motion_pass_probe(18)
Test.wait.frames(6)
Test.capture.screenshot(
    "candidate-02-conditional-invalid-object-discard",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.restore_motion_pass_probe()
local patched = better_aa.reroute_vegetation_camera_motion()
Test.report.value("renderMeshIndirectPatchedMethods", patched)
Test.wait.frames(12)
Test.capture.screenshot(
    "candidate-03-render-mesh-indirect-camera-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.report.value(
    "renderMeshIndirectReroutedCalls",
    better_aa.render_producer_suppression_hits())

better_aa.set_buffer_view("FinalColor")
Test.wait.frames(6)
Test.capture.screenshot(
    "candidate-04-render-mesh-indirect-final-color",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.restore_render_producers()
better_aa.set_buffer_view("FinalColor")
Test.wait.frames(6)
Test.capture.screenshot(
    "candidate-05-legacy-final-color",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.set_vegetation_motion_repair(true)

Test.report.note(
    "Diagnostic-only comparison: native Built-in object motion, camera-pass-only control, exact invalid-history object discard, and a direct Graphics.RenderMeshIndirect reroute using RenderParams.motionVectorMode=Camera")
