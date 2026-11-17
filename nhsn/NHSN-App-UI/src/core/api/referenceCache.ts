/**
 * In-memory cache for static reference data (vendors, timezones, measures,
 * HSLOC codes) so it isn't refetched every time a step mounts. Also dedupes
 * concurrent calls for the same key.
 */

const memoryCache = new Map<string, Promise<unknown>>();

export function cachedReference<T>(key: string, fetcher: () => Promise<T>): Promise<T> {
  const existing = memoryCache.get(key) as Promise<T> | undefined;
  if (existing) {
    return existing;
  }

  const pending = fetcher().catch(error => {
    memoryCache.delete(key);
    throw error;
  });
  memoryCache.set(key, pending);
  return pending;
}
