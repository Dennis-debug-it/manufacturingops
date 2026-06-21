import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ApiError, clearStoredAuth, getStoredAuth, onUnauthorized, persistAuthToken } from '../lib/api'
import { authService, type LoginInput, type SignupInput } from '../services/authService'
import type { ApiBranch, ApiPermissions, ChangePasswordInput, LoginResponse } from '../types/api'
import type { AppSession, AuthBranch, Role } from '../types/app'
import { asBoolean, asNullableString, asString, normalizeBranches, normalizePermissions, pickRecord } from '../utils/apiDefaults'
import { cleanupBodyInteractivity, clearTransientAppState, dispatchUiReset } from '../utils/appCleanup'
import { isPlatformRole } from '../utils/constants'
import { getPlatformRoleLabel, normalizeAppRole, normalizeRole } from '../utils/roles'

const SESSION_STORAGE_KEY = 'ecosys.serviceops.session'

type AuthContextValue = {
  session: AppSession | null
  currentUser: AppSession | null
  role: Role | null
  tenant: { id?: string; name: string; code: string } | null
  permissions: ApiPermissions | null
  branches: AuthBranch[]
  isAuthenticated: boolean
  isReady: boolean
  loading: boolean
  canAccess: (roles: Role[]) => boolean
  updateSession: (partial: Partial<AppSession>) => void
  login: (input: LoginInput, onStatusChange?: (status: 'signing-in' | 'loading-workspace') => void) => Promise<AppSession>
  signup: (input: SignupInput) => Promise<AppSession>
  changePassword: (input: ChangePasswordInput) => Promise<string>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function createAvatar(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('') || 'ES'
}

function mapBranches(branches: ApiBranch[]): AuthBranch[] {
  return branches.map((branch) => ({
    id: branch.id,
    name: branch.name,
    code: branch.code,
    location: branch.location ?? null,
    isActive: branch.isActive,
  }))
}

function unwrapMePayload(payload: unknown) {
  const root = pickRecord(payload) ?? {}
  const container = pickRecord(root.data, root.result) ?? root
  const user = pickRecord(container.user) ?? container
  const tenant = pickRecord(container.tenant)
  const branches = container.branches ?? tenant?.branches ?? root.branches

  return { root, container, user, tenant: tenant ?? {}, branches }
}

function logSessionMappingFailure(payload: unknown) {
  if (!import.meta.env.DEV) return

  console.warn('[auth] Unable to map /api/auth/me response into a session.', payload)
}

function buildSessionFromMe(payload: unknown, token: string): AppSession {
  const { root, container, user, tenant, branches } = unwrapMePayload(payload)
  const role = normalizeAppRole(asString(user.role ?? container.role ?? root.role, 'user'))
  const userId = asString(user.userId ?? user.id ?? container.userId ?? container.id ?? root.userId ?? root.id)
  const fullName = asString(user.fullName ?? user.name ?? container.fullName ?? container.name ?? root.fullName ?? root.name, 'Ecosys User')
  const email = asString(user.email ?? user.emailAddress ?? container.email ?? container.emailAddress ?? root.email ?? root.emailAddress, '')
  const permissions = normalizePermissions(user.permissions ?? container.permissions ?? root.permissions)
  const normalizedBranches = normalizeBranches(branches)
  const tenantId = asString(tenant.tenantId ?? container.tenantId ?? root.tenantId)
  const tenantName = asString(
    tenant.companyName ?? tenant.name ?? container.companyName ?? container.tenantName ?? root.companyName ?? root.tenantName,
    isPlatformRole(role) ? 'Ecosys Platform' : 'Workspace',
  )
  const jobTitle = asNullableString(user.jobTitle ?? container.jobTitle ?? root.jobTitle)
  const department = asNullableString(user.department ?? container.department ?? root.department)
  const defaultBranchId = asNullableString(user.defaultBranchId ?? container.defaultBranchId ?? root.defaultBranchId) ?? undefined
  const mustChangePassword = asBoolean(user.mustChangePassword ?? user.MustChangePassword ?? container.mustChangePassword ?? root.mustChangePassword)

  if (!userId || !email) {
    logSessionMappingFailure(payload)
  }

  return {
    id: userId,
    accountId: userId,
    token,
    userId,
    tenantId,
    fullName,
    name: fullName,
    email,
    role,
    tenantName,
    tenantCode: '',
    title: jobTitle || (isPlatformRole(role) ? getPlatformRoleLabel(role) : role === 'tenantadmin' || role === 'admin' ? 'Tenant Admin' : role === 'technician' ? 'Technician' : 'User'),
    branchId: defaultBranchId,
    defaultBranchId,
    avatar: createAvatar(fullName),
    sessionStartedAt: new Date().toISOString(),
    permissions,
    branches: mapBranches(normalizedBranches),
    department: department ?? undefined,
    hasAllBranchAccess: asBoolean(user.hasAllBranchAccess ?? container.hasAllBranchAccess ?? root.hasAllBranchAccess),
    mustChangePassword,
    country: asNullableString(tenant.country ?? container.country ?? root.country) ?? undefined,
    industry: asNullableString(tenant.industry ?? container.industry ?? root.industry) ?? undefined,
    logoUrl: asNullableString(tenant.logoUrl ?? container.logoUrl ?? root.logoUrl),
    primaryColor: asNullableString(tenant.primaryColor ?? container.primaryColor ?? root.primaryColor) ?? undefined,
    secondaryColor: asNullableString(tenant.secondaryColor ?? container.secondaryColor ?? root.secondaryColor) ?? undefined,
  }
}

function mergeMustChangePassword(session: AppSession, loginResponse: LoginResponse): AppSession {
  return {
    ...session,
    mustChangePassword: asBoolean(loginResponse.user?.mustChangePassword, session.mustChangePassword),
  }
}

function readStoredSession() {
  if (typeof window === 'undefined') return null
  const raw = window.localStorage.getItem(SESSION_STORAGE_KEY)
  if (!raw) return null

  try {
    return JSON.parse(raw) as AppSession
  } catch {
    window.localStorage.removeItem(SESSION_STORAGE_KEY)
    return null
  }
}

function persistSession(session: AppSession | null) {
  if (typeof window === 'undefined') return
  if (!session) {
    window.localStorage.removeItem(SESSION_STORAGE_KEY)
    return
  }
  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
}

function clearLocalSessionState() {
  clearStoredAuth()
  persistSession(null)
  clearTransientAppState()
  cleanupBodyInteractivity()
  dispatchUiReset()
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AppSession | null>(() => readStoredSession())
  const [loading, setLoading] = useState(true)
  const [isReady, setIsReady] = useState(false)

  useEffect(() => {
    onUnauthorized(() => {
      cleanupBodyInteractivity()
      setSession(null)
      setLoading(false)
      setIsReady(true)
      persistSession(null)
      clearTransientAppState()
      dispatchUiReset()
    })
    return () => onUnauthorized(null)
  }, [])

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()

    async function bootstrap() {
      const storedAuth = getStoredAuth()

      if (!storedAuth?.token) {
        if (mounted) {
          cleanupBodyInteractivity()
          setSession(null)
          setLoading(false)
          setIsReady(true)
        }
        return
      }

      try {
        const me = await authService.getCurrentUser(controller.signal)
        if (!mounted) return
        const nextSession = buildSessionFromMe(me, storedAuth.token)
        setSession(nextSession)
        persistSession(nextSession)
      } catch {
        if (!mounted) return
        clearLocalSessionState()
        setSession(null)
      } finally {
        if (mounted) {
          setLoading(false)
          setIsReady(true)
        }
      }
    }

    void bootstrap()

    return () => {
      mounted = false
      controller.abort()
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      currentUser: session,
      role: session?.role ?? null,
      tenant: session
        ? {
            id: session.tenantId,
            name: session.tenantName,
            code: session.tenantCode,
          }
        : null,
      permissions: session?.permissions ?? null,
      branches: session?.branches ?? [],
      isAuthenticated: Boolean(session?.token),
      isReady,
      loading,
      canAccess: (roles) => Boolean(
        session && roles.some((role) => normalizeRole(String(role)) === normalizeRole(String(session.role))),
      ),
      updateSession: (partial) => {
        setSession((prev) => {
          if (!prev) return prev
          const next = { ...prev, ...partial }
          persistSession(next)
          return next
        })
      },
      login: async (input, onStatusChange) => {
        cleanupBodyInteractivity()
        dispatchUiReset()
        setLoading(true)
        try {
          onStatusChange?.('signing-in')
          const loginResponse = await authService.login(input)
          persistAuthToken(loginResponse.token)
          onStatusChange?.('loading-workspace')
          let me: unknown
          try {
            me = await authService.getCurrentUser()
          } catch {
            clearLocalSessionState()
            setSession(null)
            setIsReady(true)
            throw new Error('We could not load your session. Please try again.')
          }
          const nextSession = buildSessionFromMe(me, loginResponse.token)
          const hydratedSession = mergeMustChangePassword(nextSession, loginResponse)
          setSession(hydratedSession)
          persistSession(hydratedSession)
          cleanupBodyInteractivity()
          setIsReady(true)
          return hydratedSession
        } catch (error) {
          clearLocalSessionState()
          setSession(null)
          setIsReady(true)
          if (error instanceof ApiError) {
            if (error.status === 401) {
              throw new Error('Wrong email or password. Please check your details and try again.', { cause: error })
            }

            if (error.status === 403) {
              if (typeof error.message === 'string' && error.message.toLowerCase().includes('inactive')) {
                throw new Error('Your account is inactive. Please contact your administrator.', { cause: error })
              }

              throw new Error('Wrong email or password. Please check your details and try again.', { cause: error })
            }

            if (error.status === 0) {
              throw new Error('Unable to reach the server. Please check your connection and try again.', { cause: error })
            }

            if (error.status >= 500) {
              throw new Error('Something went wrong while signing in. Please try again shortly.', { cause: error })
            }
          }

          throw error
        } finally {
          setLoading(false)
        }
      },
      signup: async (input) => {
        cleanupBodyInteractivity()
        dispatchUiReset()
        setLoading(true)
        try {
          const signupResponse = await authService.signup(input)
          persistAuthToken(signupResponse.token)
          const me = await authService.getCurrentUser()
          const nextSession = buildSessionFromMe(me, signupResponse.token)
          setSession(nextSession)
          persistSession(nextSession)
          cleanupBodyInteractivity()
          setIsReady(true)
          return nextSession
        } catch (error) {
          clearLocalSessionState()
          setSession(null)
          setIsReady(true)
          throw error
        } finally {
          setLoading(false)
        }
      },
      changePassword: async (input) => {
        if (!session) {
          throw new Error('Your session has expired. Please sign in again.')
        }

        setLoading(true)
        try {
          const response = await authService.changePassword(input)
          const nextSession = { ...session, mustChangePassword: false }
          setSession(nextSession)
          persistSession(nextSession)
          return response.message
        } finally {
          setLoading(false)
        }
      },
      logout: async () => {
        setLoading(true)
        setSession(null)
        clearLocalSessionState()
        try {
          await authService.logout()
        } finally {
          cleanupBodyInteractivity()
          setLoading(false)
          setIsReady(true)
        }
      },
    }),
    [isReady, loading, session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider')
  }
  return context
}
