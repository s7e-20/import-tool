const storageKey = 'annuity-import-api-key'

export function getApiKey() {
  return sessionStorage.getItem(storageKey) || ''
}

export function setApiKey(key) {
  if (key) sessionStorage.setItem(storageKey, key)
  else sessionStorage.removeItem(storageKey)
}

export async function api(path, options = {}) {
  const headers = new Headers(options.headers || {})
  const key = getApiKey()
  if (key) headers.set('Authorization', `Bearer ${key}`)

  const response = await fetch(path, { ...options, headers })
  const text = await response.text()
  let data = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = { message: text }
    }
  }

  if (response.status === 401 && !String(path).startsWith('/api/auth/login')) {
    setApiKey('')
    window.dispatchEvent(new Event('auth-lost'))
  }

  if (!response.ok) {
    const error = new Error(data?.message || response.statusText || 'Request failed')
    error.payload = data
    error.status = response.status
    throw error
  }

  return data
}
