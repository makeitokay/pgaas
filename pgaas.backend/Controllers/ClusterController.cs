using Core;
using Core.Entities;
using Core.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pgaas.Controllers.Dto.Clusters;
using pgaas.backend.Attributes;

namespace pgaas.Controllers;

[ApiController]
[Route("workspace/{workspaceId}/cluster")]
public class ClusterController : ControllerBase
{
	private readonly IRepository<Cluster> _clusterRepository;
	private readonly IKubernetesPostgresClusterManager _kubernetesPostgresClusterManager;
	private readonly IValidator<CreateClusterDto> _validator;

    public ClusterController(
	    IRepository<Cluster> clusterRepository,
	    IKubernetesPostgresClusterManager kubernetesPostgresClusterManager,
	    IValidator<CreateClusterDto> validator)
    {
	    _clusterRepository = clusterRepository;
	    _kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
	    _validator = validator;
    }

    [HttpPost]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> CreateOrEdit(int workspaceId, [FromBody] CreateClusterDto createClusterDto)
    {
	    var validationResult = await _validator.ValidateAsync(createClusterDto);
	    if (!validationResult.IsValid)
	    {
		    var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
		    return BadRequest(errors);
	    }

	    Cluster cluster;
	    var existingCluster = await _clusterRepository.Items
		    .FirstOrDefaultAsync(c => c.SystemName == createClusterDto.SystemName);
	    if (existingCluster != null)
	    {
		    if (existingCluster.Status != ClusterStatus.Running)
			    return BadRequest("Cannot edit non running cluster.");
			    
		    cluster = existingCluster;
		    if (cluster.Configuration.StorageSize != createClusterDto.StorageSize)
			    cluster.Status = ClusterStatus.RecreatingStorage;
	    }
	    else
	    {
		    cluster = new Cluster
		    {
			    Status = ClusterStatus.Initialization,
			    SystemName = createClusterDto.SystemName
		    };
	    }

        cluster.Configuration = new ClusterConfiguration
        {
            StorageSize = createClusterDto.StorageSize,
            Cpu = createClusterDto.Cpu,
            Memory = createClusterDto.Memory,
            MajorVersion = createClusterDto.MajorVersion,
            DatabaseName = createClusterDto.DatabaseName,
            LcCollate = createClusterDto.LcCollate,
            LcCtype = createClusterDto.LcCtype,
            Instances = createClusterDto.Instances,
            OwnerName = createClusterDto.OwnerName
        };
        
        if (existingCluster != null)
        {
	        await _clusterRepository.UpdateAsync(cluster);
        }
        else
        {
	        await _clusterRepository.CreateAsync(cluster);
        }

        return Ok();
    }

    [HttpPost("{id}/restart")]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> Restart(int workspaceId, int id)
    {
        var cluster = await _clusterRepository.TryGetAsync(id);
        if (cluster == null)
        {
            return NotFound();
        }

        if (cluster.Status > ClusterStatus.Deleting)
        {
            return BadRequest("Cannot restart a deleted cluster.");
        }

        await _kubernetesPostgresClusterManager.RestartClusterAsync(cluster);
        cluster.Status = ClusterStatus.Restarting;
        await _clusterRepository.UpdateAsync(cluster);

        return Ok(cluster);
    }

    [HttpGet("{id}")]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> Get(int workspaceId, int id)
    {
        var cluster = await _clusterRepository.TryGetAsync(id);
        if (cluster == null)
        {
            return NotFound();
        }

        return Ok(cluster);
    }
    
    [HttpGet]
    [WorkspaceAuthorizationByRole(Role.Viewer)]
    public async Task<IActionResult> Get(int workspaceId)
    {
	    var clusters = await _clusterRepository.Items.Where(c => c.WorkspaceId == workspaceId).ToListAsync();
	    return Ok(clusters);
    }
}