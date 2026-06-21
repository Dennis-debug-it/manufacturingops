import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { clearStoredAuth } from '../lib/api'
import { cleanupBodyInteractivity, clearTransientAppState, dispatchUiReset } from '../utils/appCleanup'

type ErrorBoundaryProps = {
  children: ReactNode
}

type ErrorBoundaryState = {
  hasError: boolean
  message: string
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = {
    hasError: false,
    message: '',
  }

  static getDerivedStateFromError(error: Error) {
    return {
      hasError: true,
      message: error.message,
    }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    if (import.meta.env.DEV) {
      console.error('Unhandled Ecosys ServiceOps error', error, errorInfo)
    }
  }

  render() {
    if (!this.state.hasError) {
      return this.props.children
    }

    return (
      <div className="min-h-dvh bg-app p-4 sm:p-6 lg:p-8">
        <div className="surface-card mx-auto max-w-2xl">
          <h1 className="text-2xl font-semibold text-app">Something went wrong</h1>
          <p className="mt-3 text-sm text-muted">
            The app hit an unexpected error. You can return to the dashboard or sign out and re-open your session.
          </p>
          {import.meta.env.DEV && this.state.message ? (
            <pre className="panel-subtle mt-4 overflow-x-auto rounded-2xl p-4 text-xs text-rose-200">{this.state.message}</pre>
          ) : null}
          <div className="mt-6 flex flex-wrap gap-3">
            <Link to="/dashboard" className="button-primary">
              Back to dashboard
            </Link>
            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                clearStoredAuth()
                clearTransientAppState()
                cleanupBodyInteractivity()
                dispatchUiReset()
                window.location.replace('/login')
              }}
            >
              Logout
            </button>
          </div>
        </div>
      </div>
    )
  }
}
