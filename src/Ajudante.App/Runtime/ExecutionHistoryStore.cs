using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajudante.App.Runtime;

public interface IExecutionHistoryStore
{
    Task<FlowExecutionHistoryEntry[]> ListAsync(int limit = 50, string? flowId = null, CancellationToken cancellationToken = default);

    Task UpsertAsync(FlowExecutionHistoryEntry entry, CancellationToken cancellationToken = default);
}

public sealed class ExecutionHistoryStore : IExecutionHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly int _retentionLimit;
    private readonly List<FlowExecutionHistoryEntry> _entries;

    public ExecutionHistoryStore(string filePath, int retentionLimit = 200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
        _retentionLimit = Math.Max(1, retentionLimit);
        _entries = LoadEntries(filePath);
    }

    public Task<FlowExecutionHistoryEntry[]> ListAsync(int limit = 50, string? flowId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FlowExecutionHistoryEntry[] result;
        lock (_sync)
        {
            IEnumerable<FlowExecutionHistoryEntry> query = _entries;
            if (!string.IsNullOrWhiteSpace(flowId))
            {
                query = query.Where(entry => string.Equals(entry.FlowId, flowId, StringComparison.OrdinalIgnoreCase));
            }

            result = query
                .OrderByDescending(entry => entry.StartedAt)
                .Take(Math.Max(1, limit))
                .Select(CloneEntry)
                .ToArray();
        }

        return Task.FromResult(result);
    }

    public async Task UpsertAsync(FlowExecutionHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        string payload;
        lock (_sync)
        {
            var existingIndex = _entries.FindIndex(candidate =>
                string.Equals(candidate.RunId, entry.RunId, StringComparison.OrdinalIgnoreCase));

            var clone = CloneEntry(entry);
            if (existingIndex >= 0)
            {
                _entries[existingIndex] = clone;
            }
            else
            {
                _entries.Add(clone);
            }

            TrimEntries_NoLock();
            payload = JsonSerializer.Serialize(_entries, JsonOptions);
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_filePath, payload, cancellationToken);
    }

    private static List<FlowExecutionHistoryEntry> LoadEntries(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var payload = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return [];
            }

            var entries = JsonSerializer.Deserialize<List<FlowExecutionHistoryEntry>>(payload, JsonOptions);
            return entries ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void TrimEntries_NoLock()
    {
        if (_entries.Count <= _retentionLimit)
        {
            return;
        }

        var retained = _entries
            .OrderByDescending(entry => entry.StartedAt)
            .Take(_retentionLimit)
            .Select(CloneEntry)
            .ToList();

        _entries.Clear();
        _entries.AddRange(retained);
    }

    private static FlowExecutionHistoryEntry CloneEntry(FlowExecutionHistoryEntry entry)
    {
        return new FlowExecutionHistoryEntry
        {
            RunId = entry.RunId,
            FlowId = entry.FlowId,
            FlowName = entry.FlowName,
            Source = entry.Source,
            TriggerNodeId = entry.TriggerNodeId,
            StartedAt = entry.StartedAt,
            FinishedAt = entry.FinishedAt,
            Result = entry.Result,
            Error = entry.Error,
            Logs = entry.Logs
                .Select(log => new ExecutionHistoryLogEntry
                {
                    Timestamp = log.Timestamp,
                    Level = log.Level,
                    NodeId = log.NodeId,
                    Message = log.Message
                })
                .ToArray(),
            NodeStatuses = entry.NodeStatuses
                .Select(nodeStatus => new ExecutionNodeStatusEntry
                {
                    Timestamp = nodeStatus.Timestamp,
                    NodeId = nodeStatus.NodeId,
                    Status = nodeStatus.Status
                })
                .ToArray()
        };
    }
}
