using Core.Entities;
using Core.Kubernetes.CustomResource;
using k8s;
using k8s.Models;

namespace Core;

public interface IKubernetesPostgresClusterManager
{
	Task CreateClusterAsync(Cluster cluster);
	Task UpdateClusterAsync(Cluster cluster);
	Task DeleteClusterAsync(Cluster cluster);
	Task RestartClusterAsync(Cluster cluster);
	Task<CloudnativePgClusterStatus?> GetClusterStatusAsync(Cluster cluster);
	Task<IEnumerable<string>> GetClusterHostsAsync(Cluster cluster);
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

	public async Task UpdateClusterAsync(Cluster cluster)
	{
		using var client = new GenericClient(kubernetes, "helm.toolkit.fluxcd.io", "v2", "helmreleases");

		var existingHelmRelease = await client
			.ReadNamespacedAsync<FluxHelmRelease>(cluster.SystemName, cluster.SystemName);
		
		var helmRelease = CreateHelmRelease(cluster);
		helmRelease.Metadata.ResourceVersion = existingHelmRelease.Metadata.ResourceVersion;

		await client.ReplaceNamespacedAsync(helmRelease, cluster.SystemName, cluster.ClusterNameInKubernetes);
	}

	public Task DeleteClusterAsync(Cluster cluster)
	{
		throw new NotImplementedException();
	}

	public async Task RestartClusterAsync(Cluster cluster)
	{
		using var client = CreateCnpgKubernetesClient();
		
		var patchStr = $$"""
		                 {
		                     "metadata": {
		                         "annotations": {
		                             "kubectl.kubernetes.io/restartedAt": "{{DateTime.UtcNow:o}}"
		                         }
		                     }
		                 }
		                 """;
		await client.PatchNamespacedAsync<CloudnativePgCluster>(new V1Patch(patchStr, V1Patch.PatchType.MergePatch),
			cluster.SystemName, cluster.ClusterNameInKubernetes);
	}

	public async Task<CloudnativePgClusterStatus?> GetClusterStatusAsync(Cluster cluster)
	{
		using var client = CreateCnpgKubernetesClient();
		var cloudnativePgCluster = await client
			.ReadNamespacedAsync<CloudnativePgCluster>(cluster.SystemName, cluster.ClusterNameInKubernetes);
		return cloudnativePgCluster?.Status;
	}

	public async Task<IEnumerable<string>> GetClusterHostsAsync(Cluster cluster)
	{
		var podList = await kubernetes.CoreV1.ListNamespacedPodAsync(
			cluster.SystemName,
			labelSelector: $"cnpg.io/cluster={cluster.ClusterNameInKubernetes}");
		return podList.Items.Select(pod => pod.Metadata.Name);
	}

	private FluxHelmRelease CreateHelmRelease(Cluster cluster)
	{
		var configuration = cluster.Configuration;
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
				Values = new Dictionary<string, object?>
				{
					["name"] = cluster.ClusterNameInKubernetes,
					["systemName"] = cluster.SystemName,
					["majorVersion"] = configuration.MajorVersion,
					["instances"] = configuration.Instances,
					["storageSize"] = $"{configuration.StorageSize}Gi",
					["memory"] = $"{configuration.Memory}Mi",
					["cpu"] = $"{configuration.Cpu}m",
					["databaseName"] = configuration.DatabaseName,
					["lcCollate"] = configuration.LcCollate,
					["lcCtype"] = configuration.LcCtype,
					["ownerName"] = configuration.OwnerName,
					["ownerPassword"] = configuration.OwnerPassword,
					["postgresqlParameters"] = configuration.Parameters,
					["pooler"] = new Dictionary<string, string?>
					{
						["enabled"] = (configuration.PoolerMode is not null).ToString().ToLower(),
						["poolMode"] = configuration.PoolerMode,
						["maxClientConnections"] = configuration.PoolerMaxConnections?.ToString(),
						["defaultPoolSize"] = configuration.PoolerDefaultPoolSize?.ToString()
					},
					["sg"] = new Dictionary<string, object?>
					{
						["enabled"] = (cluster.SecurityGroupId is not null).ToString().ToLower(),
						["ips"] = cluster.SecurityGroup?.AllowedIps
					},
					["backups"] = new Dictionary<string, string?>
					{
						["enabled"] = (configuration.BackupScheduleCronExpression is not null).ToString().ToLower(),
						["schedule"] = configuration.BackupScheduleCronExpression,
						["method"] = configuration.BackupMethod
					},
					["recoveryFromBackup"] = new Dictionary<string, string?>
					{
						["enabled"] = cluster.RecoveryFromBackup.ToString().ToLower(),
						["backupName"] = cluster.ClusterNameInKubernetes
					}
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

	private GenericClient CreateCnpgKubernetesClient() => new(kubernetes, "postgresql.cnpg.io", "v1", "clusters");
}