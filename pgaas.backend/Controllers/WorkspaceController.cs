using Core.Entities;
using Core.Repositories;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pgaas.Controllers.Dto.Workspaces;

namespace pgaas.Controllers;

[ApiController]
[Route("workspaces")]
[Authorize]
public class WorkspaceController : ControllerBase
{
	private readonly IRepository<Workspace> _repository;
	private readonly IRepository<WorkspaceUser> _workspaceUserRepository;
	private readonly IRepository<User> _userRepository;

	public WorkspaceController(
		IRepository<Workspace> repository,
		IRepository<WorkspaceUser> workspaceUserRepository,
		IRepository<User> userRepository)
	{
		_repository = repository;
		_workspaceUserRepository = workspaceUserRepository;
		_userRepository = userRepository;
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateWorkspaceDto createWorkspaceDto)
	{
		if (string.IsNullOrWhiteSpace(createWorkspaceDto.Name))
		{
			return BadRequest("Workspace name is required.");
		}
		
		var userId = User.Claims.GetUserId();
		var user = await _userRepository.GetAsync(userId);

		var workspace = new Workspace
		{
			Name = createWorkspaceDto.Name,
			Owner = user
		};
		await _repository.CreateAsync(workspace);

		await _workspaceUserRepository.CreateAsync(new WorkspaceUser
		{
			User = user,
			Workspace = workspace,
			Role = Role.Admin
		});

		return Ok();
	}

	[HttpGet("{id}")]
	public async Task<IActionResult> Get(int id)
	{
		var workspace = await _repository.TryGetAsync(id);
		if (workspace == null)
		{
			return NotFound();
		}

		var workspaceDto = new WorkspaceDto
		{
			Id = workspace.Id,
			Name = workspace.Name
		};

		return Ok(workspaceDto);
	}
}
