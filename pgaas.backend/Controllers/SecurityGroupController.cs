using System.Text.RegularExpressions;
using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pgaas.backend.Attributes;
using pgaas.Controllers.Dto.SecurityGroups;

namespace pgaas.Controllers;

[Route("workspace/{workspaceId:int}/sg")]
[ApiController]
public class SecurityGroupController : ControllerBase
{
	private readonly IRepository<SecurityGroup> _repository;
	private readonly IRepository<Cluster> _clusterRepository;
	private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;

	private static readonly string _ipRegex = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)([\/][0-3][0-2]?|[\/][1-2][0-9]|[\/][0-9])?$";

	public SecurityGroupController(
		IRepository<SecurityGroup> repository,
		IKubernetesPostgresClusterManager kubernetesPostgresClusterManager,
		IRepository<Cluster> clusterRepository)
	{
		_repository = repository;
		_kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
		_clusterRepository = clusterRepository;
	}

	[HttpGet]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetAllAsync(int workspaceId)
	{
		var securityGroups = await _repository
			.Items
			.Where(sg => sg.WorkspaceId == workspaceId)
			.ToListAsync();
		return Ok(securityGroups.Select(sg => new { sg.Id, sg.WorkspaceId, sg.Name, sg.AllowedIps}));
	}

	[HttpPost]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateOrUpdateAsync(int workspaceId, [FromBody] CreateOrUpdateSecurityGroupDto request)
	{
		if (request.AllowedIps.Any(ip => !Regex.IsMatch(ip, _ipRegex)))
			return BadRequest("Incorrect IP addresses.");
		
		SecurityGroup? securityGroup = null;
		if (request.Id is not null)
			securityGroup = await _repository.Items
				.Where(sg => sg.Id == request.Id.Value)
				.FirstOrDefaultAsync();
		if (securityGroup == null)
		{
			securityGroup = new SecurityGroup
			{
				Name = request.Name,
				AllowedIps = request.AllowedIps,
				WorkspaceId = workspaceId
			};
			await _repository.CreateAsync(securityGroup);
		}
		else
		{
			securityGroup.Name = request.Name;
			securityGroup.AllowedIps = request.AllowedIps;
			await _repository.UpdateAsync(securityGroup);
			
			var clusters = _clusterRepository.Items
				.Where(c =>
					c.WorkspaceId == workspaceId
					&& c.SecurityGroupId == securityGroup.Id)
				.ToList();

			foreach (var cluster in clusters)
			{
				await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
			}
		}
        
		return Ok();
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync(int workspaceId, int id)
	{
		var securityGroup = await _repository.TryGetAsync(id);
		if (securityGroup == null || securityGroup.WorkspaceId != workspaceId)
		{
			return NotFound();
		}
        
		await _repository.DeleteAsync(securityGroup);
		return Ok();
	}
}