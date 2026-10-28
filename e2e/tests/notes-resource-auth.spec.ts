import { test, expect } from '@playwright/test'

test.describe('Resource-Based Authorization: Personal Notes', () => {
  test('creates, updates, and deletes personal notes with resource policies', async ({ page }) => {
    // 1. Log in
    await page.goto('/login')
    await page.locator('#email').fill('user@identity.local')
    await page.locator('#password').fill('Password123!')
    await page.getByRole('button', { name: /sign in/i }).click()
    await expect(page).toHaveURL(/.*\/app/)

    // 2. Navigate to Notes
    await page.goto('/app/notes')
    await expect(page.getByRole('heading', { name: /my notes/i })).toBeVisible()

    // 3. Open Create Note modal
    const noteTitle = `Architectural Spike ${Date.now()}`
    const noteContent = 'Evaluating ES256 key rollover grace periods in ASP.NET Core 10.'

    await page.getByRole('button', { name: /\+ new note/i }).click()
    await page.locator('#newTitle').fill(noteTitle)
    await page.locator('#newContent').fill(noteContent)
    await page.getByRole('button', { name: /save note/i }).click()

    // 4. Verify note card is listed
    await expect(page.getByText(noteTitle)).toBeVisible()

    // 5. Open note detail page
    await page.getByRole('link', { name: /view \/ edit/i }).first().click()
    await expect(page.locator('#title')).toHaveValue(noteTitle)

    // 6. Update note title
    const updatedTitle = `${noteTitle} (Updated)`
    await page.locator('#title').fill(updatedTitle)
    await page.getByRole('button', { name: /save changes/i }).click()
    await expect(page.getByText('Note updated successfully.')).toBeVisible()

    // 7. Delete note
    page.on('dialog', dialog => dialog.accept())
    await page.getByRole('button', { name: /delete note/i }).click()

    // 8. Verify redirected back to notes index
    await expect(page).toHaveURL(/.*\/app\/notes/)
  })
})
