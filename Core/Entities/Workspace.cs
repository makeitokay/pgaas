using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class Workspace : BaseEntity
{
	[Column("Name")]
	public string Name { get; set; }
	
	public virtual ICollection<WorkspaceUser> WorkspaceUsers { get; set; } = new List<WorkspaceUser>();
	
	public virtual User Owner { get; set; }
	
	[Column("OwnerId")]
	public int OwnerId { get; set; }
}