import { CalendarDays, FileText, Mail, MapPin, Phone, Save, UserRound, X } from 'lucide-react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { api } from '../lib/api'
import type { CustomerDetails } from '../types'

type CustomerDraft = {
  fullName: string
  phone: string
  email: string
  address: string
  citizenId: string
  taxCode: string
  birthday: string
  notes: string
}

const emptyDraft: CustomerDraft = { fullName: '', phone: '', email: '', address: '', citizenId: '', taxCode: '', birthday: '', notes: '' }

export function CustomerEditorDialog({
  customerId,
  initialValues,
  onClose,
  onSaved,
}: {
  customerId?: string
  initialValues?: Partial<CustomerDraft>
  onClose: () => void
  onSaved: (customer: CustomerDetails) => void
}) {
  const [draft, setDraft] = useState<CustomerDraft>({ ...emptyDraft, ...initialValues })
  const query = useQuery({
    queryKey: ['customer-details', customerId],
    queryFn: () => api.get<CustomerDetails>(`/api/customers/${customerId}`),
    enabled: Boolean(customerId),
  })
  /* eslint-disable react-hooks/set-state-in-effect -- hydrate the editable draft once the API record arrives */
  useEffect(() => {
    if (!query.data) return
    setDraft({
      fullName: query.data.fullName,
      phone: query.data.phone ?? '',
      email: query.data.email ?? '',
      address: query.data.address ?? '',
      citizenId: query.data.citizenId ?? '',
      taxCode: query.data.taxCode ?? '',
      birthday: query.data.birthday ?? '',
      notes: query.data.notes ?? '',
    })
  }, [query.data])
  /* eslint-enable react-hooks/set-state-in-effect */

  const mutation = useMutation({
    mutationFn: (body: object) => customerId
      ? api.put<CustomerDetails>(`/api/customers/${customerId}`, body)
      : api.post<CustomerDetails>('/api/customers', body),
    onSuccess: onSaved,
  })

  function change(field: keyof CustomerDraft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    mutation.mutate({
      fullName: draft.fullName,
      phone: draft.phone || null,
      email: draft.email || null,
      address: draft.address || null,
      citizenId: draft.citizenId || null,
      taxCode: draft.taxCode || null,
      birthday: draft.birthday || null,
      notes: draft.notes || null,
    })
  }

  return createPortal(<div className="modal-backdrop customer-editor-backdrop" onMouseDown={onClose}>
    <form className="modal-card customer-editor-dialog" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
      <div className="modal-head">
        <div><span className="eyebrow accent">HỒ SƠ KHÁCH HÀNG</span><h2>{customerId ? 'Chỉnh sửa khách hàng' : 'Thêm khách hàng'}</h2><p>Dữ liệu này được dùng chung tại menu Khách hàng và khi lập hóa đơn.</p></div>
        <button type="button" className="icon-button" onClick={onClose} aria-label="Đóng"><X size={20} /></button>
      </div>
      {(query.isError || mutation.isError) && <div className="form-alert">{(query.error ?? mutation.error)?.message}</div>}
      {query.isLoading ? <div className="dialog-loading">Đang tải hồ sơ khách hàng…</div> : <div className="customer-editor-fields">
        <label><span><UserRound size={15} /> Họ và tên *</span><input value={draft.fullName} onChange={(event) => change('fullName', event.target.value)} required /></label>
        <label><span><Phone size={15} /> Số điện thoại</span><input value={draft.phone} onChange={(event) => change('phone', event.target.value)} inputMode="tel" /></label>
        <label><span><Mail size={15} /> Email</span><input type="email" value={draft.email} onChange={(event) => change('email', event.target.value)} /></label>
        <label><span>CCCD</span><input value={draft.citizenId} onChange={(event) => change('citizenId', event.target.value)} inputMode="numeric" /></label>
        <label><span>Mã số thuế</span><input value={draft.taxCode} onChange={(event) => change('taxCode', event.target.value)} /></label>
        <label className="full"><span><MapPin size={15} /> Địa chỉ</span><input value={draft.address} onChange={(event) => change('address', event.target.value)} /></label>
        <label><span><CalendarDays size={15} /> Ngày sinh</span><input type="date" value={draft.birthday} onChange={(event) => change('birthday', event.target.value)} /></label>
        <label className="full"><span><FileText size={15} /> Thông tin phụ</span><textarea rows={3} value={draft.notes} onChange={(event) => change('notes', event.target.value)} placeholder="Nguồn khách, nhu cầu, ghi chú liên hệ…" /></label>
      </div>}
      <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Hủy</button><button className="primary-button" disabled={mutation.isPending || query.isLoading}><Save size={17} /> {mutation.isPending ? 'Đang lưu…' : 'Lưu khách hàng'}</button></div>
    </form>
  </div>, document.body)
}
