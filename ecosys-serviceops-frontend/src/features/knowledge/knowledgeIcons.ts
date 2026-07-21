import {
  BookOpen,
  Wrench,
  ShieldCheck,
  Package,
  Wifi,
  Zap,
  Droplets,
  Thermometer,
  AlertTriangle,
  FileText,
  Settings2,
  LifeBuoy,
  ClipboardList,
  CreditCard,
  Users,
  type LucideIcon,
} from 'lucide-react'

export const knowledgeIconOptions: { name: string; icon: LucideIcon }[] = [
  { name: 'BookOpen', icon: BookOpen },
  { name: 'Wrench', icon: Wrench },
  { name: 'ShieldCheck', icon: ShieldCheck },
  { name: 'Package', icon: Package },
  { name: 'Wifi', icon: Wifi },
  { name: 'Zap', icon: Zap },
  { name: 'Droplets', icon: Droplets },
  { name: 'Thermometer', icon: Thermometer },
  { name: 'AlertTriangle', icon: AlertTriangle },
  { name: 'FileText', icon: FileText },
  { name: 'Settings2', icon: Settings2 },
  { name: 'LifeBuoy', icon: LifeBuoy },
  { name: 'ClipboardList', icon: ClipboardList },
  { name: 'CreditCard', icon: CreditCard },
  { name: 'Users', icon: Users },
]

const knowledgeIconMap = new Map(knowledgeIconOptions.map((option) => [option.name, option.icon]))

export function resolveKnowledgeIcon(name: string | null | undefined): LucideIcon {
  return (name && knowledgeIconMap.get(name)) || BookOpen
}
