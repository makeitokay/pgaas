namespace pgaas.Controllers.Dto.Postgres;

public class ConfigurationReadinessDto
{
	public string Status { get; set; }
	public IEnumerable<string>? FailedParameters { get; set; }

	public static ConfigurationReadinessDto Waiting() => new() { Status = "waiting" };
	public static ConfigurationReadinessDto Failed(IEnumerable<string> parameters) => new()
	{
		FailedParameters = parameters,
		Status = "failed"
	};
	public static ConfigurationReadinessDto Success() => new() { Status = "success" };
}