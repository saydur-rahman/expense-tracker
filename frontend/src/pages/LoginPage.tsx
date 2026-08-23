import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '../auth/AuthContext'
import GoogleSignInButton from '../auth/GoogleSignInButton'
import Button from '../components/Button'
import TextField from '../components/TextField'
import { ApiError } from '../api/client'

const schema = z.object({
  email: z.string().email('Enter a valid email'),
  password: z.string().min(1, 'Password is required'),
})

type FormValues = z.infer<typeof schema>

export default function LoginPage() {
  const { login, loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  async function onSubmit(values: FormValues) {
    setServerError(null)
    try {
      await login(values.email, values.password)
      navigate('/')
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong.')
    }
  }

  async function onGoogleToken(idToken: string) {
    setServerError(null)
    try {
      await loginWithGoogle(idToken)
      navigate('/')
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong.')
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-white px-4 dark:bg-gray-950">
      <div className="w-full max-w-sm">
        <h1 className="mb-6 text-center text-2xl font-semibold text-gray-900 dark:text-gray-100">
          Log in
        </h1>
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register('email')}
          />
          <TextField
            label="Password"
            type="password"
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
          {serverError && <p className="text-sm text-red-600 dark:text-red-400">{serverError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Logging in…' : 'Log in'}
          </Button>
        </form>

        <div className="my-6 flex items-center gap-3 text-gray-400">
          <div className="h-px flex-1 bg-gray-200 dark:bg-gray-800" />
          <span className="text-xs uppercase">or</span>
          <div className="h-px flex-1 bg-gray-200 dark:bg-gray-800" />
        </div>

        <div className="flex justify-center">
          <GoogleSignInButton onIdToken={onGoogleToken} />
        </div>

        <p className="mt-6 text-center text-sm text-gray-500 dark:text-gray-400">
          No account?{' '}
          <Link to="/register" className="text-indigo-600 dark:text-indigo-400">
            Register
          </Link>
        </p>
      </div>
    </div>
  )
}
