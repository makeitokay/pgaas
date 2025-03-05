﻿using System.Text.Json.Serialization;

namespace Core.Kubernetes.CustomResource;

public class CloudnativePgBackup : CustomResource<CloudnativePgBackupSpec>
{
	
}

public class CloudnativePgBackupList : CustomResourceList<CloudnativePgBackup>
{
	
}

public class CloudnativePgBackupSpec
{
	[JsonPropertyName("method")]
	public string Method { get; set; }
	
	[JsonPropertyName("cluster")]
	public CloudnativePgBackupSpecClusterSpec Cluster { get; set; }
}

public class CloudnativePgBackupSpecClusterSpec
{
	[JsonPropertyName("name")]
	public string Name { get; set; }
}