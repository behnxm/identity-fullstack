import { test, expect } from '@playwright/test'

test.describe('Session Security & Rotating Refresh Token Management', () => {
  test('displays active sessions table with current device badge and password update', async ({ page }) => {
    // 1. Log in
    await page.goto('/login')
    await page.locator('#email').fill('user@identity.local')
    await page.locator('#password').fill('Password!12345678')
    await page.getByRole('button', { name: /sign in/i }).click()
    await expect(page).toHaveURL(/.*\/app/)

    // 2. Navigate to security
    await page.goto('/app/security')

    // 3. Verify Active Sessions table
    await expect(page.getByRole('heading', { name: /active sessions/i })).toBeVisible()
    await expect(page.getByText('Current Device')).toBeVisible()

    // 4. Verify password change form validation
    await page.locator('#currentPassword').fill('Password!12345678')
    await page.locator('#newSecPassword').fill('Short')
    await page.locator('#confirmSecPassword').fill('Short')
    await page.getByRole('button', { name: /update password/i }).click()

    // HTML5 or validation message should require at least 12 characters
    const validationMessage = await page.locator('#newSecPassword').evaluate((el: HTMLInputElement) => el.validationMessage)
    expect(validationMessage.length).toBeGreaterThan(0)
  })
})
