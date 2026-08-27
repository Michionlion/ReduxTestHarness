Test.name("Session isolation restoration probe")

Test.report.metric("supersampling", Test.render.get("supersampling"))
Test.report.value("clouds", Test.render.get("clouds"))
Test.assert.true_(Test.game.is_ready(), "the retained game should remain usable")
