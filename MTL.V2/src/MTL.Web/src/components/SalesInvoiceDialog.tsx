import {
  Banknote,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  ClipboardPenLine,
  Clock3,
  CreditCard,
  History,
  PackagePlus,
  Percent,
  Phone,
  Plus,
  Save,
  Search,
  Trash2,
  UserRound,
  UserRoundCog,
  Wrench,
} from 'lucide-react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useDeferredValue, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { CustomerEditorDialog } from './CustomerEditorDialog'
import { useAuth } from '../auth/AuthProvider'
import { api } from '../lib/api'
import type { Customer, CustomerDetails, CustomerLookup, InstallmentProvider, InvoiceSettings, PagedResult, PaymentAccount, PaymentMethod, Product, Promotion, RepairOrder, RepairStatus, SalesDiscountSource, SalesInvoiceDetails, SalesInvoiceType, ServiceType, VoucherValidation } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const money = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })
const warrantyOptions = ['1 tuần', '1 tháng', '3 tháng', '6 tháng', '12 tháng', '18 tháng', '24 tháng', '36 tháng']
const unitOptions = ['Cái', 'Chiếc', 'Bộ', 'Máy', 'Dịch vụ']

type CartLine = {
  key: string
  productId?: string
  sku: string
  name: string
  quantity: number
  unitName: string
  warrantyPeriod: string
  unitPrice: number
  discountAmount: number
  serviceTypeId: string
}

type RepairDeviceDraft = {
  key: string
  id?: string
  deviceModel: string
  serialNumber: string
  accessories: string
  deviceCondition: string
  services: { serviceTypeId: string; notes: string; amount: number }[]
}

type PaymentMode = 'Cash' | 'BankTransfer' | 'Mixed'

type PaymentDraft = {
  key: string
  method: PaymentMethod
  paymentAccountId: string
  amount: number
  reference: string
}

type DiscountDraft = {
  key: string
  source: SalesDiscountSource
  promotionId?: string
  voucherCode?: string
  name: string
  amount: number
}

const newPayment = (method: PaymentMethod = 'Cash'): PaymentDraft => ({ key: crypto.randomUUID(), method, paymentAccountId: '', amount: 0, reference: '' })
const newRepairDevice = (): RepairDeviceDraft => ({ key: crypto.randomUUID(), deviceModel: '', serialNumber: '', accessories: '', deviceCondition: '', services: [] })

export function SalesInvoiceDialog({ invoiceId, onClose, onCreated }: { invoiceId?: string; onClose: () => void; onCreated: (id: string) => void }) {
  const { user } = useAuth()
  const hydratedInvoiceId = useRef<string | null>(null)
  const paymentDueCustomized = useRef(false)
  const [invoiceType, setInvoiceType] = useState<SalesInvoiceType>('Sale')
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [customerAddress, setCustomerAddress] = useState('')
  const [customerNotes, setCustomerNotes] = useState('')
  const [customerHistory, setCustomerHistory] = useState<CustomerLookup | null>(null)
  const [customerId, setCustomerId] = useState<string | undefined>()
  const [showCustomerEditor, setShowCustomerEditor] = useState(false)
  const [customerSuggestionOpen, setCustomerSuggestionOpen] = useState(false)
  const [invoiceDate, setInvoiceDate] = useState(toDateTimeLocal(new Date()))
  const [expectedReturnAt, setExpectedReturnAt] = useState('')
  const [productSearch, setProductSearch] = useState('')
  const [cart, setCart] = useState<CartLine[]>([])
  const [showManualLine, setShowManualLine] = useState(false)
  const [manualLineName, setManualLineName] = useState('')
  const [manualLineUnit, setManualLineUnit] = useState('Dịch vụ')
  const [manualLineWarranty, setManualLineWarranty] = useState('3 tháng')
  const [manualLinePrice, setManualLinePrice] = useState(0)
  const [promotionId, setPromotionId] = useState('')
  const [voucherCode, setVoucherCode] = useState('')
  const [showManualDiscount, setShowManualDiscount] = useState(false)
  const [manualDiscountName, setManualDiscountName] = useState('')
  const [manualDiscountAmount, setManualDiscountAmount] = useState(0)
  const [discounts, setDiscounts] = useState<DiscountDraft[]>([])
  const [payments, setPayments] = useState<PaymentDraft[]>([newPayment()])
  const [paymentMode, setPaymentMode] = useState<PaymentMode>('Cash')
  const [isInstallment, setIsInstallment] = useState(false)
  const [installmentProvider, setInstallmentProvider] = useState('')
  const [installmentReference, setInstallmentReference] = useState('')
  const [installmentPaymentAccountId, setInstallmentPaymentAccountId] = useState('')
  const [paymentDueDate, setPaymentDueDate] = useState('')
  const [notes, setNotes] = useState('')
  const [printNotes, setPrintNotes] = useState('')
  const [validationError, setValidationError] = useState('')

  const [repairDevices, setRepairDevices] = useState<RepairDeviceDraft[]>([newRepairDevice()])
  const [repairStatus, setRepairStatus] = useState<RepairStatus>('InProgress')
  const deferredCustomerPhone = useDeferredValue(customerPhone.replace(/\D/g, ''))

  const invoiceQuery = useQuery({ queryKey: ['sales-invoice-edit', invoiceId], queryFn: () => api.get<SalesInvoiceDetails>(`/api/sales/${invoiceId}`), enabled: Boolean(invoiceId) })
  const accountsQuery = useQuery({ queryKey: ['sales-payment-accounts'], queryFn: () => api.get<PaymentAccount[]>('/api/sales/payment-accounts') })
  const installmentProvidersQuery = useQuery({ queryKey: ['sales-installment-providers'], queryFn: () => api.get<InstallmentProvider[]>('/api/sales/installment-providers') })
  const promotionsQuery = useQuery({ queryKey: ['sales-promotions'], queryFn: () => api.get<Promotion[]>('/api/sales/promotions') })
  const serviceTypesQuery = useQuery({ queryKey: ['sales-service-types'], queryFn: () => api.get<ServiceType[]>('/api/sales/service-types') })
  const invoiceSettingsQuery = useQuery({ queryKey: ['invoice-settings'], queryFn: () => api.get<InvoiceSettings>('/api/settings/invoice') })
  const productQuery = useQuery({
    queryKey: ['sales-product-search', productSearch],
    queryFn: () => api.get<{ items: Product[] }>(`/api/products?search=${encodeURIComponent(productSearch.trim())}&stock=in_stock&status=Active&pageSize=12`),
    enabled: productSearch.trim().length >= 2,
  })
  const customerSuggestionsQuery = useQuery({
    queryKey: ['sales-customer-suggestions', deferredCustomerPhone],
    queryFn: () => api.get<PagedResult<Customer>>(`/api/customers?search=${encodeURIComponent(deferredCustomerPhone)}&page=1&pageSize=8`),
    enabled: customerSuggestionOpen && deferredCustomerPhone.length >= 3 && !customerHistory,
  })
  const savedCustomerHistoryQuery = useQuery({
    queryKey: ['sales-customer-history', customerId, deferredCustomerPhone],
    queryFn: () => api.get<CustomerLookup>(`/api/customers/lookup?phone=${encodeURIComponent(deferredCustomerPhone)}`),
    enabled: Boolean(invoiceId && customerId && deferredCustomerPhone.length >= 8 && !customerHistory),
    retry: false,
  })
  const selectedCustomerHistory = customerHistory ?? savedCustomerHistoryQuery.data ?? null

  const lookupMutation = useMutation({
    mutationFn: (phone: string) => api.get<CustomerLookup>(`/api/customers/lookup?phone=${encodeURIComponent(phone)}`),
    onSuccess: (customer) => {
      setCustomerId(customer.id)
      setCustomerHistory(customer)
      setCustomerName(customer.fullName)
      setCustomerPhone(customer.phone ?? customerPhone)
      setCustomerEmail(customer.email ?? '')
      setCustomerAddress(customer.address ?? '')
      setCustomerNotes(customer.notes ?? '')
      setCustomerSuggestionOpen(false)
    },
    onError: () => {
      setCustomerHistory(null)
      setCustomerSuggestionOpen(false)
    },
  })

  /* eslint-disable react-hooks/set-state-in-effect -- hydrate the multi-section invoice editor from the selected record */
  useEffect(() => {
    const invoice = invoiceQuery.data
    if (!invoice || invoiceSettingsQuery.isLoading || hydratedInvoiceId.current === invoice.id) return
    hydratedInvoiceId.current = invoice.id
    setInvoiceType(invoice.type)
    setCustomerId(invoice.customerId)
    setCustomerName(invoice.customerName)
    setCustomerPhone(invoice.customerPhone ?? '')
    setCustomerEmail(invoice.customerEmail ?? '')
    setCustomerAddress(invoice.customerAddress ?? '')
    setInvoiceDate(toDateTimeLocal(new Date(invoice.invoiceDateUtc)))
    setCart(invoice.lines.filter((line) => invoice.type !== 'Repair' || line.sku !== 'DICH-VU-SUA-CHUA').map((line) => ({
      key: line.id,
      productId: line.productId,
      sku: line.sku,
      name: line.productName,
      quantity: line.quantity,
      unitName: line.unitName,
      warrantyPeriod: line.warrantyPeriod || '3 tháng',
      unitPrice: line.unitPrice,
      discountAmount: line.discountAmount,
      serviceTypeId: invoice.serviceTypes?.find((item) => item.code === detectProductServiceTypeCode(line.productName))?.id ?? '',
    })))
    setDiscounts((invoice.discounts ?? []).map((discount) => ({
      key: discount.id || crypto.randomUUID(),
      source: discount.source,
      promotionId: discount.promotionId,
      voucherCode: discount.code,
      name: discount.name,
      amount: discount.amount,
    })))
    const immediatePayments = invoice.payments.filter((payment) => !payment.isScheduled)
    const hydratedPayments = immediatePayments.length > 0 ? immediatePayments.map((payment) => ({ key: payment.id, method: payment.method, paymentAccountId: payment.paymentAccountId ?? '', amount: payment.amount, reference: payment.reference ?? '' })) : [newPayment()]
    setPayments(hydratedPayments)
    const hasCash = hydratedPayments.some((payment) => payment.method === 'Cash')
    const hasBank = hydratedPayments.some((payment) => payment.method === 'BankTransfer')
    setPaymentMode(hasCash && hasBank ? 'Mixed' : hasBank ? 'BankTransfer' : 'Cash')
    setIsInstallment(invoice.isInstallment)
    setInstallmentProvider(invoice.installmentProvider ?? '')
    setInstallmentReference(invoice.installmentReference ?? '')
    setInstallmentPaymentAccountId(invoice.installmentPaymentAccountId ?? '')
    setPaymentDueDate(invoice.paymentDueDateUtc ? toDateTimeLocal(new Date(invoice.paymentDueDateUtc)) : '')
    paymentDueCustomized.current = Boolean(invoice.paymentDueDateUtc)
    const hydratedRepairs = invoice.repairs?.length ? invoice.repairs : invoice.repair ? [invoice.repair] : []
    const hasAutomaticInternalContent = invoice.type === 'Repair'
      ? isAutomaticRepairNote(invoice.notes, hydratedRepairs, invoice.lines)
      : isAutomaticSaleNote(invoice.notes, invoice.lines)
    setNotes(hasAutomaticInternalContent ? '' : invoice.notes ?? '')
    setPrintNotes(invoice.printNotes ?? '')
    setRepairStatus(normalizeProcessingStatus(invoice.repairStatus ?? hydratedRepairs[0]?.status))
    setExpectedReturnAt(invoice.expectedReturnAtUtc
      ? toDateTimeLocal(new Date(invoice.expectedReturnAtUtc))
      : hydratedRepairs[0]?.expectedReturnAtUtc
        ? toDateTimeLocal(new Date(hydratedRepairs[0].expectedReturnAtUtc))
        : addDaysToLocalDateTime(toDateTimeLocal(new Date(invoice.invoiceDateUtc)), invoiceSettingsQuery.data?.defaultRepairTurnaroundDays ?? 5))
    setRepairDevices(hydratedRepairs.length ? hydratedRepairs.map((repair) => ({
      key: repair.id || crypto.randomUUID(),
      id: repair.id,
      deviceModel: repair.deviceModel,
      serialNumber: repair.serialNumber ?? '',
      accessories: repair.accessories ?? '',
      deviceCondition: repair.deviceCondition,
      services: repair.services?.length
        ? repair.services.map((service) => ({ serviceTypeId: service.id, notes: service.notes ?? '', amount: service.amount }))
        : (repair.serviceTypes ?? []).map((service, index) => ({ serviceTypeId: service.id, notes: '', amount: index === 0 ? repair.estimatedCost : 0 })),
    })) : [newRepairDevice()])
  }, [invoiceQuery.data, invoiceSettingsQuery.data, invoiceSettingsQuery.isLoading])

  useEffect(() => {
    if (!invoiceSettingsQuery.data || invoiceId) return
    setExpectedReturnAt((current) => current || addDaysToLocalDateTime(invoiceDate, invoiceSettingsQuery.data.defaultRepairTurnaroundDays))
  }, [invoiceDate, invoiceId, invoiceSettingsQuery.data])
  /* eslint-enable react-hooks/set-state-in-effect */

  const saleSubtotal = useMemo(() => cart.reduce((sum, line) => sum + line.quantity * line.unitPrice - line.discountAmount, 0), [cart])
  const repairSubtotal = useMemo(() => repairDevices.reduce((sum, device) => sum + device.services.reduce((deviceSum, service) => deviceSum + service.amount, 0), 0), [repairDevices])
  const subtotal = invoiceType === 'Repair' ? repairSubtotal + saleSubtotal : saleSubtotal
  const selectedPromotion = promotionsQuery.data?.find((item) => item.id === promotionId)
  const resolvedDiscounts = useMemo(() => discounts.map((discount) => {
    if (discount.source !== 'Promotion' || !discount.promotionId) return discount
    const promotion = promotionsQuery.data?.find((item) => item.id === discount.promotionId)
    if (!promotion) return discount
    const amount = promotion.isActive && subtotal >= promotion.minimumOrderAmount
      ? calculateDiscount(promotion.discountType, promotion.value, promotion.maximumDiscountAmount, subtotal)
      : 0
    return { ...discount, amount }
  }), [discounts, promotionsQuery.data, subtotal])
  const promotionDiscount = Math.min(subtotal, resolvedDiscounts.filter((item) => item.source !== 'Voucher').reduce((sum, item) => sum + item.amount, 0))
  const voucherDiscount = Math.min(subtotal - promotionDiscount, resolvedDiscounts.filter((item) => item.source === 'Voucher').reduce((sum, item) => sum + item.amount, 0))
  const total = Math.max(0, subtotal - promotionDiscount - voucherDiscount)
  const resolvedPayments = useMemo(() => {
    const cash = payments.find((payment) => payment.method === 'Cash') ?? newPayment('Cash')
    const bank = payments.find((payment) => payment.method === 'BankTransfer') ?? newPayment('BankTransfer')
    if (isInstallment) return [{ ...cash, amount: Math.max(0, cash.amount || 0), reference: '' }]
    if (paymentMode === 'Cash') return [{ ...cash, amount: total, reference: '' }]
    if (paymentMode === 'BankTransfer') return [{ ...bank, amount: total, reference: '' }]
    const cashAmount = Math.min(total, Math.max(0, cash.amount || 0))
    return [{ ...cash, amount: cashAmount, reference: '' }, { ...bank, amount: total - cashAmount, reference: '' }]
  }, [isInstallment, paymentMode, payments, total])
  const paid = resolvedPayments.reduce((sum, payment) => sum + Math.max(0, payment.amount || 0), 0)
  const outstanding = Math.max(0, total - paid)
  const suggestedNotes = useMemo(() => {
    if (invoiceType === 'Repair') {
      const repairDescriptions = repairDevices.map((device, index) => {
        const model = device.deviceModel.trim() || `Máy ${index + 1}`
        const serviceNames = device.services
          .map((service) => {
            const name = serviceTypesQuery.data?.find((item) => item.id === service.serviceTypeId)?.name
            return name ? formatRepairServiceDescription(name, service.notes) : ''
          })
          .filter(Boolean)
        return serviceNames.length > 0 ? `Sửa (Thay) ${serviceNames.join(', ')} cho ${model}` : `Sửa chữa cho ${model}`
      })
      return [...repairDescriptions, ...cart.map((line) => line.name.trim()).filter(Boolean)].join(' + ')
    }
    return cart.map((line) => line.name.trim()).filter(Boolean).join(' + ')
  }, [cart, invoiceType, repairDevices, serviceTypesQuery.data])
  const invoicePurpose = notes.trim() || suggestedNotes
  const customerSuggestions = customerSuggestionsQuery.data?.items ?? []
  const exactCustomer = customerSuggestions.find((customer) => normalizePhone(customer.phone ?? '') === deferredCustomerPhone)
  const canCreateCustomer = deferredCustomerPhone.length >= 8 && !customerId && !exactCustomer && !customerSuggestionsQuery.isFetching

  const voucherMutation = useMutation({
    mutationFn: () => api.get<VoucherValidation>(`/api/sales/vouchers/validate?code=${encodeURIComponent(voucherCode.trim())}&subtotal=${subtotal}`),
    onSuccess: (validated) => {
      setDiscounts((current) => current.some((item) => item.source === 'Voucher' && item.voucherCode === validated.code)
        ? current
        : [...current, { key: crypto.randomUUID(), source: 'Voucher', voucherCode: validated.code, name: validated.name, amount: validated.discountAmount }])
      setVoucherCode('')
    },
  })

  const createMutation = useMutation({
    mutationFn: (body: object) => invoiceId ? api.put<{ id: string }>(`/api/sales/${invoiceId}`, body) : api.post<{ id: string }>('/api/sales', body),
    onSuccess: (invoice) => onCreated(invoice.id),
  })

  function addProduct(product: Product) {
    setCart((current) => {
      const existing = current.find((line) => line.productId === product.id)
      if (existing) return current.map((line) => line.key === existing.key ? { ...line, quantity: line.quantity + 1 } : line)
      const detectedCode = detectProductServiceTypeCode(product.name, product.categoryName)
      const serviceTypeId = serviceTypesQuery.data?.find((item) => item.code === detectedCode)?.id ?? ''
      return [...current, { key: crypto.randomUUID(), productId: product.id, sku: product.sku, name: product.name, quantity: 1, unitName: 'Cái', warrantyPeriod: '3 tháng', unitPrice: product.salePrice, discountAmount: 0, serviceTypeId }]
    })
    setDiscounts((current) => current.filter((item) => item.source !== 'Voucher'))
    setProductSearch('')
  }

  function addManualLine() {
    if (!manualLineName.trim()) return
    setCart((current) => [...current, { key: crypto.randomUUID(), sku: 'DICH-VU', name: manualLineName.trim(), quantity: 1, unitName: manualLineUnit.trim() || 'Dịch vụ', warrantyPeriod: manualLineWarranty, unitPrice: Math.max(0, manualLinePrice), discountAmount: 0, serviceTypeId: '' }])
    setManualLineName('')
    setManualLinePrice(0)
    setShowManualLine(false)
    setDiscounts((current) => current.filter((item) => item.source !== 'Voucher'))
  }

  function addPromotionDiscount() {
    const promotion = promotionsQuery.data?.find((item) => item.id === promotionId)
    if (!promotion || discounts.some((item) => item.source === 'Promotion' && item.promotionId === promotion.id)) return
    const amount = subtotal >= promotion.minimumOrderAmount
      ? calculateDiscount(promotion.discountType, promotion.value, promotion.maximumDiscountAmount, subtotal)
      : 0
    setDiscounts((current) => [...current, { key: crypto.randomUUID(), source: 'Promotion', promotionId: promotion.id, name: promotion.name, amount }])
    setPromotionId('')
  }

  function addManualDiscount() {
    const name = manualDiscountName.trim()
    const amount = Math.max(0, manualDiscountAmount)
    if (!name || amount <= 0) return
    setDiscounts((current) => [...current, {
      key: crypto.randomUUID(),
      source: 'Manual',
      name,
      amount,
    }])
    setManualDiscountName('')
    setManualDiscountAmount(0)
    setShowManualDiscount(false)
  }

  function customerSaved(customer: CustomerDetails) {
    setCustomerId(customer.id)
    setCustomerName(customer.fullName)
    setCustomerPhone(customer.phone ?? '')
    setCustomerEmail(customer.email ?? '')
    setCustomerAddress(customer.address ?? '')
    setCustomerNotes(customer.notes ?? '')
    setShowCustomerEditor(false)
    if (customer.phone) lookupMutation.mutate(customer.phone)
  }

  function updateCartLine(key: string, changes: Partial<CartLine>) {
    setCart((current) => current.map((line) => line.key === key ? { ...line, ...changes } : line))
    setDiscounts((current) => current.filter((item) => item.source !== 'Voucher'))
  }

  function updateRepairDevice(key: string, changes: Partial<RepairDeviceDraft>) {
    setRepairDevices((current) => current.map((device) => device.key === key ? { ...device, ...changes } : device))
  }

  function toggleRepairServiceType(key: string, serviceTypeId: string) {
    setRepairDevices((current) => current.map((device) => device.key === key
      ? { ...device, services: device.services.some((service) => service.serviceTypeId === serviceTypeId) ? device.services.filter((service) => service.serviceTypeId !== serviceTypeId) : [...device.services, { serviceTypeId, notes: '', amount: 0 }] }
      : device))
    setDiscounts((current) => current.filter((item) => item.source !== 'Voucher'))
  }

  function updateRepairServiceAmount(key: string, serviceTypeId: string, amount: number) {
    setRepairDevices((current) => current.map((device) => device.key === key
      ? { ...device, services: device.services.map((service) => service.serviceTypeId === serviceTypeId ? { ...service, amount } : service) }
      : device))
    setDiscounts((current) => current.filter((item) => item.source !== 'Voucher'))
  }

  function updateRepairServiceNotes(key: string, serviceTypeId: string, notes: string) {
    setRepairDevices((current) => current.map((device) => device.key === key
      ? { ...device, services: device.services.map((service) => service.serviceTypeId === serviceTypeId ? { ...service, notes } : service) }
      : device))
  }

  function updatePayment(key: string, changes: Partial<PaymentDraft>) {
    setPayments((current) => current.map((payment) => payment.key === key ? { ...payment, ...changes } : payment))
  }

  function changePaymentMode(mode: PaymentMode) {
    setPaymentMode(mode)
    const cash = payments.find((payment) => payment.method === 'Cash')
    const bank = payments.find((payment) => payment.method === 'BankTransfer')
    if (mode === 'Cash') setPayments([{ ...(cash ?? newPayment('Cash')), amount: total, reference: '' }])
    else if (mode === 'BankTransfer') setPayments([{ ...(bank ?? newPayment('BankTransfer')), amount: total, reference: '' }])
    else {
      const cashPayment = cash ?? newPayment('Cash')
      const bankPayment = bank ?? newPayment('BankTransfer')
      const cashAmount = Math.min(total, Math.max(0, cashPayment.amount || 0))
      setPayments([{ ...cashPayment, amount: cashAmount, reference: '' }, { ...bankPayment, amount: total - cashAmount, reference: '' }])
    }
  }

  function changeSaleMode(nextInstallment: boolean) {
    setIsInstallment(nextInstallment)
    if (nextInstallment) {
      const cash = payments.find((payment) => payment.method === 'Cash') ?? newPayment('Cash')
      setPaymentMode('Cash')
      setPayments([cash])
      if (!paymentDueDate) {
        setPaymentDueDate(addDaysToLocalDateTime(invoiceDate, 2))
        paymentDueCustomized.current = false
      }
      return
    }
    setInstallmentPaymentAccountId('')
    setPaymentDueDate('')
    paymentDueCustomized.current = false
    changePaymentMode('Cash')
  }

  function changeInvoiceDate(value: string) {
    const days = invoiceSettingsQuery.data?.defaultRepairTurnaroundDays ?? 5
    const previousDefault = addDaysToLocalDateTime(invoiceDate, days)
    setInvoiceDate(value)
    const nextDefault = addDaysToLocalDateTime(value, days)
    setExpectedReturnAt((current) => !current || current === previousDefault ? nextDefault : current)
    if (isInstallment && !paymentDueCustomized.current) setPaymentDueDate(addDaysToLocalDateTime(value, 2))
  }

  function selectCustomer(customer: Customer) {
    if (!customer.phone) return
    setCustomerId(customer.id)
    setCustomerPhone(customer.phone)
    setCustomerSuggestionOpen(false)
    lookupMutation.mutate(customer.phone)
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    const submitError = getSubmitError()
    if (submitError) {
      setValidationError(submitError)
      event.currentTarget.scrollTo({ top: 0, behavior: 'smooth' })
      return
    }
    setValidationError('')
    createMutation.mutate({
      type: invoiceType,
      customerName,
      customerPhone,
      customerEmail: customerEmail || null,
      customerAddress: customerAddress || null,
      customerNotes: customerNotes || null,
      invoiceDateUtc: fromDateTimeLocal(invoiceDate),
      lines: cart.map((line) => ({
        productId: line.productId,
        productName: line.name,
        quantity: line.quantity,
        unitName: line.unitName,
        warrantyPeriod: line.warrantyPeriod,
        unitPrice: line.unitPrice,
        discountAmount: line.discountAmount,
        serviceTypeIds: line.serviceTypeId ? [line.serviceTypeId] : [],
        notes: null,
      })),
      promotionId: null,
      manualDiscountName: null,
      manualDiscountAmount: 0,
      voucherCode: null,
      discounts: resolvedDiscounts.map((discount) => ({
        source: discount.source,
        promotionId: discount.promotionId ?? null,
        voucherCode: discount.voucherCode ?? null,
        name: discount.name,
        amount: discount.amount,
        notes: null,
      })),
      payments: resolvedPayments.filter((payment) => payment.amount > 0).map((payment) => ({
        method: payment.method,
        paymentAccountId: payment.method === 'BankTransfer' ? payment.paymentAccountId || null : null,
        amount: payment.amount,
        paidAtUtc: fromDateTimeLocal(invoiceDate),
        reference: null,
        notes: null,
      })),
      isInstallment,
      installmentProvider: installmentProvider || null,
      installmentReference: installmentReference || null,
      installmentPaymentAccountId: isInstallment && outstanding > 0 ? installmentPaymentAccountId || null : null,
      paymentDueDateUtc: isInstallment && paymentDueDate ? fromDateTimeLocal(paymentDueDate) : null,
      serviceTypeIds: invoiceType === 'Sale' ? [...new Set(cart.map((line) => line.serviceTypeId).filter(Boolean))] : [],
      repair: null,
      repairStatus,
      expectedReturnAtUtc: invoiceType === 'Repair' && expectedReturnAt ? fromDateTimeLocal(expectedReturnAt) : null,
      repairs: invoiceType === 'Repair' ? repairDevices.map((device) => ({
        id: device.id ?? null,
        deviceModel: device.deviceModel,
        serialNumber: device.serialNumber || null,
        accessories: device.accessories || null,
        deviceCondition: device.deviceCondition || null,
        customerRequest: null,
        technicianAssessment: null,
        estimatedCost: device.services.reduce((sum, service) => sum + service.amount, 0),
        expectedReturnAtUtc: null,
        serviceTypeIds: device.services.map((service) => service.serviceTypeId),
        services: device.services,
        notes: null,
      })) : [],
      notes: invoicePurpose || null,
      printNotes: printNotes.trim() || null,
    })
  }

  function getSubmitError() {
    if (customerPhone.replace(/\D/g, '').length < 8) return 'Hãy nhập số điện thoại khách hàng hợp lệ.'
    if (!customerId || !customerName.trim()) return 'Hãy chọn khách hàng đã có hoặc tạo hồ sơ khách hàng mới trước khi lưu.'
    if (invoiceType === 'Sale' && cart.length === 0) return 'Hãy chọn ít nhất một sản phẩm hoặc dịch vụ.'
    if (invoiceType === 'Repair') {
      if (repairDevices.length === 0) return 'Phiếu tiếp nhận cần ít nhất một máy sửa chữa.'
      if (!expectedReturnAt) return 'Hãy chọn ngày dự kiến trả cho phiếu sửa chữa.'
      const invalidIndex = repairDevices.findIndex((device) => !device.deviceModel.trim() || device.services.length === 0)
      if (invalidIndex >= 0) {
        const device = repairDevices[invalidIndex]!
        if (!device.deviceModel.trim()) return `Máy ${invalidIndex + 1}: hãy nhập model máy.`
        return `Máy ${invalidIndex + 1}: hãy chọn ít nhất một loại dịch vụ.`
      }
    }
    if (paid > total) return 'Số tiền đã nhận không được vượt tổng hóa đơn.'
    if (isInstallment && !installmentProvider) return 'Hãy chọn đơn vị trả góp.'
    if (isInstallment && paid <= 0) return 'Hãy nhập số tiền mặt khách trả trước.'
    if (isInstallment && outstanding <= 0) return 'Tiền trả trước phải nhỏ hơn tổng hóa đơn trả góp.'
    if (isInstallment && outstanding > 0 && !installmentPaymentAccountId) return 'Hãy chọn tài khoản sẽ nhận tiền góp.'
    if (isInstallment && outstanding > 0 && !paymentDueDate) return 'Hãy chọn ngày dự kiến nhận tiền góp.'
    if (!isInstallment && paymentMode === 'Mixed') {
      const cashAmount = resolvedPayments.find((payment) => payment.method === 'Cash')?.amount ?? 0
      if (cashAmount <= 0 || cashAmount >= total) return 'Thanh toán kết hợp cần số tiền mặt lớn hơn 0 và nhỏ hơn tổng hóa đơn.'
    }
    const invalidBankPayment = resolvedPayments.find((payment) => payment.method === 'BankTransfer' && payment.amount > 0 && !payment.paymentAccountId)
    if (invalidBankPayment) return 'Mỗi khoản chuyển khoản cần chọn tài khoản ngân hàng nhận tiền.'
    return ''
  }

  const productSection = <section className="sales-section">
    <div className="sales-section-title"><span><PackagePlus size={19} /></span><div><h3>SẢN PHẨM & DỊCH VỤ</h3><p>Tìm sản phẩm đang còn hàng và điều chỉnh số lượng, đơn giá nếu cần.</p></div></div>
    <div className="product-entry-toolbar"><label className="sale-product-search"><Search size={18} /><input value={productSearch} onChange={(event) => setProductSearch(event.target.value)} placeholder="Nhập ít nhất 2 ký tự để tìm sản phẩm…" /></label><button type="button" className="secondary-button" onClick={() => setShowManualLine((current) => !current)}><Plus size={16} /> Thêm dịch vụ nhập tay</button></div>
    {showManualLine && <div className="manual-line-entry">
      <label><span>Tên sản phẩm / dịch vụ *</span><input value={manualLineName} onChange={(event) => setManualLineName(event.target.value)} placeholder="Ví dụ: Cài Windows và phần mềm" /></label>
      <label><span>Đơn vị</span><select value={manualLineUnit} onChange={(event) => setManualLineUnit(event.target.value)}>{unitOptions.map((unit) => <option key={unit}>{unit}</option>)}</select></label>
      <label><span>Bảo hành</span><select value={manualLineWarranty} onChange={(event) => setManualLineWarranty(event.target.value)}>{warrantyOptions.map((warranty) => <option key={warranty}>{warranty}</option>)}</select></label>
      <label><span>Đơn giá</span><MoneyInput value={manualLinePrice} onChange={setManualLinePrice} /></label>
      <button type="button" className="primary-button" disabled={!manualLineName.trim()} onClick={addManualLine}><Plus size={16} /> Thêm vào hóa đơn</button>
    </div>}
    {productSearch.trim().length >= 2 && <div className="sale-product-results">
      {productQuery.isLoading && <p>Đang tìm sản phẩm…</p>}
      {productQuery.data?.items.map((product) => <button type="button" key={product.id} onClick={() => addProduct(product)}><span className="sale-result-image">{product.primaryImageUrl ? <img src={product.primaryImageUrl} alt="" /> : <PackagePlus size={19} />}</span><span><strong>{product.name}</strong><small>{product.stockQuantity.toLocaleString('vi-VN')} sản phẩm · {product.sku}</small></span><b>{currency.format(product.salePrice)}</b><Plus size={17} /></button>)}
      {!productQuery.isLoading && productQuery.data?.items.length === 0 && <p>Không tìm thấy sản phẩm còn hàng.</p>}
    </div>}
    <div className="sales-cart">
      {cart.length === 0 && <div className="sales-cart-empty"><PackagePlus size={28} /><span>Chưa chọn sản phẩm</span></div>}
      {cart.map((line) => <article key={line.key}>
        <div className="sales-cart-name"><strong>{line.name}</strong><small>{line.sku}</small></div>
        <label><span>Loại dịch vụ</span><select value={line.serviceTypeId} onChange={(event) => updateCartLine(line.key, { serviceTypeId: event.target.value })}><option value="">Chưa gắn</option>{serviceTypesQuery.data?.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <label><span>Số lượng</span><input type="number" min={1} step={1} value={line.quantity} onChange={(event) => updateCartLine(line.key, { quantity: Math.max(1, Number(event.target.value)) })} /></label>
        <label><span>Đơn vị</span><select value={line.unitName} onChange={(event) => updateCartLine(line.key, { unitName: event.target.value })}>{withCurrentOption(unitOptions, line.unitName).map((unit) => <option key={unit}>{unit}</option>)}</select></label>
        <label><span>Bảo hành</span><select value={line.warrantyPeriod} onChange={(event) => updateCartLine(line.key, { warrantyPeriod: event.target.value })}>{withCurrentOption(warrantyOptions, line.warrantyPeriod).map((warranty) => <option key={warranty}>{warranty}</option>)}</select></label>
        <label><span>Đơn giá</span><MoneyInput value={line.unitPrice} onChange={(value) => updateCartLine(line.key, { unitPrice: value })} /></label>
        <strong className="sales-line-total">{currency.format(line.quantity * line.unitPrice - line.discountAmount)}</strong>
        <button type="button" className="icon-button" onClick={() => { setCart((current) => current.filter((item) => item.key !== line.key)); setDiscounts((current) => current.filter((item) => item.source !== 'Voucher')) }} aria-label={`Xóa ${line.name}`}><Trash2 size={16} /></button>
      </article>)}
    </div>
  </section>

  const paymentSection = <section className="sales-summary-card summary-payment-card">
    <div className="summary-payment-title"><span><Banknote size={18} /></span><div><small>THANH TOÁN</small><h3>NGUỒN TIỀN THANH TOÁN</h3></div></div>
    {invoiceType === 'Sale' && <div className="sale-mode-options sale-mode-first"><button type="button" className={!isInstallment ? 'is-active' : ''} onClick={() => changeSaleMode(false)}><CheckCircle2 size={17} /><span><strong>Bán trực tiếp</strong></span></button><button type="button" className={isInstallment ? 'is-active' : ''} onClick={() => changeSaleMode(true)}><Clock3 size={17} /><span><strong>Bán trả góp</strong></span></button></div>}

    {!isInstallment && <div className="payment-mode-options" role="group" aria-label="Hình thức thanh toán">
      <button type="button" className={paymentMode === 'Cash' ? 'is-active' : ''} onClick={() => changePaymentMode('Cash')}><Banknote size={17} /><span><strong>Tiền mặt</strong></span></button>
      <button type="button" className={paymentMode === 'BankTransfer' ? 'is-active' : ''} onClick={() => changePaymentMode('BankTransfer')}><CreditCard size={17} /><span><strong>Chuyển khoản</strong></span></button>
      <button type="button" className={paymentMode === 'Mixed' ? 'is-active' : ''} onClick={() => changePaymentMode('Mixed')}><Plus size={17} /><span><strong>Tiền mặt + Chuyển khoản</strong></span></button>
    </div>}

    {isInstallment && <div className="installment-flow-card">
      <div className="installment-flow-explainer"><Clock3 size={18} /><span><strong>Phần banking tự động</strong></span><b>{currency.format(outstanding)}</b></div>
      <div className="sales-form-grid installment-fields">
        <label><span>Tiền mặt trả trước *</span><MoneyInput value={resolvedPayments[0]?.amount ?? 0} onChange={(value) => updatePayment(resolvedPayments[0]!.key, { amount: value })} /></label>
        <label><span>Đơn vị trả góp *</span><select value={installmentProvider} onChange={(event) => setInstallmentProvider(event.target.value)}><option value="">Chọn đơn vị trả góp…</option>{installmentProvidersQuery.data?.filter((provider) => provider.isActive).map((provider) => <option key={provider.id} value={provider.name}>{provider.name}</option>)}{installmentProvider && !installmentProvidersQuery.data?.some((provider) => provider.name === installmentProvider) && <option value={installmentProvider}>{installmentProvider}</option>}</select></label>
        <label><span>Tài khoản nhận tiền góp *</span><select value={installmentPaymentAccountId} onChange={(event) => setInstallmentPaymentAccountId(event.target.value)} required={outstanding > 0}><option value="">Chọn tài khoản ngân hàng…</option>{accountsQuery.data?.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} · {account.bankName} · {account.accountNumber}</option>)}</select></label>
        <div className="installment-date-field"><span>Ngày dự kiến nhận tiền góp *</span><VietnameseDateInput key={`payment-due-${paymentDueDate}`} ariaLabel="Ngày dự kiến nhận tiền góp" value={paymentDueDate} onChange={(value) => { paymentDueCustomized.current = true; setPaymentDueDate(value) }} required={outstanding > 0} /><small>Mặc định sau 2 ngày kể từ ngày lập hóa đơn.</small></div>
      </div>
    </div>}

    {!isInstallment && paymentMode === 'Cash' && <div className="payment-auto-row"><Banknote size={18} /><span><strong>Thanh toán 100% tiền mặt</strong><small>{currency.format(total)}</small></span></div>}
    {!isInstallment && paymentMode === 'BankTransfer' && (() => {
      const bank = resolvedPayments.find((payment) => payment.method === 'BankTransfer')!
      return <div className="payment-simple-grid is-bank-only"><label><span>Tài khoản nhận tiền *</span><select value={bank.paymentAccountId} onChange={(event) => updatePayment(bank.key, { paymentAccountId: event.target.value })}><option value="">Chọn tài khoản ngân hàng…</option>{accountsQuery.data?.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} · {account.bankName} · {account.accountNumber}</option>)}</select></label></div>
    })()}
    {!isInstallment && paymentMode === 'Mixed' && (() => {
      const cash = resolvedPayments.find((payment) => payment.method === 'Cash')!
      const bank = resolvedPayments.find((payment) => payment.method === 'BankTransfer')!
      return <div className="payment-simple-grid mixed"><label><span>Số tiền mặt *</span><MoneyInput value={cash.amount} onChange={(value) => updatePayment(cash.key, { amount: value })} /></label><div className="payment-readonly"><span>Chuyển khoản tự tính</span><strong>{currency.format(bank.amount)}</strong></div><label><span>Tài khoản nhận tiền *</span><select value={bank.paymentAccountId} onChange={(event) => updatePayment(bank.key, { paymentAccountId: event.target.value })}><option value="">Chọn tài khoản ngân hàng…</option>{accountsQuery.data?.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} · {account.bankName} · {account.accountNumber}</option>)}</select></label></div>
    })()}
  </section>

  if (invoiceId && invoiceQuery.isLoading) return <div className="sales-editor-backdrop"><div className="sales-editor-loading">Đang tải hóa đơn để chỉnh sửa…</div></div>
  if (invoiceId && invoiceQuery.isError) return <div className="sales-editor-backdrop"><div className="sales-editor-loading"><div className="form-alert">{invoiceQuery.error.message}</div><button className="secondary-button" onClick={onClose}>Đóng</button></div></div>

  return <><div className="sales-editor-backdrop">
    <form className="sales-editor" onSubmit={submit} noValidate>
      <header className="sales-editor-head">
        <div><span className="eyebrow accent">{invoiceId ? 'CHỈNH SỬA GIAO DỊCH' : 'GIAO DỊCH MỚI'}</span><h2>{invoiceId ? `Sửa ${invoiceQuery.data?.number ?? 'hóa đơn'}` : invoiceType === 'Sale' ? 'Tạo hóa đơn bán hàng' : 'Tiếp nhận máy sửa chữa'}</h2></div>
        <div><button type="button" className="secondary-button" onClick={onClose}>Đóng</button><button className="primary-button" disabled={createMutation.isPending}><Save size={18} /> {createMutation.isPending ? 'Đang lưu…' : invoiceId ? 'Cập nhật hóa đơn' : invoiceType === 'Repair' ? 'Lưu phiếu tiếp nhận' : 'Lưu hóa đơn'}</button></div>
      </header>

      {validationError && <div className="form-alert sales-editor-alert">{validationError}</div>}
      {createMutation.isError && <div className="form-alert sales-editor-alert">{createMutation.error.message}</div>}

      <div className="sales-editor-layout">
        <main className="sales-editor-main">
          <section className="sales-section transaction-kind-card">
            <button type="button" disabled={Boolean(invoiceId)} className={invoiceType === 'Sale' ? 'is-active' : ''} onClick={() => { setInvoiceType('Sale'); setRepairStatus('InProgress') }}><PackagePlus size={20} /><span><strong>Bán sản phẩm</strong></span></button>
            <button type="button" disabled={Boolean(invoiceId)} className={invoiceType === 'Repair' ? 'is-active' : ''} onClick={() => { setInvoiceType('Repair'); setRepairStatus('InProgress') }}><Wrench size={20} /><span><strong>Nhận sửa chữa</strong></span></button>
          </section>

          <section className="sales-section invoice-note-card">
            <div className="sales-section-title"><span><ClipboardPenLine size={19} /></span><div><h3>THÔNG TIN HOÁ ĐƠN</h3></div></div>
            <div className="invoice-customer-block">
              <div className="customer-lookup-row">
                <label><span>Số điện thoại khách hàng *</span><div className="customer-phone-field"><Phone size={17} /><input value={customerPhone} onFocus={() => setCustomerSuggestionOpen(true)} onBlur={() => window.setTimeout(() => setCustomerSuggestionOpen(false), 150)} onChange={(event) => { setCustomerPhone(event.target.value); setCustomerId(undefined); setCustomerHistory(null); setCustomerEmail(''); setCustomerAddress(''); setCustomerNotes(''); setCustomerSuggestionOpen(true); lookupMutation.reset() }} placeholder="Gõ từ 3 số để tìm khách hàng…" autoComplete="off" required />
                  {customerSuggestionOpen && deferredCustomerPhone.length >= 3 && <div className="customer-phone-suggestions">
                    {customerSuggestionsQuery.isFetching && <p>Đang lọc khách hàng…</p>}
                    {customerSuggestions.map((customer) => <button type="button" key={customer.id} className={customer.id === exactCustomer?.id ? 'is-exact' : ''} onMouseDown={(event) => event.preventDefault()} onClick={() => selectCustomer(customer)}><span className="suggestion-avatar">{customer.fullName.slice(0, 1).toUpperCase()}</span><span><strong>{customer.fullName}</strong><small>{customer.phone} · {customer.code}</small></span><b>{currency.format(customer.totalSpent)}</b></button>)}
                    {!customerSuggestionsQuery.isFetching && customerSuggestions.length === 0 && <p>Chưa có khách hàng khớp số điện thoại này.</p>}
                  </div>}
                </div></label>
                <button type="button" className="secondary-button" disabled={customerPhone.replace(/\D/g, '').length < 8 || lookupMutation.isPending} onClick={() => lookupMutation.mutate(customerPhone)}><Search size={16} /> {lookupMutation.isPending ? 'Đang tìm…' : 'Tra cứu'}</button>
              </div>
              {exactCustomer && !customerId && <div className="customer-choice-callout existing"><CheckCircle2 size={17} /><span><strong>Số điện thoại đã tồn tại</strong><small>Chọn hồ sơ {exactCustomer.fullName} để dùng lịch sử mua hàng.</small></span><button type="button" className="secondary-button" onClick={() => selectCustomer(exactCustomer)}>Chọn khách hàng</button></div>}
              {canCreateCustomer && <div className="customer-choice-callout new"><UserRoundCog size={17} /><span><strong>Đây là số điện thoại mới</strong><small>Tạo hồ sơ khách hàng trước khi lưu hóa đơn để quản lý thông tin đầy đủ.</small></span><button type="button" className="secondary-button" onClick={() => setShowCustomerEditor(true)}>Tạo khách hàng mới</button></div>}
              {lookupMutation.isError && !canCreateCustomer && <div className="inline-note neutral">Không tìm thấy khách hàng — hãy tạo hồ sơ mới để tiếp tục.</div>}
              {customerId && <div className="customer-found"><CheckCircle2 size={17} /><div><strong>{customerName}{selectedCustomerHistory?.code ? ` · ${selectedCustomerHistory.code}` : ''}</strong><small>{selectedCustomerHistory ? `${selectedCustomerHistory.invoices.length} hóa đơn gần đây · Tổng mua ${currency.format(selectedCustomerHistory.totalSpent)}` : 'Đã chọn hồ sơ khách hàng'}</small></div><button type="button" className="secondary-button customer-detail-button" onClick={() => setShowCustomerEditor(true)}><UserRoundCog size={17} /> Sửa thông tin</button></div>}
            </div>
            <div className="invoice-notes-grid">
              <label className="invoice-notes-field"><span>Nội dung hiển thị nội bộ</span><textarea value={notes || suggestedNotes} onChange={(event) => setNotes(event.target.value === suggestedNotes ? '' : event.target.value)} placeholder="Nội dung dành cho nhân viên" rows={2} /><small>Tự động cập nhật theo model, thẻ dịch vụ và sản phẩm đã chọn. Nội dung này không in trên bill.</small></label>
              <label className="invoice-notes-field print-notes-field"><span>Ghi chú</span><textarea value={printNotes} onChange={(event) => setPrintNotes(event.target.value)} placeholder="Thông tin cần hiển thị khi in bill" rows={2} maxLength={2000} /><small>Chỉ nội dung tại ô này được hiển thị trên bản in.</small></label>
            </div>
          </section>

          {invoiceType === 'Repair' && <section className="sales-section repair-intake-card">
            <div className="repair-section-toolbar">
              <div className="sales-section-title"><span><Wrench size={19} /></span><div><h3>THÔNG TIN MÁY SỬA CHỮA</h3></div></div>
              <button type="button" className="secondary-button" onClick={() => setRepairDevices((current) => [...current, newRepairDevice()])}><Plus size={17} /> Thêm máy</button>
            </div>
            <div className="repair-device-list">
              {repairDevices.map((device, index) => <article className="repair-device-card" key={device.key}>
                <header><div><span className="repair-device-number">Máy {index + 1}</span><strong>{device.deviceModel.trim() || 'Chưa nhập model'}</strong></div>{repairDevices.length > 1 && <button type="button" className="icon-button danger" onClick={() => setRepairDevices((current) => current.filter((item) => item.key !== device.key))} aria-label={`Xóa máy ${index + 1}`}><Trash2 size={17} /></button>}</header>
                <div className="sales-form-grid repair-device-fields">
                  <label><span>Model máy *</span><input value={device.deviceModel} onChange={(event) => updateRepairDevice(device.key, { deviceModel: event.target.value })} placeholder="Ví dụ: Dell Latitude 7420" required /></label>
                  <label><span>Phụ kiện nhận kèm</span><input value={device.accessories} onChange={(event) => updateRepairDevice(device.key, { accessories: event.target.value })} placeholder="Sạc, túi, chuột…" /></label>
                  <label><span>Tình trạng</span><input value={device.deviceCondition} onChange={(event) => updateRepairDevice(device.key, { deviceCondition: event.target.value })} placeholder="Có thể để trống" /></label>
                </div>
                <div className="repair-service-types">
                  <span>Loại dịch vụ *</span>
                  <div>{serviceTypesQuery.data?.filter((item) => item.isActive).map((item) => <label className={device.services.some((service) => service.serviceTypeId === item.id) ? 'is-selected' : ''} key={item.id}><input type="checkbox" checked={device.services.some((service) => service.serviceTypeId === item.id)} onChange={() => toggleRepairServiceType(device.key, item.id)} /><span>{item.name}</span></label>)}</div>
                  {!serviceTypesQuery.isLoading && device.services.length === 0 && <small>Chọn ít nhất một dịch vụ, sau đó nhập giá cho từng dịch vụ.</small>}
                </div>
                {device.services.length > 0 && <div className="repair-service-prices">
                  <div className="repair-service-prices-head"><span>Dịch vụ đã chọn</span><span>Ghi chú</span><span>Giá dịch vụ</span></div>
                  {device.services.map((service) => {
                    const serviceType = serviceTypesQuery.data?.find((item) => item.id === service.serviceTypeId)
                    const serviceName = serviceType?.name ?? 'Dịch vụ'
                    return <div className="repair-service-price-row" key={service.serviceTypeId}><strong>{serviceName}</strong><input value={service.notes} onChange={(event) => updateRepairServiceNotes(device.key, service.serviceTypeId, event.target.value)} placeholder="Ví dụ: LCD 15.6 inch, loa trong…" aria-label={`Ghi chú ${serviceName}`} /><MoneyInput ariaLabel={`Giá dịch vụ ${serviceName}`} value={service.amount} onChange={(value) => updateRepairServiceAmount(device.key, service.serviceTypeId, value)} /></div>
                  })}
                  <div className="repair-service-total"><span>Tổng chi phí máy {index + 1}</span><strong>{currency.format(device.services.reduce((sum, service) => sum + service.amount, 0))}</strong></div>
                </div>}
              </article>)}
            </div>
          </section>}

          {productSection}

          <section className="sales-section">
            <div className="sales-section-title"><span><Percent size={19} /></span><div><h3>KHUYẾN MÃI & VOUCHER</h3></div></div>
            <div className="sales-discount-grid">
              <label><span>Chương trình khuyến mãi</span><div className="discount-add-control"><span className="select-shell"><select value={promotionId} onChange={(event) => setPromotionId(event.target.value)}><option value="">Chọn chương trình…</option>{promotionsQuery.data?.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name} ({discountText(item.value, item.discountType)})</option>)}</select><ChevronDown size={15} /></span><button type="button" className="secondary-button" disabled={!selectedPromotion || subtotal < selectedPromotion.minimumOrderAmount || discounts.some((item) => item.source === 'Promotion' && item.promotionId === promotionId)} onClick={addPromotionDiscount}><Plus size={16} /> Thêm</button><button type="button" className="secondary-button manual-discount-button" onClick={() => setShowManualDiscount((current) => !current)}><Plus size={16} /> Thêm khuyến mãi nhập tay</button></div></label>
              <label><span>Mã voucher</span><div className="voucher-input"><input value={voucherCode} onChange={(event) => setVoucherCode(event.target.value.toUpperCase())} placeholder="Nhập mã…" /><button type="button" className="secondary-button" disabled={!voucherCode.trim() || subtotal <= 0 || voucherMutation.isPending || discounts.some((item) => item.source === 'Voucher' && item.voucherCode === voucherCode.trim())} onClick={() => voucherMutation.mutate()}>{voucherMutation.isPending ? 'Đang kiểm tra…' : 'Thêm voucher'}</button></div></label>
            </div>
            {showManualDiscount && <div className="manual-discount-entry">
              <label><span>Nội dung / lý do *</span><input value={manualDiscountName} onChange={(event) => setManualDiscountName(event.target.value)} placeholder="Ví dụ: Thu lại SSD của khách" /></label>
              <label><span>Số tiền giảm *</span><MoneyInput value={manualDiscountAmount} onChange={setManualDiscountAmount} ariaLabel="Số tiền khuyến mãi nhập tay" /></label>
              <button type="button" className="primary-button" disabled={!manualDiscountName.trim() || manualDiscountAmount <= 0 || subtotal <= 0} onClick={addManualDiscount}><Plus size={16} /> Thêm vào hóa đơn</button>
            </div>}
            <div className="sales-discount-lines">
              {resolvedDiscounts.map((discount) => <article key={discount.key}>
                <span className={`discount-source source-${discount.source.toLowerCase()}`}>{discount.source === 'Promotion' ? 'Khuyến mãi' : discount.source === 'Voucher' ? 'Voucher' : 'Nhập tay'}</span>
                <div><strong>{discount.name}</strong>{discount.voucherCode && <small>Mã: {discount.voucherCode}</small>}</div>
                <b>− {currency.format(discount.amount)}</b>
                <button type="button" className="icon-button" onClick={() => setDiscounts((current) => current.filter((item) => item.key !== discount.key))} aria-label={`Xóa ${discount.name}`}><Trash2 size={16} /></button>
              </article>)}
              {resolvedDiscounts.length === 0 && <div className="discount-lines-empty">Chưa áp dụng dòng khuyến mãi hoặc voucher nào.</div>}
            </div>
            {voucherMutation.isError && <div className="inline-note danger">{voucherMutation.error.message}</div>}
          </section>

        </main>

        <aside className="sales-editor-summary">
          <div className="sales-summary-card">
            <span className="eyebrow">TỔNG KẾT HÓA ĐƠN</span>
            <div className="summary-invoice-code"><span>Mã phiếu</span><strong>{invoiceQuery.data?.number ?? 'Tự động khi lưu'}</strong></div>
            <div className="summary-invoice-meta">
              <label><span>Người lập phiếu</span><input value={invoiceQuery.data?.issuer.name ?? user?.fullName ?? 'Nhân viên'} readOnly /></label>
              <div className="invoice-meta-field"><span>Ngày lập phiếu</span><VietnameseDateInput key={`invoice-date-${invoiceDate}`} ariaLabel="Ngày lập phiếu" value={invoiceDate} onChange={changeInvoiceDate} required /></div>
              <label><span>Trạng thái xử lý phiếu</span><select value={repairStatus} onChange={(event) => setRepairStatus(event.target.value as RepairStatus)}>
                <option value="InProgress">Đang xử lý</option>
                <option value="Returned">Đã xử lý</option>
                <option value="Cancelled">Đã hủy</option>
              </select></label>
              {invoiceType === 'Repair' && <div className="invoice-meta-field"><span>Ngày dự kiến trả máy</span><VietnameseDateInput key={`expected-return-${expectedReturnAt}`} ariaLabel="Ngày dự kiến trả máy" value={expectedReturnAt} onChange={setExpectedReturnAt} required /></div>}
            </div>
            <div className="summary-divider" />
            <h3>{invoiceType === 'Sale' ? `${cart.length} sản phẩm` : `${repairDevices.length} máy sửa chữa`}</h3>
            {invoicePurpose && <div className="summary-purpose"><ClipboardPenLine size={15} /><span>{invoicePurpose}</span></div>}
            <dl><div><dt>Tạm tính</dt><dd>{currency.format(subtotal)}</dd></div>{promotionDiscount > 0 && <div className="discount"><dt>Khuyến mãi</dt><dd>− {currency.format(promotionDiscount)}</dd></div>}{voucherDiscount > 0 && <div className="discount"><dt>Voucher</dt><dd>− {currency.format(voucherDiscount)}</dd></div>}<div className="grand-total"><dt>Khách cần trả</dt><dd>{currency.format(total)}</dd></div><div><dt>{repairStatus === 'Returned' ? 'Đã ghi nhận' : 'Phân bổ dự kiến'}</dt><dd className="paid">{currency.format(paid)}</dd></div><div className="remaining"><dt>{isInstallment ? 'Banking dự kiến' : 'Còn phải thu'}</dt><dd>{currency.format(outstanding)}</dd></div></dl>
            {paid > total && <div className="inline-note danger">Số tiền nhận đang vượt tổng hóa đơn.</div>}
            {isInstallment && outstanding > 0 && <div className="installment-summary"><Clock3 size={17} /><span><strong>Khoản banking tự động</strong><small>{paymentDueDate ? `Lên lịch: ${formatVietnameseDate(paymentDueDate)}` : 'Chưa chọn ngày dự kiến'}</small></span></div>}
          </div>
          {paymentSection}
          {selectedCustomerHistory && selectedCustomerHistory.invoices.length > 0 && <div className="sales-summary-card sales-customer-history-card">
            <div className="summary-customer-head"><span><UserRound size={18} /></span><div><small>KHÁCH HÀNG THÂN THUỘC</small><strong>{selectedCustomerHistory.fullName}</strong><em>{selectedCustomerHistory.code}</em></div></div>
            <div className="summary-customer-metrics"><span><small>Tổng tiền đã mua</small><strong>{currency.format(selectedCustomerHistory.totalSpent)}</strong></span><span><small>Hóa đơn gần đây</small><strong>{selectedCustomerHistory.invoices.length}</strong></span></div>
            <div className="summary-customer-invoices"><div><History size={15} /><span>Hóa đơn gần đây</span></div>{selectedCustomerHistory.invoices.slice(0, 5).map((invoice) => <article key={invoice.id}><span><strong>{invoice.number}</strong><small>{new Date(invoice.invoiceDateUtc).toLocaleDateString('vi-VN')}</small></span><b>{currency.format(invoice.totalAmount)}</b></article>)}</div>
          </div>}
        </aside>
      </div>
    </form>
  </div>
  {showCustomerEditor && <CustomerEditorDialog customerId={customerId} initialValues={{ fullName: customerName, phone: customerPhone, email: customerEmail, address: customerAddress, notes: customerNotes }} onClose={() => setShowCustomerEditor(false)} onSaved={customerSaved} />}
  </>
}

function MoneyInput({ value, onChange, ariaLabel }: { value: number; onChange: (value: number) => void; ariaLabel?: string }) {
  return <input
    className="money-input"
    type="text"
    inputMode="numeric"
    aria-label={ariaLabel}
    value={money.format(Math.max(0, Math.round(value || 0)))}
    onChange={(event) => onChange(parseMoney(event.target.value))}
    onFocus={(event) => event.currentTarget.select()}
  />
}

function VietnameseDateInput({ value, onChange, required, ariaLabel }: { value: string; onChange: (value: string) => void; required?: boolean; ariaLabel: string }) {
  const [draft, setDraft] = useState(() => formatVietnameseDate(value))

  function updateDraft(next: string) {
    setDraft(next)
    if (!next.trim()) {
      onChange('')
      return
    }
    const parsed = parseVietnameseDate(next, value)
    if (parsed) onChange(parsed)
  }

  function normalizeDraft() {
    if (!draft.trim()) {
      onChange('')
      return
    }
    const parsed = parseVietnameseDate(draft, value)
    setDraft(parsed ? formatVietnameseDate(parsed) : formatVietnameseDate(value))
  }

  function selectFromCalendar(next: string) {
    const merged = mergeDateWithExistingTime(next, value)
    setDraft(formatVietnameseDate(merged))
    onChange(merged)
  }

  return <div className="vietnamese-datetime-input">
    <input
      type="text"
      inputMode="numeric"
      aria-label={ariaLabel}
      title="Định dạng: ngày/tháng/năm"
      placeholder="dd/MM/yyyy"
      value={draft}
      required={required}
      onChange={(event) => updateDraft(event.target.value)}
      onBlur={normalizeDraft}
    />
    <span className="datetime-picker-control" title="Chọn ngày từ lịch">
      <CalendarDays size={18} />
      <input
        className="datetime-native-picker"
        type="date"
        value={datePartFromLocalDateTime(value)}
        onChange={(event) => selectFromCalendar(event.target.value)}
        onClick={(event) => {
          try { event.currentTarget.showPicker() } catch { /* Browser fallback: native click still opens the control. */ }
        }}
        aria-label={`Chọn ${ariaLabel.toLowerCase()} từ lịch`}
      />
    </span>
  </div>
}

function parseMoney(value: string) {
  const digits = value.replace(/\D/g, '')
  return digits ? Number(digits) : 0
}

function detectProductServiceTypeCode(name: string, categoryName?: string) {
  const normalized = `${categoryName ?? ''} ${name}`.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toUpperCase()
  if (/\b(LAPTOP|NOTEBOOK|MAY TINH XACH TAY)\b/.test(normalized)) return 'LAPTOP'
  if (/\b(PC|DESKTOP|MAY TINH DE BAN|FULL BO)\b/.test(normalized)) return 'PC'
  return ''
}

function formatServiceName(name: string) {
  const acronyms = new Set(['BIOS', 'CPU', 'GPU', 'HDD', 'LCD', 'PC', 'RAM', 'SSD', 'USB', 'WIFI'])
  return name.trim().toLocaleLowerCase('vi-VN').split(/\s+/).map((word, index) => {
    const upper = word.toLocaleUpperCase('vi-VN')
    if (acronyms.has(upper)) return upper
    return index === 0 ? `${word.charAt(0).toLocaleUpperCase('vi-VN')}${word.slice(1)}` : word
  }).join(' ')
}

function formatRepairServiceDescription(name: string, notes?: string) {
  const formattedName = formatServiceName(name)
  const normalizedNotes = notes?.trim()
  return normalizedNotes ? `${formattedName} (${normalizedNotes})` : formattedName
}

function isAutomaticRepairNote(value: string | undefined, repairs: RepairOrder[], lines: SalesInvoiceDetails['lines']) {
  if (!value?.trim()) return true
  const normalizedValue = value.trim()
  const models = repairs.map((repair, index) => repair.deviceModel.trim() || `Máy ${index + 1}`)
  if (models.length === 0) return false
  const repairSummary = repairs.map((repair, index) => {
    const model = repair.deviceModel.trim() || `Máy ${index + 1}`
    const services = repair.services?.length
      ? repair.services.map((service) => formatRepairServiceDescription(service.name, service.notes)).filter(Boolean)
      : repair.serviceTypes?.map((service) => formatRepairServiceDescription(service.name)).filter(Boolean) ?? []
    return services.length > 0 ? `Sửa (Thay) ${services.join(', ')} cho ${model}` : `Sửa chữa cho ${model}`
  })
  const unformattedRepairSummary = repairs.map((repair, index) => {
    const model = repair.deviceModel.trim() || `Máy ${index + 1}`
    const services = repair.services?.length
      ? repair.services.map((service) => service.name).filter(Boolean)
      : repair.serviceTypes?.map((service) => service.name).filter(Boolean) ?? []
    return services.length > 0 ? `Sửa (Thay) ${services.join(', ')} cho ${model}` : `Sửa chữa cho ${model}`
  })
  const extraProducts = lines.filter((line) => line.sku !== 'DICH-VU-SUA-CHUA').map((line) => line.productName.trim()).filter(Boolean)
  const generatedSummary = [...repairSummary, ...extraProducts].join(' + ')
  const unformattedGeneratedSummary = [...unformattedRepairSummary, ...extraProducts].join(' + ')
  const modelSummary = models.join(' + ')
  const legacySummary = repairs.slice(0, 2)
    .map((repair) => [repair.deviceModel.trim(), repair.deviceCondition.trim()].filter(Boolean).join(' — '))
    .join(' + ') + (repairs.length > 2 ? ` + ${repairs.length - 2} máy khác` : '')
  return normalizedValue === generatedSummary || normalizedValue === unformattedGeneratedSummary || normalizedValue === modelSummary || normalizedValue === legacySummary || normalizedValue === models[0]
}

function isAutomaticSaleNote(value: string | undefined, lines: SalesInvoiceDetails['lines']) {
  if (!value?.trim()) return true
  const productNames = lines.map((line) => line.productName.trim()).filter(Boolean)
  if (productNames.length === 0) return false
  const generatedSummary = productNames.join(' + ')
  const legacyNames = productNames.slice(0, 2)
  const legacySummary = `${legacyNames.join(' + ')}${productNames.length > 2 ? ` + ${productNames.length - 2} sản phẩm khác` : ''}`
  return value.trim() === generatedSummary || value.trim() === legacySummary || value.trim() === productNames[0]
}

function normalizePhone(value: string) {
  return value.replace(/\D/g, '')
}

function normalizeProcessingStatus(status?: RepairStatus): RepairStatus {
  if (status === 'Returned' || status === 'Cancelled') return status
  return 'InProgress'
}

function withCurrentOption(options: string[], current: string) {
  return current && !options.includes(current) ? [current, ...options] : options
}

function calculateDiscount(type: Promotion['discountType'], value: number, maximum: number | undefined, subtotal: number) {
  const discount = type === 'Percentage' ? subtotal * value / 100 : value
  return Math.max(0, Math.min(subtotal, maximum ? Math.min(discount, maximum) : discount))
}

function discountText(value: number, type: Promotion['discountType']) {
  return type === 'Percentage' ? `${value}%` : currency.format(value)
}

function toDateTimeLocal(date: Date) {
  const offset = date.getTimezoneOffset()
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16)
}

function formatVietnameseDate(value: string) {
  if (!value) return ''
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (!match) return ''
  return `${match[3]}/${match[2]}/${match[1]}`
}

function parseVietnameseDate(value: string, existingValue: string) {
  const match = value.trim().match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/)
  if (!match) return null
  const [, dayText, monthText, yearText] = match
  const day = Number(dayText)
  const month = Number(monthText)
  const year = Number(yearText)
  const date = new Date(year, month - 1, day)
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null
  return mergeDateWithExistingTime(`${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`, existingValue)
}

function datePartFromLocalDateTime(value: string) {
  return value.match(/^(\d{4}-\d{2}-\d{2})/)?.[1] ?? ''
}

function mergeDateWithExistingTime(date: string, existingValue: string) {
  if (!date) return ''
  const time = existingValue.match(/T(\d{2}:\d{2})/)?.[1] ?? '00:00'
  return `${date}T${time}`
}

function addDaysToLocalDateTime(value: string, days: number) {
  const date = value ? new Date(value) : new Date()
  date.setDate(date.getDate() + days)
  return toDateTimeLocal(date)
}

function fromDateTimeLocal(value: string) {
  return value ? new Date(value).toISOString() : null
}
