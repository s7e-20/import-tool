<script setup>
import { computed, ref } from 'vue'
import { api } from './api.js'

const props = defineProps({ hiddenCount: { type: Number, default: 59 } })

const excel = ref(null)
const mappingFile = ref(null)
const mappingDoc = ref(null)
const inspect = ref(null)
const worksheet = ref('')
const busy = ref(false)
const message = ref('')
const result = ref(null)
const excelInput = ref(null)
const mappingInput = ref(null)

const summary = computed(() => {
  const assignments = mappingDoc.value?.assignments
  if (!Array.isArray(assignments)) return ''
  const source = assignments.filter(item => item.mode === 'source').length
  const constant = assignments.filter(item => item.mode === 'constant').length
  return `${source + constant} fields assigned (${source} from columns, ${constant} constants) · ${props.hiddenCount} system fields automatic`
})

const selectedSheet = computed(() => {
  const name = worksheet.value || inspect.value?.worksheet
  return (inspect.value?.worksheets || []).find(sheet => sheet.name === name) || null
})

const canExecute = computed(() => {
  if (!excel.value || !mappingFile.value || busy.value || !inspect.value) return false
  if (inspect.value.message) return false
  if (inspect.value.needsWorksheet && !worksheet.value) return false
  return true
})

const problemCount = computed(() => {
  if (!result.value || result.value.ok) return 0
  return (result.value.definitionErrors?.length || 0) + (result.value.errors?.length || 0)
})

function pickExcel() { excelInput.value?.click() }
function pickMapping() { mappingInput.value?.click() }

async function onExcel(event) {
  excel.value = event.target.files?.[0] || null
  event.target.value = ''
  inspect.value = null
  result.value = null
  message.value = ''
  if (excel.value && mappingFile.value) await review()
}

async function onMapping(event) {
  mappingFile.value = event.target.files?.[0] || null
  event.target.value = ''
  mappingDoc.value = null
  inspect.value = null
  result.value = null
  message.value = ''
  if (mappingFile.value) {
    try {
      mappingDoc.value = JSON.parse(await mappingFile.value.text())
    } catch {
      mappingDoc.value = null
    }
  }
  if (excel.value && mappingFile.value) await review()
}

function formData() {
  const body = new FormData()
  body.append('excel', excel.value)
  body.append('mapping', mappingFile.value)
  if (worksheet.value) body.append('worksheet', worksheet.value)
  return body
}

async function review() {
  busy.value = true
  message.value = ''
  result.value = null
  try {
    inspect.value = await api('/api/loads/inspect', { method: 'POST', body: formData() })
    if (inspect.value.worksheet && !inspect.value.needsWorksheet)
      worksheet.value = inspect.value.worksheet
    else
      worksheet.value = ''
  } catch (error) {
    inspect.value = null
    message.value = error.message
  } finally {
    busy.value = false
  }
}

async function execute() {
  busy.value = true
  message.value = ''
  result.value = null
  try {
    result.value = await api('/api/loads/execute', { method: 'POST', body: formData() })
  } catch (error) {
    message.value = error.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="flow">
    <div>
      <h1>Execute a load</h1>
      <p class="lede">Choose the Excel file and a saved mapping (JSON). The mapping is read-only here. To change it, use Edit mapping with the original Excel file. Every execution inserts the rows again.</p>
    </div>

    <section class="card" aria-label="Load settings">
      <div class="load-grid">
        <div>
          <span class="quiet" style="font-weight:600;color:#3d4656">Excel file</span>
          <div class="picked">
            <span class="name grow">{{ excel ? excel.name : 'Choose an .xlsx file' }}</span>
            <button type="button" class="change" @click="pickExcel">{{ excel ? 'Change' : 'Browse' }}</button>
            <input ref="excelInput" type="file" accept=".xlsx" hidden @change="onExcel" />
          </div>
        </div>
        <div>
          <span class="quiet" style="font-weight:600;color:#3d4656">Mapping (JSON)</span>
          <div class="picked">
            <span class="name grow">{{ mappingFile ? mappingFile.name : 'Choose a .json file' }}</span>
            <button type="button" class="change" @click="pickMapping">{{ mappingFile ? 'Change' : 'Browse' }}</button>
            <input ref="mappingInput" type="file" accept=".json,application/json" hidden @change="onMapping" />
          </div>
        </div>
        <label v-if="inspect">Worksheet
          <select v-model="worksheet" :disabled="!inspect.needsWorksheet">
            <option v-if="!inspect.needsWorksheet" :value="inspect.worksheet">{{ inspect.worksheet }} (from mapping)</option>
            <template v-else>
              <option value="">Select a worksheet</option>
              <option v-for="sheet in (inspect.worksheets || []).filter(item => item.hasDataset)" :key="sheet.name" :value="sheet.name">
                {{ sheet.name }} ({{ sheet.rowCount }} rows)
              </option>
            </template>
          </select>
        </label>
        <div v-if="summary" class="summary-block">
          <span>Mapping summary</span>
          <span>{{ summary }}<template v-if="worksheet"> · worksheet {{ worksheet }}</template></span>
        </div>
        <div style="grid-column: 1 / -1" class="actions">
          <button class="primary" :disabled="!canExecute" @click="execute">{{ busy ? 'Executing…' : 'Execute' }}</button>
        </div>
      </div>
    </section>

    <section v-if="inspect?.message" class="banner bad" role="alert">
      <h3>This Excel file does not match the mapping</h3>
      <p>{{ inspect.message }}</p>
      <p>Open Edit mapping with this Excel file and the JSON to change the assignments.</p>
    </section>
    <p v-if="message" class="banner bad">{{ message }}</p>

    <section v-if="result?.ok" class="card" role="status" aria-label="Result">
      <div class="result-head">
        <h2>Result</h2>
        <span class="done">Completed</span>
      </div>
      <div class="bar" role="progressbar" aria-valuenow="100" aria-valuemin="0" aria-valuemax="100" aria-label="Progress"><div></div></div>
      <div class="stats">
        <div class="stat"><div class="k">Rows read</div><div class="v">{{ result.dataRows }}</div></div>
        <div class="stat good"><div class="k">Inserted</div><div class="v">{{ result.inserted }}</div></div>
        <div class="stat"><div class="k">Empty rows skipped</div><div class="v">{{ selectedSheet?.emptyRowsSkipped ?? 0 }}</div></div>
        <div class="stat"><div class="k">Errors</div><div class="v">0</div></div>
      </div>
      <p class="fine">Saved in CASE_TRANSFERT with SOURCE_FILE = {{ excel?.name }}.<span v-if="result.firstId"> IdRecord {{ result.firstId }}–{{ result.lastId }}.</span></p>
    </section>

    <section v-else-if="result" class="card" style="border-color:#f4b4ae" role="alert" aria-label="Result">
      <div class="result-head">
        <h2>Result</h2>
        <span class="stopped">Stopped: {{ problemCount }} error{{ problemCount === 1 ? '' : 's' }}</span>
      </div>
      <div class="stats">
        <div class="stat"><div class="k">Rows read</div><div class="v">{{ result.dataRows ?? '—' }}</div></div>
        <div class="stat"><div class="k">Inserted</div><div class="v">{{ result.inserted ?? 0 }}</div></div>
        <div class="stat"><div class="k">Empty rows skipped</div><div class="v">{{ selectedSheet?.emptyRowsSkipped ?? 0 }}</div></div>
        <div class="stat bad"><div class="k">Errors</div><div class="v">{{ problemCount }}</div></div>
      </div>
      <div style="overflow-x:auto">
        <table class="errors">
          <thead>
            <tr><th>Excel row</th><th>Field</th><th>Problem</th></tr>
          </thead>
          <tbody>
            <tr v-for="issue in result.definitionErrors || []" :key="issue">
              <td>—</td><td>—</td><td>{{ issue }}</td>
            </tr>
            <tr v-for="(issue, index) in result.errors || []" :key="index">
              <td><strong>{{ issue.row || '—' }}</strong></td>
              <td>{{ issue.target || '—' }}</td>
              <td>{{ issue.message }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="fine">Nothing was inserted. Correct the file or the mapping, then run the load again.</p>
    </section>
  </div>
</template>
