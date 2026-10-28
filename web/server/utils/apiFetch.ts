import type { H3Event } from 'h3'

export async function apiFetch<T>(
  event: H3Event,
  path: string,
  options: Parameters<typeof $fetch>[1] = {}
): Promise<T> {
  const config = useRuntimeConfig(event)
  const apiBase = config.apiBase as string

  const clientIp = getRequestIP(event, { xForwardedFor: true }) || '127.0.0.1'
  const userAgent = getRequestHeader(event, 'user-agent') || 'IdentityNuxtBFF/1.0'

  const headers = new Headers(options.headers || {})
  headers.set('X-Forwarded-For', clientIp)
  headers.set('User-Agent', userAgent)

  try {
    return (await ($fetch as any)(`${apiBase}${path}`, {
      ...options,
      headers
    })) as T
  } catch (error: any) {
    const status = error.statusCode || error.status || 500
    const data = error.data || {
      title: error.message || 'API Communication Error',
      status,
      detail: error.data?.detail || error.message
    }

    throw createError({
      statusCode: status,
      statusMessage: error.statusMessage || 'API Error',
      data
    })
  }
}
