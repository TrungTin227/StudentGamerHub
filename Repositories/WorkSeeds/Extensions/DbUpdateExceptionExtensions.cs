using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;

namespace Repositories.WorkSeeds.Extensions
{
    public static class DbUpdateExceptionExtensions
    {
        private const string UniqueViolationSqlState = "23505";

        public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
        {
            if (exception is null)
            {
                return false;
            }

            if (exception.InnerException is not DbException dbException)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(dbException.SqlState) && dbException.SqlState == UniqueViolationSqlState)
            {
                return true;
            }

            var sqlStateProperty = dbException.GetType().GetProperty("SqlState");
            if (sqlStateProperty?.GetValue(dbException) is string sqlState && sqlState == UniqueViolationSqlState)
            {
                return true;
            }

            var numberProperty = dbException.GetType().GetProperty("Number");
            if (numberProperty?.GetValue(dbException) is int number && (number == 2601 || number == 2627))
            {
                return true;
            }

            var constraintNameProperty = dbException.GetType().GetProperty("ConstraintName");
            if (constraintNameProperty?.GetValue(dbException) is string constraintName
                && !string.IsNullOrWhiteSpace(constraintName)
                && constraintName.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var message = dbException.Message;
            return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("violation of unique", StringComparison.OrdinalIgnoreCase);
        }
    }
}
