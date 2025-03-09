namespace pgaas.Controllers.Dto.Backups;

public class BackupScheduleRequest
{
    public string CronExpression { get; set; }
    
    public string Method { get; set; }
}