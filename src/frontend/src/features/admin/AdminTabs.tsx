import { NavLink } from 'react-router-dom'

const tabs = [
  { to: '/admin/users', label: 'Users' },
  { to: '/admin/feedback', label: 'Feedback' },
]

/**
 * Navigation between the admin screens. Kept here rather than in the main nav so
 * the mobile tab bar doesn't grow an item per admin page.
 */
export default function AdminTabs() {
  return (
    <nav aria-label="Admin sections" className="flex gap-1 rounded-xl border border-line bg-raised p-1">
      {tabs.map((t) => (
        <NavLink
          key={t.to}
          to={t.to}
          className={({ isActive }) =>
            `flex-1 rounded-lg px-3 py-2 text-center text-sm font-medium transition-colors ${
              isActive
                ? 'bg-card text-brand-700 shadow-sm dark:text-brand-300'
                : 'text-ink-muted hover:text-ink'
            }`
          }
        >
          {t.label}
        </NavLink>
      ))}
    </nav>
  )
}
