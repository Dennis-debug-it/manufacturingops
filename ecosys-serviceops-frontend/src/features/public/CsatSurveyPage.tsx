import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Star } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'

interface CsatSurveyInfo {
  companyName: string
  workOrderNumber: string
  alreadySubmitted: boolean
}

const scoreLabels = ['Very dissatisfied', 'Dissatisfied', 'Neutral', 'Satisfied', 'Very satisfied']

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-xl rounded-3xl border border-slate-200 bg-white p-8 shadow-sm">{children}</div>
    </div>
  )
}

export function CsatSurveyPage() {
  const { token } = useParams<{ token: string }>()
  const [score, setScore] = useState(0)
  const [hovered, setHovered] = useState(0)
  const [comment, setComment] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [submitError, setSubmitError] = useState('')

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-csat', token],
    queryFn: () => api.get<CsatSurveyInfo>(`/api/public/csat/${token}`),
    enabled: Boolean(token),
    retry: false,
  })

  const submit = useMutation({
    mutationFn: () => api.post(`/api/public/csat/${token}`, { score, comment: comment.trim() || null }),
    onSuccess: () => setSubmitted(true),
    onError: (error: unknown) => {
      setSubmitError(resolveErrorMessage(error))
    },
  })

  if (isLoading) {
    return (
      <Shell>
        <Spinner label="Loading survey" />
      </Shell>
    )
  }

  if (isError || !data) {
    return (
      <Shell>
        <h1 className="text-xl font-semibold text-slate-900">Survey unavailable</h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">This survey link is invalid, expired, or has already been used. If you believe this is a mistake, please contact your service provider.</p>
      </Shell>
    )
  }

  if (submitted || data.alreadySubmitted) {
    return (
      <Shell>
        <h1 className="text-xl font-semibold text-slate-900">Thank you!</h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">
          Your feedback on work order <span className="font-mono">{data.workOrderNumber}</span> has been recorded. {data.companyName} appreciates you taking the time.
        </p>
      </Shell>
    )
  }

  return (
    <Shell>
      <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{data.companyName}</div>
      <h1 className="mt-2 text-xl font-semibold text-slate-900">How did we do?</h1>
      <p className="mt-1 text-sm leading-6 text-slate-600">
        Work order <span className="font-mono">{data.workOrderNumber}</span> has been completed. Your rating takes less than a minute and helps us improve.
      </p>

      <div className="mt-6">
        <div className="flex items-center gap-2" role="radiogroup" aria-label="Rating">
          {[1, 2, 3, 4, 5].map((value) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={score === value}
              aria-label={`${value} star${value > 1 ? 's' : ''} — ${scoreLabels[value - 1]}`}
              onClick={() => setScore(value)}
              onMouseEnter={() => setHovered(value)}
              onMouseLeave={() => setHovered(0)}
              className="rounded-lg p-1 transition hover:scale-110"
            >
              <Star
                className={cn(
                  'h-9 w-9 transition',
                  (hovered || score) >= value ? 'fill-amber-400 text-amber-400' : 'fill-slate-100 text-slate-300',
                )}
              />
            </button>
          ))}
        </div>
        <div className="mt-2 h-5 text-sm text-slate-600">{score > 0 ? scoreLabels[score - 1] : hovered > 0 ? scoreLabels[hovered - 1] : ''}</div>
      </div>

      <div className="mt-4">
        <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="csat-comment">Anything you'd like to add? (optional)</label>
        <textarea
          id="csat-comment"
          value={comment}
          onChange={(event) => setComment(event.target.value)}
          rows={4}
          maxLength={2000}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-slate-900"
        />
      </div>

      {submitError ? <div className="mt-3 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{submitError}</div> : null}

      <button
        type="button"
        disabled={score === 0 || submit.isPending}
        onClick={() => {
          setSubmitError('')
          submit.mutate()
        }}
        className="mt-6 inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {submit.isPending ? 'Submitting…' : 'Submit Feedback'}
      </button>
    </Shell>
  )
}
