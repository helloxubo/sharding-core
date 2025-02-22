﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShardingCore.Sharding.MergeEngines.EnumeratorStreamMergeEngines.EnumeratorAsync
{
    internal class EmptyQueryEnumerator<T> : IAsyncEnumerator<T>,IEnumerator<T>
    {

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

#if !EFCORE2
        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(false);
        }
#endif

#if EFCORE2
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
#endif

        public bool MoveNext()
        {
            return false;
        }

        public void Reset()
        {
            
        }


        T IEnumerator<T>.Current => default;

        object IEnumerator.Current => default;

        T IAsyncEnumerator<T>.Current => default;

        public void Dispose()
        {
        }
    }
}
