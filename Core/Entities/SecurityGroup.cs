using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

public class SecurityGroup : BaseEntity
{
	[Column("Name")]
	public string Name { get; set; }
	
	[Column("AllowedIps")]
	public List<string> AllowedIps { get; set; }
	
	[Column("WorkspaceId")]
	public int WorkspaceId { get; set; }
	
	public Workspace Workspace { get; set; }
}