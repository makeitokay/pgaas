using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using pgaas.backend.Attributes;
using pgaas.Controllers.Dto.Replication;

namespace pgaas.Controllers;

[ApiController]
[Route("workspace/{workspaceId}/cluster/{clusterId}/replication")]
public class ReplicationController : ControllerBase
{
    private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;
    private readonly IRepository<Cluster> _clusterRepository;

    public ReplicationController(
        IKubernetesPostgresClusterManager kubernetesPostgresClusterManager,
        IRepository<Cluster> clusterRepository)
    {
        _kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
        _clusterRepository = clusterRepository;
    }

    [HttpGet("hosts")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> GetHostsAsync(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        var hosts = await _kubernetesPostgresClusterManager.GetClusterHostsAsync(cluster);
        return Ok(hosts);
    }

    [HttpPost("hosts")]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> AddHostAsync(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        cluster.Configuration.Instances += 1;
        await _clusterRepository.UpdateAsync(cluster);
        await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
        return Ok();
    }

    [HttpDelete("hosts")]
    [WorkspaceAuthorizationByRole(Role.Admin)]
    public async Task<IActionResult> RemoveHostAsync(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        if (cluster.Configuration.Instances <= 1) return BadRequest("Cannot delete host from cluster");
        
        cluster.Configuration.Instances -= 1;
        if (cluster.Configuration.SyncReplicas > cluster.Configuration.Instances - 1)
        {
            cluster.Configuration.SyncReplicas = cluster.Configuration.Instances - 1;
        }
        await _clusterRepository.UpdateAsync(cluster);
        await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);    
        return Ok();

    }

    [HttpPost("settings")]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> SetReplicationSettingsAsync(int workspaceId, int clusterId, [FromBody] ReplicationSettingsDto settings)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        if (settings.SyncReplicas > cluster.Configuration.Instances - 1 || settings.SyncReplicas <= 0)
        {
	        return BadRequest("Sync replicas should be less or equal than cluster replicas and greater than zero.");
        }
        
        cluster.Configuration.SyncReplicas = settings.SyncReplicas;
        cluster.Configuration.DataDurability = settings.DataDurability;
        await _clusterRepository.UpdateAsync(cluster);
        await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
        return Ok();
    }
}