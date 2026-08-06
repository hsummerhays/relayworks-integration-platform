<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { listIntegrationRuns, submitIntegrationRun } from './api'
import { demoRuns } from './demoData'
import type { IntegrationRun, SubmitIntegrationRunRequest } from './types'

const runs = ref<IntegrationRun[]>([])
const loading = ref(true)
const submitting = ref(false)
const apiUnavailable = ref(false)
const message = ref('')

const form = reactive<SubmitIntegrationRunRequest>({
  tenantId: '5d963a18-c113-4bea-b2c7-c71a121e9f4b',
  connectionId: '857840a1-3440-431d-a696-07616926d50b',
  operation: 'TimeEntryExport',
  idempotencyKey: '',
  totalRecords: 1,
})

const attentionCount = computed(
  () => runs.value.filter((run) => run.status === 'CompletedWithErrors' || run.status === 'Failed').length,
)
const completedCount = computed(() => runs.value.filter((run) => run.status === 'Completed').length)
const processedRecords = computed(() =>
  runs.value.reduce((total, run) => total + run.acceptedRecords + run.rejectedRecords, 0),
)

function formatStatus(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

async function refreshRuns(): Promise<void> {
  loading.value = true
  try {
    runs.value = await listIntegrationRuns()
    apiUnavailable.value = false
  } catch {
    runs.value = demoRuns
    apiUnavailable.value = true
  } finally {
    loading.value = false
  }
}

async function submitRun(): Promise<void> {
  submitting.value = true
  message.value = ''

  try {
    const result = await submitIntegrationRun(form)
    message.value = result.isDuplicate
      ? 'Existing run returned for that idempotency key.'
      : 'Integration run submitted.'
    form.idempotencyKey = ''
    await refreshRuns()
  } catch (error) {
    message.value = error instanceof Error ? error.message : 'Unable to submit the run.'
  } finally {
    submitting.value = false
  }
}

onMounted(refreshRuns)
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <a class="brand" href="#" aria-label="RelayWorks home">
        <span class="brand-mark" aria-hidden="true">RW</span>
        <span>
          <strong>RelayWorks</strong>
          <small>Integration Operations</small>
        </span>
      </a>
      <div class="environment"><span></span> Reference environment</div>
    </header>

    <main>
      <section class="hero">
        <div>
          <p class="eyebrow">Operations console</p>
          <h1>Integration runs</h1>
          <p>Monitor synchronization activity, rejected records, and work requiring operator attention.</p>
        </div>
        <button class="secondary-button" type="button" :disabled="loading" @click="refreshRuns">
          {{ loading ? 'Refreshing…' : 'Refresh runs' }}
        </button>
      </section>

      <div v-if="apiUnavailable" class="notice" role="status">
        API unavailable. Showing representative demo data so the console remains reviewable.
      </div>

      <section class="metrics" aria-label="Integration run summary">
        <article><span>Total runs</span><strong>{{ runs.length }}</strong></article>
        <article><span>Completed</span><strong>{{ completedCount }}</strong></article>
        <article class="attention"><span>Needs attention</span><strong>{{ attentionCount }}</strong></article>
        <article><span>Records processed</span><strong>{{ processedRecords.toLocaleString() }}</strong></article>
      </section>

      <section class="workspace">
        <article class="panel run-list-panel">
          <div class="panel-heading">
            <div><p class="eyebrow">Recent activity</p><h2>Synchronization history</h2></div>
            <span>{{ runs.length }} runs</span>
          </div>

          <div class="table-wrap">
            <table>
              <thead>
                <tr><th>Route</th><th>Status</th><th>Records</th><th>Started</th></tr>
              </thead>
              <tbody>
                <tr v-for="run in runs" :key="run.id">
                  <td>
                    <strong>{{ formatStatus(run.operation) }}</strong>
                    <small>{{ run.idempotencyKey }}</small>
                  </td>
                  <td><span class="status" :data-status="run.status">{{ formatStatus(run.status) }}</span></td>
                  <td>
                    <strong>{{ run.acceptedRecords.toLocaleString() }} accepted</strong>
                    <small v-if="run.rejectedRecords">{{ run.rejectedRecords }} rejected</small>
                    <small v-else>{{ run.totalRecords.toLocaleString() }} submitted</small>
                  </td>
                  <td>{{ formatDate(run.createdAtUtc) }}</td>
                </tr>
                <tr v-if="!runs.length && !loading"><td colspan="4" class="empty">No runs submitted yet.</td></tr>
              </tbody>
            </table>
          </div>
        </article>

        <aside class="panel submit-panel">
          <p class="eyebrow">New work</p>
          <h2>Submit a run</h2>
          <p>Create an idempotent time-entry export for a configured customer connection.</p>

          <form @submit.prevent="submitRun">
            <label>Tenant ID<input v-model.trim="form.tenantId" required /></label>
            <label>Connection ID<input v-model.trim="form.connectionId" required /></label>
            <label>Operation<input value="Time entry export" disabled /></label>
            <label>Idempotency key<input v-model.trim="form.idempotencyKey" required placeholder="customer-records-date" /></label>
            <label>Record count<input v-model.number="form.totalRecords" required type="number" min="1" /></label>
            <button class="primary-button" type="submit" :disabled="submitting || apiUnavailable">
              {{ submitting ? 'Submitting…' : 'Submit integration run' }}
            </button>
          </form>
          <p v-if="message" class="form-message" role="status">{{ message }}</p>
          <small v-if="apiUnavailable" class="form-hint">Start the API to enable submissions.</small>
        </aside>
      </section>
    </main>
  </div>
</template>
