export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly details?: unknown,
  ) {
    super(message)
  }
}

let csrfToken: string | null = null

export function resetCsrfToken() {
  csrfToken = null
}

async function ensureCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken

  const response = await fetch('/api/auth/csrf', { credentials: 'include' })
  if (!response.ok) throw new ApiError('Không thể khởi tạo phiên bảo mật.', response.status)
  const data = (await response.json()) as { token: string }
  csrfToken = data.token
  return data.token
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')

  if (init.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-CSRF-TOKEN', await ensureCsrfToken())
  }

  const response = await fetch(path, {
    ...init,
    method,
    headers,
    credentials: 'include',
  })

  if (response.status === 204) return undefined as T

  const contentType = response.headers.get('content-type') ?? ''
  const data: unknown = contentType.includes('json')
    ? await response.json()
    : await response.text()

  if (!response.ok) {
    const message = getApiErrorMessage(data, response.status)
    throw new ApiError(message, response.status, data)
  }

  return data as T
}

function getApiErrorMessage(data: unknown, status: number) {
  if (typeof data === 'object' && data !== null) {
    if ('message' in data && String((data as { message: unknown }).message).trim()) {
      return String((data as { message: unknown }).message)
    }
    if ('detail' in data && String((data as { detail: unknown }).detail).trim()) {
      return String((data as { detail: unknown }).detail)
    }
    if ('errors' in data) {
      const errors = (data as { errors: unknown }).errors
      if (typeof errors === 'object' && errors !== null) {
        const messages = Object.values(errors).flatMap((value) => Array.isArray(value) ? value.map(String) : [String(value)])
        if (messages.length > 0) return messages.join(' ')
      }
    }
    if ('title' in data && String((data as { title: unknown }).title).trim()) {
      return String((data as { title: unknown }).title)
    }
  }
  if (typeof data === 'string' && data.trim()) return data.trim()
  return `Yêu cầu không thành công (HTTP ${status}).`
}

export const api = {
  get: <T>(path: string) => apiFetch<T>(path),
  post: <T>(path: string, body?: unknown) =>
    apiFetch<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    apiFetch<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => apiFetch<T>(path, { method: 'DELETE' }),
}

