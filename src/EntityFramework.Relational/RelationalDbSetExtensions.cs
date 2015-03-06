// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Query;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;

// ReSharper disable once CheckNamespace

namespace Microsoft.Data.Entity
{
    public static class RelationalDbSetExtensions
    {
        public static IQueryable<TEntity> FromSql<TEntity>([NotNull]this DbSet<TEntity> dbSet, [NotNull]string query) where TEntity : class
        {
            Check.NotNull(dbSet, nameof(dbSet));
            Check.NotNull(query, nameof(query));

            if (dbSet.Context.Database as RelationalDatabase == null)
            {
                throw new InvalidOperationException(Strings.RelationalNotInUse);
            }

            var queryProvider = ((IAccessor<IServiceProvider>)dbSet).Service.GetRequiredService<EntityQueryProvider>();

            var queryable = new EntityQueryable<TEntity>(queryProvider);
            queryable.AddAnnotation("Sql", query);

            return queryProvider.CreateQuery<TEntity>(Expression.Constant(queryable));
        }
    }
}
