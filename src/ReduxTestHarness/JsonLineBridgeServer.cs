using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ReduxTestHarness
{
    internal sealed class JsonLineBridgeServer : IDisposable
    {
        private const int MaxRequestCharacters = 4 * 1024 * 1024;
        private readonly ConcurrentQueue<BridgeCommand> _commands =
            new ConcurrentQueue<BridgeCommand>();
        private readonly Action<string> _log;
        private TcpListener _listener;
        private CancellationTokenSource _cancellation;

        public JsonLineBridgeServer(Action<string> log)
        {
            _log = log;
        }

        public void Start(int port)
        {
            if (_listener != null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start(8);
            Task.Run(() => AcceptLoop(_cancellation.Token));
            _log("Listening on 127.0.0.1:" + port + ".");
        }

        public bool TryDequeue(out BridgeCommand command)
        {
            return _commands.TryDequeue(out command);
        }

        private async Task AcceptLoop(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    TcpClient accepted = client;
                    client = null;
                    _ = Task.Run(() => HandleClient(accepted, cancellation));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException error)
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        _log("Accept failed: " + error.Message);
                    }
                }
                finally
                {
                    if (client != null)
                    {
                        client.Dispose();
                    }
                }
            }
        }

        private void HandleClient(TcpClient client, CancellationToken cancellation)
        {
            client.ReceiveTimeout = 30000;
            client.SendTimeout = 30000;
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(
                stream, new UTF8Encoding(false), false, 4096, true))
            using (var writer = new StreamWriter(
                stream, new UTF8Encoding(false), 4096, true))
            {
                writer.AutoFlush = true;
                try
                {
                    string line = ReadLineLimited(reader, MaxRequestCharacters);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        writer.WriteLine(Error("empty_request", "Request must be one JSON object.").ToString(Newtonsoft.Json.Formatting.None));
                        return;
                    }

                    JObject payload;
                    try
                    {
                        payload = JObject.Parse(line);
                    }
                    catch (Exception parseError)
                    {
                        writer.WriteLine(Error("invalid_json", parseError.Message).ToString(Newtonsoft.Json.Formatting.None));
                        return;
                    }

                    string commandName = (string)payload["command"];
                    if (string.IsNullOrWhiteSpace(commandName))
                    {
                        writer.WriteLine(Error("missing_command", "The command field is required.").ToString(Newtonsoft.Json.Formatting.None));
                        return;
                    }

                    var completed = new ManualResetEventSlim(false);
                    JObject response = null;
                    int completionCount = 0;
                    int abandoned = 0;
                    _commands.Enqueue(new BridgeCommand
                    {
                        Command = commandName,
                        Payload = payload,
                        IsAbandoned = () => Volatile.Read(ref abandoned) != 0,
                        Complete = value =>
                        {
                            if (Volatile.Read(ref abandoned) != 0)
                            {
                                return;
                            }
                            if (Interlocked.Exchange(ref completionCount, 1) != 0)
                            {
                                return;
                            }
                            response = value;
                            completed.Set();
                        }
                    });

                    if (!completed.Wait(TimeSpan.FromSeconds(30), cancellation))
                    {
                        Interlocked.Exchange(ref abandoned, 1);
                        response = Error("command_timeout", "The game main thread did not process the command in time.");
                    }
                    writer.WriteLine((response ?? Error("no_response", "Command produced no response."))
                        .ToString(Newtonsoft.Json.Formatting.None));
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception error)
                {
                    try
                    {
                        writer.WriteLine(Error("bridge_error", error.Message).ToString(Newtonsoft.Json.Formatting.None));
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string ReadLineLimited(TextReader reader, int maximumCharacters)
        {
            var builder = new StringBuilder(Math.Min(4096, maximumCharacters));
            while (true)
            {
                int value = reader.Read();
                if (value < 0 || value == '\n')
                {
                    return builder.ToString().TrimEnd('\r');
                }
                if (builder.Length >= maximumCharacters)
                {
                    throw new InvalidDataException(
                        "Request exceeds the " + maximumCharacters + " character limit.");
                }
                builder.Append((char)value);
            }
        }

        public static JObject Success()
        {
            return new JObject { ["ok"] = true };
        }

        public static JObject Error(string code, string message)
        {
            return new JObject
            {
                ["ok"] = false,
                ["code"] = code,
                ["error"] = message
            };
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation = _cancellation;
            TcpListener listener = _listener;
            _cancellation = null;
            _listener = null;
            if (cancellation != null)
            {
                cancellation.Cancel();
            }
            if (listener != null)
            {
                listener.Stop();
            }
            if (cancellation != null)
            {
                cancellation.Dispose();
            }
        }
    }
}
