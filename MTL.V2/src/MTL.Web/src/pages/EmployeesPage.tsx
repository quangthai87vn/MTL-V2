import {
  CheckCircle2,
  KeyRound,
  LockKeyhole,
  Pencil,
  Plus,
  Save,
  Search,
  ShieldCheck,
  Trash2,
  UserRoundCheck,
  Users,
  X,
} from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { PageHeader } from '../components/PageHeader'
import { StatusBadge } from '../components/StatusBadge'
import { api } from '../lib/api'
import type { Employee, EmployeeRole, PermissionItem } from '../types'

type EmployeeStatusFilter = 'all' | 'active' | 'inactive' | 'locked' | 'password'

const roleLabels: Record<string, string> = {
  Administrator: 'Quản trị viên',
  Manager: 'Quản lý',
  Sales: 'Bán hàng',
  Warehouse: 'Kho',
}

function displayRole(name: string) {
  return roleLabels[name] ?? name
}

function isLockedOut(employee: Employee) {
  return Boolean(employee.lockoutEndUtc && new Date(employee.lockoutEndUtc).getTime() > Date.now())
}

export function EmployeesPage() {
  const [tab, setTab] = useState<'employees' | 'roles'>('employees')
  const [editor, setEditor] = useState<Employee | 'new' | null>(null)
  const [resetTarget, setResetTarget] = useState<Employee | null>(null)
  const [roleEditor, setRoleEditor] = useState<EmployeeRole | 'new' | null>(null)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<EmployeeStatusFilter>('all')
  const [roleFilter, setRoleFilter] = useState('all')
  const { user, hasPermission } = useAuth()
  const queryClient = useQueryClient()
  const employeesQuery = useQuery({ queryKey: ['employees'], queryFn: () => api.get<Employee[]>('/api/employees') })
  const rolesQuery = useQuery({ queryKey: ['employee-roles'], queryFn: () => api.get<EmployeeRole[]>('/api/employees/roles') })
  const permissionsQuery = useQuery({ queryKey: ['permission-catalog'], queryFn: () => api.get<PermissionItem[]>('/api/employees/permissions'), enabled: hasPermission('roles.manage') })
  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => api.put(`/api/employees/${id}/status`, { isActive }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['employees'] }),
  })
  const deleteRoleMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/api/employees/roles/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['employee-roles'] }),
  })
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['employees'] })
    void queryClient.invalidateQueries({ queryKey: ['employee-roles'] })
  }

  const employees = employeesQuery.data ?? []
  const filteredEmployees = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase('vi-VN')
    return employees.filter((employee) => {
      const matchesSearch = !normalizedSearch || [employee.fullName, employee.email, employee.phone ?? '', ...employee.roles]
        .some((value) => value.toLocaleLowerCase('vi-VN').includes(normalizedSearch))
      const locked = isLockedOut(employee)
      const matchesStatus = statusFilter === 'all' ||
        (statusFilter === 'active' && employee.isActive && !locked) ||
        (statusFilter === 'inactive' && !employee.isActive) ||
        (statusFilter === 'locked' && locked) ||
        (statusFilter === 'password' && employee.mustChangePassword)
      const matchesRole = roleFilter === 'all' || employee.roles.includes(roleFilter)
      return matchesSearch && matchesStatus && matchesRole
    })
  }, [employees, roleFilter, search, statusFilter])

  const activeCount = employees.filter((employee) => employee.isActive && !isLockedOut(employee)).length
  const disabledCount = employees.filter((employee) => !employee.isActive || isLockedOut(employee)).length
  const pendingPasswordCount = employees.filter((employee) => employee.mustChangePassword).length

  function removeRole(role: EmployeeRole) {
    if (role.isSystem || role.userCount > 0) return
    if (window.confirm(`Xóa vai trò “${role.name}”?`)) deleteRoleMutation.mutate(role.id)
  }

  return <>
    <PageHeader eyebrow="NHÂN SỰ & BẢO MẬT" title="Nhân viên" description="Quản lý tài khoản, vai trò và quyền sử dụng hệ thống." actions={tab === 'employees' && hasPermission('employees.manage') ? <button className="primary-button" onClick={() => setEditor('new')}><Plus size={18} /> Thêm nhân viên</button> : tab === 'roles' && hasPermission('roles.manage') ? <button className="primary-button" onClick={() => setRoleEditor('new')}><Plus size={18} /> Thêm vai trò</button> : null} />

    <section className="employee-summary-grid" aria-label="Tổng quan tài khoản">
      <article className="panel"><span className="employee-summary-icon"><Users size={20} /></span><div><small>Tổng tài khoản</small><strong>{employees.length}</strong></div></article>
      <article className="panel"><span className="employee-summary-icon success"><CheckCircle2 size={20} /></span><div><small>Đang hoạt động</small><strong>{activeCount}</strong></div></article>
      <article className="panel"><span className="employee-summary-icon danger"><LockKeyhole size={20} /></span><div><small>Đã khóa</small><strong>{disabledCount}</strong></div></article>
      <article className="panel"><span className="employee-summary-icon warning"><KeyRound size={20} /></span><div><small>Chờ đổi mật khẩu</small><strong>{pendingPasswordCount}</strong></div></article>
    </section>

    <nav className="employee-tabs">
      <button className={tab === 'employees' ? 'is-active' : ''} onClick={() => setTab('employees')}><Users size={17} /> Danh sách nhân viên</button>
      {hasPermission('roles.manage') && <button className={tab === 'roles' ? 'is-active' : ''} onClick={() => setTab('roles')}><ShieldCheck size={17} /> Vai trò & phân quyền</button>}
    </nav>

    {tab === 'employees' ? <section className="panel table-panel">
      <div className="table-toolbar employee-toolbar">
        <label className="search-box"><Search size={18} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên, email hoặc số điện thoại…" /></label>
        <div className="employee-filters">
          <select value={roleFilter} onChange={(event) => setRoleFilter(event.target.value)} aria-label="Lọc theo vai trò"><option value="all">Tất cả vai trò</option>{rolesQuery.data?.map((role) => <option key={role.id} value={role.name}>{displayRole(role.name)}</option>)}</select>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as EmployeeStatusFilter)} aria-label="Lọc theo trạng thái"><option value="all">Tất cả trạng thái</option><option value="active">Đang hoạt động</option><option value="inactive">Đã vô hiệu</option><option value="locked">Tạm khóa đăng nhập</option><option value="password">Chờ đổi mật khẩu</option></select>
          <span>{filteredEmployees.length}/{employees.length} tài khoản</span>
        </div>
      </div>
      {(employeesQuery.isError || statusMutation.isError) && <div className="inline-table-alert">{(statusMutation.error ?? employeesQuery.error)?.message}</div>}
      <div className="data-table-wrap"><table className="data-table"><thead><tr><th>Nhân viên</th><th>Điện thoại</th><th>Vai trò</th><th>Lần đăng nhập</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
        {filteredEmployees.map((employee) => {
          const locked = isLockedOut(employee)
          const isSelf = employee.id === user?.id
          return <tr key={employee.id}>
            <td><div className="person-cell"><span className="avatar light">{employee.fullName.slice(0, 1).toUpperCase()}</span><span><strong>{employee.fullName}{isSelf && <em className="self-label">Bạn</em>}</strong><small>{employee.email}</small></span></div></td>
            <td>{employee.phone || '—'}</td>
            <td><div className="employee-role-list">{employee.roles.length ? employee.roles.map((role) => <span key={role}>{displayRole(role)}</span>) : <small>Chưa phân vai trò</small>}</div></td>
            <td>{employee.lastLoginAtUtc ? new Date(employee.lastLoginAtUtc).toLocaleString('vi-VN') : 'Chưa đăng nhập'}</td>
            <td><div className="employee-status-stack">{!employee.isActive ? <StatusBadge tone="danger">Đã vô hiệu</StatusBadge> : locked ? <StatusBadge tone="warning">Tạm khóa</StatusBadge> : <StatusBadge tone="success">Hoạt động</StatusBadge>}{employee.mustChangePassword && <small>Chờ đổi mật khẩu</small>}</div></td>
            <td><div className="row-actions">{hasPermission('employees.manage') && <>
              <button className="icon-button" onClick={() => setEditor(employee)} aria-label={`Sửa ${employee.fullName}`} title="Chỉnh sửa"><Pencil size={17} /></button>
              <button className="icon-button" onClick={() => setResetTarget(employee)} disabled={isSelf} aria-label={`Đặt lại mật khẩu ${employee.fullName}`} title={isSelf ? 'Dùng menu tài khoản để đổi mật khẩu của bạn' : 'Đặt lại mật khẩu'}><KeyRound size={17} /></button>
              <button className="text-button" disabled={isSelf || statusMutation.isPending} onClick={() => statusMutation.mutate({ id: employee.id, isActive: !employee.isActive || locked })}>{employee.isActive && !locked ? 'Khóa' : 'Mở khóa'}</button>
            </>}</div></td>
          </tr>
        })}
        {!employeesQuery.isLoading && filteredEmployees.length === 0 && <tr><td colSpan={6} className="empty-cell"><UserRoundCheck /> Không có nhân viên phù hợp.</td></tr>}
      </tbody></table></div>
    </section> : <>
      {deleteRoleMutation.isError && <div className="form-alert role-page-alert">{deleteRoleMutation.error.message}</div>}
      <section className="role-cards-grid">
        {rolesQuery.data?.map((role) => <article className="panel role-card" key={role.id}>
          <div><span className="role-icon"><ShieldCheck size={19} /></span><span><strong>{displayRole(role.name)}</strong><small>{role.isSystem ? 'Vai trò hệ thống' : 'Vai trò tùy chỉnh'}</small></span></div>
          <div className="role-metrics"><span><strong>{role.userCount}</strong> nhân viên</span><span><strong>{role.permissions.length}</strong> quyền</span></div>
          <div className="role-permission-preview">{role.permissions.slice(0, 5).map((permission) => <span key={permission}>{permissionsQuery.data?.find((item) => item.key === permission)?.label ?? permission}</span>)}{role.permissions.length > 5 && <span>+{role.permissions.length - 5} quyền</span>}</div>
          <div className="role-card-actions"><button className="secondary-button" disabled={role.name === 'Administrator'} onClick={() => setRoleEditor(role)}><Pencil size={16} /> Chỉnh quyền</button>{!role.isSystem && <button className="icon-button danger" disabled={role.userCount > 0 || deleteRoleMutation.isPending} onClick={() => removeRole(role)} title={role.userCount > 0 ? 'Vai trò đang được sử dụng' : 'Xóa vai trò'}><Trash2 size={17} /></button>}</div>
        </article>)}
      </section>
    </>}

    {editor && <EmployeeEditorDialog employee={editor === 'new' ? undefined : editor} roles={rolesQuery.data ?? []} onClose={() => setEditor(null)} onSaved={() => { setEditor(null); refresh() }} />}
    {resetTarget && <ResetPasswordDialog employee={resetTarget} onClose={() => setResetTarget(null)} onSaved={() => { setResetTarget(null); refresh() }} />}
    {roleEditor && <RoleEditorDialog role={roleEditor === 'new' ? undefined : roleEditor} permissions={permissionsQuery.data ?? []} onClose={() => setRoleEditor(null)} onSaved={() => { setRoleEditor(null); refresh() }} />}
  </>
}

function EmployeeEditorDialog({ employee, roles, onClose, onSaved }: { employee?: Employee; roles: EmployeeRole[]; onClose: () => void; onSaved: () => void }) {
  const mutation = useMutation({
    mutationFn: (body: object) => employee ? api.put(`/api/employees/${employee.id}`, body) : api.post('/api/employees', body),
    onSuccess: onSaved,
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const body = { fullName: form.get('fullName'), email: form.get('email'), phone: form.get('phone') || null, role: form.get('role'), isActive: form.get('isActive') === 'on' }
    mutation.mutate(employee ? body : { ...body, initialPassword: form.get('password') })
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="modal-card employee-editor-dialog" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
    <div className="modal-head"><div><span className="eyebrow accent">HỒ SƠ NHÂN VIÊN</span><h2>{employee ? 'Chỉnh sửa nhân viên' : 'Thêm nhân viên'}</h2><p>Thông tin đăng nhập, liên hệ và vai trò sử dụng hệ thống.</p></div><button type="button" className="icon-button" onClick={onClose}><X size={20} /></button></div>
    {mutation.isError && <div className="form-alert">{mutation.error.message}</div>}
    <div className="form-grid">
      <label><span>Họ và tên *</span><input name="fullName" defaultValue={employee?.fullName} required /></label>
      <label><span>Email đăng nhập *</span><input name="email" type="email" autoComplete="off" defaultValue={employee?.email} required /></label>
      <label><span>Số điện thoại</span><input name="phone" inputMode="tel" defaultValue={employee?.phone} /></label>
      <label><span>Vai trò *</span><select name="role" defaultValue={employee?.roles[0] ?? (roles.some((role) => role.name === 'Sales') ? 'Sales' : roles[0]?.name)} required><option value="" disabled>Chọn vai trò</option>{roles.map((role) => <option value={role.name} key={role.id}>{displayRole(role.name)}</option>)}</select></label>
      {!employee && <label className="full"><span>Mật khẩu tạm *</span><input name="password" type="password" autoComplete="new-password" minLength={12} required /><small>Tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.</small></label>}
      <label className="toggle-row full"><span><strong>Tài khoản hoạt động</strong><small>Tài khoản bị khóa sẽ không thể đăng nhập.</small></span><input name="isActive" type="checkbox" defaultChecked={employee?.isActive ?? true} /></label>
    </div>
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Hủy</button><button className="primary-button" disabled={mutation.isPending || roles.length === 0}><Save size={17} /> {mutation.isPending ? 'Đang lưu…' : 'Lưu nhân viên'}</button></div>
  </form></div>
}

function ResetPasswordDialog({ employee, onClose, onSaved }: { employee: Employee; onClose: () => void; onSaved: () => void }) {
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [requireChange, setRequireChange] = useState(true)
  const [localError, setLocalError] = useState('')
  const mutation = useMutation({
    mutationFn: () => api.post(`/api/employees/${employee.id}/reset-password`, { newPassword: password, requireChangeOnNextLogin: requireChange }),
    onSuccess: onSaved,
  })
  function submit(event: FormEvent) {
    event.preventDefault()
    if (password !== confirmPassword) {
      setLocalError('Mật khẩu xác nhận không khớp.')
      return
    }
    setLocalError('')
    mutation.mutate()
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="modal-card reset-password-dialog" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
    <div className="modal-head"><div><span className="eyebrow accent">BẢO MẬT TÀI KHOẢN</span><h2>Đặt lại mật khẩu</h2><p>Tạo mật khẩu tạm mới cho {employee.fullName}.</p></div><button type="button" className="icon-button" onClick={onClose}><X size={20} /></button></div>
    {(localError || mutation.isError) && <div className="form-alert">{localError || mutation.error?.message}</div>}
    <div className="form-stack">
      <label><span>Mật khẩu tạm mới *</span><input type="password" autoComplete="new-password" minLength={12} value={password} onChange={(event) => setPassword(event.target.value)} required autoFocus /><small>Tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.</small></label>
      <label><span>Xác nhận mật khẩu *</span><input type="password" autoComplete="new-password" minLength={12} value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} required /></label>
      <label className="toggle-row"><span><strong>Yêu cầu đổi khi đăng nhập</strong><small>Nhân viên phải đặt mật khẩu riêng trước khi tiếp tục.</small></span><input type="checkbox" checked={requireChange} onChange={(event) => setRequireChange(event.target.checked)} /></label>
    </div>
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Hủy</button><button className="primary-button" disabled={mutation.isPending}><KeyRound size={17} /> {mutation.isPending ? 'Đang cập nhật…' : 'Đặt lại mật khẩu'}</button></div>
  </form></div>
}

function RoleEditorDialog({ role, permissions, onClose, onSaved }: { role?: EmployeeRole; permissions: PermissionItem[]; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(role?.name ?? '')
  const [selected, setSelected] = useState<string[]>(role?.permissions ?? [])
  const grouped = useMemo(() => Object.entries(permissions.reduce<Record<string, PermissionItem[]>>((result, item) => { (result[item.group] ??= []).push(item); return result }, {})), [permissions])
  const mutation = useMutation({ mutationFn: () => role ? api.put(`/api/employees/roles/${role.id}`, { name, permissions: selected }) : api.post('/api/employees/roles', { name, permissions: selected }), onSuccess: onSaved })
  function toggle(key: string, checked: boolean) { setSelected((current) => checked ? [...new Set([...current, key])] : current.filter((item) => item !== key)) }
  function toggleGroup(items: PermissionItem[]) {
    const allSelected = items.every((item) => selected.includes(item.key))
    setSelected((current) => allSelected ? current.filter((key) => !items.some((item) => item.key === key)) : [...new Set([...current, ...items.map((item) => item.key)])])
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="modal-card role-editor-dialog" onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} onMouseDown={(event) => event.stopPropagation()}>
    <div className="modal-head"><div><span className="eyebrow accent">VAI TRÒ & PHÂN QUYỀN</span><h2>{role ? `Chỉnh quyền ${displayRole(role.name)}` : 'Tạo vai trò mới'}</h2><p>Chọn các chức năng nhân viên được phép xem và thao tác.</p></div><button type="button" className="icon-button" onClick={onClose}><X size={20} /></button></div>
    {mutation.isError && <div className="form-alert">{mutation.error.message}</div>}
    <label className="role-name-field"><span>Tên vai trò *</span><input value={name} onChange={(event) => setName(event.target.value)} readOnly={role?.isSystem} required />{role?.isSystem && <small>Vai trò hệ thống không thể đổi tên.</small>}</label>
    <div className="permission-groups">{grouped.map(([group, items]) => {
      const allSelected = items.every((item) => selected.includes(item.key))
      return <section key={group}><div className="permission-group-head"><h3>{group}</h3><button type="button" onClick={() => toggleGroup(items)}>{allSelected ? 'Bỏ chọn' : 'Chọn tất cả'}</button></div>{items.map((permission) => <label key={permission.key}><input type="checkbox" checked={selected.includes(permission.key)} onChange={(event) => toggle(permission.key, event.target.checked)} /><span><strong>{permission.label}</strong><small>{permission.key}</small></span></label>)}</section>
    })}</div>
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Hủy</button><button className="primary-button" disabled={mutation.isPending}><Save size={17} /> {mutation.isPending ? 'Đang lưu…' : 'Lưu vai trò'}</button></div>
  </form></div>
}
