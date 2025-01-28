namespace Infrastructure;

public class Constants
{
	public static class Authentication
	{
		public const string Issuer = "pgaas";
		public const string Audience = "pgaas.client";
	}

	public static class ClaimTypes
	{
		public const string UserId = "UserId";
	} 
}