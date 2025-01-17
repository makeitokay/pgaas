using Core.Entities;
using Core.Kubernetes.CustomResource;
using k8s;
using k8s.Models;

namespace Core;

public interface IKubernetesPostgresClusterManager
{
	Task CreateClusterAsync(Cluster cluster);
	Task DeleteClusterAsync(Cluster cluster);
}

public class KubernetesPostgresClusterManager(IKubernetes kubernetes) : IKubernetesPostgresClusterManager
{
	public async Task CreateClusterAsync(Cluster cluster)
	{
		if (!await IsNamespaceExistAsync(cluster.SystemName))
			await kubernetes.CoreV1.CreateNamespaceAsync(new V1Namespace
			{
				Metadata = new V1ObjectMeta
				{
					Name = cluster.SystemName
				}
			});

		using var client = new GenericClient(kubernetes, "helm.toolkit.fluxcd.io", "v2", "helmreleases");
		
		var helmRelease = CreateHelmRelease(cluster);
		await client.CreateNamespacedAsync(helmRelease, cluster.SystemName);
	}

	public Task DeleteClusterAsync(Cluster cluster)
	{
		throw new NotImplementedException();
	}

	private FluxHelmRelease CreateHelmRelease(Cluster cluster)
	{
		var helmRelease = new FluxHelmRelease
		{
			Kind = "HelmRelease",
			ApiVersion = "helm.toolkit.fluxcd.io/v2",
			Metadata = new V1ObjectMeta { Name = cluster.SystemName, NamespaceProperty = cluster.SystemName },
			Spec = new FluxHelmReleaseSpec
			{
				Chart = new FluxHelmReleaseSpecChart
				{
					Spec = new FluxHelmReleaseSpecChartSpec
					{
						Chart = "pg-cluster",
						Version = "1.x.x",
						SourceRef = new FluxSourceRef
						{
							Kind = FluxSource.HelmRepository, Name = "pgaas", Namespace = "default"
						}
					}
				},
				Interval = "10m",
				Values = new Dictionary<string, object>
				{
					["name"] = cluster.SystemName
				}
			}
		};
		return helmRelease;
	}

	private async Task<bool> IsNamespaceExistAsync(string ns)
	{
		try
		{
			await kubernetes.CoreV1.ReadNamespaceAsync(ns);
		}
		catch (AggregateException e)
		{
			foreach (var innerEx in e.InnerExceptions)
			{
				if (innerEx is not k8s.Autorest.HttpOperationException exception) continue;
				if (exception.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
					return false;
			}
		}
		catch (k8s.Autorest.HttpOperationException e)
		{
			if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
				return false;
		}

		return true;
	}

}