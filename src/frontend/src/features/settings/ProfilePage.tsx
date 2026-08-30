import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { profileApi } from '../../api/profile'
import { ApiError } from '../../api/client'
import { userManager } from '../../auth/oidc'

export default function ProfilePage() {
  const queryClient = useQueryClient()
  const [displayName, setDisplayName] = useState('')
  const [mobileNumber, setMobileNumber] = useState('')
  const [country, setCountry] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const { data: profile, isLoading } = useQuery({ queryKey: ['profile'], queryFn: profileApi.get })
  const { data: countries } = useQuery({
    queryKey: ['profile', 'countries'],
    queryFn: profileApi.countries,
    staleTime: Infinity,
  })

  // Seed the form once the profile arrives; after that the fields are the user's to edit.
  useEffect(() => {
    if (!profile) return
    setDisplayName(profile.displayName)
    setMobileNumber(profile.mobileNumber ?? '')
    setCountry(profile.country ?? '')
  }, [profile])

  const save = useMutation({
    mutationFn: () => profileApi.update({ displayName, mobileNumber, country }),
    onSuccess: async (updated) => {
      setError(null)
      setSaved(true)
      queryClient.setQueryData(['profile'], updated)

      // Currency rides on the access token, so a country change only reaches the
      // rest of the app once a fresh token is issued.
      if (updated.country !== profile?.country) {
        try {
          await userManager.signinSilent()
        } catch {
          setError('Saved. Sign out and back in to update the currency shown across the app.')
        }
      }
    },
    onError: (err) => {
      setSaved(false)
      setError(err instanceof ApiError ? err.message : 'Could not save your profile.')
    },
  })

  if (isLoading) return <p className="text-ink-muted">Loading…</p>

  const selected = countries?.find((c) => c.code === country)

  return (
    <div className="flex flex-col gap-4">
      <form
        onSubmit={(e) => {
          e.preventDefault()
          save.mutate()
        }}
        className="flex flex-col gap-4 rounded-xl border border-line bg-card p-4 shadow-sm"
      >
        <div>
          <h2 className="font-medium text-ink">Profile</h2>
          <p className="text-sm text-ink-muted">
            Your country sets the currency every amount is shown in.
          </p>
        </div>

        <Field label="Email">
          <input
            value={profile?.email ?? ''}
            disabled
            className="w-full cursor-not-allowed rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink-muted"
          />
          <span className="mt-1 block text-xs text-ink-muted">
            Your email is how you sign in and can't be changed here.
          </span>
        </Field>

        <Field label="Name">
          <input
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            required
            maxLength={100}
            autoComplete="name" className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
          />
        </Field>

        <Field label="Mobile number">
          <input
            value={mobileNumber}
            onChange={(e) => setMobileNumber(e.target.value)}
            type="tel" inputMode="tel"required
            autoComplete="tel" className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
          />
        </Field>

        <Field label="Country">
          <select
            value={country}
            onChange={(e) => setCountry(e.target.value)}
            required
            className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
          >
            <option value="">Select your country</option>
            {countries?.map((option) => (
              <option key={option.code} value={option.code}>
                {option.name} ({option.currencyCode})
              </option>
            ))}
          </select>
          {selected && (
            <span className="mt-1 block text-xs text-ink-muted">
              Amounts will be shown in {selected.currencyCode}.
            </span>
          )}
        </Field>

        {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}
        {saved && !error && !save.isPending && (
          <p className="text-sm text-positive-700 dark:text-positive-400">Profile saved.</p>
        )}

        <button
          type="submit" disabled={save.isPending}
          className="self-start rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 active:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
        >
          {save.isPending ? 'Saving…' : 'Save changes'}
        </button>
      </form>

      {/* A Google-only account has no password to change, so it is offered none. */}
      {profile?.hasPassword && <PasswordSection />}
    </div>
  )
}

/**
 * Changing the password from inside a signed-in session. Only rendered for an account
 * that registered with one — a Google-only account has none, and gaining a password is
 * not something this screen offers. No current password is asked for: the bearer token
 * is the proof of identity.
 */
function PasswordSection() {
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const change = useMutation({
    mutationFn: () => profileApi.changePassword({ newPassword: password, confirmPassword }),
    onSuccess: () => {
      setError(null)
      setDone(true)
      setPassword('')
      setConfirmPassword('')
    },
    onError: (err) => {
      setDone(false)
      setError(err instanceof ApiError ? err.message : 'Could not update your password.')
    },
  })

  // Checked here as well as on the server so a mismatch shows without a round trip.
  function submit(e: React.FormEvent) {
    e.preventDefault()
    setDone(false)
    if (password !== confirmPassword) {
      setError('The passwords do not match.')
      return
    }
    setError(null)
    change.mutate()
  }

  return (
    <form
      onSubmit={submit}
      className="flex flex-col gap-4 rounded-xl border border-line bg-card p-4 shadow-sm"
    >
      <div>
        <h2 className="font-medium text-ink">Password</h2>
        <p className="text-sm text-ink-muted">
          Pick a new password of at least 8 characters. Use it the next time you sign in.
        </p>
      </div>

      <Field label="New password">
        <input
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          type="password" required
          minLength={8}
          autoComplete="new-password" className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
        />
      </Field>

      <Field label="Retype password">
        <input
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          type="password" required
          minLength={8}
          autoComplete="new-password" className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
        />
      </Field>

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}
      {done && !error && !change.isPending && (
        <p className="text-sm text-positive-700 dark:text-positive-400">Password updated.</p>
      )}

      <button
        type="submit" disabled={change.isPending}
        className="self-start rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 active:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
      >
        {change.isPending ? 'Saving…' : 'Update password'}
      </button>
    </form>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-ink-soft">{label}</span>
      {children}
    </label>
  )
}
