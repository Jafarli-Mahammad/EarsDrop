using Dapper;
using Infrastructure.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence;

public class DataContext
{
    private readonly string _databasePath;
    private readonly ILogger<DataContext> _logger;
    private volatile bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public DataContext(
        IOptions<DownloadOptions> downloadOptions,
        ILogger<DataContext> logger)
    {
        _logger = logger;

        // Resolve to an absolute path immediately so we always know where the DB is.
        var rawPath = downloadOptions.Value.DatabasePath;
        _databasePath = Path.GetFullPath(rawPath);

        _logger.LogInformation(
            "DataContext: resolved SQLite database path → '{DatabasePath}'",
            _databasePath);

        // Ensure the containing directory exists right now, not just during Init.
        EnsureDirectoryExists();
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    public string DatabasePath => _databasePath;

    public SqliteConnection CreateConnection()
    {
        // Guard: directory must exist before we attempt to open the DB.
        EnsureDirectoryExists();

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            // Wait for locks instead of failing immediately with SQLITE_BUSY when
            // multiple downloads write concurrently.
            DefaultTimeout = 30
        }.ConnectionString;

        return new SqliteConnection(connectionString);
    }

    /// <summary>
    /// Idempotent: safe to call multiple times. Creates tables if they don't exist yet.
    /// </summary>
    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            EnsureDirectoryExists();

            _logger.LogInformation(
                "Initializing SQLite database at '{DatabasePath}'…",
                _databasePath);

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // WAL journal mode allows concurrent readers while a writer is active,
            // which suits a desktop app running multiple downloads in parallel.
            await connection.ExecuteAsync(new CommandDefinition(
                "PRAGMA journal_mode=WAL;", cancellationToken: cancellationToken));

            const string sql = """
                CREATE TABLE IF NOT EXISTS DownloadJobs (
                    Id            TEXT    PRIMARY KEY,
                    Url           TEXT    NOT NULL,
                    Platform      INTEGER NOT NULL,
                    Title         TEXT    NOT NULL,
                    Uploader      TEXT    NOT NULL,
                    DurationTicks INTEGER NOT NULL,
                    ThumbnailUrl  TEXT,
                    OutputFormat  INTEGER NOT NULL,
                    Status        INTEGER NOT NULL,
                    OutputPath    TEXT,
                    MetadataJson  TEXT,
                    CreatedAt     TEXT    NOT NULL,
                    CompletedAt   TEXT,
                    ErrorMessage  TEXT
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));

            _initialized = true;
            _logger.LogInformation(
                "SQLite database initialized successfully at '{DatabasePath}'",
                _databasePath);
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private void EnsureDirectoryExists()
    {
        var folder = Path.GetDirectoryName(_databasePath);
        if (string.IsNullOrEmpty(folder)) return;

        if (!Directory.Exists(folder))
        {
            _logger.LogInformation(
                "Creating database directory '{Folder}'",
                folder);
            Directory.CreateDirectory(folder);
        }
    }
}