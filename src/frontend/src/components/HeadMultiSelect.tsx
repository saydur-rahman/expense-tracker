import type { Category } from '../api/categories'
import SearchableSelect from './SearchableSelect'

/** A head option always knows its category, so `group` is required here. */
interface HeadOption {
  value: string
  label: string
  group: string
}

/**
 * Pick several heads: a chip for each one chosen, and the ordinary searchable picker to
 * add the next.
 *
 * Built on `SearchableSelect` unchanged rather than teaching it multi-select — one at a
 * time is how you actually link heads, and the chips make what is already linked plain.
 */
export default function HeadMultiSelect({
  categories,
  value,
  onChange,
  placeholder = 'Add a head…',
  emptyHint,
}: {
  categories: Category[]
  value: string[]
  onChange: (headIds: string[]) => void
  placeholder?: string
  emptyHint?: string
}) {
  const heads: HeadOption[] = categories.flatMap((category) =>
    category.heads.map((head) => ({
      value: head.id,
      label: head.name,
      group: category.name,
    })),
  )

  const chosen = value
    .map((id) => heads.find((h) => h.value === id))
    .filter((head): head is HeadOption => head !== undefined)

  // Already-linked heads drop out of the picker rather than being offered and refused.
  const available = heads.filter((head) => !value.includes(head.value))

  return (
    <div className="flex flex-col gap-2">
      {chosen.length > 0 ? (
        <ul className="flex flex-wrap gap-1.5">
          {chosen.map((head) => (
            <li key={head.value}>
              <span className="inline-flex items-center gap-1.5 rounded-full bg-raised py-1 pl-2.5 pr-1 text-xs text-ink-soft">
                <span className="text-ink-muted">{head.group} ›</span> {head.label}
                <button
                  type="button"
                  aria-label={`Unlink ${head.label}`}
                  onClick={() => onChange(value.filter((id) => id !== head.value))}
                  className="grid size-4 place-items-center rounded-full text-ink-muted transition-colors hover:bg-negative-100 hover:text-negative-700 dark:hover:bg-negative-950 dark:hover:text-negative-400"
                >
                  ×
                </button>
              </span>
            </li>
          ))}
        </ul>
      ) : (
        emptyHint && <p className="text-xs text-ink-muted">{emptyHint}</p>
      )}

      <SearchableSelect
        value=""
        onChange={(headId) => headId && onChange([...value, headId])}
        options={available}
        placeholder={placeholder}
        disabled={available.length === 0}
        className="max-w-sm"
      />
    </div>
  )
}
