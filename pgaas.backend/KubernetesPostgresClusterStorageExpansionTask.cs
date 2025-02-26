using Core;

namespace pgaas.backend;

public class KubernetesPostgresClusterStorageExpansionTask(
	IKubernetesPostgresClusterSynchronizationService synchronizationService) : BackgroundTask
{
	protected override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
	{
		await synchronizationService.ExpandStorageAsync();
	}
}