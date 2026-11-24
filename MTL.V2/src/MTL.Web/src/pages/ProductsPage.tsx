import {
  AlertTriangle,
  CalendarDays,
  CheckSquare,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  DownloadCloud,
  ExternalLink,
  ImageOff,
  LayoutGrid,
  PackageOpen,
  PackageX,
  Pencil,
  Plus,
  Printer,
  RefreshCw,
  Rows3,
  Search,
  SlidersHorizontal,
  Trash2,
  UploadCloud,
  X,
} from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useDeferredValue, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { PageHeader } from '../components/PageHeader'
import { ProductEditorDialog } from '../components/ProductEditorDialog'
import { ProductLabelPrintDialog } from '../components/ProductLabelPrintDialog'
import { StatusBadge } from '../components/StatusBadge'
import { api } from '../lib/api'
import type { BulkProductResult, PagedResult, Product, ProductDetails, ProductFilterOptions } from '../types'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const dateFormat = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })

type ViewMode = 'grid' | 'card'
type StockFilter = 'all' | 'in_stock' | 'out_of_stock' | 'on_backorder'

export function ProductsPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [viewMode, setViewMode] = useState<ViewMode>(() => localStorage.getItem('mtl-products-view') === 'card' ? 'card' : 'grid')
  const [stock, setStock] = useState<StockFilter>('all')
  const [status, setStatus] = useState('')
  const [syncState, setSyncState] = useState('')
  const [category, setCategory] = useState('')
  const [brand, setBrand] = useState('')
  const [warning, setWarning] = useState('all')
  const [sort, setSort] = useState('newest')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(24)
  const [editorProductId, setEditorProductId] = useState<string | 'new' | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const selectedProductsRef = useRef<Map<string, Product>>(new Map())
  const [labelProducts, setLabelProducts] = useState<Product[] | null>(null)
  const [saveNotice, setSaveNotice] = useState('')
  const { hasPermission } = useAuth()
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: ['products', deferredSearch, stock, status, syncState, category, brand, warning, sort, page, pageSize],
    queryFn: () => {
      const parameters = new URLSearchParams({ page: String(page), pageSize: String(pageSize), stock, sort })
      if (deferredSearch) parameters.set('search', deferredSearch)
      if (status) parameters.set('status', status)
      if (syncState) parameters.set('syncState', syncState)
      if (category.startsWith('id:')) parameters.set('categoryId', category.slice(3))
      if (category.startsWith('name:')) parameters.set('category', category.slice(5))
      if (brand) parameters.set('brand', brand)
      if (warning !== 'all') parameters.set('warning', warning)
      return api.get<PagedResult<Product>>(`/api/products?${parameters.toString()}`)
    },
    placeholderData: (previous) => previous,
  })
  const filterOptionsQuery = useQuery({
    queryKey: ['product-filter-options'],
    queryFn: () => api.get<ProductFilterOptions>('/api/products/filter-options'),
    staleTime: 5 * 60 * 1000,
  })

  const syncMutation = useMutation({
    mutationFn: () => api.post('/api/integrations/woocommerce/sync/products'),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products'] })
      void queryClient.invalidateQueries({ queryKey: ['product-filter-options'] })
    },
  })
  const pushMutation = useMutation({
    mutationFn: (id: string) => api.post(`/api/products/${id}/push-to-woocommerce`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['products'] }),
  })
  const bulkMutation = useMutation({
    mutationFn: (action: string) => api.post<BulkProductResult>('/api/products/bulk', { productIds: [...selectedIds], action }),
    onSuccess: () => {
      selectedProductsRef.current.clear()
      setSelectedIds(new Set())
      void queryClient.invalidateQueries({ queryKey: ['products'] })
      void queryClient.invalidateQueries({ queryKey: ['product-filter-options'] })
    },
  })

  const total = query.data?.total ?? 0
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const firstItem = total === 0 ? 0 : (page - 1) * pageSize + 1
  const lastItem = Math.min(page * pageSize, total)
  const hasActiveFilters = Boolean(search || stock !== 'all' || status || syncState || category || brand || warning !== 'all' || sort !== 'newest')

  function changeView(mode: ViewMode) {
    setViewMode(mode)
    localStorage.setItem('mtl-products-view', mode)
  }

  function resetFilters() {
    setSearch('')
    setStock('all')
    setStatus('')
    setSyncState('')
    setCategory('')
    setBrand('')
    setWarning('all')
    setSort('newest')
    setPage(1)
  }

  function toggleSelection(id: string) {
    setSelectedIds((current) => {
      const next = new Set(current)
      if (next.has(id)) {
        next.delete(id)
        selectedProductsRef.current.delete(id)
      } else if (next.size < 100) {
        next.add(id)
        const product = query.data?.items.find((item) => item.id === id)
        if (product) selectedProductsRef.current.set(id, product)
      }
      return next
    })
  }

  function togglePageSelection(products: Product[]) {
    setSelectedIds((current) => {
      const next = new Set(current)
      const allSelected = products.every((product) => next.has(product.id))
      products.forEach((product) => {
        if (allSelected) {
          next.delete(product.id)
          selectedProductsRef.current.delete(product.id)
        } else if (next.size < 100) {
          next.add(product.id)
          selectedProductsRef.current.set(product.id, product)
        }
      })
      return next
    })
  }

  function clearSelection() {
    selectedProductsRef.current.clear()
    setSelectedIds(new Set())
  }

  function openLabelPrint() {
    query.data?.items.forEach((product) => {
      if (selectedIds.has(product.id)) selectedProductsRef.current.set(product.id, product)
    })
    const products = [...selectedIds].map((id) => selectedProductsRef.current.get(id)).filter((product): product is Product => Boolean(product))
    if (products.length > 0) setLabelProducts(products)
  }

  function runBulk(action: string) {
    if (action === 'trash' && !window.confirm(`Đưa ${selectedIds.size} sản phẩm vào thùng rác trên WooCommerce?`)) return
    bulkMutation.mutate(action)
  }

  return (
    <>
      <PageHeader
        eyebrow="DANH MỤC"
        title="Sản phẩm"
        description="Theo dõi giá, tồn kho và trạng thái đồng bộ với mytholaptop.vn trên một màn hình."
        actions={
          <>
            {hasPermission('woocommerce.manage') && <button className="secondary-button" onClick={() => syncMutation.mutate()} disabled={syncMutation.isPending}><DownloadCloud size={18} /> {syncMutation.isPending ? 'Đang đồng bộ…' : 'Lấy từ Woo'}</button>}
            {hasPermission('products.manage') && <button className="primary-button" onClick={() => setEditorProductId('new')}><Plus size={18} /> Thêm sản phẩm</button>}
          </>
        }
      />

      {(syncMutation.isError || pushMutation.isError || bulkMutation.isError || query.isError || filterOptionsQuery.isError) && <div className="form-alert">{(syncMutation.error ?? pushMutation.error ?? bulkMutation.error ?? query.error ?? filterOptionsQuery.error)?.message}</div>}
      {saveNotice && <div className="form-success product-save-notice">{saveNotice}</div>}
      {bulkMutation.data && <div className={bulkMutation.data.failed ? 'form-alert' : 'form-success'}>Đã xử lý {bulkMutation.data.processed} sản phẩm{bulkMutation.data.failed ? `, ${bulkMutation.data.failed} sản phẩm lỗi.` : ' thành công.'}</div>}

      <section className="panel product-browser">
        <div className="product-toolbar">
          <label className="search-box product-search">
            <Search size={19} />
            <input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1) }} placeholder="Tìm tên, SKU, thương hiệu hoặc danh mục…" />
            {search && <button type="button" className="search-clear" onClick={() => { setSearch(''); setPage(1) }} aria-label="Xóa nội dung tìm kiếm"><X size={16} /></button>}
          </label>

          <div className="product-toolbar-right">
            <span className="product-total"><strong>{total.toLocaleString('vi-VN')}</strong> sản phẩm</span>
            <div className="view-switcher" aria-label="Kiểu hiển thị">
              <button className={viewMode === 'grid' ? 'is-active' : ''} onClick={() => changeView('grid')} aria-pressed={viewMode === 'grid'} title="Hiển thị dạng bảng"><Rows3 size={17} /> Grid</button>
              <button className={viewMode === 'card' ? 'is-active' : ''} onClick={() => changeView('card')} aria-pressed={viewMode === 'card'} title="Hiển thị dạng thẻ"><LayoutGrid size={17} /> Card</button>
            </div>
          </div>
        </div>

        <div className="product-filters">
          <div className="filter-heading"><SlidersHorizontal size={17} /><span>Bộ lọc</span></div>
          <FilterSelect label="Tồn kho" value={stock} onChange={(value) => { setStock(value as StockFilter); setPage(1) }}>
            <option value="all">Tất cả tồn kho</option><option value="in_stock">Còn hàng</option><option value="out_of_stock">Hết hàng</option><option value="on_backorder">Cho phép đặt trước</option>
          </FilterSelect>
          <FilterSelect label="Trạng thái" value={status} onChange={(value) => { setStatus(value); setPage(1) }}>
            <option value="">Tất cả trạng thái</option><option value="Active">Đang bán</option><option value="Draft">Bản nháp</option><option value="Inactive">Tạm ẩn</option><option value="Discontinued">Ngừng kinh doanh</option>
          </FilterSelect>
          <FilterSelect label="Đồng bộ" value={syncState} onChange={(value) => { setSyncState(value); setPage(1) }}>
            <option value="">Tất cả đồng bộ</option><option value="Synced">Đã đồng bộ</option><option value="NotLinked">Chưa liên kết</option><option value="PendingPush">Chờ đẩy lên Woo</option><option value="PendingPull">Chờ lấy từ Woo</option><option value="Conflict">Xung đột</option><option value="Failed">Đồng bộ lỗi</option>
          </FilterSelect>
          <FilterSelect label="Danh mục" value={category} onChange={(value) => { setCategory(value); setPage(1) }}>
            <option value="">Tất cả danh mục</option>{filterOptionsQuery.data?.categories.map((item) => <option key={`${item.wooCategoryId}-${item.path}`} value={item.wooCategoryId ? `id:${item.wooCategoryId}` : `name:${item.name}`}>{`${'— '.repeat(item.depth)}${item.name}`} ({item.count})</option>)}
          </FilterSelect>
          <FilterSelect label="Thương hiệu" value={brand} onChange={(value) => { setBrand(value); setPage(1) }}>
            <option value="">Tất cả thương hiệu</option>{filterOptionsQuery.data?.brands.map((item) => <option key={item.name} value={item.name}>{item.name} ({item.count})</option>)}
          </FilterSelect>
          <FilterSelect label="Cảnh báo" value={warning} onChange={(value) => { setWarning(value); setPage(1) }}>
            <option value="all">Tất cả dữ liệu</option><option value="missing_any">Thiếu ảnh hoặc giá</option><option value="missing_image">Thiếu ảnh</option><option value="missing_price">Thiếu giá bán</option>
          </FilterSelect>
          <FilterSelect label="Sắp xếp" value={sort} onChange={(value) => { setSort(value); setPage(1) }}>
            <option value="newest">Mới tạo gần đây</option><option value="oldest">Cũ nhất trước</option><option value="price_desc">Giá cao đến thấp</option><option value="price_asc">Giá thấp đến cao</option><option value="name_asc">Tên A–Z</option><option value="stock_desc">Tồn kho nhiều nhất</option>
          </FilterSelect>
          {hasActiveFilters && <button className="clear-filters" type="button" onClick={resetFilters}><X size={15} /> Xóa lọc</button>}
        </div>

        {hasPermission('products.manage') && selectedIds.size > 0 && <BulkActionBar count={selectedIds.size} pending={bulkMutation.isPending} onAction={runBulk} onPrint={openLabelPrint} onClear={clearSelection} />}

        {viewMode === 'grid'
          ? <ProductTable products={query.data?.items ?? []} isLoading={query.isLoading} canPush={hasPermission('woocommerce.manage')} canEdit={hasPermission('products.manage')} selectedIds={selectedIds} onToggle={toggleSelection} onTogglePage={togglePageSelection} onEdit={setEditorProductId} onPush={(id) => pushMutation.mutate(id)} />
          : <ProductCards products={query.data?.items ?? []} isLoading={query.isLoading} canPush={hasPermission('woocommerce.manage')} canEdit={hasPermission('products.manage')} selectedIds={selectedIds} onToggle={toggleSelection} onEdit={setEditorProductId} onPush={(id) => pushMutation.mutate(id)} onResetFilters={resetFilters} />}

        {!query.isLoading && query.data?.items.length === 0 && viewMode === 'grid' && <EmptyProducts onResetFilters={resetFilters} />}

        <div className="pagination-bar">
          <div className="pagination-summary">
            <span>Hiển thị <strong>{firstItem}–{lastItem}</strong> trên <strong>{total.toLocaleString('vi-VN')}</strong></span>
            <label><span>Mỗi trang</span><select value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1) }}><option value={24}>24</option><option value={48}>48</option><option value={96}>96</option></select></label>
          </div>
          <nav className="pagination" aria-label="Phân trang sản phẩm">
            <button onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1} aria-label="Trang trước"><ChevronLeft size={18} /></button>
            {paginationItems(page, pageCount).map((item, index) => item === '…' ? <span key={`ellipsis-${index}`}>…</span> : <button key={item} className={item === page ? 'is-active' : ''} onClick={() => setPage(item)} aria-current={item === page ? 'page' : undefined}>{item}</button>)}
            <button onClick={() => setPage((value) => Math.min(pageCount, value + 1))} disabled={page >= pageCount} aria-label="Trang sau"><ChevronRight size={18} /></button>
          </nav>
        </div>
      </section>

      {editorProductId && <ProductEditorDialog
        productId={editorProductId === 'new' ? undefined : editorProductId}
        filterOptions={filterOptionsQuery.data}
        onClose={() => setEditorProductId(null)}
        onSaved={(savedProduct: ProductDetails, wasEditing: boolean) => {
          setEditorProductId(null)
          setPage(1)
          setSaveNotice(wasEditing ? `Đã cập nhật “${savedProduct.name}” thành công.` : `Đã thêm “${savedProduct.name}” thành công.`)
          void queryClient.invalidateQueries({ queryKey: ['products'] })
          void queryClient.invalidateQueries({ queryKey: ['product-filter-options'] })
        }}
        onDeleted={() => { setEditorProductId(null); clearSelection(); void queryClient.invalidateQueries({ queryKey: ['products'] }); void queryClient.invalidateQueries({ queryKey: ['product-filter-options'] }) }}
      />}
      {labelProducts && <ProductLabelPrintDialog products={labelProducts} onClose={() => setLabelProducts(null)} />}
    </>
  )
}

function FilterSelect({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: ReactNode }) {
  return <label className="filter-select"><span>{label}</span><span className="select-shell"><select value={value} onChange={(event) => onChange(event.target.value)}>{children}</select><ChevronDown size={15} /></span></label>
}

function ProductTable({ products, isLoading, canPush, canEdit, selectedIds, onToggle, onTogglePage, onEdit, onPush }: { products: Product[]; isLoading: boolean; canPush: boolean; canEdit: boolean; selectedIds: Set<string>; onToggle: (id: string) => void; onTogglePage: (products: Product[]) => void; onEdit: (id: string) => void; onPush: (id: string) => void }) {
  const allSelected = products.length > 0 && products.every((product) => selectedIds.has(product.id))
  return <div className="data-table-wrap product-table-wrap"><table className="data-table product-table"><thead><tr><th className="select-column"><input type="checkbox" checked={allSelected} disabled={!canEdit || products.length === 0} onChange={() => onTogglePage(products)} aria-label="Chọn tất cả sản phẩm trang này" /></th><th>Sản phẩm</th><th>Giá bán</th><th>Giá vốn</th><th>Tồn kho</th><th>Trạng thái</th><th>Đồng bộ</th><th>Ngày tạo</th><th /></tr></thead><tbody>
    {isLoading && <tr><td colSpan={9} className="empty-cell"><RefreshCw className="spin" /> Đang tải dữ liệu…</td></tr>}
    {!isLoading && products.map((product) => {
      const outOfStock = product.availability === 'OutOfStock'
      const onBackorder = product.availability === 'OnBackorder'
      return <tr key={product.id} className={`product-row${outOfStock ? ' is-out-of-stock' : ''}${onBackorder ? ' is-backorder' : ''}`}>
        <td className="select-column"><input type="checkbox" checked={selectedIds.has(product.id)} disabled={!canEdit} onChange={() => onToggle(product.id)} aria-label={`Chọn ${product.name}`} /></td>
        <td><div className="product-cell"><ProductThumbnail product={product} canEdit={canEdit} onEdit={onEdit} /><span className="product-identity"><button type="button" onClick={() => canEdit && onEdit(product.id)} disabled={!canEdit}>{product.name}</button><small>{product.sku}{product.categoryName ? ` · ${product.categoryName}` : ''}</small><ProductWarnings product={product} /></span></div></td>
        <td><strong className="sale-price">{currency.format(product.salePrice)}</strong>{product.regularPrice && product.regularPrice > product.salePrice ? <small className="regular-price">{currency.format(product.regularPrice)}</small> : null}</td>
        <td className="muted-money">{currency.format(product.costPrice)}</td>
        <td><AvailabilityBadge product={product} /></td>
        <td><StatusBadge tone={product.status === 'Active' ? 'success' : 'neutral'}>{statusLabel(product.status)}</StatusBadge></td>
        <td><StatusBadge tone={syncTone(product.syncState)}>{syncLabel(product.syncState)}</StatusBadge></td>
        <td><span className="date-cell"><CalendarDays size={14} /> {dateFormat.format(new Date(product.createdAtUtc))}</span></td>
        <td><div className="product-row-actions">{product.wooPermalink && <a className="icon-button" href={product.wooPermalink} target="_blank" rel="noreferrer" title="Mở trên website"><ExternalLink size={17} /></a>}{canEdit && <button className="icon-button" title="Chỉnh sửa" onClick={() => onEdit(product.id)}><Pencil size={17} /></button>}{canPush && <button className="icon-button" title="Đẩy lên WooCommerce" onClick={() => onPush(product.id)}><UploadCloud size={18} /></button>}</div></td>
      </tr>
    })}
  </tbody></table></div>
}

function ProductCards({ products, isLoading, canPush, canEdit, selectedIds, onToggle, onEdit, onPush, onResetFilters }: { products: Product[]; isLoading: boolean; canPush: boolean; canEdit: boolean; selectedIds: Set<string>; onToggle: (id: string) => void; onEdit: (id: string) => void; onPush: (id: string) => void; onResetFilters: () => void }) {
  if (isLoading) return <div className="product-card-grid">{Array.from({ length: 12 }, (_, index) => <div className="product-card skeleton-card" key={index} />)}</div>
  if (products.length === 0) return <EmptyProducts onResetFilters={onResetFilters} />

  return <div className="product-card-grid">{products.map((product) => {
    const outOfStock = product.availability === 'OutOfStock'
    const onBackorder = product.availability === 'OnBackorder'
    return <article className={`product-card${outOfStock ? ' is-out-of-stock' : ''}${onBackorder ? ' is-backorder' : ''}${selectedIds.has(product.id) ? ' is-selected' : ''}`} key={product.id}>
      {canEdit && <label className="card-selector"><input type="checkbox" checked={selectedIds.has(product.id)} onChange={() => onToggle(product.id)} aria-label={`Chọn ${product.name}`} /><span><CheckSquare size={15} /></span></label>}
      {canEdit ? <button type="button" className="product-card-media product-media-edit" onClick={() => onEdit(product.id)} aria-label={`Sửa ${product.name}`} title="Nhấp vào ảnh để sửa sản phẩm">
        {product.primaryImageUrl ? <img src={product.primaryImageUrl} alt={product.name} loading="lazy" /> : <div className="product-image-fallback"><PackageOpen size={34} /><span>Chưa có ảnh</span></div>}
        {(outOfStock || onBackorder) && <span className={`out-of-stock-ribbon${onBackorder ? ' backorder' : ''}`}>{outOfStock ? <PackageX size={14} /> : <PackageOpen size={14} />} {outOfStock ? 'Hết hàng' : 'Đặt trước'}</span>}
        <span className={`card-status ${product.status === 'Active' ? 'active' : ''}`}>{statusLabel(product.status)}</span>
      </button> : product.wooPermalink ? <a className="product-card-media" href={product.wooPermalink} target="_blank" rel="noreferrer" title="Mở sản phẩm trên website">
        {product.primaryImageUrl ? <img src={product.primaryImageUrl} alt={product.name} loading="lazy" /> : <div className="product-image-fallback"><PackageOpen size={34} /><span>Chưa có ảnh</span></div>}
        {(outOfStock || onBackorder) && <span className={`out-of-stock-ribbon${onBackorder ? ' backorder' : ''}`}>{outOfStock ? <PackageX size={14} /> : <PackageOpen size={14} />} {outOfStock ? 'Hết hàng' : 'Đặt trước'}</span>}
        <span className={`card-status ${product.status === 'Active' ? 'active' : ''}`}>{statusLabel(product.status)}</span>
      </a> : <div className="product-card-media">
        {product.primaryImageUrl ? <img src={product.primaryImageUrl} alt={product.name} loading="lazy" /> : <div className="product-image-fallback"><PackageOpen size={34} /><span>Chưa có ảnh</span></div>}
        {(outOfStock || onBackorder) && <span className={`out-of-stock-ribbon${onBackorder ? ' backorder' : ''}`}>{outOfStock ? <PackageX size={14} /> : <PackageOpen size={14} />} {outOfStock ? 'Hết hàng' : 'Đặt trước'}</span>}
        <span className={`card-status ${product.status === 'Active' ? 'active' : ''}`}>{statusLabel(product.status)}</span>
      </div>}
      <div className="product-card-body">
        <div className="product-card-meta"><span>{product.categoryName ?? product.brand ?? 'Sản phẩm'}</span><span>{product.sku}</span></div>
        <button className="product-card-title" type="button" onClick={() => canEdit && onEdit(product.id)} disabled={!canEdit} title={product.name}>{product.name}</button>
        <ProductWarnings product={product} />
        <div className="card-price"><strong>{currency.format(product.salePrice)}</strong>{product.regularPrice && product.regularPrice > product.salePrice ? <del>{currency.format(product.regularPrice)}</del> : null}</div>
        <div className="card-stock-row"><AvailabilityBadge product={product} compact /><small>{dateFormat.format(new Date(product.createdAtUtc))}</small></div>
      </div>
      <footer className="product-card-footer"><StatusBadge tone={syncTone(product.syncState)}>{syncLabel(product.syncState)}</StatusBadge><span className="card-actions">{product.wooPermalink && <a className="icon-button" href={product.wooPermalink} target="_blank" rel="noreferrer" title="Mở trên website"><ExternalLink size={16} /></a>}{canEdit && <button className="icon-button" title="Chỉnh sửa" onClick={() => onEdit(product.id)}><Pencil size={16} /></button>}{canPush && <button className="icon-button" title="Đẩy lên WooCommerce" onClick={() => onPush(product.id)}><UploadCloud size={17} /></button>}</span></footer>
    </article>
  })}</div>
}

function ProductThumbnail({ product, canEdit, onEdit }: { product: Product; canEdit: boolean; onEdit: (id: string) => void }) {
  const content = product.primaryImageUrl ? <img src={product.primaryImageUrl} alt="" loading="lazy" /> : <PackageOpen size={19} />
  if (canEdit) {
    return <button type="button" className="product-thumb product-media-edit" onClick={() => onEdit(product.id)} aria-label={`Sửa ${product.name}`} title="Nhấp vào ảnh để sửa sản phẩm">{content}</button>
  }
  return product.wooPermalink
    ? <a className="product-thumb" href={product.wooPermalink} target="_blank" rel="noreferrer" title="Mở trên website">{content}</a>
    : <span className="product-thumb">{content}</span>
}

function ProductWarnings({ product }: { product: Product }) {
  if (product.primaryImageUrl && product.salePrice > 0) return null
  return <span className="product-warnings">
    {!product.primaryImageUrl && <span><ImageOff size={12} /> Thiếu ảnh</span>}
    {product.salePrice <= 0 && <span><AlertTriangle size={12} /> Thiếu giá</span>}
  </span>
}

function AvailabilityBadge({ product, compact = false }: { product: Product; compact?: boolean }) {
  if (product.availability === 'OutOfStock') return <span className="stock-badge out"><PackageX size={14} /> Hết hàng</span>
  if (product.availability === 'OnBackorder') return <span className="stock-badge backorder"><PackageOpen size={14} /> Đặt trước</span>
  const quantity = product.trackInventory ? `${compact ? 'Còn ' : ''}${product.stockQuantity.toLocaleString('vi-VN')}${compact ? '' : ' sản phẩm'}` : 'Còn hàng'
  return <span className="stock-badge in">{quantity}</span>
}

function EmptyProducts({ onResetFilters }: { onResetFilters: () => void }) {
  return <div className="product-empty"><span><PackageOpen size={34} /></span><h3>Không tìm thấy sản phẩm</h3><p>Thử thay đổi từ khóa hoặc xóa các bộ lọc đang áp dụng.</p><button className="secondary-button" onClick={onResetFilters}><X size={16} /> Xóa bộ lọc</button></div>
}

function BulkActionBar({ count, pending, onAction, onPrint, onClear }: { count: number; pending: boolean; onAction: (action: string) => void; onPrint: () => void; onClear: () => void }) {
  return <div className="bulk-action-bar">
    <div><CheckSquare size={18} /><strong>Đã chọn {count} sản phẩm</strong><small>Tối đa 100 sản phẩm mỗi lượt</small></div>
    <span className="bulk-buttons">
      <button className="primary-button bulk-print-button" onClick={onPrint} disabled={pending}><Printer size={16} /> In tem</button>
      <button className="secondary-button" onClick={() => onAction('push')} disabled={pending}><UploadCloud size={16} /> Đồng bộ Woo</button>
      <button className="secondary-button" onClick={() => onAction('publish')} disabled={pending}>Đăng bán</button>
      <button className="secondary-button" onClick={() => onAction('draft')} disabled={pending}>Chuyển bản nháp</button>
      <button className="bulk-trash" onClick={() => onAction('trash')} disabled={pending}><Trash2 size={16} /> Thùng rác</button>
      <button className="icon-button" onClick={onClear} disabled={pending} aria-label="Bỏ chọn"><X size={17} /></button>
    </span>
  </div>
}

function paginationItems(current: number, total: number): Array<number | '…'> {
  if (total <= 7) return Array.from({ length: total }, (_, index) => index + 1)
  const pages = new Set([1, total, current - 1, current, current + 1])
  const sorted = [...pages].filter((value) => value >= 1 && value <= total).sort((a, b) => a - b)
  const result: Array<number | '…'> = []
  sorted.forEach((value, index) => {
    const previous = sorted[index - 1]
    if (previous !== undefined && value - previous > 1) result.push('…')
    result.push(value)
  })
  return result
}

function statusLabel(status: Product['status']) {
  return ({ Active: 'Đang bán', Draft: 'Bản nháp', Inactive: 'Tạm ẩn', Discontinued: 'Ngừng bán' })[status]
}

function syncLabel(state: Product['syncState']) {
  return ({ NotLinked: 'Chưa liên kết', Synced: 'Đã đồng bộ', PendingPush: 'Chờ đẩy', PendingPull: 'Chờ lấy', Conflict: 'Xung đột', Failed: 'Lỗi' })[state]
}

function syncTone(state: Product['syncState']): 'success' | 'danger' | 'warning' | 'neutral' {
  if (state === 'Synced') return 'success'
  if (state === 'Failed' || state === 'Conflict') return 'danger'
  if (state === 'PendingPush' || state === 'PendingPull') return 'warning'
  return 'neutral'
}
