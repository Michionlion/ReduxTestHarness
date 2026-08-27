Test.name("Intentional forbidden log policy failure")
Test.report.fail_on_log("HARNESS_FORBIDDEN_LOG_SENTINEL")
Test.report.log("HARNESS_FORBIDDEN_LOG_SENTINEL")
Test.wait.frames(5)
