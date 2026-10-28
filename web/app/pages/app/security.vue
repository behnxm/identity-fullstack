<script setup lang="ts">
import QRCode from 'qrcode'

definePageMeta({
  layout: 'user'
})

interface UserSessionResponse {
  id: string
  createdAt: string
  lastSeenAt: string
  expiresAt: string
  ipAddress: string | null
  userAgent: string | null
  isCurrent: boolean
}

const route = useRoute()
const router = useRouter()
const { user, fetch: fetchSession } = useUserSession()
const { can } = useCapabilities()
const { proxyGet, proxyPost, proxyDelete, enable2Fa, disable2Fa, logoutAll, refreshPolicies } = useBff()

const isMfaRequiredBanner = computed(() => route.query.required_mfa === '1')

// Change Password state
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const passwordLoading = ref(false)
const passwordError = ref<string | null>(null)
const passwordSuccess = ref<string | null>(null)

// 2FA state
const qrCanvas = ref<HTMLCanvasElement | null>(null)
const setupSharedKey = ref('')
const setupUri = ref('')
const isSettingUp2Fa = ref(false)
const verificationCode = ref('')
const mfaLoading = ref(false)
const mfaError = ref<string | null>(null)
const recoveryCodes = ref<string[] | null>(null)

// Disable 2FA modal state
const showDisableModal = ref(false)
const disablePassword = ref('')
const disableCode = ref('')
const disableLoading = ref(false)
const disableError = ref<string | null>(null)

// Sessions state
const sessions = ref<UserSessionResponse[]>([])
const sessionsLoading = ref(true)
const sessionActionLoading = ref(false)

const loadSessions = async () => {
  sessionsLoading.value = true
  try {
    sessions.value = await proxyGet<UserSessionResponse[]>('/me/sessions')
  } catch {
    // Session list error
  } finally {
    sessionsLoading.value = false
  }
}

onMounted(() => {
  loadSessions()
})

const handleChangePassword = async () => {
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'New passwords do not match.'
    return
  }
  if (newPassword.value.length < 12) {
    passwordError.value = 'New password must be at least 12 characters.'
    return
  }

  passwordLoading.value = true
  passwordError.value = null
  passwordSuccess.value = null

  try {
    await proxyPost('/me/security/password', {
      currentPassword: currentPassword.value,
      newPassword: newPassword.value
    })
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
    passwordSuccess.value = 'Password changed successfully. All other active sessions have been revoked.'
    await loadSessions()
  } catch (err: any) {
    passwordError.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to update password.'
  } finally {
    passwordLoading.value = false
  }
}

const startTwoFactorSetup = async () => {
  mfaLoading.value = true
  mfaError.value = null
  try {
    const res = await proxyGet<{ sharedKey: string; authenticatorUri: string }>('/me/security/2fa/setup')
    setupSharedKey.value = res.sharedKey
    setupUri.value = res.authenticatorUri
    isSettingUp2Fa.value = true

    await nextTick()
    if (qrCanvas.value) {
      await QRCode.toCanvas(qrCanvas.value, res.authenticatorUri, {
        width: 180,
        margin: 1,
        color: {
          dark: '#000000',
          light: '#ffffff'
        }
      })
    }
  } catch (err: any) {
    mfaError.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to initialize 2FA setup.'
  } finally {
    mfaLoading.value = false
  }
}

const confirmTwoFactorSetup = async () => {
  if (!verificationCode.value.trim()) return
  mfaLoading.value = true
  mfaError.value = null

  try {
    const res = await enable2Fa(verificationCode.value.trim())
    recoveryCodes.value = res.recoveryCodes
    isSettingUp2Fa.value = false
    verificationCode.value = ''
    await fetchSession()
    await refreshPolicies()
  } catch (err: any) {
    mfaError.value = err?.data?.detail || err?.data?.title || err?.message || 'Invalid verification code.'
  } finally {
    mfaLoading.value = false
  }
}

const handleRegenerateRecoveryCodes = async () => {
  if (!confirm('This will invalidate all previous recovery codes. Continue?')) return
  mfaLoading.value = true
  mfaError.value = null
  try {
    const res = await proxyPost<{ recoveryCodes: string[] }>('/me/security/2fa/recovery-codes')
    recoveryCodes.value = res.recoveryCodes
  } catch (err: any) {
    mfaError.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to regenerate recovery codes.'
  } finally {
    mfaLoading.value = false
  }
}

const handleDisableTwoFactor = async () => {
  disableLoading.value = true
  disableError.value = null
  try {
    await disable2Fa({
      password: disablePassword.value,
      code: disableCode.value.trim()
    })
    showDisableModal.value = false
    disablePassword.value = ''
    disableCode.value = ''
    await fetchSession()
    await refreshPolicies()
  } catch (err: any) {
    disableError.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to disable 2FA.'
  } finally {
    disableLoading.value = false
  }
}

const handleRevokeSession = async (sessionId: string) => {
  sessionActionLoading.value = true
  try {
    await proxyDelete(`/me/sessions/${sessionId}`)
    await loadSessions()
  } catch (err: any) {
    alert(err?.data?.detail || 'Failed to revoke session.')
  } finally {
    sessionActionLoading.value = false
  }
}

const handleLogoutAll = async () => {
  if (!confirm('Are you sure you want to log out from all sessions including this one?')) return
  await logoutAll()
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-neutral-900 dark:text-white">
        Security & Authentication
      </h1>
      <p class="text-xs text-neutral-500 mt-0.5">
        Manage two-factor authentication, active sessions, and account credentials.
      </p>
    </div>

    <!-- MFA Required Banner (Forced Enrollment Flow) -->
    <div
      v-if="isMfaRequiredBanner"
      class="p-4 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-start space-x-3"
      role="alert"
    >
      <div class="w-6 h-6 rounded-md bg-amber-500 text-neutral-950 flex items-center justify-center font-bold text-xs shrink-0 mt-0.5">
        !
      </div>
      <div>
        <h3 class="text-sm font-semibold text-amber-400">
          Two-Factor Authentication Required for Admin Access
        </h3>
        <p class="text-xs text-neutral-300 mt-1 leading-relaxed">
          Your account holds administrative permissions, but policy <code class="font-mono text-amber-300">panel.admin</code> mandates active multi-factor authentication.
          Complete the TOTP enrollment below to access the Admin Console.
        </p>
      </div>
    </div>

    <!-- Two-Factor Authentication Section -->
    <div class="bg-white dark:bg-neutral-900 rounded-2xl p-6 border border-neutral-200 dark:border-neutral-800 shadow-sm space-y-4">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-base font-semibold text-neutral-900 dark:text-white">
            Two-Factor Authentication (TOTP)
          </h2>
          <p class="text-xs text-neutral-500 mt-0.5">
            RFC 6238 time-based one-time passwords for zero-trust step-up authorization.
          </p>
        </div>

        <span
          class="px-2.5 py-1 rounded-full text-xs font-semibold"
          :class="user?.twoFactorEnabled ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800' : 'bg-neutral-100 dark:bg-neutral-800 text-neutral-600 dark:text-neutral-400'"
        >
          {{ user?.twoFactorEnabled ? 'Enabled' : 'Disabled' }}
        </span>
      </div>

      <div
        v-if="mfaError"
        class="p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      >
        {{ mfaError }}
      </div>

      <!-- Recovery Codes Display Box -->
      <div
        v-if="recoveryCodes"
        class="p-5 rounded-xl bg-amber-500/10 border border-amber-500/30 space-y-3"
      >
        <div class="flex items-center justify-between">
          <h3 class="text-xs font-bold text-amber-400 uppercase tracking-wider">
            Emergency Recovery Codes (Save These Now)
          </h3>
          <span class="text-[10px] text-neutral-400">Single-use each</span>
        </div>
        <p class="text-xs text-neutral-300 leading-relaxed">
          Store these 10 recovery codes in a secure password manager. If you lose access to your authenticator app, these are the only way to recover your account.
        </p>
        <div class="grid grid-cols-2 sm:grid-cols-5 gap-2 bg-neutral-950 p-4 rounded-lg border border-neutral-800 font-mono text-xs text-center text-amber-300">
          <div v-for="c in recoveryCodes" :key="c" class="py-1 bg-neutral-900 rounded select-all">
            {{ c }}
          </div>
        </div>
        <div class="flex items-center justify-between pt-1">
          <button
            type="button"
            class="text-xs text-neutral-400 hover:text-white underline"
            @click="recoveryCodes = null"
          >
            I have securely saved these codes
          </button>

          <NuxtLink
            v-if="can('panel.admin')"
            to="/admin"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-400 text-neutral-950 transition-colors"
          >
            Proceed to Admin Console →
          </NuxtLink>
        </div>
      </div>

      <!-- Not Enabled & Not Setting Up -->
      <div v-if="!user?.twoFactorEnabled && !isSettingUp2Fa && !recoveryCodes" class="pt-2">
        <p class="text-xs text-neutral-500 dark:text-neutral-400 mb-4">
          Add an extra layer of security using Google Authenticator, Microsoft Authenticator, 1Password, or any RFC 6238 compatible app.
        </p>
        <button
          type="button"
          :disabled="mfaLoading"
          class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
          @click="startTwoFactorSetup"
        >
          <span v-if="mfaLoading">Generating Key...</span>
          <span v-else>Set Up Two-Factor Authentication</span>
        </button>
      </div>

      <!-- Enrollment Wizard in Progress -->
      <div v-if="isSettingUp2Fa" class="pt-2 border-t border-neutral-200 dark:border-neutral-800 space-y-4">
        <div class="text-xs font-semibold text-neutral-800 dark:text-neutral-200">
          Step 1: Scan QR Code or Enter Key
        </div>

        <div class="flex flex-col sm:flex-row items-center gap-6">
          <div class="bg-white p-2 rounded-xl shadow-inner border border-neutral-200">
            <canvas ref="qrCanvas" class="w-[180px] h-[180px]" />
          </div>

          <div class="space-y-2 text-xs text-neutral-600 dark:text-neutral-400">
            <p>1. Open your authenticator app and scan the QR code.</p>
            <p>2. If you cannot scan the code, enter this key manually:</p>
            <div class="p-2.5 bg-neutral-100 dark:bg-neutral-800 rounded-lg font-mono text-xs font-semibold text-neutral-900 dark:text-neutral-100 select-all border border-neutral-200 dark:border-neutral-700">
              {{ setupSharedKey }}
            </div>
            <p class="text-[11px] text-neutral-500">
              Algorithm: HMAC-SHA1, 30-second timestep, 6 digits.
            </p>
          </div>
        </div>

        <div class="pt-2">
          <div class="text-xs font-semibold text-neutral-800 dark:text-neutral-200 mb-2">
            Step 2: Enter 6-digit Code to Confirm
          </div>
          <form class="flex items-center gap-3 max-w-sm" @submit.prevent="confirmTwoFactorSetup">
            <input
              v-model="verificationCode"
              type="text"
              maxlength="7"
              required
              autocomplete="one-time-code"
              placeholder="123456"
              class="w-36 text-center font-mono text-base px-3 py-1.5 rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
            <button
              type="submit"
              :disabled="mfaLoading || !verificationCode.trim()"
              class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
            >
              <span v-if="mfaLoading">Verifying...</span>
              <span v-else>Verify & Enable</span>
            </button>
            <button
              type="button"
              class="text-xs text-neutral-500 hover:text-neutral-700 dark:hover:text-neutral-300"
              @click="isSettingUp2Fa = false"
            >
              Cancel
            </button>
          </form>
        </div>
      </div>

      <!-- Already Enabled Actions -->
      <div v-if="user?.twoFactorEnabled && !recoveryCodes" class="flex flex-wrap items-center gap-3 pt-2">
        <button
          type="button"
          :disabled="mfaLoading"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-neutral-100 dark:bg-neutral-800 hover:bg-neutral-200 dark:hover:bg-neutral-700 text-neutral-800 dark:text-neutral-200 transition-colors"
          @click="handleRegenerateRecoveryCodes"
        >
          Regenerate Recovery Codes
        </button>

        <button
          type="button"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-50 dark:bg-red-950/40 hover:bg-red-100 dark:hover:bg-red-900/60 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-900 transition-colors"
          @click="showDisableModal = true"
        >
          Disable 2FA
        </button>
      </div>
    </div>

    <!-- Change Password Card -->
    <div class="bg-white dark:bg-neutral-900 rounded-2xl p-6 border border-neutral-200 dark:border-neutral-800 shadow-sm space-y-4">
      <div>
        <h2 class="text-base font-semibold text-neutral-900 dark:text-white">
          Change Password
        </h2>
        <p class="text-xs text-neutral-500 mt-0.5">
          Updating your password will immediately revoke all other active sessions across devices.
        </p>
      </div>

      <div
        v-if="passwordError"
        class="p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      >
        {{ passwordError }}
      </div>

      <div
        v-if="passwordSuccess"
        class="p-3 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-700 dark:text-emerald-300 font-medium"
      >
        {{ passwordSuccess }}
      </div>

      <form class="space-y-4 max-w-lg" @submit.prevent="handleChangePassword">
        <div>
          <label for="currentPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Current Password
          </label>
          <input
            id="currentPassword"
            v-model="currentPassword"
            type="password"
            required
            autocomplete="current-password"
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
        </div>

        <div>
          <label for="newSecPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            New Password <span class="text-neutral-400 font-normal">(min 12 characters)</span>
          </label>
          <input
            id="newSecPassword"
            v-model="newPassword"
            type="password"
            required
            minlength="12"
            autocomplete="new-password"
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
        </div>

        <div>
          <label for="confirmSecPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Confirm New Password
          </label>
          <input
            id="confirmSecPassword"
            v-model="confirmPassword"
            type="password"
            required
            autocomplete="new-password"
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
        </div>

        <button
          type="submit"
          :disabled="passwordLoading || !newPassword"
          class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
        >
          <span v-if="passwordLoading">Updating...</span>
          <span v-else>Update Password</span>
        </button>
      </form>
    </div>

    <!-- Active Sessions Section -->
    <div class="bg-white dark:bg-neutral-900 rounded-2xl border border-neutral-200 dark:border-neutral-800 overflow-hidden shadow-sm">
      <div class="px-6 py-4 border-b border-neutral-200 dark:border-neutral-800 flex items-center justify-between">
        <div>
          <h2 class="text-base font-semibold text-neutral-900 dark:text-white">
            Active Sessions
          </h2>
          <p class="text-xs text-neutral-500 mt-0.5">
            Devices and browser instances with active rotating refresh tokens.
          </p>
        </div>

        <button
          type="button"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-600 hover:bg-red-500 text-white transition-colors"
          @click="handleLogoutAll"
        >
          Revoke All Sessions
        </button>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs">
          <thead class="bg-neutral-50 dark:bg-neutral-800/50 text-neutral-500 font-semibold border-b border-neutral-200 dark:border-neutral-800">
            <tr>
              <th class="px-6 py-3">Device / User Agent</th>
              <th class="px-6 py-3">IP Address</th>
              <th class="px-6 py-3">Last Active</th>
              <th class="px-6 py-3">Status</th>
              <th class="px-6 py-3 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-neutral-200 dark:divide-neutral-800">
            <tr v-for="s in sessions" :key="s.id" class="hover:bg-neutral-50 dark:hover:bg-neutral-800/30">
              <td class="px-6 py-3 max-w-xs truncate text-neutral-800 dark:text-neutral-200 font-mono text-[11px]">
                {{ s.userAgent || 'Unknown Device' }}
              </td>
              <td class="px-6 py-3 font-mono text-neutral-600 dark:text-neutral-400">
                {{ s.ipAddress || '—' }}
              </td>
              <td class="px-6 py-3 text-neutral-500">
                {{ new Date(s.lastSeenAt).toLocaleString() }}
              </td>
              <td class="px-6 py-3">
                <span
                  v-if="s.isCurrent"
                  class="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800"
                >
                  Current Device
                </span>
                <span v-else class="text-[10px] text-neutral-400">
                  Active
                </span>
              </td>
              <td class="px-6 py-3 text-right">
                <button
                  v-if="!s.isCurrent"
                  type="button"
                  :disabled="sessionActionLoading"
                  class="text-xs font-semibold text-red-600 dark:text-red-400 hover:underline disabled:opacity-50"
                  @click="handleRevokeSession(s.id)"
                >
                  Revoke
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Disable 2FA Modal -->
    <div
      v-if="showDisableModal"
      class="fixed inset-0 z-50 bg-neutral-950/70 backdrop-blur-sm flex items-center justify-center p-4"
    >
      <div class="bg-white dark:bg-neutral-900 border border-neutral-200 dark:border-neutral-800 rounded-2xl max-w-md w-full p-6 shadow-2xl">
        <h2 class="text-lg font-bold text-neutral-900 dark:text-white mb-2">
          Disable Two-Factor Authentication
        </h2>
        <p class="text-xs text-neutral-500 mb-4">
          Enter your current password and a valid 6-digit TOTP code to confirm disabling 2FA.
        </p>

        <div
          v-if="disableError"
          class="mb-4 p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300"
        >
          {{ disableError }}
        </div>

        <form class="space-y-4" @submit.prevent="handleDisableTwoFactor">
          <div>
            <label for="disablePassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
              Account Password
            </label>
            <input
              id="disablePassword"
              v-model="disablePassword"
              type="password"
              required
              class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
          </div>

          <div>
            <label for="disableCode" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
              Current 6-Digit TOTP Code
            </label>
            <input
              id="disableCode"
              v-model="disableCode"
              type="text"
              maxlength="7"
              required
              placeholder="123456"
              class="w-full px-3 py-2 text-center font-mono text-base rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
          </div>

          <div class="flex items-center justify-end space-x-2 pt-2">
            <button
              type="button"
              class="px-3 py-2 text-xs font-medium rounded-lg text-neutral-600 dark:text-neutral-400 hover:bg-neutral-100 dark:hover:bg-neutral-800"
              @click="showDisableModal = false"
            >
              Cancel
            </button>
            <button
              type="submit"
              :disabled="disableLoading || !disablePassword || !disableCode"
              class="px-4 py-2 text-xs font-semibold rounded-lg bg-red-600 hover:bg-red-500 text-white transition-colors disabled:opacity-50"
            >
              <span v-if="disableLoading">Disabling...</span>
              <span v-else>Confirm & Disable</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
