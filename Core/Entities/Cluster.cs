using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

[Table("Clusters")]
public class Cluster : BaseEntity
{
	[Column("SystemName")]
	public string SystemName { get; set; }
}