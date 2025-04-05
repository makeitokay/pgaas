using Core.Entities;
using Dapper;
using Npgsql;

namespace Core;

public interface IPostgresSqlManager
{
	Task CreateOrUpdateUserAsync(Cluster cluster, string username, string password, string database, List<string> roles, DateTime? expiryDate = null);
	Task CreateDatabaseAsync(Cluster cluster, string database, string owner, string lcCollate, string lcCtype);
	Task DeleteDatabaseAsync(Cluster cluster, string database);
	Task DeleteUserAsync(Cluster cluster, string username);

	Task<IEnumerable<(string Username, string[] Roles, DateTime? ExpiryDate)>> GetUsersAsync(Cluster cluster);
	Task<List<string>> GetRolesAsync(Cluster cluster);
	Task<List<string>> GetDatabasesAsync(Cluster cluster);
	Task<IDictionary<string, string>> GetConfigurationAsync(Cluster cluster);
	Task<List<string>> GetConfigurationInvalidParameters(Cluster cluster);
	
	Task<IEnumerable<object>> GetTopQueriesAsync(Cluster cluster);
	Task<IEnumerable<object>> GetDeadlocksAsync(Cluster cluster);

}

public class PostgresSqlManager(bool useLocalKubernetesAddress) : IPostgresSqlManager
{
	public async Task CreateOrUpdateUserAsync(Cluster cluster, string username, string password, string database,
		List<string> roles, DateTime? expiryDate = null)
	{
		if (username.StartsWith("pg_"))
		{
			throw new ArgumentException("Cannot create user starting with pg_%.");
		}
		
		var expirySql = expiryDate.HasValue ? $"VALID UNTIL '{expiryDate.Value:yyyy-MM-dd HH:mm:ss}'" : "";
		var rolesSql = roles.Count != 0 ? $"GRANT {string.Join(", ", roles)} TO {username};" : "";

		var revokePgRolesSql = $@"
            DO $$
		    DECLARE r TEXT;
		    BEGIN
		        FOR r IN
		            SELECT r.rolname
		            FROM pg_roles r
		                     JOIN pg_auth_members m ON r.oid = m.roleid
		            WHERE m.member = (SELECT oid FROM pg_roles WHERE rolname = '{username}')
		              AND r.rolname LIKE 'pg_%'
		            LOOP
		                EXECUTE 'REVOKE ' || r || ' FROM {username}';
		            END LOOP;
		    END $$;
        ";

		var userCreateOrUpdateSql = $@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{username}') THEN
                    CREATE ROLE {username} WITH LOGIN PASSWORD '{password}' {expirySql};
                ELSE
                    ALTER ROLE {username} WITH LOGIN PASSWORD '{password}' {expirySql};
                END IF;
            END $$;
            {revokePgRolesSql}
            {rolesSql}
            GRANT CONNECT ON DATABASE {database} TO {username};
            ALTER ROLE {username} SET search_path TO public;
        ";

		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		await using var transaction = await connection.BeginTransactionAsync();
		try
		{
			await connection.ExecuteAsync(userCreateOrUpdateSql, transaction: transaction);
			await transaction.CommitAsync();
		}
		catch 
		{
			await transaction.RollbackAsync();
			throw;
		}
	}
	
	public async Task<List<string>> GetDatabasesAsync(Cluster cluster)
	{
		var getDatabasesSql = "SELECT datname FROM pg_database WHERE datistemplate = false and datname != 'postgres';";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return (await connection.QueryAsync<string>(getDatabasesSql)).ToList();
	}

	public async Task<IDictionary<string, string?>> GetConfigurationAsync(Cluster cluster)
	{
		var getConfigSql = "SELECT name, setting FROM pg_settings";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		var results = await connection.QueryAsync<(string, string?)>(getConfigSql);
		return results.ToDictionary(r => r.Item1, r => r.Item2);
	}

	public async Task<List<string>> GetConfigurationInvalidParameters(Cluster cluster)
	{
		var sql = "select name from pg_file_settings where error is not null;";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		var results = await connection.QueryAsync<string>(sql);
		return results.ToList();
	}

	public async Task<IEnumerable<object>> GetTopQueriesAsync(Cluster cluster)
	{
		var result = new List<object>();
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		var cmd = new NpgsqlCommand(@"
                SELECT query, total_exec_time, calls,
                       mean_exec_time, stddev_exec_time,
                       rows, shared_blks_hit, shared_blks_read
                FROM pg_stat_statements
                ORDER BY total_exec_time DESC
                LIMIT 10;", connection);

		await using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			result.Add(new
			{
				Query = reader["query"].ToString(),
				TotalExecTime = reader["total_exec_time"],
				Calls = reader["calls"],
				MeanExecTime = reader["mean_exec_time"],
				StddevExecTime = reader["stddev_exec_time"],
				Rows = reader["rows"],
				SharedBlocksHit = reader["shared_blks_hit"],
				SharedBlocksRead = reader["shared_blks_read"]
			});
		}
		return result;
	}


	public async Task<IEnumerable<object>> GetDeadlocksAsync(Cluster cluster)
	{
		var result = new List<object>();
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		var cmd = new NpgsqlCommand(@"SELECT datname, deadlocks FROM pg_stat_database where datname not in ('postgres', 'template1', 'template0') and datname is not null;", connection);

		try
		{
			await using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				result.Add(new
				{
					Database = reader["datname"].ToString(),
					DeadlockCount = reader["deadlocks"]
				});
			}
		}
		catch
		{
			result.Add(new { Error = "Deadlock log data not accessible." });
		}

		return result;
	}

	public async Task DeleteDatabaseAsync(Cluster cluster, string database)
	{
		var dropDbSql = $"DROP DATABASE IF EXISTS {database};";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await connection.ExecuteAsync(dropDbSql);
	}
	
	public async Task CreateDatabaseAsync(Cluster cluster, string database, string owner, string lcCollate, string lcCtype)
	{
		var createDbSql = $@"
			CREATE DATABASE {database} WITH OWNER = {owner} ENCODING = 'UTF8' LC_COLLATE = '{lcCollate}' LC_CTYPE = '{lcCtype}' CONNECTION LIMIT = -1;
		";

		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await connection.ExecuteAsync(createDbSql);
	}
	
	public async Task DeleteUserAsync(Cluster cluster, string username)
	{
		var deleteUserSql = $@"
            REASSIGN OWNED BY {username} TO postgres;
            DROP OWNED BY {username};
            DROP ROLE {username};
        ";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await connection.ExecuteAsync(deleteUserSql);
	}

	public async Task<IEnumerable<(string Username, string[] Roles, DateTime? ExpiryDate)>> GetUsersAsync(
		Cluster cluster)
	{
		var getUsersSql = @"
		SELECT rolname AS username,
		       ARRAY(SELECT r.rolname FROM pg_roles r JOIN pg_auth_members m ON r.oid = m.roleid WHERE m.member = u.oid) AS roles,
		       rolvaliduntil AS expirydate
		FROM pg_roles u where rolname not like 'pg\_%' and rolname not in ('postgres', 'streaming_replica') and rolname != 'pgaas';
        ";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return await connection.QueryAsync<(string, string[], DateTime?)>(getUsersSql);
	}

	public async Task<List<string>> GetRolesAsync(Cluster cluster)
	{
		var getRolesSql = @"
			SELECT rolname AS role
				FROM pg_roles u where rolname like 'pg\_%' and rolname not in ('postgres', 'streaming_replica');
        ";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return (await connection.QueryAsync<string>(getRolesSql)).ToList();
	}

	private string GetConnectionString(Cluster cluster)
	{
		var host = useLocalKubernetesAddress
			? $"{cluster.ClusterNameInKubernetes}-rw.{cluster.SystemName}"
			: $"localhost:5433";
		var builder = new NpgsqlConnectionStringBuilder
		{
			Host = host,
			Port = 5432,
			Username = "pgaas",
			Password = "qwerty123",
			Database = "postgres",
			Pooling = true,
			SslMode = SslMode.Disable
		};
		return builder.ConnectionString;
	}
}