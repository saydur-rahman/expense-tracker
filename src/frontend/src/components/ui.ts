/**
 * The handful of surface and control classes the screens share, kept in one place
 * so a card on the dashboard and a card on the settings screen can't drift apart.
 * These use the semantic surface tokens from index.css, so they follow the theme
 * without any `dark:` variants.
 */

/** A raised panel: the app's default container for a block of content. */
export const card = 'rounded-xl border border-line bg-card shadow-sm'

/** A dashed placeholder shown where content would be. */
export const emptyState =
  'rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted'

/** Full-size text input / select — comfortable enough to tap on a phone. */
export const field =
  'w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink ' +
  'placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none'

/** The compact variant used for filters and inline edits. */
export const fieldSm =
  'rounded-lg border border-line bg-input px-2.5 py-2 text-sm text-ink ' +
  'transition-colors focus:border-brand-500 focus:outline-none'

/** Small uppercase label above a figure. */
export const eyebrow = 'text-xs font-medium uppercase tracking-wide text-ink-muted'

export const pageTitle = 'text-xl font-semibold tracking-tight text-ink'

export const subtleText = 'text-sm text-ink-muted'
