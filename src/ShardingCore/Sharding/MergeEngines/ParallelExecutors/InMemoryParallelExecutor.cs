﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShardingCore.Core;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RoutingRuleEngine;
using ShardingCore.Extensions;
using ShardingCore.Sharding.Abstractions.ParallelExecutors;
using ShardingCore.Sharding.MergeEngines.Abstractions;
using ShardingCore.Sharding.MergeEngines.Common;
using ShardingCore.Sharding.StreamMergeEngines;

namespace ShardingCore.Sharding.MergeEngines.ParallelExecutors
{
    internal class InMemoryParallelExecutor<TEntity,TResult>:IParallelExecutor<RouteQueryResult<TResult>>
    {
        private readonly StreamMergeContext<TEntity> _streamMergeContext;
        private readonly Func<IQueryable, Task<TResult>> _efQuery;

        public InMemoryParallelExecutor(StreamMergeContext<TEntity> streamMergeContext, Func<IQueryable, Task<TResult>> efQuery)
        {
            _streamMergeContext = streamMergeContext;
            _efQuery = efQuery;
        }
        public async Task<ShardingMergeResult<RouteQueryResult<TResult>>> ExecuteAsync(SqlExecutorUnit sqlExecutorUnit, CancellationToken cancellationToken = new CancellationToken())
        {
             var connectionMode = _streamMergeContext.RealConnectionMode(sqlExecutorUnit.ConnectionMode);
            var dataSourceName = sqlExecutorUnit.RouteUnit.DataSourceName;
            var routeResult = sqlExecutorUnit.RouteUnit.TableRouteResult;

            var (asyncExecuteQueryable, dbContext) =
                CreateAsyncExecuteQueryable(dataSourceName, routeResult, connectionMode);

            var queryResult = await _efQuery(asyncExecuteQueryable);
            var routeQueryResult = new RouteQueryResult<TResult>(dataSourceName, routeResult, queryResult);
            return new ShardingMergeResult<RouteQueryResult<TResult>>(dbContext, routeQueryResult);
        }
        private (IQueryable queryable, DbContext dbContext) CreateAsyncExecuteQueryable(string dsname, TableRouteResult tableRouteResult, ConnectionModeEnum connectionMode)
        {
            var shardingDbContext = _streamMergeContext.CreateDbContext(dsname, tableRouteResult, connectionMode);
            var newQueryable = (IQueryable<TEntity>)_streamMergeContext.GetReWriteQueryable()
                .ReplaceDbContextQueryable(shardingDbContext);
            return (newQueryable, shardingDbContext);
        }

    }
}
