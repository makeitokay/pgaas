using Core;
using Core.Repositories;
using FluentValidation;
using Infrastructure;
using Infrastructure.Repositories;
using k8s;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using pgaas.backend;
using pgaas.Controllers.Dto.Clusters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
	options.AddPolicy("CORSPolicy", b =>
	{
		b
			.AllowAnyOrigin()
			.AllowAnyMethod()
			.AllowAnyHeader();
	});
});

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = Constants.Authentication.Issuer,
			ValidateAudience = true,
			ValidAudience = Constants.Authentication.Audience,
			ValidateLifetime = true,
			IssuerSigningKey = new SymmetricSecurityKey("PostgreSQLAsAServiceInKubernetesUltraSecretKey2024"u8.ToArray()),
			ValidateIssuerSigningKey = true
		};
	});
builder.Services
	.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IKubernetes>(sp =>
{
	var config = builder.Environment.IsDevelopment()
		? KubernetesClientConfiguration.BuildConfigFromConfigFile()
		: KubernetesClientConfiguration.InClusterConfig();
	return new Kubernetes(config);
});

builder.Services.AddScoped<IKubernetesPostgresClusterManager, KubernetesPostgresClusterManager>();
builder.Services
	.AddSingleton<IKubernetesPostgresClusterSynchronizationService, KubernetesPostgresClusterSynchronizationService>();

builder.Services.AddHostedService<KubernetesPostgresClusterSynchronizationTask>();
builder.Services.AddSingleton<IPostgresSqlManager, PostgresSqlManager>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateClusterDto>();

builder.Services.Configure<List<PostgresConfigurationParameter>>(
	builder.Configuration.GetSection("PostgresConfiguration"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var services = scope.ServiceProvider;
	var context = services.GetRequiredService<ApplicationDbContext>();
	context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/hello", async (IKubernetesPostgresClusterManager manager, ApplicationDbContext dbContext) =>
{
	var cluster = await dbContext.Clusters.FirstAsync();
	await manager.RestartClusterAsync(cluster);
});

app.Run();