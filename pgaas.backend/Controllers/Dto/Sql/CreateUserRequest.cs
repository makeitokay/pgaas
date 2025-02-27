namespace pgaas.Controllers.Dto.Sql;

public class CreateUserRequest
{
	public string Username { get; set; }
	public string Password { get; set; }
	public string Database { get; set; }
	public List<string> Roles { get; set; }
	public DateTime? ExpiryDate { get; set; }
}