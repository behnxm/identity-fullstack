import { defineEventHandler, getMethod, readRawBody, getRequestHeaders, setResponseStatus } from 'h3'

const ALLOWED_PREFIXES = ['me', 'notes', 'admin']
const MAX_BODY_BYTES = 100 * 1024 // 100 KB

export default defineEventHandler(async (event) => {
  const rawPath = event.context.params?.path || ''
  const trimmedPath = rawPath.replace(/^\/+/, '')

  // Allowlist validation
  const firstSegment = trimmedPath.split('/')[0] || ''
  if (!ALLOWED_PREFIXES.includes(firstSegment)) {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden',
      data: {
        code: 'proxy.forbidden_route',
        title: 'Route Forbidden',
        detail: `Proxy access to route prefix '${firstSegment}' is not permitted.`
      }
    })
  }

  const method = getMethod(event)
  const query = getQuery(event)
  const config = useRuntimeConfig(event)
  const apiBase = config.apiBase as string

  // Body handling
  let body: any = undefined
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
    const rawBody = await readRawBody(event)
    if (rawBody) {
      if (rawBody.length > MAX_BODY_BYTES) {
        throw createError({
          statusCode: 413,
          statusMessage: 'Payload Too Large',
          data: {
            code: 'proxy.payload_too_large',
            title: 'Payload Too Large',
            detail: `Request body exceeds maximum size of ${MAX_BODY_BYTES} bytes.`
          }
        })
      }
      const contentType = getRequestHeader(event, 'content-type')
      if (contentType && !contentType.includes('application/json')) {
        throw createError({
          statusCode: 415,
          statusMessage: 'Unsupported Media Type',
          data: {
            code: 'proxy.unsupported_media_type',
            title: 'Unsupported Media Type',
            detail: 'Proxy only accepts application/json payloads.'
          }
        })
      }
      body = typeof rawBody === 'string' ? rawBody : (rawBody ? (rawBody as any).toString() : undefined)
    }
  }

  // Obtain valid access token through single-flight refresh lock
  let token = await getValidAccessToken(event)

  const clientIp = getRequestIP(event, { xForwardedFor: true }) || '127.0.0.1'
  const userAgent = getRequestHeader(event, 'user-agent') || 'IdentityNuxtBFF/1.0'

  const targetUrl = `${apiBase}/api/v1/${trimmedPath}`

  const executeRequest = async (accessToken: string) => {
    return await $fetch.raw(targetUrl, {
      method: method as any,
      query,
      body,
      headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
        'X-Forwarded-For': clientIp,
        'User-Agent': userAgent
      },
      ignoreResponseError: true
    })
  }

  let response = await executeRequest(token)

  // Single retry on 401 token expired
  const errCode = (response._data as any)?.extensions?.code
  if (response.status === 401 && errCode === 'auth.token_expired') {
    // Proactively refresh and retry
    const session = (await getUserSession(event)) as any
    if (session?.tokens?.sessionId) {
      // Force token refresh by setting expiration to 0
      session.tokens.expiresAt = 0
      await setUserSession(event, session)
      token = await getValidAccessToken(event)
      response = await executeRequest(token)
    }
  }

  setResponseStatus(event, response.status)

  // Pass through response body
  return response._data
})
