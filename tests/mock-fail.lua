-- MOCK_FAIL: the PowerShell mock bridge recognizes this marker.
Test.name("Intentional mock failure")
Test.assert.true_(false, "This file is transport-test input and never runs in KSP2")
