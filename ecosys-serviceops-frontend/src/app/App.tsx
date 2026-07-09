import { BrowserRouter } from 'react-router-dom'
import { ErrorBoundary } from '../shared/components/ErrorBoundary'
import { Providers } from './providers'
import { AppRouter } from './router'

export function App() {
  return (
    <ErrorBoundary>
      <Providers>
        <BrowserRouter>
          <AppRouter />
        </BrowserRouter>
      </Providers>
    </ErrorBoundary>
  )
}
