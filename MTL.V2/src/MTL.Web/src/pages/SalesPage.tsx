import {
  Banknote,
  CalendarClock,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleDollarSign,
  Clock3,
  CreditCard,
  FileText,
  PackageCheck,
  Pencil,
  Plus,
  Printer,
  Search,
  Settings2,
  Trash2,
  Wrench,
} from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useDeferredValue, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { PageHeader } from '../components/PageHeader'
import { SalesInvoiceDetailsDialog } from '../components/SalesInvoiceDetailsDialog'
import { SalesInvoiceDialog } from '../components/SalesInvoiceDialog'
import { SalesManagementDialog } from '../components/SalesManagementDialog'
import { StatusBadge } from '../components/StatusBadge'
import { api } from '../lib/api'
import type { PagedResult, RepairStatus, SalesInvoice, SalesSummary, ServiceType } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const dateTime = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function SalesPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [type, setType] = useState('')
  const [serviceTypeId, setServiceTypeId] = useState('')
  const [repairProgress, setRepairProgress] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [showCreate, setShowCreate] = useState(false)
  const [showManagement, setShowManagement] = useState(false)
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string | null>(null)
  const [editInvoiceId, setEditInvoiceId] = useState<string | null>(null)
  const [printInvoiceId, setPrintInvoiceId] = useState<string | null>(null)
  const { hasPermission } = useAuth()
  const queryClient = useQueryClient()

  const summaryQuery = useQuery({ queryKey: ['sales-summary'], queryFn: () => api.get<SalesSummary>('/api/sales/summary') })
  const serviceTypesQuery = useQuery({ queryKey: ['sales-service-types'], queryFn: () => api.get<ServiceType[]>('/api/sales/service-types') })
  const invoicesQuery = useQuery({
    queryKey: ['sales-invoices', deferredSearch, type, serviceTypeId, repairProgress, page, pageSize],
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (deferredSearch) params.set('search', deferredSearch)
      if (type) params.set('type', type)
      if (serviceTypeId) params.set('serviceTypeId', serviceTypeId)
      if (repairProgress) params.set('repairProgress', repairProgress)
      return api.get<PagedResult<SalesInvoice>>(`/api/sales?${params.toString()}`)
    },
    placeholderData: (previous) => previous,
  })
  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/api/sales/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['sales-summary'] })
      void queryClient.invalidateQueries({ queryKey: ['customers'] })
    },
  })

  const summary = summaryQuery.data
  const total = invoicesQuery.data?.total ?? 0
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const firstItem = total === 0 ? 0 : (page - 1) * pageSize + 1
  const lastItem = Math.min(page * pageSize, total)

  function invoiceCreated(id: string) {
    setShowCreate(false)
    setEditInvoiceId(null)
    setPage(1)
    setSelectedInvoiceId(id)
    void queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
    void queryClient.invalidateQueries({ queryKey: ['sales-summary'] })
    void queryClient.invalidateQueries({ queryKey: ['customers'] })
  }

  function deleteInvoice(invoice: SalesInvoice) {
    if (!window.confirm(`Xóa hóa đơn ${invoice.number}? Tồn kho, doanh số khách hàng và lượt dùng voucher sẽ được hoàn lại.`)) return
    deleteMutation.mutate(invoice.id)
  }

  return <>
    <PageHeader
      eyebrow="GIAO DỊCH"
      title="HÓA ĐƠN BÁN HÀNG"
      description="Tạo hóa đơn, theo dõi nguồn tiền, công nợ trả góp và phiếu tiếp nhận sửa chữa."
      actions={<>
        {hasPermission('sales.manage') && <button className="secondary-button" onClick={() => setShowManagement(true)}><Settings2 size={18} /> Danh mục bán hàng</button>}
        {hasPermission('sales.manage') && <button className="primary-button" onClick={() => setShowCreate(true)}><Plus size={18} /> Tạo hóa đơn</button>}
      </>}
    />

    {(summaryQuery.isError || invoicesQuery.isError || deleteMutation.isError) && <div className="form-alert">{(summaryQuery.error ?? invoicesQuery.error ?? deleteMutation.error)?.message}</div>}

    <section className="sales-metrics">
      <article><span className="sales-metric-icon red"><CircleDollarSign size={22} /></span><div><small>Giá trị đã xử lý hôm nay</small><strong>{currency.format(summary?.invoiceTotalToday ?? 0)}</strong><p>Giao dịch đã hoàn tất xử lý</p></div></article>
      <article><span className="sales-metric-icon green"><Banknote size={22} /></span><div><small>Nguồn tiền đã ghi nhận</small><strong>{currency.format(summary?.collectedToday ?? 0)}</strong><p>Tiền mặt và banking của phiếu đã xử lý</p></div></article>
      <article><span className="sales-metric-icon amber"><Clock3 size={22} /></span><div><small>Công nợ cần thu</small><strong>{currency.format(summary?.outstanding ?? 0)}</strong><p>Khoản góp và hóa đơn chưa đủ tiền</p></div></article>
      <article><span className="sales-metric-icon blue"><Wrench size={22} /></span><div><small>Máy chưa xử lý xong</small><strong>{summary?.activeRepairs ?? 0}</strong><p>Thuộc các phiếu đang xử lý</p></div></article>
    </section>

    <section className="panel sales-browser">
      <div className="sales-toolbar">
        <label className="search-box sales-search"><Search size={18} /><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1) }} placeholder="Số hóa đơn, khách hàng hoặc nội dung công việc…" /></label>
        <label className="sales-filter"><select value={type} onChange={(event) => { setType(event.target.value); setPage(1) }}><option value="">Tất cả nghiệp vụ</option><option value="Sale">Bán hàng</option><option value="Repair">Sửa chữa</option></select><ChevronDown size={15} /></label>
        <label className="sales-filter"><select value={serviceTypeId} onChange={(event) => { setServiceTypeId(event.target.value); setPage(1) }}><option value="">Tất cả loại dịch vụ</option>{serviceTypesQuery.data?.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select><ChevronDown size={15} /></label>
        <label className="sales-filter"><select value={repairProgress} onChange={(event) => { setRepairProgress(event.target.value); setPage(1) }}><option value="">Tất cả trạng thái xử lý</option><option value="pending">Đang xử lý</option><option value="processed">Đã xử lý</option></select><ChevronDown size={15} /></label>
        <span className="sales-total"><strong>{invoicesQuery.data?.total ?? 0}</strong> hóa đơn</span>
      </div>

      <div className="data-table-wrap sales-table-wrap">
        <table className="data-table sales-table">
          <thead><tr><th>Hóa đơn</th><th>Khách hàng</th><th>Nội dung / ghi chú</th><th>Nghiệp vụ</th><th>Trạng thái xử lý</th><th>Tổng tiền</th><th>Đã ghi nhận</th><th>Còn lại</th><th>Nguồn tiền</th><th>Người lập phiếu</th><th>Ngày lập phiếu</th><th>Thao tác</th></tr></thead>
          <tbody>
            {invoicesQuery.isLoading && <tr><td colSpan={12} className="empty-cell">Đang tải hóa đơn…</td></tr>}
            {invoicesQuery.data?.items.map((invoice) => <tr key={invoice.id} className={`sales-invoice-row ${invoice.repairStatus === 'Returned' ? 'repair-is-returned' : invoice.repairStatus === 'ReadyForReturn' ? 'repair-is-ready' : 'repair-is-pending'}`} onClick={() => setSelectedInvoiceId(invoice.id)}>
              <td><button type="button" className="invoice-number" onClick={() => setSelectedInvoiceId(invoice.id)}><FileText size={16} /><span><strong>{invoice.number}</strong><small>{invoice.type === 'Repair' && invoice.repairDeviceModel ? `${invoice.repairDeviceModel}${invoice.repairDeviceCount > 1 ? ` + ${invoice.repairDeviceCount - 1} máy` : ''}` : invoice.isInstallment ? 'Bán trả góp' : 'Bán trực tiếp'}</small></span></button></td>
              <td><div className="sales-customer-cell"><strong>{invoice.customerName}</strong><span>{invoice.customerPhone || 'Chưa có số điện thoại'}</span><small>{invoice.customerInvoiceCount || 0} đơn · Tổng mua {currency.format(invoice.customerTotalSpent || 0)}</small></div></td>
              <td className="invoice-purpose-cell"><strong>{invoice.notes || (invoice.type === 'Repair' ? `Sửa chữa ${invoice.repairDeviceModel || 'máy khách'}` : 'Bán sản phẩm')}</strong><small>{invoice.type === 'Repair' ? 'Nội dung tiếp nhận / sửa chữa' : 'Nội dung đơn hàng'}</small></td>
              <td><div className="invoice-service-cell">{invoice.type === 'Sale' ? <span className={`invoice-type sale ${invoice.isInstallment ? 'installment' : ''}`}><PackageCheck size={14} /> {invoice.isInstallment ? 'Bán trả góp' : 'Bán trực tiếp'}</span> : <span className="invoice-type repair"><Wrench size={14} /> Sửa chữa</span>}<div className="invoice-service-tags">{invoice.serviceTypes?.map((item) => <span key={item.id}>{item.name}</span>)}</div></div></td>
              <td><div className="repair-progress-cell"><StatusBadge tone={processingTone(invoice.repairStatus)}>{processingLabel(invoice.repairStatus, invoice.type)}</StatusBadge>{invoice.type === 'Repair' && <small>{invoice.repairDeviceCount} máy trong phiếu</small>}</div></td>
              <td className="money">{currency.format(invoice.totalAmount)}</td>
              <td className="collected-money">{invoice.isInstallment ? <span className="installment-money-breakdown"><strong>{currency.format(invoice.paidAmount)}</strong><small>{currency.format(invoice.receivedAmount)} tiền mặt + {currency.format(invoice.scheduledAmount)} banking</small></span> : currency.format(invoice.paidAmount)}</td>
              <td className={invoice.outstandingAmount > 0 ? 'outstanding-money' : 'muted-money'}>{currency.format(invoice.outstandingAmount)}</td>
              <td><StatusBadge tone={processingTone(invoice.repairStatus)}>{invoice.repairStatus === 'Returned' ? 'Đã ghi nhận' : invoice.repairStatus === 'Cancelled' ? 'Đã hủy' : 'Chờ xử lý'}</StatusBadge>{invoice.paymentDueDateUtc && invoice.isInstallment ? <small className="due-date"><CalendarClock size={12} /> Banking {new Date(invoice.paymentDueDateUtc).toLocaleDateString('vi-VN')}</small> : null}</td>
              <td><span className="invoice-creator">{invoice.createdByName || 'Dữ liệu cũ'}</span></td>
              <td><span className="date-cell">{dateTime.format(new Date(invoice.invoiceDateUtc))}</span></td>
              <td><div className="invoice-row-actions">{hasPermission('sales.manage') && <button type="button" className="icon-button" onClick={(event) => { event.stopPropagation(); setEditInvoiceId(invoice.id) }} aria-label={`Sửa ${invoice.number}`} title="Sửa hóa đơn"><Pencil size={16} /></button>}<button type="button" className="icon-button" onClick={(event) => { event.stopPropagation(); setPrintInvoiceId(invoice.id) }} aria-label={`In ${invoice.number}`} title="In hóa đơn"><Printer size={16} /></button>{hasPermission('sales.manage') && <button type="button" className="icon-button danger" onClick={(event) => { event.stopPropagation(); deleteInvoice(invoice) }} aria-label={`Xóa ${invoice.number}`} title="Xóa hóa đơn" disabled={deleteMutation.isPending}><Trash2 size={16} /></button>}</div></td>
            </tr>)}
            {!invoicesQuery.isLoading && invoicesQuery.data?.items.length === 0 && <tr><td colSpan={12} className="empty-cell"><CreditCard size={22} /> Chưa có hóa đơn phù hợp.</td></tr>}
          </tbody>
        </table>
      </div>
      <div className="pagination-bar">
        <div className="pagination-summary">
          <span>Hiển thị <strong>{firstItem}–{lastItem}</strong> trên <strong>{total.toLocaleString('vi-VN')}</strong></span>
          <label><span>Mỗi trang</span><select value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1) }}><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></label>
        </div>
        <nav className="pagination" aria-label="Phân trang hóa đơn">
          <button onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1} aria-label="Trang trước"><ChevronLeft size={18} /></button>
          {paginationItems(page, pageCount).map((item, index) => item === '…' ? <span key={`ellipsis-${index}`}>…</span> : <button key={item} className={item === page ? 'is-active' : ''} onClick={() => setPage(item)} aria-current={item === page ? 'page' : undefined}>{item}</button>)}
          <button onClick={() => setPage((value) => Math.min(pageCount, value + 1))} disabled={page >= pageCount} aria-label="Trang sau"><ChevronRight size={18} /></button>
        </nav>
      </div>
    </section>

    {showCreate && <SalesInvoiceDialog onClose={() => setShowCreate(false)} onCreated={invoiceCreated} />}
    {editInvoiceId && <SalesInvoiceDialog invoiceId={editInvoiceId} onClose={() => setEditInvoiceId(null)} onCreated={invoiceCreated} />}
    {showManagement && <SalesManagementDialog onClose={() => setShowManagement(false)} />}
    {selectedInvoiceId && <SalesInvoiceDetailsDialog invoiceId={selectedInvoiceId} onClose={() => setSelectedInvoiceId(null)} onChanged={() => {
      void queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['sales-summary'] })
    }} />}
    {printInvoiceId && <SalesInvoiceDetailsDialog invoiceId={printInvoiceId} autoPrintMode="a4" onClose={() => setPrintInvoiceId(null)} onChanged={() => { void queryClient.invalidateQueries({ queryKey: ['sales-invoices'] }) }} />}
  </>
}

function processingLabel(status: RepairStatus | undefined, _type: SalesInvoice['type']) {
  if (status === 'Returned') return 'Đã xử lý'
  if (status === 'Cancelled') return 'Đã hủy'
  return 'Đang xử lý'
}

function processingTone(status?: RepairStatus): 'success' | 'danger' | 'warning' | 'neutral' {
  if (status === 'Returned') return 'success'
  if (status === 'Cancelled') return 'danger'
  if (status === 'ReadyForReturn') return 'neutral'
  if (status) return 'warning'
  return 'neutral'
}

function paginationItems(current: number, total: number): Array<number | '…'> {
  if (total <= 7) return Array.from({ length: total }, (_, index) => index + 1)
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total]
  if (current >= total - 3) return [1, '…', total - 4, total - 3, total - 2, total - 1, total]
  return [1, '…', current - 1, current, current + 1, '…', total]
}

