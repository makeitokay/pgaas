namespace pgaas.Controllers.Dto.Sql;

public class CreateUserRequest
{
	public string Username { get; set; } = null!;
	public string Password { get; set; } = null!;
	public string Database { get; set; } = null!;
}