import { BrowserRouter } from 'react-router-dom'
import { ErrorBoundary } from '../shared/components/ErrorBoundary'
import { InstallAppBanner } from '../shared/components/InstallAppBanner'
import { Providers } from './providers'
import { AppRouter } from './router'

export function App() {
  return (
    <ErrorBoundary>
      <Providers>
        <BrowserRouter>
          <AppRouter />
          <InstallAppBanner />
        </BrowserRouter>
      </Providers>
    </ErrorBoundary>
  )
}
