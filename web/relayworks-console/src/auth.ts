import { PublicClientApplication, InteractionRequiredAuthError, type AccountInfo } from '@azure/msal-browser'
import { reactive } from 'vue'

const enabled = import.meta.env.VITE_AUTH_ENABLED === 'true'
const apiScope = import.meta.env.VITE_API_SCOPE ?? ''
const developmentTenantId = import.meta.env.VITE_DEVELOPMENT_TENANT_ID ?? '5d963a18-c113-4bea-b2c7-c71a121e9f4b'
const msal = enabled ? new PublicClientApplication({ auth: {
  clientId: import.meta.env.VITE_ENTRA_CLIENT_ID ?? '',
  authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID ?? 'organizations'}`,
  redirectUri: window.location.origin,
}, cache: { cacheLocation: 'sessionStorage' } }) : null

export const authState = reactive({ enabled, ready: false, authenticated: !enabled, error: '',
  displayName: enabled ? '' : 'Local developer', tenantId: enabled ? '' : developmentTenantId,
  roles: enabled ? [] as string[] : ['Integration.Admin', 'Integration.Operator'] })

function applyAccount(account: AccountInfo | null) {
  if (!account) { authState.authenticated = false; authState.displayName = ''; authState.tenantId = ''; authState.roles = []; return }
  authState.authenticated = true; authState.displayName = account.name ?? account.username
}
function applyAccessToken(token: string) {
  const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, unknown>
  authState.tenantId = String(payload.relayworks_tenant_id ?? '')
  authState.roles = Array.isArray(payload.roles) ? payload.roles.map(String) : []
  if (!authState.tenantId) { authState.authenticated = false; authState.error = 'Your account is not assigned to a RelayWorks tenant.' }
}
export async function initializeAuth() {
  if (!msal) { authState.ready = true; return }
  try {
    if (!import.meta.env.VITE_ENTRA_CLIENT_ID || !apiScope) throw new Error('Entra client and API scope are required.')
    await msal.initialize(); await msal.handleRedirectPromise()
    applyAccount(msal.getActiveAccount() ?? msal.getAllAccounts()[0] ?? null)
    if (authState.authenticated) { const token = await accessToken(); if (token) applyAccessToken(token) }
  } catch { authState.authenticated = false; authState.error = 'Secure sign-in is not configured or could not be initialized.' }
  finally { authState.ready = true }
}
export async function signIn() { if (msal) { authState.error = ''; const r = await msal.loginPopup({ scopes: [apiScope] }); msal.setActiveAccount(r.account); applyAccount(r.account); if (r.accessToken) applyAccessToken(r.accessToken) } }
export async function signOut() { if (msal) await msal.logoutPopup(); applyAccount(null) }
export const hasRole = (role: string) => authState.roles.includes(role)
async function accessToken() {
  if (!msal) return null
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0]
  if (!account) throw new InteractionRequiredAuthError()
  try { return (await msal.acquireTokenSilent({ account, scopes: [apiScope] })).accessToken }
  catch (error) { if (!(error instanceof InteractionRequiredAuthError)) throw error
    return (await msal.acquireTokenPopup({ account, scopes: [apiScope] })).accessToken }
}
export async function authenticatedFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const token = await accessToken(); const headers = new Headers(init.headers)
  if (token) applyAccessToken(token)
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(input, { ...init, headers })
  if (response.status === 401 && enabled) authState.authenticated = false
  return response
}
