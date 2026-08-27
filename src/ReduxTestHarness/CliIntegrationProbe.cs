using System;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ReduxTestHarness
{
    internal static class CliIntegrationProbe
    {
        private const string ServerTypeName =
            "Redux.CliIntegration.CliIntegrationServer";
        private const string ReplTypeName =
            "Redux.CliIntegration.CliIntegrationCSharpRepl";
        private const string ReportTypeName =
            "Redux.CliIntegration.CliIntegrationRunReport";

        public static JObject Snapshot()
        {
            Type server = FindType(ServerTypeName);
            Type repl = FindType(ReplTypeName);
            Type report = FindType(ReportTypeName);
            var result = new JObject
            {
                ["available"] = server != null,
                ["server"] = server != null,
                ["csharpRepl"] = repl != null,
                ["runReport"] = report != null
            };

            if (server != null)
            {
                FieldInfo port = server.GetField(
                    "DefaultPort",
                    BindingFlags.Public | BindingFlags.Static);
                if (port != null)
                {
                    result["defaultPort"] = Convert.ToInt32(port.GetRawConstantValue());
                }
                result["assembly"] = server.Assembly.GetName().Name;
            }
            return result;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    Type type = assemblies[index].GetType(fullName, false, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // A diagnostic probe must never prevent the harness from starting.
                }
            }
            return null;
        }
    }
}
