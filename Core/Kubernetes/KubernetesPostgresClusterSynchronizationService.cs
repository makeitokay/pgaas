using Core.Entities;
using Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Core;

public interface IKubernetesPostgresClusterSynchronizationService
{
	Task SynchronizeAsync();
}

public class KubernetesPostgresClusterSynchronizationService(
	IKubernetesPostgresClusterManager clusterManager,
	IServiceProvider serviceProvider)
	: IKubernetesPostgresClusterSynchronizationService
{
	public async Task SynchronizeAsync()
	{
		using var scope = serviceProvider.CreateScope();
		var clusterRepository = scope.ServiceProvider.GetRequiredService<IRepository<Cluster>>();
		
		var initializationClusters = clusterRepository
			.Items
			.Where(c => c.Status == ClusterStatus.Initialization)
			.ToList();

		foreach (var cluster in initializationClusters)
		{
			await clusterManager.CreateClusterAsync(cluster);
			cluster.Status = ClusterStatus.Starting;
			await clusterRepository.UpdateAsync(cluster);
		}
	}
}