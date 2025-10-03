using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Repositories.WorkSeeds.Extensions
{
    public static class ModelBuilderSoftDeleteExtensions
    {
        public static void ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
        {
            foreach (var et in modelBuilder.Model.GetEntityTypes())
            {
                // Nếu entity đó implement ISoftDelete
                if (typeof(ISoftDelete).IsAssignableFrom(et.ClrType))
                {
                    // e => EF.Property<bool>(e, "IsDeleted") == false
                    var param = Expression.Parameter(et.ClrType, "e");
                    var prop = Expression.Call(
                        typeof(EF), nameof(EF.Property), new[] { typeof(bool) },
                        param, Expression.Constant(nameof(ISoftDelete.IsDeleted)));
                    var body = Expression.Equal(prop, Expression.Constant(false));
                    var lambda = Expression.Lambda(body, param);

                    modelBuilder.Entity(et.ClrType).HasQueryFilter(lambda);
                }
            }
        }
    }

}
