interface PeriodPickerProps {
  label: string
  offset: number
  onOffsetChange: (offset: number) => void
}

export default function PeriodPicker({ label, offset, onOffsetChange }: PeriodPickerProps) {
  return (
    <div className="flex items-center justify-between gap-2 rounded-xl border border-line bg-card p-1.5 shadow-sm">
      <Step direction="previous" onClick={() => onOffsetChange(offset - 1)} />
      <span className="text-sm font-medium text-ink">{label}</span>
      <Step direction="next" onClick={() => onOffsetChange(offset + 1)} />
    </div>
  )
}

function Step({ direction, onClick }: { direction: 'previous' | 'next'; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      aria-label={`${direction === 'previous' ? 'Previous' : 'Next'} period`}
      className="grid size-9 place-items-center rounded-lg text-lg leading-none text-ink-muted transition-colors hover:bg-raised hover:text-brand-700 dark:hover:text-brand-300"
    >
      {direction === 'previous' ? '‹' : '›'}
    </button>
  )
}
