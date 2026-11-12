<script setup>
import { ref } from 'vue'
import { api, setApiKey } from './api.js'

const emit = defineEmits(['logged-in'])

const login = ref('')
const password = ref('')
const busy = ref(false)
const message = ref('')

async function submit() {
  busy.value = true
  message.value = ''
  try {
    const result = await api('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login: login.value, password: password.value })
    })
    setApiKey(result.apiKey)
    emit('logged-in')
  } catch (error) {
    message.value = error.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="login-screen">
    <form class="card login-card" @submit.prevent="submit">
      <div class="brand-mark">Annuity Import Tool</div>
      <h1>Sign in</h1>
      <p class="lede">Use your user login. The session ends when this browser window is closed.</p>
      <label for="login">Login
        <input id="login" v-model="login" name="username" autocomplete="username" :disabled="busy" />
      </label>
      <label for="password">Password
        <input id="password" v-model="password" name="password" type="password" autocomplete="current-password" :disabled="busy" />
      </label>
      <p v-if="message" class="banner bad">{{ message }}</p>
      <button class="primary" type="submit" :disabled="busy || !login.trim() || !password">{{ busy ? 'Signing in…' : 'Sign in' }}</button>
    </form>
  </div>
</template>
