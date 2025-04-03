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
	private readonly IRepository<SecurityGroup> _securityGroupRepository;

    public ClusterController(
	    IRepository<Cluster> clusterRepository,
	    IKubernetesPostgresClusterManager kubernetesPostgresClusterManager,
	    IValidator<CreateClusterDto> validator, IRepository<SecurityGroup> securityGroupRepository)
    {
	    _clusterRepository = clusterRepository;
	    _kubernetesPostgresClusterManager = kubernetesPostgresClusterManager;
	    _validator = validator;
	    _securityGroupRepository = securityGroupRepository;
    }

    [HttpPost]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> CreateOrEditAsync(int workspaceId, [FromBody] CreateClusterDto createClusterDto)
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
		    if (cluster.Configuration.StorageSize > createClusterDto.StorageSize)
		    {
				return BadRequest("Cannot decrease cluster storage size");
		    }
	    }
	    else
	    {
		    cluster = new Cluster
		    {
			    Status = ClusterStatus.Starting,
			    SystemName = createClusterDto.SystemName,
			    ClusterNameInKubernetes = createClusterDto.SystemName,
			    WorkspaceId = workspaceId
		    };
	    }

	    if (createClusterDto.SecurityGroupId is not null)
	    {
		    var sg = await _securityGroupRepository.TryGetAsync(createClusterDto.SecurityGroupId.Value);
		    if (sg is null)
			    return BadRequest("Security group not found.");
		    
		    cluster.SecurityGroupId = sg.Id;
		    cluster.SecurityGroup = sg;
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
            OwnerName = createClusterDto.OwnerName,
            OwnerPassword = createClusterDto.OwnerPassword,
            PoolerMode = createClusterDto.PoolerMode,
            PoolerMaxConnections = createClusterDto.PoolerMaxConnections,
            PoolerDefaultPoolSize = createClusterDto.PoolerDefaultPoolSize,
            BackupScheduleCronExpression = createClusterDto.BackupScheduleCronExpression,
            BackupMethod = createClusterDto.BackupMethod
        };
        
        if (existingCluster != null)
        {
	        await _clusterRepository.UpdateAsync(cluster);
	        await _kubernetesPostgresClusterManager.UpdateClusterAsync(cluster);
        }
        else
        {
	        await _clusterRepository.CreateAsync(cluster);
	        await _kubernetesPostgresClusterManager.CreateClusterAsync(cluster);
        }

        return Ok();
    }

    [HttpPost("{id}/restart")]
    [WorkspaceAuthorizationByRole(Role.Editor)]
    public async Task<IActionResult> RestartAsync(int workspaceId, int id)
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
    public async Task<IActionResult> GetAsync(int workspaceId, int id)
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
    public async Task<IActionResult> GetAsync(int workspaceId)
    {
	    var clusters = await _clusterRepository.Items.Where(c => c.WorkspaceId == workspaceId).ToListAsync();
	    return Ok(clusters);
    }
}