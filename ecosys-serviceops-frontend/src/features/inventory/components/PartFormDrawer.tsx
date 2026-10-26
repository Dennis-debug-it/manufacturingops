import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useCatalogueSuggestions, useCreateMaterial } from '../api'
import { useSuppliers } from '../purchaseOrdersApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

interface FormValues {
  itemCode: string
  itemName: string
  category: string
  unitOfMeasure: string
  quantityOnHand: string
  reorderLevel: string
  unitCost: string
  barcode: string
  preferredSupplierId: string
  reorderQuantity: string
}

/**
 * The fields the server is allowed to blame. A 400 whose message names anything else keeps the
 * interceptor's toast rather than being pinned to an unrelated input.
 */
const FORM_FIELDS = [
  'itemCode',
  'itemName',
  'category',
  'unitOfMeasure',
  'quantityOnHand',
  'reorderLevel',
  'unitCost',
  'barcode',
  'preferredSupplierId',
  'reorderQuantity',
] as const satisfies readonly (keyof FormValues)[]

export function PartFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    defaultValues: { itemCode: '', itemName: '', category: '', unitOfMeasure: 'unit', quantityOnHand: '0', reorderLevel: '0', unitCost: '', barcode: '', preferredSupplierId: '', reorderQuantity: '' },
  })
  const createMaterial = useCreateMaterial()
  const { categories, units } = useCatalogueSuggestions()
  const { data: suppliers } = useSuppliers()

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
        barcode: values.barcode.trim() || null,
        preferredSupplierId: values.preferredSupplierId || null,
        reorderQuantity: values.reorderQuantity ? Number(values.reorderQuantity) : null,
      })
      toast.success(`Part "${created.itemName}" created`)
      close()
    } catch (error) {
      // A model-validation 400 names the field it rejected; pin the message to that input instead
      // of leaving the user with only the interceptor's one-line toast. Anything the helper can't
      // place stays with that toast.
      applyApiFieldErrors<FormValues>(error, { setError, fields: FORM_FIELDS })
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
          <button type="submit" form="part-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
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
            {errors.itemCode ? <p className={errorClass}>{errors.itemCode.message ?? 'Required'}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="part-uom">Unit of Measure</label>
            <input id="part-uom" list="part-unit-options" {...register('unitOfMeasure')} className={fieldClass} />
            <datalist id="part-unit-options">{units.map((unit) => <option key={unit} value={unit} />)}</datalist>
            {errors.unitOfMeasure?.message ? <p className={errorClass}>{errors.unitOfMeasure.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="part-name">Item Name</label>
          <input id="part-name" {...register('itemName', { required: true })} className={fieldClass} />
          {errors.itemName ? <p className={errorClass}>{errors.itemName.message ?? 'Required'}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="part-category">Category</label>
          <input id="part-category" list="part-category-options" {...register('category')} className={fieldClass} />
          <datalist id="part-category-options">{categories.map((category) => <option key={category} value={category} />)}</datalist>
          {errors.category?.message ? <p className={errorClass}>{errors.category.message}</p> : null}
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className={labelClass} htmlFor="part-qty">Initial Qty</label>
            <input id="part-qty" type="number" step="0.01" {...register('quantityOnHand')} className={fieldClass} />
            {errors.quantityOnHand?.message ? <p className={errorClass}>{errors.quantityOnHand.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="part-reorder">Reorder Level</label>
            <input id="part-reorder" type="number" step="0.01" {...register('reorderLevel')} className={fieldClass} />
            {errors.reorderLevel?.message ? <p className={errorClass}>{errors.reorderLevel.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="part-cost">Unit Cost</label>
            <input id="part-cost" type="number" step="0.01" {...register('unitCost')} className={fieldClass} />
            {errors.unitCost?.message ? <p className={errorClass}>{errors.unitCost.message}</p> : null}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="part-barcode">Barcode / MPN</label>
            <input id="part-barcode" {...register('barcode')} className={fieldClass} />
            {errors.barcode?.message ? <p className={errorClass}>{errors.barcode.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="part-reorder-qty">Reorder Qty</label>
            <input id="part-reorder-qty" type="number" min="0" step="0.01" placeholder="Auto" {...register('reorderQuantity')} className={fieldClass} />
            {errors.reorderQuantity?.message ? <p className={errorClass}>{errors.reorderQuantity.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="part-supplier">Preferred Supplier</label>
          <select id="part-supplier" {...register('preferredSupplierId')} className={fieldClass}>
            <option value="">None</option>
            {(suppliers ?? []).filter((supplier) => supplier.status === 'Active').map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>)}
          </select>
          {errors.preferredSupplierId?.message ? <p className={errorClass}>{errors.preferredSupplierId.message}</p> : null}
          <p className="mt-1 text-xs text-slate-500">Reorder suggestions draft purchase orders to this supplier.</p>
        </div>
      </form>
    </FormDrawer>
  )
}
