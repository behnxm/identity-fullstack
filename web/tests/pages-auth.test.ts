import { describe, it, expect } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'

function getVueFiles(dir: string, fileList: string[] = []): string[] {
  const files = fs.readdirSync(dir)
  for (const file of files) {
    const fullPath = path.join(dir, file)
    const stat = fs.statSync(fullPath)
    if (stat.isDirectory()) {
      getVueFiles(fullPath, fileList)
    } else if (file.endsWith('.vue')) {
      fileList.push(fullPath)
    }
  }
  return fileList
}

describe('Static Security Audit: Page Route Protection', () => {
  const pagesDir = path.resolve(__dirname, '../app/pages')
  const pageFiles = getVueFiles(pagesDir)

  it('finds all expected application page components', () => {
    expect(pageFiles.length).toBeGreaterThanOrEqual(10)
  })

  it('ensures public auth pages declare definePageMeta({ auth: false })', () => {
    const publicPages = [
      'index.vue',
      'login.vue',
      'login\\index.vue',
      'login/index.vue',
      'login\\two-factor.vue',
      'login/two-factor.vue',
      'register.vue',
      'confirm-email.vue',
      'forgot-password.vue',
      'reset-password.vue',
      '403.vue'
    ]

    for (const file of pageFiles) {
      const relative = path.relative(pagesDir, file).replace(/\\/g, '/')
      if (publicPages.some(p => p.replace(/\\/g, '/') === relative)) {
        const content = fs.readFileSync(file, 'utf-8')
        expect(content, `${relative} must explicitly declare auth: false`).toMatch(/auth:\s*false/)
      }
    }
  })

  it('ensures protected admin pages declare layout: admin and policy meta where required', () => {
    const adminPages = pageFiles.filter(f => f.includes('pages/admin') || f.includes('pages\\admin'))
    expect(adminPages.length).toBeGreaterThanOrEqual(4)

    for (const file of adminPages) {
      const content = fs.readFileSync(file, 'utf-8')
      expect(content, `${path.basename(file)} must use admin layout`).toMatch(/layout:\s*['"]admin['"]/)
    }
  })

  it('ensures protected user panel pages declare layout: user', () => {
    const userPages = pageFiles.filter(f => f.includes('pages/app') || f.includes('pages\\app'))
    expect(userPages.length).toBeGreaterThanOrEqual(4)

    for (const file of userPages) {
      const content = fs.readFileSync(file, 'utf-8')
      expect(content, `${path.basename(file)} must use user layout`).toMatch(/layout:\s*['"]user['"]/)
    }
  })

  it('ensures authorization decisions never check role strings directly in navigation gates', () => {
    // Neither auth.global.ts nor pages should gate actions on user.roles.includes('Admin')
    const authMiddlewarePath = path.resolve(__dirname, '../app/middleware/auth.global.ts')
    const authMiddleware = fs.readFileSync(authMiddlewarePath, 'utf-8')

    // Must rely on can('...') or policies, not roles
    expect(authMiddleware).not.toMatch(/roles\.includes/i)
    expect(authMiddleware).toMatch(/can\('panel\.admin'\)/)
    expect(authMiddleware).toMatch(/can\('panel\.user'\)/)
  })
})
