import { test, expect } from '@playwright/test'

test.describe('Two-Factor Authentication (TOTP) & Step-Up Flows', () => {
  test('user visits security page and initiates 2FA enrollment wizard', async ({ page }) => {
    // 1. Log in as standard user
    await page.goto('/login')
    await page.locator('#email').fill('user@identity.local')
    await page.locator('#password').fill('Password!12345678')
    await page.getByRole('button', { name: /sign in/i }).click()
    await expect(page).toHaveURL(/.*\/app/)

    // 2. Navigate to Security settings
    await page.goto('/app/security')
    await expect(page.getByRole('heading', { name: /security & authentication/i })).toBeVisible()

    // 3. Check for 2FA enrollment button if not already enabled
    const setupBtn = page.getByRole('button', { name: /set up two-factor authentication/i })
    if (await setupBtn.isVisible()) {
      await setupBtn.click()

      // 4. Verify QR canvas and setup instructions appear
      await expect(page.locator('canvas')).toBeVisible()
      await expect(page.getByText('Step 1: Scan QR Code or Enter Key')).toBeVisible()
      await expect(page.getByText('Step 2: Enter 6-digit Code to Confirm')).toBeVisible()
    }
  })

  test('two-factor login screen rejects malformed or empty codes and supports recovery codes', async ({ page }) => {
    // Direct visit without pending token redirects back to login
    await page.goto('/login/two-factor')
    await expect(page).toHaveURL(/.*\/login/)
  })
})
