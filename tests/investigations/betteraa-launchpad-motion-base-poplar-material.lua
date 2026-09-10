Test.name("Redux Better AA launchpad base-poplar material isolation")

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
better_aa.set_buffer_view("raw")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
better_aa.set_motion_pass_probe(0)
Test.wait.frames(24)

local item_id = "7821a620-ecf3-4106-909d-0d7ca7cb01a4"
better_aa.isolate_vegetation_item(item_id)
Test.wait.frames(6)
Test.capture.screenshot(
    "base-poplar-all-materials",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.isolate_vegetation_item_first_material(item_id)
Test.wait.frames(6)
Test.capture.screenshot(
    "base-poplar-first-material-only",
    { scale = 1, hideUI = false, waitFrames = 1 })

better_aa.restore_render_producers()
better_aa.restore_motion_pass_probe()
Test.report.note(
    "The first Base_tree_poplar_large_01_kerbin material is M_Kerbin_Grassland_Branch_01 using NatureManufacture Shaders/Trees/Tree_Leaves_Specular; the second is M_Bark_1 using KSP2's opaque indirect-scatter shader")
