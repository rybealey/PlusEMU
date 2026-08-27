using System.Data;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Plus.Core;
using Plus.Database.Interfaces;

namespace Plus.Database;

public sealed class Database : IDatabase
{
    private readonly string _connectionStr;

    public Database(IOptions<DatabaseConfiguration> configuration)
    {
        _connectionStr = new MySqlConnectionStringBuilder
        {
            ConnectionTimeout = 10,
            Database = configuration.Value.Name,
            DefaultCommandTimeout = 30,
            MaximumPoolSize = configuration.Value.MaximumPoolSize,
            MinimumPoolSize = configuration.Value.MinimumPoolSize,
            Password = configuration.Value.Password,
            Pooling = true,
            Port = configuration.Value.Port,
            Server = configuration.Value.Hostname,
            UserID = configuration.Value.Username,
            // AllowZeroDateTime must stay OFF: it makes the driver return
            // DATE/DATETIME columns as MySqlDateTime, which Dapper cannot map
            // onto DateTime properties (users.vip_last_stipend broke every
            // VIP login at SSO). ConvertZeroDateTime alone covers legacy
            // zero-dates by returning DateTime.MinValue.
            ConvertZeroDateTime = true,
            SslMode = MySqlSslMode.None
        }.ToString();
    }

    public bool IsConnected()
    {
        try
        {
            var con = new MySqlConnection(_connectionStr);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT 1+1";
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            con.Close();
        }
        catch (MySqlException)
        {
            return false;
        }
        return true;
    }

    [Obsolete("Use IDatabase.Connection instead")]
    public IQueryAdapter GetQueryReactor()
    {
        try
        {
            IDatabaseClient dbConnection = new DatabaseConnection(_connectionStr);
            dbConnection.Connect();
            return dbConnection.GetQueryReactor();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            return null;
        }
    }

    public IDbConnection Connection() => new MySqlConnection(_connectionStr);
}