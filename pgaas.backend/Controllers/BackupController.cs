using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using pgaas.backend.Attributes;
using pgaas.Controllers.Dto.Backups;

namespace pgaas.Controllers;

[ApiController]
[Route("workspace/{workspaceId}/cluster/{clusterId}/backup")]
public class BackupController : ControllerBase
{
	private readonly IRepository<Cluster> _clusterRepository;
	private readonly IKubernetesBackupManager _kubernetesBackupManager;
	private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;

	public BackupController(
		IRepository<Cluster> clusterRepository,
		IKubernetesBackupManager kubernetesBackupManager,
		IKubernetesPostgresClusterManager kubernetesPostgresClusterManager)
	{
		_clusterRepository = clusterRepository;
		_kubernetesBackupManager = kubernetesBackupManager;
		_kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
	}
	
	[HttpGet]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetBackups(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var backups = await _kubernetesBackupManager.GetBackupsAsync(cluster);
		return Ok(backups.Select(b => new { b.Status }));
	}

	[HttpPost]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateBackup(int workspaceId, int clusterId, [FromBody] CreateBackupRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var backup = await _kubernetesBackupManager.CreateBackupAsync(cluster, request.Method);
		return Ok(new { backup });
	}

	[HttpPost("schedule")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> ScheduleBackup(int workspaceId, int clusterId, [FromBody] BackupScheduleRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		
		cluster.Configuration.BackupScheduleCronExpression = request.CronExpression;
		cluster.Configuration.BackupMethod = request.Method;
		await _clusterRepository.UpdateAsync(cluster);
		await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
		return Ok();
	}

	[HttpPost("recovery")]
	[WorkspaceAuthorizationByRole(Role.Admin)]
	public async Task<IActionResult> RecoveryFromBackup(int workspaceId, int clusterId, [FromBody] RecoveryFromBackupRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var backups = await _kubernetesBackupManager.GetBackupsAsync(cluster);
		if (backups.All(b => b.Metadata.Name != request.BackupName))
			return BadRequest("invalid backup name");
	    
		var backup = backups.Single(b => b.Metadata.Name == request.BackupName);
		cluster.ClusterNameInKubernetes = backup.Metadata.Name;
		cluster.RecoveryFromBackup = true;
		cluster.Configuration.Parameters = null;
		await _clusterRepository.UpdateAsync(cluster);
		await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
		return Ok();
	}
}