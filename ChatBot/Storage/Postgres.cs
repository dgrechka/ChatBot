using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Storage
{
    public class PostgresConnection : IDisposable
    {
        private readonly NpgsqlDataSource _dataSource;

        public NpgsqlDataSource DataSource => _dataSource;

        public PostgresConnection(PostgresConfig config)
        {
            var connectionString = $"Host={config.Host};Port={config.Port};Username={config.Username};Password={config.Password};Database={config.Database}";
            _dataSource = NpgsqlDataSource.Create(connectionString);
        }

        public void Dispose()
        {
            _dataSource.Dispose();
        }
    }

    public abstract class PostgresBasedStorage
    {
        protected readonly PostgresConnection _postgres;
        private bool _initialized = false;
        private SemaphoreSlim _initSem = new SemaphoreSlim(1);

        protected abstract Task InitializeCore(CancellationToken token);

        protected async Task EnsureInitialized(CancellationToken token)
        {
            await _initSem.WaitAsync();
            try
            {
                // create Message History table if it doesn't exist
                if (!_initialized)
                {
                    await InitializeCore(token);
                    _initialized = true;
                }
            }
            finally
            {
                _initSem.Release();
            }
        }
        public PostgresBasedStorage(PostgresConnection postgres)
        {
            _postgres = postgres;
        }


    }
}
