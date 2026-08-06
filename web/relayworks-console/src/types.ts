export type IntegrationRunStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'CompletedWithErrors'
  | 'Failed'

export type IntegrationOperation = 'TimeEntryExport'

export interface IntegrationRun {
  id: string
  tenantId: string
  connectionId: string
  operation: IntegrationOperation
  idempotencyKey: string
  totalRecords: number
  acceptedRecords: number
  rejectedRecords: number
  status: IntegrationRunStatus
  createdAtUtc: string
  completedAtUtc: string | null
}

export interface SubmitIntegrationRunRequest {
  tenantId: string
  connectionId: string
  operation: IntegrationOperation
  idempotencyKey: string
  totalRecords: number
}

export interface SubmitIntegrationRunResult {
  run: IntegrationRun
  isDuplicate: boolean
}
