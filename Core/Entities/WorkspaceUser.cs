using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class WorkspaceUser : BaseEntity
{
	public int UserId { get; set; }

	public int WorkspaceId { get; set; }

	[Column("Role")]
	public Role Role { get; set; }

	public User User { get; set; } = null!;
	public Workspace Workspace { get; set; } = null!;
}

public enum Role
{
	Viewer,
	Editor,
	Admin
}