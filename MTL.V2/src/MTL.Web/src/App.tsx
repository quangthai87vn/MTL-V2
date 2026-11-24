import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthProvider'
import { AppShell } from './components/AppShell'
import { CustomersPage } from './pages/CustomersPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { DashboardPage } from './pages/DashboardPage'
import { EmployeesPage } from './pages/EmployeesPage'
import { LoginPage } from './pages/LoginPage'
import { ModulePlaceholderPage } from './pages/ModulePlaceholderPage'
import { ProductsPage } from './pages/ProductsPage'
import { PurchasesPage } from './pages/PurchasesPage'
import { SalesPage } from './pages/SalesPage'
import { SettingsPage } from './pages/SettingsPage'

function ProtectedLayout() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (user.mustChangePassword) return <Navigate to="/change-password" replace />
  return <AppShell><Outlet /></AppShell>
}

export function App() {
  const { user, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="app-loader">
        <div className="loader-mark">MTL</div>
        <span>Đang khởi tạo hệ thống…</span>
      </div>
    )
  }

  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to={user.mustChangePassword ? '/change-password' : '/'} replace /> : <LoginPage />} />
      <Route path="/change-password" element={user ? <ChangePasswordPage /> : <Navigate to="/login" replace />} />
      <Route element={<ProtectedLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="customers" element={<CustomersPage />} />
        <Route path="employees" element={<EmployeesPage />} />
        <Route path="sales" element={<SalesPage />} />
        <Route path="purchases" element={<PurchasesPage />} />
        <Route path="inventory" element={<ModulePlaceholderPage module="inventory" />} />
        <Route path="reports" element={<ModulePlaceholderPage module="reports" />} />
        <Route path="settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to={user ? '/' : '/login'} replace />} />
    </Routes>
  )
}

