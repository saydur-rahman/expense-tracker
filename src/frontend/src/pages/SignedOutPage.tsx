import { useAuth } from '../auth/AuthContext'

/**
 * Where signing out lands you.
 *
 * This page is deliberately **outside** ProtectedRoute. Sending someone back to `/`
 * after a sign-out put them straight onto a protected route, which immediately
 * started a fresh sign-in — and with Google, which still had its own session, that
 * came back instantly as the same user. Logging out looked like it did nothing.
 */
export default function SignedOutPage() {
  const { login } = useAuth()

  return (
    <div className="flex min-h-screen items-center justify-center bg-page px-4">
      <div className="w-full max-w-sm rounded-xl border border-line bg-card p-6 text-center shadow-sm">
        <span
          aria-hidden="true"
          className="mx-auto mb-4 grid size-10 place-items-center rounded-xl bg-brand-600 text-sm font-bold text-white"
        >
          ৳
        </span>
        <h1 className="text-lg font-semibold tracking-tight text-ink">You're signed out</h1>
        <p className="mt-1 text-sm text-ink-muted">Your session has ended on this device.</p>
        <button
          onClick={() => login()}
          className="mt-5 w-full rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 active:bg-brand-800 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
        >
          Sign in again
        </button>
      </div>
    </div>
  )
}
