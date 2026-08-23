import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoriesApi, type Category } from '../../api/categories'
import { ApiError } from '../../api/client'

export default function CategoriesPage() {
  const queryClient = useQueryClient()
  const [newCategory, setNewCategory] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: categories, isLoading } = useQuery({
    queryKey: ['categories'],
    queryFn: () => categoriesApi.list(),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['categories'] })
    queryClient.invalidateQueries({ queryKey: ['budgets'] })
  }

  const createCategory = useMutation({
    mutationFn: (name: string) => categoriesApi.create(name),
    onSuccess: () => {
      setNewCategory('')
      setError(null)
      invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not add category.'),
  })

  if (isLoading) return <p className="text-gray-500">Loading…</p>

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Categories</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Group your spending into categories, each with heads underneath.
        </p>
      </div>

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
          placeholder="New category"
          className="flex-1 rounded-lg border border-gray-300 px-3 py-2.5 text-base focus:border-indigo-500 focus:outline-none dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
        />
        <button
          type="submit"
          disabled={createCategory.isPending}
          className="rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-50"
        >
          Add
        </button>
      </form>

      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

      {categories?.length === 0 && (
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
          No categories yet. Add one above to get started.
        </p>
      )}

      <div className="flex flex-col gap-3">
        {categories?.map((category) => (
          <CategoryCard key={category.id} category={category} onChanged={invalidate} />
        ))}
      </div>
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
    <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
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
            className="flex-1 rounded border border-gray-300 px-2 py-1 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
          />
        ) : (
          <button
            onClick={() => setRenaming(true)}
            className="text-left font-medium text-gray-900 dark:text-gray-100"
          >
            {category.name}
          </button>
        )}
        <button
          onClick={handle(() => categoriesApi.archive(category.id))}
          className="shrink-0 text-sm text-gray-400 hover:text-red-600"
          title="Remove this category (its past data is kept)"
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
              className="shrink-0 text-xs text-gray-400 hover:text-red-600"
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
          placeholder="Add a head"
          className="flex-1 rounded border border-gray-300 px-2 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
        />
        <button type="submit" className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:text-gray-300">
          Add
        </button>
      </form>

      {error && <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>}
    </div>
  )
}

function HeadName({ head, onChanged }: { head: { id: string; name: string }; onChanged: () => void }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(head.name)

  if (!editing) {
    return (
      <button onClick={() => setEditing(true)} className="text-left text-sm text-gray-700 dark:text-gray-300">
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
      className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
    />
  )
}
