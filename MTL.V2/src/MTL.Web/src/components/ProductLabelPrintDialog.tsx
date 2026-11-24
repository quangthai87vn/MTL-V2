import { Printer, Tag, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { createPortal } from 'react-dom'
import type { Product } from '../types'

type LabelSizeKey = '80x40' | '70x40' | '50x30'

const labelSizes: Record<LabelSizeKey, { label: string; width: number; height: number }> = {
  '80x40': { label: '80 × 40 mm (khuyên dùng)', width: 80, height: 40 },
  '70x40': { label: '70 × 40 mm', width: 70, height: 40 },
  '50x30': { label: '50 × 30 mm', width: 50, height: 30 },
}

const priceFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })

function escapeHtml(value: string) {
  return value.replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;',
  }[character] ?? character))
}

export function ProductLabelPrintDialog({ products, onClose }: { products: Product[]; onClose: () => void }) {
  const [sizeKey, setSizeKey] = useState<LabelSizeKey>('80x40')
  const [copies, setCopies] = useState(1)
  const size = labelSizes[sizeKey]
  const labels = useMemo(
    () => products.flatMap((product) => Array.from({ length: copies }, (_, copyIndex) => ({ product, copyIndex }))),
    [copies, products],
  )
  const productsWithoutPrice = products.filter((product) => product.salePrice <= 0).length

  const printLabels = () => {
    const frame = document.createElement('iframe')
    frame.title = 'In tem sản phẩm'
    frame.style.position = 'fixed'
    frame.style.left = '-10000px'
    frame.style.top = '-10000px'
    frame.style.width = '1px'
    frame.style.height = '1px'
    frame.style.border = '0'

    const labelMarkup = labels.map(({ product }) => {
      const nameSize = product.name.length > 90 ? '3.25mm' : product.name.length > 60 ? '3.65mm' : '4mm'
      const price = product.salePrice > 0 ? `${priceFormat.format(product.salePrice)} đ` : 'Liên hệ'

      return `<div class="label-sheet"><article class="product-price-label size-${sizeKey}">
        <h3 style="font-size:${nameSize}">${escapeHtml(product.name)}</h3>
        <strong>${escapeHtml(price)}</strong>
        <em>www.mytholaptop.vn</em>
      </article></div>`
    }).join('')

    frame.srcdoc = `<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>In tem sản phẩm</title>
      <style>
        @page { size: ${size.width}mm ${size.height}mm; margin: 0; }
        * { box-sizing: border-box; }
        html, body { width: ${size.width}mm; margin: 0; padding: 0; background: #fff; }
        body { font-family: "Roboto", Arial, sans-serif; }
        .label-sheet { width: ${size.width}mm; height: ${size.height}mm; margin: 0; padding: 2mm; display: block; overflow: hidden; break-inside: avoid; break-after: page; page-break-inside: avoid; page-break-after: always; }
        .label-sheet:last-child { break-after: auto; page-break-after: auto; }
        .product-price-label { width: 100%; height: 100%; padding: 2.2mm 3mm 1.8mm; display: flex; flex-direction: column; align-items: center; justify-content: center; overflow: hidden; border: .3mm solid #111; color: #111; background: #fff; text-align: center; }
        h3 { width: 100%; max-height: 9.2mm; margin: 0; display: -webkit-box; overflow: hidden; font-weight: 700; line-height: 1.08; letter-spacing: -.08mm; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
        strong { margin-top: 1.1mm; font-size: 6.2mm; line-height: 1; letter-spacing: .05mm; }
        em { margin-top: 1.3mm; font-size: 3.1mm; line-height: 1; font-style: normal; }
        .size-50x30 { padding: 1.5mm 2mm 1.2mm; }
        .size-50x30 h3 { max-height: 7.2mm; font-size: 2.75mm !important; }
        .size-50x30 strong { margin-top: .8mm; font-size: 4.7mm; }
        .size-50x30 em { margin-top: .9mm; font-size: 2.5mm; }
      </style></head><body>${labelMarkup}</body></html>`

    frame.addEventListener('load', () => {
      const printWindow = frame.contentWindow
      const printDocument = frame.contentDocument
      if (!printWindow || !printDocument) {
        frame.remove()
        return
      }

      const cleanup = () => frame.remove()
      printWindow.addEventListener('afterprint', cleanup, { once: true })
      window.setTimeout(cleanup, 120_000)
      void printDocument.fonts.ready.then(() => {
        printWindow.focus()
        printWindow.print()
      })
    }, { once: true })

    document.body.appendChild(frame)
  }

  return createPortal(<div className="label-print-backdrop" role="presentation">
    <section className="label-print-dialog" role="dialog" aria-modal="true" aria-labelledby="label-print-title">
      <style>{`@media print { @page { size: ${size.width}mm ${size.height}mm; margin: 0; } }`}</style>

      <header className="label-print-head">
        <div>
          <span className="eyebrow accent">TEM SẢN PHẨM</span>
          <h2 id="label-print-title">Xem trước và in tem</h2>
          <p>{products.length} sản phẩm · {labels.length} tem sẽ được in</p>
        </div>
        <button type="button" className="icon-button" onClick={onClose} aria-label="Đóng cửa sổ in tem"><X size={21} /></button>
      </header>

      <div className="label-print-toolbar">
        <label>
          <span>Khổ tem</span>
          <select value={sizeKey} onChange={(event) => setSizeKey(event.target.value as LabelSizeKey)}>
            {Object.entries(labelSizes).map(([key, item]) => <option key={key} value={key}>{item.label}</option>)}
          </select>
        </label>
        <label>
          <span>Số bản mỗi sản phẩm</span>
          <input type="number" min={1} max={20} value={copies} onChange={(event) => setCopies(Math.min(20, Math.max(1, Number(event.target.value) || 1)))} />
        </label>
        <div className="label-print-tip">
          <Tag size={18} />
          <span><strong>Thiết lập máy in</strong><small>Chọn tỷ lệ 100% và lề “Không” để tem đúng kích thước.</small></span>
        </div>
      </div>

      <div className="label-print-status" aria-live="polite">
        {productsWithoutPrice > 0 && <div className="label-price-warning">Có {productsWithoutPrice} sản phẩm chưa có giá bán; tem sẽ hiển thị “Liên hệ”.</div>}
      </div>

      <div className="label-print-area" aria-label="Bản xem trước tem sản phẩm">
        {labels.map(({ product, copyIndex }) => <div
          className="label-sheet"
          style={{ width: `${size.width}mm`, height: `${size.height}mm` }}
          key={`${product.id}-${copyIndex}`}
        >
          <article className={`product-price-label size-${sizeKey}`}>
            <h3 style={{ fontSize: product.name.length > 90 ? '3.25mm' : product.name.length > 60 ? '3.65mm' : '4mm' }}>{product.name}</h3>
            <strong>{product.salePrice > 0 ? `${priceFormat.format(product.salePrice)} đ` : 'Liên hệ'}</strong>
            <em>www.mytholaptop.vn</em>
          </article>
        </div>)}
      </div>

      <footer className="label-print-actions">
        <span><strong>{labels.length}</strong> tem · khổ {size.width} × {size.height} mm</span>
        <div>
          <button type="button" className="secondary-button" onClick={onClose}>Đóng</button>
          <button type="button" className="primary-button" onClick={printLabels}><Printer size={18} /> In tem</button>
        </div>
      </footer>
    </section>
  </div>, document.body)
}
