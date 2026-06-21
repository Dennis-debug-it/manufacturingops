import { api } from '../lib/api'
import type { ChangePasswordInput, LoginResponse, SignupResponse } from '../types/api'

export type LoginInput = {
  email: string
  password: string
}

export type SignupInput = {
  fullName: string
  email: string
  password: string
  companyName: string
  industry?: string
  country: string
}

export type ResetPasswordInput = {
  token: string
  newPassword: string
  confirmPassword: string
}

export const authService = {
  async login(input: LoginInput) {
    return api.post<LoginResponse>('/api/auth/login', {
      email: input.email.trim(),
      password: input.password,
    })
  },

  async signup(input: SignupInput) {
    return api.post<SignupResponse>('/api/auth/signup', {
      fullName: input.fullName.trim(),
      email: input.email.trim(),
      password: input.password,
      companyName: input.companyName.trim(),
      industry: input.industry?.trim() || null,
      country: input.country.trim(),
    })
  },

  getCurrentUser(signal?: AbortSignal) {
    return api.get<unknown>('/api/auth/me', { signal })
  },

  forgotPassword(email: string) {
    return api.post<{ message: string }>('/api/auth/forgot-password', {
      email: email.trim(),
    })
  },

  resetPassword(input: ResetPasswordInput) {
    return api.post<{ message: string }>('/api/auth/reset-password', {
      token: input.token,
      newPassword: input.newPassword,
      confirmPassword: input.confirmPassword,
    })
  },

  changePassword(input: ChangePasswordInput) {
    return api.post<{ message: string }>('/api/auth/change-password', {
      currentPassword: input.currentPassword,
      newPassword: input.newPassword,
      confirmPassword: input.confirmPassword,
    })
  },

  async logout() {
    try {
      await api.post('/api/auth/logout')
    } catch {
      // Ignore logout network errors so we can always clear the local session.
    }
  },
}
