using Core.Entities;
using Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
	protected ApplicationDbContext DbContext { get; }

	protected DbSet<TEntity> Set { get; }

	public async Task DeleteAsync(TEntity entity)
	{
		Set.Remove(entity);
		await DbContext.SaveChangesAsync();
	}

	public async Task UpdateRangeAsync(IEnumerable<TEntity> entities)
	{
		Set.UpdateRange(entities);
		await DbContext.SaveChangesAsync();
	}

	public IQueryable<TEntity> Items => Set.AsQueryable();

	public Repository(ApplicationDbContext dbContext)
	{
		DbContext = dbContext;
		Set = dbContext.Set<TEntity>();
	}

	public async Task<TEntity> CreateAsync(TEntity entity)
	{
		Set.Add(entity);
		await DbContext.SaveChangesAsync();
		return entity;
	}

	public async Task<TEntity> UpdateAsync(TEntity entity)
	{
		Set.Update(entity);
		await DbContext.SaveChangesAsync();
		return entity;
	}

	public async Task<TEntity> GetAsync(int id)
	{
		return await Set.FindAsync(id) ?? throw new ArgumentException($"Entity with id = {id} not found");
	}
	
	public async Task<TEntity?> TryGetAsync(int id)
	{
		return await Set.FindAsync(id);
	}

}