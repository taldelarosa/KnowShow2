using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Simple SQLite connection pool for efficient concurrent database operations.
/// Manages a pool of reusable SQLite connections to reduce connection overhead.
/// </summary>
public class SqliteConnectionPool : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteConnectionPool> _logger;
    private readonly ConcurrentQueue<SqliteConnection> _availableConnections;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxPoolSize;
    private int _currentConnections;
    private bool _disposed;

    /// <summary>
    /// Initializes a new SQLite connection pool.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="maxPoolSize">Maximum number of connections in the pool (default: 10)</param>
    /// <param name="logger">Logger instance</param>
    public SqliteConnectionPool(string connectionString, int maxPoolSize = 10, ILogger<SqliteConnectionPool>? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteConnectionPool>.Instance;
        _maxPoolSize = maxPoolSize;
        _availableConnections = new ConcurrentQueue<SqliteConnection>();
        _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        _currentConnections = 0;

        // Enable WAL mode for better concurrent performance if it's a file database
        if (!connectionString.Contains(":memory:"))
        {
            EnableWalMode();
        }
    }

    /// <summary>
    /// Gets a connection from the pool or creates a new one if pool is not full.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A pooled SQLite connection</returns>
    public async Task<PooledSqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteConnectionPool));

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            // Try to get an existing connection from the pool
            if (_availableConnections.TryDequeue(out var existingConnection))
            {
                if (existingConnection.State == System.Data.ConnectionState.Open)
                {
                    return new PooledSqliteConnection(existingConnection, this);
                }
                else
                {
                    // Connection was closed, dispose it and create new one
                    existingConnection.Dispose();
                    Interlocked.Decrement(ref _currentConnections);
                }
            }

            // Create a new connection
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            Interlocked.Increment(ref _currentConnections);

            _logger.LogDebug("Created new pooled connection. Current pool size: {CurrentConnections}", _currentConnections);
            
            return new PooledSqliteConnection(connection, this);
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Returns a connection to the pool for reuse.
    /// </summary>
    /// <param name="connection">The connection to return</param>
    internal void ReturnConnection(SqliteConnection connection)
    {
        if (_disposed || connection == null)
        {
            connection?.Dispose();
            _semaphore.Release();
            return;
        }

        if (connection.State == System.Data.ConnectionState.Open)
        {
            _availableConnections.Enqueue(connection);
        }
        else
        {
            connection.Dispose();
            Interlocked.Decrement(ref _currentConnections);
        }

        _semaphore.Release();
    }

    /// <summary>
    /// Enables WAL mode for better concurrent performance on file databases.
    /// </summary>
    private void EnableWalMode()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            var result = command.ExecuteScalar()?.ToString();
            
            _logger.LogInformation("SQLite WAL mode enabled for better concurrent performance: {Result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not enable WAL mode: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Gets the current number of connections in the pool.
    /// </summary>
    public int CurrentConnections => _currentConnections;

    /// <summary>
    /// Gets the maximum pool size.
    /// </summary>
    public int MaxPoolSize => _maxPoolSize;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Dispose all connections in the pool
        while (_availableConnections.TryDequeue(out var connection))
        {
            connection.Dispose();
        }

        _semaphore.Dispose();
        _logger.LogInformation("SQLite connection pool disposed. Total connections created: {TotalConnections}", _currentConnections);
    }
}

/// <summary>
/// A wrapper around SqliteConnection that automatically returns the connection to the pool when disposed.
/// </summary>
public class PooledSqliteConnection : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteConnectionPool _pool;
    private bool _disposed;

    internal PooledSqliteConnection(SqliteConnection connection, SqliteConnectionPool pool)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Gets the underlying SQLite connection.
    /// </summary>
    public SqliteConnection Connection => _connection;

    /// <summary>
    /// Creates a new command for this connection.
    /// </summary>
    /// <returns>A new SqliteCommand</returns>
    public SqliteCommand CreateCommand() => _connection.CreateCommand();

    /// <summary>
    /// Returns the connection to the pool instead of disposing it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.ReturnConnection(_connection);
    }
}