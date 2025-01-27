using System.Security.Cryptography;
using System.Text;

namespace Infrastructure;

public interface IPasswordManager
{
	string GetPasswordHash(string password, out byte[] salt);
	bool VerifyPassword(string password, string passwordHash, byte[] salt);
}

public class PasswordManager : IPasswordManager
{
	private const int HashingIterationsCount = 350000;
	private static readonly HashAlgorithmName _hashingAlgorithm = HashAlgorithmName.SHA512;
	private const int HashingKeySize = 64;

	public string GetPasswordHash(string password, out byte[] salt)
	{
		salt = RandomNumberGenerator.GetBytes(16);

		var hash = Rfc2898DeriveBytes.Pbkdf2(
			Encoding.UTF8.GetBytes(password),
			salt,
			HashingIterationsCount,
			_hashingAlgorithm,
			HashingKeySize);

		return Convert.ToHexString(hash);
	}

	public bool VerifyPassword(string password, string passwordHash, byte[] salt)
	{
		var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			HashingIterationsCount,
			_hashingAlgorithm,
			HashingKeySize);

		return hashToCompare.SequenceEqual(Convert.FromHexString(passwordHash));
	}
}