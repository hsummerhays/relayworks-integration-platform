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

export type RecordResultStatus =
  | 'Processing' | 'Succeeded' | 'Rejected' | 'RetryableFailure' | 'UnknownOutcome' | 'ManuallyResolved'

export interface IntegrationRecordResult {
  id: string
  runId: string
  tenantId: string
  sourceRecordId: string
  sourceVersion: string
  employeeReference: string
  projectReference: string
  status: RecordResultStatus
  errorCode: string | null
  errorMessage: string | null
  destinationReference: string | null
  resolutionNotes: string | null
  updatedAtUtc: string
}

export interface ConnectionProfile {
  id: string
  tenantId: string
  name: string
  provider: string
  supportsIdempotencyKey: boolean
  supportsReadAfterWrite: boolean
  maxConfirmedNoCommitRetries: number
  secretReference: string
  configurationVersion: string
  isActive: boolean
  updatedAtUtc: string
}

export type CreateConnectionProfileRequest = Omit<ConnectionProfile, 'configurationVersion' | 'isActive' | 'updatedAtUtc'>
