using Core.Entities;

namespace Core.Repositories;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
	Task<TEntity> CreateAsync(TEntity entity);
	Task<TEntity> UpdateAsync(TEntity entity);
	Task<TEntity> GetAsync(int id);
	Task<TEntity?> TryGetAsync(int id);
	Task DeleteAsync(TEntity entity);
	Task UpdateRangeAsync(IEnumerable<TEntity> entities);
	IQueryable<TEntity> Items { get; }
}