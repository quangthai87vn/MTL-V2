import { ArrowLeft, KeyRound, ShieldCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

export function ChangePasswordPage() {
  const { user, changePassword } = useAuth()
  const navigate = useNavigate()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (newPassword !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.')
      return
    }

    setSubmitting(true)
    setError('')
    try {
      await changePassword(currentPassword, newPassword)
      navigate('/', { replace: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không thể đổi mật khẩu.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="password-page">
      <form className="login-card" onSubmit={submit}>
        <div className="login-icon"><KeyRound size={24} /></div>
        <span className="eyebrow accent">BẢO MẬT TÀI KHOẢN</span>
        <h2>{user?.mustChangePassword ? 'Đổi mật khẩu lần đầu' : 'Đổi mật khẩu'}</h2>
        <p>{user?.mustChangePassword ? 'Hãy tạo mật khẩu riêng trước khi sử dụng hệ thống.' : 'Cập nhật mật khẩu định kỳ để bảo vệ tài khoản của bạn.'}</p>
        {error && <div className="form-alert">{error}</div>}
        <label><span>Mật khẩu hiện tại</span><input type="password" autoComplete="current-password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required autoFocus /></label>
        <label><span>Mật khẩu mới</span><input type="password" autoComplete="new-password" minLength={12} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required /></label>
        <label><span>Xác nhận mật khẩu mới</span><input type="password" autoComplete="new-password" minLength={12} value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} required /></label>
        <button className="primary-button login-submit" disabled={submitting}>{submitting ? 'Đang cập nhật…' : 'Đổi mật khẩu và tiếp tục'}</button>
        {!user?.mustChangePassword && <button type="button" className="password-back-button" onClick={() => navigate(-1)}><ArrowLeft size={16} /> Quay lại hệ thống</button>}
        <small className="security-note"><ShieldCheck size={13} /> Tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.</small>
      </form>
    </div>
  )
}

