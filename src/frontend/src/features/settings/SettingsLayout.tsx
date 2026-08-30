import { NavLink, Outlet } from 'react-router-dom'

const sections = [
  { to: 'profile', label: 'Profile', hint: 'Your name, mobile and country' },
  { to: 'month-cycle', label: 'Month cycle', hint: 'The day your month starts' },
]

/**
 * Settings shell: sections down the left, the chosen one to the right. On a phone
 * the list collapses to a row of pills above the content rather than eating half
 * the screen width.
 */
export default function SettingsLayout() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight text-ink">Settings</h1>

      <div className="flex flex-col gap-4 md:flex-row md:items-start md:gap-6">
        <nav
          aria-label="Settings sections" className="flex gap-2 overflow-x-auto md:w-56 md:shrink-0 md:flex-col md:overflow-visible"
        >
          {sections.map((section) => (
            <NavLink
              key={section.to}
              to={section.to}
              className={({ isActive }) =>
                `shrink-0 rounded-lg px-3 py-2 text-sm font-medium transition md:shrink ${
                  isActive
                    ? 'bg-brand-50 text-brand-700 dark:bg-brand-950 dark:text-brand-300'
                    : 'text-ink-soft hover:bg-raised'
                }`
              }
            >
              <span className="block">{section.label}</span>
              <span className="hidden text-xs font-normal text-ink-muted md:block">
                {section.hint}
              </span>
            </NavLink>
          ))}
        </nav>

        <div className="min-w-0 flex-1">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
