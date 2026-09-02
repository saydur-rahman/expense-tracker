import { useEffect, useId, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { NavLink, useLocation } from 'react-router-dom'

export interface NavItem {
  to: string
  label: string
  end?: boolean
}

/**
 * The screens that don't fit on the bar.
 *
 * The nav was full at five items and the app keeps growing, so only the three you
 * reach daily stay visible and the rest live behind this. It renders as a dropdown
 * under the desktop nav and as a sheet on a phone — same list, same order, so the
 * two don't drift.
 */
export default function MoreMenu({
  items,
  variant,
  onLogout,
}: {
  items: NavItem[]
  variant: 'desktop' | 'mobile'
  /** Only passed on the phone, where the header has no room for it. */
  onLogout?: () => void
}) {
  const { pathname } = useLocation()
  const panelId = useId()
  const triggerRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  // Tracking *which* route it was opened on, rather than a bare boolean, closes the
  // panel on any navigation — including the browser's back button — without an
  // effect that sets state.
  const [openedAt, setOpenedAt] = useState<string | null>(null)
  const open = openedAt === pathname
  const close = () => setOpenedAt(null)

  // Without this the button looks inactive while you are on one of its screens, and
  // Budgets appears to have vanished from the app.
  const holdsCurrent = items.some((item) =>
    item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`),
  )

  useEffect(() => {
    if (!open) return

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close()
    }
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (!panelRef.current?.contains(target) && !triggerRef.current?.contains(target)) close()
    }

    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open])

  // Focus goes into the panel when it opens and back to the button when it closes,
  // so a keyboard never lands somewhere invisible.
  const wasOpen = useRef(false)
  useEffect(() => {
    if (open) {
      panelRef.current?.querySelector<HTMLElement>('a, button')?.focus()
    } else if (wasOpen.current) {
      triggerRef.current?.focus()
    }
    wasOpen.current = open
  }, [open])

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `block rounded-lg px-3 py-2.5 text-sm transition-colors ${
      isActive
        ? 'bg-brand-50 font-medium text-brand-700 dark:bg-brand-950 dark:text-brand-300'
        : 'text-ink-soft hover:bg-raised hover:text-ink'
    }`

  const links = items.map((item) => (
    <NavLink key={item.to} to={item.to} end={item.end} onClick={close} className={linkClass}>
      {item.label}
    </NavLink>
  ))

  if (variant === 'desktop') {
    return (
      <div className="relative">
        <button
          ref={triggerRef}
          type="button"
          onClick={() => (open ? close() : setOpenedAt(pathname))}
          aria-expanded={open}
          aria-controls={panelId}
          className={`-mb-px flex items-center gap-1 border-b-2 px-3 py-3 text-sm font-medium transition-colors ${
            open || holdsCurrent
              ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300'
              : 'border-transparent text-ink-muted hover:border-line hover:text-ink'
          }`}
        >
          More
          <span
            aria-hidden="true"
            className={`text-[0.625rem] transition-transform ${open ? 'rotate-180' : ''}`}
          >
            ▾
          </span>
        </button>

        {open && (
          <div
            ref={panelRef}
            id={panelId}
            className="absolute left-0 top-full z-20 mt-1 w-56 rounded-xl border border-line bg-card p-1 shadow-lg"
          >
            {links}
          </div>
        )}
      </div>
    )
  }

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        onClick={() => (open ? close() : setOpenedAt(pathname))}
        aria-expanded={open}
        aria-controls={panelId}
        className={`relative flex-1 px-1 py-2.5 text-center text-[0.6875rem] font-medium transition-colors ${
          open || holdsCurrent ? 'text-brand-700 dark:text-brand-300' : 'text-ink-muted'
        }`}
      >
        <span
          aria-hidden="true"
          className={`absolute inset-x-3 top-0 h-0.5 rounded-full transition-colors ${
            open || holdsCurrent ? 'bg-brand-600 dark:bg-brand-400' : 'bg-transparent'
          }`}
        />
        More
      </button>

      {/* Through a portal: the bottom bar carries `backdrop-blur`, which makes it the
          containing block for any fixed descendant — the sheet would be trapped
          inside a 48px-tall strip. */}
      {open &&
        createPortal(
          <div className="md:hidden">
            <div aria-hidden="true" className="fixed inset-0 z-30 bg-ink/40" />
            <div
              ref={panelRef}
              id={panelId}
              role="dialog"
              aria-modal="true"
              aria-label="More"
              className="fixed inset-x-0 bottom-0 z-40 rounded-t-2xl border-t border-line bg-card px-3 pb-[calc(env(safe-area-inset-bottom)+0.75rem)] pt-3 shadow-lg"
            >
              <div
                aria-hidden="true"
                className="mx-auto mb-3 h-1 w-10 rounded-full bg-line"
              />
              <div className="grid grid-cols-2 gap-1">{links}</div>

              {onLogout && (
                <div className="mt-2 border-t border-line-soft pt-2">
                  <button
                    type="button"
                    onClick={() => {
                      close()
                      onLogout()
                    }}
                    className="block w-full rounded-lg px-3 py-2.5 text-left text-sm text-ink-muted transition-colors hover:bg-raised hover:text-ink"
                  >
                    Log out
                  </button>
                </div>
              )}
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
