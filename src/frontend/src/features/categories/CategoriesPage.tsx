import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoriesApi, type Category, type CategoryKind } from '../../api/categories'
import { ApiError } from '../../api/client'
import LedgerTabs from '../../components/LedgerTabs'

export default function CategoriesPage() {
  const queryClient = useQueryClient()
  const [newCategory, setNewCategory] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [kind, setKind] = useState<CategoryKind>('Expense')
  const [search, setSearch] = useState('')
  const isIncome = kind === 'Income'

  const { data: categories, isLoading } = useQuery({
    queryKey: ['categories', kind],
    queryFn: () => categoriesApi.list(kind),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['categories'] })
    queryClient.invalidateQueries({ queryKey: ['budgets'] })
    queryClient.invalidateQueries({ queryKey: ['summary'] })
  }

  const createCategory = useMutation({
    mutationFn: (name: string) => categoriesApi.create(name, kind),
    onSuccess: () => {
      setNewCategory('')
      setError(null)
      invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not add category.'),
  })

  const query = search.trim().toLowerCase()
  const visibleCategories =
    categories?.filter(
      (c) =>
        !query ||
        c.name.toLowerCase().includes(query) ||
        c.heads.some((h) => h.name.toLowerCase().includes(query)),
    ) ?? []

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-ink">Categories</h1>
        <p className="text-sm text-ink-muted">
          {isIncome
            ? 'Group where your money comes from, each with heads underneath. Income takes no budget.'
            : 'Group your spending into categories, each with heads underneath.'}
        </p>
      </div>

      <LedgerTabs value={kind} onChange={setKind} />

      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (newCategory.trim()) createCategory.mutate(newCategory.trim())
        }}
        className="flex gap-2"
      >
        <input
          value={newCategory}
          onChange={(e) => setNewCategory(e.target.value)}
          placeholder={isIncome ? 'New income category' : 'New category'}
          className="flex-1 rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
        />
        <button
          type="submit" disabled={createCategory.isPending}
          className="rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 active:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
        >
          Add
        </button>
      </form>

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

      {categories?.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          {isIncome
            ? 'No income categories yet. Add one above — Salary, Freelance, and so on.'
            : 'No categories yet. Add one above to get started.'}
        </p>
      )}

      {(categories?.length ?? 0) > 4 && (
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search categories and heads…"
          className="w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none"
        />
      )}

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      <div className="flex flex-col gap-3">
        {visibleCategories.map((category) => (
          <CategoryCard key={category.id} category={category} onChanged={invalidate} />
        ))}
      </div>

      {(categories?.length ?? 0) > 0 && visibleCategories.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          Nothing matches “{search}”.
        </p>
      )}
    </div>
  )
}

function CategoryCard({ category, onChanged }: { category: Category; onChanged: () => void }) {
  const [newHead, setNewHead] = useState('')
  const [renaming, setRenaming] = useState(false)
  const [name, setName] = useState(category.name)
  const [error, setError] = useState<string | null>(null)

  const handle = (fn: () => Promise<unknown>) => async () => {
    setError(null)
    try {
      await fn()
      onChanged()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.')
    }
  }

  return (
    <div className="rounded-xl border border-line bg-card p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2">
        {renaming ? (
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onBlur={handle(async () => {
              if (name.trim() && name !== category.name) {
                await categoriesApi.rename(category.id, name.trim())
              }
              setRenaming(false)
            })}
            autoFocus
            className="flex-1 rounded-lg border border-line bg-card px-2.5 py-1.5 transition-colors focus:border-brand-500 focus:outline-none"
          />
        ) : (
          <button
            onClick={() => setRenaming(true)}
            className="text-left font-medium text-ink"
          >
            {category.name}
          </button>
        )}
        <button
          onClick={handle(() => categoriesApi.archive(category.id))}
          className="shrink-0 text-sm font-medium text-ink-muted transition-colors hover:text-negative-600" title="Remove this category (its past data is kept)"
        >
          Remove
        </button>
      </div>

      <ul className="mt-3 flex flex-col gap-1">
        {category.heads.map((head) => (
          <li key={head.id} className="flex items-center justify-between gap-2 py-1">
            <HeadName head={head} onChanged={onChanged} />
            <button
              onClick={handle(() => categoriesApi.archiveHead(head.id))}
              className="shrink-0 text-xs font-medium text-ink-muted transition-colors hover:text-negative-600"
            >
              Remove
            </button>
          </li>
        ))}
      </ul>

      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (!newHead.trim()) return
          handle(async () => {
            await categoriesApi.createHead(category.id, newHead.trim())
            setNewHead('')
          })()
        }}
        className="mt-3 flex gap-2"
      >
        <input
          value={newHead}
          onChange={(e) => setNewHead(e.target.value)}
          placeholder="Add a head" className="flex-1 rounded-lg border border-line bg-card px-2.5 py-2 text-sm transition-colors focus:border-brand-500 focus:outline-none"
        />
        <button type="submit" className="rounded-lg border border-line bg-card px-3 py-2 text-sm font-medium text-ink-soft transition-colors hover:bg-raised">
          Add
        </button>
      </form>

      {error && <p className="mt-2 text-sm text-negative-600 dark:text-negative-400">{error}</p>}
    </div>
  )
}

function HeadName({ head, onChanged }: { head: { id: string; name: string }; onChanged: () => void }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(head.name)

  if (!editing) {
    return (
      <button onClick={() => setEditing(true)} className="text-left text-sm text-ink-soft">
        {head.name}
      </button>
    )
  }

  return (
    <input
      value={name}
      onChange={(e) => setName(e.target.value)}
      onBlur={async () => {
        if (name.trim() && name !== head.name) {
          await categoriesApi.renameHead(head.id, name.trim())
          onChanged()
        }
        setEditing(false)
      }}
      autoFocus
      className="flex-1 rounded-lg border border-line bg-card px-2.5 py-1.5 text-sm transition-colors focus:border-brand-500 focus:outline-none"
    />
  )
}
