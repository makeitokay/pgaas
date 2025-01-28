namespace pgaas.Controllers.Dto.Clusters;

public class CreateClusterDto
{
	public string SystemName { get; set; } = null!;
	public int StorageSize { get; set; }
	public int Cpu { get; set; }
	public int Memory { get; set; }
	public int MajorVersion { get; set; }
	public string DatabaseName { get; set; } = null!;
	public string LcCollate { get; set; } = null!;
	public string LcCtype { get; set; } = null!;
	public int Instances { get; set; }
	public string OwnerName { get; set; } = null!;
}