import { BadgePercent, Building2, HandCoins, Pencil, Plus, Save, Tags, TicketPercent, Trash2, X } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { api } from '../lib/api'
import type { DiscountType, InstallmentProvider, PaymentAccount, Promotion, ServiceType, Voucher } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

type ManagementTab = 'accounts' | 'installment-providers' | 'promotions' | 'vouchers' | 'service-types'
type CatalogKind = 'payment-accounts' | 'installment-providers' | 'promotions' | 'vouchers' | 'service-types'
type CatalogEditor =
  | { kind: 'payment-accounts'; item: PaymentAccount }
  | { kind: 'installment-providers'; item: InstallmentProvider }
  | { kind: 'promotions'; item: Promotion }
  | { kind: 'vouchers'; item: Voucher }
  | { kind: 'service-types'; item: ServiceType }

export function SalesManagementDialog({ onClose }: { onClose: () => void }) {
  const [tab, setTab] = useState<ManagementTab>('accounts')
  const [editor, setEditor] = useState<CatalogEditor | null>(null)
  const queryClient = useQueryClient()
  const accountsQuery = useQuery({ queryKey: ['sales-payment-accounts'], queryFn: () => api.get<PaymentAccount[]>('/api/sales/payment-accounts') })
  const installmentProvidersQuery = useQuery({ queryKey: ['sales-installment-providers'], queryFn: () => api.get<InstallmentProvider[]>('/api/sales/installment-providers') })
  const promotionsQuery = useQuery({ queryKey: ['sales-promotions'], queryFn: () => api.get<Promotion[]>('/api/sales/promotions') })
  const vouchersQuery = useQuery({ queryKey: ['sales-vouchers'], queryFn: () => api.get<Voucher[]>('/api/sales/vouchers') })
  const serviceTypesQuery = useQuery({ queryKey: ['sales-service-types'], queryFn: () => api.get<ServiceType[]>('/api/sales/service-types') })

  const accountMutation = useMutation({
    mutationFn: (body: object) => api.post('/api/sales/payment-accounts', body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-payment-accounts'] }),
  })
  const installmentProviderMutation = useMutation({
    mutationFn: (body: object) => api.post('/api/sales/installment-providers', body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-installment-providers'] }),
  })
  const promotionMutation = useMutation({
    mutationFn: (body: object) => api.post('/api/sales/promotions', body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-promotions'] }),
  })
  const voucherMutation = useMutation({
    mutationFn: (body: object) => api.post('/api/sales/vouchers', body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-vouchers'] }),
  })
  const serviceTypeMutation = useMutation({
    mutationFn: (body: object) => api.post('/api/sales/service-types', body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-service-types'] }),
  })
  const editMutation = useMutation({
    mutationFn: ({ kind, id, body }: { kind: CatalogKind; id: string; body: object }) => api.put(`/api/sales/${kind}/${id}`, body),
    onSuccess: (_result, variables) => {
      queryClient.invalidateQueries({ queryKey: [queryKeyFor(variables.kind)] })
      setEditor(null)
    },
  })
  const deleteMutation = useMutation({
    mutationFn: ({ kind, id }: { kind: CatalogKind; id: string }) => api.delete(`/api/sales/${kind}/${id}`),
    onSuccess: (_result, variables) => queryClient.invalidateQueries({ queryKey: [queryKeyFor(variables.kind)] }),
  })

  function removeItem(kind: CatalogKind, id: string, label: string) {
    if (!window.confirm(`Xóa “${label}”? Dữ liệu này sẽ không còn xuất hiện trong danh mục.`)) return
    deleteMutation.mutate({ kind, id })
  }

  function createAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    accountMutation.mutate({
      name: form.get('name'),
      bankName: form.get('bankName'),
      accountNumber: form.get('accountNumber'),
      accountHolder: form.get('accountHolder') || null,
      isActive: true,
      displayOrder: 0,
    }, { onSuccess: () => formElement.reset() })
  }

  function createPromotion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    promotionMutation.mutate({
      name: form.get('name'),
      description: form.get('description') || null,
      discountType: form.get('discountType') as DiscountType,
      value: numberValue(form.get('value')),
      minimumOrderAmount: numberValue(form.get('minimumOrderAmount')),
      maximumDiscountAmount: optionalNumber(form.get('maximumDiscountAmount')),
      startsAtUtc: optionalDate(form.get('startsAtUtc')),
      endsAtUtc: optionalDate(form.get('endsAtUtc')),
      isActive: true,
    }, { onSuccess: () => formElement.reset() })
  }

  function createInstallmentProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    installmentProviderMutation.mutate({
      name: form.get('name'),
      code: form.get('code') || null,
      isActive: true,
      displayOrder: numberValue(form.get('displayOrder')),
    }, { onSuccess: () => formElement.reset() })
  }

  function createVoucher(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    voucherMutation.mutate({
      code: form.get('code'),
      name: form.get('name'),
      discountType: form.get('discountType') as DiscountType,
      value: numberValue(form.get('value')),
      minimumOrderAmount: numberValue(form.get('minimumOrderAmount')),
      maximumDiscountAmount: optionalNumber(form.get('maximumDiscountAmount')),
      startsAtUtc: optionalDate(form.get('startsAtUtc')),
      endsAtUtc: optionalDate(form.get('endsAtUtc')),
      usageLimit: optionalNumber(form.get('usageLimit')),
      isActive: true,
    }, { onSuccess: () => formElement.reset() })
  }

  function createServiceType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    serviceTypeMutation.mutate({
      name: form.get('name'),
      code: form.get('code') || null,
      isActive: true,
      displayOrder: numberValue(form.get('displayOrder')),
    }, { onSuccess: () => formElement.reset() })
  }

  const error = accountMutation.error ?? installmentProviderMutation.error ?? promotionMutation.error ?? voucherMutation.error ?? serviceTypeMutation.error ?? editMutation.error ?? deleteMutation.error

  return <div className="modal-backdrop sales-management-backdrop" onMouseDown={onClose}>
    <section className="modal-card sales-management-dialog" onMouseDown={(event) => event.stopPropagation()}>
      <div className="modal-head">
        <div><span className="eyebrow accent">CẤU HÌNH BÁN HÀNG</span><h2>Danh mục bán hàng</h2><p>Quản lý nơi nhận tiền, đơn vị trả góp, giảm giá và loại dịch vụ.</p></div>
        <button type="button" className="icon-button" onClick={onClose} aria-label="Đóng"><X size={20} /></button>
      </div>

      <nav className="sales-management-tabs">
        <button type="button" className={tab === 'accounts' ? 'is-active' : ''} onClick={() => setTab('accounts')}><Building2 size={17} /> Tài khoản banking</button>
        <button type="button" className={tab === 'installment-providers' ? 'is-active' : ''} onClick={() => setTab('installment-providers')}><HandCoins size={17} /> Đơn vị trả góp</button>
        <button type="button" className={tab === 'promotions' ? 'is-active' : ''} onClick={() => setTab('promotions')}><BadgePercent size={17} /> Khuyến mãi</button>
        <button type="button" className={tab === 'vouchers' ? 'is-active' : ''} onClick={() => setTab('vouchers')}><TicketPercent size={17} /> Voucher</button>
        <button type="button" className={tab === 'service-types' ? 'is-active' : ''} onClick={() => setTab('service-types')}><Tags size={17} /> Loại dịch vụ</button>
      </nav>

      {error && <div className="form-alert">{error.message}</div>}

      {tab === 'accounts' && <div className="sales-management-content">
        <form className="sales-management-form" onSubmit={createAccount}>
          <h3><Plus size={17} /> Thêm tài khoản nhận tiền</h3>
          <div className="form-grid">
            <label><span>Tên dễ nhớ *</span><input name="name" required placeholder="Ví dụ: Vietcombank công ty" /></label>
            <label><span>Ngân hàng *</span><input name="bankName" required placeholder="Vietcombank" /></label>
            <label><span>Số tài khoản *</span><input name="accountNumber" required /></label>
            <label><span>Chủ tài khoản</span><input name="accountHolder" /></label>
          </div>
          <button className="primary-button" disabled={accountMutation.isPending}><Plus size={16} /> Thêm tài khoản</button>
        </form>
        <div className="sales-management-list">
          <h3>Đang sử dụng</h3>
          {accountsQuery.data?.map((account) => <article key={account.id}><span className="management-icon"><Building2 size={18} /></span><div><strong>{account.name}</strong><small>{account.bankName} · {account.accountNumber}</small><small>{account.accountHolder}</small></div><span className={account.isActive ? 'management-state active' : 'management-state'}>{account.isActive ? 'Hoạt động' : 'Đã tắt'}</span><span className="management-actions"><button type="button" onClick={() => setEditor({ kind: 'payment-accounts', item: account })} aria-label={`Sửa ${account.name}`}><Pencil size={15} /></button><button type="button" className="danger" onClick={() => removeItem('payment-accounts', account.id, account.name)} aria-label={`Xóa ${account.name}`}><Trash2 size={15} /></button></span></article>)}
          {!accountsQuery.isLoading && accountsQuery.data?.length === 0 && <p className="management-empty">Chưa có tài khoản banking. Thêm tài khoản trước khi ghi nhận chuyển khoản.</p>}
        </div>
      </div>}

      {tab === 'installment-providers' && <div className="sales-management-content">
        <form className="sales-management-form" onSubmit={createInstallmentProvider}>
          <h3><Plus size={17} /> Thêm đơn vị trả góp</h3>
          <div className="form-grid">
            <label><span>Tên đơn vị *</span><input name="name" required placeholder="Ví dụ: HD Saison" /></label>
            <label><span>Mã</span><input name="code" placeholder="Tự tạo từ tên nếu để trống" /></label>
            <label><span>Thứ tự hiển thị</span><input name="displayOrder" type="number" defaultValue={100} /></label>
          </div>
          <button className="primary-button" disabled={installmentProviderMutation.isPending}><Plus size={16} /> Thêm đơn vị</button>
        </form>
        <div className="sales-management-list">
          <h3>Đơn vị trả góp hiện có</h3>
          {installmentProvidersQuery.data?.map((item) => <article key={item.id}><span className="management-icon"><HandCoins size={18} /></span><div><strong>{item.name}</strong><small>{item.code} · Thứ tự {item.displayOrder}</small></div><span className={item.isActive ? 'management-state active' : 'management-state'}>{item.isActive ? 'Hoạt động' : 'Đã tắt'}</span><span className="management-actions"><button type="button" onClick={() => setEditor({ kind: 'installment-providers', item })} aria-label={`Sửa ${item.name}`}><Pencil size={15} /></button><button type="button" className="danger" onClick={() => removeItem('installment-providers', item.id, item.name)} aria-label={`Xóa ${item.name}`}><Trash2 size={15} /></button></span></article>)}
          {!installmentProvidersQuery.isLoading && installmentProvidersQuery.data?.length === 0 && <p className="management-empty">Chưa có đơn vị trả góp.</p>}
        </div>
      </div>}

      {tab === 'promotions' && <div className="sales-management-content">
        <form className="sales-management-form" onSubmit={createPromotion}>
          <h3><Plus size={17} /> Thêm chương trình khuyến mãi</h3>
          <DiscountFields />
          <div className="form-grid management-dates">
            <label><span>Bắt đầu</span><input name="startsAtUtc" type="datetime-local" /></label>
            <label><span>Kết thúc</span><input name="endsAtUtc" type="datetime-local" /></label>
          </div>
          <button className="primary-button" disabled={promotionMutation.isPending}><Plus size={16} /> Thêm khuyến mãi</button>
        </form>
        <div className="sales-management-list">
          <h3>Chương trình hiện có</h3>
          {promotionsQuery.data?.map((promotion) => <article key={promotion.id}><span className="management-icon"><BadgePercent size={18} /></span><div><strong>{promotion.name}</strong><small>{discountLabel(promotion.discountType, promotion.value)} · Đơn tối thiểu {currency.format(promotion.minimumOrderAmount)}</small>{promotion.description && <small>{promotion.description}</small>}</div><span className={promotion.isActive ? 'management-state active' : 'management-state'}>{promotion.isActive ? 'Hoạt động' : 'Đã tắt'}</span><span className="management-actions"><button type="button" onClick={() => setEditor({ kind: 'promotions', item: promotion })} aria-label={`Sửa ${promotion.name}`}><Pencil size={15} /></button><button type="button" className="danger" onClick={() => removeItem('promotions', promotion.id, promotion.name)} aria-label={`Xóa ${promotion.name}`}><Trash2 size={15} /></button></span></article>)}
        </div>
      </div>}

      {tab === 'vouchers' && <div className="sales-management-content">
        <form className="sales-management-form" onSubmit={createVoucher}>
          <h3><Plus size={17} /> Tạo mã voucher</h3>
          <div className="form-grid">
            <label><span>Mã voucher *</span><input name="code" required placeholder="MTL200K" style={{ textTransform: 'uppercase' }} /></label>
            <label><span>Tên chương trình *</span><input name="name" required /></label>
          </div>
          <DiscountFields hideName />
          <div className="form-grid management-dates">
            <label><span>Bắt đầu</span><input name="startsAtUtc" type="datetime-local" /></label>
            <label><span>Kết thúc</span><input name="endsAtUtc" type="datetime-local" /></label>
            <label><span>Giới hạn lượt dùng</span><input name="usageLimit" type="number" min={1} step={1} placeholder="Không giới hạn" /></label>
          </div>
          <button className="primary-button" disabled={voucherMutation.isPending}><Plus size={16} /> Tạo voucher</button>
        </form>
        <div className="sales-management-list">
          <h3>Voucher hiện có</h3>
          {vouchersQuery.data?.map((voucher) => <article key={voucher.id}><span className="management-icon"><TicketPercent size={18} /></span><div><strong>{voucher.code} · {voucher.name}</strong><small>{discountLabel(voucher.discountType, voucher.value)} · Đã dùng {voucher.usedCount}{voucher.usageLimit ? `/${voucher.usageLimit}` : ''} lượt</small></div><span className={voucher.isActive ? 'management-state active' : 'management-state'}>{voucher.isActive ? 'Hoạt động' : 'Đã tắt'}</span><span className="management-actions"><button type="button" onClick={() => setEditor({ kind: 'vouchers', item: voucher })} aria-label={`Sửa ${voucher.code}`}><Pencil size={15} /></button><button type="button" className="danger" onClick={() => removeItem('vouchers', voucher.id, voucher.code)} aria-label={`Xóa ${voucher.code}`}><Trash2 size={15} /></button></span></article>)}
          {!vouchersQuery.isLoading && vouchersQuery.data?.length === 0 && <p className="management-empty">Chưa có voucher.</p>}
        </div>
      </div>}

      {tab === 'service-types' && <div className="sales-management-content">
        <form className="sales-management-form" onSubmit={createServiceType}>
          <h3><Plus size={17} /> Thêm loại dịch vụ</h3>
          <div className="form-grid">
            <label><span>Tên loại dịch vụ *</span><input name="name" required placeholder="Ví dụ: Thay pin" /></label>
            <label><span>Mã</span><input name="code" placeholder="Tự tạo từ tên nếu để trống" /></label>
            <label><span>Thứ tự hiển thị</span><input name="displayOrder" type="number" defaultValue={100} /></label>
          </div>
          <button className="primary-button" disabled={serviceTypeMutation.isPending}><Plus size={16} /> Thêm loại dịch vụ</button>
        </form>
        <div className="sales-management-list">
          <h3>Loại dịch vụ hiện có</h3>
          {serviceTypesQuery.data?.map((item) => <article key={item.id}><span className="management-icon"><Tags size={18} /></span><div><strong>{item.name}</strong><small>{item.code} · Thứ tự {item.displayOrder}</small></div><span className={item.isActive ? 'management-state active' : 'management-state'}>{item.isActive ? 'Hoạt động' : 'Đã tắt'}</span><span className="management-actions"><button type="button" onClick={() => setEditor({ kind: 'service-types', item })} aria-label={`Sửa ${item.name}`}><Pencil size={15} /></button><button type="button" className="danger" onClick={() => removeItem('service-types', item.id, item.name)} aria-label={`Xóa ${item.name}`}><Trash2 size={15} /></button></span></article>)}
          {!serviceTypesQuery.isLoading && serviceTypesQuery.data?.length === 0 && <p className="management-empty">Chưa có loại dịch vụ.</p>}
        </div>
      </div>}
    </section>
    {editor && <CatalogEditorDialog editor={editor} pending={editMutation.isPending} onClose={() => setEditor(null)} onSave={(body) => editMutation.mutate({ kind: editor.kind, id: editor.item.id, body })} />}
  </div>
}

function CatalogEditorDialog({ editor, pending, onClose, onSave }: { editor: CatalogEditor; pending: boolean; onClose: () => void; onSave: (body: object) => void }) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    if (editor.kind === 'payment-accounts') {
      onSave({
        name: form.get('name'),
        bankName: form.get('bankName'),
        accountNumber: form.get('accountNumber'),
        accountHolder: form.get('accountHolder') || null,
        isActive: form.get('isActive') === 'on',
        displayOrder: numberValue(form.get('displayOrder')),
      })
      return
    }
    if (editor.kind === 'service-types') {
      onSave({
        name: form.get('name'),
        code: form.get('code') || null,
        isActive: form.get('isActive') === 'on',
        displayOrder: numberValue(form.get('displayOrder')),
      })
      return
    }
    if (editor.kind === 'installment-providers') {
      onSave({
        name: form.get('name'),
        code: form.get('code') || null,
        isActive: form.get('isActive') === 'on',
        displayOrder: numberValue(form.get('displayOrder')),
      })
      return
    }
    const common = {
      discountType: form.get('discountType') as DiscountType,
      value: numberValue(form.get('value')),
      minimumOrderAmount: numberValue(form.get('minimumOrderAmount')),
      maximumDiscountAmount: optionalNumber(form.get('maximumDiscountAmount')),
      startsAtUtc: optionalDate(form.get('startsAtUtc')),
      endsAtUtc: optionalDate(form.get('endsAtUtc')),
      isActive: form.get('isActive') === 'on',
    }
    if (editor.kind === 'promotions') {
      onSave({ ...common, name: form.get('name'), description: form.get('description') || null })
      return
    }
    onSave({ ...common, code: form.get('code'), name: form.get('name'), usageLimit: optionalNumber(form.get('usageLimit')) })
  }

  const account = editor.kind === 'payment-accounts' ? editor.item : null
  const installmentProvider = editor.kind === 'installment-providers' ? editor.item : null
  const promotion = editor.kind === 'promotions' ? editor.item : null
  const voucher = editor.kind === 'vouchers' ? editor.item : null
  const serviceType = editor.kind === 'service-types' ? editor.item : null
  const discountItem = promotion ?? voucher
  return <div className="catalog-editor-layer" onMouseDown={onClose}>
    <form className="catalog-editor-card" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
      <div className="modal-head"><div><span className="eyebrow accent">CHỈNH SỬA DANH MỤC</span><h2>{editor.kind === 'payment-accounts' ? 'Tài khoản banking' : editor.kind === 'installment-providers' ? 'Đơn vị trả góp' : editor.kind === 'promotions' ? 'Chương trình khuyến mãi' : editor.kind === 'service-types' ? 'Loại dịch vụ' : 'Mã voucher'}</h2></div><button type="button" className="icon-button" onClick={onClose}><X size={19} /></button></div>
      {editor.kind === 'service-types' ? <div className="form-grid">
        <label><span>Tên loại dịch vụ *</span><input name="name" required defaultValue={serviceType?.name} /></label>
        <label><span>Mã *</span><input name="code" required defaultValue={serviceType?.code} /></label>
        <label><span>Thứ tự hiển thị</span><input name="displayOrder" type="number" defaultValue={serviceType?.displayOrder} /></label>
      </div> : editor.kind === 'installment-providers' ? <div className="form-grid">
        <label><span>Tên đơn vị *</span><input name="name" required defaultValue={installmentProvider?.name} /></label>
        <label><span>Mã *</span><input name="code" required defaultValue={installmentProvider?.code} /></label>
        <label><span>Thứ tự hiển thị</span><input name="displayOrder" type="number" defaultValue={installmentProvider?.displayOrder} /></label>
      </div> : editor.kind === 'payment-accounts' ? <div className="form-grid">
        <label><span>Tên dễ nhớ *</span><input name="name" required defaultValue={account?.name} /></label>
        <label><span>Ngân hàng *</span><input name="bankName" required defaultValue={account?.bankName} /></label>
        <label><span>Số tài khoản *</span><input name="accountNumber" required defaultValue={account?.accountNumber} /></label>
        <label><span>Chủ tài khoản</span><input name="accountHolder" defaultValue={account?.accountHolder} /></label>
        <label><span>Thứ tự hiển thị</span><input name="displayOrder" type="number" defaultValue={account?.displayOrder} /></label>
      </div> : <>
        <div className="form-grid">
          {voucher && <label><span>Mã voucher *</span><input name="code" required defaultValue={voucher.code} style={{ textTransform: 'uppercase' }} /></label>}
          <label><span>Tên chương trình *</span><input name="name" required defaultValue={discountItem?.name} /></label>
          {promotion && <label><span>Mô tả</span><input name="description" defaultValue={promotion.description ?? ''} /></label>}
          <label><span>Loại giảm</span><select name="discountType" defaultValue={discountItem?.discountType}><option value="FixedAmount">Số tiền cố định</option><option value="Percentage">Phần trăm</option></select></label>
          <label><span>Giá trị *</span><input name="value" type="number" min={1} required defaultValue={discountItem?.value} /></label>
          <label><span>Đơn tối thiểu</span><input name="minimumOrderAmount" type="number" min={0} defaultValue={discountItem?.minimumOrderAmount} /></label>
          <label><span>Giảm tối đa</span><input name="maximumDiscountAmount" type="number" min={0} defaultValue={discountItem?.maximumDiscountAmount ?? ''} /></label>
          <label><span>Bắt đầu</span><input name="startsAtUtc" type="datetime-local" defaultValue={toDateTimeInput(discountItem?.startsAtUtc)} /></label>
          <label><span>Kết thúc</span><input name="endsAtUtc" type="datetime-local" defaultValue={toDateTimeInput(discountItem?.endsAtUtc)} /></label>
          {voucher && <label><span>Giới hạn lượt dùng</span><input name="usageLimit" type="number" min={1} defaultValue={voucher.usageLimit ?? ''} /></label>}
        </div>
      </>}
      <label className="catalog-active-toggle"><input name="isActive" type="checkbox" defaultChecked={editor.item.isActive} /><span>Đang hoạt động</span></label>
      <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Hủy</button><button className="primary-button" disabled={pending}><Save size={16} /> {pending ? 'Đang lưu…' : 'Lưu thay đổi'}</button></div>
    </form>
  </div>
}

function DiscountFields({ hideName = false }: { hideName?: boolean }) {
  return <div className="form-grid discount-fields">
    {!hideName && <><label><span>Tên chương trình *</span><input name="name" required /></label><label><span>Mô tả</span><input name="description" placeholder="Điều kiện áp dụng…" /></label></>}
    <label><span>Loại giảm</span><select name="discountType"><option value="FixedAmount">Số tiền cố định</option><option value="Percentage">Phần trăm</option></select></label>
    <label><span>Giá trị *</span><input name="value" type="number" min={1} step={1} required /></label>
    <label><span>Đơn tối thiểu</span><input name="minimumOrderAmount" type="number" min={0} step={1000} defaultValue={0} /></label>
    <label><span>Giảm tối đa</span><input name="maximumDiscountAmount" type="number" min={0} step={1000} placeholder="Không giới hạn" /></label>
  </div>
}

function numberValue(value: FormDataEntryValue | null) {
  return Number(value || 0)
}

function optionalNumber(value: FormDataEntryValue | null) {
  return value === null || value === '' ? null : Number(value)
}

function optionalDate(value: FormDataEntryValue | null) {
  return value ? new Date(String(value)).toISOString() : null
}

function discountLabel(type: DiscountType, value: number) {
  return type === 'Percentage' ? `Giảm ${value}%` : `Giảm ${currency.format(value)}`
}

function queryKeyFor(kind: CatalogKind) {
  return kind === 'payment-accounts' ? 'sales-payment-accounts' : kind === 'installment-providers' ? 'sales-installment-providers' : kind === 'promotions' ? 'sales-promotions' : kind === 'service-types' ? 'sales-service-types' : 'sales-vouchers'
}

function toDateTimeInput(value?: string) {
  if (!value) return ''
  const date = new Date(value)
  const offset = date.getTimezoneOffset()
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16)
}
