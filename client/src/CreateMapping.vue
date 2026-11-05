<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { api } from './api.js'

const props = defineProps({ fields: { type: Array, default: () => [] } })
const emit = defineEmits(['done'])

const step = ref('upload')
const busy = ref(false)
const message = ref('')
const upload = ref(null)
const worksheet = ref('')
const columns = ref([])
const rowCount = ref(0)
const suggestions = ref({})
const filter = ref('')
const assess = ref({})
const testResult = ref(null)
const assignments = reactive({})
let assessTimer = 0

const visibleFields = computed(() => {
  const query = filter.value.trim().toLowerCase()
  if (!query) return props.fields
  return props.fields.filter(field =>
    field.title.toLowerCase().includes(query) || field.column.toLowerCase().includes(query))
})

const assignedCount = computed(() =>
  props.fields.filter(field => assignments[field.column]?.mode && assignments[field.column].mode !== 'unassigned').length)

watch(() => props.fields, ensureAssignments, { immediate: true })

function ensureAssignments() {
  for (const field of props.fields) {
    if (!assignments[field.column]) {
      assignments[field.column] = {
        mode: 'unassigned',
        sourceHeader: '',
        constant: '',
        dateFormat: '',
        decimalSeparator: '',
        thousandsSeparator: '',
        thousandsNone: false
      }
    }
  }
}

function mappingBody() {
  return {
    format: 'annuity-import-mapping',
    version: 1,
    worksheet: worksheet.value,
    sourceHeaders: columns.value.map(column => column.header),
    assignments: props.fields.map(field => {
      const entry = assignments[field.column]
      return {
        target: field.column,
        mode: entry.mode,
        sourceHeader: entry.mode === 'source' ? entry.sourceHeader || null : null,
        constant: entry.mode === 'constant' ? entry.constant : null,
        dateFormat: entry.mode === 'source' && entry.dateFormat ? entry.dateFormat : null,
        decimalSeparator: entry.mode === 'source' && entry.decimalSeparator ? entry.decimalSeparator : null,
        thousandsSeparator: entry.mode === 'source' && !entry.thousandsNone && entry.thousandsSeparator
          ? entry.thousandsSeparator
          : null,
        thousandsSeparatorNone: entry.mode === 'source' ? entry.thousandsNone : false
      }
    })
  }
}

function scheduleAssess() {
  clearTimeout(assessTimer)
  assessTimer = setTimeout(runAssess, 250)
}

async function runAssess() {
  if (!upload.value || step.value !== 'configure') return
  try {
    const result = await api(`/api/uploads/${upload.value.uploadId}/assess`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(mappingBody())
    })
    assess.value = result.targets || {}
  } catch (error) {
    message.value = error.message
  }
}

async function onExcel(event) {
  const file = event.target.files?.[0]
  if (!file) return
  busy.value = true
  message.value = ''
  testResult.value = null
  for (const key of Object.keys(assignments)) delete assignments[key]
  assess.value = {}
  const body = new FormData()
  body.append('file', file)
  try {
    const data = await api('/api/uploads', { method: 'POST', body })
    upload.value = data
    if (data.needsWorksheet) {
      worksheet.value = ''
      step.value = 'sheet'
    } else {
      worksheet.value = data.selectedWorksheet
      await loadDataset()
    }
  } catch (error) {
    message.value = error.message
  } finally {
    busy.value = false
    event.target.value = ''
  }
}

async function chooseSheet(name) {
  busy.value = true
  message.value = ''
  try {
    await api(`/api/uploads/${upload.value.uploadId}/worksheet`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ worksheet: name })
    })
    worksheet.value = name
    await loadDataset()
  } catch (error) {
    message.value = error.message
  } finally {
    busy.value = false
  }
}

async function loadDataset() {
  const data = await api(`/api/uploads/${upload.value.uploadId}/dataset`)
  columns.value = data.columns
  rowCount.value = data.rowCount
  suggestions.value = data.suggestions || {}
  worksheet.value = data.worksheet
  ensureAssignments()
  step.value = 'configure'
  await runAssess()
}

function useSuggestion(field) {
  const header = suggestions.value[field.column]
  if (!header) return
  assignments[field.column].mode = 'source'
  assignments[field.column].sourceHeader = header
  scheduleAssess()
}

async function runTest() {
  busy.value = true
  message.value = ''
  try {
    testResult.value = await api(`/api/uploads/${upload.value.uploadId}/test`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(mappingBody())
    })
  } catch (error) {
    message.value = error.message
    testResult.value = null
  } finally {
    busy.value = false
  }
}

async function exportMapping() {
  await runTest()
  if (!testResult.value?.ok) {
    message.value = 'Fix the mapping errors before exporting.'
    return
  }

  const blob = new Blob([JSON.stringify(mappingBody(), null, 2)], { type: 'application/json' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = 'case-transfert-mapping.json'
  link.click()
  URL.revokeObjectURL(link.href)
  step.value = 'done'
}

function fieldInfo(column) {
  return assess.value[column] || {}
}
</script>

<template>
  <section v-if="step === 'upload'" class="panel">
    <h2>Create new mapping</h2>
    <p class="muted">Upload one .xlsx file. Row 1 is the header row and data starts on row 2. A saved mapping cannot be opened here.</p>
    <label class="file">
      <input type="file" accept=".xlsx" :disabled="busy" @change="onExcel" />
    </label>
    <p v-if="message" class="error">{{ message }}</p>
  </section>

  <section v-else-if="step === 'sheet'" class="panel">
    <h2>Choose a worksheet</h2>
    <p class="muted">{{ upload.fileName }} has more than one worksheet with data. Choose exactly one.</p>
    <div class="sheet-list">
      <button
        v-for="sheet in upload.worksheets"
        :key="sheet.name"
        :disabled="!sheet.hasDataset || busy"
        @click="chooseSheet(sheet.name)"
      >
        <strong>{{ sheet.name }}</strong>
        <span v-if="sheet.hasDataset"> — {{ sheet.rowCount }} data rows</span>
        <span v-else> — {{ sheet.error || 'No dataset' }}</span>
      </button>
    </div>
    <p v-if="message" class="error">{{ message }}</p>
    <button class="secondary" @click="step = 'upload'">Choose a different file</button>
  </section>

  <section v-else-if="step === 'configure'">
    <div class="panel">
      <div class="toolbar">
        <div>
          <h2>Map {{ upload.fileName }}</h2>
          <p class="muted">Worksheet {{ worksheet }} · {{ rowCount }} data rows · {{ assignedCount }} of {{ fields.length }} fields assigned. Hidden fields are filled automatically and are not listed.</p>
        </div>
        <div class="row">
          <button class="secondary" :disabled="busy" @click="runTest">Test</button>
          <button :disabled="busy" @click="exportMapping">Export JSON</button>
        </div>
      </div>
      <input v-model="filter" type="search" placeholder="Filter target fields" />
      <p v-if="message" class="error">{{ message }}</p>
      <div v-if="testResult" :class="testResult.ok ? 'banner ok' : 'banner bad'">
        <span v-if="testResult.ok">Test passed. {{ testResult.readyRows }} rows can be loaded. Nothing was written to SQL Server.</span>
        <span v-else>Test did not pass. {{ testResult.readyRows }} of {{ testResult.dataRows }} rows are ready.</span>
        <ul v-if="testResult.definitionErrors?.length" class="issues">
          <li v-for="issue in testResult.definitionErrors" :key="issue">{{ issue }}</li>
        </ul>
        <ul v-if="testResult.errors?.length" class="issues">
          <li v-for="(issue, index) in testResult.errors" :key="index">
            <span v-if="issue.row">Row {{ issue.row }}, </span>{{ issue.target }}: {{ issue.message }}
          </li>
        </ul>
      </div>
    </div>

    <div class="panel" style="overflow:auto; max-height: 70vh;">
      <table class="mapping-table">
        <thead>
          <tr>
            <th>Target</th>
            <th>SQL type</th>
            <th>Assignment</th>
            <th>Source or constant</th>
            <th>Sample</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="field in visibleFields" :key="field.column">
            <td>
              <span class="title" :class="{ required: field.mandatory }">
                {{ field.title }}<span v-if="field.mandatory"> *</span>
              </span>
              <span class="technical">{{ field.column }}</span>
            </td>
            <td class="sql-type">{{ field.sqlType }}</td>
            <td>
              <select v-model="assignments[field.column].mode" @change="scheduleAssess">
                <option value="unassigned">Unassigned</option>
                <option value="source">Source</option>
                <option value="constant">Constant</option>
              </select>
            </td>
            <td>
              <div class="stack">
                <template v-if="assignments[field.column].mode === 'source'">
                  <select v-model="assignments[field.column].sourceHeader" @change="scheduleAssess">
                    <option value="">Select a source column</option>
                    <option v-for="column in columns" :key="column.key" :value="column.header">
                      {{ column.letter }} — {{ column.header }}
                    </option>
                  </select>
                  <button
                    v-if="suggestions[field.column] && assignments[field.column].sourceHeader !== suggestions[field.column]"
                    class="secondary"
                    type="button"
                    @click="useSuggestion(field)"
                  >
                    Use suggested {{ suggestions[field.column] }}
                  </button>
                  <template v-if="fieldInfo(field.column).requiresDateFormat">
                    <input
                      v-model="assignments[field.column].dateFormat"
                      type="text"
                      placeholder="Source date format, e.g. dd.MM.yyyy"
                      @input="scheduleAssess"
                    />
                  </template>
                  <template v-if="fieldInfo(field.column).requiresNumberFormat">
                    <input
                      v-model="assignments[field.column].decimalSeparator"
                      type="text"
                      maxlength="1"
                      placeholder="Decimal separator"
                      @input="scheduleAssess"
                    />
                    <label>
                      <input v-model="assignments[field.column].thousandsNone" type="checkbox" @change="scheduleAssess" />
                      No thousands separator
                    </label>
                    <input
                      v-if="!assignments[field.column].thousandsNone"
                      v-model="assignments[field.column].thousandsSeparator"
                      type="text"
                      maxlength="1"
                      placeholder="Thousands separator"
                      @input="scheduleAssess"
                    />
                  </template>
                </template>
                <input
                  v-else-if="assignments[field.column].mode === 'constant'"
                  v-model="assignments[field.column].constant"
                  type="text"
                  placeholder="Value applied to every row"
                  @input="scheduleAssess"
                />
                <span v-else class="muted">Stored as NULL</span>
                <span v-if="fieldInfo(field.column).message" class="required">{{ fieldInfo(field.column).message }}</span>
              </div>
            </td>
            <td class="sample">
              <div v-if="fieldInfo(field.column).observedKinds?.length">
                Observed: {{ fieldInfo(field.column).observedKinds.join(', ') }}
              </div>
              <div v-for="sample in fieldInfo(field.column).samples || []" :key="sample.row + sample.source">
                Row {{ sample.row }}: {{ sample.source || 'blank' }}
                <span v-if="sample.result"> → {{ sample.result }}</span>
                <span v-if="sample.error" class="bad"> — {{ sample.error }}</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <section v-else class="panel">
    <h2>Mapping exported</h2>
    <p>The JSON file has been downloaded. This screen does not reopen it for editing. To change a mapping, create a new one from an Excel file.</p>
    <div class="row">
      <button @click="emit('done')">Go to execute a load</button>
      <button class="secondary" @click="step = 'upload'">Create another mapping</button>
    </div>
  </section>
</template>
