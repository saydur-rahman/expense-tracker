import type { InputHTMLAttributes } from 'react'

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
}

export default function TextField({ label, error, id, ...props }: TextFieldProps) {
  const inputId = id ?? props.name
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={inputId} className="text-sm font-medium text-gray-700 dark:text-gray-300">
        {label}
      </label>
      <input
        id={inputId}
        className="rounded-lg border border-gray-300 px-3 py-3 text-base focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
        {...props}
      />
      {error && <span className="text-sm text-red-600 dark:text-red-400">{error}</span>}
    </div>
  )
}
