using Core.Entities;
using Core.Kubernetes.CustomResource;
using k8s;
using k8s.Models;

namespace Core;

public interface IKubernetesBackupManager
{
	Task CreateBackupAsync(Cluster cluster, string method);
	Task DeleteBackupAsync(Cluster cluster, string backupName);
	Task RecoveryFromBackupAsync(Cluster cluster, string backupName);
	Task<List<CloudnativePgBackup>> GetBackupsAsync(Cluster cluster);
}

public class KubernetesBackupManager(IKubernetes kubernetes) : IKubernetesBackupManager
{
	private const string BackupApiGroup = "postgresql.cnpg.io";
	private const string BackupApiVersion = "v1";
	private const string BackupPlural = "backups";
	
	public async Task CreateBackupAsync(Cluster cluster, string method)
    {
        var backup = new CloudnativePgBackup
        {
            ApiVersion = $"{BackupApiGroup}/{BackupApiVersion}",
            Kind = "Backup",
            Metadata = new V1ObjectMeta
            {
                Name = $"{cluster.SystemName}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                NamespaceProperty = cluster.SystemName
            },
            Spec = new CloudnativePgBackupSpec
            {
                Method = method,
                Cluster = new CloudnativePgBackupSpecClusterSpec
                {
	                Name = cluster.ClusterNameInKubernetes
                }
            }
        };

        using var client = CreateCnpgKubernetesBackupsClient();

        await client.CreateNamespacedAsync(backup, cluster.SystemName);
    }

    public async Task DeleteBackupAsync(Cluster cluster, string backupName)
    {
	    using var client = CreateCnpgKubernetesBackupsClient();

	    await client.DeleteNamespacedAsync<CloudnativePgBackup>(cluster.SystemName, backupName);
    }

    public async Task RecoveryFromBackupAsync(Cluster cluster, string backupName)
    {
	    throw new NotImplementedException();
    }

    public async Task<List<CloudnativePgBackup>> GetBackupsAsync(Cluster cluster)
    {
	    using var client = CreateCnpgKubernetesBackupsClient();

	    var backups = await client.ListNamespacedAsync<CloudnativePgBackupList>(cluster.SystemName);
	    return backups.Items;
    }
    
    private GenericClient CreateCnpgKubernetesBackupsClient() =>
	    new(kubernetes, BackupApiGroup, BackupApiVersion, BackupPlural);
}