using DockerUpdater.Worker.Docker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DockerUpdater.Worker.Tests
{
    public sealed class RecoveryJournalTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly RecoveryJournal _journal;

        public RecoveryJournalTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "docker-updater-tests-" + Guid.NewGuid().ToString("N")[..8]);
            _journal = CreateJournal(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void GetPendingEntries_ReturnsEmptyForFreshJournal()
        {
            Assert.Empty(_journal.GetPendingEntries());
        }

        [Fact]
        public void BeginRecreation_AddsEntry()
        {
            _journal.BeginRecreation(MakeEntry("my-app"));

            IReadOnlyList<RecoveryEntry> entries = _journal.GetPendingEntries();
            Assert.Single(entries);
            Assert.Equal("my-app", entries[0].ContainerName);
            Assert.Equal(RecoveryPhase.Renamed, entries[0].Phase);
        }

        [Fact]
        public void BeginRecreation_ReplacesExistingEntryForSameName()
        {
            _journal.BeginRecreation(MakeEntry("my-app", imageName: "my-app:v1"));
            _journal.BeginRecreation(MakeEntry("my-app", imageName: "my-app:v2"));

            IReadOnlyList<RecoveryEntry> entries = _journal.GetPendingEntries();
            Assert.Single(entries);
            Assert.Equal("my-app:v2", entries[0].ImageName);
        }

        [Fact]
        public void RecordCreated_UpdatesPhaseAndNewContainerId()
        {
            _journal.BeginRecreation(MakeEntry("web"));

            _journal.RecordCreated("web", "new-container-id-123");

            RecoveryEntry entry = Assert.Single(_journal.GetPendingEntries());
            Assert.Equal(RecoveryPhase.Created, entry.Phase);
            Assert.Equal("new-container-id-123", entry.NewContainerId);
        }

        [Fact]
        public void RecordStarted_UpdatesPhase()
        {
            _journal.BeginRecreation(MakeEntry("api"));
            _journal.RecordCreated("api", "new-id");

            _journal.RecordStarted("api");

            RecoveryEntry entry = Assert.Single(_journal.GetPendingEntries());
            Assert.Equal(RecoveryPhase.Started, entry.Phase);
        }

        [Fact]
        public void Complete_RemovesEntry()
        {
            _journal.BeginRecreation(MakeEntry("app1"));
            _journal.BeginRecreation(MakeEntry("app2"));

            _journal.Complete("app1");

            IReadOnlyList<RecoveryEntry> remaining = _journal.GetPendingEntries();
            Assert.Single(remaining);
            Assert.Equal("app2", remaining[0].ContainerName);
        }

        [Fact]
        public void Complete_OnUnknownName_DoesNotThrow()
        {
            _journal.Complete("nonexistent");
            Assert.Empty(_journal.GetPendingEntries());
        }

        [Fact]
        public void Journal_SurvivesReload()
        {
            _journal.BeginRecreation(MakeEntry("persistent", imageName: "img:v3"));
            _journal.RecordCreated("persistent", "new-id-abc");
            _journal.RecordStarted("persistent");

            RecoveryJournal reloaded = CreateJournal(_tempDir);

            RecoveryEntry entry = Assert.Single(reloaded.GetPendingEntries());
            Assert.Equal("persistent", entry.ContainerName);
            Assert.Equal("img:v3", entry.ImageName);
            Assert.Equal(RecoveryPhase.Started, entry.Phase);
            Assert.Equal("new-id-abc", entry.NewContainerId);
            Assert.True(entry.WasRunning);
        }

        [Fact]
        public void Journal_HandlesCorruptFile()
        {
            string filePath = Path.Combine(_tempDir, "recovery-journal.json");
            File.WriteAllText(filePath, "NOT VALID JSON {{{");

            RecoveryJournal journal = CreateJournal(_tempDir);

            Assert.Empty(journal.GetPendingEntries());
        }

        [Fact]
        public void Journal_PreservesAllEntryFields()
        {
            RecoveryEntry original = new()
            {
                ContainerName = "svc",
                ImageName = "svc:v5",
                BackupContainerId = "backup-id-xyz",
                NewContainerId = null,
                WasRunning = false,
                ReviveStopped = true,
                Phase = RecoveryPhase.Renamed
            };

            _journal.BeginRecreation(original);

            RecoveryJournal reloaded = CreateJournal(_tempDir);
            RecoveryEntry loaded = Assert.Single(reloaded.GetPendingEntries());

            Assert.Equal("svc", loaded.ContainerName);
            Assert.Equal("svc:v5", loaded.ImageName);
            Assert.Equal("backup-id-xyz", loaded.BackupContainerId);
            Assert.Null(loaded.NewContainerId);
            Assert.False(loaded.WasRunning);
            Assert.True(loaded.ReviveStopped);
            Assert.Equal(RecoveryPhase.Renamed, loaded.Phase);
        }

        [Fact]
        public void Journal_MultipleEntries_IndependentLifecycles()
        {
            _journal.BeginRecreation(MakeEntry("a"));
            _journal.BeginRecreation(MakeEntry("b"));
            _journal.BeginRecreation(MakeEntry("c"));

            _journal.RecordCreated("b", "b-new-id");
            _journal.Complete("a");

            IReadOnlyList<RecoveryEntry> entries = _journal.GetPendingEntries();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.ContainerName == "b" && e.Phase == RecoveryPhase.Created);
            Assert.Contains(entries, e => e.ContainerName == "c" && e.Phase == RecoveryPhase.Renamed);
        }

        private static RecoveryEntry MakeEntry(string name, string imageName = "test:latest")
        {
            return new RecoveryEntry
            {
                ContainerName = name,
                ImageName = imageName,
                BackupContainerId = $"backup-{name}",
                WasRunning = true,
                ReviveStopped = false,
                Phase = RecoveryPhase.Renamed
            };
        }

        private static RecoveryJournal CreateJournal(string dataDir)
        {
            ILogger<RecoveryJournal> logger = NullLogger<RecoveryJournal>.Instance;
            return new RecoveryJournal(dataDir, logger);
        }
    }
}
