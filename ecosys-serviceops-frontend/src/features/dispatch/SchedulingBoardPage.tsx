import { useMemo, useState } from 'react'
import { format } from 'date-fns'
import toast from 'react-hot-toast'
import {
  DndContext,
  PointerSensor,
  pointerWithin,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragMoveEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import {
  useAssignBoardWorkOrder,
  useRescheduleBoardWorkOrder,
  useSchedulingBoard,
  useUnassignBoardWorkOrder,
  type BoardWorkOrder,
} from './boardApi'
import { WorkOrderDispatchDrawer } from '../work-orders/components/WorkOrderDispatchDrawer'
import { UnassignedPanel } from './components/UnassignedPanel'
import { TechnicianRow, type DragPreview } from './components/TechnicianRow'
import {
  clampSlotIndex,
  hasOverlap,
  ROW_HEIGHT,
  SLOT_COUNT,
  SLOT_WIDTH,
  slotIndexToDate,
  slotLabel,
} from './gridUtils'

const PRIORITIES = ['Critical', 'High', 'Medium', 'Low'] as const

interface ActiveDrag {
  workOrder: BoardWorkOrder
  sourceTechnicianId: string | null
}

function computeSlotFromEvent(event: DragMoveEvent | DragEndEvent): { technicianId: string; slotIndex: number } | null {
  const overId = event.over?.id
  if (typeof overId !== 'string' || !overId.startsWith('row:')) {
    return null
  }
  const technicianId = overId.slice('row:'.length)
  const rowRect = event.over?.rect
  const activeRect = event.active.rect.current.translated ?? event.active.rect.current.initial
  if (!rowRect || !activeRect) {
    return { technicianId, slotIndex: 0 }
  }
  const relativeLeft = activeRect.left - rowRect.left
  const slotIndex = clampSlotIndex(Math.round(relativeLeft / SLOT_WIDTH))
  return { technicianId, slotIndex }
}

export function SchedulingBoardPage() {
  const [selectedDate, setSelectedDate] = useState(() => new Date())
  const [stepMode, setStepMode] = useState<'day' | 'week'>('day')
  const [priorityFilter, setPriorityFilter] = useState<string | null>(null)
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null)
  const [dragPreview, setDragPreview] = useState<DragPreview | null>(null)
  // Scheduling a job and dispatching it are different steps: the board assigns and times the visit,
  // the drawer records the technician's response and site milestones that Start/Complete depend on.
  const [dispatchWorkOrderId, setDispatchWorkOrderId] = useState<string | null>(null)

  const dateParam = format(selectedDate, 'yyyy-MM-dd')
  const { data, isLoading, isError } = useSchedulingBoard(dateParam)
  const assignMutation = useAssignBoardWorkOrder(dateParam)
  const unassignMutation = useUnassignBoardWorkOrder(dateParam)
  const rescheduleMutation = useRescheduleBoardWorkOrder(dateParam)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }))

  const technicians = useMemo(() => {
    const list = data?.technicians ?? []
    if (!priorityFilter) return list
    return list.map((technician) => ({
      ...technician,
      workOrders: technician.workOrders.filter((wo) => wo.priority === priorityFilter),
    }))
  }, [data, priorityFilter])

  const unassigned = useMemo(() => {
    const list = data?.unassigned ?? []
    return priorityFilter ? list.filter((wo) => wo.priority === priorityFilter) : list
  }, [data, priorityFilter])

  const step = (direction: -1 | 1) => {
    setSelectedDate((current) => {
      const next = new Date(current)
      next.setDate(next.getDate() + direction * (stepMode === 'week' ? 7 : 1))
      return next
    })
  }

  const handleDragStart = (event: DragStartEvent) => {
    const dragData = event.active.data.current as ActiveDrag | undefined
    if (dragData) {
      setActiveDrag(dragData)
    }
  }

  const handleDragMove = (event: DragMoveEvent) => {
    if (!activeDrag) return
    const slot = computeSlotFromEvent(event)
    if (!slot) {
      setDragPreview(null)
      return
    }
    const technician = data?.technicians.find((t) => t.id === slot.technicianId)
    if (!technician) {
      setDragPreview(null)
      return
    }
    const slotSpan = Math.max(activeDrag.workOrder.estimatedDurationMins / 30, 0.75)
    const start = slotIndexToDate(selectedDate, slot.slotIndex)
    const end = new Date(start.getTime() + activeDrag.workOrder.estimatedDurationMins * 60_000)
    const valid = !hasOverlap(technician.workOrders, start, end, activeDrag.workOrder.id)
    setDragPreview({ technicianId: slot.technicianId, slotIndex: slot.slotIndex, slotSpan, valid })
  }

  const resetDragState = () => {
    setActiveDrag(null)
    setDragPreview(null)
  }

  const handleDragEnd = (event: DragEndEvent) => {
    const drag = event.active.data.current as ActiveDrag | undefined
    if (!drag) {
      resetDragState()
      return
    }

    const overId = event.over?.id
    if (overId === 'unassigned-panel') {
      if (drag.sourceTechnicianId) {
        unassignMutation.mutate({ workOrderId: drag.workOrder.id })
      }
      resetDragState()
      return
    }

    const slot = computeSlotFromEvent(event)
    if (!slot) {
      resetDragState()
      return
    }

    const technician = data?.technicians.find((t) => t.id === slot.technicianId)
    if (!technician) {
      resetDragState()
      return
    }

    const start = slotIndexToDate(selectedDate, slot.slotIndex)
    const end = new Date(start.getTime() + drag.workOrder.estimatedDurationMins * 60_000)

    if (hasOverlap(technician.workOrders, start, end, drag.workOrder.id)) {
      toast.error('That technician already has a job scheduled at that time.')
      resetDragState()
      return
    }

    if (drag.sourceTechnicianId === slot.technicianId) {
      rescheduleMutation.mutate({
        workOrderId: drag.workOrder.id,
        scheduledStart: start.toISOString(),
        scheduledEnd: end.toISOString(),
      })
    } else {
      assignMutation.mutate({
        workOrderId: drag.workOrder.id,
        technicianId: slot.technicianId,
        scheduledStart: start.toISOString(),
        scheduledEnd: end.toISOString(),
      })
    }

    resetDragState()
  }

  return (
    <div className="flex h-full flex-col gap-4">
      <PageHeader
        title="Scheduling Board"
        subtitle="Drag work orders onto a technician's timeline to assign and schedule them."
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-1 rounded-control border border-slate-200 bg-white px-1 py-1">
              <button type="button" onClick={() => step(-1)} className="rounded-lg p-1.5 hover:bg-slate-100" aria-label="Previous">
                <ChevronLeft className="h-4 w-4" />
              </button>
              <span className="min-w-[9rem] px-1 text-center text-sm font-medium text-slate-900">{format(selectedDate, 'EEE, d MMM yyyy')}</span>
              <button type="button" onClick={() => step(1)} className="rounded-lg p-1.5 hover:bg-slate-100" aria-label="Next">
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
            <button
              type="button"
              onClick={() => setSelectedDate(new Date())}
              className="inline-flex h-9 items-center rounded-control border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Today
            </button>
            <div className="flex items-center gap-1 rounded-control border border-slate-200 bg-white px-1 py-1">
              {(['day', 'week'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setStepMode(option)}
                  className={cn(
                    'rounded-lg px-2.5 py-1 text-xs font-medium capitalize transition',
                    stepMode === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100',
                  )}
                >
                  {option}
                </button>
              ))}
            </div>
            <select
              value={priorityFilter ?? ''}
              onChange={(event) => setPriorityFilter(event.target.value || null)}
              className="h-9 rounded-control border border-slate-200 bg-white px-2 text-sm text-slate-700"
            >
              <option value="">All priorities</option>
              {PRIORITIES.map((priority) => (
                <option key={priority} value={priority}>
                  {priority}
                </option>
              ))}
            </select>
          </div>
        }
      />

      {isLoading ? (
        <Spinner label="Loading scheduling board" />
      ) : isError ? (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">
          The scheduling board could not be loaded.
        </div>
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={pointerWithin}
          onDragStart={handleDragStart}
          onDragMove={handleDragMove}
          onDragEnd={handleDragEnd}
          onDragCancel={resetDragState}
        >
          {/* The app shell does not bound page height, so the board sets its own. Without this the
              unassigned list and technician rows stretched the page to fit every card, turning the
              board into one endless scroll. Each column now scrolls inside the viewport instead. */}
          <div className="flex h-[calc(100dvh-13rem)] min-h-[28rem] flex-col gap-4 lg:flex-row">
            <div className="h-64 w-full shrink-0 lg:h-full lg:max-w-xs">
              <UnassignedPanel workOrders={unassigned} isDraggingAssigned={Boolean(activeDrag?.sourceTechnicianId)} />
            </div>

            <div className="min-h-0 min-w-0 flex-1 overflow-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
              <div style={{ width: 224 + SLOT_COUNT * SLOT_WIDTH }}>
                <div className="sticky top-0 z-30 flex border-b border-slate-200 bg-slate-50">
                  <div className="sticky left-0 z-20 w-56 shrink-0 border-r border-slate-200 bg-slate-50 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    Technician
                  </div>
                  <div className="relative shrink-0" style={{ width: SLOT_COUNT * SLOT_WIDTH, height: ROW_HEIGHT / 2 }}>
                    {Array.from({ length: SLOT_COUNT }).map((_, index) =>
                      index % 2 === 0 ? (
                        <span
                          key={index}
                          className="absolute top-1/2 -translate-y-1/2 text-[11px] font-medium text-slate-500"
                          style={{ left: index * SLOT_WIDTH + 4 }}
                        >
                          {slotLabel(index)}
                        </span>
                      ) : null,
                    )}
                  </div>
                </div>
                {technicians.length === 0 ? (
                  <div className="p-8 text-center text-sm text-slate-500">No technicians available for this branch.</div>
                ) : (
                  technicians.map((technician) => (
                    <TechnicianRow
                      key={technician.id}
                      technician={technician}
                      day={selectedDate}
                      preview={dragPreview}
                      onOpenDispatch={(workOrder) => setDispatchWorkOrderId(workOrder.id)}
                    />
                  ))
                )}
              </div>
            </div>
          </div>
        </DndContext>
      )}

      <WorkOrderDispatchDrawer workOrderId={dispatchWorkOrderId} onClose={() => setDispatchWorkOrderId(null)} />
    </div>
  )
}
