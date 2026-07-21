import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface BoardWorkOrder {
  id: string
  woNumber: string
  clientName: string
  siteAddress: string
  priority: string
  estimatedDurationMins: number
  scheduledStart: string | null
  scheduledEnd: string | null
  status: string
  description: string | null
  checklistCount: number
}

export type TechnicianBoardStatus = 'available' | 'on-job' | 'offline'

export interface BoardTechnician {
  id: string
  fullName: string
  status: TechnicianBoardStatus
  workOrders: BoardWorkOrder[]
}

export interface SchedulingBoard {
  technicians: BoardTechnician[]
  unassigned: BoardWorkOrder[]
}

const boardKey = (date: string, branchId?: string) => ['dispatch', 'board', date, branchId ?? null] as const

export function useSchedulingBoard(date: string, branchId?: string) {
  return useQuery({
    queryKey: boardKey(date, branchId),
    queryFn: () => api.get<SchedulingBoard>(endpoints.dispatch.board, { date, branchId }),
    staleTime: 15_000,
    refetchInterval: 30_000,
  })
}

export interface AssignWorkOrderPayload {
  workOrderId: string
  technicianId: string
  scheduledStart: string
  scheduledEnd: string
}

export interface UnassignWorkOrderPayload {
  workOrderId: string
}

export interface RescheduleWorkOrderPayload {
  workOrderId: string
  scheduledStart: string
  scheduledEnd: string
}

interface LocatedWorkOrder {
  workOrder: BoardWorkOrder
  sourceTechnicianId: string | null
}

function locateWorkOrder(board: SchedulingBoard, workOrderId: string): LocatedWorkOrder | null {
  const unassigned = board.unassigned.find((wo) => wo.id === workOrderId)
  if (unassigned) {
    return { workOrder: unassigned, sourceTechnicianId: null }
  }
  for (const technician of board.technicians) {
    const workOrder = technician.workOrders.find((wo) => wo.id === workOrderId)
    if (workOrder) {
      return { workOrder, sourceTechnicianId: technician.id }
    }
  }
  return null
}

function removeWorkOrder(board: SchedulingBoard, workOrderId: string): SchedulingBoard {
  return {
    unassigned: board.unassigned.filter((wo) => wo.id !== workOrderId),
    technicians: board.technicians.map((technician) => ({
      ...technician,
      workOrders: technician.workOrders.filter((wo) => wo.id !== workOrderId),
    })),
  }
}

const OPEN_STATUSES = new Set(['New', 'Open', 'Reopened'])

function insertIntoTechnician(board: SchedulingBoard, technicianId: string, workOrder: BoardWorkOrder): SchedulingBoard {
  return {
    ...board,
    technicians: board.technicians.map((technician) =>
      technician.id === technicianId
        ? { ...technician, workOrders: [...technician.workOrders, workOrder].sort((a, b) => (a.scheduledStart ?? '').localeCompare(b.scheduledStart ?? '')) }
        : technician,
    ),
  }
}

function invalidateBoard(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['dispatch', 'board'] })
}

export function useAssignBoardWorkOrder(date: string, branchId?: string) {
  const queryClient = useQueryClient()
  const key = boardKey(date, branchId)
  return useMutation({
    mutationFn: (payload: AssignWorkOrderPayload) => api.patch<BoardWorkOrder>(endpoints.dispatch.boardAssign, payload),
    onMutate: async (payload) => {
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<SchedulingBoard>(key)
      if (previous) {
        const located = locateWorkOrder(previous, payload.workOrderId)
        if (located) {
          const updated: BoardWorkOrder = {
            ...located.workOrder,
            scheduledStart: payload.scheduledStart,
            scheduledEnd: payload.scheduledEnd,
            status: OPEN_STATUSES.has(located.workOrder.status) ? 'Scheduled' : located.workOrder.status,
          }
          const next = insertIntoTechnician(removeWorkOrder(previous, payload.workOrderId), payload.technicianId, updated)
          queryClient.setQueryData(key, next)
        }
      }
      return { previous }
    },
    onError: (_error, _payload, context) => {
      if (context?.previous) {
        queryClient.setQueryData(key, context.previous)
      }
    },
    onSettled: () => invalidateBoard(queryClient),
  })
}

export function useUnassignBoardWorkOrder(date: string, branchId?: string) {
  const queryClient = useQueryClient()
  const key = boardKey(date, branchId)
  return useMutation({
    mutationFn: (payload: UnassignWorkOrderPayload) => api.patch<BoardWorkOrder>(endpoints.dispatch.boardUnassign, payload),
    onMutate: async (payload) => {
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<SchedulingBoard>(key)
      if (previous) {
        const located = locateWorkOrder(previous, payload.workOrderId)
        if (located) {
          const updated: BoardWorkOrder = {
            ...located.workOrder,
            scheduledStart: null,
            scheduledEnd: null,
            status: located.workOrder.status === 'Assigned' || located.workOrder.status === 'Scheduled' ? 'Open' : located.workOrder.status,
          }
          const next = removeWorkOrder(previous, payload.workOrderId)
          queryClient.setQueryData(key, { ...next, unassigned: [...next.unassigned, updated] })
        }
      }
      return { previous }
    },
    onError: (_error, _payload, context) => {
      if (context?.previous) {
        queryClient.setQueryData(key, context.previous)
      }
    },
    onSettled: () => invalidateBoard(queryClient),
  })
}

export function useRescheduleBoardWorkOrder(date: string, branchId?: string) {
  const queryClient = useQueryClient()
  const key = boardKey(date, branchId)
  return useMutation({
    mutationFn: (payload: RescheduleWorkOrderPayload) => api.patch<BoardWorkOrder>(endpoints.dispatch.boardReschedule, payload),
    onMutate: async (payload) => {
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<SchedulingBoard>(key)
      if (previous) {
        const next: SchedulingBoard = {
          unassigned: previous.unassigned,
          technicians: previous.technicians.map((technician) => ({
            ...technician,
            workOrders: technician.workOrders.map((wo) =>
              wo.id === payload.workOrderId
                ? { ...wo, scheduledStart: payload.scheduledStart, scheduledEnd: payload.scheduledEnd }
                : wo,
            ),
          })),
        }
        queryClient.setQueryData(key, next)
      }
      return { previous }
    },
    onError: (_error, _payload, context) => {
      if (context?.previous) {
        queryClient.setQueryData(key, context.previous)
      }
    },
    onSettled: () => invalidateBoard(queryClient),
  })
}
