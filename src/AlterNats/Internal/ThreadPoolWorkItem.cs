﻿using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AlterNats.Internal;

internal sealed class ThreadPoolWorkItem<T> : IThreadPoolWorkItem
{
    static readonly ConcurrentQueue<ThreadPoolWorkItem<T>> pool = new();

    ThreadPoolWorkItem<T>? nextNode;
    public ref ThreadPoolWorkItem<T>? NextNode => ref nextNode;

    Action<NatsKey,T?>? continuation;
    NatsKey subject;
    T? value;
    ILoggerFactory? loggerFactory;

    ThreadPoolWorkItem()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ThreadPoolWorkItem<T> Create(Action<NatsKey,T?> continuation, NatsKey subject, T? value, ILoggerFactory loggerFactory)
    {
        if (!pool.TryDequeue(out var item))
        {
            item = new ThreadPoolWorkItem<T>();
        }

        item.continuation = continuation;
        item.subject = subject;
        item.value = value;
        item.loggerFactory = loggerFactory;

        return item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute()
    {
        var call = continuation;
        var s = subject;
        var v = value;
        var factory = loggerFactory;
        continuation = null;
        value = default;
        subject = default;
        loggerFactory = null;
        if (call != null)
        {
            pool.Enqueue(this);

            try
            {
                call.Invoke(s,v);
            }
            catch (Exception ex)
            {
                if (loggerFactory != null)
                {
                    loggerFactory.CreateLogger<ThreadPoolWorkItem<T>>().LogError(ex, "Error occured during execute callback on ThreadPool.");
                }
            }
        }
    }
}
