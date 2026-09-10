Test.name("Redux Better AA RenderMeshIndirect vegetation coverage")

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
better_aa.set_buffer_view("FinalColor")

local yaws = { 0, 90, 180, 270 }
for index, yaw in ipairs(yaws) do
    Test.camera.orbit {
        distance = 250,
        yaw = yaw,
        pitch = 8,
        fov = 55
    }
    Test.wait.frames(18)

    better_aa.restore_render_producers()
    Test.wait.frames(8)
    Test.capture.screenshot(
        string.format("coverage-%02d-yaw-%03d-legacy", index, yaw),
        { scale = 1, hideUI = false, waitFrames = 1 })

    local patched = better_aa.reroute_vegetation_camera_motion()
    Test.assert.greater(
        patched,
        0,
        "Expected the vegetation indirect-render method to be patched")
    Test.wait.frames(8)
    Test.capture.screenshot(
        string.format("coverage-%02d-yaw-%03d-render-mesh-indirect", index, yaw),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.report.value(
    "renderMeshIndirectReroutedCalls",
    better_aa.render_producer_suppression_hits())
better_aa.restore_render_producers()
better_aa.set_vegetation_motion_repair(true)

Test.report.note(
    "Diagnostic-only coverage comparison for the RenderMeshIndirect camera-motion candidate. Captures four low-horizon views with legacy and rerouted vegetation draws.")
