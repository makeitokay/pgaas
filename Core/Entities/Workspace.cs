using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class Workspace : BaseEntity
{
	[Column("Name")]
	public string Name { get; set; }
	
	public ICollection<WorkspaceUser> WorkspaceUsers { get; set; } = new List<WorkspaceUser>();
}