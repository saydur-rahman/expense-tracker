import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import ImpersonationBanner from '../components/ImpersonationBanner'

const baseNavItems = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/expenses', label: 'Expenses', end: false },
  { to: '/budgets', label: 'Budgets', end: false },
  { to: '/categories', label: 'Categories', end: false },
]

export default function AppLayout() {
  const { user, logout, isAdmin } = useAuth()
  const navItems = isAdmin
    ? [...baseNavItems, { to: '/admin/users', label: 'Users', end: false }]
    : baseNavItems

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-950">
      <ImpersonationBanner />
      <header className="border-b border-gray-200 bg-white px-4 py-3 dark:border-gray-800 dark:bg-gray-900">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-3">
          <span className="font-semibold text-gray-900 dark:text-gray-100">Expense Tracker</span>
          <div className="flex items-center gap-3">
            <NavLink
              to="/settings/month-cycle"
              className="text-sm text-gray-500 hover:text-indigo-600 dark:text-gray-400"
            >
              {user?.displayName}
            </NavLink>
            <button
              onClick={logout}
              className="text-sm text-gray-500 hover:text-indigo-600 dark:text-gray-400"
            >
              Log out
            </button>
          </div>
        </div>
      </header>

      {/* Desktop nav; on mobile this is replaced by the fixed bottom bar below. */}
      <nav className="hidden border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900 md:block">
        <div className="mx-auto flex max-w-3xl gap-1 px-4">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `px-4 py-3 text-sm font-medium transition ${
                  isActive
                    ? 'border-b-2 border-indigo-600 text-indigo-600 dark:text-indigo-400'
                    : 'text-gray-500 hover:text-gray-900 dark:text-gray-400'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </div>
      </nav>

      <main className="mx-auto max-w-3xl px-4 pb-24 pt-4 md:pb-8">
        <Outlet />
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-gray-200 bg-white pb-[env(safe-area-inset-bottom)] dark:border-gray-800 dark:bg-gray-900 md:hidden">
        <div className="flex">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `flex-1 py-3 text-center text-xs font-medium transition ${
                  isActive ? 'text-indigo-600 dark:text-indigo-400' : 'text-gray-500 dark:text-gray-400'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  )
}
