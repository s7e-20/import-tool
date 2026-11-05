<script setup>
import { onMounted, ref } from 'vue'
import { api } from './api.js'
import CreateMapping from './CreateMapping.vue'
import ExecuteLoad from './ExecuteLoad.vue'

const page = ref('create')
const fields = ref([])
const health = ref(null)

onMounted(async () => {
  try {
    const catalogue = await api('/api/catalogue')
    fields.value = catalogue.fields
  } catch (error) {
    health.value = { database: 'unavailable', message: error.message }
  }

  try {
    health.value = await api('/api/health')
  } catch (error) {
    health.value = { database: 'unavailable', message: error.message }
  }
})
</script>

<template>
  <header class="app-header">
    <div>
      <h1>Annuity import</h1>
      <p>Excel worksheet to dbo.CASE_TRANSFERT</p>
    </div>
    <nav>
      <button :class="{ active: page === 'create' }" @click="page = 'create'">Create new mapping</button>
      <button :class="{ active: page === 'execute' }" @click="page = 'execute'">Execute a load</button>
    </nav>
  </header>
  <div class="status-line" :class="{ bad: health && health.database !== 'ok' }">
    <span v-if="!health">Checking SQL Server…</span>
    <span v-else-if="health.database === 'ok'">Connected to SQL Server. Target table {{ health.table }}.</span>
    <span v-else>SQL Server is not ready. You can still create and test a mapping. {{ health.message }}</span>
  </div>
  <main>
    <CreateMapping v-if="page === 'create'" :fields="fields" @done="page = 'execute'" />
    <ExecuteLoad v-else />
  </main>
</template>
