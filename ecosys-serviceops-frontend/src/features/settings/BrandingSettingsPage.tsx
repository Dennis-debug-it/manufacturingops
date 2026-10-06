import { useRef, useState } from 'react'
import toast from 'react-hot-toast'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ImageUp } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { usePermission } from '../../shared/hooks/usePermission'
import { SettingsForm } from './components/SettingsForm'

const ACCEPTED_TYPES = '.png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp'
const MAX_BYTES = 10_000_000

function LogoUploader() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const { data } = useQuery({
    queryKey: ['settings', 'branding'],
    queryFn: () => api.get<Record<string, unknown>>('/api/settings/branding'),
    staleTime: 30_000,
  })
  const logoUrl = typeof data?.logoUrl === 'string' && data.logoUrl ? data.logoUrl : null

  const upload = async (file: File) => {
    if (file.size > MAX_BYTES) {
      toast.error('Logo must be smaller than 10 MB')
      return
    }
    setUploading(true)
    try {
      const formData = new FormData()
      formData.append('file', file)
      await api.post<{ logoUrl: string }>(endpoints.tenant.brandingLogo, formData)
      await queryClient.invalidateQueries({ queryKey: ['settings', 'branding'] })
      toast.success('Logo uploaded. It will appear in the sidebar the next time you sign in.')
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setUploading(false)
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <h2 className="text-sm font-semibold text-slate-900">Company Logo</h2>
      <p className="mt-1 text-sm text-slate-500">Shown on documents, emails, the client portal and the sign-in screen. PNG, JPG or WebP.</p>
      <div className="mt-4 flex flex-wrap items-center gap-4">
        <div className="flex h-20 w-40 items-center justify-center overflow-hidden rounded-lg border border-dashed border-slate-300 bg-slate-50">
          {logoUrl ? <img src={logoUrl} alt="Company logo" className="max-h-full max-w-full object-contain" /> : <span className="text-xs text-slate-400">No logo yet</span>}
        </div>
        {canManage ? (
          <>
            <input
              ref={inputRef}
              type="file"
              accept={ACCEPTED_TYPES}
              className="hidden"
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) void upload(file)
              }}
            />
            <button
              type="button"
              onClick={() => inputRef.current?.click()}
              disabled={uploading}
              className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
            >
              <ImageUp className="h-4 w-4" />
              {uploading ? 'Uploading…' : logoUrl ? 'Replace Logo' : 'Upload Logo'}
            </button>
          </>
        ) : null}
      </div>
    </section>
  )
}

export function BrandingSettingsPage() {
  return (
    <div className="space-y-4">
      <SettingsForm
        title="Branding"
        subtitle="Logo, primary colour, document, and email branding."
        queryKey="branding"
        fetchUrl="/api/settings/branding"
        saveUrl="/api/settings/branding"
        fields={[
          { key: 'primaryColor', label: 'Primary Colour', type: 'color' },
          { key: 'secondaryColor', label: 'Secondary Colour', type: 'color' },
          { key: 'showPoweredByEcosys', label: 'Show "Powered by Ecosys"', type: 'checkbox' },
        ]}
      />
      <LogoUploader />
    </div>
  )
}
