import { useCallback, useEffect } from 'react'

/**
 * Guards against accidental navigation away from a page with unsaved changes.
 *
 * - Shows a browser confirm dialog on tab close / refresh (beforeunload).
 * - Intercepts anchor-based in-app navigation before the router handles it.
 *
 * @param isDirty - true when there are unsaved changes that would be lost
 * @param message - optional custom message shown in the confirm dialog
 */
export function useUnsavedChangesGuard(
  isDirty: boolean,
  message = 'You have unsaved changes. Leave this page and discard them?',
) {
  const handleBeforeUnload = useCallback(
    (event: BeforeUnloadEvent) => {
      if (isDirty) {
        event.preventDefault()
        event.returnValue = message
      }
    },
    [isDirty, message],
  )

  useEffect(() => {
    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [handleBeforeUnload])

  useEffect(() => {
    if (!isDirty) {
      return
    }

    const handleDocumentClick = (event: MouseEvent) => {
      if (event.defaultPrevented) {
        return
      }

      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return
      }

      const target = event.target
      if (!(target instanceof Element)) {
        return
      }

      const anchor = target.closest('a[href]')
      if (!(anchor instanceof HTMLAnchorElement)) {
        return
      }

      if (anchor.target && anchor.target !== '_self') {
        return
      }

      if (anchor.hasAttribute('download')) {
        return
      }

      const nextUrl = new URL(anchor.href, window.location.href)
      const currentUrl = new URL(window.location.href)
      const isSameDocument =
        nextUrl.origin === currentUrl.origin
        && nextUrl.pathname === currentUrl.pathname
        && nextUrl.search === currentUrl.search
        && nextUrl.hash === currentUrl.hash

      if (isSameDocument) {
        return
      }

      if (!window.confirm(message)) {
        event.preventDefault()
        event.stopPropagation()
      }
    }

    document.addEventListener('click', handleDocumentClick, true)
    return () => document.removeEventListener('click', handleDocumentClick, true)
  }, [isDirty, message])
}
