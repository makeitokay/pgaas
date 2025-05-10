using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using pgaas.backend;
using pgaas.backend.Attributes;
using pgaas.Controllers.Dto.Postgres;

namespace pgaas.Controllers;

[Route("workspace/{workspaceId}/cluster/{clusterId}")]
[ApiController]
public class PostgresController : ControllerBase
{
	private readonly IPostgresSqlManager _postgresSqlManager;
	private readonly IRepository<Cluster> _clusterRepository;
	private readonly IOptions<List<PostgresConfigurationParameter>> _postgresConfigurationParameters;
	private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;

	public PostgresController(
		IPostgresSqlManager sqlManager,
		IRepository<Cluster> clusterRepository,
		IOptions<List<PostgresConfigurationParameter>> postgresConfigurationParameters,
		IKubernetesPostgresClusterManager kubernetesPostgresClusterManager)
	{
		_postgresSqlManager = sqlManager;
		_clusterRepository = clusterRepository;
		_postgresConfigurationParameters = postgresConfigurationParameters;
		_kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
	}

	[HttpPost("database")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateDatabaseAsync(int workspaceId, int clusterId, [FromBody] CreateDatabaseRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.CreateDatabaseAsync(cluster, request.Database, request.Owner, request.LcCollate, request.LcCtype);
		return Ok();
	}
	
	[HttpGet("databases")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetDatabasesAsync(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var databases = await _postgresSqlManager.GetDatabasesAsync(cluster);
		return Ok(databases);
	}

	[HttpDelete("database/{database}")]
	[WorkspaceAuthorizationByRole(Role.Admin)]
	public async Task<IActionResult> DeleteDatabaseAsync(int workspaceId, int clusterId, string database)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.DeleteDatabaseAsync(cluster, database);
		return Ok();
	}

	[HttpPost("user")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateOrUpdateUserAsync(int workspaceId, int clusterId, [FromBody] CreateUserRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.CreateOrUpdateUserAsync(cluster, request.Username, request.Password, request.Database, request.Roles ?? [], request.ExpiryDate);
		return Ok();
	}

	[HttpGet("users")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetUsersAsync(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var users = await _postgresSqlManager.GetUsersAsync(cluster);
		return Ok(users
			.Select(u => new { u.Username, u.Roles, u.ExpiryDate, canBeEdited = u.Username != cluster.Configuration.OwnerName })
			.OrderBy(u => u.Username));
	}
	
	[HttpGet("roles")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetAvailableRolesAsync(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var roles = await _postgresSqlManager.GetRolesAsync(cluster);
		return Ok(roles);
	}

	[HttpDelete("user/{username}")]
	[WorkspaceAuthorizationByRole(Role.Admin)]
	public async Task<IActionResult> DeleteUserAsync(int workspaceId, int clusterId, string username)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		if (username == cluster.Configuration.OwnerName || username == "pgaas")
			return BadRequest("Cannot delete this user.");
		await _postgresSqlManager.DeleteUserAsync(cluster, username);
		return Ok();
	}
	
	[HttpGet("configuration")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetConfigurationAsync(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var config = await _postgresSqlManager.GetConfigurationAsync(cluster);

		var parameters = _postgresConfigurationParameters
			.Value
			.Select(setting =>
				setting with
				{
					Value = config.TryGetValue(setting.Name, out var value) ? value : null
				}).ToList();

		return Ok(parameters);
	}
	
	[HttpPost("configuration")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> UpdateConfigurationAsync(
		int workspaceId,
		int clusterId,
		[FromBody] Dictionary<string, string?> parameters)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		cluster.Configuration.Parameters = parameters;
		await _clusterRepository.UpdateAsync(cluster);
		await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);

		return Ok();
	}
	
	[HttpGet("configuration/readiness")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> GetConfigurationReadinessAsync(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var status = await _kubernetesPostgresClusterManager.GetClusterStatusAsync(cluster);
		if (status is null || !status.IsHealthy())
			return Ok(ConfigurationReadinessDto.Waiting());
		
		var configuration = await _postgresSqlManager.GetConfigurationAsync(cluster);
		var configurationInDatabase = cluster.Configuration.Parameters;
		if (configurationInDatabase is null)
			return Ok(ConfigurationReadinessDto.Success());

		var mismatchedParameters = configurationInDatabase
			.Where(kv =>
			{
				configuration.TryGetValue(kv.Key, out var valueInDatabase);
				return kv.Value != valueInDatabase
				       && !(string.IsNullOrWhiteSpace(kv.Value) && string.IsNullOrWhiteSpace(valueInDatabase));
			})
			.Select(kv => kv.Key)
			.ToList();
		if (mismatchedParameters.Count == 0)
			return Ok(ConfigurationReadinessDto.Success());

		var invalidParameters = await _postgresSqlManager.GetConfigurationInvalidParameters(cluster);
		return Ok(mismatchedParameters.All(p => invalidParameters.Contains(p))
			? ConfigurationReadinessDto.Failed(mismatchedParameters)
			: ConfigurationReadinessDto.Waiting());

	}
}
