using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

[Table("Users")]
public class User : BaseEntity
{
	[Column("FirstName")]
	public string FirstName { get; set; }
	
	[Column("LastName")]
	public string LastName { get; set; }

	[Column("Email")]
	public string Email { get; set; }

	[Column("PasswordHash")]
	public string PasswordHash { get; set; }

	[Column("PasswordSalt")]
	public byte[] PasswordSalt { get; set; }
	
	public ICollection<WorkspaceUser> WorkspaceUsers { get; set; } = new List<WorkspaceUser>();
}