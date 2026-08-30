import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import ImpersonationBanner from '../components/ImpersonationBanner'

const baseNavItems = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/expenses', label: 'Expenses', end: false },
  { to: '/incomes', label: 'Income', end: false },
  { to: '/budgets', label: 'Budgets', end: false },
  { to: '/categories', label: 'Categories', end: false },
]

export default function AppLayout() {
  const { user, logout, isAdmin } = useAuth()
  const navItems = isAdmin
    ? [...baseNavItems, { to: '/admin/users', label: 'Users', end: false }]
    : baseNavItems

  return (
    <div className="min-h-screen bg-page">
      <ImpersonationBanner />

      <header className="sticky top-0 z-10 border-b border-line bg-card/85 backdrop-blur">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-3 px-4 py-3">
          <span className="flex items-center gap-2">
            {/* A small brand mark, so the header isn't just a line of text. */}
            <span
              aria-hidden="true" className="grid size-7 place-items-center rounded-lg bg-brand-600 text-xs font-bold text-white"
            >
              ৳
            </span>
            <span className="font-semibold tracking-tight text-ink">
              Expense Tracker
            </span>
          </span>

          <div className="flex items-center gap-1">
            <NavLink
              to="/settings" className="rounded-lg px-2.5 py-1.5 text-sm text-ink-soft transition-colors hover:bg-raised hover:text-brand-700 dark:hover:text-brand-300"
            >
              {user?.displayName}
            </NavLink>
            <button
              onClick={logout}
              className="rounded-lg px-2.5 py-1.5 text-sm text-ink-muted transition-colors hover:bg-raised hover:text-ink"
            >
              Log out
            </button>
          </div>
        </div>

        {/* Desktop nav; on mobile this is replaced by the fixed bottom bar below. */}
        <nav className="hidden border-t border-line-soft md:block">
          <div className="mx-auto flex max-w-3xl gap-1 px-4">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `-mb-px border-b-2 px-3 py-3 text-sm font-medium transition-colors ${
                    isActive
                      ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300'
                      : 'border-transparent text-ink-muted hover:border-line hover:text-ink'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </div>
        </nav>
      </header>

      <main className="mx-auto max-w-3xl px-4 pb-24 pt-5 md:pb-10">
        <Outlet />
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-line bg-card/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden">
        <div className="mx-auto flex max-w-3xl">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `relative flex-1 px-1 py-2.5 text-center text-[0.6875rem] font-medium transition-colors ${
                  isActive
                    ? 'text-brand-700 dark:text-brand-300'
                    : 'text-ink-muted'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  {/* A short bar rather than a filled pill: it marks the tab without
                      crowding six labels on a narrow phone. */}
                  <span
                    aria-hidden="true" className={`absolute inset-x-3 top-0 h-0.5 rounded-full transition-colors ${
 isActive ? 'bg-brand-600 dark:bg-brand-400' : 'bg-transparent'
                    }`}
                  />
                  {item.label}
                </>
              )}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  )
}
