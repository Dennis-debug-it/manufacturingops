import type { ErrorInfo, ReactNode } from 'react'
import React from 'react'

export interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
}

export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  public state: ErrorBoundaryState = { hasError: false }

  public static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true }
  }

  public componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled React error', error, info)
  }

  public render() {
    if (this.state.hasError) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-slate-50 px-6">
          <div className="max-w-xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">Application error</p>
            <h1 className="mt-2 text-2xl font-semibold text-slate-900">We hit a render problem</h1>
            <p className="mt-2 text-sm text-slate-600">
              The page could not render correctly. Try reloading the page or return to the dashboard.
            </p>
            <button
              type="button"
              className="mt-6 inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
              onClick={() => this.setState({ hasError: false })}
            >
              Retry
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
