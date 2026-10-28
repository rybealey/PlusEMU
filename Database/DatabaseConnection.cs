using System.Data;
using MySqlConnector;
using Plus.Database.Adapter;
using Plus.Database.Interfaces;

namespace Plus.Database;

public class DatabaseConnection : IDatabaseClient
{
    private static readonly NLog.ILogger Log = NLog.LogManager.GetLogger("Plus.Database.DatabaseConnection");

    private readonly IQueryAdapter _adapter;
    private readonly MySqlConnection _con;

    public DatabaseConnection(string connectionString)
    {
        _con = new(connectionString);
        _adapter = new NormalQueryReactor(this);
    }

    public void Connect()
    {
        if (_con.State == ConnectionState.Closed)
        {
            try
            {
                _con.Open();
            }
            catch (Exception e)
            {
                // pixelrp (Twist, 2026-10-28): one line in the log, not the
                // whole stack trace on the console - when the pool runs dry
                // every waiting thread lands here at once, and each one held
                // the console while it printed.
                Log.Error("Could not open a database connection: {message}", e.Message);
            }
        }
    }

    public void Disconnect()
    {
        if (_con.State == ConnectionState.Open) _con.Close();
    }

    public IQueryAdapter GetQueryReactor() => _adapter;

    public MySqlCommand CreateNewCommand() => _con.CreateCommand();

    public void Dispose()
    {
        if (_con.State == ConnectionState.Open) _con.Close();
        _con.Dispose();
        GC.SuppressFinalize(this);
    }
}