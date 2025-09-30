using System.Linq.Expressions;
using System.Reflection;

namespace BusinessObjects.Common.Pagination
{
    internal static class OrderByHelper
    {
        public static IOrderedQueryable<T> OrderByProperty<T>(this IQueryable<T> source, string propertyName, bool desc)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var (member, memberType) = GetPropertyOrField(param, propertyName);
            var lambda = Expression.Lambda(member, param);

            var method = GetOrderByMethod(desc, typeof(T), memberType);
            return (IOrderedQueryable<T>)method.Invoke(null, new object[] { source, lambda })!;
        }

        public static IOrderedQueryable<T> ThenByProperty<T>(this IOrderedQueryable<T> source, string propertyName, bool desc)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var (member, memberType) = GetPropertyOrField(param, propertyName);
            var lambda = Expression.Lambda(member, param);

            var method = GetThenByMethod(desc, typeof(T), memberType);
            return (IOrderedQueryable<T>)method.Invoke(null, new object[] { source, lambda })!;
        }

        private static (MemberExpression member, Type type) GetPropertyOrField(ParameterExpression param, string name)
        {
            // Hỗ trợ dot-path: "User.Profile.FullName"
            Expression current = param;
            Type currentType = param.Type;

            foreach (var part in name.Split('.'))
            {
                var prop = currentType.GetProperty(part);
                if (prop != null)
                {
                    current = Expression.Property(current, prop);
                    currentType = prop.PropertyType;
                    continue;
                }

                var field = currentType.GetField(part);
                if (field != null)
                {
                    current = Expression.Field(current, field);
                    currentType = field.FieldType;
                    continue;
                }

                throw new ArgumentException($"Property/Field '{part}' not found on type '{currentType.Name}'.");
            }

            return ((MemberExpression)current, currentType);
        }

        private static MethodInfo GetOrderByMethod(bool desc, Type t, Type key)
        {
            var name = desc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
            return GetQueryableMethod(name, t, key);
        }

        private static MethodInfo GetThenByMethod(bool desc, Type t, Type key)
        {
            var name = desc ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            return typeof(Queryable).GetMethods()
                .Single(m => m.Name == name
                          && m.IsGenericMethodDefinition
                          && m.GetParameters().Length == 2)
                .MakeGenericMethod(t, key);
        }

        private static MethodInfo GetQueryableMethod(string name, Type t, Type key)
        {
            return typeof(Queryable).GetMethods()
                .Single(m => m.Name == name
                          && m.IsGenericMethodDefinition
                          && m.GetParameters().Length == 2)
                .MakeGenericMethod(t, key);
        }
    }
}
