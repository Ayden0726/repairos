using System.Text;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Services;

/// <summary>
/// Maps an IMEI.info lookup result onto quote / used-device form fields.
/// Shared so quotes and refurbished buy-in stay consistent.
/// </summary>
public static class ImeiLookupFill
{
    public const string NotesBlockStart = "--- IMEI lookup ---";
    public const string NotesBlockEnd = "--- end IMEI lookup ---";

    public sealed record AppliedFields(
        string? Brand,
        string? Model,
        string? ModelNumber,
        string? Category,
        string? Colour,
        string? Storage,
        string? Imei,
        string? Specs,
        string? NotesBlock,
        string Summary,
        bool ColourIsHint,
        bool StorageIsHint);

    public static AppliedFields FromResult(ImeiLookupResultDto result, string? normalizedImei = null)
    {
        var brand = FirstNonEmpty(result.Brand, result.Manufacturer);
        var model = FirstNonEmpty(result.Model, result.ModelNumber);
        var modelNumber = NullIfEmpty(result.ModelNumber);
        // Avoid duplicating model number in the model string when they match.
        if (!string.IsNullOrWhiteSpace(model) &&
            !string.IsNullOrWhiteSpace(modelNumber) &&
            string.Equals(model.Trim(), modelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            modelNumber = null;
        }

        var category = MapCategory(result.DeviceCategory);
        var colour = NullIfEmpty(result.Colour);
        var storage = NullIfEmpty(result.Storage);
        var colourHint = colour is not null && !result.ColourIsDeviceSpecific;
        var storageHint = storage is not null && !result.StorageIsDeviceSpecific;

        var specs = BuildSpecsLine(result);
        var notes = BuildNotesBlock(result);

        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(brand) || !string.IsNullOrWhiteSpace(model))
            summaryParts.Add($"{brand} {model}".Trim());
        if (!string.IsNullOrWhiteSpace(modelNumber))
            summaryParts.Add($"({modelNumber})");
        if (!string.IsNullOrWhiteSpace(storage))
            summaryParts.Add(storage + (storageHint ? "?" : ""));
        if (!string.IsNullOrWhiteSpace(colour))
            summaryParts.Add(colour + (colourHint ? "?" : ""));
        summaryParts.Add(result.VerificationStatus);
        if (result.FromCache) summaryParts.Add("cached");
        if (result.Duplicates is { Count: > 0 })
            summaryParts.Add($"{result.Duplicates.Count} possible duplicate(s)");

        return new AppliedFields(
            brand,
            model,
            modelNumber,
            category,
            colour,
            storage,
            NullIfEmpty(normalizedImei),
            specs,
            notes,
            string.Join(" · ", summaryParts.Where(s => !string.IsNullOrWhiteSpace(s))),
            colourHint,
            storageHint);
    }

    public static string MergeNotesBlock(string? existingNotes, string? block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return existingNotes?.Trim() ?? string.Empty;

        var existing = existingNotes ?? string.Empty;
        var cleaned = StripNotesBlock(existing).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return block.Trim();
        return cleaned + Environment.NewLine + Environment.NewLine + block.Trim();
    }

    public static string StripNotesBlock(string notes)
    {
        var start = notes.IndexOf(NotesBlockStart, StringComparison.Ordinal);
        if (start < 0) return notes;
        var end = notes.IndexOf(NotesBlockEnd, start, StringComparison.Ordinal);
        if (end < 0) return notes[..start].TrimEnd();
        end += NotesBlockEnd.Length;
        var before = notes[..start].TrimEnd();
        var after = end < notes.Length ? notes[end..].TrimStart() : string.Empty;
        if (string.IsNullOrWhiteSpace(before)) return after;
        if (string.IsNullOrWhiteSpace(after)) return before;
        return before + Environment.NewLine + Environment.NewLine + after;
    }

    public static string MapCategory(string? raw) =>
        (raw ?? string.Empty).Trim() switch
        {
            "Phone" or "Smartphone" or "Mobile" or "Handset" => "Phone",
            "Tablet" or "iPad" => "Tablet",
            "Laptop" or "Notebook" or "MacBook" => "Laptop",
            "PC" or "Desktop" or "Computer" => "PC",
            "" => string.Empty,
            var other => other
        };

    private static string? BuildSpecsLine(ImeiLookupResultDto r)
    {
        var parts = new List<string>();
        Add(parts, "RAM", r.Ram);
        Add(parts, "Display", r.Display);
        Add(parts, "Chipset", FirstNonEmpty(r.Chipset, r.Processor));
        Add(parts, "Battery", r.BatteryCapacity);
        Add(parts, "Network", r.Network);
        if (!string.IsNullOrWhiteSpace(r.Warranty) && r.WarrantyIsDeviceSpecific)
            Add(parts, "Warranty", r.Warranty);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string BuildNotesBlock(ImeiLookupResultDto r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(NotesBlockStart);
        if (!string.IsNullOrWhiteSpace(r.Tac)) sb.AppendLine($"TAC: {r.Tac}");
        if (!string.IsNullOrWhiteSpace(r.Manufacturer) &&
            !string.Equals(r.Manufacturer, r.Brand, StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"Manufacturer: {r.Manufacturer}");
        if (!string.IsNullOrWhiteSpace(r.ModelNumber)) sb.AppendLine($"Model number: {r.ModelNumber}");
        if (!string.IsNullOrWhiteSpace(r.Display)) sb.AppendLine($"Display: {r.Display}");
        if (!string.IsNullOrWhiteSpace(r.Processor)) sb.AppendLine($"Processor: {r.Processor}");
        if (!string.IsNullOrWhiteSpace(r.Chipset)) sb.AppendLine($"Chipset: {r.Chipset}");
        if (!string.IsNullOrWhiteSpace(r.BatteryCapacity)) sb.AppendLine($"Battery: {r.BatteryCapacity}");
        if (!string.IsNullOrWhiteSpace(r.Network)) sb.AppendLine($"Network: {r.Network}");
        if (!string.IsNullOrWhiteSpace(r.Ram)) sb.AppendLine($"RAM: {r.Ram}");
        if (!string.IsNullOrWhiteSpace(r.Storage))
            sb.AppendLine($"Storage: {r.Storage}{(r.StorageIsDeviceSpecific ? "" : " (TAC hint — confirm)")}");
        if (!string.IsNullOrWhiteSpace(r.Colour))
            sb.AppendLine($"Colour: {r.Colour}{(r.ColourIsDeviceSpecific ? "" : " (TAC hint — confirm)")}");
        if (!string.IsNullOrWhiteSpace(r.Warranty))
            sb.AppendLine($"Warranty: {r.Warranty}{(r.WarrantyIsDeviceSpecific ? "" : " (unverified)")}");
        sb.AppendLine($"Provider: {r.Provider} · {r.VerificationStatus}" + (r.FromCache ? " · cached" : ""));
        if (r.Duplicates is { Count: > 0 })
        {
            sb.AppendLine("Possible duplicates in shop:");
            foreach (var d in r.Duplicates.Take(5))
                sb.AppendLine($"  - {d.Source}: {d.Label}" + (d.TicketNumber is null ? "" : $" ({d.TicketNumber})"));
        }
        sb.Append(NotesBlockEnd);
        return sb.ToString();
    }

    private static void Add(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{label} {value.Trim()}");
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
