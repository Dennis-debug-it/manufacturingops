import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useCreateMaterial } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface FormValues {
  itemCode: string
  itemName: string
  category: string
  unitOfMeasure: string
  quantityOnHand: string
  reorderLevel: string
  unitCost: string
}

export function PartFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<FormValues>({
    defaultValues: { itemCode: '', itemName: '', category: '', unitOfMeasure: 'unit', quantityOnHand: '0', reorderLevel: '0', unitCost: '' },
  })
  const createMaterial = useCreateMaterial()

  const close = () => { reset(); onClose() }

  const onSubmit = handleSubmit(async (values) => {
    if (!values.itemCode.trim() || !values.itemName.trim()) {
      toast.error('Item code and name are required')
      return
    }
    try {
      const created = await createMaterial.mutateAsync({
        itemCode: values.itemCode.trim(),
        itemName: values.itemName.trim(),
        category: values.category.trim() || null,
        unitOfMeasure: values.unitOfMeasure.trim() || 'unit',
        quantityOnHand: Number(values.quantityOnHand) || 0,
        reorderLevel: Number(values.reorderLevel) || 0,
        unitCost: values.unitCost ? Number(values.unitCost) : null,
      })
      toast.success(`Part "${created.itemName}" created`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  })

  return (
    <FormDrawer
      open={open}
      title="New Part"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="part-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {isSubmitting ? 'Creating…' : 'Create Part'}
          </button>
        </div>
      }
    >
      <form id="part-form" onSubmit={onSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="part-code">Item Code</label>
            <input id="part-code" {...register('itemCode', { required: true })} className={fieldClass} />
            {errors.itemCode ? <p className="mt-1 text-xs text-rose-600">Required</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="part-uom">Unit of Measure</label>
            <input id="part-uom" {...register('unitOfMeasure')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="part-name">Item Name</label>
          <input id="part-name" {...register('itemName', { required: true })} className={fieldClass} />
          {errors.itemName ? <p className="mt-1 text-xs text-rose-600">Required</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="part-category">Category</label>
          <input id="part-category" {...register('category')} className={fieldClass} />
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className={labelClass} htmlFor="part-qty">Initial Qty</label>
            <input id="part-qty" type="number" step="0.01" {...register('quantityOnHand')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="part-reorder">Reorder Level</label>
            <input id="part-reorder" type="number" step="0.01" {...register('reorderLevel')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="part-cost">Unit Cost</label>
            <input id="part-cost" type="number" step="0.01" {...register('unitCost')} className={fieldClass} />
          </div>
        </div>
      </form>
    </FormDrawer>
  )
}
