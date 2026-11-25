import { AlertTriangle, Building2, CalendarClock, CheckCircle2, ImagePlus, KeyRound, ReceiptText, RefreshCw, Save, Store, Upload } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { PageHeader } from '../components/PageHeader'
import { api } from '../lib/api'
import type { CompanySettings, InvoiceSettings } from '../types'

type WooConfiguration = {
  baseUrl?: string
  isEnabled: boolean
  hasCredentials: boolean
  credentialsReadable: boolean
  hasWebhookSecret: boolean
  webhookSecretReadable: boolean
  lastSuccessfulSyncAtUtc?: string
  lastError?: string
}

export function SettingsPage() {
  const [tab, setTab] = useState<'woo' | 'company' | 'invoice'>('woo')
  return <>
    <PageHeader eyebrow="HỆ THỐNG" title="Cấu hình" description="Kết nối dịch vụ và thông tin vận hành của doanh nghiệp." />
    <div className="settings-grid">
      <section className="panel settings-nav">
        <button className={tab === 'woo' ? 'is-active' : ''} onClick={() => setTab('woo')}><Store /> WooCommerce</button>
        <button className={tab === 'company' ? 'is-active' : ''} onClick={() => setTab('company')}><KeyRound /> Thông tin công ty</button>
        <button className={tab === 'invoice' ? 'is-active' : ''} onClick={() => setTab('invoice')}><ReceiptText /> Hóa đơn</button>
      </section>
      {tab === 'woo' ? <WooSettingsForm /> : tab === 'company' ? <CompanySettingsForm /> : <InvoiceSettingsForm />}
    </div>
  </>
}

function InvoiceSettingsForm() {
  const queryClient = useQueryClient()
  const query = useQuery({ queryKey: ['invoice-settings'], queryFn: () => api.get<InvoiceSettings>('/api/settings/invoice') })
  const [days, setDays] = useState<number | null>(null)
  const effectiveDays = days ?? query.data?.defaultRepairTurnaroundDays ?? 5
  const mutation = useMutation({
    mutationFn: () => api.put<InvoiceSettings>('/api/settings/invoice', { defaultRepairTurnaroundDays: effectiveDays }),
    onSuccess: (saved) => {
      setDays(saved.defaultRepairTurnaroundDays)
      void queryClient.invalidateQueries({ queryKey: ['invoice-settings'] })
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    mutation.mutate()
  }

  return <form className="panel settings-form invoice-settings-form" onSubmit={submit}>
    <div className="panel-heading"><div><span className="eyebrow">CẤU HÌNH HÓA ĐƠN</span><h2>Hóa đơn & sửa chữa</h2><p>Thiết lập thời gian hẹn trả mặc định cho phiếu tiếp nhận sửa chữa.</p></div><CalendarClock size={30} /></div>
    {(query.isError || mutation.isError) && <div className="form-alert">{(query.error ?? mutation.error)?.message}</div>}
    {mutation.isSuccess && <div className="form-success">Đã lưu cấu hình hóa đơn.</div>}
    <div className="invoice-setting-card">
      <label><span>Số ngày dự kiến trả máy sau khi tiếp nhận *</span><input type="number" min={1} max={90} step={1} value={effectiveDays} onChange={(event) => setDays(Math.min(90, Math.max(1, Number(event.target.value) || 1)))} required /></label>
      <div><CalendarClock size={20} /><span>Ví dụ: tiếp nhận ngày 21/11/2026 thì hệ thống tự hẹn trả ngày <strong>{new Date(2026, 10, 21 + effectiveDays).toLocaleDateString('vi-VN')}</strong>.</span></div>
    </div>
    <div className="modal-actions"><button className="primary-button" disabled={mutation.isPending || query.isLoading}><Save size={17} /> {mutation.isPending ? 'Đang lưu…' : 'Lưu cấu hình hóa đơn'}</button></div>
  </form>
}

function WooSettingsForm() {
  const queryClient = useQueryClient()
  const query = useQuery({ queryKey: ['woo-config'], queryFn: () => api.get<WooConfiguration>('/api/integrations/woocommerce/configuration') })
  const [baseUrl, setBaseUrl] = useState<string | null>(null)
  const [consumerKey, setConsumerKey] = useState('')
  const [consumerSecret, setConsumerSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')
  const [isEnabled, setIsEnabled] = useState<boolean | null>(null)
  const effectiveBaseUrl = baseUrl ?? query.data?.baseUrl ?? 'https://mytholaptop.vn'
  const effectiveIsEnabled = isEnabled ?? query.data?.isEnabled ?? true
  const credentialsNeedReset = query.data?.hasCredentials === true && query.data.credentialsReadable === false
  const saveMutation = useMutation({ mutationFn: () => api.put<WooConfiguration>('/api/integrations/woocommerce/configuration', { baseUrl: effectiveBaseUrl, consumerKey: consumerKey || null, consumerSecret: consumerSecret || null, webhookSecret: webhookSecret || null, isEnabled: effectiveIsEnabled }), onSuccess: () => { setConsumerKey(''); setConsumerSecret(''); setWebhookSecret(''); setBaseUrl(null); setIsEnabled(null); void queryClient.invalidateQueries({ queryKey: ['woo-config'] }) } })
  const testMutation = useMutation({ mutationFn: () => api.post('/api/integrations/woocommerce/test') })
  function submit(event: FormEvent) { event.preventDefault(); saveMutation.mutate() }

  return <form className="panel settings-form" onSubmit={submit}>
    <div className="panel-heading"><div><span className="eyebrow">TÍCH HỢP WEBSITE</span><h2>WooCommerce</h2><p>Credential được mã hóa trước khi lưu vào PostgreSQL.</p></div>{query.data?.credentialsReadable ? <span className="status-badge success"><CheckCircle2 size={14} /> Đã cấu hình</span> : credentialsNeedReset ? <span className="status-badge warning"><AlertTriangle size={14} /> Cần nhập lại khóa</span> : null}</div>
    {(saveMutation.isError || testMutation.isError) && <div className="form-alert">{(saveMutation.error ?? testMutation.error)?.message}</div>}
    {credentialsNeedReset && <div className="form-alert">Bộ khóa bảo mật trên máy chủ không khớp với database. Hãy nhập lại đầy đủ Consumer Key và Consumer Secret, hoặc khôi phục bộ khóa Data Protection đã dùng khi xuất database.</div>}
    {query.data?.hasWebhookSecret && !query.data.webhookSecretReadable && <div className="form-alert">Webhook Secret hiện tại cũng không thể giải mã. Hãy nhập lại nếu website đang dùng webhook thời gian thực.</div>}
    {(saveMutation.isSuccess || testMutation.isSuccess) && <div className="form-success">Thao tác thành công.</div>}
    <div className="form-stack">
      <label><span>Website URL</span><input value={effectiveBaseUrl} onChange={(event) => setBaseUrl(event.target.value)} type="url" required /></label>
      <div className="form-grid"><label><span>Consumer Key</span><input value={consumerKey} onChange={(event) => setConsumerKey(event.target.value)} type="password" placeholder={query.data?.credentialsReadable ? 'Để trống nếu không đổi' : 'Nhập lại ck_…'} required={query.data ? !query.data.credentialsReadable : false} /></label><label><span>Consumer Secret</span><input value={consumerSecret} onChange={(event) => setConsumerSecret(event.target.value)} type="password" placeholder={query.data?.credentialsReadable ? 'Để trống nếu không đổi' : 'Nhập lại cs_…'} required={query.data ? !query.data.credentialsReadable : false} /></label></div>
      <label><span>Webhook Secret</span><input value={webhookSecret} onChange={(event) => setWebhookSecret(event.target.value)} type="password" placeholder={query.data?.hasWebhookSecret ? 'Để trống nếu không đổi' : 'Chỉ cần khi dùng webhook thời gian thực'} /></label>
      <label className="toggle-row"><span><strong>Bật đồng bộ WooCommerce</strong><small>Cho phép hệ thống đọc và cập nhật sản phẩm website.</small></span><input type="checkbox" checked={effectiveIsEnabled} onChange={(event) => setIsEnabled(event.target.checked)} /></label>
    </div>
    {query.data?.lastSuccessfulSyncAtUtc && <p className="last-sync">Đồng bộ thành công gần nhất: {new Date(query.data.lastSuccessfulSyncAtUtc).toLocaleString('vi-VN')}</p>}
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={() => testMutation.mutate()} disabled={!query.data?.credentialsReadable || testMutation.isPending}><RefreshCw size={17} /> Kiểm tra kết nối</button><button className="primary-button" disabled={saveMutation.isPending}><Save size={17} /> {saveMutation.isPending ? 'Đang lưu…' : 'Lưu cấu hình'}</button></div>
  </form>
}

function CompanySettingsForm() {
  const queryClient = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const query = useQuery({ queryKey: ['company-settings'], queryFn: () => api.get<CompanySettings>('/api/settings/company') })
  const [draft, setDraft] = useState<CompanySettings>({ name: '' })
  const [fileError, setFileError] = useState('')
  /* eslint-disable react-hooks/set-state-in-effect -- initialize the company form from persisted settings */
  useEffect(() => { if (query.data) setDraft(query.data) }, [query.data])
  /* eslint-enable react-hooks/set-state-in-effect */
  const mutation = useMutation({
    mutationFn: () => api.put<CompanySettings>('/api/settings/company', draft),
    onSuccess: (saved) => { setDraft(saved); void queryClient.invalidateQueries({ queryKey: ['company-settings'] }) },
  })
  function change(field: keyof CompanySettings, value: string) { setDraft((current) => ({ ...current, [field]: value })) }
  function uploadLogo(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return
    setFileError('')
    if (!file.type.startsWith('image/')) { setFileError('Vui lòng chọn tập tin hình ảnh.'); return }
    if (file.size > 2 * 1024 * 1024) { setFileError('Logo không được lớn hơn 2 MB.'); return }
    const reader = new FileReader()
    reader.onload = () => setDraft((current) => ({ ...current, logoDataUrl: String(reader.result) }))
    reader.readAsDataURL(file)
  }
  function submit(event: FormEvent) { event.preventDefault(); mutation.mutate() }

  return <form className="panel settings-form company-settings-form" onSubmit={submit}>
    <div className="panel-heading"><div><span className="eyebrow">THÔNG TIN XUẤT HÓA ĐƠN</span><h2>Thông tin công ty</h2><p>Các thông tin này được tự động đưa lên hóa đơn A4 và bill 80mm.</p></div><Building2 size={30} /></div>
    {(query.isError || mutation.isError || fileError) && <div className="form-alert">{fileError || (query.error ?? mutation.error)?.message}</div>}
    {mutation.isSuccess && <div className="form-success">Đã lưu thông tin công ty.</div>}
    <div className="company-settings-layout">
      <div className="form-stack">
        <label><span>Tên công ty *</span><input value={draft.name} onChange={(event) => change('name', event.target.value)} required /></label>
        <div className="form-grid"><label><span>Số điện thoại</span><input value={draft.phone ?? ''} onChange={(event) => change('phone', event.target.value)} /></label><label><span>Email</span><input type="email" value={draft.email ?? ''} onChange={(event) => change('email', event.target.value)} /></label></div>
        <label><span>Địa chỉ</span><input value={draft.address ?? ''} onChange={(event) => change('address', event.target.value)} /></label>
        <div className="form-grid"><label><span>Mã số thuế</span><input value={draft.taxCode ?? ''} onChange={(event) => change('taxCode', event.target.value)} /></label><label><span>Website</span><input value={draft.website ?? ''} onChange={(event) => change('website', event.target.value)} placeholder="https://mytholaptop.vn" /></label></div>
      </div>
      <aside className="company-logo-card">
        <span>Logo công ty</span>
        <div className="company-logo-preview">{draft.logoDataUrl ? <img src={draft.logoDataUrl} alt="Logo công ty" /> : <ImagePlus size={38} />}</div>
        <input ref={fileRef} type="file" accept="image/*" onChange={uploadLogo} hidden />
        <button type="button" className="secondary-button" onClick={() => fileRef.current?.click()}><Upload size={17} /> Chọn logo</button>
        {draft.logoDataUrl && <button type="button" className="text-button danger" onClick={() => setDraft((current) => ({ ...current, logoDataUrl: undefined }))}>Xóa logo</button>}
        <small>PNG/JPG/WebP, tối đa 2 MB.</small>
      </aside>
    </div>
    <div className="modal-actions"><button className="primary-button" disabled={mutation.isPending || query.isLoading}><Save size={17} /> {mutation.isPending ? 'Đang lưu…' : 'Lưu thông tin công ty'}</button></div>
  </form>
}
