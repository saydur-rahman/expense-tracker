import { useEffect, useMemo, useRef, useState } from 'react'

export interface SelectOption {
  value: string
  label: string
  /** Optional heading this option sits under, e.g. its category. */
  group?: string
}

interface SearchableSelectProps {
  value: string
  onChange: (value: string) => void
  options: SelectOption[]
  placeholder?: string
  /** Label for the "no selection" entry. Omit to make a choice mandatory. */
  emptyLabel?: string
  disabled?: boolean
  className?: string
  id?: string
}

/**
 * A select you can type into. Once someone has thirty heads across a dozen
 * categories, scrolling a native dropdown stops being usable — so this filters as
 * you type and keeps the category headings for context.
 *
 * Built by hand rather than pulling in a combobox library: it is the only widget
 * that needed one, and this keeps the bundle (and the dependency list) small.
 */
export default function SearchableSelect({
  value,
  onChange,
  options,
  placeholder = 'Choose…',
  emptyLabel,
  disabled,
  className = '',
  id,
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)

  const selected = options.find((o) => o.value === value)

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase()
    const pool = emptyLabel !== undefined ? [{ value: '', label: emptyLabel }, ...options] : options
    if (!q) return pool
    // Match on the head name or its category, so "food" finds everything under Food.
    return pool.filter((o) => `${o.group ?? ''} ${o.label}`.toLowerCase().includes(q))
  }, [options, query, emptyLabel])

  // Close when focus or a click leaves the control.
  useEffect(() => {
    if (!open) return
    const onPointerDown = (e: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('touchstart', onPointerDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('touchstart', onPointerDown)
    }
  }, [open])

  useEffect(() => {
    if (open) {
      setQuery('')
      setHighlight(0)
      // Let the popover paint before stealing focus, or mobile keyboards misbehave.
      requestAnimationFrame(() => searchRef.current?.focus())
    }
  }, [open])

  const choose = (optionValue: string) => {
    onChange(optionValue)
    setOpen(false)
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlight((h) => Math.min(h + 1, matches.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(h - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      const option = matches[highlight]
      if (option) choose(option.value)
    } else if (e.key === 'Escape') {
      setOpen(false)
    }
  }

  let lastGroup: string | undefined

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      <button
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-2 rounded-lg border border-line bg-input px-3 py-2.5 text-left text-base text-ink transition-colors focus:border-brand-500 focus:outline-none disabled:cursor-not-allowed disabled:opacity-50"
      >
        <span className={`truncate ${selected ? '' : 'text-ink-muted'}`}>
          {selected ? selected.label : placeholder}
        </span>
        <span aria-hidden="true" className="shrink-0 text-xs text-ink-muted">
          ▾
        </span>
      </button>

      {open && (
        <div className="absolute z-30 mt-1 w-full overflow-hidden rounded-xl border border-line bg-card shadow-lg">
          <div className="border-b border-line-soft p-2">
            <input
              ref={searchRef}
              value={query}
              onChange={(e) => {
                setQuery(e.target.value)
                setHighlight(0)
              }}
              onKeyDown={onKeyDown}
              placeholder="Search…"
              className="w-full rounded-lg border border-line bg-input px-2.5 py-2 text-sm text-ink placeholder:text-ink-muted focus:border-brand-500 focus:outline-none"
            />
          </div>

          <ul role="listbox" className="max-h-64 overflow-y-auto py-1">
            {matches.length === 0 && (
              <li className="px-3 py-4 text-center text-sm text-ink-muted">Nothing matches.</li>
            )}
            {matches.map((option, index) => {
              const showGroup = option.group && option.group !== lastGroup
              lastGroup = option.group
              const isSelected = option.value === value
              const isHighlighted = index === highlight

              return (
                <li key={option.value || '__empty'}>
                  {showGroup && (
                    <p className="px-3 pb-1 pt-2 text-xs font-medium uppercase tracking-wide text-ink-muted">
                      {option.group}
                    </p>
                  )}
                  <button
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    onMouseEnter={() => setHighlight(index)}
                    onClick={() => choose(option.value)}
                    className={`block w-full px-3 py-2 text-left text-sm transition-colors ${
                      isHighlighted ? 'bg-raised' : ''
                    } ${isSelected ? 'font-medium text-brand-700 dark:text-brand-300' : 'text-ink'}`}
                  >
                    {option.label}
                  </button>
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </div>
  )
}
