import { Banknote, Building2, CalendarClock, CreditCard, FileText, Phone, Printer, ReceiptText, Wrench, X } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { api } from '../lib/api'
import type { RepairStatus, SalesInvoiceDetails } from '../types'
import { StatusBadge } from './StatusBadge'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const dateTime = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' })

export function SalesInvoiceDetailsDialog({ invoiceId, autoPrintMode, onClose }: { invoiceId: string; autoPrintMode?: 'a4' | 'bill'; onClose: () => void; onChanged: () => void }) {
  const [printMode, setPrintMode] = useState<'a4' | 'bill'>('a4')
  const [referenceNow] = useState(() => Date.now())
  const autoPrinted = useRef(false)

  const invoiceQuery = useQuery({ queryKey: ['sales-invoice', invoiceId], queryFn: () => api.get<SalesInvoiceDetails>(`/api/sales/${invoiceId}`) })

  function printInvoice(mode: 'a4' | 'bill') {
    setPrintMode(mode)
    window.setTimeout(() => window.print(), 80)
  }

  const invoice = invoiceQuery.data
  const invoiceRepairStatus = invoice?.repairStatus ?? invoice?.repairs?.[0]?.status ?? invoice?.repair?.status ?? 'InProgress'
  const outstanding = invoice?.outstandingAmount ?? 0
  const scheduledPayments = invoice?.payments.filter((payment) => payment.isScheduled) ?? []
  const scheduledAmount = scheduledPayments.reduce((sum, payment) => sum + payment.amount, 0)
  const nextScheduledPayment = scheduledPayments.find((payment) => new Date(payment.paidAtUtc).getTime() > referenceNow) ?? scheduledPayments[0]
  useEffect(() => {
    if (!invoice || !autoPrintMode || autoPrinted.current) return
    autoPrinted.current = true
    setPrintMode(autoPrintMode)
    window.setTimeout(() => window.print(), 80)
  }, [autoPrintMode, invoice])

  return <><div className="modal-backdrop sales-details-backdrop" onMouseDown={onClose}>
    <section className={`modal-card sales-details-dialog invoice-print-area print-${printMode}`} onMouseDown={(event) => event.stopPropagation()}>
      <div className="invoice-print-brand">{invoice?.company.logoDataUrl && <img src={invoice.company.logoDataUrl} alt="Logo công ty" />}<div><strong>{invoice?.company.name ?? 'MỸ THO LAPTOP'}</strong>{invoice?.company.address && <span>{invoice.company.address}</span>}<span>{[invoice?.company.phone, invoice?.company.email].filter(Boolean).join(' · ')}</span>{invoice?.company.taxCode && <span>MST: {invoice.company.taxCode}</span>}<span>{invoice?.company.website}</span></div><small>{invoice?.type === 'Repair' ? 'PHIẾU TIẾP NHẬN SỬA CHỮA' : 'HÓA ĐƠN BÁN HÀNG'}</small></div>
      <div className="sales-details-head">
        <div><span className="eyebrow accent">{invoice?.type === 'Repair' ? 'PHIẾU TIẾP NHẬN SỬA CHỮA' : 'HÓA ĐƠN BÁN HÀNG'}</span><h2>{invoice?.number ?? 'Đang tải…'}</h2>{invoice && <p>{dateTime.format(new Date(invoice.invoiceDateUtc))} · {invoice.customerName}</p>}</div>
        <div className="sales-details-actions"><button type="button" className="secondary-button no-print" onClick={() => printInvoice('a4')}><Printer size={17} /> In A4</button><button type="button" className="secondary-button no-print" onClick={() => printInvoice('bill')}><ReceiptText size={17} /> In bill 80mm</button><button type="button" className="icon-button no-print" onClick={onClose} aria-label="Đóng"><X size={20} /></button></div>
      </div>

      {invoiceQuery.isError && <div className="form-alert">{invoiceQuery.error.message}</div>}
      {invoiceQuery.isLoading && <div className="sales-details-loading">Đang tải hóa đơn…</div>}
      {invoice && <>
        <div className="sales-details-overview">
          <article><small>Khách hàng</small><strong>{invoice.customerName}</strong><span><Phone size={14} /> {invoice.customerPhone || 'Chưa có số điện thoại'}</span>{invoice.customerAddress && <span>{invoice.customerAddress}</span>}</article>
          <article><small>Nghiệp vụ</small><strong>{invoice.type === 'Sale' ? 'Bán sản phẩm' : 'Tiếp nhận sửa chữa'}</strong><StatusBadge tone={repairStatusTone(invoiceRepairStatus)}>{repairStatusLabel(invoiceRepairStatus)}</StatusBadge></article>
          <article><small>Tổng hóa đơn</small><strong className="details-total">{currency.format(invoice.totalAmount)}</strong><span>{invoiceRepairStatus === 'Returned' ? 'Đã ghi nhận' : 'Phân bổ dự kiến'}: {currency.format(invoice.paidAmount)}</span></article>
          <article className={outstanding > 0 || scheduledAmount > 0 ? 'has-debt' : ''}><small>{invoice.isInstallment ? 'Banking đã lên lịch' : 'Còn phải thu'}</small><strong>{currency.format(invoice.isInstallment ? scheduledAmount : outstanding)}</strong>{nextScheduledPayment && <span><CalendarClock size={14} /> Dự kiến {dateTime.format(new Date(nextScheduledPayment.paidAtUtc))}</span>}</article>
        </div>
        <div className="invoice-issued-by"><span>Người xuất hóa đơn</span><strong>{invoice.issuer.name}</strong>{invoice.issuer.email && <small>{invoice.issuer.email}</small>}</div>

        {invoice.notes && <section className="invoice-purpose-detail"><FileText size={18} /><div><small>NỘI DUNG HIỂN THỊ NỘI BỘ</small><strong>{invoice.notes}</strong></div></section>}
        {invoice.printNotes && <section className="invoice-purpose-detail print-note-preview"><ReceiptText size={18} /><div><small>GHI CHÚ TRÊN BILL</small><strong>{invoice.printNotes}</strong></div></section>}

        {(invoice.repairs?.length || invoice.repair) && <section className="sales-details-section repair-details">
          <h3><Wrench size={18} /> Thông tin máy sửa chữa ({invoice.repairs?.length || 1})</h3>
          <div className="repair-overall-status"><span>Trạng thái xử lý toàn phiếu</span><StatusBadge tone={repairStatusTone(invoiceRepairStatus)}>{repairStatusLabel(invoiceRepairStatus)}</StatusBadge>{invoice.expectedReturnAtUtc && <strong><CalendarClock size={15} /> Dự kiến trả {dateTime.format(new Date(invoice.expectedReturnAtUtc))}</strong>}</div>
          <div className="repair-detail-list">{(invoice.repairs?.length ? invoice.repairs : invoice.repair ? [invoice.repair] : []).map((repair, index) => <article key={repair.id || `${repair.deviceModel}-${index}`}><header><strong>Máy {index + 1}: {repair.deviceModel}</strong><span>{repair.services?.map((item) => item.name).join(' · ') || repair.serviceTypes?.map((item) => item.name).join(' · ') || 'Chưa gắn loại dịch vụ'}</span></header><dl><div><dt>Serial / Service Tag</dt><dd>{repair.serialNumber || '—'}</dd></div><div><dt>Phụ kiện nhận kèm</dt><dd>{repair.accessories || 'Không ghi nhận'}</dd></div><div><dt>Tổng dịch vụ</dt><dd>{currency.format(repair.estimatedCost)}</dd></div><div className="full"><dt>Dịch vụ & chi phí</dt><dd>{repair.services?.length ? repair.services.map((service) => `${service.name}${service.notes ? ` — ${service.notes}` : ''}: ${currency.format(service.amount)}`).join(' · ') : 'Chưa tách chi phí dịch vụ'}</dd></div><div className="full"><dt>Tình trạng</dt><dd>{repair.deviceCondition || 'Không ghi nhận'}</dd></div></dl></article>)}</div>
        </section>}

        <section className="sales-details-section">
          <h3><FileText size={18} /> Chi tiết hóa đơn</h3>
          <div className="details-lines"><div className="details-line details-line-head"><span>Nội dung</span><span>SL</span><span>Đơn giá</span><span>Thành tiền</span></div>{invoice.lines.map((line) => <div className="details-line" key={line.id}><span><strong>{line.productName}</strong><small>{line.sku} · {line.unitName} · BH {line.warrantyPeriod || '—'}</small></span><span>{line.quantity.toLocaleString('vi-VN')}</span><span>{currency.format(line.unitPrice)}</span><span>{currency.format(line.lineTotal)}</span></div>)}</div>
          <div className="details-totals"><div><span>Tạm tính</span><strong>{currency.format(invoice.subtotal)}</strong></div>{invoice.discounts.map((discount) => <div className="discount" key={discount.id || `${discount.source}-${discount.name}`}><span>{discount.source === 'Voucher' ? 'Voucher' : discount.source === 'Manual' ? 'Giảm nhập tay' : 'Khuyến mãi'} · {discount.name}</span><strong>− {currency.format(discount.amount)}</strong></div>)}<div className="grand"><span>Tổng cộng</span><strong>{currency.format(invoice.totalAmount)}</strong></div></div>
        </section>

        <section className="sales-details-section">
          <h3><CreditCard size={18} /> Lịch sử thanh toán</h3>
          <div className="payment-history">{invoice.payments.map((payment) => <article className={payment.isScheduled || invoiceRepairStatus !== 'Returned' ? 'is-scheduled' : ''} key={payment.id}><span className="payment-method-icon">{payment.method === 'Cash' ? <Banknote size={17} /> : <Building2 size={17} />}</span><div><strong>{payment.isScheduled ? `Dự kiến về ${payment.paymentAccountName || 'tài khoản banking'}` : payment.method === 'Cash' ? 'Tiền mặt' : payment.paymentAccountName || 'Chuyển khoản'}</strong><small>{invoiceRepairStatus === 'Returned' ? 'Đã ghi nhận' : 'Sẽ ghi nhận khi phiếu được xử lý'} · {dateTime.format(new Date(payment.paidAtUtc))}</small></div><b>{currency.format(payment.amount)}</b></article>)}{invoice.payments.length === 0 && <p>Chưa phân bổ nguồn tiền.</p>}</div>
        </section>

        {invoice.isInstallment && <section className="installment-detail"><CalendarClock size={18} /><div><strong>Khoản tiền góp đã tự động lên lịch</strong><p>{invoice.installmentProvider || 'Chưa ghi đơn vị'}{invoice.installmentReference ? ` · Hồ sơ ${invoice.installmentReference}` : ''}{invoice.installmentPaymentAccountName ? ` · Tiền góp về ${invoice.installmentPaymentAccountName}` : ''}{nextScheduledPayment ? ` · ${dateTime.format(new Date(nextScheduledPayment.paidAtUtc))}` : ''}</p></div><b>{currency.format(scheduledAmount)}</b></section>}

      </>}
    </section>
  </div>
  {invoice && createPortal(<InvoicePrintTemplate invoice={invoice} mode={printMode} />, document.body)}
  </>
}

function InvoicePrintTemplate({ invoice, mode }: { invoice: SalesInvoiceDetails; mode: 'a4' | 'bill' }) {
  const printedAt = formatPrintDateTime(new Date())
  const invoiceDate = formatPrintDate(new Date(invoice.invoiceDateUtc))
  const companyPhone = invoice.company.phone?.replace(/^hotline:\s*/i, '')

  return <article className={`invoice-print-template print-${mode}`} aria-hidden="true">
    <header className="print-invoice-header">
      <div className="print-company-block">
        {invoice.company.logoDataUrl && <img src={invoice.company.logoDataUrl} alt="Logo công ty" />}
        <div><h1>{invoice.company.name || 'MỸ THO LAPTOP'}</h1>{invoice.company.address && <p>{invoice.company.address}</p>}{companyPhone && <p>Hotline: {companyPhone}</p>}{invoice.company.taxCode && <p className="print-company-tax">MST: {invoice.company.taxCode}</p>}</div>
      </div>
      <div className="print-document-title"><h2>{invoice.type === 'Repair' ? 'PHIẾU TIẾP NHẬN' : 'HÓA ĐƠN'}</h2><div className="print-document-meta"><span><b>Số HĐ:</b> {invoice.number}</span><span><b>Ngày xuất:</b> {invoiceDate}</span></div></div>
    </header>

    <section className="print-customer-order">
      <div><h3>THÔNG TIN KHÁCH HÀNG</h3><p className="print-customer-line"><span><b>Khách hàng:</b> {invoice.customerName}</span><span><b>Điện thoại:</b> {invoice.customerPhone || '—'}</span></p>{invoice.customerAddress && <p className="print-customer-address"><b>Địa chỉ:</b> {invoice.customerAddress}</p>}</div>
      <div className="print-order-line"><h3>THÔNG TIN ĐƠN HÀNG</h3><p><b>Người lập:</b> {invoice.issuer.name}</p>{invoice.type === 'Repair' && invoice.expectedReturnAtUtc && <p><b>Dự kiến trả:</b> {formatPrintDate(new Date(invoice.expectedReturnAtUtc))}</p>}</div>
    </section>

    {(invoice.repairs?.length || invoice.repair) && <section className="print-repair-info">{(invoice.repairs?.length ? invoice.repairs : invoice.repair ? [invoice.repair] : []).map((repair, index) => <div key={repair.id || `${repair.deviceModel}-${index}`}><p><b>Máy {index + 1}:</b> {repair.deviceModel}</p><p className="print-repair-meta"><span><b>Phụ kiện nhận kèm:</b> {repair.accessories || '—'}</span><span><b>Tình trạng:</b> {repair.deviceCondition || '—'}</span><span><b>Bảo hành:</b> {repair.warrantyPeriod || '—'}</span></p><p><b>Dịch vụ:</b> {repair.services?.length ? repair.services.map((service) => `${service.name}${service.notes ? ` — ${service.notes}` : ''} (${formatPrintMoney(service.amount)} đ)`).join(', ') : repair.serviceTypes?.map((item) => item.name).join(', ') || '—'}</p></div>)}</section>}

    {invoice.printNotes && <section className="print-invoice-note"><b>Ghi chú:</b> <span>{invoice.printNotes}</span></section>}

    <table className="print-lines-table">
      <thead><tr><th>STT</th><th>HÀNG HÓA / DỊCH VỤ</th><th>SL</th><th>ĐƠN GIÁ</th><th>THÀNH TIỀN</th></tr></thead>
      <tbody>
        {invoice.lines.map((line, index) => <tr key={line.id}><td>{index + 1}</td><td>{line.productName}<small>{line.unitName}{line.warrantyPeriod ? ` · Bảo hành ${line.warrantyPeriod}` : ''}</small></td><td>{line.quantity.toLocaleString('vi-VN')}</td><td>{formatPrintMoney(line.unitPrice)}</td><td>{formatPrintMoney(line.lineTotal)}</td></tr>)}
        {invoice.discounts.map((discount, index) => <tr className="print-discount-row" key={discount.id || `${discount.source}-${index}`}><td>{invoice.lines.length + index + 1}</td><td>{discount.source === 'Voucher' ? `Voucher ${discount.code || ''} — ${discount.name}` : discount.name}</td><td>1</td><td>−{formatPrintMoney(discount.amount)}</td><td>−{formatPrintMoney(discount.amount)}</td></tr>)}
      </tbody>
    </table>

    <section className="print-payment-summary">
      <h3>THÔNG TIN THANH TOÁN</h3>
      <dl><div><dt>Tổng tiền hàng hóa:</dt><dd>{formatPrintMoney(invoice.subtotal)} đ</dd></div><div><dt>Tiền chiết khấu:</dt><dd>{formatPrintMoney(invoice.discountAmount)} đ</dd></div><div className="print-total"><dt>Thanh toán:</dt><dd>{formatPrintMoney(invoice.totalAmount)} đ</dd></div>{invoice.outstandingAmount > 0 && <div><dt>Còn phải thu:</dt><dd>{formatPrintMoney(invoice.outstandingAmount)} đ</dd></div>}</dl>
    </section>

    <p className="print-amount-words">SỐ TIỀN (BẰNG CHỮ): {numberToVietnamese(invoice.totalAmount).toUpperCase()}.</p>
    <section className="print-signatures"><div><b>Khách hàng</b><span>Ký, họ tên</span></div><div><b>Thủ kho</b><span>Ký, họ tên</span></div><div><b>Triển khai</b><span>Ký, họ tên</span></div><div><b>Người bán hàng</b><span>Ký, họ tên</span></div></section>
    <footer>Hệ thống phần mềm MTL TECHNOLOGY — Ngày in: {printedAt}</footer>
  </article>
}

function formatPrintMoney(value: number) {
  return Math.round(value).toLocaleString('vi-VN')
}

function formatPrintDateTime(value: Date) {
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${pad(value.getDate())}/${pad(value.getMonth() + 1)}/${value.getFullYear()} ${pad(value.getHours())}:${pad(value.getMinutes())}`
}

function formatPrintDate(value: Date) {
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${pad(value.getDate())}/${pad(value.getMonth() + 1)}/${value.getFullYear()}`
}

function numberToVietnamese(value: number) {
  const number = Math.max(0, Math.round(value))
  if (number === 0) return 'Không đồng chẵn'
  const scale = ['', 'nghìn', 'triệu', 'tỷ', 'nghìn tỷ', 'triệu tỷ']
  const groups: number[] = []
  let remaining = number
  while (remaining > 0) {
    groups.push(remaining % 1000)
    remaining = Math.floor(remaining / 1000)
  }
  const words: string[] = []
  for (let index = groups.length - 1; index >= 0; index -= 1) {
    const group = groups[index] ?? 0
    if (group === 0) continue
    words.push(readThreeDigits(group, index < groups.length - 1 && group < 100))
    const scaleName = scale[index]
    if (scaleName) words.push(scaleName)
  }
  return `${words.join(' ').replace(/\s+/g, ' ').trim()} đồng chẵn`.replace(/^./, (letter) => letter.toUpperCase())
}

function readThreeDigits(value: number, full: boolean) {
  const digits = ['không', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín']
  const digit = (index: number) => digits[index] ?? ''
  const hundred = Math.floor(value / 100)
  const ten = Math.floor((value % 100) / 10)
  const unit = value % 10
  const words: string[] = []
  if (hundred > 0 || full) words.push(`${digit(hundred)} trăm`)
  if (ten > 1) {
    words.push(`${digit(ten)} mươi`)
    if (unit === 1) words.push('mốt')
    else if (unit === 5) words.push('lăm')
    else if (unit > 0) words.push(digit(unit))
  } else if (ten === 1) {
    words.push('mười')
    if (unit === 5) words.push('lăm')
    else if (unit > 0) words.push(digit(unit))
  } else if (unit > 0) {
    if (hundred > 0 || full) words.push('lẻ')
    words.push(digit(unit))
  }
  return words.join(' ')
}

function repairStatusLabel(status?: RepairStatus) {
  if (status === 'Returned') return 'Đã xử lý'
  if (status === 'Cancelled') return 'Đã hủy'
  return 'Đang xử lý'
}

function repairStatusTone(status?: RepairStatus): 'success' | 'danger' | 'warning' | 'neutral' {
  if (status === 'ReadyForReturn' || status === 'Returned') return 'success'
  if (status === 'Cancelled') return 'danger'
  if (status === 'Diagnosing' || status === 'Quoted' || status === 'InProgress') return 'warning'
  return 'neutral'
}

