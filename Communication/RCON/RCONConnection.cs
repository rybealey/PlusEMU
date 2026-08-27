using System.Net.Sockets;
using System.Text;
using NLog;

namespace Plus.Communication.RCON;

public class RconConnection
{
    private static readonly ILogger Log = LogManager.GetLogger("Plus.Communication.Rcon.RconConnection");
    private byte[] _buffer = new byte[1024];
    private Socket _socket;

    public RconConnection(Socket socket)
    {
        _socket = socket;
        try
        {
            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, OnCallBack, _socket);
        }
        catch
        {
            Dispose();
        }
    }

    public void OnCallBack(IAsyncResult iAr)
    {
        try
        {
            if (!int.TryParse(_socket.EndReceive(iAr).ToString(), out var bytes))
            {
                Dispose();
                return;
            }
            var data = Encoding.Default.GetString(_buffer, 0, bytes);
            var trimmed = data.Trim();
            if (trimmed.Length > 0 && trimmed[0] == '{')
            {
                // CMS JSON dialect: {"key": "...", "data": {...}}. One request per connection —
                // write the response and flush it before the socket closes in Dispose().
                var success = PlusEnvironment.RconSocket.GetCommands().ParseJson(trimmed, out var response);
                if (!success) Log.Error($"Failed to execute a JSON RCON command. Raw data: {data}");
                SendResponse(response);
            }
            else
            {
                // Legacy dialect: command\x01p1:p2. No response is written.
                if (!PlusEnvironment.RconSocket.GetCommands().Parse(data)) Log.Error($"Failed to execute a MUS command. Raw data: {data}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        Dispose();
    }

    private void SendResponse(string response)
    {
        if (string.IsNullOrEmpty(response) || _socket == null)
            return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(response);
            var sent = 0;
            while (sent < bytes.Length)
                sent += _socket.Send(bytes, sent, bytes.Length - sent, SocketFlags.None);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write RCON JSON response: {e}");
        }
    }

    public void Dispose()
    {
        try
        {
            if (_socket != null)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                _socket.Dispose();
            }
        }
        catch (Exception e)
        {
            // A peer reset (or an already-dropped socket) after we've
            // written the JSON response can make Shutdown/Close throw here.
            // This runs from an async I/O completion callback on the
            // threadpool, where an unhandled exception is fatal to the
            // process - never let it escape uncaught.
            Log.Error($"Error disposing RCON connection socket: {e}");
        }
        finally
        {
            _socket = null;
            _buffer = null;
        }
    }
}