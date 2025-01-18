using System.Text.Json.Serialization;

namespace Core.Kubernetes.CustomResource;

public class CloudnativePgCluster : CustomResource
{
	[JsonPropertyName("status")]
	public CloudnativePgClusterStatus Status { get; set; }
}

public class CloudnativePgClusterStatus
{
	public bool IsHealthy() => Phase == "Cluster in healthy state";
	
	[JsonPropertyName("phase")]
	public string Phase { get; set; }
}