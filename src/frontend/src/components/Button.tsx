import type { ButtonHTMLAttributes } from 'react'

export default function Button({
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={`w-full rounded-lg bg-indigo-600 px-4 py-3 text-base font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400 ${className}`}
      {...props}
    />
  )
}
