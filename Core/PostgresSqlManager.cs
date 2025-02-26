using Core.Entities;
using Dapper;
using Npgsql;

namespace Core;

public interface IPostgresSqlManager
{
	Task CreateUserAsync(Cluster cluster, string username, string password, string database);
	Task CreateDatabaseAsync(Cluster cluster, string database, string owner);
}

public class PostgresSqlManager : IPostgresSqlManager
{
	public async Task CreateUserAsync(Cluster cluster, string username, string password, string database)
	{
		var userCreateSql = $@"
            CREATE ROLE {username} WITH LOGIN PASSWORD '{password}';
            GRANT CONNECT ON DATABASE {database} TO {username};
            ALTER ROLE {username} SET search_path TO public;
        ";

		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		await using var transaction = await connection.BeginTransactionAsync();
		try
		{
			await connection.ExecuteAsync(userCreateSql, transaction: transaction);
			await transaction.CommitAsync();
		}
		catch 
		{
			await transaction.RollbackAsync();
			throw;
		}
	}

	public async Task CreateDatabaseAsync(Cluster cluster, string database, string owner)
	{
		var createDbSql = $@"
            CREATE DATABASE {database} WITH OWNER = {owner} ENCODING = 'UTF8' CONNECTION LIMIT = -1;
        ";

		var connectionString = GetConnectionString(cluster);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		await connection.ExecuteAsync(createDbSql);
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