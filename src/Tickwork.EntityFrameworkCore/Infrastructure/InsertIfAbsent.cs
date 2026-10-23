using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tickwork.EntityFrameworkCore.Infrastructure;

/// <summary>
/// The SQL dialect used to insert a row only when no row with the same unique key exists.
/// </summary>
internal enum InsertIfAbsentDialect
{
    /// <summary>No single-statement form is known for the provider: check, insert, and treat a lost race as absent-no-more.</summary>
    Portable,

    /// <summary><c>INSERT ... ON CONFLICT (key) DO NOTHING</c>.</summary>
    PostgreSql,

    /// <summary><c>INSERT ... ON CONFLICT (key) DO NOTHING</c>.</summary>
    Sqlite,

    /// <summary><c>INSERT ... SELECT ... WHERE NOT EXISTS (... WITH (UPDLOCK, HOLDLOCK))</c>.</summary>
    SqlServer,

    /// <summary><c>INSERT IGNORE ...</c>.</summary>
    MySql
}

/// <summary>
/// Inserts one entity unless a row with the same values in a unique key already exists, and
/// reports whether it inserted it. It is what <c>Upsert(entity).On(key).NoUpdate()</c> did: the
/// existing row is never changed, nothing is tracked, and a concurrent writer that inserts the
/// same key first makes this call return 0 rather than throw. Any other failure - a foreign key,
/// a different unique index, a lost connection - still throws.
/// </summary>
internal static class InsertIfAbsentExtensions
{
    /// <summary>
    /// Inserts <paramref name="entity"/> unless a row whose <paramref name="keyProperties"/> equal
    /// the entity's already exists. The key must be backed by a unique index.
    /// </summary>
    /// <returns>1 when the row was inserted, 0 when a row with the key already existed.</returns>
    internal static Task<int> InsertIfAbsentAsync<TEntity>(
        this DbContext context,
        TEntity entity,
        IReadOnlyList<string> keyProperties,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(keyProperties);
        if (keyProperties.Count == 0)
            throw new ArgumentException("At least one key property is required.", nameof(keyProperties));

        var dialect = DialectOf(context.Database.ProviderName);
        return dialect == InsertIfAbsentDialect.Portable
            ? InsertIfAbsentPortableAsync(context, entity, keyProperties, cancellationToken)
            : InsertIfAbsentInOneStatementAsync(context, entity, keyProperties, dialect, cancellationToken);
    }

    internal static InsertIfAbsentDialect DialectOf(string providerName) => providerName switch
    {
        "Npgsql.EntityFrameworkCore.PostgreSQL" => InsertIfAbsentDialect.PostgreSql,
        "Microsoft.EntityFrameworkCore.Sqlite" => InsertIfAbsentDialect.Sqlite,
        "Microsoft.EntityFrameworkCore.SqlServer" => InsertIfAbsentDialect.SqlServer,
        "Pomelo.EntityFrameworkCore.MySql" or "MySql.EntityFrameworkCore" or "MySql.Data.EntityFrameworkCore" => InsertIfAbsentDialect.MySql,
        _ => InsertIfAbsentDialect.Portable
    };

    /// <summary>
    /// Builds the one statement for <paramref name="dialect"/> from identifiers and parameter
    /// placeholders the provider has already delimited.
    /// </summary>
    internal static string BuildSql(
        InsertIfAbsentDialect dialect,
        string table,
        IReadOnlyList<(string Column, string Value)> values,
        IReadOnlyList<(string Column, string Value)> key)
    {
        var columns = string.Join(", ", values.Select(v => v.Column));
        var placeholders = string.Join(", ", values.Select(v => v.Value));

        switch (dialect)
        {
            case InsertIfAbsentDialect.PostgreSql:
            case InsertIfAbsentDialect.Sqlite:
                return $"INSERT INTO {table} ({columns}) VALUES ({placeholders}) ON CONFLICT ({string.Join(", ", key.Select(k => k.Column))}) DO NOTHING";

            case InsertIfAbsentDialect.MySql:
                return $"INSERT IGNORE INTO {table} ({columns}) VALUES ({placeholders})";

            case InsertIfAbsentDialect.SqlServer:
                // UPDLOCK + HOLDLOCK hold a key-range lock from the check to the insert, so a
                // concurrent statement for the same key waits and then finds the row.
                var match = string.Join(" AND ", key.Select(k => $"{k.Column} = {k.Value}"));
                return $"INSERT INTO {table} ({columns}) SELECT {placeholders} WHERE NOT EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE {match})";

            default:
                throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "The portable form is not one statement.");
        }
    }

    private static async Task<int> InsertIfAbsentInOneStatementAsync<TEntity>(
        DbContext context,
        TEntity entity,
        IReadOnlyList<string> keyProperties,
        InsertIfAbsentDialect dialect,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"'{typeof(TEntity).Name}' is not part of the model of '{context.GetType().Name}'.");
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"'{entityType.DisplayName()}' is not mapped to a table.");
        var schema = entityType.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(tableName, schema);
        var sql = context.GetService<ISqlGenerationHelper>();

        var values = new List<(string Column, string Value)>();
        var keyColumns = new (string Column, string Value)[keyProperties.Count];
        var parameters = new List<DbParameter>();

        using var factory = context.Database.GetDbConnection().CreateCommand();

        foreach (var property in entityType.GetProperties())
        {
            var column = property.GetColumnName(storeObject);
            if (column is null)
                continue;

            var keyIndex = IndexOf(keyProperties, property.Name);

            // A shadow property has no value on the entity: leave it to the column's default,
            // unless it is part of the key, which cannot be.
            var member = (MemberInfo)property.PropertyInfo ?? property.FieldInfo;
            if (member is null)
            {
                if (keyIndex >= 0)
                    throw new InvalidOperationException($"Key property '{property.Name}' has no CLR member.");
                continue;
            }

            var value = member is PropertyInfo pi ? pi.GetValue(entity) : ((FieldInfo)member).GetValue(entity);
            var name = "p" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var parameter = property.GetRelationalTypeMapping()
                .CreateParameter(factory, sql.GenerateParameterName(name), value, property.IsNullable);
            parameters.Add(parameter);

            var delimited = sql.DelimitIdentifier(column);
            var placeholder = sql.GenerateParameterNamePlaceholder(name);
            values.Add((delimited, placeholder));
            if (keyIndex >= 0)
                keyColumns[keyIndex] = (delimited, placeholder);
        }

        for (var i = 0; i < keyColumns.Length; i++)
        {
            if (keyColumns[i].Column is null)
                throw new InvalidOperationException($"Key property '{keyProperties[i]}' is not a column of '{tableName}'.");
        }

        var statement = BuildSql(dialect, sql.DelimitIdentifier(tableName, schema), values, keyColumns);

        // The statement carries its own placeholders; no {n} is left for EF to substitute.
        return await context.Database
            .ExecuteSqlRawAsync(statement, parameters, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// For a provider with no known single-statement form: look for the key, insert when absent,
    /// and when the insert fails because another writer inserted the key first, report 0.
    /// </summary>
    internal static async Task<int> InsertIfAbsentPortableAsync<TEntity>(
        DbContext context,
        TEntity entity,
        IReadOnlyList<string> keyProperties,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var exists = KeyPredicate(context, entity, keyProperties);
        var set = context.Set<TEntity>();

        if (await set.AsNoTracking().AnyAsync(exists, cancellationToken).ConfigureAwait(false))
            return 0;

        var entry = context.Entry(entity);
        entry.State = EntityState.Added;
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;
            if (await set.AsNoTracking().AnyAsync(exists, cancellationToken).ConfigureAwait(false))
                return 0;
            throw;
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    private static Expression<Func<TEntity, bool>> KeyPredicate<TEntity>(DbContext context, TEntity entity, IReadOnlyList<string> keyProperties)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"'{typeof(TEntity).Name}' is not part of the model of '{context.GetType().Name}'.");
        var row = Expression.Parameter(typeof(TEntity), "row");
        Expression body = null;

        foreach (var name in keyProperties)
        {
            var property = entityType.FindProperty(name)
                ?? throw new InvalidOperationException($"'{entityType.DisplayName()}' has no property '{name}'.");
            var member = property.PropertyInfo
                ?? throw new InvalidOperationException($"Key property '{name}' has no CLR property.");
            var value = member.GetValue(entity);
            var equal = Expression.Equal(
                Expression.Property(row, member),
                Expression.Constant(value, member.PropertyType));
            body = body is null ? equal : Expression.AndAlso(body, equal);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body, row);
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}
