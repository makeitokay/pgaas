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

	public BackupController(IBackupService backupService, IRepository<Cluster> clusterRepository)
	{
		_backupService = backupService;
		_clusterRepository = clusterRepository;
	}
	
	[HttpGet]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetBackups(int workspaceId, int clusterId)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var backups = await _backupService.GetBackupsAsync(cluster);
		return Ok(backups);
	}

	[HttpPost]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> CreateBackup(int workspaceId, int clusterId, [FromBody] CreateBackupRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		var backupId = await _backupService.CreateBackupAsync(cluster, request.Method);
		return Ok(new { BackupId = backupId });
	}

	[HttpPost("schedule")]
	[WorkspaceAuthorizationByRole(Role.Editor)]
	public async Task<IActionResult> ScheduleBackup(int workspaceId, int clusterId, [FromBody] BackupScheduleRequest request)
	{
		var cluster = await _clusterRepository.GetAsync(clusterId);
		await _backupService.ScheduleBackupAsync(cluster, request.CronExpression, request.Method);
		return Ok();
	}
}