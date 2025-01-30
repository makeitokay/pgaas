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
	public async Task<IActionResult> CreateUser(int workspaceId, int clusterId, [FromBody] CreateUserRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _postgresSqlManager.CreateUserAsync(cluster, request.Username, request.Password, request.Database);

		return Ok();
	}
}