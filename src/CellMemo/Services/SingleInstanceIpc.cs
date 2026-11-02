using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CellMemo.Services
{
    public class IpcPayload
    {
        public string Command { get; set; } = "ACTIVATE";
        public string[] FilePaths { get; set; } = Array.Empty<string>();
    }

    public static class SingleInstanceIpc
    {
        public const string MutexName = @"Global\CellMemo_SingleInstance_Mutex_8F9A2B1C";
        public const string PipeName = "CellMemo_IPC_Pipe_8F9A2B1C";

        public static bool SendToExistingInstance(string[] args)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(500); // 500ms timeout

                var payload = new IpcPayload
                {
                    Command = "ACTIVATE",
                    FilePaths = args
                };

                string json = JsonSerializer.Serialize(payload);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static NamedPipeServerStream? _activeServer;
        private static readonly object _serverLock = new();

        public static void StopServer()
        {
            lock (_serverLock)
            {
                try
                {
                    _activeServer?.Dispose();
                    _activeServer = null;
                }
                catch { }
            }
        }

        public static void StartServer(Action<IpcPayload> onMessageReceived, CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    NamedPipeServerStream? server = null;
                    try
                    {
                        server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        lock (_serverLock)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                server.Dispose();
                                break;
                            }
                            _activeServer = server;
                        }

                        await server.WaitForConnectionAsync(cancellationToken);

                        using var reader = new StreamReader(server);
                        string? line = await reader.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            var payload = JsonSerializer.Deserialize<IpcPayload>(line);
                            if (payload != null)
                            {
                                onMessageReceived(payload);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // 接続エラー時は少し待機して再試行
                        try
                        {
                            await Task.Delay(200, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        lock (_serverLock)
                        {
                            server?.Dispose();
                            if (_activeServer == server)
                            {
                                _activeServer = null;
                            }
                        }
                    }
                }
            }, cancellationToken);
        }
    }
}
