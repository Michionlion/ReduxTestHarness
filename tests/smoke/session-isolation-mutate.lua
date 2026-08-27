Test.name("Session isolation mutation smoke test")

local originalScale = Test.render.get("supersampling")
local originalClouds = Test.render.get("clouds")
local changedScale = originalScale < 1.5 and 2.0 or 1.0

Test.report.metric("originalSupersampling", originalScale)
Test.report.value("originalClouds", originalClouds)

Test.render.set("supersampling", changedScale)
Test.render.set("clouds", not originalClouds)

Test.assert.near(
    Test.render.get("supersampling"),
    changedScale,
    0.001,
    "supersampling should change within this test")
Test.assert.equal(
    Test.render.get("clouds"),
    not originalClouds,
    "cloud rendering should change within this test")

Test.report.note("The harness must restore both settings when this test finishes")
