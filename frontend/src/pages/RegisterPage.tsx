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
  displayName: z.string().min(1, 'Name is required').max(100),
  email: z.string().email('Enter a valid email'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
})

type FormValues = z.infer<typeof schema>

export default function RegisterPage() {
  const { register: registerUser, loginWithGoogle } = useAuth()
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
      await registerUser(values.email, values.password, values.displayName)
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
          Create your account
        </h1>
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <TextField
            label="Name"
            autoComplete="name"
            error={errors.displayName?.message}
            {...register('displayName')}
          />
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
            autoComplete="new-password"
            error={errors.password?.message}
            {...register('password')}
          />
          {serverError && <p className="text-sm text-red-600 dark:text-red-400">{serverError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
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
          Already have an account?{' '}
          <Link to="/login" className="text-indigo-600 dark:text-indigo-400">
            Log in
          </Link>
        </p>
      </div>
    </div>
  )
}
