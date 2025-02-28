using System.Text.Json.Serialization;

namespace Core.Kubernetes.CustomResource;

public class CloudnativePgCluster : CustomResource<CloudnativePgClusterSpec>
{
	[JsonPropertyName("status")]
	public CloudnativePgClusterStatus Status { get; set; }
}

public class CloudnativePgClusterStatus
{
	public bool IsHealthy() => Phase == "Cluster in healthy state";
	
	public bool IsRestarting() => Phase is "Waiting for the instances to become active" 
		or "Primary instance is being restarted without a switchover";
	
	[JsonPropertyName("phase")]
	public string Phase { get; set; }
}

public class CloudnativePgClusterSpec
{
	[JsonPropertyName("storage")]
	public CloudnativePgClusterStorageSpec Storage { get; set; }
}

public class CloudnativePgClusterStorageSpec
{
	[JsonPropertyName("size")]
	public string Size { get; set; }
}