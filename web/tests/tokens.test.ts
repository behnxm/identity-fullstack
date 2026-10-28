import { describe, it, expect, vi } from 'vitest'

describe('BFF Token Lifecycle & Single-Flight Mutex', () => {
  it('returns current token if more than 60 seconds remain before expiration', async () => {
    const now = Math.floor(Date.now() / 1000)
    const token = 'valid-unexpired-token'
    const expiresAt = now + 120 // 120 seconds left

    let refreshCalled = false
    const refreshFn = async () => {
      refreshCalled = true
      return 'new-token'
    }

    const getToken = async () => {
      if (expiresAt - now > 60) {
        return token
      }
      return await refreshFn()
    }

    const result = await getToken()
    expect(result).toBe('valid-unexpired-token')
    expect(refreshCalled).toBe(false)
  })

  it('triggers refresh if 60 seconds or less remain before expiration', async () => {
    const now = Math.floor(Date.now() / 1000)
    const token = 'almost-expired-token'
    const expiresAt = now + 45 // 45 seconds left (< 60)

    let refreshCalled = false
    const refreshFn = async () => {
      refreshCalled = true
      return 'refreshed-access-token'
    }

    const getToken = async () => {
      if (expiresAt - now > 60) {
        return token
      }
      return await refreshFn()
    }

    const result = await getToken()
    expect(result).toBe('refreshed-access-token')
    expect(refreshCalled).toBe(true)
  })

  it('deduplicates simultaneous concurrent token refreshes into a single flight', async () => {
    const flights = new Map<string, Promise<string>>()
    const sessionId = 'session-uuid-123'
    let refreshExecutionCount = 0

    const executeRefreshNetworkCall = async (): Promise<string> => {
      refreshExecutionCount++
      // Simulate 50ms network delay
      await new Promise(resolve => setTimeout(resolve, 50))
      return `fresh-token-${refreshExecutionCount}`
    }

    const getRefreshedToken = async (): Promise<string> => {
      if (flights.has(sessionId)) {
        return await flights.get(sessionId)!
      }

      const flight = (async () => {
        try {
          return await executeRefreshNetworkCall()
        } finally {
          flights.delete(sessionId)
        }
      })()

      flights.set(sessionId, flight)
      return await flight
    }

    // Launch 10 simultaneous requests
    const results = await Promise.all([
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken(),
      getRefreshedToken()
    ])

    // Verify all 10 callers received the same token
    expect(results).toHaveLength(10)
    for (const res of results) {
      expect(res).toBe('fresh-token-1')
    }

    // Crucial check: Network call was executed EXACTLY ONCE
    expect(refreshExecutionCount).toBe(1)
    expect(flights.has(sessionId)).toBe(false)
  })
})
