using Core;
using Core.Kubernetes;
using Core.Repositories;
using Infrastructure;
using Infrastructure.Repositories;
using k8s;
using Microsoft.EntityFrameworkCore;
using pgaas.backend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddSingleton<IKubernetes>(sp =>
{
	var config = builder.Environment.IsDevelopment()
		? KubernetesClientConfiguration.BuildConfigFromConfigFile()
		: KubernetesClientConfiguration.InClusterConfig();
	return new Kubernetes(config);
});

builder.Services.AddSingleton<IKubernetesPostgresClusterManager, KubernetesPostgresClusterManager>();
builder.Services
	.AddSingleton<IKubernetesPostgresClusterSynchronizationService, KubernetesPostgresClusterSynchronizationService>();

builder.Services.AddHostedService<KubernetesPostgresClusterSynchronizationTask>();

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

app.UseAuthorization();

app.MapControllers();

app.MapGet("/hello", async (IKubernetesPostgresClusterManager manager, ApplicationDbContext dbContext) =>
{
	var cluster = await dbContext.Clusters.FirstAsync();
	await manager.CreateClusterAsync(cluster);
});

app.Run();