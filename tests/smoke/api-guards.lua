Test.name("Lua API validation guards")

local finiteWaitAccepted = pcall(function()
    Test.wait.seconds(0 / 0)
end)
Test.assert.false_(finiteWaitAccepted, "NaN wait durations must be rejected")

local hugeCaptureAccepted = pcall(function()
    Test.capture.screenshot("unsafe-scale", { scale = 100 })
end)
Test.assert.false_(hugeCaptureAccepted, "unsafe screenshot scales must be rejected")

local cyclic = {}
cyclic.self = cyclic
local cyclicReportAccepted = pcall(function()
    Test.report.value("cyclic", cyclic)
end)
Test.assert.false_(cyclicReportAccepted, "cyclic report tables must be rejected")

local negativeToleranceAccepted = pcall(function()
    Test.assert.near(1, 1, -1)
end)
Test.assert.false_(negativeToleranceAccepted, "negative assertion tolerances must be rejected")

local invalidThrottleAccepted = pcall(function()
    Test.flight.set_throttle(1.1)
end)
Test.assert.false_(invalidThrottleAccepted, "throttle outside 0 through 1 must be rejected")

local invalidCameraAccepted = pcall(function()
    Test.camera.orbit { distance = -1, yaw = 0, pitch = 0 }
end)
Test.assert.false_(invalidCameraAccepted, "non-positive camera distance must be rejected")

Test.report.note("Validated bounded numeric, control, camera, screenshot, and report inputs")
