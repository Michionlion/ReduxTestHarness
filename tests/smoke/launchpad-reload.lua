Test.name("Repeated launchpad fixture reload smoke test")

local fixture = "local/launchpad-fly-safe-15"
local vesselName = "Fly Safe-15"

Test.game.load_save(fixture)
Test.game.wait_for_state("Flight", 45)
local first = Test.flight.start(vesselName)

Test.assert.equal(first.name, vesselName, "first load should select the fixture vessel")
Test.assert.equal(first.body, "Kerbin", "first load should initialize Kerbin")
Test.assert.equal(first.situation, "PreLaunch", "first load should begin at the launchpad")

Test.wait.frames(30)

Test.game.load_save(fixture)
Test.game.wait_for_state("Flight", 45)
local second = Test.flight.start(vesselName)

Test.assert.equal(second.name, vesselName, "second load should select the fixture vessel")
Test.assert.equal(second.body, "Kerbin", "second load should rebuild Kerbin")
Test.assert.equal(second.situation, "PreLaunch", "second load should return to the launchpad")
Test.report.value("reloadSituation", second.situation)
Test.report.note("Loaded the same fixture twice after KSP2 universe teardown")
