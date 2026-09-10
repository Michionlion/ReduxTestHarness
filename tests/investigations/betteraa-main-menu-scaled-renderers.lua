Test.name("Redux Better AA main-menu scaled renderer inventory")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_backend("Off")
Test.render.wait_stable(60)

better_aa.select_camera("Skybox")
Test.report.value(
    "skyboxRenderersA",
    better_aa.scaled_planet_snapshot())
Test.wait.frames(30)
Test.report.value(
    "skyboxRenderersB",
    better_aa.scaled_planet_snapshot())

better_aa.select_camera("Camera.Scaled")
Test.report.value(
    "scaledCameraRenderers",
    better_aa.scaled_planet_snapshot())
Test.report.note(
    "Inventoried active renderers visible to the main-menu Skybox and Camera.Scaled cameras, including shaders, motion passes, transforms, and Unity motion modes")
