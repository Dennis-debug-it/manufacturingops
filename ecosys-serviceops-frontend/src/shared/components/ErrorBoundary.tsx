import type { ErrorInfo, ReactNode } from 'react'
import React from 'react'
import { recordBootError, resetApp } from '../../app/bootGuard'

export interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
  message: string | null
}

export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  public state: ErrorBoundaryState = { hasError: false, message: null }

  public static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    return { hasError: true, message: error instanceof Error ? error.message : String(error) }
  }

  public componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled React error', error, info)
    recordBootError(error)
  }

  public render() {
    if (this.state.hasError) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-slate-50 px-6">
          <div className="max-w-xl rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-6 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">Application error</p>
            <h1 className="mt-2 text-2xl font-semibold text-slate-900">We hit a render problem</h1>
            <p className="mt-2 text-sm text-slate-600">
              The page could not render correctly. Try reloading the page or return to the dashboard.
            </p>
            {/* Shown so a screenshot of this page says what actually failed. */}
            {this.state.message ? <p className="mt-2 break-words text-xs text-rose-700">{this.state.message}</p> : null}
            <button
              type="button"
              className="mt-6 inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
              onClick={() => this.setState({ hasError: false, message: null })}
            >
              Retry
            </button>
            <button
              type="button"
              className="ml-2 mt-6 inline-flex h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700"
              onClick={() => void resetApp()}
            >
              Reset app
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
