<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { api, getApiKey, setApiKey } from './api.js'
import CreateMapping from './CreateMapping.vue'
import ExecuteLoad from './ExecuteLoad.vue'
import Login from './Login.vue'

const authed = ref(false)
const checking = ref(true)
const page = ref('create')
const fields = ref([])
const hiddenCount = ref(59)
const health = ref(null)

onMounted(async () => {
  window.addEventListener('auth-lost', onAuthLost)
  if (!getApiKey()) {
    checking.value = false
    return
  }

  try {
    await api('/api/auth/session')
    authed.value = true
    await loadApp()
  } catch {
    authed.value = false
  } finally {
    checking.value = false
  }
})

onBeforeUnmount(() => window.removeEventListener('auth-lost', onAuthLost))

function onAuthLost() {
  authed.value = false
  checking.value = false
}

async function onLoggedIn() {
  authed.value = true
  page.value = 'create'
  await loadApp()
}

async function loadApp() {
  try {
    const catalogue = await api('/api/catalogue')
    fields.value = catalogue.fields
    hiddenCount.value = catalogue.hiddenCount ?? 59
  } catch (error) {
    if (!authed.value) return
    health.value = { database: 'unavailable', message: error.message }
  }

  try {
    health.value = await api('/api/health')
  } catch (error) {
    if (!authed.value) return
    health.value = { database: 'unavailable', message: error.message }
  }
}

async function signOut() {
  const key = getApiKey()
  setApiKey('')
  authed.value = false
  fields.value = []
  health.value = null
  if (!key) return
  await fetch('/api/auth/logout', {
    method: 'POST',
    headers: { Authorization: `Bearer ${key}` }
  }).catch(() => {})
}
</script>

<template>
  <p v-if="checking" class="session-check">Checking session…</p>
  <Login v-else-if="!authed" @logged-in="onLoggedIn" />
  <div v-else>
    <header class="app-header">
      <div class="brand">Annuity Import Tool</div>
      <nav aria-label="Main menu">
        <button :class="{ active: page === 'create' }" @click="page = 'create'">Create new mapping</button>
        <button :class="{ active: page === 'edit' }" @click="page = 'edit'">Edit mapping</button>
        <button :class="{ active: page === 'execute' }" @click="page = 'execute'">Execute a load</button>
      </nav>
      <button type="button" class="sign-out" @click="signOut">Sign out</button>
    </header>
    <p v-if="health && health.database !== 'ok'" class="db-alert">SQL Server is not ready. You can still create and test a mapping. {{ health.message }}</p>
    <main>
      <CreateMapping v-if="page === 'create'" :fields="fields" :hidden-count="hiddenCount" @done="page = 'execute'" />
      <CreateMapping v-else-if="page === 'edit'" mode="edit" :fields="fields" :hidden-count="hiddenCount" @done="page = 'execute'" />
      <ExecuteLoad v-else :hidden-count="hiddenCount" />
    </main>
  </div>
</template>
