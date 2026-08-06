import type {
  IntegrationRun,
  SubmitIntegrationRunRequest,
  SubmitIntegrationRunResult,
  IntegrationRecordResult,
  ConnectionProfile,
  CreateConnectionProfileRequest,
} from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'

async function ensureSuccess(response: Response): Promise<Response> {
  if (response.ok) return response

  const detail = await response.text()
  throw new Error(detail || `Request failed with status ${response.status}`)
}

export async function listConnections(tenantId: string): Promise<ConnectionProfile[]> {
  const response = await ensureSuccess(await fetch(`${apiBaseUrl}/api/connections?tenantId=${tenantId}`))
  return response.json() as Promise<ConnectionProfile[]>
}

export async function createConnection(request: CreateConnectionProfileRequest): Promise<ConnectionProfile> {
  const response = await ensureSuccess(await fetch(`${apiBaseUrl}/api/connections`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
  }))
  return response.json() as Promise<ConnectionProfile>
}

export async function listRunRecords(runId: string): Promise<IntegrationRecordResult[]> {
  const response = await ensureSuccess(await fetch(`${apiBaseUrl}/api/integration-runs/${runId}/records`))
  return response.json() as Promise<IntegrationRecordResult[]>
}

export async function resolveIssue(id: string, resolutionNotes: string): Promise<IntegrationRecordResult> {
  const response = await ensureSuccess(await fetch(`${apiBaseUrl}/api/reconciliation-issues/${id}/resolve`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resolutionNotes }),
  }))
  return response.json() as Promise<IntegrationRecordResult>
}

export async function listIntegrationRuns(): Promise<IntegrationRun[]> {
  const response = await ensureSuccess(await fetch(`${apiBaseUrl}/api/integration-runs`))
  return response.json() as Promise<IntegrationRun[]>
}

export async function submitIntegrationRun(
  request: SubmitIntegrationRunRequest,
): Promise<SubmitIntegrationRunResult> {
  const response = await ensureSuccess(
    await fetch(`${apiBaseUrl}/api/integration-runs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),
  )

  return response.json() as Promise<SubmitIntegrationRunResult>
}
