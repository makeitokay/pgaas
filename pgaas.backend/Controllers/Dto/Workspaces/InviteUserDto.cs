using Core.Entities;

namespace pgaas.Controllers.Dto.Workspaces;

public class InviteUserDto
{
	public string Email { get; set; }
	public Role Role { get; set; }
}