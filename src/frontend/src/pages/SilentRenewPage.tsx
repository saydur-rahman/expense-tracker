import { useEffect } from 'react'
import { userManager } from '../auth/oidc'

/** Rendered inside the hidden iframe oidc-client-ts uses to refresh tokens. */
export default function SilentRenewPage() {
  useEffect(() => {
    userManager.signinSilentCallback().catch(() => {
      /* The parent frame surfaces renewal failures; nothing to show here. */
    })
  }, [])

  return null
}
