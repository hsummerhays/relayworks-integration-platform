<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { listIntegrationRuns, listRunRecords, resolveIssue, submitIntegrationRun } from './api'
import { demoRecords, demoRuns } from './demoData'
import type { IntegrationRecordResult, IntegrationRun, SubmitIntegrationRunRequest } from './types'

const runs = ref<IntegrationRun[]>([])
const records = ref<IntegrationRecordResult[]>([])
const selectedRunId = ref('')
const loading = ref(true)
const recordsLoading = ref(false)
const submitting = ref(false)
const apiUnavailable = ref(false)
const message = ref('')
const filter = ref<'all' | 'attention' | 'resolved'>('attention')
const theme = ref(localStorage.getItem('relayworks-theme') ?? 'dark')
const resolutionNotes = reactive<Record<string, string>>({})

const form = reactive<SubmitIntegrationRunRequest>({
  tenantId: '5d963a18-c113-4bea-b2c7-c71a121e9f4b',
  connectionId: '857840a1-3440-431d-a696-07616926d50b',
  operation: 'TimeEntryExport', idempotencyKey: '', totalRecords: 1,
})

const attentionCount = computed(() => records.value.filter(r => ['Rejected', 'UnknownOutcome'].includes(r.status)).length)
const ambiguousCount = computed(() => records.value.filter(r => r.status === 'UnknownOutcome').length)
const completedCount = computed(() => runs.value.filter(r => r.status === 'Completed').length)
const processedRecords = computed(() => runs.value.reduce((sum, r) => sum + r.acceptedRecords + r.rejectedRecords, 0))
const selectedRun = computed(() => runs.value.find(r => r.id === selectedRunId.value))
const filteredRecords = computed(() => records.value.filter(record =>
  filter.value === 'all' || (filter.value === 'attention' && ['Rejected', 'UnknownOutcome'].includes(record.status)) ||
  (filter.value === 'resolved' && record.status === 'ManuallyResolved')))

function formatStatus(value: string) { return value.replace(/([a-z])([A-Z])/g, '$1 $2') }
function formatDate(value: string) { return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }).format(new Date(value)) }

async function refreshRuns() {
  loading.value = true
  try {
    runs.value = await listIntegrationRuns(); apiUnavailable.value = false
  } catch { runs.value = demoRuns; apiUnavailable.value = true }
  finally {
    loading.value = false
    if (!selectedRunId.value && runs.value.length) selectedRunId.value = runs.value[0].id
  }
}

async function loadRecords(runId: string) {
  if (!runId) return
  recordsLoading.value = true
  try { records.value = await listRunRecords(runId) }
  catch { records.value = demoRecords.filter(r => r.runId === runId) }
  finally { recordsLoading.value = false }
}

async function submitRun() {
  submitting.value = true; message.value = ''
  try {
    const result = await submitIntegrationRun(form)
    message.value = result.isDuplicate ? 'Existing run returned for that idempotency key.' : 'Integration run submitted.'
    form.idempotencyKey = ''; await refreshRuns()
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

watch(selectedRunId, loadRecords)
onMounted(refreshRuns)
</script>

<template>
  <div class="app-shell" :data-theme="theme">
    <header class="topbar">
      <a class="brand" href="#"><span class="brand-mark">RW</span><span><strong>RelayWorks</strong><small>Integration Operations</small></span></a>
      <div class="top-actions"><div class="environment"><span></span> Reference environment</div><button class="icon-button" @click="toggleTheme">{{ theme === 'dark' ? 'Light' : 'Dark' }} mode</button></div>
    </header>

    <main>
      <section class="hero"><div><p class="eyebrow">Operations console</p><h1>Integration control room</h1><p>Trace every construction record, isolate uncertain writes, and reconcile exceptions without risking duplicate payroll or billing.</p></div><button class="secondary-button" :disabled="loading" @click="refreshRuns">{{ loading ? 'Refreshing…' : 'Refresh runs' }}</button></section>
      <div v-if="apiUnavailable" class="notice">API unavailable. Representative operations data is active.</div>

      <section class="metrics">
        <article><span>Total runs</span><strong>{{ runs.length }}</strong><small>Across configured routes</small></article>
        <article><span>Clean completions</span><strong>{{ completedCount }}</strong><small>No record exceptions</small></article>
        <article class="attention"><span>Open issues</span><strong>{{ attentionCount }}</strong><small>{{ ambiguousCount }} require reconciliation</small></article>
        <article><span>Records processed</span><strong>{{ processedRecords.toLocaleString() }}</strong><small>Current history window</small></article>
      </section>

      <section class="workspace">
        <div class="main-column">
          <article class="panel run-list-panel">
            <div class="panel-heading"><div><p class="eyebrow">Recent activity</p><h2>Synchronization history</h2></div><span>{{ runs.length }} runs</span></div>
            <div class="table-wrap"><table><thead><tr><th>Route</th><th>Status</th><th>Records</th><th>Started</th></tr></thead><tbody>
              <tr v-for="run in runs" :key="run.id" :class="{ selected: selectedRunId === run.id }" tabindex="0" @click="selectedRunId = run.id" @keydown.enter="selectedRunId = run.id">
                <td><strong>{{ formatStatus(run.operation) }}</strong><small>{{ run.idempotencyKey }}</small></td>
                <td><span class="status" :data-status="run.status">{{ formatStatus(run.status) }}</span></td>
                <td><strong>{{ run.acceptedRecords.toLocaleString() }} accepted</strong><small>{{ run.rejectedRecords ? `${run.rejectedRecords} requiring attention` : `${run.totalRecords} submitted` }}</small></td>
                <td>{{ formatDate(run.createdAtUtc) }}</td>
              </tr>
            </tbody></table></div>
          </article>

          <article class="panel records-panel">
            <div class="panel-heading"><div><p class="eyebrow">Record intelligence</p><h2>{{ selectedRun ? selectedRun.idempotencyKey : 'Select a run' }}</h2></div><div class="filters"><button v-for="value in ['attention','all','resolved']" :key="value" :class="{ active: filter === value }" @click="filter = value as typeof filter">{{ value }}</button></div></div>
            <div v-if="recordsLoading" class="empty">Loading record outcomes…</div>
            <div v-else-if="!filteredRecords.length" class="empty">No records match this view.</div>
            <div v-else class="issue-list">
              <article v-for="record in filteredRecords" :key="record.id" class="issue" :data-status="record.status">
                <div class="issue-icon">{{ record.status === 'UnknownOutcome' ? '?' : record.status === 'Rejected' ? '!' : '✓' }}</div>
                <div class="issue-body"><div class="issue-title"><strong>{{ record.sourceRecordId }}</strong><span class="status" :data-status="record.status">{{ formatStatus(record.status) }}</span></div>
                  <p>{{ record.errorMessage || record.resolutionNotes || 'Delivered successfully.' }}</p>
                  <small>{{ record.employeeReference }} · {{ record.projectReference || 'Project missing' }} · version {{ record.sourceVersion }}</small>
                  <div v-if="['Rejected','UnknownOutcome'].includes(record.status)" class="resolve-row"><input v-model="resolutionNotes[record.id]" placeholder="Document verification or corrective action" /><button class="primary-button" :disabled="!resolutionNotes[record.id]?.trim()" @click="markResolved(record)">Mark resolved</button></div>
                </div>
              </article>
            </div>
          </article>
        </div>

        <aside class="panel submit-panel"><p class="eyebrow">New work</p><h2>Submit a run</h2><p>Create an idempotent time-entry export for a configured connection.</p>
          <form @submit.prevent="submitRun"><label>Tenant ID<input v-model.trim="form.tenantId" required /></label><label>Connection ID<input v-model.trim="form.connectionId" required /></label><label>Operation<input value="Time entry export" disabled /></label><label>Idempotency key<input v-model.trim="form.idempotencyKey" required placeholder="customer-records-date" /></label><label>Record count<input v-model.number="form.totalRecords" required type="number" min="1" /></label><button class="primary-button" :disabled="submitting || apiUnavailable">{{ submitting ? 'Submitting…' : 'Submit integration run' }}</button></form>
          <p v-if="message" class="form-message">{{ message }}</p><small v-if="apiUnavailable" class="form-hint">Start the API to enable submissions.</small>
        </aside>
      </section>
    </main>
  </div>
</template>
