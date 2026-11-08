import type { CrystalVariant } from '../components/Crystal'

const ROUTE_CRYSTAL: Array<[prefix: string, variant: CrystalVariant]> = [
  ['/dashboard', 'dashboard'],
  ['/work-orders', 'work-orders'],
  ['/inventory', 'assets'],
  ['/maintenance', 'assets'],
  ['/scheduling', 'command-centre'],
  ['/live-map', 'command-centre'],
  ['/reports', 'analytics'],
  ['/finance', 'analytics'],
]

export function crystalForScreen(pathname: string): CrystalVariant {
  const match = ROUTE_CRYSTAL.find(([prefix]) => pathname.startsWith(prefix))
  return match?.[1] ?? 'dashboard'
}
