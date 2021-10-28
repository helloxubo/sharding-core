﻿using System;
using Sample.BulkConsole.Entities;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Sharding.PaginationConfigurations;
using ShardingCore.VirtualRoutes.Days;

namespace Sample.BulkConsole
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: 2021/9/7 21:16:47
    * @Ver: 1.0
    * @Email: 326308290@qq.com
    */
    public class OrderVirtualRoute:AbstractSimpleShardingDayKeyDateTimeVirtualTableRoute<Order>
    {

        public override DateTime GetBeginTime()
        {
            return DateTime.Now.Date.AddDays(-3);
        }

        public override IPaginationConfiguration<Order> CreatePaginationConfiguration()
        {
            return new OrderPaginationConfiguration();
        }
    }
}
