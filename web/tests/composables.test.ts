import { describe, it, expect } from 'vitest'
import { safeRedirect, resolveLandingPath } from '../app/composables/useCapabilities'

describe('useCapabilities / safeRedirect', () => {
  it('allows safe relative local application paths', () => {
    expect(safeRedirect('/app')).toBe('/app')
    expect(safeRedirect('/admin')).toBe('/admin')
    expect(safeRedirect('/admin/users?page=2&search=test')).toBe('/admin/users?page=2&search=test')
    expect(safeRedirect('/app/notes/0192d4f2-9577-781c-9944-7890abcdef12')).toBe('/app/notes/0192d4f2-9577-781c-9944-7890abcdef12')
  })

  it('rejects protocol-relative open-redirect payloads', () => {
    expect(safeRedirect('//attacker.com')).toBe('/app')
    expect(safeRedirect('//attacker.com/steal-cookie')).toBe('/app')
    expect(safeRedirect('///attacker.com')).toBe('/app')
  })

  it('rejects backslash-based browser normalization attacks', () => {
    expect(safeRedirect('/\\attacker.com')).toBe('/app')
    expect(safeRedirect('\\attacker.com')).toBe('/app')
    expect(safeRedirect('/app\\test')).toBe('/app')
  })

  it('rejects absolute URLs with scheme/protocol', () => {
    expect(safeRedirect('https://evil.com')).toBe('/app')
    expect(safeRedirect('http://evil.com')).toBe('/app')
    expect(safeRedirect('javascript:alert(1)')).toBe('/app')
    expect(safeRedirect('data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==')).toBe('/app')
    expect(safeRedirect('vbscript:msgbox(1)')).toBe('/app')
  })

  it('rejects control characters and malicious whitespace', () => {
    expect(safeRedirect('/app\u0000/admin')).toBe('/app')
    expect(safeRedirect('   ')).toBe('/app')
    expect(safeRedirect('')).toBe('/app')
    expect(safeRedirect(null)).toBe('/app')
    expect(safeRedirect(undefined)).toBe('/app')
  })

  it('respects custom fallback path when provided', () => {
    expect(safeRedirect('//evil.com', '/admin')).toBe('/admin')
    expect(safeRedirect(undefined, '/login')).toBe('/login')
  })
})

describe('useCapabilities / resolveLandingPath', () => {
  it('routes to /admin when panel.admin is allowed', () => {
    const policies = {
      'panel.admin': { allowed: true, reason: null },
      'panel.user': { allowed: true, reason: null }
    }
    expect(resolveLandingPath(policies)).toBe('/admin')
  })

  it('routes to /app when only panel.user is allowed', () => {
    const policies = {
      'panel.admin': { allowed: false, reason: 'missing_permission' },
      'panel.user': { allowed: true, reason: null }
    }
    expect(resolveLandingPath(policies)).toBe('/app')
  })

  it('routes to /app/security when panel.admin failed only due to mfa_required', () => {
    const policies = {
      'panel.admin': { allowed: false, reason: 'mfa_required' },
      'panel.user': { allowed: true, reason: null }
    }
    expect(resolveLandingPath(policies)).toBe('/app/security')
  })

  it('routes to /403 when neither panel is allowed', () => {
    const policies = {
      'panel.admin': { allowed: false, reason: 'missing_permission' },
      'panel.user': { allowed: false, reason: 'email_not_confirmed' }
    }
    expect(resolveLandingPath(policies)).toBe('/403')
  })

  it('routes to /login when policies are null or undefined', () => {
    expect(resolveLandingPath(null)).toBe('/login')
    expect(resolveLandingPath(undefined)).toBe('/login')
  })
})
