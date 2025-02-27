namespace pgaas.Controllers.Dto.Sql;

public class CreateDatabaseRequest
{
	public string Database { get; set; }
	public string Owner { get; set; }
	public string LcCollate { get; set; }
	public string LcCtype { get; set; }

}