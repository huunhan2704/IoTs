using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;

namespace Iot04.Api.Infrastructure;

/// <summary>Tạo kết nối SQL Server (Windows Authentication, shared memory khi chạy local).</summary>
public sealed class Db(IConfiguration cfg)
{
    private readonly string _cs = cfg.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

    public async Task<SqlConnection> OpenAsync()
    {
        var c = new SqlConnection(_cs);
        await c.OpenAsync();
        return c;
    }
}

public static class DbExtensions
{
    /// <summary>Chuyển kết quả Dapper dynamic thành Dictionary để serialize JSON ổn định.</summary>
    public static List<Dictionary<string, object?>> ToDicts(this IEnumerable<dynamic> rows) =>
        rows.Select(r => ((IDictionary<string, object?>)r).ToDictionary(k => k.Key, k => k.Value)).ToList();

    public static Dictionary<string, object?>? ToDict(this object? row) =>
        row is IDictionary<string, object?> d ? d.ToDictionary(k => k.Key, k => k.Value) : null;

    public static async Task Audit(this SqlConnection c, Guid? userId, string action, string entityType, object? entityId, object? details = null, SqlTransaction? tx = null) =>
        await c.ExecuteAsync(
            "INSERT INTO audit_logs(user_id, action, entity_type, entity_id, details) VALUES (@userId, @action, @entityType, @entityId, @details)",
            new { userId, action, entityType, entityId = entityId?.ToString(), details = details is null ? null : JsonSerializer.Serialize(details) }, tx);
}

/// <summary>Mọi DATETIME2 trong CSDL là UTC -> luôn xuất ISO-8601 có hậu tố 'Z'.</summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Local ? DateTimeKind.Local : DateTimeKind.Utc)
            .ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
}

// ----------------------------------------------------------------------------- Auth
public sealed record AuthUser(Guid Id, string Username, string FullName, string Role);

public static class Passwords
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('$');
        if (p.Length != 3 || p[0] != "PBKDF2") return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[1]), Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(p[2]));
    }
}

/// <summary>Token ký HMAC-SHA256 (stateless, không phụ thuộc thư viện JWT).</summary>
public sealed class TokenService(IConfiguration cfg)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(cfg["Auth:Secret"] ?? "change-me");
    private sealed record Payload(Guid Id, string U, string N, string R, long Exp);

    public string Issue(AuthUser u)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload(u.Id, u.Username, u.FullName, u.Role,
            DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds()));
        var body = WebEncoders.Base64UrlEncode(json);
        return body + "." + WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(body)));
    }

    public AuthUser? Validate(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return null;
            var expected = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(expected, WebEncoders.Base64UrlDecode(parts[1]))) return null;
            var p = JsonSerializer.Deserialize<Payload>(WebEncoders.Base64UrlDecode(parts[0]));
            if (p is null || p.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return new AuthUser(p.Id, p.U, p.N, p.R);
        }
        catch { return null; }
    }
}

public static class AuthExtensions
{
    public static AuthUser? CurrentUser(this HttpContext ctx) => ctx.Items["user"] as AuthUser;

    /// <summary>Giới hạn vai trò được phép gọi endpoint. ADMIN luôn được phép.</summary>
    public static RouteHandlerBuilder Roles(this RouteHandlerBuilder b, params string[] roles) =>
        b.AddEndpointFilter(async (c, next) =>
        {
            var u = c.HttpContext.CurrentUser();
            if (u is null) return Results.Json(new { error = "Chưa đăng nhập" }, statusCode: 401);
            if (u.Role != "ADMIN" && !roles.Contains(u.Role))
                return Results.Json(new { error = "Bạn không có quyền thực hiện thao tác này" }, statusCode: 403);
            return await next(c);
        });
}

// ----------------------------------------------------------------------------- Realtime
/// <summary>Hub SignalR (WebSocket) đẩy dữ liệu real-time tới dashboard.</summary>
public sealed class MonitorHub : Hub { }

public sealed class Notifier(IHubContext<MonitorHub> hub)
{
    public Task Reading(object payload) => hub.Clients.All.SendAsync("reading", payload);
    public Task SensorState(object payload) => hub.Clients.All.SendAsync("sensorState", payload);
    public Task Alert(object payload) => hub.Clients.All.SendAsync("alert", payload);
    public Task Drift(object payload) => hub.Clients.All.SendAsync("drift", payload);
}

/// <summary>Bộ đếm metric ingestion (tương đương OpenTelemetry counter readings.accepted / readings.rejected).</summary>
public sealed class IngestionMetrics
{
    private long _accepted, _duplicates;
    public ConcurrentDictionary<string, long> Rejected { get; } = new();
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public void Accept() => Interlocked.Increment(ref _accepted);
    public void Duplicate() => Interlocked.Increment(ref _duplicates);
    public void Reject(string reason) => Rejected.AddOrUpdate(reason, 1, (_, v) => v + 1);

    public object Snapshot() => new
    {
        startedAt = StartedAt,
        accepted = Interlocked.Read(ref _accepted),
        duplicates = Interlocked.Read(ref _duplicates),
        rejected = Rejected.ToDictionary(k => k.Key, k => k.Value),
        rejectedTotal = Rejected.Values.Sum()
    };
}
