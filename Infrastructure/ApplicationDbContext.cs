using System.Text.Json;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure;

public class ApplicationDbContext : DbContext
{
	public DbSet<Cluster> Clusters => Set<Cluster>();
	
	public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			foreach (var property in entityType.GetProperties())
			{
				if (!property.ClrType.IsEnum) continue;
				var type = typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType);
				var converter = Activator.CreateInstance(type, new ConverterMappingHints()) as ValueConverter;

				property.SetValueConverter(converter);
			}
		}

		modelBuilder
			.Entity<Cluster>()
			.HasIndex(c => c.SystemName)
			.IsUnique();
		
		modelBuilder
			.Entity<User>()
			.HasIndex(c => c.Email)
			.IsUnique();
		
		modelBuilder.Entity<WorkspaceUser>()
			.HasOne(wu => wu.User)
			.WithMany(u => u.WorkspaceUsers)
			.HasForeignKey(wu => wu.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<WorkspaceUser>()
			.HasOne(wu => wu.Workspace)
			.WithMany(w => w.WorkspaceUsers)
			.HasForeignKey(wu => wu.WorkspaceId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<WorkspaceUser>()
			.HasIndex(wu => new { wu.UserId, wu.WorkspaceId })
			.IsUnique();

		modelBuilder.Entity<Workspace>()
			.HasIndex(w => w.Name)
			.IsUnique();
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder
			.UseLazyLoadingProxies();
	}
}