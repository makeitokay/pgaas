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
}