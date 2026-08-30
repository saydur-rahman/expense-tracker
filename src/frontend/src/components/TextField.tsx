import type { InputHTMLAttributes } from 'react'
import { field } from './ui'

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
}

export default function TextField({ label, error, id, ...props }: TextFieldProps) {
  const inputId = id ?? props.name
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={inputId} className="text-sm font-medium text-ink-soft">
        {label}
      </label>
      <input
        id={inputId}
        className={`${field} ${error ? 'border-negative-500 dark:border-negative-500' : ''}`}
        {...props}
      />
      {error && <span className="text-sm text-negative-600 dark:text-negative-400">{error}</span>}
    </div>
  )
}
