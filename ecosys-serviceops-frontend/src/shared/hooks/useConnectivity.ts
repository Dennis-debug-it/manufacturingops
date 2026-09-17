import { useSyncExternalStore } from 'react'
import { isConnectivityOffline, subscribeConnectivity } from '../api/connectivityStore'

/** True while the app believes it cannot reach the API server at all. */
export function useConnectivity(): boolean {
  return useSyncExternalStore(subscribeConnectivity, isConnectivityOffline, () => false)
}
