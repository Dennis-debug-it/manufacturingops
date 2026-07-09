import { Outlet } from 'react-router-dom'

export function PlatformLayout() {
  return (
    <div className="space-y-4"><Outlet /></div>
  )
}
