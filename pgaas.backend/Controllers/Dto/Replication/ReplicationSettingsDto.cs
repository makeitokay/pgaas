namespace pgaas.Controllers.Dto.Replication;

public class ReplicationSettingsDto
{
	public int SyncReplicas { get; set; }
	public string DataDurability { get; set; } = "preferred";
}