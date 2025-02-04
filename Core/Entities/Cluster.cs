﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

[Table("Clusters")]
public class Cluster : BaseEntity
{
	[Column("SystemName")]
	public string SystemName { get; set; }
	
	[Column("Status")]
	public ClusterStatus Status { get; set; }
	
	public virtual ClusterConfiguration Configuration { get; set; }

	public string ClusterNameInKubernetes => $"pg-{SystemName}";
}


public enum ClusterStatus
{
	Initialization,
	Starting,
	Restarting,
	Running,
	Deleting,
	Deleted
}