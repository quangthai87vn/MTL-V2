import { Clock3, Eye, FileText, Pencil, Plus, Search, Users, X } from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { CustomerEditorDialog } from '../components/CustomerEditorDialog'
import { PageHeader } from '../components/PageHeader'
import { api } from '../lib/api'
import type { Customer, CustomerLookup, PagedResult } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export function CustomersPage() {
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [editCustomerId, setEditCustomerId] = useState<string | null>(null)
  const [selectedPhone, setSelectedPhone] = useState<string | null>(null)
  const { hasPermission } = useAuth()
  const queryClient = useQueryClient()
  const query = useQuery({ queryKey: ['customers', search], queryFn: () => api.get<PagedResult<Customer>>(`/api/customers?search=${encodeURIComponent(search)}&pageSize=50`) })

  return <>
    <PageHeader eyebrow="CRM" title="Khách hàng" description="Thông tin liên hệ, lịch sử mua và giá trị khách hàng." actions={hasPermission('customers.manage') && <button className="primary-button" onClick={() => setShowCreate(true)}><Plus size={18} /> Thêm khách hàng</button>} />
    <section className="panel table-panel">
      <div className="table-toolbar"><label className="search-box"><Search size={18} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên, số điện thoại, CCCD hoặc email…" /></label><span>{query.data?.total ?? 0} khách hàng</span></div>
      <div className="data-table-wrap"><table className="data-table"><thead><tr><th>Khách hàng</th><th>Liên hệ</th><th>CCCD</th><th>Tổng mua</th><th>Lần mua gần nhất</th><th>Thao tác</th></tr></thead><tbody>
        {query.data?.items.map((customer) => <tr key={customer.id}><td><div className="person-cell"><span className="avatar light">{customer.fullName.slice(0, 1)}</span><span><strong>{customer.fullName}</strong><small>{customer.code}</small></span></div></td><td><strong>{customer.phone || '—'}</strong><small className="table-subline">{customer.email || 'Chưa có email'}</small></td><td>{customer.citizenId || '—'}</td><td className="money">{currency.format(customer.totalSpent)}</td><td>{customer.lastPurchaseAtUtc ? new Date(customer.lastPurchaseAtUtc).toLocaleDateString('vi-VN') : 'Chưa mua hàng'}</td><td><div className="row-actions">{customer.phone && <button type="button" className="icon-button customer-history-button" onClick={() => setSelectedPhone(customer.phone ?? null)} aria-label="Xem lịch sử hóa đơn"><Eye size={17} /></button>}{hasPermission('customers.manage') && <button type="button" className="icon-button" onClick={() => setEditCustomerId(customer.id)} aria-label={`Sửa ${customer.fullName}`}><Pencil size={17} /></button>}</div></td></tr>)}
        {!query.isLoading && query.data?.items.length === 0 && <tr><td colSpan={6} className="empty-cell"><Users /> Chưa có khách hàng.</td></tr>}
      </tbody></table></div>
    </section>
    {showCreate && <CustomerEditorDialog onClose={() => setShowCreate(false)} onSaved={() => { setShowCreate(false); void queryClient.invalidateQueries({ queryKey: ['customers'] }) }} />}
    {editCustomerId && <CustomerEditorDialog customerId={editCustomerId} onClose={() => setEditCustomerId(null)} onSaved={() => { setEditCustomerId(null); void queryClient.invalidateQueries({ queryKey: ['customers'] }) }} />}
    {selectedPhone && <CustomerHistoryDialog phone={selectedPhone} onClose={() => setSelectedPhone(null)} />}
  </>
}

function CustomerHistoryDialog({ phone, onClose }: { phone: string; onClose: () => void }) {
  const query = useQuery({ queryKey: ['customer-lookup', phone], queryFn: () => api.get<CustomerLookup>(`/api/customers/lookup?phone=${encodeURIComponent(phone)}`) })
  const customer = query.data
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="modal-card customer-history-dialog" onMouseDown={(event) => event.stopPropagation()}>
    <div className="modal-head"><div><span className="eyebrow accent">LỊCH SỬ KHÁCH HÀNG</span><h2>{customer?.fullName ?? 'Đang tải…'}</h2>{customer && <p>{customer.code} · {customer.phone}</p>}</div><button type="button" className="icon-button" onClick={onClose} aria-label="Đóng"><X size={20} /></button></div>
    {query.isError && <div className="form-alert">{query.error.message}</div>}
    {customer && <>
      <div className="customer-history-summary"><article><small>Tổng giá trị giao dịch</small><strong>{currency.format(customer.totalSpent)}</strong></article><article><small>Số hóa đơn</small><strong>{customer.invoices.length}</strong></article><article><small>Lần mua gần nhất</small><strong>{customer.lastPurchaseAtUtc ? new Date(customer.lastPurchaseAtUtc).toLocaleDateString('vi-VN') : 'Chưa có'}</strong></article></div>
      <div className="customer-history-list"><h3><FileText size={18} /> Hóa đơn đã mua / sửa chữa</h3>{customer.invoices.map((invoice) => <article key={invoice.id}><span className="history-invoice-icon">{invoice.type === 'Sale' ? <FileText size={17} /> : <Clock3 size={17} />}</span><div><strong>{invoice.number}</strong><small>{invoice.type === 'Sale' ? 'Bán hàng' : 'Sửa chữa'} · {new Date(invoice.invoiceDateUtc).toLocaleString('vi-VN')}</small></div><span><strong>{currency.format(invoice.totalAmount)}</strong><small>{invoice.outstandingAmount > 0 ? `Còn nợ ${currency.format(invoice.outstandingAmount)}` : 'Đã thanh toán'}</small></span></article>)}{customer.invoices.length === 0 && <p className="management-empty">Khách hàng chưa có hóa đơn.</p>}</div>
    </>}
  </section></div>
}

