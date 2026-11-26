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
  RotateCcw,
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
import type { PagedResult, RepairStatus, SalesFilterOptions, SalesInvoice, SalesPaymentSource, SalesSummary, ServiceType } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export function SalesPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [type, setType] = useState('')
  const [serviceTypeId, setServiceTypeId] = useState('')
  const [repairProgress, setRepairProgress] = useState('')
  const [saleMode, setSaleMode] = useState('')
  const [paymentMethod, setPaymentMethod] = useState('')
  const [createdByUserId, setCreatedByUserId] = useState('')
  const [paymentAccountId, setPaymentAccountId] = useState('')
  const [showFilters, setShowFilters] = useState(false)
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
  const filterOptionsQuery = useQuery({ queryKey: ['sales-filter-options'], queryFn: () => api.get<SalesFilterOptions>('/api/sales/filter-options') })
  const invoicesQuery = useQuery({
    queryKey: ['sales-invoices', deferredSearch, type, serviceTypeId, repairProgress, saleMode, paymentMethod, createdByUserId, paymentAccountId, page, pageSize],
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (deferredSearch) params.set('search', deferredSearch)
      if (type) params.set('type', type)
      if (serviceTypeId) params.set('serviceTypeId', serviceTypeId)
      if (repairProgress) params.set('repairProgress', repairProgress)
      if (saleMode) params.set('saleMode', saleMode)
      if (paymentMethod) params.set('paymentMethod', paymentMethod)
      if (createdByUserId) params.set('createdByUserId', createdByUserId)
      if (paymentAccountId) params.set('paymentAccountId', paymentAccountId)
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
  const activeFilterCount = [type, serviceTypeId, repairProgress, saleMode, paymentMethod, createdByUserId, paymentAccountId].filter(Boolean).length
  const hasFilters = Boolean(search || activeFilterCount)

  function clearAdvancedFilters() {
    setType('')
    setServiceTypeId('')
    setRepairProgress('')
    setSaleMode('')
    setPaymentMethod('')
    setCreatedByUserId('')
    setPaymentAccountId('')
    setPage(1)
  }

  function clearFilters() {
    setSearch('')
    clearAdvancedFilters()
  }

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

    {(summaryQuery.isError || invoicesQuery.isError || filterOptionsQuery.isError || deleteMutation.isError) && <div className="form-alert">{(summaryQuery.error ?? invoicesQuery.error ?? filterOptionsQuery.error ?? deleteMutation.error)?.message}</div>}

    <section className="sales-metrics">
      <article><span className="sales-metric-icon red"><CircleDollarSign size={22} /></span><div><small>Giá trị đã xử lý hôm nay</small><strong>{currency.format(summary?.invoiceTotalToday ?? 0)}</strong><p>Giao dịch đã hoàn tất xử lý</p></div></article>
      <article><span className="sales-metric-icon green"><Banknote size={22} /></span><div><small>Nguồn tiền đã ghi nhận</small><strong>{currency.format(summary?.collectedToday ?? 0)}</strong><p>Tiền mặt và banking của phiếu đã xử lý</p></div></article>
      <article><span className="sales-metric-icon amber"><Clock3 size={22} /></span><div><small>Công nợ cần thu</small><strong>{currency.format(summary?.outstanding ?? 0)}</strong><p>Khoản góp và hóa đơn chưa đủ tiền</p></div></article>
      <article><span className="sales-metric-icon blue"><Wrench size={22} /></span><div><small>Máy chưa xử lý xong</small><strong>{summary?.activeRepairs ?? 0}</strong><p>Thuộc các phiếu đang xử lý</p></div></article>
    </section>

    <section className="panel sales-browser">
      <div className="sales-toolbar">
        <label className="search-box sales-search"><Search size={18} /><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1) }} placeholder="Nhập thông tin tìm kiếm" aria-label="Nhập thông tin tìm kiếm" /></label>
        <button type="button" className={`secondary-button sales-filter-toggle ${showFilters ? 'is-open' : ''}`} onClick={() => setShowFilters((value) => !value)} aria-expanded={showFilters} aria-controls="sales-filter-panel"><Settings2 size={16} /><span>Bộ lọc</span>{activeFilterCount > 0 && <b>{activeFilterCount}</b>}<ChevronDown className="sales-filter-toggle-chevron" size={15} /></button>
        {hasFilters && <button type="button" className="secondary-button sales-clear-filters" onClick={clearFilters}><RotateCcw size={15} /> Xóa điều kiện</button>}
        <span className="sales-total"><strong>{invoicesQuery.data?.total ?? 0}</strong> hóa đơn</span>
      </div>
      {showFilters && <div className="sales-filter-panel" id="sales-filter-panel">
        <div className="sales-filter-panel-head">
          <div><strong>Bộ lọc hóa đơn</strong><span>Thu hẹp danh sách theo nghiệp vụ, nguồn tiền và người lập phiếu.</span></div>
          {activeFilterCount > 0 && <button type="button" className="sales-filter-reset" onClick={clearAdvancedFilters}><RotateCcw size={14} /> Đặt lại bộ lọc</button>}
        </div>
        <div className="sales-filter-grid">
          <label className="sales-filter-field"><span>Nghiệp vụ</span><div className="sales-filter"><select value={type} onChange={(event) => { setType(event.target.value); setPage(1) }}><option value="">Tất cả nghiệp vụ</option><option value="Sale">Bán hàng</option><option value="Repair">Sửa chữa</option></select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field"><span>Loại dịch vụ</span><div className="sales-filter"><select value={serviceTypeId} onChange={(event) => { setServiceTypeId(event.target.value); setPage(1) }}><option value="">Tất cả loại dịch vụ</option>{serviceTypesQuery.data?.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field"><span>Trạng thái xử lý</span><div className="sales-filter"><select value={repairProgress} onChange={(event) => { setRepairProgress(event.target.value); setPage(1) }}><option value="">Tất cả trạng thái xử lý</option><option value="pending">Đang xử lý</option><option value="processed">Đã xử lý</option></select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field"><span>Hình thức bán</span><div className="sales-filter"><select value={saleMode} onChange={(event) => { setSaleMode(event.target.value); setPage(1) }}><option value="">Trực tiếp & trả góp</option><option value="direct">Bán trực tiếp</option><option value="installment">Trả góp</option></select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field"><span>Phương thức tiền</span><div className="sales-filter"><select value={paymentMethod} onChange={(event) => { setPaymentMethod(event.target.value); setPage(1) }}><option value="">Mọi phương thức tiền</option><option value="cash">Có tiền mặt</option><option value="bank">Có chuyển khoản</option><option value="mixed">Tiền mặt + chuyển khoản</option><option value="unallocated">Chưa phân bổ</option></select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field"><span>Người lập phiếu</span><div className="sales-filter"><select value={createdByUserId} onChange={(event) => { setCreatedByUserId(event.target.value); setPage(1) }}><option value="">Tất cả người lập</option>{filterOptionsQuery.data?.creators.map((creator) => <option key={creator.id} value={creator.id}>{creator.name}</option>)}</select><ChevronDown size={15} /></div></label>
          <label className="sales-filter-field sales-filter-field-wide"><span>Tài khoản banking</span><div className="sales-filter banking-filter"><select value={paymentAccountId} onChange={(event) => { setPaymentAccountId(event.target.value); setPage(1) }}><option value="">Tất cả tài khoản banking</option>{filterOptionsQuery.data?.paymentAccounts.map((account) => <option key={account.id} value={account.id}>{account.name} · {account.bankName}</option>)}</select><ChevronDown size={15} /></div></label>
        </div>
      </div>}

      <div className="data-table-wrap sales-table-wrap">
        <table className="data-table sales-table">
          <thead><tr><th className="sales-actions-column">Thao tác</th><th>Ngày lập phiếu</th><th>Hóa đơn</th><th className="sales-customer-column">Khách hàng</th><th className="sales-purpose-column">Nội dung nội bộ & thẻ</th><th>Nghiệp vụ</th><th>Trạng thái xử lý</th><th className="sales-money-column">Tổng tiền</th><th className="sales-money-column">Đã ghi nhận</th><th className="sales-money-column">Còn lại</th><th>Phương thức tiền</th><th>Tài khoản banking</th><th>Người lập phiếu</th><th>Ngày trả máy</th><th>Ngày cập nhật lần cuối</th></tr></thead>
          <tbody>
            {invoicesQuery.isLoading && <tr><td colSpan={15} className="empty-cell">Đang tải hóa đơn…</td></tr>}
            {invoicesQuery.data?.items.map((invoice) => {
              const banking = bankingSummaries(invoice.paymentSources)
              return <tr key={invoice.id} className={`sales-invoice-row ${invoice.repairStatus === 'Returned' ? 'repair-is-returned' : invoice.repairStatus === 'ReadyForReturn' ? 'repair-is-ready' : 'repair-is-pending'}`} onClick={() => setSelectedInvoiceId(invoice.id)}>
              <td className="sales-actions-column"><div className="invoice-row-actions">{hasPermission('sales.manage') && <button type="button" className="icon-button" onClick={(event) => { event.stopPropagation(); setEditInvoiceId(invoice.id) }} aria-label={`Sửa ${invoice.number}`} title="Sửa hóa đơn"><Pencil size={16} /></button>}<button type="button" className="icon-button" onClick={(event) => { event.stopPropagation(); setPrintInvoiceId(invoice.id) }} aria-label={`In ${invoice.number}`} title="In hóa đơn"><Printer size={16} /></button>{hasPermission('sales.manage') && <button type="button" className="icon-button danger" onClick={(event) => { event.stopPropagation(); deleteInvoice(invoice) }} aria-label={`Xóa ${invoice.number}`} title="Xóa hóa đơn" disabled={deleteMutation.isPending}><Trash2 size={16} /></button>}</div></td>
              <td><span className="date-cell created-date">{formatDateTime(invoice.invoiceDateUtc)}</span></td>
              <td><button type="button" className="invoice-number" onClick={() => setSelectedInvoiceId(invoice.id)}><FileText size={16} /><span><strong>{invoice.number}</strong><small>{invoice.type === 'Repair' && invoice.repairDeviceModel ? `${invoice.repairDeviceModel}${invoice.repairDeviceCount > 1 ? ` + ${invoice.repairDeviceCount - 1} máy` : ''}` : invoice.isInstallment && invoice.installmentProvider ? invoice.installmentProvider : 'Bán sản phẩm'}</small>{invoice.isInstallment && <span className="invoice-sale-mode installment">Trả góp</span>}</span></button></td>
              <td className="sales-customer-column"><div className="sales-customer-cell"><strong>{invoice.customerName}</strong><span>{invoice.customerPhone || 'Chưa có số điện thoại'}</span><small>{invoice.customerInvoiceCount || 0} đơn · Tổng mua {currency.format(invoice.customerTotalSpent || 0)}</small></div></td>
              <td className="invoice-purpose-cell sales-purpose-column"><strong>{invoice.notes || (invoice.type === 'Repair' ? `Sửa chữa ${invoice.repairDeviceModel || 'máy khách'}` : 'Bán sản phẩm')}</strong><div className="invoice-service-tags">{invoice.serviceTypes?.map((item, index) => <span className={serviceTagTone(item.code, index)} key={item.id}>{item.name}</span>)}{invoice.serviceTypes?.length === 0 && <span className="tag-neutral">Chưa gắn thẻ</span>}</div></td>
              <td><div className="invoice-service-cell">{invoice.type === 'Sale' ? <span className="invoice-type sale"><PackageCheck size={14} /> Bán sản phẩm</span> : <span className="invoice-type repair"><Wrench size={14} /> Sửa chữa</span>}</div></td>
              <td><div className="repair-progress-cell"><StatusBadge tone={processingTone(invoice.repairStatus)}>{processingLabel(invoice.repairStatus)}</StatusBadge>{invoice.type === 'Repair' && <small>{invoice.repairDeviceCount} máy trong phiếu</small>}</div></td>
              <td className="money sales-money-column">{currency.format(invoice.totalAmount)}</td>
              <td className="collected-money sales-money-column">{invoice.isInstallment ? <span className="installment-money-breakdown"><strong>{currency.format(invoice.paidAmount)}</strong><small>{currency.format(invoice.receivedAmount)} tiền mặt + {currency.format(invoice.scheduledAmount)} banking</small></span> : currency.format(invoice.paidAmount)}</td>
              <td className={`${invoice.outstandingAmount > 0 ? 'outstanding-money' : 'muted-money'} sales-money-column`}>{currency.format(invoice.outstandingAmount)}</td>
              <td><div className="payment-method-cell"><span className={`payment-method-badge ${paymentMethodTone(invoice.paymentSources)}`}>{paymentMethodLabel(invoice.paymentSources)}</span>{invoice.paymentDueDateUtc && invoice.isInstallment ? <small className="due-date"><CalendarClock size={12} /> Dự kiến {new Date(invoice.paymentDueDateUtc).toLocaleDateString('vi-VN')}</small> : null}</div></td>
              <td><div className="banking-account-cell">{banking.map((account) => <span key={account.key}><strong>{account.name}</strong><small>{account.bankName}{account.accountNumber ? ` · ${maskAccountNumber(account.accountNumber)}` : ''}</small><b>{currency.format(account.amount)}</b></span>)}{banking.length === 0 && <span className="banking-empty">Không có chuyển khoản</span>}</div></td>
              <td><span className="invoice-creator">{invoice.createdByName || 'Dữ liệu cũ'}</span></td>
              <td><span className={`date-cell return-date ${invoice.expectedReturnAtUtc ? '' : 'is-empty'}`}>{invoice.expectedReturnAtUtc ? formatDateTime(invoice.expectedReturnAtUtc) : '—'}</span></td>
              <td><span className="date-cell updated-date">{formatDateTime(invoice.updatedAtUtc)}</span></td>
            </tr>})}
            {!invoicesQuery.isLoading && invoicesQuery.data?.items.length === 0 && <tr><td colSpan={15} className="empty-cell"><CreditCard size={22} /> Chưa có hóa đơn phù hợp.</td></tr>}
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

function processingLabel(status: RepairStatus | undefined) {
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

function paymentMethodLabel(sources: SalesPaymentSource[]) {
  const hasCash = sources.some((source) => source.method === 'Cash')
  const hasBank = sources.some((source) => source.method === 'BankTransfer')
  if (hasCash && hasBank) return 'Tiền mặt + chuyển khoản'
  if (hasCash) return 'Tiền mặt'
  if (hasBank) return 'Chuyển khoản'
  return 'Chưa phân bổ'
}

function paymentMethodTone(sources: SalesPaymentSource[]) {
  const hasCash = sources.some((source) => source.method === 'Cash')
  const hasBank = sources.some((source) => source.method === 'BankTransfer')
  if (hasCash && hasBank) return 'mixed'
  if (hasCash) return 'cash'
  if (hasBank) return 'bank'
  return 'empty'
}

function bankingSummaries(sources: SalesPaymentSource[]) {
  const summaries = new Map<string, { key: string; name: string; bankName: string; accountNumber?: string; amount: number }>()
  for (const source of sources.filter((item) => item.method === 'BankTransfer')) {
    const key = source.paymentAccountId || `${source.bankName || 'bank'}:${source.accountNumber || source.paymentAccountName || 'unknown'}`
    const current = summaries.get(key)
    if (current) {
      current.amount += source.amount
      continue
    }
    summaries.set(key, {
      key,
      name: source.paymentAccountName || 'Chưa chọn tài khoản',
      bankName: source.bankName || 'Banking',
      accountNumber: source.accountNumber,
      amount: source.amount,
    })
  }
  return [...summaries.values()]
}

function maskAccountNumber(value: string) {
  const compact = value.replace(/\s+/g, '')
  return compact.length <= 6 ? compact : `•••• ${compact.slice(-6)}`
}

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function serviceTagTone(code: string, index: number) {
  const normalized = code.toUpperCase()
  if (normalized.includes('LAPTOP') || normalized.includes('LCD')) return 'tag-blue'
  if (normalized.includes('POWER') || normalized.includes('PIN') || normalized.includes('NGUON')) return 'tag-amber'
  if (normalized.includes('PHIM') || normalized.includes('KEYBOARD')) return 'tag-red'
  if (normalized.includes('VE-SINH') || normalized.includes('SOFTWARE') || normalized.includes('PHAN-MEM')) return 'tag-green'
  return ['tag-slate', 'tag-blue', 'tag-amber', 'tag-green', 'tag-red'][index % 5]
}

function paginationItems(current: number, total: number): Array<number | '…'> {
  if (total <= 7) return Array.from({ length: total }, (_, index) => index + 1)
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total]
  if (current >= total - 3) return [1, '…', total - 4, total - 3, total - 2, total - 1, total]
  return [1, '…', current - 1, current, current + 1, '…', total]
}

