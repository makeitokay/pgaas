using Core;

namespace pgaas.backend;

public class KubernetesPostgresClusterSynchronizationTask(
	IKubernetesPostgresClusterSynchronizationService synchronizationService) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			await synchronizationService.SynchronizeAsync();
			await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
		}

	}
}