import type { ReactNode } from 'react'
import { Group, Panel, Separator } from 'react-resizable-panels'
import type { Layout } from 'react-resizable-panels'

export type PanelLayout = Layout

export function PanelHandle({ className = '' }: { className?: string }) {
  return (
    <Separator
      className={`workspace-panel-handle group relative flex w-3 items-stretch justify-center focus-visible:outline-none ${className}`.trim()}
    >
      <span className="workspace-panel-handle-line my-2 w-px rounded-full" />
      <span className="workspace-panel-handle-dot absolute left-1/2 top-1/2 h-6 w-1 -translate-x-1/2 -translate-y-1/2 rounded-full opacity-0 transition group-hover:opacity-100" />
    </Separator>
  )
}

export function WorkspacePanelGroup({
  children,
  className = '',
  direction = 'horizontal',
  defaultLayout,
  onLayout,
}: {
  children: ReactNode
  className?: string
  direction?: 'horizontal' | 'vertical'
  defaultLayout?: Layout
  onLayout?: (layout: Layout) => void
}) {
  return (
    <Group
      orientation={direction === 'horizontal' ? 'horizontal' : 'vertical'}
      className={className}
      defaultLayout={defaultLayout}
      onLayoutChange={onLayout}
    >
      {children}
    </Group>
  )
}

export { Panel }
