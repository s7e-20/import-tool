export async function api(path, options = {}) {
  const response = await fetch(path, options)
  const text = await response.text()
  let data = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = { message: text }
    }
  }

  if (!response.ok) {
    const error = new Error(data?.message || response.statusText || 'Request failed')
    error.payload = data
    throw error
  }

  return data
}
