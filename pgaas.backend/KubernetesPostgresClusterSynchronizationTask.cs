using Core;

namespace pgaas.backend;

public class KubernetesPostgresClusterSynchronizationTask(
	IKubernetesPostgresClusterSynchronizationService synchronizationService) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await synchronizationService.SynchronizeAsync();
			}
			catch (Exception e)
			{
				Console.WriteLine("Unable to synchronize clusters: {0}", e);
			}
			await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
		}

	}
}