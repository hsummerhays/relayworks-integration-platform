<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { createConnection, getConnectionTest, getLatestConnectionTest, listConnections, listIntegrationRuns, listRunRecords, resolveIssue, startConnectionTest, submitIntegrationRun } from './api'
import { demoConnections, demoRecords, demoRuns } from './demoData'
import type { ConnectionProfile, ConnectionTest, CreateConnectionProfileRequest, IntegrationRecordResult, IntegrationRun, IntegrationRunStatus, RunFilters, SubmitIntegrationRunRequest } from './types'
import { authState, hasRole, initializeAuth, signIn, signOut } from './auth'

const runs = ref<IntegrationRun[]>([])
const records = ref<IntegrationRecordResult[]>([])
const connections = ref<ConnectionProfile[]>([])
const selectedRunId = ref('')
const loading = ref(true)
const recordsLoading = ref(false)
const submitting = ref(false)
const apiUnavailable = ref(false)
const message = ref('')
const filter = ref<'all' | 'attention' | 'resolved'>('attention')
const runCursor = ref<string | undefined>()
const runNextCursor = ref<string | null>(null)
const runCursorHistory = ref<(string | undefined)[]>([])
const recordCursor = ref<string | undefined>()
const recordNextCursor = ref<string | null>(null)
const recordCursorHistory = ref<(string | undefined)[]>([])
const url = new URL(window.location.href)
const runFilters = reactive<{ status: '' | IntegrationRunStatus; connectionId: string; fromUtc: string; toUtc: string }>({
  status: (url.searchParams.get('status') as IntegrationRunStatus | null) ?? '',
  connectionId: url.searchParams.get('connectionId') ?? '',
  fromUtc: url.searchParams.get('fromUtc') ?? '',
  toUtc: url.searchParams.get('toUtc') ?? '',
})
const theme = ref(localStorage.getItem('relayworks-theme') ?? 'dark')
const resolutionNotes = reactive<Record<string, string>>({})
const connectionTests = reactive<Record<string, ConnectionTest>>({})
const connectionForm = reactive<CreateConnectionProfileRequest>({
  id: crypto.randomUUID(), name: '',
  provider: 'FieldFloAccounting', authType: 'ApiKey', supportsIdempotencyKey: true, supportsReadAfterWrite: true,
  maxConfirmedNoCommitRetries: 2, secretReference: '',
})

const form = reactive<SubmitIntegrationRunRequest>({
  connectionId: '857840a1-3440-431d-a696-07616926d50b',
  operation: 'TimeEntryExport', idempotencyKey: '', totalRecords: 1,
})

const attentionCount = computed(() => records.value.filter(r => ['Rejected', 'UnknownOutcome'].includes(r.status)).length)
const ambiguousCount = computed(() => records.value.filter(r => r.status === 'UnknownOutcome').length)
const completedCount = computed(() => runs.value.filter(r => r.status === 'Completed').length)
const processedRecords = computed(() => runs.value.reduce((sum, r) => sum + r.acceptedRecords + r.rejectedRecords, 0))
const selectedRun = computed(() => runs.value.find(r => r.id === selectedRunId.value))
const canAdmin = computed(() => hasRole('Integration.Admin'))
const canOperate = computed(() => canAdmin.value || hasRole('Integration.Operator'))

function formatStatus(value: string) { return value.replace(/([a-z])([A-Z])/g, '$1 $2') }
function formatDate(value: string) { return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }).format(new Date(value)) }

async function refreshRuns(reset = false) {
  if (reset) { runCursor.value = undefined; runCursorHistory.value = [] }
  loading.value = true
  try {
    const request: RunFilters = { pageSize: 25, cursor: runCursor.value }
    if (runFilters.status) request.status = runFilters.status
    if (runFilters.connectionId) request.connectionId = runFilters.connectionId
    if (runFilters.fromUtc) request.fromUtc = new Date(`${runFilters.fromUtc}T00:00:00Z`).toISOString()
    if (runFilters.toUtc) request.toUtc = new Date(`${runFilters.toUtc}T00:00:00Z`).toISOString()
    const page = await listIntegrationRuns(request)
    runs.value = page.items; runNextCursor.value = page.nextCursor; apiUnavailable.value = false
  } catch {
    if (authState.enabled) { runs.value = []; message.value = 'Unable to load authorized integration data.' }
    else { runs.value = demoRuns; apiUnavailable.value = true }
    runNextCursor.value = null
  }
  finally {
    loading.value = false
    if (!runs.value.some(run => run.id === selectedRunId.value)) selectedRunId.value = runs.value[0]?.id ?? ''
  }
}

function applyRunFilters() {
  const nextUrl = new URL(window.location.href)
  for (const key of ['status', 'connectionId', 'fromUtc', 'toUtc']) {
    const value = runFilters[key as keyof typeof runFilters]
    if (value) nextUrl.searchParams.set(key, value); else nextUrl.searchParams.delete(key)
  }
  window.history.replaceState({}, '', nextUrl)
  selectedRunId.value = ''
  void refreshRuns(true)
}

async function nextRunPage() {
  if (!runNextCursor.value) return
  runCursorHistory.value.push(runCursor.value); runCursor.value = runNextCursor.value
  await refreshRuns()
}
async function previousRunPage() {
  runCursor.value = runCursorHistory.value.pop(); await refreshRuns()
}

async function loadConnections() {
  try {
    connections.value = await listConnections()
    await Promise.all(connections.value.map(async connection => {
      const latest = await getLatestConnectionTest(connection.id); if (latest) connectionTests[connection.id] = latest
    }))
  }
  catch {
    if (authState.enabled) { connections.value = []; message.value = 'Unable to load authorized connections.' }
    else connections.value = demoConnections
  }
}

async function saveConnection() {
  try {
    const created = await createConnection(connectionForm)
    connections.value.push(created); form.connectionId = created.id
    connectionForm.id = crypto.randomUUID(); connectionForm.name = ''; connectionForm.secretReference = ''
  } catch (error) { message.value = error instanceof Error ? error.message : 'Unable to save connection.' }
}
async function signInAndLoad() {
  try { await signIn(); if (authState.authenticated) await Promise.all([refreshRuns(), loadConnections()]) }
  catch { authState.error = 'Microsoft sign-in did not complete. Please try again.' }
}

const wait = (milliseconds: number) => new Promise(resolve => window.setTimeout(resolve, milliseconds))
async function testConnection(connection: ConnectionProfile) {
  if (apiUnavailable.value) {
    connectionTests[connection.id] = { id: crypto.randomUUID(), tenantId: connection.tenantId,
      connectionId: connection.id, configurationVersion: connection.configurationVersion, status: 'Pending',
      failureCategory: null, safeMessage: null, requestedBy: 'demo', requestedAtUtc: new Date().toISOString(),
      completedAtUtc: null, durationMilliseconds: null }
    await wait(1200); connectionTests[connection.id].status = 'Succeeded'
    connectionTests[connection.id].safeMessage = 'Authentication and provider reachability confirmed.'; return
  }
  let test = await startConnectionTest(connection.id); connectionTests[connection.id] = test
  const deadline = Date.now() + 60_000
  while (test.status === 'Pending' && Date.now() < deadline) {
    await wait(2000); test = await getConnectionTest(connection.id, test.id); connectionTests[connection.id] = test
  }
  if (test.status === 'Pending') message.value = 'The test is still running. You can leave this page and check again later.'
}

async function loadRecords(runId: string, reset = false) {
  if (!runId) return
  if (reset) { recordCursor.value = undefined; recordCursorHistory.value = [] }
  recordsLoading.value = true
  try {
    const page = await listRunRecords(runId, filter.value, recordCursor.value)
    records.value = page.items; recordNextCursor.value = page.nextCursor
  }
  catch { records.value = authState.enabled ? [] : demoRecords.filter(r => r.runId === runId); recordNextCursor.value = null }
  finally { recordsLoading.value = false }
}

async function nextRecordPage() {
  if (!recordNextCursor.value || !selectedRunId.value) return
  recordCursorHistory.value.push(recordCursor.value); recordCursor.value = recordNextCursor.value
  await loadRecords(selectedRunId.value)
}
async function previousRecordPage() {
  if (!selectedRunId.value) return
  recordCursor.value = recordCursorHistory.value.pop(); await loadRecords(selectedRunId.value)
}

async function submitRun() {
  submitting.value = true; message.value = ''
  try {
    const result = await submitIntegrationRun(form)
    message.value = result.isDuplicate ? 'Existing run returned for that idempotency key.' : 'Integration run submitted.'
    form.idempotencyKey = ''; await refreshRuns(true)
  } catch (error) { message.value = error instanceof Error ? error.message : 'Unable to submit the run.' }
  finally { submitting.value = false }
}

async function markResolved(record: IntegrationRecordResult) {
  const notes = resolutionNotes[record.id]?.trim()
  if (!notes) return
  if (apiUnavailable.value) {
    record.status = 'ManuallyResolved'; record.resolutionNotes = notes
  } else {
    const updated = await resolveIssue(record.id, notes)
    records.value = records.value.map(item => item.id === updated.id ? updated : item)
  }
  resolutionNotes[record.id] = ''
}

function toggleTheme() {
  theme.value = theme.value === 'dark' ? 'light' : 'dark'
  localStorage.setItem('relayworks-theme', theme.value)
}

watch(selectedRunId, runId => loadRecords(runId, true))
watch(filter, () => selectedRunId.value && loadRecords(selectedRunId.value, true))
onMounted(async () => {
  await initializeAuth()
  if (!authState.authenticated) return
  await Promise.all([refreshRuns(), loadConnections()])
})
</script>

<template>
  <div class="app-shell" :data-theme="theme">
    <div v-if="!authState.ready" class="auth-gate"><span class="brand-mark">RW</span><p>Establishing secure session…</p></div>
    <div v-else-if="!authState.authenticated" class="auth-gate"><span class="brand-mark">RW</span><h1>RelayWorks</h1><p>{{ authState.error || 'Sign in with your organization account to continue.' }}</p><button class="primary-button" @click="signInAndLoad">Sign in with Microsoft</button></div>
    <header v-if="authState.authenticated" class="topbar">
      <a class="brand" href="#"><span class="brand-mark">RW</span><span><strong>RelayWorks</strong><small>Integration Operations</small></span></a>
      <div class="top-actions"><div class="identity"><strong>{{ authState.displayName }}</strong><small>Tenant {{ authState.tenantId.slice(0, 8) }}</small></div><div class="environment"><span></span> Reference environment</div><button class="icon-button" @click="toggleTheme">{{ theme === 'dark' ? 'Light' : 'Dark' }} mode</button><button class="icon-button" @click="signOut">Sign out</button></div>
    </header>

    <main v-if="authState.authenticated">
      <section class="hero"><div><p class="eyebrow">Operations console</p><h1>Integration control room</h1><p>Trace every construction record, isolate uncertain writes, and reconcile exceptions without risking duplicate payroll or billing.</p></div><button class="secondary-button" :disabled="loading" @click="refreshRuns(false)">{{ loading ? 'Refreshing…' : 'Refresh runs' }}</button></section>
      <div v-if="apiUnavailable" class="notice">API unavailable. Representative operations data is active.</div>

      <section class="metrics">
        <article><span>Visible runs</span><strong>{{ runs.length }}</strong><small>Current filtered page</small></article>
        <article><span>Clean completions</span><strong>{{ completedCount }}</strong><small>No record exceptions</small></article>
        <article class="attention"><span>Open issues</span><strong>{{ attentionCount }}</strong><small>{{ ambiguousCount }} require reconciliation</small></article>
        <article><span>Records processed</span><strong>{{ processedRecords.toLocaleString() }}</strong><small>Current history window</small></article>
      </section>

      <details class="panel connection-manager">
        <summary><span><span class="eyebrow">Connector registry</span><strong>{{ connections.length }} configured connection{{ connections.length === 1 ? '' : 's' }}</strong></span><span>Manage capabilities</span></summary>
        <div class="connection-grid">
          <article v-for="connection in connections" :key="connection.id" class="connection-card">
            <div><strong>{{ connection.name }}</strong><small>{{ connection.provider }} · {{ connection.authType }} · {{ connection.configurationVersion.slice(0, 8) }}</small><small v-if="connectionTests[connection.id]" class="test-result" :data-status="connectionTests[connection.id].status">{{ formatStatus(connectionTests[connection.id].status) }}<template v-if="connectionTests[connection.id].safeMessage"> · {{ connectionTests[connection.id].safeMessage }}</template><template v-if="connectionTests[connection.id].completedAtUtc"> · {{ formatDate(connectionTests[connection.id].completedAtUtc!) }}<template v-if="connectionTests[connection.id].durationMilliseconds"> ({{ connectionTests[connection.id].durationMilliseconds }} ms)</template></template></small></div>
            <div class="capabilities"><span class="enabled">{{ connection.authType }} Auth</span><span class="enabled">Secret Configured</span><span :class="{ enabled: connection.supportsIdempotencyKey }">Idempotency key</span><span :class="{ enabled: connection.supportsReadAfterWrite }">Read after write</span><span>{{ connection.maxConfirmedNoCommitRetries }} safe retries</span></div>
            <button class="secondary-button test-button" :disabled="!canOperate || connectionTests[connection.id]?.status === 'Pending'" @click="testConnection(connection)">{{ connectionTests[connection.id]?.status === 'Pending' ? 'Testing…' : 'Test connection' }}</button>
          </article>
          <form v-if="canAdmin" class="connection-form" @submit.prevent="saveConnection">
            <label>Connection name<input v-model.trim="connectionForm.name" required placeholder="FieldFlo → Sage 100" /></label>
            <label>Provider<input v-model.trim="connectionForm.provider" required /></label>
            <label>Authentication strategy<select v-model="connectionForm.authType"><option value="ApiKey">API Key (Header)</option><option value="OAuth2">OAuth 2.0 (Client Credentials)</option><option value="Basic">Basic Auth (Base64)</option><option value="MutualTls">Mutual TLS (Certificate)</option></select></label>
            <label>Key Vault secret URI<input v-model.trim="connectionForm.secretReference" required placeholder="https://vault.vault.azure.net/secrets/customer" /></label>
            <label class="check"><input v-model="connectionForm.supportsIdempotencyKey" type="checkbox" /> Native idempotency key</label>
            <label class="check"><input v-model="connectionForm.supportsReadAfterWrite" type="checkbox" /> Read-after-write lookup</label>
            <label>Safe retries<input v-model.number="connectionForm.maxConfirmedNoCommitRetries" type="number" min="0" max="10" /></label>
            <button class="primary-button" :disabled="apiUnavailable">Save connection</button>
          </form>
        </div>
      </details>

      <section class="workspace">
        <div class="main-column">
          <article class="panel run-list-panel">
            <div class="panel-heading"><div><p class="eyebrow">Recent activity</p><h2>Synchronization history</h2></div><span>{{ runs.length }} runs</span></div>
            <form class="run-filters" @submit.prevent="applyRunFilters">
              <label>Status<select v-model="runFilters.status"><option value="">All statuses</option><option v-for="value in ['Pending','Running','Completed','CompletedWithErrors','Failed']" :key="value" :value="value">{{ formatStatus(value) }}</option></select></label>
              <label>Connection<select v-model="runFilters.connectionId"><option value="">All connections</option><option v-for="connection in connections" :key="connection.id" :value="connection.id">{{ connection.name }}</option></select></label>
              <label>From<input v-model="runFilters.fromUtc" type="date" /></label><label>Before<input v-model="runFilters.toUtc" type="date" /></label>
              <button class="secondary-button">Apply filters</button>
            </form>
            <div class="table-wrap"><table><thead><tr><th>Route</th><th>Status</th><th>Records</th><th>Started</th></tr></thead><tbody>
              <tr v-for="run in runs" :key="run.id" :class="{ selected: selectedRunId === run.id }" tabindex="0" @click="selectedRunId = run.id" @keydown.enter="selectedRunId = run.id">
                <td><strong>{{ formatStatus(run.operation) }}</strong><small>{{ run.idempotencyKey }}</small></td>
                <td><span class="status" :data-status="run.status">{{ formatStatus(run.status) }}</span></td>
                <td><strong>{{ run.acceptedRecords.toLocaleString() }} accepted</strong><small>{{ run.rejectedRecords ? `${run.rejectedRecords} requiring attention` : `${run.totalRecords} submitted` }}</small></td>
                <td>{{ formatDate(run.createdAtUtc) }}</td>
              </tr>
            </tbody></table></div>
            <div class="pager"><button class="secondary-button" :disabled="!runCursorHistory.length || loading" @click="previousRunPage">Previous</button><span>Page {{ runCursorHistory.length + 1 }}</span><button class="secondary-button" :disabled="!runNextCursor || loading" @click="nextRunPage">Next</button></div>
          </article>

          <article class="panel records-panel">
            <div class="panel-heading"><div><p class="eyebrow">Record intelligence</p><h2>{{ selectedRun ? selectedRun.idempotencyKey : 'Select a run' }}</h2></div><div class="filters"><button v-for="value in ['attention','all','resolved']" :key="value" :class="{ active: filter === value }" @click="filter = value as typeof filter">{{ value }}</button></div></div>
            <div v-if="recordsLoading" class="empty">Loading record outcomes…</div>
            <div v-else-if="!records.length" class="empty">No records match this view.</div>
            <div v-else class="issue-list">
              <article v-for="record in records" :key="record.id" class="issue" :data-status="record.status">
                <div class="issue-icon">{{ record.status === 'UnknownOutcome' ? '?' : record.status === 'Rejected' ? '!' : '✓' }}</div>
                <div class="issue-body"><div class="issue-title"><strong>{{ record.sourceRecordId }}</strong><span class="status" :data-status="record.status">{{ formatStatus(record.status) }}</span></div>
                  <p>{{ record.errorMessage || record.resolutionNotes || 'Delivered successfully.' }}</p>
                  <small>{{ record.employeeReference }} · {{ record.projectReference || 'Project missing' }} · version {{ record.sourceVersion }}</small>
                  <div v-if="canOperate && ['Rejected','UnknownOutcome'].includes(record.status)" class="resolve-row"><input v-model="resolutionNotes[record.id]" placeholder="Document verification or corrective action" /><button class="primary-button" :disabled="!resolutionNotes[record.id]?.trim()" @click="markResolved(record)">Mark resolved</button></div>
                </div>
              </article>
            </div>
            <div class="pager"><button class="secondary-button" :disabled="!recordCursorHistory.length || recordsLoading" @click="previousRecordPage">Previous</button><span>Page {{ recordCursorHistory.length + 1 }}</span><button class="secondary-button" :disabled="!recordNextCursor || recordsLoading" @click="nextRecordPage">Next</button></div>
          </article>
        </div>

        <aside class="panel submit-panel"><p class="eyebrow">New work</p><h2>Submit a run</h2><p>Create an idempotent time-entry export for a configured connection.</p>
          <form @submit.prevent="submitRun"><label>Authenticated tenant<input :value="authState.tenantId" disabled /></label><label>Connection ID<input v-model.trim="form.connectionId" required /></label><label>Operation<input value="Time entry export" disabled /></label><label>Idempotency key<input v-model.trim="form.idempotencyKey" required placeholder="customer-records-date" /></label><label>Record count<input v-model.number="form.totalRecords" required type="number" min="1" /></label><button class="primary-button" :disabled="submitting || apiUnavailable || !canOperate">{{ submitting ? 'Submitting…' : 'Submit integration run' }}</button></form>
          <p v-if="message" class="form-message">{{ message }}</p><small v-if="apiUnavailable" class="form-hint">Start the API to enable submissions.</small>
        </aside>
      </section>
    </main>
  </div>
</template>
