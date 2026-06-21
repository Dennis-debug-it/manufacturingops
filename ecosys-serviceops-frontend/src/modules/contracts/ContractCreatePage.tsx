import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { contractService } from '../../services/contractService'
import { userService } from '../../services/userService'
import type { UpsertContractInput } from '../../types/api'
import { ContractEditor } from './ContractEditor'

export function ContractCreatePage() {
  const navigate = useNavigate()
  const { pushToast } = useToast()
  const [busy, setBusy] = useState(false)
  const { data: clients } = useAsyncData((signal) => clientService.list({ status: 'active', signal }), [], [])
  const { data: users } = useAsyncData((signal) => userService.list(signal), [], [])

  async function handleSubmit(input: UpsertContractInput) {
    setBusy(true)
    try {
      const created = await contractService.create(input)
      pushToast({ title: 'Contract created', description: 'The contract is ready for coverage, rates, and lifecycle management.', tone: 'success' })
      navigate(`/contracts/${created.id}`)
    } catch (error) {
      pushToast({ title: 'Create failed', description: error instanceof Error ? error.message : 'Unable to create the contract.', tone: 'danger' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Contracts"
        title="New contract"
        description="Create the core record first, then refine reminders, rates, and renewal controls on the detail page."
        actions={<Link to="/contracts" className="button-secondary"><ArrowLeft className="h-4 w-4" />Back to contracts</Link>}
      />
      <ContractEditor clients={clients} users={users} onSubmit={handleSubmit} submitLabel="Create contract" busy={busy} />
    </div>
  )
}
