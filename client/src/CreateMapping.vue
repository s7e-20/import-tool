<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { api } from './api.js'

const props = defineProps({
  fields: { type: Array, default: () => [] },
  hiddenCount: { type: Number, default: 59 }
})
const emit = defineEmits(['done'])

const step = ref('file')
const busy = ref(false)
const message = ref('')
const upload = ref(null)
const chosenSheet = ref('')
const worksheet = ref('')
const columns = ref([])
const rowCount = ref(0)
const firstRow = ref(null)
const lastRow = ref(null)
const emptyRowsSkipped = ref(0)
const suggestions = ref({})
const assess = ref({})
const testResult = ref(null)
const exportedName = ref('')
const dragOver = ref(false)
const sourceQuery = ref('')
const fieldFilter = ref('all')
const assignments = reactive({})
let assessTimer = 0

const datasetSheets = computed(() => (upload.value?.worksheets || []).filter(sheet => sheet.hasDataset))
const selectedSheet = computed(() => (upload.value?.worksheets || []).find(sheet => sheet.name === chosenSheet.value) || null)
const assignedCount = computed(() =>
  props.fields.filter(field => assignments[field.column]?.mode && assignments[field.column].mode !== 'unassigned').length)
const attentionCount = computed(() => props.fields.filter(field => fieldStatus(field).level === 'err').length)
const canContinue = computed(() => !!selectedSheet.value?.hasDataset && !busy.value)
const canExport = computed(() => attentionCount.value === 0 && !busy.value)

const visibleFields = computed(() => props.fields.filter(field => {
  if (fieldFilter.value === 'req') return field.mandatory
  if (fieldFilter.value === 'asg') return assignments[field.column]?.mode !== 'unassigned'
  if (fieldFilter.value === 'iss') return fieldStatus(field).level === 'err'
  return true
}))

const usedSources = computed(() => {
  const used = {}
  for (const field of props.fields) {
    const entry = assignments[field.column]
    if (entry?.mode === 'source' && entry.sourceHeader)
      used[entry.sourceHeader] = (used[entry.sourceHeader] || 0) + 1
  }
  return used
})

const visibleColumns = computed(() => {
  const query = sourceQuery.value.trim().toLowerCase()
  if (!query) return columns.value
  return columns.value.filter(column => column.header.toLowerCase().includes(query))
})

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
        thousandsChoice: ''
      }
    }
  }
}

function resetAssignments() {
  for (const key of Object.keys(assignments)) delete assignments[key]
  ensureAssignments()
  assess.value = {}
  testResult.value = null
  exportedName.value = ''
}

function mappingBody() {
  return {
    format: 'annuity-import-mapping',
    version: 1,
    worksheet: worksheet.value,
    sourceHeaders: columns.value.map(column => column.header),
    assignments: props.fields.map(field => {
      const entry = assignments[field.column]
      const thousands = thousandsValue(entry)
      return {
        target: field.column,
        mode: entry.mode,
        sourceHeader: entry.mode === 'source' ? entry.sourceHeader || null : null,
        constant: entry.mode === 'constant' ? entry.constant : null,
        dateFormat: entry.mode === 'source' && entry.dateFormat ? entry.dateFormat : null,
        decimalSeparator: entry.mode === 'source' && entry.decimalSeparator ? entry.decimalSeparator : null,
        thousandsSeparator: entry.mode === 'source' ? thousands.separator : null,
        thousandsSeparatorNone: entry.mode === 'source' ? thousands.none : false
      }
    })
  }
}

function thousandsValue(entry) {
  if (entry.thousandsChoice === 'none') return { none: true, separator: null }
  if (entry.thousandsChoice === 'space') return { none: false, separator: ' ' }
  if (entry.thousandsChoice) return { none: false, separator: entry.thousandsChoice }
  return { none: false, separator: null }
}

function scheduleAssess() {
  testResult.value = null
  exportedName.value = ''
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

function onDrop(event) {
  dragOver.value = false
  const file = event.dataTransfer?.files?.[0]
  if (file) receiveFile(file)
}

function onExcel(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (file) receiveFile(file)
}

async function receiveFile(file) {
  if (!file.name.toLowerCase().endsWith('.xlsx')) {
    message.value = 'Only .xlsx files are supported.'
    return
  }

  busy.value = true
  message.value = ''
  resetAssignments()
  const body = new FormData()
  body.append('file', file)
  try {
    const data = await api('/api/uploads', { method: 'POST', body })
    upload.value = data
    const usable = (data.worksheets || []).filter(sheet => sheet.hasDataset)
    chosenSheet.value = usable.length === 1 ? usable[0].name : ''
    step.value = 'file'
  } catch (error) {
    message.value = error.message
    upload.value = null
  } finally {
    busy.value = false
  }
}

function removeFile() {
  upload.value = null
  chosenSheet.value = ''
  worksheet.value = ''
  columns.value = []
  message.value = ''
  resetAssignments()
  step.value = 'file'
}

async function continueToMapping() {
  if (!selectedSheet.value?.hasDataset) return
  busy.value = true
  message.value = ''
  const changingSheet = worksheet.value && worksheet.value !== chosenSheet.value
  try {
    await api(`/api/uploads/${upload.value.uploadId}/worksheet`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ worksheet: chosenSheet.value })
    })
    if (changingSheet) resetAssignments()
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
  firstRow.value = data.firstRow
  lastRow.value = data.lastRow
  emptyRowsSkipped.value = data.emptyRowsSkipped
  suggestions.value = data.suggestions || {}
  worksheet.value = data.worksheet
  ensureAssignments()
  step.value = 'configure'
  await runAssess()
}

function setMode(column, mode) {
  assignments[column].mode = mode
  scheduleAssess()
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

  const fileStem = (upload.value.fileName || 'mapping').replace(/\.xlsx$/i, '')
  const name = `mapping_${fileStem}_${worksheet.value}.json`
  const blob = new Blob([JSON.stringify(mappingBody(), null, 2)], { type: 'application/json' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = name
  link.click()
  URL.revokeObjectURL(link.href)
  exportedName.value = name
}

function info(column) {
  return assess.value[column] || {}
}

function fieldStatus(field) {
  const entry = assignments[field.column]
  const detail = info(field.column)
  if (!entry || entry.mode === 'unassigned') {
    return field.mandatory
      ? { level: 'err', label: 'Required', note: 'Mandatory: choose a source column or enter a constant' }
      : { level: 'info', label: 'Left empty', note: 'Saved as NULL' }
  }
  if (entry.mode === 'constant') {
    return { level: 'info', label: 'Constant', note: 'Same value on every row. Checked when you test.' }
  }
  if (!entry.sourceHeader) {
    return { level: 'err', label: 'No column', note: 'Choose a source column' }
  }
  if (detail.status === 'incompatible') {
    return { level: 'err', label: 'Incompatible', note: detail.message || 'Choose another source column.' }
  }
  if (detail.status === 'invalid' || detail.status === 'needs-date-format') {
    return { level: 'err', label: detail.requiresDateFormat ? 'Format needed' : 'Error', note: detail.message || 'This assignment needs a correction.' }
  }
  if (detail.status === 'needs-number-format' || (detail.requiresNumberFormat && !entry.decimalSeparator)) {
    return { level: 'err', label: 'Separators needed', note: detail.message || 'Choose decimal and thousands separators.' }
  }
  if (detail.requiresDateFormat && !entry.dateFormat) {
    return { level: 'err', label: 'Format needed', note: 'Text values: enter the source date format.' }
  }
  const sampleError = (detail.samples || []).find(sample => sample.error)
  if (sampleError) return { level: 'err', label: 'Error', note: sampleError.error }
  if (detail.requiresDateFormat || detail.requiresNumberFormat) {
    return { level: 'ok', label: 'Will be parsed', note: 'Using the settings beside this field.' }
  }
  if (field.kind === 'boolean') {
    return { level: 'ok', label: 'Auto boolean', note: 'Accepts 0/1/-1, Yes/No, Y/N, True/False, X, and blank.' }
  }
  if (field.kind === 'text' && (detail.observedKinds || []).includes('Number')) {
    return { level: 'ok', label: 'Stored as text', note: 'The number is saved as a plain string.' }
  }
  return { level: 'ok', label: 'No conversion', note: 'Value copied as is.' }
}

function samplePair(field) {
  const entry = assignments[field.column]
  const detail = info(field.column)
  if (!entry || entry.mode === 'unassigned') return { source: '—', result: 'NULL' }
  if (entry.mode === 'constant') return { source: 'constant', result: entry.constant ? `"${entry.constant}"` : '""' }
  const sample = (detail.samples || [])[0]
  if (!sample) {
    const column = columns.value.find(item => item.header === entry.sourceHeader)
    const preview = column?.samples?.[0]?.display
    return { source: preview || '—', result: '—' }
  }
  return { source: sample.source || '(blank)', result: sample.result || '—' }
}

function typeLabel(kinds) {
  const names = { Text: 'text', Number: 'number', DateTime: 'date', Boolean: 'boolean' }
  const present = (kinds || []).filter(kind => names[kind])
  if (!present.length) return 'Empty column'
  const labels = present.map(kind => names[kind])
  if (labels.length === 1) return labels[0][0].toUpperCase() + labels[0].slice(1)
  return `Mixed: ${labels.join(' + ')}`
}

function columnSample(column) {
  return column.samples?.[0]?.display || '(no values)'
}

function sheetDetail(sheet) {
  if (!sheet.hasDataset) return sheet.error || 'No dataset'
  const rows = `${sheet.rowCount} data row${sheet.rowCount === 1 ? '' : 's'}`
  const range = sheet.firstRow && sheet.lastRow ? ` (Excel rows ${sheet.firstRow}–${sheet.lastRow})` : ''
  return `${sheet.columnCount} columns · ${rows}${range}`
}

function rowRange() {
  if (firstRow.value && lastRow.value) return `(Excel rows ${firstRow.value}–${lastRow.value})`
  return ''
}
</script>

<template>
  <div v-if="step === 'file'" class="flow">
    <ol class="steps" aria-label="Steps">
      <li class="current" aria-current="step"><span class="bubble">1</span>Choose file &amp; worksheet</li>
      <li class="line" aria-hidden="true"></li>
      <li><span class="bubble">2</span>Configure mapping &amp; test</li>
      <li class="line" aria-hidden="true"></li>
      <li><span class="bubble">3</span>Export JSON</li>
    </ol>

    <div>
      <h1>Create new mapping</h1>
      <p class="lede">Upload an Excel file (.xlsx). Row 1 must contain the column headers; data starts on row 2.</p>
    </div>

    <section class="card" aria-label="Excel file">
      <h2>1. Excel file</h2>
      <label class="drop" :class="{ over: dragOver }" @dragover.prevent="dragOver = true" @dragleave.prevent="dragOver = false" @drop.prevent="onDrop">
        <svg aria-hidden="true" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#1F5BD8" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 16V4"></path><path d="M7 9l5-5 5 5"></path><path d="M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3"></path></svg>
        <strong>Drop an .xlsx file here or <span class="browse">browse</span></strong>
        <span>Only .xlsx for now</span>
        <input type="file" accept=".xlsx" :disabled="busy" @change="onExcel" />
      </label>
      <div v-if="upload" class="file-row">
        <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#1A6B3C" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"></path><path d="M14 3v5h5"></path><path d="M9 13l2 2 4-4"></path></svg>
        <div class="grow">
          <div class="name">{{ upload.fileName }}</div>
          <div class="meta">{{ upload.worksheets.length === 1 ? '1 worksheet found' : `${upload.worksheets.length} worksheets found` }}</div>
        </div>
        <button type="button" class="ghost" @click="removeFile">Remove</button>
      </div>
      <p v-if="message" class="banner bad">{{ message }}</p>
    </section>

    <section v-if="upload" class="card" aria-label="Worksheet">
      <h2>2. Worksheet</h2>
      <p v-if="datasetSheets.length === 1" class="note">Only one worksheet contains data, so it was selected automatically. With several sheets, choose exactly one here.</p>
      <p v-else-if="datasetSheets.length > 1" class="note">Several worksheets contain data. Choose exactly one.</p>
      <p v-else class="banner bad">None of the worksheets contain a dataset. {{ upload.worksheets.map(sheet => `${sheet.name}: ${sheet.error}`).join(' ') }}</p>
      <div style="display:flex;flex-direction:column;gap:8px">
        <label v-for="sheet in upload.worksheets" :key="sheet.name" class="sheet-option" :class="{ selected: chosenSheet === sheet.name, disabled: !sheet.hasDataset }">
          <input type="radio" name="worksheet" :value="sheet.name" :checked="chosenSheet === sheet.name" :disabled="!sheet.hasDataset" @change="chosenSheet = sheet.name" />
          <span class="title">{{ sheet.name }}</span>
          <span class="detail">{{ sheetDetail(sheet) }}</span>
        </label>
      </div>
    </section>

    <section v-if="selectedSheet?.hasDataset" class="card" aria-label="File check">
      <h2>3. File check</h2>
      <ul class="checks">
        <li><span class="tick" aria-hidden="true">✓</span>Header row found on row 1 ({{ selectedSheet.columnCount }} headers)</li>
        <li><span class="tick" aria-hidden="true">✓</span>All headers filled in and unique (case-insensitive)</li>
        <li><span class="tick" aria-hidden="true">✓</span>No values in columns without a header</li>
        <li><span class="tick" aria-hidden="true">✓</span>No merged cells in the data</li>
        <li><span class="tick" aria-hidden="true">✓</span>{{ selectedSheet.rowCount }} data rows · {{ selectedSheet.emptyRowsSkipped }} empty rows skipped</li>
      </ul>
      <p class="fine">If a check fails, the message names the Excel row or column and that worksheet cannot be selected.</p>
    </section>

    <div v-if="upload" class="actions">
      <button class="primary" :disabled="!canContinue" @click="continueToMapping">Continue to mapping</button>
    </div>
  </div>

  <div v-else style="display:flex;flex-direction:column;gap:20px">
    <ol class="steps" aria-label="Steps">
      <li class="done"><span class="bubble">✓</span>Choose file &amp; worksheet</li>
      <li class="line" aria-hidden="true"></li>
      <li class="current" aria-current="step"><span class="bubble">2</span>Configure mapping &amp; test</li>
      <li class="line" aria-hidden="true"></li>
      <li :class="{ done: exportedName }"><span class="bubble">{{ exportedName ? '✓' : '3' }}</span>Export JSON</li>
    </ol>

    <section class="summary" aria-label="Selected file">
      <div><span class="label">File</span> <strong>{{ upload.fileName }}</strong></div>
      <div><span class="label">Worksheet</span> <strong>{{ worksheet }}</strong></div>
      <div><span class="label">Source columns</span> <strong>{{ columns.length }}</strong></div>
      <div><span class="label">Data rows</span> <strong>{{ rowCount }}</strong> <span class="label">{{ rowRange() }}</span></div>
      <a href="#" @click.prevent="step = 'file'">Change file</a>
    </section>

    <div class="workspace">
      <aside class="source-pane" aria-label="Source columns">
        <div class="source-head">
          <h2>Source columns (Excel)</h2>
          <p>Row 1 headers. One column can feed several fields.</p>
          <label for="srcsearch">Search</label>
          <input id="srcsearch" v-model="sourceQuery" type="search" placeholder="e.g. Date" />
        </div>
        <ul class="source-list">
          <li v-for="column in visibleColumns" :key="column.key">
            <div class="top">
              <span class="col">{{ column.header }}</span>
              <span v-if="usedSources[column.header]" class="used">used {{ usedSources[column.header] }}×</span>
            </div>
            <div class="sub">
              <span class="kind">{{ typeLabel(column.kinds) }}</span>
              <span class="mono">{{ columnSample(column) }}</span>
            </div>
          </li>
        </ul>
      </aside>

      <div class="editor">
        <div class="toolbar-card">
          <div class="tabs" role="tablist" aria-label="Filter target fields">
            <button type="button" class="tab" :class="{ on: fieldFilter === 'all' }" @click="fieldFilter = 'all'">All fields · {{ fields.length }}</button>
            <button type="button" class="tab" :class="{ on: fieldFilter === 'req' }" @click="fieldFilter = 'req'">Mandatory · {{ fields.filter(field => field.mandatory).length }}</button>
            <button type="button" class="tab" :class="{ on: fieldFilter === 'asg' }" @click="fieldFilter = 'asg'">Assigned · {{ assignedCount }}</button>
            <button type="button" class="tab" :class="{ on: fieldFilter === 'iss' }" @click="fieldFilter = 'iss'">Needs attention · {{ attentionCount }}</button>
          </div>
          <div class="buttons">
            <button type="button" class="outline" :disabled="busy" @click="runTest">Test mapping</button>
            <button type="button" class="export" :disabled="!canExport" @click="exportMapping">Export JSON</button>
          </div>
        </div>

        <p v-if="message" class="banner bad">{{ message }}</p>
        <section v-if="testResult && !testResult.ok" class="banner bad" role="alert">
          <h3>Test stopped: {{ (testResult.definitionErrors?.length || 0) + (testResult.errors?.length || 0) }} item(s) must be fixed first</h3>
          <ul>
            <li v-for="issue in testResult.definitionErrors || []" :key="issue">{{ issue }}</li>
            <li v-for="(issue, index) in testResult.errors || []" :key="index">
              <span v-if="issue.row">Excel row {{ issue.row }}, </span>{{ issue.target }}: {{ issue.message }}
            </li>
          </ul>
        </section>
        <section v-else-if="testResult?.ok" class="banner ok" role="status">
          <h3>Test passed: {{ testResult.readyRows }} rows<span v-if="firstRow && lastRow"> (Excel rows {{ firstRow }}–{{ lastRow }})</span> converted into CASE_TRANSFERT records</h3>
          <p>Records were built in memory only. <strong>Nothing was saved to the database.</strong> The {{ hiddenCount }} system fields were filled automatically (Record Direction = I, Source = Import tool, SOURCE_FILE = {{ upload.fileName }}, DateRecord = import date).</p>
        </section>
        <section v-if="exportedName" class="banner info" role="status">
          <strong>{{ exportedName }} downloaded.</strong>
          This mapping is now final. Use it in <a href="#" @click.prevent="emit('done')">Execute a load</a>. To change it later, start again with an Excel file.
        </section>

        <section class="card" style="padding:0;gap:0" aria-label="Target fields">
          <div class="legend">
            <span><span class="star">*</span> = mandatory</span>
            <span>Fields not listed here ({{ hiddenCount }} system fields) are filled automatically and cannot be changed.</span>
          </div>
          <div style="overflow-x:auto">
            <div style="min-width:960px">
              <div class="grid-head">
                <div>Target field (CASE_TRANSFERT)</div>
                <div>Assignment</div>
                <div>Source column / constant</div>
                <div>Sample: Excel → result</div>
                <div>Status</div>
              </div>
              <p v-if="!visibleFields.length" class="empty-filter">No fields in this filter.</p>
              <div v-for="field in visibleFields" :key="field.column" class="map-row" :class="{ err: fieldStatus(field).level === 'err' }">
                <div>
                  <div class="field-name">{{ field.title }}<span v-if="field.mandatory" class="star"> *</span></div>
                  <div class="field-type mono">{{ field.sqlType }} · {{ field.mandatory ? 'mandatory' : 'optional' }}</div>
                </div>
                <div class="modes" role="group" :aria-label="`Assignment for ${field.title}`">
                  <button type="button" :class="{ on: assignments[field.column].mode === 'source' }" @click="setMode(field.column, 'source')">Source</button>
                  <button type="button" :class="{ on: assignments[field.column].mode === 'constant' }" @click="setMode(field.column, 'constant')">Constant</button>
                  <button type="button" :class="{ on: assignments[field.column].mode === 'unassigned' }" @click="setMode(field.column, 'unassigned')">Unassigned</button>
                </div>
                <div class="stack">
                  <template v-if="assignments[field.column].mode === 'source'">
                    <select v-model="assignments[field.column].sourceHeader" :aria-label="`Source column for ${field.title}`" @change="scheduleAssess">
                      <option value="">Choose a column…</option>
                      <option v-for="column in columns" :key="column.key" :value="column.header">{{ column.header }}</option>
                    </select>
                    <span v-if="assignments[field.column].sourceHeader" class="quiet">Excel type: {{ typeLabel(columns.find(column => column.header === assignments[field.column].sourceHeader)?.kinds) }}</span>
                    <button v-if="suggestions[field.column] && assignments[field.column].sourceHeader !== suggestions[field.column]" type="button" class="suggest" @click="useSuggestion(field)">Use suggested {{ suggestions[field.column] }}</button>
                    <label v-if="info(field.column).requiresDateFormat" class="format-box">
                      Source date format
                      <input v-model="assignments[field.column].dateFormat" class="mono" placeholder="dd.MM.yyyy" @input="scheduleAssess" />
                      <span class="quiet">e.g. dd.MM.yyyy · yyyy-MM-dd · dd/MM/yyyy</span>
                    </label>
                    <div v-if="info(field.column).requiresNumberFormat" class="format-box split">
                      <label>Decimal
                        <select v-model="assignments[field.column].decimalSeparator" @change="scheduleAssess">
                          <option value="">Choose…</option>
                          <option value=".">. (dot)</option>
                          <option value=",">, (comma)</option>
                        </select>
                      </label>
                      <label>Thousands
                        <select v-model="assignments[field.column].thousandsChoice" @change="scheduleAssess">
                          <option value="">Choose…</option>
                          <option value="none">None</option>
                          <option value=".">. (dot)</option>
                          <option value=",">, (comma)</option>
                          <option value="'">' (apostrophe)</option>
                          <option value="space">space</option>
                        </select>
                      </label>
                    </div>
                  </template>
                  <template v-else-if="assignments[field.column].mode === 'constant'">
                    <input v-model="assignments[field.column].constant" class="const-input mono" :aria-label="`Constant for ${field.title}`" placeholder="Type a value" @input="scheduleAssess" />
                    <span class="quiet">Same value on every row. Saved exactly as typed.</span>
                  </template>
                  <span v-else class="quiet">Not imported</span>
                </div>
                <div class="sample-cell mono">
                  <span class="dim">{{ samplePair(field).source }}</span>
                  <span class="arrow"> → </span>
                  <strong>{{ samplePair(field).result }}</strong>
                </div>
                <div class="stack">
                  <span class="badge" :class="fieldStatus(field).level">{{ fieldStatus(field).label }}</span>
                  <span class="quiet">{{ fieldStatus(field).note }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>
