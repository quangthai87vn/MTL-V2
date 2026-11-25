import {
  Bold,
  CalendarDays,
  Code2,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Eye,
  ExternalLink,
  Globe2,
  Images,
  ImageOff,
  Italic,
  Link,
  List,
  ListOrdered,
  PackagePlus,
  PackageCheck,
  Save,
  Star,
  Tag,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useRef, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react'
import { api, apiFetch, ApiError } from '../lib/api'
import type { MediaItem, MediaPage, ProductDetails, ProductFilterOptions, ProductImage, ProductTag } from '../types'

type ProductEditorDialogProps = {
  productId?: string
  filterOptions?: ProductFilterOptions
  onClose: () => void
  onSaved: (product: ProductDetails, wasEditing: boolean) => void
  onDeleted: () => void
}

const emptyProduct: ProductDetails = {
  id: '',
  sku: '',
  name: '',
  salePrice: 0,
  costPrice: 0,
  stockQuantity: 0,
  trackInventory: false,
  availability: 'InStock',
  status: 'Draft',
  syncState: 'NotLinked',
  unit: 'Cái',
  concurrencyToken: '',
  wooCategoryIds: [],
  images: [],
  tags: [],
  catalogVisibility: 'Visible',
}

export function ProductEditorDialog({ productId, filterOptions, onClose, onSaved, onDeleted }: ProductEditorDialogProps) {
  const query = useQuery({
    queryKey: ['product-details', productId],
    queryFn: () => api.get<ProductDetails>(`/api/products/${productId}`),
    enabled: Boolean(productId),
    staleTime: 0,
    refetchOnMount: 'always',
  })

  if (productId && (query.isLoading || query.isFetching)) {
    return <div className="modal-backdrop"><div className="product-editor-modal editor-loading">Đang tải thông tin sản phẩm…</div></div>
  }

  if (query.isError) {
    return <div className="modal-backdrop"><div className="product-editor-modal editor-loading"><div className="form-alert">{query.error.message}</div><button className="secondary-button" onClick={onClose}>Đóng</button></div></div>
  }

  return <ProductEditorForm key={query.data?.concurrencyToken ?? 'new'} product={query.data ?? emptyProduct} filterOptions={filterOptions} onClose={onClose} onSaved={onSaved} onDeleted={onDeleted} />
}

function ProductEditorForm({ product, filterOptions, onClose, onSaved, onDeleted }: { product: ProductDetails; filterOptions?: ProductFilterOptions; onClose: () => void; onSaved: (product: ProductDetails, wasEditing: boolean) => void; onDeleted: () => void }) {
  const isEditing = Boolean(product.id)
  const initialCategoryIds = product.wooCategoryIds.length > 0
    ? product.wooCategoryIds
    : product.wooCategoryId ? [product.wooCategoryId] : []
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<Set<number>>(new Set(initialCategoryIds))
  const [images, setImages] = useState<ProductImage[]>(product.images.length > 0
    ? product.images
    : product.primaryImageUrl
      ? [{ url: product.primaryImageUrl, thumbnailUrl: product.primaryImageUrl, position: 0 }]
      : [])
  const [description, setDescription] = useState(product.description ?? '')
  const [shortDescription, setShortDescription] = useState(product.shortDescription ?? '')
  const [trackInventory, setTrackInventory] = useState(product.trackInventory)
  const [tags, setTags] = useState<ProductTag[]>(product.tags ?? [])
  const [tagInput, setTagInput] = useState('')
  const [showPopularTags, setShowPopularTags] = useState(false)
  const [dataTab, setDataTab] = useState<'general' | 'inventory'>('general')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [mediaPicker, setMediaPicker] = useState<'featured' | 'gallery' | null>(null)
  const [clientRequestId] = useState(() => product.id || crypto.randomUUID())

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setError('')
    const form = new FormData(event.currentTarget)
    const categoryIds = [...selectedCategoryIds]
    const primaryCategory = filterOptions?.categories.find((item) => item.wooCategoryId === categoryIds[0])
    const payload = {
      name: String(form.get('name') ?? '').trim(),
      brand: String(form.get('brand') ?? '').trim() || null,
      categoryName: primaryCategory?.name ?? null,
      wooCategoryId: primaryCategory?.wooCategoryId ?? null,
      wooCategoryIds: categoryIds,
      shortDescription,
      description,
      regularPrice: nullableNumber(form.get('regularPrice')),
      salePrice: Number(form.get('salePrice')) || 0,
      costPrice: Number(form.get('costPrice')) || 0,
      stockQuantity: Number(form.get('stockQuantity')) || 0,
      unit: String(form.get('unit') ?? '').trim() || 'Cái',
      trackInventory,
      availability: String(form.get('availability')),
      catalogVisibility: String(form.get('catalogVisibility')),
      status: String(form.get('status')),
      tags,
      primaryImageUrl: images[0]?.url ?? null,
      images: images.map((image, position) => ({ ...image, position })),
      sku: String(form.get('sku') ?? '').trim() || null,
      concurrencyToken: isEditing ? product.concurrencyToken : null,
      clientRequestId: isEditing ? null : clientRequestId,
    }

    try {
      let savedProduct: ProductDetails
      if (isEditing) {
        try {
          savedProduct = await api.put<ProductDetails>(`/api/products/${product.id}`, payload)
        } catch (reason) {
          const currentProduct = getCurrentProductFromConflict(reason)
          if (!currentProduct || !sameEditableProduct(product, currentProduct)) throw reason
          savedProduct = await api.put<ProductDetails>(`/api/products/${product.id}`, {
            ...payload,
            concurrencyToken: currentProduct.concurrencyToken,
          })
        }
      } else {
        savedProduct = await api.post<ProductDetails>('/api/products', payload)
      }
      onSaved(savedProduct, isEditing)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không thể lưu sản phẩm.')
    } finally {
      setSaving(false)
    }
  }

  async function remove() {
    if (!window.confirm(`Đưa “${product.name}” vào thùng rác trên WooCommerce và xóa khỏi MTL.V2?`)) return
    setDeleting(true)
    setError('')
    try {
      await api.delete(`/api/products/${product.id}`)
      onDeleted()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Không thể xóa sản phẩm.')
    } finally {
      setDeleting(false)
    }
  }

  function toggleCategory(categoryId: number) {
    setSelectedCategoryIds((current) => {
      const next = new Set(current)
      if (next.has(categoryId)) next.delete(categoryId)
      else next.add(categoryId)
      return next
    })
  }

  function addTags(values: string[]) {
    const normalized = values.map((value) => value.trim()).filter(Boolean)
    if (normalized.length === 0) return
    setTags((current) => {
      const byName = new Map(current.map((tag) => [tag.name.toLocaleLowerCase('vi'), tag]))
      normalized.forEach((name) => {
        const key = name.toLocaleLowerCase('vi')
        if (!byName.has(key)) byName.set(key, { name })
      })
      return [...byName.values()].slice(0, 50)
    })
    setTagInput('')
  }

  function removeTag(name: string) {
    setTags((current) => current.filter((tag) => tag.name.toLocaleLowerCase('vi') !== name.toLocaleLowerCase('vi')))
  }

  function chooseMedia(selected: MediaItem[]) {
    const mapped = selected.map<ProductImage>((item, index) => ({
      wooMediaId: item.id || undefined,
      url: item.url,
      thumbnailUrl: item.thumbnailUrl ?? item.url,
      name: item.title,
      altText: item.altText,
      position: index,
    }))
    setImages((current) => {
      if (mediaPicker === 'featured') {
        const featured = mapped[0]
        if (!featured) return current
        return [featured, ...current.filter((image) => imageKey(image) !== imageKey(featured))]
          .map((image, position) => ({ ...image, position }))
      }
      const byKey = new Map(current.map((image) => [imageKey(image), image]))
      mapped.forEach((image) => byKey.set(imageKey(image), image))
      return [...byKey.values()].slice(0, 30).map((image, position) => ({ ...image, position }))
    })
    setMediaPicker(null)
  }

  function removeImage(index: number) {
    setImages((current) => current.filter((_, position) => position !== index).map((image, position) => ({ ...image, position })))
  }

  function moveImage(index: number, direction: -1 | 1) {
    setImages((current) => {
      const target = index + direction
      if (target < 0 || target >= current.length) return current
      const next = [...current]
      const currentItem = next[index]!
      next[index] = next[target]!
      next[target] = currentItem
      return next.map((image, position) => ({ ...image, position }))
    })
  }

  return <div className="modal-backdrop product-editor-backdrop" onMouseDown={onClose}>
    <form className="product-editor-modal wordpress-editor" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
      <header className="product-editor-head wordpress-editor-head">
        <div><span className="eyebrow accent">{isEditing ? 'CHỈNH SỬA SẢN PHẨM' : 'THÊM SẢN PHẨM MỚI'}</span><h2>{isEditing ? 'Sửa sản phẩm' : 'Thêm sản phẩm mới'}</h2></div>
        <div className="editor-head-actions">
          {product.wooPermalink && <a className="secondary-button" href={product.wooPermalink} target="_blank" rel="noreferrer"><ExternalLink size={16} /> Xem sản phẩm</a>}
          <button type="button" className="icon-button" onClick={onClose} aria-label="Đóng"><X size={21} /></button>
        </div>
      </header>

      {error && <div className="form-alert editor-alert">{error}</div>}

      <div className="product-editor-grid wordpress-editor-grid">
        <main className="product-editor-main">
          <section className="editor-title-block">
            <input className="wordpress-title-input" name="name" defaultValue={product.name} required autoFocus placeholder="Tên sản phẩm" aria-label="Tên sản phẩm" />
            <div className="product-permalink"><span>Đường dẫn:</span>{product.wooPermalink ? <a href={product.wooPermalink} target="_blank" rel="noreferrer">{product.wooPermalink}</a> : <em>Sẽ được tạo sau khi lưu lên WooCommerce</em>}</div>
          </section>

          <RichHtmlEditor label="Mô tả sản phẩm" value={description} onChange={setDescription} minHeight={430} />

          <section className="editor-section product-data-panel">
            <header><h3>Dữ liệu sản phẩm</h3><span>Sản phẩm đơn giản</span></header>
            <div className="product-data-layout">
              <nav className="product-data-tabs" aria-label="Dữ liệu sản phẩm">
                <button type="button" className={dataTab === 'general' ? 'is-active' : ''} onClick={() => setDataTab('general')}>Cài đặt chung</button>
                <button type="button" className={dataTab === 'inventory' ? 'is-active' : ''} onClick={() => setDataTab('inventory')}>Kiểm kê kho hàng</button>
              </nav>
              <div className="product-data-content">
                <div className={`product-data-pane${dataTab === 'general' ? '' : ' is-hidden'}`} aria-hidden={dataTab !== 'general'}><div className="editor-form-grid three">
                  <label className="editor-field"><span>Giá thông thường (₫)</span><input name="regularPrice" type="number" min="0" defaultValue={product.regularPrice ?? ''} /></label>
                  <label className="editor-field"><span>Giá khuyến mãi (₫)</span><input name="salePrice" type="number" min="0" defaultValue={product.salePrice} required /></label>
                  <label className="editor-field"><span>Giá vốn nội bộ (₫)</span><input name="costPrice" type="number" min="0" defaultValue={product.costPrice} /></label>
                </div></div>
                <div className={`product-data-pane${dataTab === 'inventory' ? '' : ' is-hidden'}`} aria-hidden={dataTab !== 'inventory'}><div className="editor-form-grid">
                  <label className="editor-field"><span>Mã sản phẩm (SKU)</span><input name="sku" defaultValue={product.sku} placeholder="Tự động tạo nếu để trống" /></label>
                  <label className="editor-field"><span>Đơn vị tính</span><input name="unit" defaultValue={product.unit || 'Cái'} placeholder="Cái, Bộ, Chiếc…" /></label>
                  <label className="editor-field"><span>Trạng thái kho</span><select name="availability" defaultValue={product.availability}><option value="InStock">Còn hàng</option><option value="OutOfStock">Hết hàng</option><option value="OnBackorder">Cho phép đặt trước</option></select></label>
                  <label className="editor-check full"><input type="checkbox" checked={trackInventory} onChange={(event) => setTrackInventory(event.target.checked)} /><span><strong>Quản lý số lượng tồn</strong><small>Đồng bộ số lượng cụ thể lên WooCommerce.</small></span></label>
                  {trackInventory && <label className="editor-field"><span>Số lượng tồn</span><input name="stockQuantity" type="number" min="0" step="1" defaultValue={product.stockQuantity} /></label>}
                  {!trackInventory && <input name="stockQuantity" type="hidden" value="0" />}
                </div></div>
              </div>
            </div>
          </section>

          <RichHtmlEditor label="Mô tả ngắn của sản phẩm" value={shortDescription} onChange={setShortDescription} minHeight={190} />
        </main>

        <aside className="product-editor-side wordpress-editor-side">
          <EditorSideCard title="Danh mục sản phẩm" className="category-card">
            <CategoryTree categories={filterOptions?.categories ?? []} selectedIds={selectedCategoryIds} onToggle={toggleCategory} />
            <small className="editor-help">Có thể chọn nhiều danh mục. Danh mục đầu tiên được dùng làm danh mục chính trong danh sách.</small>
          </EditorSideCard>

          <EditorSideCard title="Xuất bản" className="publish-card">
            {product.wooPermalink && <a className="secondary-button editor-open-link publish-preview" href={product.wooPermalink} target="_blank" rel="noreferrer"><Eye size={16} /> Xem trước</a>}
            <div className="publish-settings">
              <label className="publish-setting-row">
                <PackageCheck size={16} />
                <span>Trạng thái</span>
                <select name="status" defaultValue={product.status}><option value="Active">Đã xuất bản</option><option value="Draft">Bản nháp</option><option value="Inactive">Tạm ẩn</option><option value="Discontinued">Ngừng kinh doanh</option></select>
              </label>
              <label className="publish-setting-row">
                <Globe2 size={16} />
                <span>Hiển thị</span>
                <select name="catalogVisibility" defaultValue={product.catalogVisibility ?? 'Visible'}><option value="Visible">Cửa hàng và tìm kiếm</option><option value="Catalog">Chỉ trong cửa hàng</option><option value="Search">Chỉ trong tìm kiếm</option><option value="Hidden">Ẩn khỏi cửa hàng</option></select>
              </label>
              <div className="publish-setting-row is-static">
                <CalendarDays size={16} />
                <span>{product.publishedAtUtc ? 'Đã xuất bản lúc' : 'Ngày xuất bản'}</span>
                <strong>{product.publishedAtUtc ? formatPublishedAt(product.publishedAtUtc) : 'Khi cập nhật'}</strong>
              </div>
            </div>
            <div className="woo-editor-state"><span className={product.wooProductId ? 'connected' : ''} /><div><strong>{product.wooProductId ? `Woo #${product.wooProductId}` : 'Chưa có trên Woo'}</strong><small>{product.syncState === 'Synced' ? 'Dữ liệu đã đồng bộ' : 'Sẽ đồng bộ khi lưu'}</small></div></div>
            <div className="publish-actions">
              {isEditing && <button type="button" className="publish-trash" onClick={() => void remove()} disabled={saving || deleting}><Trash2 size={15} /> {deleting ? 'Đang xóa…' : 'Bỏ vào thùng rác'}</button>}
              <button className="primary-button editor-save" disabled={saving || deleting}><Save size={17} /> {saving ? 'Đang lưu…' : isEditing ? 'Cập nhật' : 'Đăng sản phẩm'}</button>
            </div>
          </EditorSideCard>

          <EditorSideCard title="Thẻ sản phẩm" className="product-tags-card">
            <div className="tag-entry-row">
              <input
                value={tagInput}
                onChange={(event) => setTagInput(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key !== 'Enter') return
                  event.preventDefault()
                  addTags(tagInput.split(','))
                }}
                placeholder="Ví dụ: Laptop văn phòng"
                aria-label="Nhập thẻ sản phẩm"
              />
              <button type="button" className="secondary-button" onClick={() => addTags(tagInput.split(','))}>Thêm</button>
            </div>
            <small className="editor-help">Phân tách nhiều thẻ bằng dấu phẩy.</small>
            {tags.length > 0 && <div className="product-tag-list">{tags.map((tag) => <span key={tag.name}>{tag.name}<button type="button" onClick={() => removeTag(tag.name)} aria-label={`Xóa thẻ ${tag.name}`}><X size={13} /></button></span>)}</div>}
            {(filterOptions?.tags?.length ?? 0) > 0 && <>
              <button type="button" className="popular-tags-toggle" onClick={() => setShowPopularTags((current) => !current)}><Tag size={14} /> {showPopularTags ? 'Ẩn các thẻ phổ biến' : 'Chọn từ các thẻ được dùng nhiều nhất'}</button>
              {showPopularTags && <div className="popular-tag-list">{filterOptions?.tags?.map((tag) => <button type="button" key={tag.name} onClick={() => addTags([tag.name])}>{tag.name}<small>{tag.count}</small></button>)}</div>}
            </>}
          </EditorSideCard>

          <EditorSideCard title="Ảnh sản phẩm">
            <button type="button" className="editor-image-preview media-trigger" onClick={() => setMediaPicker('featured')}>
              {images[0] ? <img src={images[0].thumbnailUrl ?? images[0].url} alt={images[0].altText ?? product.name} /> : <span><ImageOff size={30} /> Chọn ảnh sản phẩm</span>}
            </button>
            <button type="button" className="secondary-button media-select-button" onClick={() => setMediaPicker('featured')}><Images size={16} /> {images[0] ? 'Thay ảnh sản phẩm' : 'Chọn ảnh sản phẩm'}</button>
            {images[0] && <button type="button" className="text-danger-button" onClick={() => removeImage(0)}>Xóa ảnh sản phẩm</button>}
            <small className="editor-help">Ảnh đầu tiên là ảnh đại diện và được đồng bộ lên WooCommerce.</small>
          </EditorSideCard>

          <EditorSideCard title="Album hình ảnh sản phẩm" className="product-gallery-card">
            {images.length > 1 ? <div className="product-gallery-editor">
              {images.slice(1).map((image, offset) => {
                const index = offset + 1
                return <div className="gallery-edit-item" key={imageKey(image)}>
                  <img src={image.thumbnailUrl ?? image.url} alt={image.altText ?? image.name ?? `Ảnh ${index + 1}`} />
                  <div className="gallery-item-actions">
                    <button type="button" onClick={() => moveImage(index, -1)} disabled={index <= 1} aria-label="Đưa ảnh sang trái"><ChevronLeft size={14} /></button>
                    <button type="button" onClick={() => moveImage(index, 1)} disabled={index >= images.length - 1} aria-label="Đưa ảnh sang phải"><ChevronRight size={14} /></button>
                    <button type="button" onClick={() => removeImage(index)} aria-label="Xóa ảnh khỏi album"><X size={14} /></button>
                  </div>
                </div>
              })}
            </div> : <div className="gallery-empty"><Images size={26} /><span>Chưa có ảnh album</span></div>}
            <button type="button" className="secondary-button media-select-button" onClick={() => setMediaPicker('gallery')}><Images size={16} /> Thêm ảnh vào album</button>
            <small className="editor-help">Có thể chọn và upload nhiều ảnh cùng lúc, tối đa 30 ảnh cho một sản phẩm.</small>
          </EditorSideCard>

          <EditorSideCard title="Thương hiệu sản phẩm">
            <label className="editor-field"><span>Thương hiệu</span><input name="brand" defaultValue={product.brand ?? ''} list="product-brands" placeholder="Ví dụ: Dell" /></label>
            <datalist id="product-brands">{filterOptions?.brands.map((item) => <option key={item.name} value={item.name} />)}</datalist>
          </EditorSideCard>

          {!isEditing && <div className="editor-new-note"><PackagePlus size={19} /><span>Sản phẩm sẽ được tạo đồng thời trong MTL.V2 và WooCommerce.</span></div>}
        </aside>
      </div>
    </form>
    {mediaPicker && <MediaLibraryDialog
      mode={mediaPicker === 'featured' ? 'single' : 'multiple'}
      selectedImages={mediaPicker === 'featured' ? images.slice(0, 1) : images}
      onClose={() => setMediaPicker(null)}
      onConfirm={chooseMedia}
    />}
  </div>
}

function getCurrentProductFromConflict(reason: unknown): ProductDetails | null {
  if (!(reason instanceof ApiError) || reason.status !== 409 || typeof reason.details !== 'object' || reason.details === null) {
    return null
  }

  const details = reason.details as { code?: unknown; currentProduct?: unknown }
  return details.code === 'product_concurrency' && details.currentProduct
    ? details.currentProduct as ProductDetails
    : null
}

function sameEditableProduct(left: ProductDetails, right: ProductDetails) {
  return JSON.stringify(editableProductSnapshot(left)) === JSON.stringify(editableProductSnapshot(right))
}

function editableProductSnapshot(product: ProductDetails) {
  return {
    sku: product.sku,
    name: product.name,
    shortDescription: product.shortDescription ?? null,
    description: product.description ?? null,
    brand: product.brand ?? null,
    categoryName: product.categoryName ?? null,
    wooCategoryIds: [...product.wooCategoryIds].sort((a, b) => a - b),
    salePrice: product.salePrice,
    regularPrice: product.regularPrice ?? null,
    costPrice: product.costPrice,
    stockQuantity: product.stockQuantity,
    unit: product.unit,
    trackInventory: product.trackInventory,
    availability: product.availability,
    catalogVisibility: product.catalogVisibility ?? 'Visible',
    status: product.status,
    primaryImageUrl: product.primaryImageUrl ?? null,
    images: [...product.images]
      .sort((a, b) => a.position - b.position)
      .map((image) => ({
        wooMediaId: image.wooMediaId ?? null,
        url: image.url,
        thumbnailUrl: image.thumbnailUrl ?? null,
        name: image.name ?? null,
        altText: image.altText ?? null,
        position: image.position,
      })),
    tags: [...(product.tags ?? [])]
      .map((tag) => ({ wooTagId: tag.wooTagId ?? null, name: tag.name.trim().toLocaleLowerCase('vi') }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi')),
  }
}

function EditorSideCard({ title, className = '', children }: { title: string; className?: string; children: ReactNode }) {
  return <details className={`editor-side-card wp-side-card ${className}`.trim()} open>
    <summary><strong>{title}</strong><ChevronDown size={18} /></summary>
    <div className="wp-side-card-body">{children}</div>
  </details>
}

function formatPublishedAt(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Không xác định'
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}

function CategoryTree({ categories, selectedIds, onToggle }: { categories: ProductFilterOptions['categories']; selectedIds: Set<number>; onToggle: (id: number) => void }) {
  const [search, setSearch] = useState('')
  const visibleCategories = useMemo(() => categories.filter((category) => category.path.toLocaleLowerCase('vi').includes(search.toLocaleLowerCase('vi'))), [categories, search])

  return <div className="category-tree-wrap">
    <input className="category-search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tìm danh mục…" aria-label="Tìm danh mục" />
    <div className="category-tree" role="group" aria-label="Cây danh mục sản phẩm">
      {visibleCategories.map((category) => category.wooCategoryId ? <label key={category.wooCategoryId} className="category-tree-item" style={{ paddingLeft: `${10 + category.depth * 20}px` }} title={category.path}>
        <input type="checkbox" checked={selectedIds.has(category.wooCategoryId)} onChange={() => onToggle(category.wooCategoryId!)} />
        <span>{category.name}</span><small>{category.count}</small>
      </label> : null)}
      {visibleCategories.length === 0 && <p>Không tìm thấy danh mục.</p>}
    </div>
  </div>
}

function MediaLibraryDialog({ mode, selectedImages, onClose, onConfirm }: {
  mode: 'single' | 'multiple'
  selectedImages: ProductImage[]
  onClose: () => void
  onConfirm: (items: MediaItem[]) => void
}) {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState('')
  const [selected, setSelected] = useState<Map<string, MediaItem>>(() => new Map(selectedImages.map((image) => {
    const item: MediaItem = {
      id: image.wooMediaId ?? 0,
      url: image.url,
      thumbnailUrl: image.thumbnailUrl,
      title: image.name ?? 'Ảnh sản phẩm',
      altText: image.altText,
    }
    return [mediaKey(item), item]
  })))
  const query = useQuery({
    queryKey: ['wordpress-media', page, appliedSearch],
    queryFn: () => api.get<MediaPage>(`/api/media?page=${page}&pageSize=30${appliedSearch ? `&search=${encodeURIComponent(appliedSearch)}` : ''}`),
  })
  const pageCount = Math.max(1, Math.ceil((query.data?.total ?? 0) / (query.data?.pageSize ?? 30)))

  function toggle(item: MediaItem) {
    setSelected((current) => {
      const next = new Map(current)
      const key = mediaKey(item)
      if (mode === 'single') {
        next.clear()
        next.set(key, item)
      } else if (next.has(key)) next.delete(key)
      else next.set(key, item)
      return next
    })
  }

  async function uploadFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])]
    if (files.length === 0) return
    setUploading(true)
    setUploadError('')
    const body = new FormData()
    files.forEach((file) => body.append('files', file))
    try {
      const uploaded = await apiFetch<MediaItem[]>('/api/media/upload', { method: 'POST', body })
      setSelected((current) => {
        const next = mode === 'single' ? new Map<string, MediaItem>() : new Map(current)
        uploaded.forEach((item) => next.set(mediaKey(item), item))
        return next
      })
      await queryClient.invalidateQueries({ queryKey: ['wordpress-media'] })
    } catch (reason) {
      setUploadError(reason instanceof Error ? reason.message : 'Không thể tải ảnh lên WordPress.')
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

  return <div className="media-modal-backdrop" onMouseDown={onClose}>
    <section className="media-modal" onMouseDown={(event) => event.stopPropagation()} aria-modal="true" role="dialog" aria-label="Thư viện Media WordPress">
      <header className="media-modal-head">
        <div><span className="eyebrow accent">WORDPRESS</span><h2>Thêm Media</h2></div>
        <button type="button" className="icon-button" onClick={onClose} aria-label="Đóng thư viện Media"><X size={21} /></button>
      </header>
      <div className="media-toolbar">
        <form onSubmit={(event) => { event.preventDefault(); setPage(1); setAppliedSearch(search.trim()) }}>
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tìm trong thư viện Media…" aria-label="Tìm ảnh Media" />
          <button className="secondary-button" type="submit">Tìm ảnh</button>
        </form>
        <label className={`primary-button media-upload${!query.data?.uploadEnabled ? ' is-disabled' : ''}`}>
          <Upload size={16} /> {uploading ? 'Đang tải lên…' : 'Tải ảnh lên'}
          <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" multiple={mode === 'multiple'} disabled={uploading || !query.data?.uploadEnabled} onChange={(event) => void uploadFiles(event)} />
        </label>
      </div>
      <div className="media-status" aria-live="polite">
        {query.data?.uploadEnabled === false && <div className="media-bridge-note">Đang hiển thị ảnh cache từ PostgreSQL. Cài plugin <strong>MTL Media Bridge</strong> trên WordPress để bật upload và xem toàn bộ Media Library.</div>}
        {uploadError && <div className="form-alert">{uploadError}</div>}
      </div>
      <div className="media-library-content">
        {query.isLoading && <div className="media-loading">Đang tải thư viện ảnh…</div>}
        {query.isError && <div className="form-alert">{query.error.message}</div>}
        {!query.isLoading && !query.isError && <div className="media-grid">
          {query.data?.items.map((item) => {
            const active = selected.has(mediaKey(item))
            return <button type="button" className={`media-item${active ? ' is-selected' : ''}`} key={mediaKey(item)} onClick={() => toggle(item)} title={item.title}>
              <img src={item.thumbnailUrl ?? item.url} alt={item.altText ?? item.title} />
              {active && <span className="media-check"><Check size={16} /></span>}
              <small>{item.title}</small>
            </button>
          })}
          {query.data?.items.length === 0 && <div className="media-empty"><ImageOff size={28} /><span>Không tìm thấy ảnh.</span></div>}
        </div>}
      </div>
      <footer className="media-modal-footer">
        <div className="media-pagination">
          <button type="button" className="icon-button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1}><ChevronLeft size={17} /></button>
          <span>Trang {page}/{pageCount} · {(query.data?.total ?? 0).toLocaleString('vi-VN')} ảnh</span>
          <button type="button" className="icon-button" onClick={() => setPage((value) => Math.min(pageCount, value + 1))} disabled={page >= pageCount}><ChevronRight size={17} /></button>
        </div>
        <div className="media-confirm-actions">
          <span>Đã chọn <strong>{selected.size}</strong> ảnh</span>
          <button type="button" className="secondary-button" onClick={onClose}>Đóng</button>
          <button type="button" className="primary-button" disabled={selected.size === 0} onClick={() => onConfirm([...selected.values()])}>
            <Star size={16} /> {mode === 'single' ? 'Chọn ảnh và đóng' : 'Thêm vào album và đóng'}
          </button>
        </div>
      </footer>
    </section>
  </div>
}

function RichHtmlEditor({ label, value, onChange, minHeight }: { label: string; value: string; onChange: (value: string) => void; minHeight: number }) {
  const [mode, setMode] = useState<'visual' | 'code'>('visual')
  const visualRef = useRef<HTMLDivElement>(null)

  function applyCommand(command: string, commandValue?: string) {
    visualRef.current?.focus()
    document.execCommand(command, false, commandValue)
    if (visualRef.current) onChange(visualRef.current.innerHTML)
  }

  function addLink() {
    const url = window.prompt('Nhập đường dẫn liên kết:')
    if (url) applyCommand('createLink', url)
  }

  return <section className="editor-section rich-editor-section">
    <header className="rich-editor-head">
      <h3>{label}</h3>
      <div className="rich-editor-modes" role="tablist" aria-label={`Chế độ ${label}`}>
        <button type="button" className={mode === 'visual' ? 'is-active' : ''} onClick={() => setMode('visual')} aria-selected={mode === 'visual'}>Trực quan</button>
        <button type="button" className={mode === 'code' ? 'is-active' : ''} onClick={() => setMode('code')} aria-selected={mode === 'code'}><Code2 size={14} /> Mã HTML</button>
      </div>
    </header>
    {mode === 'visual' ? <>
      <div className="rich-editor-toolbar" aria-label="Công cụ định dạng">
        <select aria-label="Kiểu văn bản" defaultValue="p" onChange={(event) => applyCommand('formatBlock', event.target.value)}><option value="p">Đoạn văn</option><option value="h2">Tiêu đề 2</option><option value="h3">Tiêu đề 3</option><option value="blockquote">Trích dẫn</option></select>
        <button type="button" onClick={() => applyCommand('bold')} title="In đậm"><Bold size={17} /></button>
        <button type="button" onClick={() => applyCommand('italic')} title="In nghiêng"><Italic size={17} /></button>
        <button type="button" onClick={() => applyCommand('insertUnorderedList')} title="Danh sách"><List size={17} /></button>
        <button type="button" onClick={() => applyCommand('insertOrderedList')} title="Danh sách đánh số"><ListOrdered size={17} /></button>
        <button type="button" onClick={addLink} title="Chèn liên kết"><Link size={17} /></button>
      </div>
      <div ref={visualRef} className="rich-editor-visual" style={{ minHeight }} contentEditable suppressContentEditableWarning dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(value) }} onInput={(event) => onChange(event.currentTarget.innerHTML)} />
    </> : <>
      <textarea className="rich-editor-code" style={{ minHeight }} value={value} onChange={(event) => onChange(event.target.value)} spellCheck={false} aria-label={`Mã HTML ${label}`} />
      <small className="html-code-note"><Code2 size={13} /> Chỉnh trực tiếp HTML; chuyển sang “Trực quan” để xem kết quả đã render.</small>
    </>}
  </section>
}

function sanitizeRichHtml(html: string) {
  const documentNode = new DOMParser().parseFromString(html, 'text/html')
  documentNode.querySelectorAll('script, iframe, object, embed, form, input, button, link, meta, base').forEach((element) => element.remove())
  documentNode.body.querySelectorAll('*').forEach((element) => {
    for (const attribute of [...element.attributes]) {
      const name = attribute.name.toLowerCase()
      const value = attribute.value.trim()
      if (name.startsWith('on') || name === 'srcdoc') element.removeAttribute(attribute.name)
      if ((name === 'href' || name === 'src') && /^(?:javascript|vbscript):/i.test(value)) element.removeAttribute(attribute.name)
      if (name === 'style' && /expression\s*\(|javascript:|url\s*\(/i.test(value)) element.removeAttribute(attribute.name)
    }
  })
  return documentNode.body.innerHTML
}

function imageKey(image: ProductImage) {
  return image.wooMediaId ? `id:${image.wooMediaId}` : `url:${image.url}`
}

function mediaKey(item: MediaItem) {
  return item.id ? `id:${item.id}` : `url:${item.url}`
}

function nullableNumber(value: FormDataEntryValue | null) {
  if (value === null || String(value).trim() === '') return null
  return Number(value)
}
