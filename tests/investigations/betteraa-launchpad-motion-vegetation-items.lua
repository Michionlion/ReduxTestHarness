Test.name("Redux Better AA launchpad vegetation-item isolation")

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

Test.capture.screenshot(
    "all-vegetation",
    { scale = 1, hideUI = false, waitFrames = 1 })

local items = {
    { id = "7c645097-dd1d-4eaf-9559-e2aa6c33a29d", name = "fern" },
    { id = "203b6b27-740f-4502-9514-2344efc35226", name = "daffodil" },
    { id = "9391dd83-70a7-454a-a84a-99d509374f28", name = "grass" },
    { id = "7821a620-ecf3-4106-909d-0d7ca7cb01a4", name = "base-poplar" },
    { id = "33871efa-2863-4a6a-a474-a2bc2a761490", name = "desert-bush" },
    { id = "7d95aa35-cdfb-4e4d-8b49-ed28b47ac460", name = "ksc-poplar" },
    { id = "e0739f57-f100-42e3-b357-7ae26abc5d67", name = "poplar-02" },
    { id = "667dfb44-9b52-4bf6-b8cb-6b1b4acf5d4a", name = "poplar-03" },
    { id = "e5814010-6562-460e-b0bc-bf627bd97199", name = "poplar-04" },
    { id = "e2bd4d81-0424-4207-8ed7-b3a1f588b88f", name = "poplar-05" },
    { id = "1bfd43fa-906c-402e-92a3-fde7489abfb8", name = "wetland-03" },
    { id = "10cb6981-7348-4ba9-bf5d-5bd9e4370d25", name = "wetland-04" },
    { id = "c0d2a8c2-9e69-4016-b25b-9a0dc3efa4bb", name = "wetland-05" }
}

for _, item in ipairs(items) do
    better_aa.isolate_vegetation_item(item.id)
    Test.wait.frames(6)
    Test.capture.screenshot(
        "only-" .. item.name,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.restore_render_producers()
better_aa.restore_motion_pass_probe()
Test.report.note(
    "Each capture permits only one Vegetation Studio item through RenderVegetationItemLODIndirect, preserving its ordinary materials and indirect buffers")
