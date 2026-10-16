import { useSyncExternalStore } from 'react'
import { Download, Share, X } from 'lucide-react'
import {
  dismissInstallPrompt,
  getInstallPromptState,
  promptInstall,
  subscribeInstallPrompt,
} from '../../app/installPrompt'
import { brand } from '../config/brand'

const HIDDEN = { kind: 'hidden' } as const

/** Bottom card inviting the user to install the app (see app/installPrompt.ts). */
export function InstallAppBanner() {
  const state = useSyncExternalStore(subscribeInstallPrompt, getInstallPromptState, () => HIDDEN)

  if (state.kind === 'hidden') {
    return null
  }

  return (
    <div
      role="dialog"
      aria-label={`Install ${brand.appName}`}
      className="fixed inset-x-0 bottom-0 z-50 p-4 pb-[calc(1rem+env(safe-area-inset-bottom))] sm:left-auto sm:max-w-sm"
    >
      <div className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-lg">
        <img src={brand.assets.mark} alt="" className="h-10 w-10 shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-slate-900">Install the Ecosys app</p>
          {state.kind === 'native' ? (
            <p className="mt-1 text-sm text-slate-600">Open it from your home screen, full screen, and keep working offline.</p>
          ) : (
            <p className="mt-1 text-sm text-slate-600">
              Tap <Share className="inline h-4 w-4 align-text-bottom" aria-label="Share" /> then{' '}
              <span className="font-medium">Add to Home Screen</span>.
            </p>
          )}
          <div className="mt-3 flex gap-2">
            {state.kind === 'native' && (
              <button
                type="button"
                onClick={() => void promptInstall()}
                className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-teal-700 px-3 text-sm font-semibold text-white hover:bg-teal-800"
              >
                <Download className="h-4 w-4" />
                Install
              </button>
            )}
            <button
              type="button"
              onClick={dismissInstallPrompt}
              className="inline-flex h-9 items-center rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Not now
            </button>
          </div>
        </div>
        <button
          type="button"
          onClick={dismissInstallPrompt}
          aria-label="Close"
          className="-m-1 rounded p-1 text-slate-400 hover:text-slate-600"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  )
}
