﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities;

[Table("Clusters")]
public class Cluster : BaseEntity
{
	[Column("SystemName")]
	public string SystemName { get; set; }
	
	[Column("Status")]
	public ClusterStatus Status { get; set; }
	
	public virtual Workspace Workspace { get; set; }
	
	[Column("WorkspaceId")]
	public int WorkspaceId { get; set; }
	
	[Column("SecurityGroupId")]
	public int? SecurityGroupId { get; set; }
	
	public virtual SecurityGroup? SecurityGroup { get; set; }
	
	public virtual ClusterConfiguration Configuration { get; set; }

	[Column("ClusterNameInKubernetes")]
	public string ClusterNameInKubernetes { get; set; }
	
	[Column("RecoveryFromBackup")]
	public bool RecoveryFromBackup { get; set; }
}


public enum ClusterStatus
{
	Starting,
	Restarting,
	Running,
	Deleted
}