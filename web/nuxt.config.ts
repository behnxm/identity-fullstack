// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2026-10-28',
  future: {
    compatibilityVersion: 4
  },
  devtools: { enabled: false },

  modules: [
    '@nuxt/ui',
    'nuxt-auth-utils',
    'nuxt-security'
  ],

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    apiBase: process.env.API_BASE_URL || 'http://127.0.0.1:5000',
    session: {
      name: process.env.NODE_ENV === 'production' ? '__Host-id_session' : 'id_session',
      password: process.env.NUXT_SESSION_PASSWORD || '32_character_minimum_dev_secret_key_pass!!',
      cookie: {
        httpOnly: true,
        sameSite: 'lax',
        secure: process.env.NODE_ENV === 'production',
        maxAge: 60 * 60 * 8 // 8 hours default, extended if rememberMe is set
      }
    }
  },

  security: {
    headers: {
      contentSecurityPolicy: {
        'default-src': ["'self'"],
        'script-src': ["'self'", "'unsafe-inline'"], // unsafe-inline with nonce support
        'style-src': ["'self'", "'unsafe-inline'"],
        'font-src': ["'self'"],
        'img-src': ["'self'", 'data:'],
        'connect-src': ["'self'"],
        'object-src': ["'none'"],
        'base-uri': ["'none'"],
        'frame-ancestors': ["'none'"]
      },
      crossOriginEmbedderPolicy: 'credentialless',
      crossOriginOpenerPolicy: 'same-origin',
      crossOriginResourcePolicy: 'same-origin',
      referrerPolicy: 'strict-origin-when-cross-origin',
      strictTransportSecurity: {
        maxAge: 31536000,
        includeSubdomains: true,
        preload: true
      },
      xContentTypeOptions: 'nosniff',
      xFrameOptions: 'DENY',
      permissionsPolicy: {
        camera: ['none'],
        microphone: ['none'],
        geolocation: ['none']
      }
    },
    csrf: false, // Managed by custom server/middleware/csrf.ts
    rateLimiter: false
  },

  typescript: {
    strict: true
  }
})
