import { useEffect, useState } from 'react'
import { apiClient } from '../api/apiClient'
import { cn } from '../utils/cn'

export interface AuthenticatedImageProps {
  src: string
  alt: string
  className?: string
  onClick?: () => void
}

/**
 * Attachment downloads require the bearer token (AttachmentsController is [Authorize]),
 * so a plain <img src> can't be used. This fetches the image as a blob through
 * apiClient (which attaches the token) and renders it via an object URL.
 */
export function AuthenticatedImage({ src, alt, className, onClick }: AuthenticatedImageProps) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null)

  useEffect(() => {
    let revoked = false
    let currentUrl: string | null = null

    apiClient
      .get<Blob>(src, { responseType: 'blob' })
      .then((response) => {
        if (revoked) return
        currentUrl = URL.createObjectURL(response.data)
        setObjectUrl(currentUrl)
      })
      .catch(() => {
        if (!revoked) setObjectUrl(null)
      })

    return () => {
      revoked = true
      if (currentUrl) URL.revokeObjectURL(currentUrl)
    }
  }, [src])

  if (!objectUrl) {
    return <div className={cn('animate-pulse bg-slate-100', className)} aria-label={`Loading ${alt}`} />
  }

  return <img src={objectUrl} alt={alt} className={className} onClick={onClick} />
}
