﻿
#if EFCORE6
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using ShardingCore.Core;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.Exceptions;
using ShardingCore.Sharding.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShardingCore.EFCores
{

    public class ShardingModelSource : ModelSource, IShardingModelSource
    {
        private readonly object _syncObject = new();

        public ShardingModelSource(ModelSourceDependencies dependencies) : base(dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));

            Dependencies = dependencies;
        }

        /// <summary>
        ///     Dependencies for this service.
        /// </summary>
        protected override ModelSourceDependencies Dependencies { get; }

        /// <summary>
        ///     Returns the model from the cache, or creates a model if it is not present in the cache.
        /// </summary>
        /// <param name="context">The context the model is being produced for.</param>
        /// <param name="conventionSetBuilder">The convention set to use when creating the model.</param>
        /// <returns>The model to be used.</returns>
        [Obsolete("Use the overload with ModelCreationDependencies")]
        public override IModel GetModel(
            DbContext context,
            IConventionSetBuilder conventionSetBuilder)
        {
            throw new ShardingCoreNotSupportException("Use the overload with ModelCreationDependencies");
        }

        /// <summary>
        ///     Returns the model from the cache, or creates a model if it is not present in the cache.
        /// </summary>
        /// <param name="context">The context the model is being produced for.</param>
        /// <param name="conventionSetBuilder">The convention set to use when creating the model.</param>
        /// <param name="modelDependencies">The dependencies object for the model.</param>
        /// <returns>The model to be used.</returns>
        [Obsolete("Use the overload with ModelCreationDependencies")]
        public override IModel GetModel(
            DbContext context,
            IConventionSetBuilder conventionSetBuilder,
            ModelDependencies modelDependencies)
        {
            throw new ShardingCoreNotSupportException("Use the overload with ModelCreationDependencies");
        }

        /// <summary>
        ///     Gets the model to be used.
        /// </summary>
        /// <param name="context">The context the model is being produced for.</param>
        /// <param name="modelCreationDependencies">The dependencies object used during the creation of the model.</param>
        /// <param name="designTime">Whether the model should contain design-time configuration.</param>
        /// <returns>The model to be used.</returns>
        public override IModel GetModel(
            DbContext context,
            ModelCreationDependencies modelCreationDependencies,
            bool designTime)
        {
            var priority = CacheItemPriority.High;
            if (context is IShardingTableDbContext shardingTableDbContext)
            {
                if (shardingTableDbContext.RouteTail is ISingleQueryRouteTail singleQueryRouteTail)
                {
                    if (singleQueryRouteTail.IsShardingTableQuery())
                    {
                        priority = CacheItemPriority.Normal;
                    }
                }
                else if (shardingTableDbContext.RouteTail is IMultiQueryRouteTail)
                {
                    var multiModel = this.CreateModel(context, modelCreationDependencies.ConventionSetBuilder, modelCreationDependencies.ModelDependencies);
                    multiModel = modelCreationDependencies.ModelRuntimeInitializer.Initialize(multiModel, designTime, modelCreationDependencies.ValidationLogger);
                    return multiModel;
                }
            }
            var cache = Dependencies.MemoryCache;
            var cacheKey = Dependencies.ModelCacheKeyFactory.Create(context, designTime);
            if (!cache.TryGetValue(cacheKey, out IModel model))
            {
                // Make sure OnModelCreating really only gets called once, since it may not be thread safe.
                var acquire = Monitor.TryEnter(_syncObject, TimeSpan.FromSeconds(3));
                if (!acquire)
                {
                    throw new ShardingCoreInvalidOperationException("cache model timeout");
                }
                try
                {
                    if (!cache.TryGetValue(cacheKey, out model))
                    {
                        model = CreateModel(
                            context, modelCreationDependencies.ConventionSetBuilder, modelCreationDependencies.ModelDependencies);

                        model = modelCreationDependencies.ModelRuntimeInitializer.Initialize(
                            model, designTime, modelCreationDependencies.ValidationLogger);

                        model = cache.Set(cacheKey, model, new MemoryCacheEntryOptions { Size = 200, Priority = priority });
                    }
                }
                finally
                {
                    Monitor.Exit(_syncObject);
                }
            }

            return model;
        }

        public IModelCacheKeyFactory GetModelCacheKeyFactory()
        {
            return Dependencies.ModelCacheKeyFactory;
        }

        public object GetSyncObject()
        {
            return _syncObject;
        }

        public void Remove(object key)
        {
            var acquire = Monitor.TryEnter(_syncObject, TimeSpan.FromSeconds(3));
            if (!acquire)
            {
                throw new ShardingCoreInvalidOperationException("cache model timeout");
            }
            try
            {

                var cache = Dependencies.MemoryCache;
                cache.Remove(key);
            }
            finally
            {
                Monitor.Exit(_syncObject);
            }
        }
    }
}
#endif