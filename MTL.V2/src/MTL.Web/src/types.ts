export type UserSession = {
  id: string
  companyId: string
  email: string
  fullName: string
  mustChangePassword: boolean
  roles: string[]
  permissions: string[]
}

export type PagedResult<T> = {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export type Product = {
  id: string
  sku: string
  name: string
  salePrice: number
  regularPrice?: number
  costPrice: number
  stockQuantity: number
  trackInventory: boolean
  availability: 'InStock' | 'OutOfStock' | 'OnBackorder'
  status: 'Draft' | 'Active' | 'Inactive' | 'Discontinued'
  syncState: 'NotLinked' | 'Synced' | 'PendingPush' | 'PendingPull' | 'Conflict' | 'Failed'
  primaryImageUrl?: string
  brand?: string
  categoryName?: string
  wooCategoryId?: number
  wooProductId?: number
  wooPermalink?: string
  createdAtUtc: string
  updatedAtUtc: string
  concurrencyToken: string
}

export type ProductDetails = Omit<Product, 'createdAtUtc' | 'updatedAtUtc'> & {
  shortDescription?: string
  description?: string
  unit: string
  wooCategoryIds: number[]
  images: ProductImage[]
}

export type ProductImage = {
  wooMediaId?: number
  url: string
  thumbnailUrl?: string
  name?: string
  altText?: string
  position: number
}

export type MediaItem = {
  id: number
  url: string
  thumbnailUrl?: string
  title: string
  altText?: string
  mimeType?: string
  width?: number
  height?: number
  createdAt?: string
}

export type MediaPage = {
  items: MediaItem[]
  total: number
  page: number
  pageSize: number
  uploadEnabled: boolean
  source: 'wordpress' | 'postgresql-cache'
}

export type ProductFilterOptions = {
  categories: Array<{
    name: string
    wooCategoryId?: number
    parentWooCategoryId?: number
    depth: number
    path: string
    count: number
  }>
  brands: Array<{ name: string; count: number }>
}

export type BulkProductResult = {
  processed: number
  failed: number
  errors: string[]
}

export type Customer = {
  id: string
  code: string
  fullName: string
  phone?: string
  email?: string
  citizenId?: string
  totalSpent: number
  lastPurchaseAtUtc?: string
}

export type CustomerDetails = Customer & {
  address?: string
  taxCode?: string
  birthday?: string
  notes?: string
}

export type SalesInvoiceType = 'Sale' | 'Repair'
export type InvoiceStatus = 'Draft' | 'Confirmed' | 'PartiallyPaid' | 'Paid' | 'Cancelled' | 'Refunded'
export type PaymentMethod = 'Cash' | 'BankTransfer'
export type DiscountType = 'FixedAmount' | 'Percentage'
export type SalesDiscountSource = 'Promotion' | 'Voucher' | 'Manual'
export type RepairStatus = 'Received' | 'Diagnosing' | 'Quoted' | 'InProgress' | 'ReadyForReturn' | 'Returned' | 'Cancelled'

export type SalesSummary = {
  invoiceTotalToday: number
  collectedToday: number
  outstanding: number
  activeRepairs: number
  readyForReturnRepairs: number
}

export type SalesInvoice = {
  id: string
  number: string
  type: SalesInvoiceType
  customerId?: string
  customerName: string
  customerPhone?: string
  invoiceDateUtc: string
  createdByName: string
  status: InvoiceStatus
  totalAmount: number
  paidAmount: number
  outstandingAmount: number
  receivedAmount: number
  scheduledAmount: number
  isInstallment: boolean
  paymentDueDateUtc?: string
  repairDeviceModel?: string
  expectedReturnAtUtc?: string
  repairStatus?: RepairStatus
  repairDeviceCount: number
  repairPendingCount: number
  repairReadyCount: number
  repairReturnedCount: number
  serviceTypes: ServiceType[]
  notes?: string
  customerTotalSpent: number
  customerInvoiceCount: number
}

export type SalesInvoiceDetails = SalesInvoice & {
  customerEmail?: string
  customerAddress?: string
  subtotal: number
  discountAmount: number
  promotionId?: string
  promotionName?: string
  promotionDiscountAmount: number
  voucherCode?: string
  voucherDiscountAmount: number
  installmentProvider?: string
  installmentReference?: string
  installmentPaymentAccountId?: string
  installmentPaymentAccountName?: string
  company: CompanySettings
  issuer: { id?: string; name: string; email?: string }
  discounts: SalesDiscountLine[]
  lines: SalesInvoiceLine[]
  payments: SalesPayment[]
  repair?: RepairOrder
  repairs: RepairOrder[]
}

export type SalesDiscountLine = {
  id: string
  source: SalesDiscountSource
  promotionId?: string
  voucherId?: string
  name: string
  code?: string
  amount: number
  notes?: string
}

export type SalesInvoiceLine = {
  id: string
  productId?: string
  sku: string
  productName: string
  quantity: number
  unitName: string
  warrantyPeriod: string
  unitPrice: number
  discountAmount: number
  lineTotal: number
  notes?: string
}

export type SalesPayment = {
  id: string
  method: PaymentMethod
  paymentAccountId?: string
  paymentAccountName?: string
  amount: number
  paidAtUtc: string
  isScheduled: boolean
  reference?: string
  notes?: string
}

export type RepairOrder = {
  id: string
  deviceModel: string
  serialNumber?: string
  accessories?: string
  deviceCondition: string
  customerRequest: string
  technicianAssessment?: string
  estimatedCost: number
  expectedReturnAtUtc?: string
  status: RepairStatus
  notes?: string
  serviceTypes: ServiceType[]
  services: RepairService[]
}

export type RepairService = {
  id: string
  name: string
  code: string
  amount: number
  notes?: string
}

export type ServiceType = {
  id: string
  name: string
  code: string
  isActive: boolean
  displayOrder: number
}

export type PaymentAccount = {
  id: string
  name: string
  bankName: string
  accountNumber: string
  accountHolder: string
  isActive: boolean
  displayOrder: number
}

export type PurchaseStatus = 'InProgress' | 'Processed' | 'Cancelled'

export type Supplier = {
  id: string
  code: string
  name: string
  contactName?: string
  phone?: string
  email?: string
  address?: string
  taxCode?: string
  notes?: string
  isActive: boolean
}

export type PurchaseSummary = {
  processedValue: number
  supplierDebt: number
  receivedToday: number
  inProgressCount: number
}

export type PurchaseInvoice = {
  id: string
  number: string
  supplierId: string
  supplierName: string
  supplierTaxCode?: string
  invoiceDateUtc: string
  receivedAtUtc: string
  dueDateUtc?: string
  status: PurchaseStatus
  subtotal: number
  discountAmount: number
  taxAmount: number
  totalAmount: number
  paidAmount: number
  outstandingAmount: number
  supplierInvoiceNumber?: string
  supplierInvoiceSeries?: string
  warehouseReceiptNumber?: string
  inventoryPosted: boolean
  lineCount: number
  receivedQuantity: number
  notes?: string
}

export type PurchaseInvoiceDetails = Omit<PurchaseInvoice, 'supplierId' | 'supplierName' | 'supplierTaxCode' | 'outstandingAmount' | 'lineCount' | 'receivedQuantity'> & {
  supplier: Supplier
  lines: PurchaseInvoiceLine[]
  payments: PurchasePayment[]
}

export type PurchaseInvoiceLine = {
  id: string
  productId?: string
  sku: string
  productName: string
  quantity: number
  receivedQuantity: number
  unitName: string
  unitCost: number
  discountAmount: number
  vatRate?: number
  taxAmount: number
  lineTotal: number
  serialNumbers: string[]
  notes?: string
}

export type PurchasePayment = {
  id: string
  method: PaymentMethod
  paymentAccountId?: string
  paymentAccountName?: string
  amount: number
  paidAtUtc: string
  notes?: string
}

export type InstallmentProvider = {
  id: string
  name: string
  code: string
  isActive: boolean
  displayOrder: number
}

export type Promotion = {
  id: string
  name: string
  description?: string
  discountType: DiscountType
  value: number
  minimumOrderAmount: number
  maximumDiscountAmount?: number
  startsAtUtc?: string
  endsAtUtc?: string
  isActive: boolean
}

export type Voucher = {
  id: string
  code: string
  name: string
  discountType: DiscountType
  value: number
  minimumOrderAmount: number
  maximumDiscountAmount?: number
  startsAtUtc?: string
  endsAtUtc?: string
  usageLimit?: number
  usedCount: number
  isActive: boolean
}

export type VoucherValidation = {
  id: string
  code: string
  name: string
  discountAmount: number
}

export type CustomerLookup = {
  id: string
  code: string
  fullName: string
  phone?: string
  email?: string
  address?: string
  citizenId?: string
  notes?: string
  totalSpent: number
  lastPurchaseAtUtc?: string
  invoices: Array<{
    id: string
    number: string
    type: SalesInvoiceType
    invoiceDateUtc: string
    status: InvoiceStatus
    totalAmount: number
    paidAmount: number
    outstandingAmount: number
  }>
}

export type Employee = {
  id: string
  fullName: string
  email: string
  phone?: string
  isActive: boolean
  mustChangePassword: boolean
  lockoutEndUtc?: string
  lastLoginAtUtc?: string
  roles: string[]
}

export type EmployeeRole = {
  id: string
  name: string
  permissions: string[]
  isSystem: boolean
  userCount: number
}

export type PermissionItem = {
  key: string
  group: string
  label: string
}

export type CompanySettings = {
  id?: string
  name: string
  code?: string
  taxCode?: string
  address?: string
  phone?: string
  email?: string
  website?: string
  logoDataUrl?: string
}

export type InvoiceSettings = {
  defaultRepairTurnaroundDays: number
}

