using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Repositories.WorkSeeds.Implements
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _sp;

        // 👇 key là (EntityType, KeyType)
        private readonly ConcurrentDictionary<(Type Entity, Type Key), object> _repos = new();
        private readonly ConcurrentDictionary<Type, object> _customRepos = new();

        public RepositoryFactory(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _sp = serviceProvider;
        }

        public IGenericRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
            where TEntity : class
        {
            var key = (typeof(TEntity), typeof(TKey));

            return (IGenericRepository<TEntity, TKey>)_repos.GetOrAdd(key, _ =>
            {
                // nếu có đăng ký open-generic trong DI, ưu tiên resolve
                var resolved = _sp.GetService<IGenericRepository<TEntity, TKey>>();
                return resolved ?? new GenericRepository<TEntity, TKey>(_context);
            });
        }

        public TRepository GetCustomRepository<TRepository>()
            where TRepository : class
        {
            return (TRepository)_customRepos.GetOrAdd(typeof(TRepository),
                _ => _sp.GetRequiredService<TRepository>());
        }
    }
}
