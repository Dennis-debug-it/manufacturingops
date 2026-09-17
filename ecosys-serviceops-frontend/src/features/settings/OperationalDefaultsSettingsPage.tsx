import { SettingsForm } from './components/SettingsForm'

export function OperationalDefaultsSettingsPage() {
  return (
    <SettingsForm
      title="Operational Defaults"
      subtitle="Default rules new work orders follow across the tenant. Individual work orders can still be handled differently by the technician's own workflow."
      queryKey="operations-policy"
      fetchUrl="/api/settings/operations-policy"
      saveUrl="/api/settings/operations-policy"
      defaultValues={{
        requireTechnicianSignature: true,
        requireClientSignature: true,
        allowRemoteCompletion: false,
      }}
      fields={[
        { key: 'requireTechnicianSignature', label: 'Require technician signature to complete a work order', type: 'checkbox' },
        { key: 'requireClientSignature', label: 'Require client signature to complete a work order', type: 'checkbox' },
        { key: 'allowRemoteCompletion', label: 'Allow work orders to be completed remotely (no on-site check-in)', type: 'checkbox' },
      ]}
    />
  )
}
