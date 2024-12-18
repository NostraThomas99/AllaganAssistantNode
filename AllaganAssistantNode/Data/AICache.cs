using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class AICache<TRecord, TObject>
    where TRecord : class
    where TObject : class
{
    private class CacheItem
    {
        public TObject Value { get; }
        public DateTime LastUpdated { get; }

        public CacheItem(TObject value, DateTime lastUpdated)
        {
            Value = value;
            LastUpdated = lastUpdated;
        }
    }

    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
    private readonly ConcurrentDictionary<string, Task<TObject?>> _inFlightFetches = new();
    private readonly Func<TRecord, TObject?> _fetchFunc;
    private readonly Func<TRecord, string> _recordKeySelector;
    private readonly TimeSpan _expirationTime;

    public AICache(Func<TRecord, TObject?> fetchFunc, Func<TRecord, string> recordKeySelector, TimeSpan expirationTime)
    {
        _fetchFunc = fetchFunc ?? throw new ArgumentNullException(nameof(fetchFunc));
        _recordKeySelector = recordKeySelector ?? throw new ArgumentNullException(nameof(recordKeySelector));
        _expirationTime = expirationTime;
    }

    public bool TryGetValue(TRecord record, out TObject? obj)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        var key = _recordKeySelector(record);

        // Check if the item is in the cache and not expired
        if (_cache.TryGetValue(key, out var cacheItem))
        {
            if (DateTime.UtcNow - cacheItem.LastUpdated < _expirationTime)
            {
                obj = cacheItem.Value;
                return true;
            }

            // If expired, remove it from the cache
            _cache.TryRemove(key, out _);
        }

        // Fetch in the background if not already being fetched
        var fetchTask = _inFlightFetches.GetOrAdd(key, _ =>
        {
            return Task.Run(() =>
            {
                TObject? fetched = null;
                try
                {
                    fetched = _fetchFunc(record);
                }
                catch (Exception ex)
                {
                    // Log error (replace Svc.Log.Error with your logging mechanism)
                    Console.Error.WriteLine($"Failed to fetch object for record {key}: {ex.Message}");
                }

                // Store the result in the cache with the current timestamp if successfully fetched
                if (fetched != null)
                {
                    _cache[key] = new CacheItem(fetched, DateTime.UtcNow);
                }

                // Remove the fetch task from the inflight map
                _inFlightFetches.TryRemove(key, out var _);
                return fetched;
            });
        });

        // Return null if data hasn't been fetched yet
        obj = null;
        return false;
    }
}