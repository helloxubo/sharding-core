﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using ShardingCore.Core.TrackerManagers;
using ShardingCore.Extensions;

namespace ShardingCore.Sharding.Visitors.Querys
{
    internal class QueryCompileParseVisitors : ExpressionVisitor
    {
        private readonly ITrackerManager _trackerManager;
        private bool isUnion;
        private bool? isNoTracking;
        private bool isIgnoreFilter;
        private readonly ISet<Type> shardingEntities = new HashSet<Type>();

        public QueryCompileParseVisitors(ITrackerManager trackerManager)
        {
            _trackerManager = trackerManager;
        }

        public CompileParseResult GetCompileParseResult()
        {
            return new CompileParseResult(isUnion, isNoTracking, isIgnoreFilter, shardingEntities);
        }
#if EFCORE2 || EFCORE3
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is IQueryable queryable)
            {
                shardingEntities.Add(queryable.ElementType);
            }

            return base.VisitConstant(node);
        }
#endif
#if EFCORE5 || EFCORE6
        protected override Expression VisitExtension(Expression node)
        {
            if (node is QueryRootExpression queryRootExpression)
            {
                shardingEntities.Add(queryRootExpression.EntityType.ClrType);
            }
            return base.VisitExtension(node);
        }
#endif
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            switch (node.Method.Name)
            {
                case nameof(Queryable.Union): isUnion = true; break;
                case nameof(EntityFrameworkQueryableExtensions.AsNoTracking): isNoTracking = true; break;
                case nameof(EntityFrameworkQueryableExtensions.AsTracking): isNoTracking = false; break;
                case nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters): isIgnoreFilter = true; break;
                case nameof(EntityFrameworkQueryableExtensions.Include):
                case nameof(EntityFrameworkQueryableExtensions.ThenInclude): DiscoverQueryEntities(node); break;
            }

            return base.VisitMethodCall(node);
        }

        private void DiscoverQueryEntities(MethodCallExpression node)
        {
            var genericArguments = node.Type.GetGenericArguments();
            for (var i = 0; i < genericArguments.Length; i++)
            {
                var genericArgument = genericArguments[i];
                if (typeof(IEnumerable).IsAssignableFrom(genericArgument))
                {
                    var arguments = genericArgument.GetGenericArguments();
                    foreach (var argument in arguments)
                    {
                        //if is db context model
                        if (_trackerManager.IsDbContextModel(argument))
                        {
                            shardingEntities.Add(argument);
                        }
                    }
                }

                if (!genericArgument.IsSimpleType())
                {
                    //if is db context model
                    if (_trackerManager.IsDbContextModel(genericArgument))
                    {
                        shardingEntities.Add(genericArgument);
                    }
                }
            }
        }
    }
}
