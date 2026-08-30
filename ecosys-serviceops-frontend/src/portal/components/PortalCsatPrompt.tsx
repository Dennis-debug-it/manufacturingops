import { useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Star } from 'lucide-react'
import { portalApi } from '../api/portalApiClient'
import { cn } from '../../shared/utils/cn'

interface PendingCsat {
  workOrderId: string
  workOrderNumber: string
  title: string
  sentAt: string | null
}

function RatingRow({ survey }: { survey: PendingCsat }) {
  const queryClient = useQueryClient()
  const [score, setScore] = useState(0)
  const [comment, setComment] = useState('')
  const [expanded, setExpanded] = useState(false)

  const submit = useMutation({
    mutationFn: () => portalApi.post(`/api/portal/csat/${survey.workOrderId}`, { score, comment: comment.trim() || null }),
    onSuccess: () => {
      toast.success('Thank you for your feedback!')
      void queryClient.invalidateQueries({ queryKey: ['portal', 'csat-pending'] })
    },
  })

  return (
    <li className="rounded-xl border border-slate-200 px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <span className="font-mono text-xs text-slate-500">{survey.workOrderNumber}</span>
          <div className="text-sm font-medium text-slate-900">{survey.title}</div>
        </div>
        <div className="flex items-center gap-1" role="radiogroup" aria-label={`Rate ${survey.workOrderNumber}`}>
          {[1, 2, 3, 4, 5].map((value) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={score === value}
              aria-label={`${value} star${value > 1 ? 's' : ''}`}
              onClick={() => {
                setScore(value)
                setExpanded(true)
              }}
              className="rounded p-0.5 transition hover:scale-110"
            >
              <Star className={cn('h-6 w-6', score >= value ? 'fill-amber-400 text-amber-400' : 'fill-slate-100 text-slate-300')} />
            </button>
          ))}
        </div>
      </div>
      {expanded ? (
        <div className="mt-3 flex flex-col gap-2 sm:flex-row">
          <input
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            maxLength={2000}
            placeholder="Any comments? (optional)"
            className="h-10 flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
          <button
            type="button"
            disabled={score === 0 || submit.isPending}
            onClick={() => submit.mutate()}
            className="inline-flex h-10 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50"
          >
            {submit.isPending ? 'Sending…' : 'Submit'}
          </button>
        </div>
      ) : null}
    </li>
  )
}

export function PortalCsatPrompt() {
  const { data } = useQuery({
    queryKey: ['portal', 'csat-pending'],
    queryFn: () => portalApi.get<PendingCsat[]>('/api/portal/csat/pending'),
    staleTime: 60_000,
  })

  if (!data || data.length === 0) {
    return null
  }

  return (
    <section className="rounded-2xl border border-amber-200 bg-amber-50 p-5">
      <h2 className="text-sm font-semibold text-slate-900">How did we do?</h2>
      <p className="mt-1 text-sm text-slate-600">Rate your recently completed work — it takes a few seconds.</p>
      <ul className="mt-3 space-y-2">
        {data.map((survey) => (
          <RatingRow key={survey.workOrderId} survey={survey} />
        ))}
      </ul>
    </section>
  )
}
