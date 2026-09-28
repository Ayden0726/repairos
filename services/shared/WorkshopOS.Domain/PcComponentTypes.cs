namespace WorkshopOS.Domain;

/// <summary>Canonical PC part component types used by inventory and build slots.</summary>
public static class PcComponentTypes
{
    public const string Cpu = "CPU";
    public const string Motherboard = "Motherboard";
    public const string Ram = "RAM";
    public const string Gpu = "GPU";
    public const string Storage = "Storage";
    public const string Psu = "PSU";
    public const string Case = "Case";
    public const string Cooler = "Cooler";
    public const string Os = "OS";
    public const string Peripheral = "Peripheral";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Cpu, Motherboard, Ram, Gpu, Storage, Psu, Case, Cooler, Os, Peripheral, Other
    ];

    /// <summary>Slots that allow multiple lines on a single build.</summary>
    public static readonly HashSet<string> MultiLine =
        new(StringComparer.OrdinalIgnoreCase) { Ram, Storage, Peripheral, Other };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Other;
        var trimmed = value.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }
        return Other;
    }

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Any(k => string.Equals(k, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
