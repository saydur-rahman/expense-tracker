import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { userManager, safeReturnPath } from '../auth/oidc'

/** Where Auth019 redirects back to after sign-in; redeems the code for tokens. */
export default function CallbackPage() {
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    userManager
      .signinRedirectCallback()
      // `state` is the route they were heading for before sign-in interrupted them.
      .then((signedIn) => navigate(safeReturnPath(signedIn.state), { replace: true }))
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'Sign-in failed.'))
  }, [navigate])

  return (
    <div className="flex min-h-screen items-center justify-center bg-card px-4">
      {error ? (
        <div className="text-center">
          <p className="text-negative-600 dark:text-negative-400">{error}</p>
          <button
            onClick={() => userManager.signinRedirect()}
            className="mt-4 rounded-lg bg-brand-600 px-4 py-2 text-white"
          >
            Try again
          </button>
        </div>
      ) : (
        <p className="text-ink-muted">Signing you in…</p>
      )}
    </div>
  )
}
