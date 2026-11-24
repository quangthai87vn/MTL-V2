import {
  BarChart3,
  Boxes,
  ChevronDown,
  CircleDollarSign,
  LayoutDashboard,
  KeyRound,
  LogOut,
  Menu,
  PackageSearch,
  ReceiptText,
  Settings,
  ShoppingCart,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

type NavItem = {
  label: string
  to: string
  icon: LucideIcon
  permission: string
}

const navItems: NavItem[] = [
  { label: 'Tổng quan', to: '/', icon: LayoutDashboard, permission: 'dashboard.read' },
  { label: 'Sản phẩm', to: '/products', icon: PackageSearch, permission: 'products.read' },
  { label: 'Bán hàng', to: '/sales', icon: ShoppingCart, permission: 'sales.read' },
  { label: 'Nhập hàng', to: '/purchases', icon: ReceiptText, permission: 'purchases.read' },
  { label: 'Kho & serial', to: '/inventory', icon: Boxes, permission: 'inventory.read' },
  { label: 'Khách hàng', to: '/customers', icon: Users, permission: 'customers.read' },
  { label: 'Nhân viên', to: '/employees', icon: CircleDollarSign, permission: 'employees.read' },
  { label: 'Báo cáo', to: '/reports', icon: BarChart3, permission: 'reports.read' },
  { label: 'Cấu hình', to: '/settings', icon: Settings, permission: 'settings.manage' },
]

export function AppShell({ children }: { children: ReactNode }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const { user, logout, hasPermission } = useAuth()

  return (
    <div className="app-shell">
      <aside className={`sidebar ${menuOpen ? 'is-open' : ''}`}>
        <div className="brand">
          <div className="brand-mark">MTL</div>
          <div>
            <strong>MỸ THO LAPTOP</strong>
            <span>Business OS</span>
          </div>
          <button className="icon-button sidebar-close" onClick={() => setMenuOpen(false)} aria-label="Đóng menu">
            <X size={20} />
          </button>
        </div>

        <nav className="main-nav" aria-label="Điều hướng chính">
          <p className="nav-label">Vận hành</p>
          {navItems.filter((item) => hasPermission(item.permission)).map((item) => {
            const Icon = item.icon
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                onClick={() => setMenuOpen(false)}
                className={({ isActive }) => (isActive ? 'nav-item is-active' : 'nav-item')}
              >
                <Icon size={19} strokeWidth={1.9} />
                <span>{item.label}</span>
              </NavLink>
            )
          })}
        </nav>

        <div className="sidebar-foot">
          <span className="system-dot" />
          <div>
            <strong>Hệ thống hoạt động</strong>
            <small>Đồng bộ bảo mật</small>
          </div>
        </div>
      </aside>

      {menuOpen && <button className="sidebar-backdrop" onClick={() => setMenuOpen(false)} aria-label="Đóng menu" />}

      <div className="app-main">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setMenuOpen(true)} aria-label="Mở menu">
            <Menu size={22} />
          </button>
          <div className="topbar-context">
            <span className="eyebrow">Không gian làm việc</span>
            <strong>MỸ THO LAPTOP</strong>
          </div>
          <div className="profile-wrap">
            <button className="profile-button" onClick={() => setProfileOpen((value) => !value)}>
              <span className="avatar">{user?.fullName.slice(0, 1).toUpperCase()}</span>
              <span className="profile-copy">
                <strong>{user?.fullName}</strong>
                <small>{user?.roles[0] ?? 'Nhân viên'}</small>
              </span>
              <ChevronDown size={16} />
            </button>
            {profileOpen && (
              <div className="profile-menu">
                <div>
                  <strong>{user?.email}</strong>
                  <small>Phiên đăng nhập bảo mật</small>
                </div>
                <Link to="/change-password" onClick={() => setProfileOpen(false)}>
                  <KeyRound size={17} /> Đổi mật khẩu
                </Link>
                <button onClick={() => void logout()}>
                  <LogOut size={17} /> Đăng xuất
                </button>
              </div>
            )}
          </div>
        </header>
        <main className="page-content">{children}</main>
      </div>
    </div>
  )
}

