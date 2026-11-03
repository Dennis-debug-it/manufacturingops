import { WifiOff } from 'lucide-react'
import { useConnectivity } from '../hooks/useConnectivity'

/**
 * Surfaces a genuine network/transport failure (no response reached the server at all) as one
 * app-wide banner instead of a toast per failed request — see ApiError.isNetworkError and
 * connectivityStore.ts. Clears itself as soon as any request gets a response again.
 */
export function ConnectivityBanner() {
  const offline = useConnectivity()

  if (!offline) {
    return null
  }

  return (
    <div className="flex items-center gap-2 bg-slate-800 px-4 py-2 text-sm font-medium text-white sm:px-6">
      <WifiOff className="h-4 w-4 shrink-0" />
      <span>Can't reach the server. Check your connection — we'll keep trying.</span>
    </div>
  )
}
