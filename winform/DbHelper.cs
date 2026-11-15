using Microsoft.Data.SqlClient;

namespace Iot04.WinForms;

public static class DbHelper
{
    public const string ConnectionString = 
        "Server=.\\SQLEXPRESS;Database=IOT04_WarehouseMonitor;Integrated Security=True;TrustServerCertificate=True;";

    public static async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }
}
