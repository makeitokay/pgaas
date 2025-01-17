using System.Text.Json.Serialization;

namespace Core.Kubernetes.CustomResource;

public class FluxHelmRelease : CustomResource<FluxHelmReleaseSpec>
{
	
}

public class FluxHelmReleaseSpec
{
	[JsonPropertyName("chart")]
	public FluxHelmReleaseSpecChart Chart { get; set; }
	
	[JsonPropertyName("interval")]
	public string Interval { get; set; }
	
	[JsonPropertyName("values")]
	public Dictionary<string, object> Values { get; set; }
}

public class FluxHelmReleaseSpecChart
{
	[JsonPropertyName("spec")]
	public FluxHelmReleaseSpecChartSpec Spec { get; set; }
}

public class FluxHelmReleaseSpecChartSpec
{
	[JsonPropertyName("chart")]
	public string Chart { get; set; }
	
	[JsonPropertyName("version")]
	public string Version { get; set; }
	
	[JsonPropertyName("sourceRef")]
	public FluxSourceRef SourceRef { get; set; }
}

public class FluxSourceRef
{
	[JsonPropertyName("kind")]
	public FluxSource Kind { get; set; }
	
	[JsonPropertyName("name")]
	public string Name { get; set; }
	
	[JsonPropertyName("namespace")]
	public string Namespace { get; set; }
}

public enum FluxSource
{
	GitRepository,
	OCIRepository,
	HelmRepository,
	Bucket
}