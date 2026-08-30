import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  block?: boolean
}

const base =
  'inline-flex items-center justify-center gap-2 rounded-lg font-medium transition-colors ' +
  'disabled:cursor-not-allowed disabled:opacity-50'

const variants: Record<Variant, string> = {
  primary:
    'bg-brand-600 text-white shadow-sm hover:bg-brand-700 active:bg-brand-800 ' +
    'dark:bg-brand-500 dark:hover:bg-brand-400 dark:active:bg-brand-300 dark:text-brand-950',
  secondary:
    'border border-line bg-card text-ink-soft hover:bg-raised active:bg-raised',
  ghost:
    'text-ink-soft hover:bg-raised hover:text-ink',
  danger:
    'text-negative-600 hover:bg-negative-50 hover:text-negative-700 ' +
    'dark:text-negative-400 dark:hover:bg-negative-950 dark:hover:text-negative-400',
}

const sizes: Record<Size, string> = {
  sm: 'px-2.5 py-1.5 text-xs',
  md: 'px-4 py-2.5 text-sm',
  lg: 'px-4 py-3 text-base',
}

export default function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  className = '',
  ...props
}: ButtonProps) {
  return (
    <button
      className={`${base} ${variants[variant]} ${sizes[size]} ${block ? 'w-full' : ''} ${className}`}
      {...props}
    />
  )
}
