import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { settingsApi } from '../../api/settings'
import Button from '../../components/Button'

export default function MonthCycleSettingsPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [startDay, setStartDay] = useState<number | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['month-cycle'],
    queryFn: settingsApi.getMonthCycle,
  })

  const mutation = useMutation({
    mutationFn: (day: number) => settingsApi.updateMonthCycle(day),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['month-cycle'] })
      queryClient.invalidateQueries({ queryKey: ['budget-period'] })
      navigate('/')
    },
  })

  if (isLoading) {
    return <p className="p-6 text-gray-500">Loading…</p>
  }

  const selected = startDay ?? data?.startDay ?? 1

  return (
    <div className="mx-auto max-w-md px-4 py-6">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Your month cycle</h1>
      <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
        Pick the day your budgeting month starts. Choose 1 for a normal calendar month, or your
        payday (e.g. 25) to track salary-to-salary.
      </p>

      <div className="mt-6 grid grid-cols-7 gap-2">
        {Array.from({ length: 31 }, (_, i) => i + 1).map((day) => (
          <button
            key={day}
            type="button"
            onClick={() => setStartDay(day)}
            className={`aspect-square rounded-lg border text-sm transition ${
              selected === day
                ? 'border-indigo-600 bg-indigo-600 text-white'
                : 'border-gray-300 text-gray-700 hover:border-indigo-400 dark:border-gray-700 dark:text-gray-300'
            }`}
          >
            {day}
          </button>
        ))}
      </div>

      <p className="mt-4 text-sm text-gray-500 dark:text-gray-400">
        {selected === 1
          ? 'Your month runs from the 1st to the last day of each calendar month.'
          : `Your month runs from the ${ordinal(selected)} to the ${ordinal(selected - 1 || 31)} of the next month.`}
        {selected > 28 && ' In shorter months this shifts to the last available day.'}
      </p>

      <div className="mt-6">
        <Button onClick={() => mutation.mutate(selected)} disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </div>
  )
}

function ordinal(n: number) {
  if (n % 100 >= 11 && n % 100 <= 13) return `${n}th`
  const suffix = { 1: 'st', 2: 'nd', 3: 'rd' }[n % 10] ?? 'th'
  return `${n}${suffix}`
}
