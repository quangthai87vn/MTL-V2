import { Banknote, Building2, PackagePlus, Plus, Save, Trash2, X } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { api } from '../lib/api'
import type { PagedResult, PaymentAccount, PaymentMethod, Product, PurchaseInvoiceDetails, PurchaseStatus, Supplier } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const money = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })

type LineDraft = {
  key: string; productId?: string; sku: string; productName: string; quantity: number; receivedQuantity: number
  unitName: string; unitCost: number; discountAmount: number; vatRate?: number; serials: string; notes: string
}
type PaymentDraft = { key: string; method: PaymentMethod; paymentAccountId: string; amount: number; paidAt: string; notes: string }

export function PurchaseInvoiceDialog({ invoiceId, onClose, onSaved }: { invoiceId?: string; onClose: () => void; onSaved: (id: string) => void }) {
  const queryClient = useQueryClient()
  const now = toLocal(new Date())
  const [supplierId, setSupplierId] = useState('')
  const [status, setStatus] = useState<PurchaseStatus>('InProgress')
  const [invoiceDate, setInvoiceDate] = useState(now)
  const [receivedAt, setReceivedAt] = useState(now)
  const [dueDate, setDueDate] = useState('')
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState('')
  const [supplierInvoiceSeries, setSupplierInvoiceSeries] = useState('')
  const [warehouseReceiptNumber, setWarehouseReceiptNumber] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<LineDraft[]>([])
  const [payments, setPayments] = useState<PaymentDraft[]>([])
  const [productSearch, setProductSearch] = useState('')
  const [showSupplierForm, setShowSupplierForm] = useState(false)
  const [supplierForm, setSupplierForm] = useState({ name: '', taxCode: '', phone: '', address: '' })

  const invoiceQuery = useQuery({ queryKey: ['purchase-edit', invoiceId], queryFn: () => api.get<PurchaseInvoiceDetails>(`/api/purchases/${invoiceId}`), enabled: Boolean(invoiceId) })
  const suppliersQuery = useQuery({ queryKey: ['purchase-suppliers'], queryFn: () => api.get<Supplier[]>('/api/purchases/suppliers') })
  const productsQuery = useQuery({ queryKey: ['purchase-products', productSearch], queryFn: () => api.get<PagedResult<Product>>(`/api/products?search=${encodeURIComponent(productSearch)}&page=1&pageSize=100&sort=name_asc`) })
  const accountsQuery = useQuery({ queryKey: ['payment-accounts'], queryFn: () => api.get<PaymentAccount[]>('/api/sales/payment-accounts') })

  useEffect(() => {
    const invoice = invoiceQuery.data
    if (!invoice) return
    setSupplierId(invoice.supplier.id); setStatus(invoice.status)
    setInvoiceDate(toLocal(new Date(invoice.invoiceDateUtc))); setReceivedAt(toLocal(new Date(invoice.receivedAtUtc)))
    setDueDate(invoice.dueDateUtc ? toLocal(new Date(invoice.dueDateUtc)) : '')
    setSupplierInvoiceNumber(invoice.supplierInvoiceNumber ?? ''); setSupplierInvoiceSeries(invoice.supplierInvoiceSeries ?? '')
    setWarehouseReceiptNumber(invoice.warehouseReceiptNumber ?? ''); setNotes(invoice.notes ?? '')
    setLines(invoice.lines.map((line) => ({ key: line.id, productId: line.productId, sku: line.sku, productName: line.productName,
      quantity: line.quantity, receivedQuantity: line.receivedQuantity, unitName: line.unitName, unitCost: line.unitCost,
      discountAmount: line.discountAmount, vatRate: line.vatRate, serials: line.serialNumbers.join('\n'), notes: line.notes ?? '' })))
    setPayments(invoice.payments.map((payment) => ({ key: payment.id, method: payment.method, paymentAccountId: payment.paymentAccountId ?? '',
      amount: payment.amount, paidAt: toLocal(new Date(payment.paidAtUtc)), notes: payment.notes ?? '' })))
  }, [invoiceQuery.data])

  const totals = useMemo(() => lines.reduce((result, line) => {
    const gross = line.quantity * line.unitCost
    const discount = Math.min(gross, line.discountAmount)
    const taxable = gross - discount
    const tax = taxable * (line.vatRate ?? 0) / 100
    return { subtotal: result.subtotal + gross, discount: result.discount + discount, tax: result.tax + tax, total: result.total + taxable + tax }
  }, { subtotal: 0, discount: 0, tax: 0, total: 0 }), [lines])
  const paid = payments.reduce((sum, payment) => sum + payment.amount, 0)

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = {
        supplierId, invoiceDateUtc: fromLocal(invoiceDate), receivedAtUtc: fromLocal(receivedAt), dueDateUtc: dueDate ? fromLocal(dueDate) : null,
        status, supplierInvoiceNumber, supplierInvoiceSeries, warehouseReceiptNumber, notes,
        lines: lines.map((line) => ({ productId: line.productId || null, sku: line.sku, productName: line.productName,
          quantity: line.quantity, receivedQuantity: line.receivedQuantity, unitName: line.unitName, unitCost: line.unitCost,
          discountAmount: line.discountAmount, vatRate: line.vatRate ?? null, serialNumbers: line.serials.split(/[\n,;]/).map((x) => x.trim()).filter(Boolean), notes: line.notes })),
        payments: payments.map((payment) => ({ method: payment.method, paymentAccountId: payment.paymentAccountId || null,
          amount: payment.amount, paidAtUtc: fromLocal(payment.paidAt), notes: payment.notes })),
      }
      return invoiceId ? api.put<PurchaseInvoiceDetails>(`/api/purchases/${invoiceId}`, payload) : api.post<PurchaseInvoiceDetails>('/api/purchases', payload)
    },
    onSuccess: (invoice) => { void queryClient.invalidateQueries({ queryKey: ['purchases'] }); void queryClient.invalidateQueries({ queryKey: ['products'] }); onSaved(invoice.id) },
  })
  const supplierMutation = useMutation({
    mutationFn: () => api.post<Supplier>('/api/purchases/suppliers', { ...supplierForm, contactName: '', email: '', notes: '', isActive: true }),
    onSuccess: (supplier) => { setSupplierId(supplier.id); setShowSupplierForm(false); void queryClient.invalidateQueries({ queryKey: ['purchase-suppliers'] }) },
  })

  function addProduct(product: Product) {
    setLines((items) => [...items, { key: crypto.randomUUID(), productId: product.id, sku: product.sku, productName: product.name,
      quantity: 1, receivedQuantity: 1, unitName: 'Cái', unitCost: product.costPrice, discountAmount: 0, vatRate: 0, serials: '', notes: '' }])
  }
  function addManualLine() { setLines((items) => [...items, { key: crypto.randomUUID(), sku: '', productName: '', quantity: 1, receivedQuantity: 1, unitName: 'Cái', unitCost: 0, discountAmount: 0, vatRate: 0, serials: '', notes: '' }]) }
  function patchLine(key: string, value: Partial<LineDraft>) { setLines((items) => items.map((line) => line.key === key ? { ...line, ...value } : line)) }
  function patchPayment(key: string, value: Partial<PaymentDraft>) { setPayments((items) => items.map((payment) => payment.key === key ? { ...payment, ...value } : payment)) }
  function addPayment(method: PaymentMethod) { setPayments((items) => [...items, { key: crypto.randomUUID(), method, paymentAccountId: '', amount: Math.max(0, totals.total - paid), paidAt: receivedAt, notes: '' }]) }

  return <div className="modal-backdrop purchase-modal-backdrop" role="dialog" aria-modal="true">
    <form className="purchase-editor" onSubmit={(event) => { event.preventDefault(); saveMutation.mutate() }}>
      <header><div><span className="eyebrow accent">{invoiceId ? 'CHỈNH SỬA PHIẾU NHẬP' : 'NHẬP HÀNG MỚI'}</span><h2>{invoiceQuery.data?.number ?? 'Phiếu nhập kho'}</h2></div><button type="button" className="icon-button" onClick={onClose}><X size={22} /></button></header>
      <div className="purchase-editor-body">
        {(saveMutation.isError || supplierMutation.isError) && <div className="form-alert">{(saveMutation.error ?? supplierMutation.error)?.message}</div>}
        <section className="purchase-form-card purchase-document-card">
          <h3>THÔNG TIN CHỨNG TỪ</h3>
          <div className="purchase-form-grid four">
            <label><span>Nhà cung cấp *</span><div className="field-with-action"><select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} required><option value="">Chọn nhà cung cấp…</option>{suppliersQuery.data?.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}{x.taxCode ? ` · MST ${x.taxCode}` : ''}</option>)}</select><button type="button" onClick={() => setShowSupplierForm((value) => !value)}><Plus size={16} /></button></div></label>
            <label><span>Trạng thái xử lý phiếu *</span><select value={status} onChange={(e) => setStatus(e.target.value as PurchaseStatus)}><option value="InProgress">Đang xử lý</option><option value="Processed">Đã xử lý</option><option value="Cancelled">Đã hủy</option></select></label>
            <label><span>Ngày hóa đơn nhà cung cấp *</span><input type="datetime-local" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} required /></label>
            <label><span>Ngày nhận hàng *</span><input type="datetime-local" value={receivedAt} onChange={(e) => setReceivedAt(e.target.value)} required /></label>
            <label><span>Số hóa đơn nhà cung cấp</span><input value={supplierInvoiceNumber} onChange={(e) => setSupplierInvoiceNumber(e.target.value)} /></label>
            <label><span>Ký hiệu hóa đơn</span><input value={supplierInvoiceSeries} onChange={(e) => setSupplierInvoiceSeries(e.target.value)} /></label>
            <label><span>Số phiếu nhập kho</span><input value={warehouseReceiptNumber} onChange={(e) => setWarehouseReceiptNumber(e.target.value)} placeholder="Tự động nếu để trống" /></label>
            <label><span>Hạn thanh toán</span><input type="datetime-local" value={dueDate} onChange={(e) => setDueDate(e.target.value)} /></label>
          </div>
          {showSupplierForm && <div className="supplier-quick-form"><input placeholder="Tên nhà cung cấp *" value={supplierForm.name} onChange={(e) => setSupplierForm({ ...supplierForm, name: e.target.value })} /><input placeholder="Mã số thuế" value={supplierForm.taxCode} onChange={(e) => setSupplierForm({ ...supplierForm, taxCode: e.target.value })} /><input placeholder="Điện thoại" value={supplierForm.phone} onChange={(e) => setSupplierForm({ ...supplierForm, phone: e.target.value })} /><input placeholder="Địa chỉ" value={supplierForm.address} onChange={(e) => setSupplierForm({ ...supplierForm, address: e.target.value })} /><button type="button" className="secondary-button" disabled={!supplierForm.name || supplierMutation.isPending} onClick={() => supplierMutation.mutate()}>Lưu nhà cung cấp</button></div>}
        </section>

        <section className="purchase-form-card">
          <div className="purchase-card-title"><div><h3>HÀNG HÓA NHẬP KHO</h3><p>Số lượng thực nhập là số lượng được cộng vào kho khi phiếu chuyển sang “Đã xử lý”.</p></div><button type="button" className="secondary-button" onClick={addManualLine}><Plus size={17} /> Dòng tự do</button></div>
          <div className="purchase-product-picker"><input value={productSearch} onChange={(e) => setProductSearch(e.target.value)} placeholder="Tìm SKU hoặc tên sản phẩm…" />{productSearch && <div className="purchase-product-results">{productsQuery.data?.items.slice(0, 8).map((product) => <button type="button" key={product.id} onClick={() => { addProduct(product); setProductSearch('') }}><PackagePlus size={16} /><span><strong>{product.name}</strong><small>{product.sku} · Giá vốn {currency.format(product.costPrice)}</small></span></button>)}</div>}</div>
          <div className="purchase-lines">
            {lines.map((line, index) => <article key={line.key} className="purchase-line-card">
              <header><strong>Dòng {index + 1}</strong><button type="button" className="icon-button danger" onClick={() => setLines((items) => items.filter((x) => x.key !== line.key))}><Trash2 size={16} /></button></header>
              <div className="purchase-line-grid">
                <label className="wide"><span>Tên hàng hóa *</span><input value={line.productName} onChange={(e) => patchLine(line.key, { productName: e.target.value })} required /></label>
                <label><span>SKU</span><input value={line.sku} onChange={(e) => patchLine(line.key, { sku: e.target.value })} /></label>
                <label><span>Đơn vị</span><input value={line.unitName} onChange={(e) => patchLine(line.key, { unitName: e.target.value })} /></label>
                <label><span>SL hóa đơn</span><NumberInput value={line.quantity} onChange={(value) => patchLine(line.key, { quantity: value })} /></label>
                <label><span>SL thực nhập</span><NumberInput value={line.receivedQuantity} onChange={(value) => patchLine(line.key, { receivedQuantity: value })} /></label>
                <label><span>Đơn giá chưa VAT</span><MoneyInput value={line.unitCost} onChange={(value) => patchLine(line.key, { unitCost: value })} /></label>
                <label><span>Chiết khấu</span><MoneyInput value={line.discountAmount} onChange={(value) => patchLine(line.key, { discountAmount: value })} /></label>
                <label><span>Thuế GTGT</span><select value={line.vatRate ?? ''} onChange={(e) => patchLine(line.key, { vatRate: e.target.value === '' ? undefined : Number(e.target.value) })}><option value="">KCT</option><option value="0">0%</option><option value="5">5%</option><option value="8">8%</option><option value="10">10%</option></select></label>
                <label className="wide"><span>Serial / Service Tag</span><textarea rows={2} value={line.serials} onChange={(e) => patchLine(line.key, { serials: e.target.value })} placeholder="Mỗi serial một dòng" /></label>
                <label className="wide"><span>Quy cách / ghi chú</span><input value={line.notes} onChange={(e) => patchLine(line.key, { notes: e.target.value })} /></label>
              </div>
            </article>)}
            {lines.length === 0 && <div className="purchase-empty">Chưa có hàng hóa trong phiếu nhập.</div>}
          </div>
        </section>

        <section className="purchase-form-card">
          <div className="purchase-card-title"><div><h3>THANH TOÁN NHÀ CUNG CẤP</h3><p>Có thể để trống để theo dõi công nợ, hoặc ghi nhiều khoản tiền mặt/chuyển khoản.</p></div><div><button type="button" className="secondary-button" onClick={() => addPayment('Cash')}><Banknote size={16} /> Tiền mặt</button><button type="button" className="secondary-button" onClick={() => addPayment('BankTransfer')}><Building2 size={16} /> Chuyển khoản</button></div></div>
          <div className="purchase-payments">{payments.map((payment) => <div key={payment.key} className="purchase-payment-row"><select value={payment.method} onChange={(e) => patchPayment(payment.key, { method: e.target.value as PaymentMethod })}><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option></select>{payment.method === 'BankTransfer' && <select value={payment.paymentAccountId} onChange={(e) => patchPayment(payment.key, { paymentAccountId: e.target.value })} required><option value="">Tài khoản chi…</option>{accountsQuery.data?.filter((x) => x.isActive).map((x) => <option value={x.id} key={x.id}>{x.name} · {x.bankName}</option>)}</select>}<MoneyInput value={payment.amount} onChange={(value) => patchPayment(payment.key, { amount: value })} /><input type="datetime-local" value={payment.paidAt} onChange={(e) => patchPayment(payment.key, { paidAt: e.target.value })} /><button type="button" className="icon-button danger" onClick={() => setPayments((items) => items.filter((x) => x.key !== payment.key))}><Trash2 size={16} /></button></div>)}{payments.length === 0 && <div className="purchase-empty compact">Chưa thanh toán — toàn bộ giá trị sẽ là công nợ nhà cung cấp.</div>}</div>
          <label className="purchase-notes"><span>Ghi chú phiếu nhập</span><textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} /></label>
        </section>
      </div>
      <footer><div className="purchase-totals"><span>Tạm tính <b>{currency.format(totals.subtotal)}</b></span><span>Chiết khấu <b>− {currency.format(totals.discount)}</b></span><span>VAT <b>{currency.format(totals.tax)}</b></span><span className="total">Tổng nhập <b>{currency.format(totals.total)}</b></span><span>Đã trả <b>{currency.format(paid)}</b></span><span>Còn nợ <b>{currency.format(Math.max(0, totals.total - paid))}</b></span></div><div><button type="button" className="secondary-button" onClick={onClose}>Đóng</button><button className="primary-button" disabled={saveMutation.isPending || lines.length === 0 || !supplierId}><Save size={18} /> {saveMutation.isPending ? 'Đang lưu…' : invoiceId ? 'Cập nhật phiếu nhập' : 'Lưu phiếu nhập'}</button></div></footer>
    </form>
  </div>
}

function MoneyInput({ value, onChange }: { value: number; onChange: (value: number) => void }) { return <input type="text" inputMode="numeric" value={money.format(Math.max(0, Math.round(value || 0)))} onChange={(e) => onChange(Number(e.target.value.replace(/\D/g, '')) || 0)} onFocus={(e) => e.currentTarget.select()} /> }
function NumberInput({ value, onChange }: { value: number; onChange: (value: number) => void }) { return <input type="number" min="0" step="0.001" value={value} onChange={(e) => onChange(Number(e.target.value) || 0)} /> }
function toLocal(date: Date) { const offset = date.getTimezoneOffset(); return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16) }
function fromLocal(value: string) { return new Date(value).toISOString() }
