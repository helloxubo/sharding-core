﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ShardingCore.Sharding.ReadWriteConfigurations.Abstractions
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: 2021/9/6 16:30:44
    * @Ver: 1.0
    * @Email: 326308290@qq.com
    */
    public interface IShardingReadWriteAccessor
    {
        Type ShardingDbContextType { get;}
        ShardingReadWriteContext ShardingReadWriteContext { get; set; }
    }
}
