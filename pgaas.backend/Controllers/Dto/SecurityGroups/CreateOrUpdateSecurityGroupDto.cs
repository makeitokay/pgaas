namespace pgaas.Controllers.Dto.SecurityGroups;

public class CreateOrUpdateSecurityGroupDto
{
	public int? Id { get; set; }
	public string Name { get; set; }
	public List<string> AllowedIps { get; set; }
}