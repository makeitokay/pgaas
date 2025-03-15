using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class WorkspaceUser : BaseEntity
{
	public int UserId { get; set; }

	public int WorkspaceId { get; set; }

	[Column("Role")]
	public Role Role { get; set; }

	public virtual User User { get; set; } = null!;
	public virtual Workspace Workspace { get; set; } = null!;
}

public enum Role
{
	Viewer,
	Editor,
	Admin
}