namespace pgaas.backend;

public abstract class BackgroundTask : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ExecuteCoreAsync(stoppingToken);
			}
			catch (Exception e)
			{
				Console.WriteLine("Unable to execute task iteration: {0}", e);
			}
			await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
		}
	}
	
	protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);
}