namespace Iot04.Api.Domain;

/// <summary>Hiện thực hóa các quy định nghiệp vụ BR01–BR15 của đồ án.</summary>
public static class Rules
{
    // BR03
    public const int IntervalMin = 30, IntervalMax = 3600, IntervalDefault = 150;

    // UC03 bước 4: dải vật lý hợp lệ
    public const decimal TempPhysicalMin = -50, TempPhysicalMax = 80;
    public const decimal HumidityPhysicalMin = 0, HumidityPhysicalMax = 100;

    // BR13
    public const int LowBatteryPercent = 20;
    public const decimal MildBreachRatio = 0.10m;      // vượt < 10% độ rộng dải -> WARNING
    public const double EmergencyZoneRatio = 0.5;     // > 50% cảm biến trong zone vi phạm -> EMERGENCY

    // BR14
    public const decimal InspectionMaxDeviation = 0.5m;

    public static readonly string[] SeverityOrder = ["INFO", "WARNING", "CRITICAL", "EMERGENCY"];
    public static int Rank(string severity) => Array.IndexOf(SeverityOrder, severity);

    /// <summary>BR12 + BR13: trả về null nếu trong ngưỡng, ngược lại WARNING/CRITICAL theo mức vượt.</summary>
    public static string? BoundsSeverity(decimal value, decimal min, decimal max)
    {
        if (value >= min && value <= max) return null;
        var exceed = value < min ? min - value : value - max;
        return exceed < (max - min) * MildBreachRatio ? "WARNING" : "CRITICAL";
    }

    /// <summary>BR11: phân loại tuổi dữ liệu.</summary>
    public static string DataAge(TimeSpan age) => age.TotalMinutes switch
    {
        < 3 => "FRESH",
        < 10 => "AGING",
        < 30 => "STALE",
        _ => "EXPIRED"
    };

    /// <summary>
    /// BR10: phân loại mức trôi. Nhiệt độ theo đồ án (0.1 / 0.3 °C/h).
    /// Độ ẩm: đồ án chưa quy định — đề xuất 0.5 / 1.5 %RH/h.
    /// </summary>
    public static string DriftSeverity(string metric, double absRatePerHour)
    {
        var (moderate, critical) = metric == "HUMIDITY" ? (0.5, 1.5) : (0.1, 0.3);
        return absRatePerHour >= critical ? "CRITICAL" : absRatePerHour >= moderate ? "MODERATE" : "NONE";
    }
}
