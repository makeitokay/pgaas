﻿using Core;

namespace pgaas.backend;

public class KubernetesPostgresClusterSynchronizationTask(
	IKubernetesPostgresClusterSynchronizationService synchronizationService) : BackgroundTask
{
	protected override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
	{
		await synchronizationService.SynchronizeAsync();
	}
}