import { useCallback, useEffect, useRef, useState } from 'react'
import { loadCached } from '../services/apiCache'

// key phải bao gồm ID/bộ lọc để mỗi tập dữ liệu có cache riêng.
export function useApiQuery(key, loader, { enabled = true, ttlMs = 30_000 } = {}) {
  const loaderRef = useRef(loader)
  loaderRef.current = loader

  const [state, setState] = useState({ data: null, error: null, isLoading: enabled })
  const [request, setRequest] = useState({ version: 0, force: false })

  const reload = useCallback(() => {
    setRequest(current => ({ version: current.version + 1, force: true }))
  }, [])

  useEffect(() => {
    if (!enabled || !key) {
      setState({ data: null, error: null, isLoading: false })
      return
    }

    let active = true
    setState({ data: null, error: null, isLoading: true })
    loadCached(key, () => loaderRef.current(), { ttlMs, force: request.force })
      .then(data => {
        if (active) setState({ data, error: null, isLoading: false })
      })
      .catch(error => {
        if (active) setState(current => ({ ...current, error, isLoading: false }))
      })

    return () => { active = false }
  }, [key, enabled, ttlMs, request])

  return { ...state, reload }
}
