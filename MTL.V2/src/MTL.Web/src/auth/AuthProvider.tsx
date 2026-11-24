import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, ApiError, resetCsrfToken } from '../lib/api'
import type { UserSession } from '../types'

type AuthContextValue = {
  user: UserSession | null
  isLoading: boolean
  login: (email: string, password: string, rememberMe: boolean) => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  logout: () => Promise<void>
  hasPermission: (permission: string) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSession | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    api
      .get<UserSession>('/api/auth/me')
      .then(setUser)
      .catch((error: unknown) => {
        if (!(error instanceof ApiError) || error.status !== 401) console.error(error)
      })
      .finally(() => setIsLoading(false))
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      login: async (email, password, rememberMe) => {
        const session = await api.post<UserSession>('/api/auth/login', { email, password, rememberMe })
        resetCsrfToken()
        setUser(session)
      },
      changePassword: async (currentPassword, newPassword) => {
        const session = await api.post<UserSession>('/api/auth/change-password', { currentPassword, newPassword })
        resetCsrfToken()
        setUser(session)
      },
      logout: async () => {
        await api.post<void>('/api/auth/logout')
        resetCsrfToken()
        setUser(null)
      },
      hasPermission: (permission) => user?.permissions.includes(permission) ?? false,
    }),
    [isLoading, user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// This hook intentionally lives beside its provider to keep the auth contract private.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}

