async function parseResponse(response) {
  const contentType = response.headers.get('content-type') || ''
  if (contentType.includes('application/json')) {
    return response.json()
  }
  return response.text()
}

function buildError(status, data) {
  const message = typeof data === 'string' ? data : JSON.stringify(data)
  const error = new Error(`Request failed (${status}): ${message}`)
  error.status = status
  error.data = data
  return error
}

export async function apiGet(path, tenantId) {
  const response = await fetch(path, {
    headers: {
      'X-Tenant-ID': tenantId
    }
  })

  if (!response.ok) {
    const data = await parseResponse(response)
    throw buildError(response.status, data)
  }

  return parseResponse(response)
}

export async function apiPostForm(path, tenantId, formData) {
  const response = await fetch(path, {
    method: 'POST',
    headers: {
      'X-Tenant-ID': tenantId
    },
    body: formData
  })

  if (!response.ok) {
    const data = await parseResponse(response)
    throw buildError(response.status, data)
  }

  return parseResponse(response)
}

export async function apiPutJson(path, tenantId, body) {
  const response = await fetch(path, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-ID': tenantId
    },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const data = await parseResponse(response)
    throw buildError(response.status, data)
  }

  return parseResponse(response)
}

export async function apiPostJson(path, tenantId, body) {
  const response = await fetch(path, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-ID': tenantId
    },
    body: body ? JSON.stringify(body) : undefined
  })

  if (!response.ok) {
    const data = await parseResponse(response)
    throw buildError(response.status, data)
  }

  return parseResponse(response)
}
