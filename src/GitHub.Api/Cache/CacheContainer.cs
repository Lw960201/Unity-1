﻿using System;
using System.Collections.Generic;
using GitHub.Logging;

namespace GitHub.Unity
{
    public class CacheContainer : ICacheContainer
    {
        private static ILogging Logger = LogHelper.GetLogger<CacheContainer>();

        private Dictionary<CacheType, Lazy<IManagedCache>> caches = new Dictionary<CacheType, Lazy<IManagedCache>>();

        public event Action<CacheType> CacheInvalidated;
        public event Action<CacheType, DateTimeOffset> CacheUpdated;

        public void SetCacheInitializer(CacheType cacheType, Func<IManagedCache> initializer)
        {
            caches.Add(cacheType, new Lazy<IManagedCache>(() => SetupCache(initializer())));
        }

        public void ValidateAll()
        {
            // this can trigger invalidation requests fyi
            foreach (var cache in caches.Values)
                cache.Value.ValidateData();
        }

        public void InvalidateAll()
        {
            foreach (var cache in caches.Values)
            {
                // force an invalidation if the cache is valid, otherwise it will do it on its own
                if (cache.Value.ValidateData())
                    cache.Value.InvalidateData();
            }
        }

        private IManagedCache SetupCache(IManagedCache cache)
        {
            cache.CacheInvalidated += OnCacheInvalidated;
            cache.CacheUpdated += OnCacheUpdated;
            return cache;
        }

        public IManagedCache GetCache(CacheType cacheType)
        {
            return caches[cacheType].Value;
        }

        public void CheckAndRaiseEventsIfCacheNewer(CacheUpdateEvent cacheUpdateEvent)
        {
            var cache = GetCache(cacheUpdateEvent.cacheType);
            var needsInvalidation = cache.ValidateData();
            if (!needsInvalidation || cache.LastUpdatedAt != cacheUpdateEvent.UpdatedTime)
            {
                OnCacheUpdated(cache.CacheType, cache.LastUpdatedAt);
            }
        }

        private void OnCacheUpdated(CacheType cacheType, DateTimeOffset datetime)
        {
            Logger.Trace("OnCacheUpdated cacheType:{0} datetime:{1}", cacheType, datetime);
            CacheUpdated.SafeInvoke(cacheType, datetime);
        }

        private void OnCacheInvalidated(CacheType cacheType)
        {
            Logger.Trace("OnCacheInvalidated cacheType:{0}", cacheType);
            CacheInvalidated.SafeInvoke(cacheType);
        }

        private bool disposed;
        private void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;

            if (disposing)
            {
                foreach (var cache in caches.Values)
                {
                    cache.Value.CacheInvalidated -= OnCacheInvalidated;
                    cache.Value.CacheUpdated -= OnCacheUpdated;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IBranchCache BranchCache { get { return (IBranchCache)caches[CacheType.Branches].Value; } }
        public IGitLogCache GitLogCache { get { return (IGitLogCache)caches[CacheType.GitLog].Value; } }
        public IGitAheadBehindCache GitTrackingStatusCache { get { return (IGitAheadBehindCache)caches[CacheType.GitAheadBehind].Value; } }
        public IGitStatusCache GitStatusEntriesCache { get { return (IGitStatusCache)caches[CacheType.GitStatus].Value; } }
        public IGitLocksCache GitLocksCache { get { return (IGitLocksCache)caches[CacheType.GitLocks].Value; } }
        public IGitUserCache GitUserCache { get { return (IGitUserCache)caches[CacheType.GitUser].Value; } }
        public IRepositoryInfoCache RepositoryInfoCache { get { return (IRepositoryInfoCache)caches[CacheType.RepositoryInfo].Value; } }
    }
}