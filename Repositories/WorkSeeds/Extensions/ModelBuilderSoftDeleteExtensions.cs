using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

                    var entity = modelBuilder.Entity(et.ClrType);
                    var existingFilter = entity.Metadata.GetQueryFilter();

                    if (existingFilter is not null)
                    {
                        var mergedBody = Expression.AndAlso(
                            ParameterReplaceVisitor.Replace(existingFilter.Body, existingFilter.Parameters[0], param),
                            lambda.Body);
                        lambda = Expression.Lambda(mergedBody, param);
                    }

                    entity.HasQueryFilter(lambda);
                }
            }
        }

        private sealed class ParameterReplaceVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _target;
            private readonly Expression _replacement;

            private ParameterReplaceVisitor(ParameterExpression target, Expression replacement)
            {
                _target = target;
                _replacement = replacement;
            }

            public static Expression Replace(Expression source, ParameterExpression target, Expression replacement)
            {
                return new ParameterReplaceVisitor(target, replacement).Visit(source)!;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _target)
                {
                    return _replacement;
                }

                return base.VisitParameter(node);
            }
        }
    }

}
