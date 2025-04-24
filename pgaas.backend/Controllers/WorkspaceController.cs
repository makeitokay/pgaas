using Core.Entities;
using Core.Repositories;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pgaas.backend.Attributes;
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
	public async Task<IActionResult> CreateAsync([FromBody] CreateWorkspaceDto createWorkspaceDto)
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
	
	[HttpPost("{workspaceId}/invite")]
	[WorkspaceAuthorizationByRole(Role.Admin)]
	public async Task<IActionResult> InviteUserAsync(int workspaceId, [FromBody] InviteUserDto inviteUserDto)
	{
		if (string.IsNullOrWhiteSpace(inviteUserDto.Email) || !Enum.IsDefined(typeof(Role), inviteUserDto.Role))
		{
			return BadRequest("Invalid email or role.");
		}

		var workspace = await _repository.TryGetAsync(workspaceId);
		if (workspace == null)
		{
			return NotFound("Workspace not found.");
		}

		var user = await _userRepository.Items.Where(u => u.Email == inviteUserDto.Email).FirstOrDefaultAsync();
		if (user == null)
		{
			return NotFound("User not found.");
		}

		var existingWorkspaceUser = await _workspaceUserRepository
			.Items
			.Where(wu => wu.WorkspaceId == workspaceId && wu.UserId == user.Id)
			.FirstOrDefaultAsync();
		if (existingWorkspaceUser != null)
		{
			return BadRequest("User is already in the workspace.");
		}

		var workspaceUser = new WorkspaceUser
		{
			User = user,
			Workspace = workspace,
			Role = inviteUserDto.Role
		};
    
		await _workspaceUserRepository.CreateAsync(workspaceUser);

		return Ok();
	}

	[HttpGet("{workspaceId}")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetAsync(int workspaceId)
	{
		var workspace = await _repository.TryGetAsync(workspaceId);
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
	
	[HttpGet]
	[Authorize]
	public async Task<IActionResult> GetAsync()
	{
		var workspaces = await _workspaceUserRepository
			.Items
			.Where(wu => wu.UserId == User.Claims.GetUserId())
			.Select(wu => wu.Workspace)
			.Distinct()
			.ToListAsync();

		return Ok(workspaces.Select(w => new WorkspaceDto
		{
			Id = w.Id,
			Name = w.Name
		}));
	}
	
	[HttpGet("{workspaceId}/users")]
	[WorkspaceAuthorizationByRole(Role.Viewer)]
	public async Task<IActionResult> GetWorkspaceUsersAsync(int workspaceId)
	{
		var workspace = await _repository.TryGetAsync(workspaceId);
		if (workspace == null)
		{
			return NotFound("Workspace not found.");
		}

		var users = await _workspaceUserRepository.Items.Where(wu => wu.WorkspaceId == workspaceId).ToListAsync();
		var userDtos = users.Select(u => new {
			u.User.Id,
			u.User.Email,
			u.Role
		});

		return Ok(userDtos);
	}
	
	[HttpGet("{workspaceId}/users/me")]
	public async Task<IActionResult> GetMeAsync(int workspaceId)
	{
		var workspace = await _repository.TryGetAsync(workspaceId);
		if (workspace == null)
		{
			return NotFound("Workspace not found.");
		}

		var user = await _workspaceUserRepository
			.Items
			.Where(wu => wu.WorkspaceId == workspaceId && wu.UserId == User.Claims.GetUserId())
			.SingleOrDefaultAsync();

		if (user is null)
			return NotFound("User in workspace not found.");

		return Ok(new
		{
			user.User.Id, user.WorkspaceId, user.Role, user.User.Email, user.User.FirstName, user.User.LastName
		});
	}
}
