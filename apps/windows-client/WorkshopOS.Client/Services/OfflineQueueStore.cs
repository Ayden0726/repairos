using System.Text.Json;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Services;

public sealed record OfflineQueuedMutation(
    Guid ClientMutationId,
    string Method,
    string Path,
    string? BodyJson,
    DateTimeOffset QueuedAt);

/// <summary>Local JSON queue of mutations captured while the API was unreachable.</summary>
public sealed class OfflineQueueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _path;

    public OfflineQueueStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkshopOS");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "offline-queue.json");
    }

    public int Count => Load().Count;

    public IReadOnlyList<OfflineQueuedMutation> Peek() => Load();

    public void Enqueue(string method, string path, string? bodyJson)
    {
        var list = Load();
        list.Add(new OfflineQueuedMutation(Guid.NewGuid(), method, path, bodyJson, DateTimeOffset.UtcNow));
        Save(list);
    }

    public void Replace(IReadOnlyList<OfflineQueuedMutation> remaining) => Save(remaining.ToList());

    public void Clear() => Save(new List<OfflineQueuedMutation>());

    public SyncPushRequest ToPushRequest()
    {
        var list = Load();
        return new SyncPushRequest(list.Select(m =>
            new SyncMutationDto(m.ClientMutationId, m.Method, m.Path, m.BodyJson)).ToList());
    }

    private List<OfflineQueuedMutation> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<OfflineQueuedMutation>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<OfflineQueuedMutation>>(json, JsonOptions)
                   ?? new List<OfflineQueuedMutation>();
        }
        catch
        {
            return new List<OfflineQueuedMutation>();
        }
    }

    private void Save(List<OfflineQueuedMutation> list)
    {
        var json = JsonSerializer.Serialize(list, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
