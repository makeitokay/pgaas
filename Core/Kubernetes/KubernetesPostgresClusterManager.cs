using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Entities;
using Core.Kubernetes.CustomResource;
using k8s;
using k8s.Models;

namespace Core;

public interface IKubernetesPostgresClusterManager : IDisposable
{
	Task CreateClusterAsync(Cluster cluster);
	Task UpdateClusterAsync(Cluster cluster);
	Task DeleteClusterAsync(Cluster cluster);
	Task RestartClusterAsync(Cluster cluster);
	Task<CloudnativePgClusterStatus?> GetClusterStatusAsync(Cluster cluster);
	Task<IEnumerable<string>> GetClusterHostsAsync(Cluster cluster);
	Task<object> GetResourceUsageAsync(Cluster cluster);
}

public class KubernetesPostgresClusterManager : IKubernetesPostgresClusterManager
{
	private readonly IKubernetes _kubernetes;
	private readonly HttpClient _httpClient;
	private readonly GenericClient _cnpgClient;
	private readonly string _prometheusBaseUrl = "http://kube-prom-stack-kube-prome-prometheus.monitoring.svc.cluster.local:9090";

	public KubernetesPostgresClusterManager(IKubernetes kubernetes, HttpClient httpClient)
	{
		_kubernetes = kubernetes;
		_httpClient = httpClient;
		_cnpgClient = new GenericClient(kubernetes, "postgresql.cnpg.io", "v1", "clusters");
	}
	
	public async Task CreateClusterAsync(Cluster cluster)
	{
		if (!await IsNamespaceExistAsync(cluster.SystemName))
			await _kubernetes.CoreV1.CreateNamespaceAsync(new V1Namespace
			{
				Metadata = new V1ObjectMeta
				{
					Name = cluster.SystemName
				}
			});

		using var client = new GenericClient(_kubernetes, "helm.toolkit.fluxcd.io", "v2", "helmreleases");
		
		var helmRelease = CreateHelmRelease(cluster);
		await client.CreateNamespacedAsync(helmRelease, cluster.SystemName);
	}

	public async Task UpdateClusterAsync(Cluster cluster)
	{
		using var client = new GenericClient(_kubernetes, "helm.toolkit.fluxcd.io", "v2", "helmreleases");

		var existingHelmRelease = await client
			.ReadNamespacedAsync<FluxHelmRelease>(cluster.SystemName, cluster.SystemName);
		
		var helmRelease = CreateHelmRelease(cluster);
		helmRelease.Metadata.ResourceVersion = existingHelmRelease.Metadata.ResourceVersion;

		await client.ReplaceNamespacedAsync(helmRelease, cluster.SystemName, cluster.SystemName);
	}

	public async Task DeleteClusterAsync(Cluster cluster)
	{
		using var client = new GenericClient(_kubernetes, "helm.toolkit.fluxcd.io", "v2", "helmreleases");

		await client.DeleteNamespacedAsync<FluxHelmRelease>(cluster.SystemName, cluster.SystemName);
	}

	public async Task RestartClusterAsync(Cluster cluster)
	{
		var patchStr = $$"""
		                 {
		                     "metadata": {
		                         "annotations": {
		                             "kubectl.kubernetes.io/restartedAt": "{{DateTime.UtcNow:o}}"
		                         }
		                     }
		                 }
		                 """;
		await _cnpgClient.PatchNamespacedAsync<CloudnativePgCluster>(new V1Patch(patchStr, V1Patch.PatchType.MergePatch),
			cluster.SystemName, cluster.ClusterNameInKubernetes);
	}

	public async Task<CloudnativePgClusterStatus?> GetClusterStatusAsync(Cluster cluster)
	{
		var cloudnativePgCluster = await _cnpgClient
			.ReadNamespacedAsync<CloudnativePgCluster>(cluster.SystemName, cluster.ClusterNameInKubernetes);
		return cloudnativePgCluster?.Status;
	}

	public async Task<IEnumerable<string>> GetClusterHostsAsync(Cluster cluster)
	{
		var podList = await _kubernetes.CoreV1.ListNamespacedPodAsync(
			cluster.SystemName,
			labelSelector: $"cnpg.io/cluster={cluster.ClusterNameInKubernetes},cnpg.io/podRole=instance");
		return podList.Items.Select(pod => pod.Metadata.Name);
	}

	public async Task<object> GetResourceUsageAsync(Cluster cluster)
	{
		var pods = await GetClusterHostsAsync(cluster);
		var result = new List<object>();

		foreach (var pod in pods)
		{
			var ns = cluster.SystemName;

			var cpuQuery =
				$"sum(node_namespace_pod_container:container_cpu_usage_seconds_total:sum_irate{{namespace=\"{ns}\", pod=\"{pod}\"}}) / " +
				$"sum(kube_pod_container_resource_requests{{job=\"kube-state-metrics\", namespace=\"{ns}\", resource=\"cpu\", pod=\"{pod}\"}})";

			var memQuery =
				$"sum(container_memory_working_set_bytes{{job=\"kubelet\", metrics_path=\"/metrics/cadvisor\", namespace=\"{ns}\", pod=~\"{pod}\"}}) / " +
				$"sum(max by(pod) (kube_pod_container_resource_requests{{job=\"kube-state-metrics\", namespace=\"{ns}\", resource=\"memory\", pod=~\"{pod}\"}}))";

			var storageQuery =
				$"max(sum by(pod)(cnpg_pg_database_size_bytes{{namespace=\"{ns}\"}}) / " +
				$"sum by (pod) (label_replace(kube_persistentvolumeclaim_resource_requests_storage_bytes{{namespace=\"{ns}\"}},  \"pod\", \"$1\", \"persistentvolumeclaim\", \"(.*)\")))";

			var cpu = await QueryPrometheus(cpuQuery);
			var mem = await QueryPrometheus(memQuery);
			var storage = await QueryPrometheus(storageQuery);

			result.Add(new
			{
				Pod = pod,
				CpuUsage = cpu,
				MemoryUsage = mem,
				StorageUsage = storage
			});
		}

		return result;
	}
	
	private async Task<double?> QueryPrometheus(string query)
	{
		var url = $"{_prometheusBaseUrl}/api/v1/query?query={Uri.EscapeDataString(query)}";
		var response = await _httpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();

		var json = await response.Content.ReadFromJsonAsync<PrometheusQueryResult>();
		var value = Convert.ToDouble(json?.Data.Result.FirstOrDefault()?.Value.ElementAtOrDefault(1).GetString(),
			CultureInfo.InvariantCulture);
		return value;
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
					["dataDurability"] = configuration.DataDurability ?? "preferred",
					["syncReplicas"] = configuration.SyncReplicas ?? 1,
					["pooler"] = new Dictionary<string, object?>
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
					["backups"] = new Dictionary<string, object?>
					{
						["enabled"] = (configuration.BackupScheduleCronExpression is not null).ToString().ToLower(),
						["schedule"] = configuration.BackupScheduleCronExpression,
						["method"] = configuration.BackupMethod
					},
					["recoveryFromBackup"] = new Dictionary<string, object?>
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
			await _kubernetes.CoreV1.ReadNamespaceAsync(ns);
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

	private class PrometheusQueryResult
	{
		[JsonPropertyName("status")]
		public string Status { get; set; }

		[JsonPropertyName("data")]
		public PrometheusData Data { get; set; }
	}

	private class PrometheusData
	{
		[JsonPropertyName("result")]
		public List<PrometheusResult> Result { get; set; }
	}

	private class PrometheusResult
	{
		[JsonPropertyName("value")]
		public JsonElement[] Value { get; set; }
	}

	public void Dispose()
	{
		_cnpgClient.Dispose();
		_httpClient.Dispose();
	}
}