using System.Text.Json;

namespace DockerUpdater.Worker.Docker
{
    public sealed class RecoveryJournal
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _filePath;
        private readonly object _lock = new();
        private List<RecoveryEntry> _entries;
        private readonly ILogger<RecoveryJournal> _logger;

        public RecoveryJournal(string dataDir, ILogger<RecoveryJournal> logger)
        {
            _logger = logger;
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "recovery-journal.json");
            _entries = Load();

            if (_entries.Count > 0)
            {
                logger.LogInformation("Recovery journal loaded with {Count} pending entry/entries from {Path}", _entries.Count, _filePath);
            }
        }

        public IReadOnlyList<RecoveryEntry> GetPendingEntries()
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }

        public void BeginRecreation(RecoveryEntry entry)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e => string.Equals(e.ContainerName, entry.ContainerName, StringComparison.Ordinal));
                _entries.Add(entry);
                Flush();
            }
        }

        public void RecordCreated(string containerName, string newContainerId)
        {
            lock (_lock)
            {
                RecoveryEntry? entry = FindEntry(containerName);
                if (entry is not null)
                {
                    entry.NewContainerId = newContainerId;
                    entry.Phase = RecoveryPhase.Created;
                    Flush();
                }
            }
        }

        public void RecordStarted(string containerName)
        {
            lock (_lock)
            {
                RecoveryEntry? entry = FindEntry(containerName);
                if (entry is not null)
                {
                    entry.Phase = RecoveryPhase.Started;
                    Flush();
                }
            }
        }

        public void Complete(string containerName)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e => string.Equals(e.ContainerName, containerName, StringComparison.Ordinal));
                Flush();
            }
        }

        private RecoveryEntry? FindEntry(string containerName)
        {
            return _entries.Find(e => string.Equals(e.ContainerName, containerName, StringComparison.Ordinal));
        }

        private void Flush()
        {
            try
            {
                string json = JsonSerializer.Serialize(_entries, s_jsonOptions);
                string tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist recovery journal to {Path}", _filePath);
            }
        }

        private List<RecoveryEntry> Load()
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<RecoveryEntry>>(json, s_jsonOptions) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse recovery journal at {Path}. Starting fresh", _filePath);
                return [];
            }
        }
    }
}
