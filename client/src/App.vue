<script setup>
import { onMounted, ref } from 'vue'
import { api } from './api.js'
import CreateMapping from './CreateMapping.vue'
import ExecuteLoad from './ExecuteLoad.vue'

const page = ref('create')
const fields = ref([])
const hiddenCount = ref(59)
const health = ref(null)

onMounted(async () => {
  try {
    const catalogue = await api('/api/catalogue')
    fields.value = catalogue.fields
    hiddenCount.value = catalogue.hiddenCount ?? 59
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
  <div>
    <header class="app-header">
      <div class="brand">Annuity Import Tool</div>
      <nav aria-label="Main menu">
        <button :class="{ active: page === 'create' }" @click="page = 'create'">Create new mapping</button>
        <button :class="{ active: page === 'edit' }" @click="page = 'edit'">Edit mapping</button>
        <button :class="{ active: page === 'execute' }" @click="page = 'execute'">Execute a load</button>
      </nav>
    </header>
    <p v-if="health && health.database !== 'ok'" class="db-alert">SQL Server is not ready. You can still create and test a mapping. {{ health.message }}</p>
    <main>
      <CreateMapping v-if="page === 'create'" :fields="fields" :hidden-count="hiddenCount" @done="page = 'execute'" />
      <CreateMapping v-else-if="page === 'edit'" mode="edit" :fields="fields" :hidden-count="hiddenCount" @done="page = 'execute'" />
      <ExecuteLoad v-else :hidden-count="hiddenCount" />
    </main>
  </div>
</template>
