using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.ShardingConfigurations;
using ShardingCore.Core.ShardingEnumerableQueries;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.Abstractions;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.PhysicDataSources;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes;
using ShardingCore.Core.VirtualTables;
using ShardingCore.Exceptions;
using ShardingCore.Extensions;
using ShardingCore.Sharding;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Sharding.ReadWriteConfigurations;
using ShardingCore.Utils;

namespace ShardingCore.Core.VirtualDatabase.VirtualDataSources
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: Friday, 05 February 2021 15:21:04
    * @Email: 326308290@qq.com
    */
    public class VirtualDataSource<TShardingDbContext> : IVirtualDataSource<TShardingDbContext> where TShardingDbContext : DbContext, IShardingDbContext
    {
        public IVirtualDataSourceConfigurationParams ConfigurationParams { get; }
        public IConnectionStringManager ConnectionStringManager { get; }

        private readonly IEntityMetadataManager<TShardingDbContext> _entityMetadataManager;
        private readonly IVirtualDataSourceRouteManager<TShardingDbContext> _dataSourceRouteManager;

        private readonly IPhysicDataSourcePool _physicDataSourcePool;

        public string ConfigId => ConfigurationParams.ConfigId;
        public int Priority => ConfigurationParams.Priority;
        public string DefaultDataSourceName { get; private set; }
        public string DefaultConnectionString { get; private set; }
        public bool UseReadWriteSeparation { get; }

        public VirtualDataSource(IEntityMetadataManager<TShardingDbContext> entityMetadataManager, IVirtualDataSourceRouteManager<TShardingDbContext> dataSourceRouteManager, IVirtualDataSourceConfigurationParams<TShardingDbContext> configurationParams)
        {
            Check.NotNull(configurationParams, nameof(configurationParams));
            Check.NotNull(configurationParams.ExtraDataSources, nameof(configurationParams.ExtraDataSources));
            Check.NotNull(configurationParams.ShardingComparer, nameof(configurationParams.ShardingComparer));
            if(configurationParams.MaxQueryConnectionsLimit<=0)
                throw new ArgumentOutOfRangeException(nameof(configurationParams.MaxQueryConnectionsLimit));
            ConfigurationParams = configurationParams;
            _physicDataSourcePool = new PhysicDataSourcePool();
            //添加数据源
            AddPhysicDataSource(new DefaultPhysicDataSource(ConfigurationParams.DefaultDataSourceName, ConfigurationParams.DefaultConnectionString, true));
            foreach (var extraDataSource in ConfigurationParams.ExtraDataSources)
            {
                AddPhysicDataSource(new DefaultPhysicDataSource(extraDataSource.Key, extraDataSource.Value, false));
            }
            _entityMetadataManager = entityMetadataManager;
            _dataSourceRouteManager = dataSourceRouteManager;
            UseReadWriteSeparation = ConfigurationParams.UseReadWriteSeparation();
            if (UseReadWriteSeparation)
            {
                CheckReadWriteSeparation();
                ConnectionStringManager = new ReadWriteConnectionStringManager(this);
            }
            else
            {
                ConnectionStringManager = new DefaultConnectionStringManager(this);

            }
        }

        private void CheckReadWriteSeparation()
        {
            if (!ConfigurationParams.ReadStrategy.HasValue)
            {
                throw new ArgumentException(nameof(ConfigurationParams.ReadStrategy));
            }
            if (!ConfigurationParams.ReadConnStringGetStrategy.HasValue)
            {
                throw new ArgumentException(nameof(ConfigurationParams.ReadConnStringGetStrategy));
            }
            if (!ConfigurationParams.ReadWriteDefaultEnable.HasValue)
            {
                throw new ArgumentException(nameof(ConfigurationParams.ReadWriteDefaultEnable));
            }
            if (!ConfigurationParams.ReadWriteDefaultPriority.HasValue)
            {
                throw new ArgumentException(nameof(ConfigurationParams.ReadWriteDefaultPriority));
            }
        }

        public IVirtualDataSourceRoute GetRoute(Type entityType)
        {
            return _dataSourceRouteManager.GetRoute(entityType);
        }

        public IVirtualDataSourceRoute<TEntity> GetRoute<TEntity>() where TEntity : class
        {
            return _dataSourceRouteManager.GetRoute<TEntity>();
        }

        public List<string> RouteTo(Type entityType, ShardingDataSourceRouteConfig routeRouteConfig)
        {
            if (!_entityMetadataManager.IsShardingDataSource(entityType))
                return new List<string>(1) { DefaultDataSourceName };
            var virtualDataSourceRoute = _dataSourceRouteManager.GetRoute(entityType);

            if (routeRouteConfig.UseQueryable())
                return virtualDataSourceRoute.RouteWithPredicate(routeRouteConfig.GetQueryable(), true);
            if (routeRouteConfig.UsePredicate())
            {
                var shardingEmptyEnumerableQuery = (IShardingEmptyEnumerableQuery)Activator.CreateInstance(typeof(ShardingEmptyEnumerableQuery<>).MakeGenericType(entityType), routeRouteConfig.GetPredicate());
                return virtualDataSourceRoute.RouteWithPredicate(shardingEmptyEnumerableQuery.EmptyQueryable(), false);
            }
            object shardingKeyValue = null;
            if (routeRouteConfig.UseValue())
                shardingKeyValue = routeRouteConfig.GetShardingKeyValue();

            if (routeRouteConfig.UseEntity())
            {
                shardingKeyValue = routeRouteConfig.GetShardingDataSource().GetPropertyValue(virtualDataSourceRoute.EntityMetadata.ShardingDataSourceProperty.Name);
            }

            if (shardingKeyValue != null)
            {
                var dataSourceName = virtualDataSourceRoute.RouteWithValue(shardingKeyValue);
                return new List<string>(1) { dataSourceName };
            }

            throw new NotImplementedException(nameof(ShardingDataSourceRouteConfig));
        }

        /// <summary>
        /// 获取默认的数据源信息
        /// </summary>
        /// <returns></returns>
        public IPhysicDataSource GetDefaultDataSource()
        {
            return GetPhysicDataSource(DefaultDataSourceName);
        }
        /// <summary>
        /// 获取物理数据源
        /// </summary>
        /// <param name="dataSourceName"></param>
        /// <returns></returns>
        /// <exception cref="ShardingCoreNotFoundException"></exception>
        public IPhysicDataSource GetPhysicDataSource(string dataSourceName)
        {
            Check.NotNull(dataSourceName, "data source name is null,plz confirm IShardingBootstrapper.Star()");
            var dataSource = _physicDataSourcePool.TryGet(dataSourceName);
            if (null == dataSource)
                throw new ShardingCoreNotFoundException($"data source:[{dataSourceName}]");

            return dataSource;
        }
        /// <summary>
        /// 获取所有的数据源名称
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllDataSourceNames()
        {
            return _physicDataSourcePool.GetAllDataSourceNames();
        }

        /// <summary>
        /// 获取数据源
        /// </summary>
        /// <param name="dataSourceName"></param>
        /// <returns></returns>
        /// <exception cref="ShardingCoreNotFoundException"></exception>
        public string GetConnectionString(string dataSourceName)
        {
            if (IsDefault(dataSourceName))
                return DefaultConnectionString;
            return GetPhysicDataSource(dataSourceName).ConnectionString;
        }

        /// <summary>
        /// 添加数据源
        /// </summary>
        /// <param name="physicDataSource"></param>
        /// <returns></returns>
        /// <exception cref="ShardingCoreInvalidOperationException">重复添加默认数据源</exception>
        public bool AddPhysicDataSource(IPhysicDataSource physicDataSource)
        {
            if (physicDataSource.IsDefault)
            {
                if (!string.IsNullOrWhiteSpace(DefaultDataSourceName))
                {
                    throw new ShardingCoreInvalidOperationException($"default data source name:[{DefaultDataSourceName}],add physic default data source name:[{physicDataSource.DataSourceName}]");
                }
                DefaultDataSourceName = physicDataSource.DataSourceName;
                DefaultConnectionString = physicDataSource.ConnectionString;
            }

            return _physicDataSourcePool.TryAdd(physicDataSource);
        }
        /// <summary>
        /// 是否是默认数据源
        /// </summary>
        /// <param name="dataSourceName"></param>
        /// <returns></returns>
        public bool IsDefault(string dataSourceName)
        {
            return DefaultDataSourceName == dataSourceName;
        }
        /// <summary>
        /// 检查是否配置默认数据源和默认链接字符串
        /// </summary>
        /// <exception cref="ShardingCoreInvalidOperationException"></exception>
        public void CheckVirtualDataSource()
        {
            if (string.IsNullOrWhiteSpace(DefaultDataSourceName))
                throw new ShardingCoreInvalidOperationException(
                    $"virtual data source not inited {nameof(DefaultDataSourceName)} in IShardingDbContext null");
            if (string.IsNullOrWhiteSpace(DefaultConnectionString))
                throw new ShardingCoreInvalidOperationException(
                    $"virtual data source not inited {nameof(DefaultConnectionString)} in IShardingDbContext null");
        }

        public DbContextOptionsBuilder UseDbContextOptionsBuilder(string connectionString,
            DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            var doUseDbContextOptionsBuilder = ConfigurationParams.UseDbContextOptionsBuilder(connectionString, dbContextOptionsBuilder);
            doUseDbContextOptionsBuilder.UseInnerDbContextSharding<TShardingDbContext>();
            ConfigurationParams.UseInnerDbContextOptionBuilder(dbContextOptionsBuilder);
            return doUseDbContextOptionsBuilder;
        }

        public DbContextOptionsBuilder UseDbContextOptionsBuilder(DbConnection dbConnection,
            DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            var doUseDbContextOptionsBuilder = ConfigurationParams.UseDbContextOptionsBuilder(dbConnection, dbContextOptionsBuilder);
            doUseDbContextOptionsBuilder.UseInnerDbContextSharding<TShardingDbContext>();
            ConfigurationParams.UseInnerDbContextOptionBuilder(dbContextOptionsBuilder);
            return doUseDbContextOptionsBuilder;
        }

        public IDictionary<string, string> GetDataSources()
        {
            return _physicDataSourcePool.GetDataSources();
        }
    }
}