import { useLocation } from 'react-router-dom'

/** Links that stay inside whichever workspace the page is shown in (office or the /tech app). */
export function useFactoryLinks() {
  const tech = useLocation().pathname.startsWith('/tech')
  return {
    tech,
    asset: (id: string, tab?: string) => `${tech ? '/tech' : ''}/assets/${id}${tab ? `?tab=${tab}` : ''}`,
    workOrder: (id: string) => (tech ? `/tech/jobs/${id}` : `/work-orders/${id}`),
    scan: (code?: string) => `${tech ? '/tech' : ''}/scan${code ? `/${encodeURIComponent(code)}` : ''}`,
  }
}
