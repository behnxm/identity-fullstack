import { defineEventHandler, getRequestHeader, getMethod, createError } from 'h3'

export default defineEventHandler((event) => {
  const method = getMethod(event)

  // Safe HTTP methods do not mutate state
  if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS') {
    return
  }

  const path = event.path

  // Only protect /api routes (BFF endpoints and proxy)
  if (!path.startsWith('/api/')) {
    return
  }

  // 1. Fetch Metadata check (Browsers supporting Sec-Fetch-Site)
  const secFetchSite = getRequestHeader(event, 'sec-fetch-site')
  if (secFetchSite && (secFetchSite === 'cross-site' || secFetchSite === 'none')) {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden',
      data: {
        code: 'csrf.rejected',
        title: 'CSRF Rejected',
        detail: 'Cross-site requests to BFF API endpoints are strictly prohibited.'
      }
    })
  }

  // 2. Custom Anti-CSRF Header requirement
  const requestedWith = getRequestHeader(event, 'x-requested-with')
  if (requestedWith !== 'XMLHttpRequest') {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden',
      data: {
        code: 'csrf.missing_header',
        title: 'Missing Required Header',
        detail: 'Requests must include X-Requested-With: XMLHttpRequest header.'
      }
    })
  }

  // 3. Origin / Referer Validation
  const host = getRequestHeader(event, 'host')
  const origin = getRequestHeader(event, 'origin')
  const referer = getRequestHeader(event, 'referer')

  if (origin) {
    try {
      const originUrl = new URL(origin)
      if (originUrl.host !== host) {
        throw createError({
          statusCode: 403,
          statusMessage: 'Forbidden',
          data: {
            code: 'csrf.origin_mismatch',
            title: 'Origin Mismatch',
            detail: 'Request origin does not match the target host.'
          }
        })
      }
    } catch {
      throw createError({
        statusCode: 403,
        statusMessage: 'Forbidden',
        data: {
          code: 'csrf.invalid_origin',
          title: 'Invalid Origin',
          detail: 'Malformed origin header.'
        }
      })
    }
  } else if (referer) {
    try {
      const refererUrl = new URL(referer)
      if (refererUrl.host !== host) {
        throw createError({
          statusCode: 403,
          statusMessage: 'Forbidden',
          data: {
            code: 'csrf.referer_mismatch',
            title: 'Referer Mismatch',
            detail: 'Request referer does not match the target host.'
          }
        })
      }
    } catch {
      throw createError({
        statusCode: 403,
        statusMessage: 'Forbidden',
        data: {
          code: 'csrf.invalid_referer',
          title: 'Invalid Referer',
          detail: 'Malformed referer header.'
        }
      })
    }
  }
})
