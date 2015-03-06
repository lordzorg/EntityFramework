// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query;
using Microsoft.Data.Entity.Query.ExpressionTreeVisitors;
using Microsoft.Data.Entity.Relational.Query.Expressions;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Relational.Query.ExpressionTreeVisitors
{
    public class RelationalEntityQueryableExpressionTreeVisitor : EntityQueryableExpressionTreeVisitor
    {
        private static readonly ParameterExpression _readerParameter
            = Expression.Parameter(typeof(DbDataReader));

        private readonly IQuerySource _querySource;

        public RelationalEntityQueryableExpressionTreeVisitor(
            [NotNull] RelationalQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource)
            : base(Check.NotNull(queryModelVisitor, nameof(queryModelVisitor)))
        {
            Check.NotNull(querySource, nameof(querySource));

            _querySource = querySource;
        }

        private new RelationalQueryModelVisitor QueryModelVisitor => (RelationalQueryModelVisitor)base.QueryModelVisitor;

        protected override Expression VisitMemberExpression([NotNull] MemberExpression memberExpression)
        {
            Check.NotNull(memberExpression, nameof(memberExpression));

            QueryModelVisitor
                .BindMemberExpression(
                    memberExpression,
                    (property, querySource, selectExpression)
                        => selectExpression.AddToProjection(
                            QueryModelVisitor.QueryCompilationContext
                                .GetColumnName(property),
                            property,
                            querySource));

            return base.VisitMemberExpression(memberExpression);
        }

        protected override Expression VisitMethodCallExpression([NotNull] MethodCallExpression methodCallExpression)
        {
            Check.NotNull(methodCallExpression, nameof(methodCallExpression));

            QueryModelVisitor
                .BindMethodCallExpression(
                    methodCallExpression,
                    (property, querySource, selectExpression)
                        => selectExpression.AddToProjection(
                            QueryModelVisitor.QueryCompilationContext
                                .GetColumnName(property),
                            property,
                            querySource));

            return base.VisitMethodCallExpression(methodCallExpression);
        }

        protected override Expression VisitConstantExpression(ConstantExpression constantExpression)
        {
            Check.NotNull(constantExpression, nameof(constantExpression));

            if (constantExpression.Type.GetTypeInfo().IsGenericType
                && constantExpression.Type.GetGenericTypeDefinition() == typeof(EntityQueryable<>))
            {
                var sql = ((IMetadata)constantExpression.Value)["Sql"];

                if (sql != null)
                {
                    return VisitRawSqlQueryable(((IQueryable)constantExpression.Value).ElementType, sql);
                }
            }

            return base.VisitConstantExpression(constantExpression);
        }

        protected override Expression VisitEntityQueryable(Type elementType)
        {
            Check.NotNull(elementType, nameof(elementType));

            return VisitSelectExpression(elementType,
                (entityType, tableName, alias) =>
                    new TableExpression(
                        tableName,
                        QueryModelVisitor.QueryCompilationContext.GetSchema(entityType),
                        alias,
                        _querySource));
        }

        protected virtual Expression VisitRawSqlQueryable(Type elementType, string sql)
        {
            Check.NotNull(elementType, nameof(elementType));
            Check.NotNull(sql, nameof(sql));

            return VisitSelectExpression(elementType,
                (entityType, tableName, alias) =>
                    new RawSqlDerivedTableExpression(
                        sql,
                        alias,
                        _querySource));
        }

        private Expression VisitSelectExpression(
            Type elementType,
            Func<IEntityType, string, string, TableExpressionBase> createTableExpression)
        {
            var queryMethodInfo = RelationalQueryModelVisitor.CreateValueReaderMethodInfo;
            var entityType = QueryModelVisitor.QueryCompilationContext.Model.GetEntityType(elementType);

            var selectExpression = new SelectExpression();
            var tableName = QueryModelVisitor.QueryCompilationContext.GetTableName(entityType);

            var alias = _querySource.ItemName.StartsWith("<generated>_")
                ? tableName.First().ToString().ToLower()
                : _querySource.ItemName;

            selectExpression.AddTable(createTableExpression(entityType, tableName, alias));

            QueryModelVisitor.AddQuery(_querySource, selectExpression);

            var queryMethodArguments
                = new List<Expression>
                    {
                        Expression.Constant(_querySource),
                        EntityQueryModelVisitor.QueryContextParameter,
                        EntityQueryModelVisitor.QuerySourceScopeParameter,
                        _readerParameter
                    };

            if (QueryModelVisitor.QuerySourceRequiresMaterialization(_querySource))
            {
                var materializer
                    = new MaterializerFactory(
                        QueryModelVisitor
                            .QueryCompilationContext
                            .EntityMaterializerSource)
                        .CreateMaterializer(
                            entityType,
                            selectExpression,
                            (p, se) =>
                                se.AddToProjection(
                                    QueryModelVisitor.QueryCompilationContext.GetColumnName(p),
                                    p,
                                    _querySource),
                            _querySource);

                queryMethodInfo
                    = RelationalQueryModelVisitor.CreateEntityMethodInfo
                        .MakeGenericMethod(elementType);

                var keyProperties
                    = entityType.GetPrimaryKey().Properties;

                var keyFactory
                    = QueryModelVisitor.QueryCompilationContext.EntityKeyFactorySource
                        .GetKeyFactory(keyProperties);

                queryMethodArguments.AddRange(
                    new[]
                        {
                            Expression.Constant(0),
                            Expression.Constant(entityType),
                            Expression.Constant(QueryModelVisitor.QuerySourceRequiresTracking(_querySource)),
                            Expression.Constant(keyFactory),
                            Expression.Constant(keyProperties),
                            materializer
                        });
            }

            return Expression.Call(
                QueryModelVisitor.QueryCompilationContext.QueryMethodProvider.QueryMethod
                    .MakeGenericMethod(queryMethodInfo.ReturnType),
                EntityQueryModelVisitor.QueryContextParameter,
                Expression.Constant(new CommandBuilder(selectExpression, QueryModelVisitor.QueryCompilationContext)),
                Expression.Lambda(
                    Expression.Call(queryMethodInfo, queryMethodArguments),
                    _readerParameter));
        }
    }
}
