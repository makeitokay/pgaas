namespace pgaas.Controllers.Dto.Sql;

public class CreateDatabaseRequest
{
	public string Database { get; set; } = null!;
	public string Owner { get; set; } = null!;
}