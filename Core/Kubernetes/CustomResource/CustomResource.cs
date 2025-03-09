using System.Text.Json.Serialization;
using k8s;
using k8s.Models;

namespace Core.Kubernetes.CustomResource;

public abstract class CustomResource : KubernetesObject, IMetadata<V1ObjectMeta>
{
	[JsonPropertyName("metadata")]
	public V1ObjectMeta Metadata { get; set; }
}

public abstract class CustomResource<TSpec> : CustomResource
{
	[JsonPropertyName("spec")]
	public TSpec Spec { get; set; }
}


public class CustomResourceList<TResource> : KubernetesObject
{
	[JsonPropertyName("metadata")]
	public V1ListMeta Metadata { get; set; }
	
	[JsonPropertyName("items")]
	public List<TResource> Items { get; set; } = [];
}