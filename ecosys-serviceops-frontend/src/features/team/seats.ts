/** Which seat a role takes — the backend's SeatTypes: technicians take field seats, viewers are free, everyone else takes an office seat. */
export type SeatType = 'office' | 'field' | 'free'

export function seatTypeFor(role: string | null | undefined): SeatType {
  const normalized = (role ?? '').toLowerCase()
  if (normalized === 'technician') return 'field'
  if (normalized === 'viewer' || normalized === 'user') return 'free'
  return 'office'
}

/** Active accounts, pending invitations included, by the seat they take. */
export function countSeatsUsed(users: ReadonlyArray<{ role: string; isActive: boolean }>) {
  const active = users.filter((user) => user.isActive)
  return {
    office: active.filter((user) => seatTypeFor(user.role) === 'office').length,
    field: active.filter((user) => seatTypeFor(user.role) === 'field').length,
  }
}
