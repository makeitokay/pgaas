using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Core.Entities;
using Core.Repositories;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using pgaas.backend;

namespace pgaas.Controllers;

[Route("auth")]
public class AuthorizationController(
	IRepository<User> userRepository,
	IPasswordManager passwordManager)
	: ControllerBase
{
	[HttpPost("login")]
	public async Task<ActionResult<LoginResponseDto>> LoginAsync([FromBody] LoginDto loginDto)
	{
		var user = await TryGetUserByEmail(loginDto.Email);

		if (user is null)
		{
			return Unauthorized("Неверный логин или пароль");
		}
		
		if (!passwordManager.VerifyPassword(loginDto.Password, user.PasswordHash, user.PasswordSalt))
		{
			return Unauthorized("Неверный логин или пароль");
		}

		return new LoginResponseDto { AccessToken = GetJwtForUser(user) };
	}

	[HttpPost("signup")]
	public async Task<ActionResult<LoginResponseDto>> SignupAsync([FromBody] SignupDto userDto)
	{
		var user = await TryGetUserByEmail(userDto.Email);

		if (user is not null)
			return BadRequest("Пользователь с таким Email уже существует!");

		var passwordHash = passwordManager.GetPasswordHash(userDto.Password, out var salt);

		user = new User
		{
			Email = userDto.Email,
			FirstName = userDto.FirstName,
			LastName = userDto.LastName,
			PasswordHash = passwordHash,
			PasswordSalt = salt
		};

		await userRepository.CreateAsync(user);

		return new LoginResponseDto { AccessToken = GetJwtForUser(user) };
	}

	private static string GetJwtForUser(User user)
	{
		var userClaims = new List<Claim>
		{
			new(ClaimTypes.Email, user.Email)
		};

		var jwt = new JwtSecurityToken(
			issuer: Constants.Authentication.Issuer,
			audience: Constants.Authentication.Audience,
			claims: userClaims,
			expires: DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
			signingCredentials: new SigningCredentials(
				new SymmetricSecurityKey("PostgreSQLAsAServiceInKubernetesUltraSecretKey2024"u8.ToArray()),
				SecurityAlgorithms.HmacSha256));

		return new JwtSecurityTokenHandler().WriteToken(jwt);
	}
	
	private async Task<User?> TryGetUserByEmail(string email) =>
		await userRepository.Items.SingleOrDefaultAsync(u => u.Email == email);
}