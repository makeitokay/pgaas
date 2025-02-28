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

	Task<IEnumerable<(string Username, List<string> Roles, DateTime? ExpiryDate)>> GetUsersAsync(Cluster cluster);
	Task<List<string>> GetRolesAsync(Cluster cluster);
	Task<List<string>> GetDatabasesAsync(Cluster cluster);
	Task<IDictionary<string, string>> GetConfigurationAsync(Cluster cluster);
	Task<List<string>> GetConfigurationInvalidParameters(Cluster cluster);
}

public class PostgresSqlManager : IPostgresSqlManager
{
	public async Task CreateOrUpdateUserAsync(Cluster cluster, string username, string password, string database,
		List<string> roles, DateTime? expiryDate = null)
	{
		if (username.StartsWith("pg_"))
		{
			throw new ArgumentException("Cannot create user starting with pg_%.");
		}
		
		var expirySql = expiryDate.HasValue ? $"VALID UNTIL '{expiryDate.Value:yyyy-MM-dd HH:mm:ss}'" : "";
		var rolesSql = roles.Any() ? $"GRANT {string.Join(", ", roles)} TO {username};" : "";

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

	public async Task UpdateUserAsync(Cluster cluster, string username, string newPassword, List<string> newRoles,
		DateTime? newExpiryDate)
	{
		var expirySql = newExpiryDate.HasValue ? $"VALID UNTIL '{newExpiryDate.Value:yyyy-MM-dd HH:mm:ss}'" : "";

		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		var revokeRolesSql = $@"
            DO $$
            DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT grantee, granted_role FROM information_schema.role_table_grants WHERE grantee = '{username}' AND granted_role LIKE 'pg_\%') LOOP
                    EXECUTE 'REVOKE ' || r.granted_role || ' FROM {username}';
                END LOOP;
            END $$;
        ";
		await connection.ExecuteAsync(revokeRolesSql);

		var rolesSql = string.Join(", ", newRoles);
		var updateUserSql = $@"
            ALTER ROLE {username} WITH PASSWORD '{newPassword}' {expirySql};
            GRANT {rolesSql} TO {username};
        ";
		await connection.ExecuteAsync(updateUserSql);
	}

	public async Task<IEnumerable<(string Username, List<string> Roles, DateTime? ExpiryDate)>> GetUsersAsync(
		Cluster cluster)
	{
		var getUsersSql = @"
		SELECT rolname AS username,
		       ARRAY(SELECT r.rolname FROM pg_roles r JOIN pg_auth_members m ON r.oid = m.roleid WHERE m.member = u.oid) AS roles,
		       rolvaliduntil AS expirydate
		FROM pg_roles u where rolname not like 'pg\_%' and rolname not in ('postgres', 'streaming_replica');
        ";
		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return await connection.QueryAsync<(string, List<string>, DateTime?)>(getUsersSql);
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

	private static string GetConnectionString(Cluster cluster)
	{
		var builder = new NpgsqlConnectionStringBuilder
		{
			Host = $"{cluster.ClusterNameInKubernetes}-rw.{cluster.SystemName}",
			Port = 5432,
			Username = "pgaas",
			Password = "qwerty123",
			Database = "postgres",
			Pooling = true
		};
		return builder.ConnectionString;
	}
}