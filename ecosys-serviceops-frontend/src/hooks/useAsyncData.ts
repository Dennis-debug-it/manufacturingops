import { useCallback, useEffect, useRef, useState, type DependencyList, type Dispatch, type SetStateAction } from 'react'

type AsyncState<T> = {
  data: T
  loading: boolean
  hasLoaded: boolean
  isRefreshing: boolean
  error: string | null
  reload: () => Promise<void>
  setData: Dispatch<SetStateAction<T>>
}

export function useAsyncData<T>(
  loader: (signal: AbortSignal) => Promise<T>,
  initialData: T,
  deps: DependencyList,
): AsyncState<T> {
  const [data, setData] = useState<T>(initialData)
  const [loading, setLoading] = useState(true)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const loaderRef = useRef(loader)
  const initialDataRef = useRef(initialData)
  const hasLoadedRef = useRef(false)
  const requestIdRef = useRef(0)

  useEffect(() => {
    loaderRef.current = loader
  }, [loader])

  useEffect(() => {
    initialDataRef.current = initialData
  }, [initialData])

  useEffect(() => {
    hasLoadedRef.current = hasLoaded
  }, [hasLoaded])

  const load = useCallback(async (signal?: AbortSignal) => {
    const requestId = ++requestIdRef.current
    const isReload = hasLoadedRef.current

    if (isReload) {
      setIsRefreshing(true)
    } else {
      setLoading(true)
    }

    setError(null)

    try {
      const next = await loaderRef.current(signal ?? new AbortController().signal)
      if (requestId !== requestIdRef.current || signal?.aborted) {
        return
      }

      setData(next)
      setHasLoaded(true)
      hasLoadedRef.current = true
    } catch (error) {
      if (signal?.aborted || (error instanceof Error && error.name === 'AbortError') || requestId !== requestIdRef.current) {
        return
      }

      setError(error instanceof Error ? error.message : 'Something went wrong while loading this view.')

      if (!hasLoadedRef.current) {
        setData(initialDataRef.current)
      }

      setHasLoaded(true)
      hasLoadedRef.current = true
    } finally {
      if (!signal?.aborted && requestId === requestIdRef.current) {
        setLoading(false)
        setIsRefreshing(false)
      }
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    void load(controller.signal)

    return () => controller.abort()
  }, [load, ...deps]) // eslint-disable-line react-hooks/exhaustive-deps

  return {
    data,
    loading,
    hasLoaded,
    isRefreshing,
    error,
    reload: async () => void load(),
    setData,
  }
}
