import { Link } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { GuideContent } from './HelpPage'
import { technicianGuide } from './guides'

/** The technician app's own how-to, reached from Guides and Me. */
export function TechHelpPage() {
  return (
    <div className="space-y-3">
      <Link to="/tech" className="inline-flex items-center gap-1 text-sm text-slate-600"><ChevronLeft className="h-4 w-4" />Today</Link>
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <GuideContent guide={technicianGuide} />
      </div>
    </div>
  )
}
