import type {
  IntegrationRun,
  SubmitIntegrationRunRequest,
  SubmitIntegrationRunResult,
  IntegrationRecordResult,
  ConnectionProfile,
  CreateConnectionProfileRequest,
  ConnectionTest,
  PagedResult,
  RunFilters,
} from './types'
import { authenticatedFetch } from './auth'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'

async function ensureSuccess(response: Response): Promise<Response> {
  if (response.ok) return response

  const detail = await response.text()
  throw new Error(detail || `Request failed with status ${response.status}`)
}

export async function startConnectionTest(connectionId: string): Promise<ConnectionTest> {
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/connections/${connectionId}/tests`, { method: 'POST' }))
  return response.json() as Promise<ConnectionTest>
}

export async function getConnectionTest(connectionId: string, testId: string): Promise<ConnectionTest> {
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/connections/${connectionId}/tests/${testId}`))
  return response.json() as Promise<ConnectionTest>
}
export async function getLatestConnectionTest(connectionId: string): Promise<ConnectionTest | null> {
  const response = await authenticatedFetch(`${apiBaseUrl}/api/connections/${connectionId}/tests/latest`)
  if (response.status === 404) return null
  return ensureSuccess(response).then(value => value.json() as Promise<ConnectionTest>)
}

export async function listConnections(): Promise<ConnectionProfile[]> {
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/connections`))
  return response.json() as Promise<ConnectionProfile[]>
}

export async function createConnection(request: CreateConnectionProfileRequest): Promise<ConnectionProfile> {
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/connections`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
  }))
  return response.json() as Promise<ConnectionProfile>
}

export async function listRunRecords(runId: string, view: string, cursor?: string): Promise<PagedResult<IntegrationRecordResult>> {
  const query = new URLSearchParams({ view, pageSize: '25' })
  if (cursor) query.set('cursor', cursor)
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/integration-runs/${runId}/records?${query}`))
  return response.json() as Promise<PagedResult<IntegrationRecordResult>>
}

export async function resolveIssue(id: string, resolutionNotes: string): Promise<IntegrationRecordResult> {
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/reconciliation-issues/${id}/resolve`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resolutionNotes }),
  }))
  return response.json() as Promise<IntegrationRecordResult>
}

export async function listIntegrationRuns(filters: RunFilters = {}): Promise<PagedResult<IntegrationRun>> {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) if (value) query.set(key, String(value))
  const response = await ensureSuccess(await authenticatedFetch(`${apiBaseUrl}/api/integration-runs?${query}`))
  return response.json() as Promise<PagedResult<IntegrationRun>>
}

export async function submitIntegrationRun(
  request: SubmitIntegrationRunRequest,
): Promise<SubmitIntegrationRunResult> {
  const response = await ensureSuccess(
    await authenticatedFetch(`${apiBaseUrl}/api/integration-runs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),
  )

  return response.json() as Promise<SubmitIntegrationRunResult>
}
