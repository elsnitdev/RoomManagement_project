const cache = new Map()

export function invalidateApiCache(key) {
  if (!key) {
    cache.clear()
    return
  }
  for (const cachedKey of cache.keys()) {
    if (cachedKey === key || cachedKey.startsWith(`${key}:`)) {
      cache.delete(cachedKey)
    }
  }
}

export function loadCached(key, loader, { ttlMs = 30_000, force = false } = {}) {
  const existing = cache.get(key)
  const now = Date.now()

  if (!force && existing?.data !== undefined && existing.expiresAt > now) {
    return Promise.resolve(existing.data)
  }
  if (existing?.promise) return existing.promise

  const entry = { data: undefined, expiresAt: 0, promise: null }
  const promise = Promise.resolve()
    .then(loader)
    .then(data => {
      if (cache.get(key) === entry) {
        entry.data = data
        entry.expiresAt = Date.now() + ttlMs
        entry.promise = null
      }
      return data
    })
    .catch(error => {
      if (cache.get(key) === entry) cache.delete(key)
      throw error
    })

  entry.promise = promise
  cache.set(key, entry)
  return promise
}
