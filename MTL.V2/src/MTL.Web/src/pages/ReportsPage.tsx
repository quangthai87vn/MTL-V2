import {
  Banknote,
  BarChart3,
  CalendarDays,
  ChevronDown,
  CircleDollarSign,
  Clock3,
  FileText,
  Landmark,
  ReceiptText,
  RotateCcw,
  Settings2,
  TrendingDown,
  TrendingUp,
  WalletCards,
} from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { PageHeader } from '../components/PageHeader'
import { api } from '../lib/api'
import type { RevenueReport, RevenueReportFilterOptions } from '../types'

type GroupBy = 'day' | 'month' | 'year'
type Preset = 'today' | 'seven-days' | 'month' | 'year' | 'custom'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })
const percent = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })

export function ReportsPage() {
  const initialRange = getPresetRange('month')
  const [fromDate, setFromDate] = useState(initialRange.from)
  const [toDate, setToDate] = useState(initialRange.to)
  const [groupBy, setGroupBy] = useState<GroupBy>('day')
  const [preset, setPreset] = useState<Preset>('month')
  const [type, setType] = useState('')
  const [paymentSource, setPaymentSource] = useState('')
  const [paymentAccountId, setPaymentAccountId] = useState('')
  const [createdByUserId, setCreatedByUserId] = useState('')
  const [showFilters, setShowFilters] = useState(true)

  const filterOptionsQuery = useQuery({
    queryKey: ['revenue-report-filter-options'],
    queryFn: () => api.get<RevenueReportFilterOptions>('/api/reports/filter-options'),
  })
  const queryString = useMemo(() => {
    const params = new URLSearchParams({ from: fromDate, to: toDate, groupBy })
    if (type) params.set('type', type)
    if (paymentSource) params.set('paymentSource', paymentSource)
    if (paymentAccountId) params.set('paymentAccountId', paymentAccountId)
    if (createdByUserId) params.set('createdByUserId', createdByUserId)
    return params.toString()
  }, [fromDate, toDate, groupBy, type, paymentSource, paymentAccountId, createdByUserId])
  const reportQuery = useQuery({
    queryKey: ['revenue-report', queryString],
    queryFn: () => api.get<RevenueReport>(`/api/reports/revenue?${queryString}`),
    enabled: Boolean(fromDate && toDate && fromDate <= toDate),
    placeholderData: (previous) => previous,
  })

  const report = reportQuery.data
  const activeFilterCount = [type, paymentSource, paymentAccountId, createdByUserId].filter(Boolean).length

  function applyPreset(nextPreset: Exclude<Preset, 'custom'>) {
    const next = getPresetRange(nextPreset)
    setFromDate(next.from)
    setToDate(next.to)
    setPreset(nextPreset)
    setGroupBy(nextPreset === 'year' ? 'month' : 'day')
  }

  function resetAdvancedFilters() {
    setType('')
    setPaymentSource('')
    setPaymentAccountId('')
    setCreatedByUserId('')
  }

  function drillIntoPeriod(item: RevenueReport['timeline'][number]) {
    setFromDate(item.from)
    setToDate(item.to)
    setPreset('custom')
    if (groupBy === 'year') setGroupBy('month')
    else if (groupBy === 'month') setGroupBy('day')
  }

  function selectBanking(accountId?: string) {
    if (!accountId) return
    setPaymentAccountId(accountId)
    setPaymentSource('bank')
    setShowFilters(true)
  }

  const error = reportQuery.error ?? filterOptionsQuery.error

  return (
    <div className="reports-page">
      <PageHeader
        eyebrow="PHÂN TÍCH"
        title="BÁO CÁO DOANH THU"
        description="Theo dõi doanh thu hóa đơn, dòng tiền thực thu và hiệu quả từng tài khoản banking."
      />

      {error && <div className="form-alert">{error.message}</div>}

      <section className="panel report-control-panel">
        <div className="report-control-top">
          <div className="report-presets" aria-label="Khoảng thời gian nhanh">
            <button type="button" className={preset === 'today' ? 'is-active' : ''} onClick={() => applyPreset('today')}>Hôm nay</button>
            <button type="button" className={preset === 'seven-days' ? 'is-active' : ''} onClick={() => applyPreset('seven-days')}>7 ngày</button>
            <button type="button" className={preset === 'month' ? 'is-active' : ''} onClick={() => applyPreset('month')}>Tháng này</button>
            <button type="button" className={preset === 'year' ? 'is-active' : ''} onClick={() => applyPreset('year')}>Năm nay</button>
          </div>
          <div className="report-control-actions">
            <div className="report-grouping" aria-label="Cấp thời gian">
              {(['day', 'month', 'year'] as GroupBy[]).map((value) => (
                <button key={value} type="button" className={groupBy === value ? 'is-active' : ''} aria-pressed={groupBy === value} onClick={() => setGroupBy(value)}>
                  {value === 'day' ? 'Ngày' : value === 'month' ? 'Tháng' : 'Năm'}
                </button>
              ))}
            </div>
            <button type="button" className={`secondary-button report-filter-toggle ${showFilters ? 'is-open' : ''}`} onClick={() => setShowFilters((value) => !value)} aria-expanded={showFilters} aria-controls="report-filter-panel">
              <Settings2 size={16} /> Bộ lọc {activeFilterCount > 0 && <b>{activeFilterCount}</b>} <ChevronDown size={15} />
            </button>
          </div>
        </div>

        {showFilters && <div className="report-filter-panel" id="report-filter-panel">
          <div className="report-filter-head">
            <div><strong>Điều kiện báo cáo</strong><span>Ngày hóa đơn dùng cho doanh thu; ngày nhận tiền dùng cho dòng tiền.</span></div>
            {activeFilterCount > 0 && <button type="button" onClick={resetAdvancedFilters}><RotateCcw size={14} /> Đặt lại bộ lọc</button>}
          </div>
          <div className="report-filter-grid">
            <label><span>Từ ngày</span><input type="date" value={fromDate} max={toDate} onChange={(event) => { setFromDate(event.target.value); setPreset('custom') }} /></label>
            <label><span>Đến ngày</span><input type="date" value={toDate} min={fromDate} onChange={(event) => { setToDate(event.target.value); setPreset('custom') }} /></label>
            <label><span>Nghiệp vụ</span><div className="report-select"><select value={type} onChange={(event) => setType(event.target.value)}><option value="">Tất cả nghiệp vụ</option><option value="Sale">Bán sản phẩm</option><option value="Repair">Sửa chữa</option></select><ChevronDown size={15} /></div></label>
            <label><span>Nguồn tiền</span><div className="report-select"><select value={paymentSource} onChange={(event) => { const value = event.target.value; setPaymentSource(value); if (value === 'cash') setPaymentAccountId('') }}><option value="">Tất cả nguồn tiền</option><option value="cash">Có tiền mặt</option><option value="bank">Có chuyển khoản</option><option value="mixed">Tiền mặt + chuyển khoản</option></select><ChevronDown size={15} /></div></label>
            <label><span>Tài khoản banking</span><div className="report-select"><select value={paymentAccountId} onChange={(event) => { setPaymentAccountId(event.target.value); if (event.target.value) setPaymentSource('bank') }}><option value="">Tất cả tài khoản</option>{filterOptionsQuery.data?.paymentAccounts.map((account) => <option key={account.id} value={account.id}>{paymentAccountOptionLabel(account)}</option>)}</select><ChevronDown size={15} /></div></label>
            <label><span>Người lập phiếu</span><div className="report-select"><select value={createdByUserId} onChange={(event) => setCreatedByUserId(event.target.value)}><option value="">Tất cả người lập</option>{filterOptionsQuery.data?.creators.map((creator) => <option key={creator.id} value={creator.id}>{creator.name}</option>)}</select><ChevronDown size={15} /></div></label>
          </div>
        </div>}
      </section>

      <section className="report-metrics" aria-label="Tổng quan doanh thu">
        <ReportMetric icon={<CircleDollarSign size={21} />} tone="red" label="Doanh thu hóa đơn" value={currency.format(report?.summary.invoiceRevenue ?? 0)} detail="Hóa đơn đã xử lý" delta={report?.summary.revenueChangePercent} />
        <ReportMetric icon={<Banknote size={21} />} tone="green" label="Tiền thực thu" value={currency.format(report?.summary.collectedAmount ?? 0)} detail="Không gồm khoản góp dự kiến" delta={report?.summary.collectedChangePercent} />
        <ReportMetric icon={<Clock3 size={21} />} tone="amber" label="Còn phải thu" value={currency.format(report?.summary.outstandingAmount ?? 0)} detail={`Dự kiến về ${currency.format(report?.summary.scheduledAmount ?? 0)}`} />
        <ReportMetric icon={<FileText size={21} />} tone="blue" label="Số hóa đơn" value={number.format(report?.summary.invoiceCount ?? 0)} detail="Phiếu trong kỳ đã chọn" />
        <ReportMetric icon={<ReceiptText size={21} />} tone="ink" label="Trung bình / hóa đơn" value={currency.format(report?.summary.averageInvoiceValue ?? 0)} detail="Giá trị trung bình trong kỳ" />
      </section>

      <section className="panel report-chart-panel report-main-chart">
        <header className="report-panel-head">
          <div><span className="section-kicker">DOANH THU THEO THỜI GIAN</span><h2>{reportRangeTitle(fromDate, toDate, groupBy)}</h2><p>Bấm vào một mốc để xem chi tiết ở cấp thời gian nhỏ hơn.</p></div>
          <div className="report-legend"><span><i className="is-revenue" />Doanh thu hóa đơn</span><span><i className="is-collected" />Tiền thực thu</span></div>
        </header>
        {reportQuery.isLoading && !report ? <ReportLoading /> : <RevenueTimelineChart data={report?.timeline ?? []} onSelect={drillIntoPeriod} />}
      </section>

      <div className="report-analysis-grid">
        <section className="panel report-chart-panel">
          <header className="report-panel-head">
            <div><span className="section-kicker">CƠ CẤU NGUỒN TIỀN</span><h2>Tiền mặt, chuyển khoản và tiền dự kiến</h2></div>
            <div className="report-legend"><span><i className="is-cash" />Tiền mặt</span><span><i className="is-bank" />Chuyển khoản</span><span><i className="is-scheduled" />Dự kiến</span></div>
          </header>
          <PaymentCompositionChart data={report?.timeline ?? []} />
          <div className="report-source-summary">
            {(report?.paymentSources ?? []).map((source) => <div key={source.key}><div><span>{source.label}</span><strong>{currency.format(source.amount)}</strong></div><div className="report-source-track"><i className={`is-${source.key}`} style={{ width: `${Math.max(0, Math.min(source.share, 100))}%` }} /></div><small>{percent.format(source.share)}% · {number.format(source.transactionCount)} giao dịch</small></div>)}
          </div>
        </section>

        <section className="panel report-chart-panel report-banking-panel">
          <header className="report-panel-head">
            <div><span className="section-kicker">BANKING NHẬN TIỀN</span><h2>Xếp hạng tài khoản</h2><p>Số thực thu, không cộng khoản góp đang dự kiến.</p></div>
            <Landmark size={20} />
          </header>
          <BankingRanking data={report?.banking ?? []} selectedId={paymentAccountId} onSelect={selectBanking} />
        </section>
      </div>

      <section className="panel report-invoice-panel">
        <header className="report-panel-head">
          <div><span className="section-kicker">CHI TIẾT HÓA ĐƠN</span><h2>Giao dịch gần nhất trong kỳ</h2><p>Hiển thị tối đa 20 hóa đơn theo điều kiện đang chọn.</p></div>
          <span className="report-generated">Cập nhật {report?.generatedAtUtc ? formatDateTime(report.generatedAtUtc) : '—'}</span>
        </header>
        <div className="report-table-wrap">
          <table className="data-table report-table">
            <thead><tr><th>Ngày lập</th><th>Hóa đơn</th><th>Khách hàng</th><th>Nghiệp vụ</th><th>Nguồn tiền</th><th className="money-head">Tổng tiền</th><th className="money-head">Thực thu</th><th className="money-head">Còn lại</th><th>Người lập</th></tr></thead>
            <tbody>
              {report?.invoices.map((invoice) => <tr key={invoice.id}><td className="date-cell">{formatDateTime(invoice.invoiceDateUtc)}</td><td><strong>{invoice.number}</strong></td><td>{invoice.customerName}</td><td><span className={`report-type-tag ${invoice.type === 'Repair' ? 'is-repair' : 'is-sale'}`}>{invoice.type === 'Repair' ? 'Sửa chữa' : 'Bán sản phẩm'}</span></td><td>{invoice.paymentSource}</td><td className="money">{currency.format(invoice.totalAmount)}</td><td className="money report-collected-money">{currency.format(invoice.collectedAmount)}</td><td className="money">{currency.format(invoice.outstandingAmount)}</td><td>{invoice.createdByName}</td></tr>)}
              {!reportQuery.isLoading && report?.invoices.length === 0 && <tr><td colSpan={9} className="empty-cell"><BarChart3 size={22} /> Chưa có hóa đơn phù hợp trong khoảng thời gian này.</td></tr>}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

function ReportMetric({ icon, tone, label, value, detail, delta }: { icon: React.ReactNode; tone: string; label: string; value: string; detail: string; delta?: number }) {
  return <article><span className={`report-metric-icon ${tone}`}>{icon}</span><div><small>{label}</small><strong>{value}</strong><p>{detail}</p></div>{delta !== undefined && <span className={`report-delta ${delta >= 0 ? 'is-up' : 'is-down'}`}>{delta >= 0 ? <TrendingUp size={13} /> : <TrendingDown size={13} />}{delta >= 0 ? '+' : ''}{percent.format(delta)}%</span>}</article>
}

function RevenueTimelineChart({ data, onSelect }: { data: RevenueReport['timeline']; onSelect: (item: RevenueReport['timeline'][number]) => void }) {
  if (data.length === 0) return <ReportEmpty />
  const width = 1040
  const height = 330
  const left = 76
  const right = 22
  const top = 20
  const bottom = 54
  const plotWidth = width - left - right
  const plotHeight = height - top - bottom
  const maximum = Math.max(1, ...data.flatMap((item) => [item.invoiceRevenue, item.collectedAmount]))
  const band = plotWidth / data.length
  const barWidth = Math.max(5, Math.min(30, band * .56))
  const y = (value: number) => top + plotHeight - (value / maximum) * plotHeight
  const labelStep = Math.max(1, Math.ceil(data.length / 12))
  const line = data.map((item, index) => `${index === 0 ? 'M' : 'L'} ${left + band * (index + .5)} ${y(item.collectedAmount)}`).join(' ')

  return <div className="report-svg-wrap"><svg className="report-timeline-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Biểu đồ doanh thu hóa đơn và tiền thực thu theo thời gian">
    {[0, .25, .5, .75, 1].map((ratio) => { const value = maximum * ratio; const lineY = y(value); return <g key={ratio}><line x1={left} y1={lineY} x2={width - right} y2={lineY} className="report-grid-line" /><text x={left - 12} y={lineY + 4} textAnchor="end" className="report-axis-label">{formatCompactMoney(value)}</text></g> })}
    {data.map((item, index) => {
      const center = left + band * (index + .5)
      const barHeight = Math.max(0, top + plotHeight - y(item.invoiceRevenue))
      return <g key={`${item.from}-${item.to}`} className="report-chart-period" role="button" tabIndex={0} aria-label={`${item.label}: doanh thu ${currency.format(item.invoiceRevenue)}, thực thu ${currency.format(item.collectedAmount)}`} onClick={() => onSelect(item)} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelect(item) } }}>
        <title>{item.label} · Doanh thu {currency.format(item.invoiceRevenue)} · Thực thu {currency.format(item.collectedAmount)} · {number.format(item.invoiceCount)} hóa đơn</title>
        <rect x={left + band * index} y={top} width={band} height={plotHeight} className="report-chart-hit" />
        <rect x={center - barWidth / 2} y={y(item.invoiceRevenue)} width={barWidth} height={barHeight} rx={4} className="report-revenue-bar" />
        {(index % labelStep === 0 || index === data.length - 1) && <text x={center} y={height - 18} textAnchor="middle" className="report-axis-label">{item.label}</text>}
      </g>
    })}
    <path d={line} className="report-collected-line" />
    {data.length <= 60 && data.map((item, index) => <circle key={item.from} cx={left + band * (index + .5)} cy={y(item.collectedAmount)} r={3.4} className="report-collected-point"><title>{item.label}: {currency.format(item.collectedAmount)}</title></circle>)}
  </svg></div>
}

function PaymentCompositionChart({ data }: { data: RevenueReport['timeline'] }) {
  if (data.length === 0) return <ReportEmpty />
  const width = 680
  const height = 248
  const left = 60
  const right = 16
  const top = 16
  const bottom = 44
  const plotWidth = width - left - right
  const plotHeight = height - top - bottom
  const totals = data.map((item) => item.cashAmount + item.bankAmount + item.scheduledAmount)
  const maximum = Math.max(1, ...totals)
  const band = plotWidth / data.length
  const barWidth = Math.max(5, Math.min(28, band * .62))
  const labelStep = Math.max(1, Math.ceil(data.length / 8))
  const segmentHeight = (value: number) => value / maximum * plotHeight

  return <div className="report-svg-wrap report-source-chart-wrap"><svg className="report-source-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Biểu đồ cơ cấu tiền mặt, chuyển khoản và khoản góp dự kiến theo thời gian">
    <defs><pattern id="report-scheduled-pattern" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><rect width="7" height="7" className="report-scheduled-pattern-base" /><line x1="0" y1="0" x2="0" y2="7" className="report-scheduled-pattern-line" /></pattern></defs>
    {[0, .5, 1].map((ratio) => { const y = top + plotHeight - plotHeight * ratio; return <g key={ratio}><line x1={left} y1={y} x2={width - right} y2={y} className="report-grid-line" /><text x={left - 10} y={y + 4} textAnchor="end" className="report-axis-label">{formatCompactMoney(maximum * ratio)}</text></g> })}
    {data.map((item, index) => {
      const center = left + band * (index + .5)
      const cashHeight = segmentHeight(item.cashAmount)
      const bankHeight = segmentHeight(item.bankAmount)
      const scheduledHeight = segmentHeight(item.scheduledAmount)
      const baseY = top + plotHeight
      return <g key={item.from}><title>{item.label} · Tiền mặt {currency.format(item.cashAmount)} · Chuyển khoản {currency.format(item.bankAmount)} · Dự kiến {currency.format(item.scheduledAmount)}</title><rect x={center - barWidth / 2} y={baseY - cashHeight} width={barWidth} height={cashHeight} className="report-cash-bar" /><rect x={center - barWidth / 2} y={baseY - cashHeight - bankHeight} width={barWidth} height={bankHeight} className="report-bank-bar" /><rect x={center - barWidth / 2} y={baseY - cashHeight - bankHeight - scheduledHeight} width={barWidth} height={scheduledHeight} fill="url(#report-scheduled-pattern)" />{(index % labelStep === 0 || index === data.length - 1) && <text x={center} y={height - 15} textAnchor="middle" className="report-axis-label">{item.label}</text>}</g>
    })}
  </svg></div>
}

function BankingRanking({ data, selectedId, onSelect }: { data: RevenueReport['banking']; selectedId: string; onSelect: (id?: string) => void }) {
  if (data.length === 0) return <div className="report-empty report-banking-empty"><WalletCards size={24} /><strong>Chưa có giao dịch banking</strong><span>Thử mở rộng khoảng thời gian hoặc bỏ bớt bộ lọc.</span></div>
  const maximum = Math.max(1, ...data.map((item) => item.actualAmount))
  return <div className="report-banking-list">{data.slice(0, 8).map((item, index) => <button type="button" key={item.paymentAccountId ?? `${item.bankName}-${item.accountNumber}`} className={selectedId && selectedId === item.paymentAccountId ? 'is-selected' : ''} onClick={() => onSelect(item.paymentAccountId)} disabled={!item.paymentAccountId}><span className="report-bank-rank">{String(index + 1).padStart(2, '0')}</span><span className="report-bank-copy"><span><strong>{item.bankName}</strong><small>{item.accountNumber ? maskAccount(item.accountNumber) : item.name}{item.accountHolder ? ` · ${item.accountHolder}` : ''}</small></span><span><b>{currency.format(item.actualAmount)}</b><small>{percent.format(item.share)}%</small></span><i><em style={{ width: `${item.actualAmount / maximum * 100}%` }} /></i>{item.scheduledAmount > 0 && <small className="report-bank-scheduled">Dự kiến: {currency.format(item.scheduledAmount)}</small>}</span></button>)}</div>
}

function ReportLoading() {
  return <div className="report-empty"><BarChart3 size={24} /><strong>Đang tổng hợp số liệu</strong><span>Dữ liệu hóa đơn và thanh toán đang được đối chiếu.</span></div>
}

function ReportEmpty() {
  return <div className="report-empty"><CalendarDays size={24} /><strong>Chưa có dữ liệu trong kỳ</strong><span>Hãy thay đổi khoảng thời gian hoặc điều kiện lọc.</span></div>
}

function getPresetRange(preset: Exclude<Preset, 'custom'>) {
  const today = new Date()
  const to = toDateInput(today)
  if (preset === 'today') return { from: to, to }
  if (preset === 'seven-days') { const from = new Date(today); from.setDate(from.getDate() - 6); return { from: toDateInput(from), to } }
  if (preset === 'year') return { from: `${today.getFullYear()}-01-01`, to }
  return { from: `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-01`, to }
}

function toDateInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

function formatCompactMoney(value: number) {
  const absolute = Math.abs(value)
  if (absolute >= 1_000_000_000) return `${percent.format(value / 1_000_000_000)} tỷ`
  if (absolute >= 1_000_000) return `${percent.format(value / 1_000_000)} tr`
  if (absolute >= 1_000) return `${percent.format(value / 1_000)} nghìn`
  return number.format(value)
}

function reportRangeTitle(from: string, to: string, groupBy: GroupBy) {
  const group = groupBy === 'day' ? 'Theo ngày' : groupBy === 'month' ? 'Theo tháng' : 'Theo năm'
  return `${group} · ${formatDate(from)} – ${formatDate(to)}`
}

function formatDate(value: string) {
  const [year, month, day] = value.split('-')
  return year && month && day ? `${day}/${month}/${year}` : value
}

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(date)
}

function maskAccount(value: string) {
  const compact = value.replace(/\s+/g, '')
  return compact.length <= 4 ? compact : `•••• ${compact.slice(-4)}`
}

function paymentAccountOptionLabel(account: RevenueReportFilterOptions['paymentAccounts'][number]) {
  return [account.bankName, maskAccount(account.accountNumber), account.accountHolder].filter(Boolean).join(' · ')
}
