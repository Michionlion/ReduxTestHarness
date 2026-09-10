Test.name("Redux Better AA launchpad vegetation render inventory")

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

better_aa.suppress_render_producer("vegetation-lod")
Test.wait.frames(12)
Test.report.value(
    "vegetationDraws",
    better_aa.vegetation_render_snapshot())
Test.report.value(
    "suppressedCalls",
    better_aa.render_producer_suppression_hits())
better_aa.restore_render_producers()

Test.report.note(
    "Inventory records each indirect vegetation item's material shader, render type, render queue, pass names, and pass-enabled state at the deterministic launchpad pose")
