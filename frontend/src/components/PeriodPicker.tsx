interface PeriodPickerProps {
  label: string
  offset: number
  onOffsetChange: (offset: number) => void
}

export default function PeriodPicker({ label, offset, onOffsetChange }: PeriodPickerProps) {
  return (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-gray-200 bg-white px-2 py-2 dark:border-gray-800 dark:bg-gray-900">
      <button
        onClick={() => onOffsetChange(offset - 1)}
        className="rounded px-3 py-2 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-800"
        aria-label="Previous period"
      >
        ‹
      </button>
      <span className="text-sm font-medium text-gray-900 dark:text-gray-100">{label}</span>
      <button
        onClick={() => onOffsetChange(offset + 1)}
        className="rounded px-3 py-2 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-800"
        aria-label="Next period"
      >
        ›
      </button>
    </div>
  )
}
