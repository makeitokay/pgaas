using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

[Table("ClusterConfigurations")]
public class ClusterConfiguration : BaseEntity
{
	[Column("ClusterId")]
	public int ClusterId { get; set; }
	
	public virtual Cluster Cluster { get; set; }
	
	[Column("StorageSize")]
	public int StorageSize { get; set; }
	
	[Column("Cpu")]
	public int Cpu { get; set; }
	
	[Column("Memory")]
	public int Memory { get; set; }
	
	[Column("MajorVersion")]
	public int MajorVersion { get; set; }
	
	[Column("DatabaseName")]
	public string DatabaseName { get; set; }
	
	[Column("LcCollate")]
	public string LcCollate { get; set; }
	
	[Column("LcCtype")]
	public string LcCtype { get; set; }
	
	[Column("Instances")]
	public int Instances { get; set; }
	
	[Column("OwnerName")]
	public string OwnerName { get; set; }
	
	[Column("OwnerName")]
	public string OwnerPassword { get; set; }
	
	[Column("Parameters")]
	public Dictionary<string, string?>? Parameters { get; set; }
	
	[Column("PoolerMode")]
	public string? PoolerMode { get; set; }
	
	[Column("PoolerMaxConnections")]
	public int? PoolerMaxConnections { get; set; }
	
	[Column("PoolerDefaultPoolSize")]
	public int? PoolerDefaultPoolSize { get; set; }
	
	[Column("BackupScheduleCronExpression")]
	public string? BackupScheduleCronExpression { get; set; }
	
	[Column("BackupMethod")]
	public string? BackupMethod { get; set; }
}