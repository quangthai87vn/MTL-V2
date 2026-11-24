import { ArrowRight, CheckCircle2, LockKeyhole, PackageCheck, ShieldCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthProvider'

export function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      await login(email, password, rememberMe)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không thể đăng nhập.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <section className="login-story">
        <div className="login-brand"><span>MTL</span> MỸ THO LAPTOP</div>
        <div className="story-content">
          <span className="story-kicker">MTL BUSINESS OS</span>
          <h1>Một nơi để vận hành toàn bộ cửa hàng.</h1>
          <p>Sản phẩm, kho, bán hàng và khách hàng được kết nối với website mytholaptop.vn trong cùng một luồng dữ liệu.</p>
          <div className="story-points">
            <div><PackageCheck /><span><strong>Đồng bộ WooCommerce</strong><small>Theo dõi từng lần cập nhật</small></span></div>
            <div><ShieldCheck /><span><strong>Phân quyền rõ ràng</strong><small>Đúng người, đúng chức năng</small></span></div>
            <div><CheckCircle2 /><span><strong>Dữ liệu có lịch sử</strong><small>Audit, serial và tồn kho minh bạch</small></span></div>
          </div>
        </div>
        <p className="story-footer">Tận tâm · Uy tín · Chất lượng</p>
      </section>

      <section className="login-panel">
        <form className="login-card" onSubmit={handleSubmit}>
          <div className="login-icon"><LockKeyhole size={24} /></div>
          <span className="eyebrow accent">Chào mừng trở lại</span>
          <h2>Đăng nhập hệ thống</h2>
          <p>Sử dụng tài khoản nhân viên được cấp bởi quản trị viên.</p>

          {error && <div className="form-alert">{error}</div>}

          <label>
            <span>Email</span>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="ten@mytholaptop.vn" required autoFocus />
          </label>
          <label>
            <span>Mật khẩu</span>
            <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Tối thiểu 12 ký tự" required />
          </label>
          <label className="checkbox-label">
            <input type="checkbox" checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} />
            <span>Duy trì đăng nhập trên thiết bị này</span>
          </label>
          <button className="primary-button login-submit" disabled={submitting}>
            {submitting ? 'Đang xác thực…' : 'Đăng nhập'} <ArrowRight size={18} />
          </button>
          <small className="security-note">Phiên làm việc được bảo vệ bằng cookie HttpOnly và CSRF token.</small>
        </form>
      </section>
    </div>
  )
}

