import { useEffect, useRef } from 'react'

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: { client_id: string; callback: (response: { credential: string }) => void }) => void
          renderButton: (parent: HTMLElement, options: { theme: string; size: string; width?: number }) => void
        }
      }
    }
  }
}

const GOOGLE_SCRIPT_ID = 'google-identity-services'

function loadGoogleScript(): Promise<void> {
  if (window.google?.accounts?.id) {
    return Promise.resolve()
  }
  return new Promise((resolve, reject) => {
    if (document.getElementById(GOOGLE_SCRIPT_ID)) {
      document.getElementById(GOOGLE_SCRIPT_ID)!.addEventListener('load', () => resolve())
      return
    }
    const script = document.createElement('script')
    script.id = GOOGLE_SCRIPT_ID
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => reject(new Error('Failed to load Google Identity Services'))
    document.head.appendChild(script)
  })
}

export default function GoogleSignInButton({ onIdToken }: { onIdToken: (idToken: string) => void }) {
  const buttonRef = useRef<HTMLDivElement>(null)
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

  useEffect(() => {
    if (!clientId || !buttonRef.current) return

    let cancelled = false
    loadGoogleScript().then(() => {
      if (cancelled || !window.google || !buttonRef.current) return
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => onIdToken(response.credential),
      })
      window.google.accounts.id.renderButton(buttonRef.current, {
        theme: 'outline',
        size: 'large',
        width: 320,
      })
    })

    return () => {
      cancelled = true
    }
  }, [clientId, onIdToken])

  if (!clientId) {
    return (
      <p className="text-sm text-gray-400">
        Google sign-in isn't configured yet (missing VITE_GOOGLE_CLIENT_ID).
      </p>
    )
  }

  return <div ref={buttonRef} />
}
