﻿using System;
using System.Collections.Generic;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Sharding.PaginationConfigurations;
using ShardingCore.Test5x.Domain.Entities;
using ShardingCore.Test5x.Shardings.PaginationConfigs;
using ShardingCore.VirtualRoutes.Days;

namespace ShardingCore.Test5x.Shardings
{
    public class LogDayVirtualTableRoute:AbstractSimpleShardingDayKeyDateTimeVirtualTableRoute<LogDay>
    {
        protected override bool EnableHintRoute => true;
        public override bool? EnableRouteParseCompileCache => true;

        public override DateTime GetBeginTime()
        {
            return new DateTime(2021, 1, 1);
        }
        

        public override void Configure(EntityMetadataTableBuilder<LogDay> builder)
        {
            builder.ShardingProperty(o => o.LogTime);
            builder.TableSeparator(string.Empty);
        }

        public override IPaginationConfiguration<LogDay> CreatePaginationConfiguration()
        {
            return new LogDayPaginationConfiguration();
        }

        public override bool AutoCreateTableByTime()
        {
            return true;
        }

        public override List<string> GetAllTails()
        {
            var beginTime = GetBeginTime().Date;

            var tails = new List<string>();
            //提前创建表
            var nowTimeStamp = new DateTime(2021,11,20).Date;
            if (beginTime > nowTimeStamp)
                throw new ArgumentException("begin time error");
            var currentTimeStamp = beginTime;
            while (currentTimeStamp <= nowTimeStamp)
            {
                var tail = ShardingKeyToTail(currentTimeStamp);
                tails.Add(tail);
                currentTimeStamp = currentTimeStamp.AddDays(1);
            }
            return tails;
        }
    }
}
