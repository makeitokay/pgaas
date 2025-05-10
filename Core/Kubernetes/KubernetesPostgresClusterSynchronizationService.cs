using Core.Entities;
using Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Core;

public interface IKubernetesPostgresClusterSynchronizationService
{
	Task SynchronizeAsync();
}

public class KubernetesPostgresClusterSynchronizationService(IServiceProvider serviceProvider)
	: IKubernetesPostgresClusterSynchronizationService
{
	public async Task SynchronizeAsync()
	{
		using var scope = serviceProvider.CreateScope();
		var clusterRepository = scope.ServiceProvider.GetRequiredService<IRepository<Cluster>>();
		var clusterManager = scope.ServiceProvider.GetRequiredService<IKubernetesPostgresClusterManager>();

		await ProcessStartingClustersAsync(clusterRepository, clusterManager);
		await ProcessRunningClustersAsync(clusterRepository, clusterManager);
	}

	private async Task ProcessStartingClustersAsync(IRepository<Cluster> clusterRepository, IKubernetesPostgresClusterManager clusterManager)
	{
		var startingClusters = clusterRepository
			.Items
			.Where(c =>
				c.Status == ClusterStatus.Starting
				|| c.Status == ClusterStatus.Restarting)
			.ToList();

		foreach (var cluster in startingClusters)
		{
			var status = await clusterManager.GetClusterStatusAsync(cluster);
			if (status is null || !status.IsHealthy()) continue;
			
			cluster.Status = ClusterStatus.Running;
			await clusterRepository.UpdateAsync(cluster);
		}
	}
	
	private async Task ProcessRunningClustersAsync(IRepository<Cluster> clusterRepository, IKubernetesPostgresClusterManager clusterManager)
	{
		var clusters = clusterRepository
			.Items
			.Where(c => c.Status == ClusterStatus.Running)
			.ToList();

		foreach (var cluster in clusters)
		{
			var status = await clusterManager.GetClusterStatusAsync(cluster);
			if (status is null || status.IsHealthy()) continue;
			
			cluster.Status = ClusterStatus.Restarting;
			await clusterRepository.UpdateAsync(cluster);
		}
	}
}