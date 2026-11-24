import { Boxes, CalendarDays, CircleDollarSign, Clock3, FileInput, Pencil, Plus, Search, Trash2, Truck } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useDeferredValue, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { PageHeader } from '../components/PageHeader'
import { PurchaseInvoiceDialog } from '../components/PurchaseInvoiceDialog'
import { StatusBadge } from '../components/StatusBadge'
import { api } from '../lib/api'
import type { PagedResult, PurchaseInvoice, PurchaseStatus, PurchaseSummary } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const date = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })

export function PurchasesPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [status, setStatus] = useState('')
  const [paymentStatus, setPaymentStatus] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [editId, setEditId] = useState<string | null>(null)
  const { hasPermission } = useAuth()
  const queryClient = useQueryClient()
  const summaryQuery = useQuery({ queryKey: ['purchases-summary'], queryFn: () => api.get<PurchaseSummary>('/api/purchases/summary') })
  const listQuery = useQuery({
    queryKey: ['purchases', deferredSearch, status, paymentStatus],
    queryFn: () => {
      const params = new URLSearchParams({ page: '1', pageSize: '100' })
      if (deferredSearch) params.set('search', deferredSearch)
      if (status) params.set('status', status)
      if (paymentStatus) params.set('paymentStatus', paymentStatus)
      return api.get<PagedResult<PurchaseInvoice>>(`/api/purchases?${params}`)
    },
  })
  const deleteMutation = useMutation({ mutationFn: (id: string) => api.delete(`/api/purchases/${id}`), onSuccess: () => refresh() })

  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ['purchases'] })
    void queryClient.invalidateQueries({ queryKey: ['purchases-summary'] })
    void queryClient.invalidateQueries({ queryKey: ['products'] })
  }
  function saved() { setShowCreate(false); setEditId(null); refresh() }
  function remove(invoice: PurchaseInvoice) {
    if (!window.confirm(`Xóa phiếu nhập ${invoice.number}? Nếu phiếu đã cộng kho, hệ thống chỉ cho xóa khi hàng và serial chưa phát sinh bán ra.`)) return
    deleteMutation.mutate(invoice.id)
  }
  const summary = summaryQuery.data

  return <>
    <PageHeader eyebrow="KHO & MUA HÀNG" title="NHẬP HÀNG" description="Quản lý chứng từ nhà cung cấp, nhận kho, serial, thuế GTGT, thanh toán và công nợ." actions={hasPermission('purchases.manage') ? <button className="primary-button" onClick={() => setShowCreate(true)}><Plus size={18} /> Tạo phiếu nhập</button> : undefined} />
    {(summaryQuery.isError || listQuery.isError || deleteMutation.isError) && <div className="form-alert">{(summaryQuery.error ?? listQuery.error ?? deleteMutation.error)?.message}</div>}

    <section className="purchase-metrics">
      <article><span className="purchase-metric-icon red"><CircleDollarSign size={22} /></span><div><small>Giá trị nhập đã xử lý</small><strong>{currency.format(summary?.processedValue ?? 0)}</strong><p>Chỉ tính phiếu đã xử lý</p></div></article>
      <article><span className="purchase-metric-icon amber"><Clock3 size={22} /></span><div><small>Công nợ nhà cung cấp</small><strong>{currency.format(summary?.supplierDebt ?? 0)}</strong><p>Giá trị đã nhập chưa thanh toán</p></div></article>
      <article><span className="purchase-metric-icon green"><Boxes size={22} /></span><div><small>Nhận kho hôm nay</small><strong>{currency.format(summary?.receivedToday ?? 0)}</strong><p>Phiếu đã xử lý trong ngày</p></div></article>
      <article><span className="purchase-metric-icon blue"><FileInput size={22} /></span><div><small>Phiếu đang xử lý</small><strong>{summary?.inProgressCount ?? 0}</strong><p>Chưa cập nhật tồn kho</p></div></article>
    </section>

    <section className="panel purchase-browser">
      <div className="purchase-toolbar">
        <label className="search-box"><Search size={18} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Số phiếu, nhà cung cấp, hóa đơn hoặc hàng hóa…" /></label>
        <select value={status} onChange={(e) => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option><option value="InProgress">Đang xử lý</option><option value="Processed">Đã xử lý</option><option value="Cancelled">Đã hủy</option></select>
        <select value={paymentStatus} onChange={(e) => setPaymentStatus(e.target.value)}><option value="">Tất cả thanh toán</option><option value="unpaid">Chưa thanh toán</option><option value="partial">Thanh toán một phần</option><option value="paid">Đã thanh toán</option></select>
        <span><strong>{listQuery.data?.total ?? 0}</strong> phiếu nhập</span>
      </div>
      <div className="data-table-wrap">
        <table className="data-table purchase-table">
          <thead><tr><th>Phiếu nhập</th><th>Nhà cung cấp</th><th>Chứng từ NCC</th><th>Ngày nhận</th><th>Hàng hóa</th><th>Trạng thái</th><th>Tổng tiền</th><th>Đã trả</th><th>Còn nợ</th><th>Thao tác</th></tr></thead>
          <tbody>
            {listQuery.isLoading && <tr><td colSpan={10} className="empty-cell">Đang tải phiếu nhập…</td></tr>}
            {listQuery.data?.items.map((invoice) => <tr key={invoice.id} className={`purchase-row is-${invoice.status.toLowerCase()}`} onClick={() => setEditId(invoice.id)}>
              <td><strong>{invoice.number}</strong><small><Truck size={13} /> {invoice.warehouseReceiptNumber || 'Chưa có số nhập kho'}</small></td>
              <td><strong>{invoice.supplierName}</strong><small>{invoice.supplierTaxCode ? `MST ${invoice.supplierTaxCode}` : 'Chưa có mã số thuế'}</small></td>
              <td><strong>{[invoice.supplierInvoiceSeries, invoice.supplierInvoiceNumber].filter(Boolean).join(' · ') || 'Chưa nhập'}</strong><small><CalendarDays size={13} /> {date.format(new Date(invoice.invoiceDateUtc))}</small></td>
              <td>{date.format(new Date(invoice.receivedAtUtc))}</td>
              <td><strong>{invoice.lineCount} dòng · {invoice.receivedQuantity} sản phẩm</strong><small>{invoice.inventoryPosted ? 'Đã cập nhật kho / serial' : 'Chưa cập nhật kho'}</small></td>
              <td><StatusBadge tone={statusTone(invoice.status)}>{statusLabel(invoice.status)}</StatusBadge></td>
              <td className="money">{currency.format(invoice.totalAmount)}</td>
              <td className="collected-money">{currency.format(invoice.paidAmount)}</td>
              <td className={invoice.outstandingAmount > 0 ? 'outstanding-money' : 'muted-money'}>{currency.format(invoice.outstandingAmount)}</td>
              <td><div className="invoice-row-actions">{hasPermission('purchases.manage') && <button className="icon-button" type="button" onClick={(e) => { e.stopPropagation(); setEditId(invoice.id) }} title="Sửa phiếu"><Pencil size={16} /></button>}{hasPermission('purchases.manage') && <button className="icon-button danger" type="button" onClick={(e) => { e.stopPropagation(); remove(invoice) }} title="Xóa phiếu"><Trash2 size={16} /></button>}</div></td>
            </tr>)}
            {!listQuery.isLoading && listQuery.data?.items.length === 0 && <tr><td colSpan={10} className="empty-cell">Chưa có phiếu nhập phù hợp.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
    {showCreate && <PurchaseInvoiceDialog onClose={() => setShowCreate(false)} onSaved={saved} />}
    {editId && <PurchaseInvoiceDialog invoiceId={editId} onClose={() => setEditId(null)} onSaved={saved} />}
  </>
}

function statusLabel(status: PurchaseStatus) { return ({ InProgress: 'Đang xử lý', Processed: 'Đã xử lý', Cancelled: 'Đã hủy' })[status] }
function statusTone(status: PurchaseStatus): 'success' | 'warning' | 'danger' { return status === 'Processed' ? 'success' : status === 'Cancelled' ? 'danger' : 'warning' }
