import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { userManager } from '../auth/oidc'

/** Where Auth019 redirects back to after sign-in; redeems the code for tokens. */
export default function CallbackPage() {
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    userManager
      .signinRedirectCallback()
      .then(() => navigate('/', { replace: true }))
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'Sign-in failed.'))
  }, [navigate])

  return (
    <div className="flex min-h-screen items-center justify-center bg-white px-4 dark:bg-gray-950">
      {error ? (
        <div className="text-center">
          <p className="text-red-600 dark:text-red-400">{error}</p>
          <button
            onClick={() => userManager.signinRedirect()}
            className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-white"
          >
            Try again
          </button>
        </div>
      ) : (
        <p className="text-gray-500 dark:text-gray-400">Signing you in…</p>
      )}
    </div>
  )
}
