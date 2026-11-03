import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PauseCircle } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { useContinueWithoutContract, useGenerateWorkOrderNow } from '../api'
import { PM_CONTRACT_LAPSED, type PmPlan } from '../types'

type Pending = 'chargeable' | 'continue' | 'hold' | null

const buttonClass = 'inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'

/**
 * A schedule whose asset's contract has lapsed raises no free visits. This says so, and offers the
 * ways forward: renew the contract (the schedule resumes by itself), raise a one-off chargeable
 * visit, or keep the schedule going without a contract.
 */
export function ContractLapsePanel({ plan }: { plan: PmPlan }) {
  const canManage = usePermission('maintenance.manage')
  const canTrigger = usePermission('maintenance.trigger')
  const generate = useGenerateWorkOrderNow(plan.id)
  const setContinue = useContinueWithoutContract(plan.id)
  const [pending, setPending] = useState<Pending>(null)
  const held = plan.status === PM_CONTRACT_LAPSED

  if (!held && !plan.continueWithoutContract) return null

  const confirm = async () => {
    const action = pending
    setPending(null)
    try {
      if (action === 'chargeable') {
        const wo = await generate.mutateAsync({ chargeable: true })
        toast.success(`Chargeable visit ${wo.workOrderNumber} raised`)
      } else if (action === 'continue') {
        await setContinue.mutateAsync(true)
        toast.success('Schedule continues without a contract')
      } else if (action === 'hold') {
        await setContinue.mutateAsync(false)
        toast.success('Schedule will hold when the contract lapses')
      }
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const dialog = {
    chargeable: {
      title: 'Raise a chargeable visit?',
      message: "This raises one PM work order now, outside contract. It is priced at the client's standard rates. The schedule stays on hold.",
      confirmLabel: 'Raise visit',
    },
    continue: {
      title: 'Continue without a contract?',
      message: "The schedule comes off hold and keeps raising visits on its cycle. Each visit is marked chargeable and priced at the client's standard rates. Use this when the client has agreed to pay per visit.",
      confirmLabel: 'Continue without contract',
    },
    hold: {
      title: 'Hold when the contract lapses?',
      message: "The schedule will stop raising visits whenever the asset isn't under contract, and resume when it is.",
      confirmLabel: 'Hold on lapse',
    },
  }

  return (
    <>
      {held ? (
        <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <div className="flex items-start gap-2">
            <PauseCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="font-semibold">PM on hold: the asset's contract has lapsed</p>
              <p className="mt-1">{plan.holdReason ?? 'No contract covers this asset, so no visit was raised.'}</p>
              {plan.heldAt ? <p className="mt-1 text-xs text-amber-800">On hold since {formatDate(plan.heldAt)}.</p> : null}
              <div className="mt-3 flex flex-wrap gap-2">
                {plan.lapsedContractId ? (
                  <Link to={`/contracts/${plan.lapsedContractId}`} className={buttonClass}>Renew the contract</Link>
                ) : null}
                {canTrigger ? (
                  <button type="button" className={buttonClass} disabled={generate.isPending} onClick={() => setPending('chargeable')}>Raise chargeable visit</button>
                ) : null}
                {canManage ? (
                  <button type="button" className={buttonClass} disabled={setContinue.isPending} onClick={() => setPending('continue')}>Continue without contract</button>
                ) : null}
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
          <p>Continues without a contract: if the asset isn't under contract, visits are still raised and are chargeable.</p>
          {canManage ? (
            <button type="button" className={buttonClass} disabled={setContinue.isPending} onClick={() => setPending('hold')}>Hold on contract lapse</button>
          ) : null}
        </div>
      )}
      <ConfirmDialog
        open={pending !== null}
        title={pending ? dialog[pending].title : ''}
        message={pending ? dialog[pending].message : ''}
        confirmLabel={pending ? dialog[pending].confirmLabel : 'Confirm'}
        onCancel={() => setPending(null)}
        onConfirm={() => void confirm()}
      />
    </>
  )
}
