using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Enums;
using Tickwork.Utilities.Interfaces;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Infrastructure
{
    /// <summary>
    /// In-memory ITickerQueryable implementation.
    /// Loads all entities via the provided loader, then applies filters/sort/pagination in-memory.
    /// Suitable for Redis, in-memory, or any provider that doesn't have a native query pipeline.
    /// </summary>
    public sealed class InMemoryTickerQueryable<TEntity> : ITickerQueryable<TEntity>
    {
        private readonly Func<CancellationToken, Task<List<TEntity>>> _loader;
        private readonly Func<IEnumerable<TEntity>, IEnumerable<TEntity>> _pipeline;

        /// <param name="loader">
        /// Async function that loads all candidate entities.
        /// Called once when a terminal method (ToArrayAsync, FirstOrDefaultAsync, etc.) is invoked.
        /// </param>
        public InMemoryTickerQueryable(Func<CancellationToken, Task<List<TEntity>>> loader)
            : this(loader, q => q)
        {
        }

        private InMemoryTickerQueryable(
            Func<CancellationToken, Task<List<TEntity>>> loader,
            Func<IEnumerable<TEntity>, IEnumerable<TEntity>> pipeline)
        {
            _loader = loader;
            _pipeline = pipeline;
        }

        private InMemoryTickerQueryable<TEntity> Append(Func<IEnumerable<TEntity>, IEnumerable<TEntity>> step)
        {
            var currentPipeline = _pipeline;
            return new InMemoryTickerQueryable<TEntity>(_loader, q => step(currentPipeline(q)));
        }

        public ITickerQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            var compiled = predicate.Compile();
            return Append(q => q.Where(compiled));
        }

        public ITickerQueryable<TEntity> WithRelated(params TickerRelation[] relations)
            => this; // In-memory: relations are already loaded by the loader

        public ITickerQueryable<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            var compiled = keySelector.Compile();
            return Append(q => q.OrderBy(compiled));
        }

        public ITickerQueryable<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            var compiled = keySelector.Compile();
            return Append(q => q.OrderByDescending(compiled));
        }

        public ITickerQueryable<TEntity> Skip(int count)
            => Append(q => q.Skip(count));

        public ITickerQueryable<TEntity> Take(int count)
            => Append(q => q.Take(count));

        public ITickerQueryable<TEntity> AsNoTracking()
            => this; // No-op for in-memory

        public async Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken = default)
        {
            var items = await _loader(cancellationToken).ConfigureAwait(false);
            return _pipeline(items).ToArray();
        }

        public async Task<TEntity> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            var items = await _loader(cancellationToken).ConfigureAwait(false);
            return _pipeline(items).FirstOrDefault();
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            var items = await _loader(cancellationToken).ConfigureAwait(false);
            return _pipeline(items).Count();
        }

        public async Task<PaginationResult<TEntity>> ToPaginatedAsync(
            int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 1000);

            var items = await _loader(cancellationToken).ConfigureAwait(false);
            var query = _pipeline(items);

            var count = query.Count();
            var page = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return new PaginationResult<TEntity>(page, count, pageNumber, pageSize);
        }
    }
}
