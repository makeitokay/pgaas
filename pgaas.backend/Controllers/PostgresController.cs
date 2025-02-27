using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using pgaas.backend.Attributes;
using pgaas.Controllers.Dto.Sql;

namespace pgaas.Controllers;

[Route("workspace/{workspaceId}/cluster/{clusterId}")]
[ApiController]
public class PostgresController : ControllerBase
{
	private readonly IPostgresSqlManager _postgresSqlManager;
	private readonly IRepository<Cluster> _clusterRepository;

	public PostgresController(IPostgresSqlManager sqlManager, IRepository<Cluster> clusterRepository)
	{
		_postgresSqlManager = sqlManager;
		_clusterRepository = clusterRepository;
	}

	[HttpPost("database")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateDatabase(int workspaceId, int clusterId, [FromBody] CreateDatabaseRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.CreateDatabaseAsync(cluster, request.Database, request.Owner);
		return Ok();
	}

	[HttpPost("user")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateOrUpdateUser(int workspaceId, int clusterId, [FromBody] CreateUserRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.CreateOrUpdateUserAsync(cluster, request.Username, request.Password, request.Database, request.Roles, request.ExpiryDate);
		return Ok();
	}

	[HttpGet("users")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetUsers(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var users = await _postgresSqlManager.GetUsersAsync(cluster);
		return Ok(users);
	}
	
	[HttpGet("roles")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetAvailableRoles(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var roles = await _postgresSqlManager.GetRolesAsync(cluster);
		return Ok(roles);
	}

	[HttpDelete("user/{username}")]
	[WorkspaceAuthorizationByRole(Role.Admin)]
	public async Task<IActionResult> DeleteUser(int workspaceId, int clusterId, string username)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.DeleteUserAsync(cluster, username);
		return Ok();
	}
}
