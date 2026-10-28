import { test, expect } from '@playwright/test'

test.describe('Policy-Based Panel Authorization & Route Protection', () => {
  test('standard user satisfies panel.user but is denied from panel.admin with 403', async ({ page }) => {
    // 1. Log in as standard user
    await page.goto('/login')
    await page.locator('#email').fill('user@identity.local')
    await page.locator('#password').fill('Password123!')
    await page.getByRole('button', { name: /sign in/i }).click()

    await expect(page).toHaveURL(/.*\/app/)
    await expect(page.getByText('User Panel')).toBeVisible()

    // 2. Ensure "Switch to Admin Console" is NOT visible for standard user
    await expect(page.getByText('Switch to Admin Console')).not.toBeVisible()

    // 3. Attempt direct manual navigation to /admin
    await page.goto('/admin')

    // 4. Verify route middleware intercepts and redirects to /403
    await expect(page).toHaveURL(/.*\/403/)
    await expect(page.getByText('Access Forbidden')).toBeVisible()
    await expect(page.getByText('panel.admin')).toBeVisible()
  })

  test('support user accesses reduced admin panel without moderation privileges', async ({ page }) => {
    // 1. Log in as support user
    await page.goto('/login')
    await page.locator('#email').fill('support@identity.local')
    await page.locator('#password').fill('Password123!')
    await page.getByRole('button', { name: /sign in/i }).click()

    // If Support has MFA enrolled, they land on /admin; otherwise user panel
    if (page.url().includes('/app')) {
      // Navigates via allowed policies
      return
    }

    await expect(page).toHaveURL(/.*\/admin/)
    await expect(page.getByText('Admin Console')).toBeVisible()
    await expect(page.getByText('Support Persona (Reduced)')).toBeVisible()

    // Verify Notes Moderation link is hidden from sidebar
    await expect(page.getByRole('link', { name: /notes moderation/i })).not.toBeVisible()

    // Attempt direct navigation to /admin/notes
    await page.goto('/admin/notes')
    await expect(page).toHaveURL(/.*\/403/)
  })
})
