import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { settingsApi, WEEK_DAYS, type PeriodKind, type WeekDay } from '../../api/settings'
import Button from '../../components/Button'

/**
 * Choosing the rhythm budgets are cut into: monthly from a day of the month, or weekly
 * from a day of the week. Doubles as onboarding — RequireMonthCycle sends anyone who has
 * never chosen here before letting them near a budget screen.
 */
export default function MonthCycleSettingsPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const { data, isLoading } = useQuery({
    queryKey: ['month-cycle'],
    queryFn: settingsApi.getMonthCycle,
  })

  const [kind, setKind] = useState<PeriodKind>('Month')
  const [startDay, setStartDay] = useState(1)
  const [weekStartsOn, setWeekStartsOn] = useState<WeekDay>('Monday')

  // Seed once the setting arrives; after that the choices are the user's to change.
  useEffect(() => {
    if (!data) return
    setKind(data.periodKind)
    setStartDay(data.startDay)
    setWeekStartsOn(data.weekStartsOn)
  }, [data])

  const mutation = useMutation({
    mutationFn: () => settingsApi.updateMonthCycle({ periodKind: kind, startDay, weekStartsOn }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['month-cycle'] })
      queryClient.invalidateQueries({ queryKey: ['budget-period'] })
      navigate('/')
    },
  })

  if (isLoading) {
    return <p className="p-6 text-ink-muted">Loading…</p>
  }

  return (
    <div className="mx-auto max-w-md px-4 py-6">
      <h1 className="text-xl font-semibold tracking-tight text-ink">Your budget cycle</h1>
      <p className="mt-2 text-sm text-ink-muted">
        Pick the rhythm you budget in. Every total, budget and report is cut to it.
      </p>

      <div
        role="radiogroup"
        aria-label="Budget rhythm"
        className="mt-6 flex gap-2 rounded-xl border border-line bg-raised p-1"
      >
        <RhythmOption label="Monthly" hint="Pay to pay" selected={kind === 'Month'} onSelect={() => setKind('Month')} />
        <RhythmOption label="Weekly" hint="Week to week" selected={kind === 'Week'} onSelect={() => setKind('Week')} />
      </div>

      {kind === 'Month' ? (
        <>
          <p className="mt-6 text-sm font-medium text-ink-soft">The day your month starts</p>
          <div className="mt-2 grid grid-cols-7 gap-2">
            {Array.from({ length: 31 }, (_, i) => i + 1).map((day) => (
              <button
                key={day}
                type="button" onClick={() => setStartDay(day)}
                className={`aspect-square rounded-lg border text-sm transition ${
 startDay === day
                    ? 'border-brand-600 bg-brand-600 text-white'
                    : 'border-line text-ink-soft hover:border-brand-400'
                }`}
              >
                {day}
              </button>
            ))}
          </div>

          <p className="mt-4 text-sm text-ink-muted">
            {startDay === 1
              ? 'Your month runs from the 1st to the last day of each calendar month.'
              : `Your month runs from the ${ordinal(startDay)} to the ${ordinal(startDay - 1 || 31)} of the next month.`}
            {startDay > 28 && ' In shorter months this shifts to the last available day.'}
          </p>
        </>
      ) : (
        <>
          <p className="mt-6 text-sm font-medium text-ink-soft">The day your week starts</p>
          <div className="mt-2 flex flex-col gap-2">
            {WEEK_DAYS.map((day) => (
              <button
                key={day}
                type="button" onClick={() => setWeekStartsOn(day)}
                className={`rounded-lg border px-3 py-2.5 text-left text-sm transition ${
 weekStartsOn === day
                    ? 'border-brand-600 bg-brand-600 text-white'
                    : 'border-line text-ink-soft hover:border-brand-400'
                }`}
              >
                {day}
              </button>
            ))}
          </div>

          <p className="mt-4 text-sm text-ink-muted">
            Your budget runs {weekStartsOn} to {dayBefore(weekStartsOn)}, and rolls over every{' '}
            {weekStartsOn}. Budgets you set carry into the next week the same way they do
            between months.
          </p>
        </>
      )}

      {data?.isConfigured && data.periodKind !== kind && (
        <p className="mt-4 rounded-lg border border-line bg-raised p-3 text-sm text-ink-soft">
          Switching rhythm doesn't change any month you've already budgeted — those stay
          exactly as they are. Your first {kind === 'Week' ? 'week' : 'month'} starts unbudgeted,
          because a {kind === 'Week' ? 'monthly' : 'weekly'} figure split across{' '}
          {kind === 'Week' ? 'weeks' : 'a month'} would be an amount you never chose.
        </p>
      )}

      {mutation.isError && (
        <p className="mt-4 text-sm text-negative-600 dark:text-negative-400">
          Could not save your budget cycle.
        </p>
      )}

      <div className="mt-6">
        <Button onClick={() => mutation.mutate()} disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </div>
  )
}

function RhythmOption({
  label,
  hint,
  selected,
  onSelect,
}: {
  label: string
  hint: string
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onSelect}
      className={`flex-1 rounded-lg px-3 py-2 text-center transition ${
        selected ? 'bg-card shadow-sm' : 'hover:bg-card/50'
      }`}
    >
      <span className={`block text-sm font-medium ${selected ? 'text-brand-700 dark:text-brand-300' : 'text-ink-soft'}`}>
        {label}
      </span>
      <span className="block text-xs text-ink-muted">{hint}</span>
    </button>
  )
}

function dayBefore(day: WeekDay): WeekDay {
  const index = WEEK_DAYS.indexOf(day)
  return WEEK_DAYS[(index + WEEK_DAYS.length - 1) % WEEK_DAYS.length]
}

function ordinal(n: number) {
  if (n % 100 >= 11 && n % 100 <= 13) return `${n}th`
  const suffix = { 1: 'st', 2: 'nd', 3: 'rd' }[n % 10] ?? 'th'
  return `${n}${suffix}`
}
