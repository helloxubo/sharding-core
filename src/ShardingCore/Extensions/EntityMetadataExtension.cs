﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.VirtualDatabase;

namespace ShardingCore.Extensions
{
    public static class EntityMetadataExtension
    {
        public static bool ShardingDataSourceFieldIsKey(this EntityMetadata metadata)
        {
            if (!metadata.IsMultiDataSourceMapping&& metadata.IsSingleKey)
                return false;
            return metadata.ShardingDataSourceProperty.Name == metadata.PrimaryKeyProperties.First().Name;
        }
        public static bool ShardingTableFieldIsKey(this EntityMetadata metadata)
        {
            if (!metadata.IsMultiTableMapping && metadata.IsSingleKey)
                return false;
            return metadata.ShardingTableProperty.Name == metadata.PrimaryKeyProperties.First().Name;
        }
    }
}
