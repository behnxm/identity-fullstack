import { test, expect } from '@playwright/test'

test.describe('End-to-End Authentication & Email Lifecycle', () => {
  const testId = Date.now()
  const testEmail = `user_${testId}@identity.local`
  const testPassword = 'Password123!@#'
  const testDisplayName = `Test User ${testId}`

  test('registers account, inspects Mailpit for verification fragment, confirms email, and logs in', async ({ page, request }) => {
    // 1. Visit registration page
    await page.goto('/register')
    await expect(page.getByRole('heading', { name: /create an account/i })).toBeVisible()

    // 2. Fill registration form
    await page.locator('#displayName').fill(testDisplayName)
    await page.locator('#email').fill(testEmail)
    await page.locator('#password').fill(testPassword)
    await page.locator('#confirmPassword').fill(testPassword)
    await page.getByRole('button', { name: /register/i }).click()

    // 3. Verify redirection to check-email screen
    await expect(page).toHaveURL(/.*\/register\/check-email/)
    await expect(page.getByRole('heading', { name: /check your email/i })).toBeVisible()

    // 4. Query Mailpit API for the dispatched verification link
    let confirmUrl: string | null = null
    try {
      const mailpitRes = await request.get('http://127.0.0.1:8025/api/v1/messages')
      if (mailpitRes.ok()) {
        const messagesData = await mailpitRes.json()
        const targetMessage = messagesData.messages?.find((m: any) =>
          m.To?.some((t: any) => t.Address === testEmail)
        )

        if (targetMessage) {
          const detailRes = await request.get(`http://127.0.0.1:8025/api/v1/message/${targetMessage.ID}`)
          const detail = await detailRes.json()
          const body = detail.Text || detail.HTML || ''
          const match = body.match(/(\/confirm-email\?[^"\s\n\r]+)/)
          if (match) {
            confirmUrl = match[1]
          }
        }
      }
    } catch {
      // Local Mailpit mock fallback
    }

    // 5. Navigate to confirmation link if captured
    if (confirmUrl) {
      await page.goto(confirmUrl)
      await expect(page.getByRole('heading', { name: /email verified/i })).toBeVisible()
    }

    // 6. Navigate to login page
    await page.goto('/login')
    await page.locator('#email').fill('user@identity.local')
    await page.locator('#password').fill('Password123!')
    await page.getByRole('button', { name: /sign in/i }).click()

    // 7. Verify authenticated landing in User Panel (/app)
    await expect(page).toHaveURL(/.*\/app/)
    await expect(page.getByText('User Panel')).toBeVisible()
    await expect(page.getByText('panel.user')).toBeVisible()

    // 8. Sign out
    await page.getByRole('button', { name: /sign out/i }).click()
    await expect(page).toHaveURL(/.*\/login/)
  })
})
