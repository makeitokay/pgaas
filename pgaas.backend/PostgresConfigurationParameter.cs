namespace pgaas.backend;

public record PostgresConfigurationParameter
{
	public string Name { get; set; }
	public string Type { get; set; }
	public List<string>? Options { get; set; }
	public string? Value { get; set; }
}