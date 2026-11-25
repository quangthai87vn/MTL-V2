import { ArrowUpRight, Boxes, CircleDollarSign, PackageCheck } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/PageHeader'
import { api } from '../lib/api'

type Summary = {
  productCount: number
  lowStockCount: number
  customerCount: number
  monthlyRevenue: number
  generatedAtUtc: string
}

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
const month = new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric' })
const time = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' })

export function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: () => api.get<Summary>('/api/dashboard/summary'),
  })

  const stats = [
    { label: 'Sản phẩm', value: (data?.productCount ?? 0).toLocaleString('vi-VN'), detail: 'Mặt hàng đang quản lý' },
    { label: 'Sắp hết hàng', value: (data?.lowStockCount ?? 0).toLocaleString('vi-VN'), detail: 'Sản phẩm theo dõi tồn kho ≤ 2', alert: (data?.lowStockCount ?? 0) > 0 },
    { label: 'Khách hàng', value: (data?.customerCount ?? 0).toLocaleString('vi-VN'), detail: 'Hồ sơ khách hàng đã lưu' },
  ]
  const updatedAt = data?.generatedAtUtc ? time.format(new Date(data.generatedAtUtc)) : null
  const monthLabel = month.format(new Date()).replace(/^./, (character) => character.toUpperCase())

  return (
    <div className="dashboard-page">
      <PageHeader
        eyebrow="Trung tâm điều hành"
        title="Tổng quan vận hành"
        description="Doanh thu, tồn kho, khách hàng và kết nối bán hàng của MỸ THO LAPTOP"
      />

      <section className="dashboard-overview" aria-label="Chỉ số vận hành">
        <article className="revenue-card">
          <div className="revenue-card-head">
            <div>
              <span>Doanh thu tháng</span>
              <small>{monthLabel}</small>
            </div>
            <span className="revenue-mark" aria-hidden="true">MTL</span>
          </div>
          <strong>{isLoading ? '—' : currency.format(data?.monthlyRevenue ?? 0)}</strong>
          <div className="revenue-card-foot">
            <span>Dữ liệu trực tiếp từ hệ thống</span>
            <span>{updatedAt ? `Cập nhật ${updatedAt}` : 'Đang cập nhật'}</span>
          </div>
        </article>

        <div className="dashboard-stat-list">
          {stats.map(({ label, value, detail, alert }, index) => (
            <article className={alert ? 'dashboard-stat is-alert' : 'dashboard-stat'} key={label}>
              <div>
                <span>{label}</span>
                <small>{detail}</small>
              </div>
              <strong>{isLoading ? '—' : value}</strong>
              <span className="dashboard-stat-index" aria-hidden="true">0{index + 1}</span>
            </article>
          ))}
        </div>
      </section>

      <div className="dashboard-grid">
        <section className="panel quick-panel">
          <div className="panel-heading">
            <div><span className="section-kicker">Thao tác nhanh</span><h2>Bắt đầu công việc</h2></div>
            <span className="panel-meta">3 lối tắt</span>
          </div>
          <div className="quick-actions">
            <Link to="/products"><PackageCheck size={18} strokeWidth={1.5} /><span><strong>Quản lý sản phẩm</strong><small>Giá bán và đồng bộ WooCommerce</small></span><ArrowUpRight size={18} strokeWidth={1.5} /></Link>
            <Link to="/sales"><CircleDollarSign size={18} strokeWidth={1.5} /><span><strong>Tạo hóa đơn</strong><small>Ghi nhận bán hàng hoặc sửa chữa</small></span><ArrowUpRight size={18} strokeWidth={1.5} /></Link>
            <Link to="/inventory"><Boxes size={18} strokeWidth={1.5} /><span><strong>Kiểm tra kho</strong><small>Serial và biến động tồn</small></span><ArrowUpRight size={18} strokeWidth={1.5} /></Link>
          </div>
        </section>

        <section className="panel sync-panel">
          <div className="panel-heading">
            <div><span className="section-kicker">Tích hợp bán hàng</span><h2>WooCommerce</h2></div>
            <span className="sync-status"><i /> Sẵn sàng</span>
          </div>
          <div className="sync-visual"><span className="sync-logo">MTL</span><span className="sync-line"><i /></span><span className="sync-logo woo">WOO</span></div>
          <p>Sản phẩm được lưu tại PostgreSQL và liên kết với website bằng Woo ID. Mọi lần đồng bộ đều có lịch sử.</p>
          <Link className="text-link" to="/settings">Mở cấu hình tích hợp <ArrowUpRight size={16} strokeWidth={1.5} /></Link>
        </section>
      </div>
    </div>
  )
}

