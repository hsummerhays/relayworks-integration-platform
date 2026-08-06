import type { IntegrationRun } from './types'

const tenantId = '5d963a18-c113-4bea-b2c7-c71a121e9f4b'

export const demoRuns: IntegrationRun[] = [
  {
    id: '9ec842a0-4f46-4bd9-b6e2-308272828a32',
    tenantId,
    connectionId: '857840a1-3440-431d-a696-07616926d50b',
    operation: 'TimeEntryExport',
    idempotencyKey: 'northwind-time-2026-w32',
    totalRecords: 248,
    acceptedRecords: 224,
    rejectedRecords: 24,
    status: 'CompletedWithErrors',
    createdAtUtc: '2026-08-06T15:42:00Z',
    completedAtUtc: '2026-08-06T15:43:18Z',
  },
  {
    id: '1b8c6464-aefe-47cb-b639-a7617d3392ac',
    tenantId,
    connectionId: 'f13866b8-8826-41d5-bd6e-0ddb41b89a49',
    operation: 'TimeEntryExport',
    idempotencyKey: 'summit-time-2026-w32',
    totalRecords: 86,
    acceptedRecords: 86,
    rejectedRecords: 0,
    status: 'Completed',
    createdAtUtc: '2026-08-06T14:30:00Z',
    completedAtUtc: '2026-08-06T14:30:44Z',
  },
  {
    id: 'ec3d8d66-fe40-44f1-86f1-1ed9164b8f23',
    tenantId,
    connectionId: '857840a1-3440-431d-a696-07616926d50b',
    operation: 'TimeEntryExport',
    idempotencyKey: 'apex-time-2026-w32',
    totalRecords: 34,
    acceptedRecords: 0,
    rejectedRecords: 0,
    status: 'Running',
    createdAtUtc: '2026-08-06T16:02:00Z',
    completedAtUtc: null,
  },
]
