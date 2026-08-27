using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace ReduxTestHarness
{
    /// <summary>
    /// Optional extension point for Redux mods that want to expose stable,
    /// mod-owned semantic test operations without adding reflection APIs to the
    /// harness. Register during mod initialization and dispose the returned
    /// handle during mod teardown.
    /// </summary>
    public static class TestApiRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Action<Script, Table>> Builders =
            new Dictionary<string, Action<Script, Table>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a fresh extension-table builder under the SpaceWarp mod ID.
        /// </summary>
        public static IDisposable Register(
            string modId,
            Action<Script, Table> configureExtension)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("A non-empty SpaceWarp mod ID is required.", "modId");
            }
            if (configureExtension == null)
            {
                throw new ArgumentNullException("configureExtension");
            }

            string key = modId.Trim();
            lock (Sync)
            {
                if (Builders.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "A Redux test API extension is already registered for '" + key + "'.");
                }
                Builders.Add(key, configureExtension);
            }
            return new Registration(key, configureExtension);
        }

        /// <summary>
        /// Creates a Lua callback that converts synchronous CLR failures into
        /// catchable Lua runtime errors. Extension APIs should use this helper
        /// for semantic operations that can reject input or unavailable state.
        /// </summary>
        public static DynValue Callback(
            string name,
            Func<ScriptExecutionContext, CallbackArguments, DynValue> callback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A non-empty callback name is required.", "name");
            }
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }
            return DynValue.NewCallback(
                (context, arguments) =>
                {
                    try
                    {
                        return callback(context, arguments);
                    }
                    catch (ScriptRuntimeException)
                    {
                        throw;
                    }
                    catch (Exception error)
                    {
                        throw new ScriptRuntimeException(error.Message);
                    }
                },
                name);
        }

        internal static void Populate(
            Script script,
            Table extensions,
            Action<string> warning)
        {
            KeyValuePair<string, Action<Script, Table>>[] snapshot;
            lock (Sync)
            {
                snapshot = new KeyValuePair<string, Action<Script, Table>>[Builders.Count];
                int index = 0;
                foreach (KeyValuePair<string, Action<Script, Table>> pair in Builders)
                {
                    snapshot[index++] = pair;
                }
            }
            Array.Sort(snapshot, (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key));

            for (int index = 0; index < snapshot.Length; index++)
            {
                KeyValuePair<string, Action<Script, Table>> pair = snapshot[index];
                var table = new Table(script);
                try
                {
                    pair.Value(script, table);
                    extensions.Set(pair.Key, DynValue.NewTable(table));
                }
                catch (Exception error)
                {
                    if (warning != null)
                    {
                        warning(
                            "Test API extension '" + pair.Key + "' failed to initialize: " +
                            error.Message);
                    }
                }
            }
        }

        private static void Unregister(
            string modId,
            Action<Script, Table> configureExtension)
        {
            lock (Sync)
            {
                Action<Script, Table> current;
                if (Builders.TryGetValue(modId, out current) &&
                    ReferenceEquals(current, configureExtension))
                {
                    Builders.Remove(modId);
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private string _modId;
            private Action<Script, Table> _configureExtension;

            public Registration(string modId, Action<Script, Table> configureExtension)
            {
                _modId = modId;
                _configureExtension = configureExtension;
            }

            public void Dispose()
            {
                string modId = _modId;
                Action<Script, Table> configureExtension = _configureExtension;
                _modId = null;
                _configureExtension = null;
                if (modId != null && configureExtension != null)
                {
                    Unregister(modId, configureExtension);
                }
            }
        }
    }
}
