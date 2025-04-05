using Core;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using pgaas.backend.Attributes;

namespace pgaas.Controllers;

[ApiController]
[Route("workspace/{workspaceId}/cluster/{clusterId}/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly IPostgresSqlManager _sqlManager;
    private readonly IRepository<Cluster> _clusterRepository;
    private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;

    public MonitoringController(
        IPostgresSqlManager sqlManager,
        IRepository<Cluster> clusterRepository,
        IKubernetesPostgresClusterManager kubernetesPostgresClusterManager)
    {
        _sqlManager = sqlManager;
        _clusterRepository = clusterRepository;
        _kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
    }

    [HttpGet("dashboard")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> GetGrafanaLink(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();
        if (cluster.Status != ClusterStatus.Running)
            return BadRequest("Cannot get grafana dashboard for non running cluster");

        var link = $"http://grafana.pgaas.ru/d/{cluster.SystemName}/{cluster.SystemName}";
        return Ok(new { link });
    }

    [HttpGet("top-queries")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> GetTopQueries(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        var queries = await _sqlManager.GetTopQueriesAsync(cluster);
        return Ok(queries);
    }

    [HttpGet("deadlocks")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> GetDeadlocks(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        var deadlocks = await _sqlManager.GetDeadlocksAsync(cluster);
        return Ok(deadlocks);
    }

    [HttpGet("resources")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> GetResourceUsage(int workspaceId, int clusterId)
    {
        var cluster = await _clusterRepository.TryGetAsync(clusterId);
        if (cluster == null) return NotFound();

        var resources = await _kubernetesPostgresClusterManager.GetResourceUsageAsync(cluster);
        return Ok(resources);
    }
}