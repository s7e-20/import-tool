<script setup>
import { computed, ref } from 'vue'
import { api } from './api.js'

const excel = ref(null)
const mappingFile = ref(null)
const inspect = ref(null)
const worksheet = ref('')
const busy = ref(false)
const message = ref('')
const result = ref(null)

const canExecute = computed(() => {
  if (!excel.value || !mappingFile.value || busy.value || !inspect.value) return false
  if (inspect.value.message) return false
  if (inspect.value.needsWorksheet && !worksheet.value) return false
  return true
})

function onExcel(event) {
  excel.value = event.target.files?.[0] || null
  inspect.value = null
  result.value = null
  message.value = ''
  if (excel.value && mappingFile.value) review()
}

function onMapping(event) {
  mappingFile.value = event.target.files?.[0] || null
  inspect.value = null
  result.value = null
  message.value = ''
  if (excel.value && mappingFile.value) review()
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
  <section class="panel">
    <h2>Execute a load</h2>
    <p class="muted">Choose the Excel file and a mapping JSON file. The mapping is read-only. Every execution inserts the rows again. This tool does not look for duplicates.</p>
    <div class="row">
      <label class="file">Excel
        <input type="file" accept=".xlsx" @change="onExcel" />
      </label>
      <label class="file">Mapping
        <input type="file" accept=".json,application/json" @change="onMapping" />
      </label>
    </div>
    <p v-if="excel || mappingFile" class="muted">
      <span v-if="excel">{{ excel.name }}</span>
      <span v-if="excel && mappingFile"> · </span>
      <span v-if="mappingFile">{{ mappingFile.name }}</span>
    </p>

    <div v-if="inspect && inspect.needsWorksheet" class="stack" style="margin-top: 0.8rem;">
      <label>
        Worksheet
        <select v-model="worksheet">
          <option value="">Select a worksheet</option>
          <option v-for="sheet in inspect.worksheets.filter(item => item.hasDataset)" :key="sheet.name" :value="sheet.name">
            {{ sheet.name }} ({{ sheet.rowCount }} rows)
          </option>
        </select>
      </label>
    </div>
    <p v-else-if="inspect?.worksheet" class="muted">Worksheet from the mapping: {{ inspect.worksheet }}<span v-if="inspect.rowCount"> · {{ inspect.rowCount }} data rows</span></p>
    <p v-if="inspect?.message" class="error">{{ inspect.message }}</p>
    <p v-if="message" class="error">{{ message }}</p>

    <div class="row" style="margin-top: 0.9rem;">
      <button :disabled="!canExecute" @click="execute">{{ busy ? 'Executing…' : 'Execute' }}</button>
    </div>

    <div v-if="result" :class="result.ok ? 'banner ok' : 'banner bad'">
      <span v-if="result.ok">Inserted {{ result.inserted }} rows into dbo.CASE_TRANSFERT<span v-if="result.firstId"> (IdRecord {{ result.firstId }}–{{ result.lastId }})</span>.</span>
      <span v-else>The load was not inserted.</span>
      <ul v-if="result.definitionErrors?.length" class="issues">
        <li v-for="issue in result.definitionErrors" :key="issue">{{ issue }}</li>
      </ul>
      <ul v-if="result.errors?.length" class="issues">
        <li v-for="(issue, index) in result.errors" :key="index">
          <span v-if="issue.row">Row {{ issue.row }}, </span>
          <span v-if="issue.target">{{ issue.target }}: </span>{{ issue.message }}
        </li>
      </ul>
    </div>
  </section>
</template>
