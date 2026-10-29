import { describe, it, expect } from 'vitest'
import csrfMiddleware from '../server/middleware/csrf'

// Helper to construct a mock H3Event compatible with H3 v2
function createMockEvent(options: {
  method: string
  path: string
  headers?: Record<string, string>
}) {
  const headers = new Headers()
  for (const [k, v] of Object.entries(options.headers || {})) {
    headers.set(k, v)
  }

  const req: any = {
    method: options.method,
    url: options.path,
    headers
  }

  return {
    req,
    path: options.path,
    context: {}
  } as any
}

describe('CSRF Middleware', () => {
  it('allows safe methods (GET, HEAD, OPTIONS) without anti-CSRF headers', () => {
    const getEvent = createMockEvent({ method: 'GET', path: '/api/v1/notes' })
    expect(() => csrfMiddleware(getEvent)).not.toThrow()

    const headEvent = createMockEvent({ method: 'HEAD', path: '/api/v1/notes' })
    expect(() => csrfMiddleware(headEvent)).not.toThrow()

    const optionsEvent = createMockEvent({ method: 'OPTIONS', path: '/api/v1/notes' })
    expect(() => csrfMiddleware(optionsEvent)).not.toThrow()
  })

  it('allows non-API requests without anti-CSRF headers', () => {
    const pageEvent = createMockEvent({
      method: 'POST',
      path: '/login',
      headers: { host: 'localhost:3000' }
    })
    expect(() => csrfMiddleware(pageEvent)).not.toThrow()
  })

  it('allows nuxt-auth-utils internal DELETE /api/_auth/session without custom headers', () => {
    const sessionEvent = createMockEvent({
      method: 'DELETE',
      path: '/api/_auth/session',
      headers: {
        host: 'localhost:3000',
        'sec-fetch-site': 'same-origin'
      }
    })
    expect(() => csrfMiddleware(sessionEvent)).not.toThrow()
  })

  it('rejects nuxt-auth-utils internal /api/_auth/session if Sec-Fetch-Site is cross-site', () => {
    const sessionEvent = createMockEvent({
      method: 'DELETE',
      path: '/api/_auth/session',
      headers: {
        host: 'localhost:3000',
        'sec-fetch-site': 'cross-site'
      }
    })
    try {
      csrfMiddleware(sessionEvent)
      expect.unreachable('Should have thrown 403')
    } catch (err: any) {
      expect(err.statusCode).toBe(403)
      expect(err.data.code).toBe('csrf.rejected')
    }
  })

  it('rejects POST requests missing X-Requested-With header', () => {
    const event = createMockEvent({
      method: 'POST',
      path: '/api/auth/login',
      headers: { host: 'localhost:3000' }
    })

    try {
      csrfMiddleware(event)
      expect.unreachable('Should have thrown 403')
    } catch (err: any) {
      expect(err.statusCode).toBe(403)
      expect(err.data.code).toBe('csrf.missing_header')
    }
  })

  it('rejects cross-site requests indicated by Sec-Fetch-Site', () => {
    const event = createMockEvent({
      method: 'POST',
      path: '/api/proxy/notes',
      headers: {
        host: 'localhost:3000',
        'x-requested-with': 'XMLHttpRequest',
        'sec-fetch-site': 'cross-site'
      }
    })

    try {
      csrfMiddleware(event)
      expect.unreachable('Should have thrown 403')
    } catch (err: any) {
      expect(err.statusCode).toBe(403)
      expect(err.data.code).toBe('csrf.rejected')
    }
  })

  it('rejects requests with mismatched Origin header', () => {
    const event = createMockEvent({
      method: 'POST',
      path: '/api/proxy/notes',
      headers: {
        host: 'localhost:3000',
        origin: 'https://evil-attacker.com',
        'x-requested-with': 'XMLHttpRequest'
      }
    })

    try {
      csrfMiddleware(event)
      expect.unreachable('Should have thrown 403')
    } catch (err: any) {
      expect(err.statusCode).toBe(403)
      expect(err.data.code).toBe('csrf.invalid_origin')
    }
  })

  it('rejects requests with mismatched Referer header when Origin is absent', () => {
    const event = createMockEvent({
      method: 'POST',
      path: '/api/proxy/notes',
      headers: {
        host: 'localhost:3000',
        referer: 'https://evil-attacker.com/csrf-page',
        'x-requested-with': 'XMLHttpRequest'
      }
    })

    try {
      csrfMiddleware(event)
      expect.unreachable('Should have thrown 403')
    } catch (err: any) {
      expect(err.statusCode).toBe(403)
      expect(err.data.code).toBe('csrf.invalid_referer')
    }
  })

  it('allows valid state-changing requests with matching Origin and X-Requested-With', () => {
    const event = createMockEvent({
      method: 'POST',
      path: '/api/proxy/notes',
      headers: {
        host: 'localhost:3000',
        origin: 'http://localhost:3000',
        'x-requested-with': 'XMLHttpRequest',
        'sec-fetch-site': 'same-origin'
      }
    })

    expect(() => csrfMiddleware(event)).not.toThrow()
  })
})
