import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider, useAuth } from 'react-oidc-context'
import { setAccessToken } from './api'
import './index.css'
import App from './App.tsx'

const authority = import.meta.env.VITE_OIDC_AUTHORITY as string | undefined
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID as string | undefined
const oidcEnabled = Boolean(authority && clientId)

export function AuthenticatedApp() {
  const auth = useAuth()
  setAccessToken(auth.user?.access_token)

  if (auth.isLoading) {
    return <main className="auth-screen"><span className="spinner" /> Connecting to your identity provider…</main>
  }

  if (!auth.isAuthenticated) {
    return (
      <main className="auth-screen">
        <div className="auth-card">
          <span className="brand-mark" aria-hidden="true">A</span>
          <h1>Sign in to AgentForge</h1>
          <p>Use your organization account to continue to the workspace.</p>
          {auth.error && <p className="chat-error" role="alert">{auth.error.message}</p>}
          <button className="primary-button" onClick={() => void auth.signinRedirect()} type="button">Continue to sign in</button>
        </div>
      </main>
    )
  }

  const displayName = auth.user?.profile.name ?? auth.user?.profile.preferred_username ?? 'Workspace member'
  return <App displayName={displayName} />
}

const app = oidcEnabled ? (
  <AuthProvider
    authority={authority!}
    client_id={clientId!}
    redirect_uri={window.location.origin}
    response_type="code"
    scope={import.meta.env.VITE_OIDC_SCOPE || 'openid profile email'}
    onSigninCallback={() => window.history.replaceState({}, document.title, window.location.pathname)}
  >
    <AuthenticatedApp />
  </AuthProvider>
) : <App />

createRoot(document.getElementById('root')!).render(<StrictMode>{app}</StrictMode>)
