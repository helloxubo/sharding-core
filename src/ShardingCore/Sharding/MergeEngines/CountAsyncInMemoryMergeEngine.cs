﻿using Microsoft.EntityFrameworkCore;
using ShardingCore.Core.ShardingPage.Abstractions;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Sharding.MergeEngines.Abstractions.InMemoryMerge;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ShardingCore.Sharding.Abstractions.ParallelExecutors;
using ShardingCore.Sharding.MergeEngines.ParallelControls;

namespace ShardingCore.Sharding.StreamMergeEngines
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: 2021/8/17 22:36:14
    * @Ver: 1.0
    * @Email: 326308290@qq.com
    */
    internal class CountAsyncInMemoryMergeEngine<TEntity> : AbstractEnsureMethodCallInMemoryAsyncMergeEngine<TEntity,int>
    {
        private readonly IShardingPageManager _shardingPageManager;
        public CountAsyncInMemoryMergeEngine(StreamMergeContext<TEntity> streamMergeContext) : base(streamMergeContext)
        {
            _shardingPageManager = ShardingContainer.GetService<IShardingPageManager>();
        }


        public override async Task<int> MergeResultAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var result = await base.ExecuteAsync(queryable =>((IQueryable<TEntity>)queryable).CountAsync(cancellationToken), cancellationToken);

            if (_shardingPageManager.Current != null)
            {
                foreach (var routeQueryResult in result)
                {
                    _shardingPageManager.Current.RouteQueryResults.Add(new RouteQueryResult<long>(routeQueryResult.DataSourceName, routeQueryResult.TableRouteResult, routeQueryResult.QueryResult));
                }
            }
            return result.Sum(o=>o.QueryResult);
        }

        protected override IParallelExecuteControl<TResult> CreateParallelExecuteControl<TResult>(IParallelExecutor<TResult> executor)
        {
            return NoTripParallelExecuteControl<TResult>.Create(GetStreamMergeContext(), executor);
        }
    }
}