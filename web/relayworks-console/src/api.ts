import type {
  IntegrationRun,
  SubmitIntegrationRunRequest,
  SubmitIntegrationRunResult,
} from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'

async function ensureSuccess(response: Response): Promise<Response> {
  if (response.ok) return response

  const detail = await response.text()
  throw new Error(detail || `Request failed with status ${response.status}`)
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
