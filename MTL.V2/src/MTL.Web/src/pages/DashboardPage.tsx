import { AlertTriangle, ArrowUpRight, Boxes, CircleDollarSign, PackageCheck, Users } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/PageHeader'
import { api } from '../lib/api'

type Summary = {
  productCount: number
  lowStockCount: number
  customerCount: number
  monthlyRevenue: number
}

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: () => api.get<Summary>('/api/dashboard/summary'),
  })

  const cards = [
    { label: 'Doanh thu tháng', value: currency.format(data?.monthlyRevenue ?? 0), icon: CircleDollarSign, tone: 'red' },
    { label: 'Sản phẩm', value: (data?.productCount ?? 0).toLocaleString('vi-VN'), icon: PackageCheck, tone: 'blue' },
    { label: 'Sắp hết hàng', value: (data?.lowStockCount ?? 0).toLocaleString('vi-VN'), icon: AlertTriangle, tone: 'amber' },
    { label: 'Khách hàng', value: (data?.customerCount ?? 0).toLocaleString('vi-VN'), icon: Users, tone: 'green' },
  ]

  return (
    <>
      <PageHeader eyebrow="TRUNG TÂM ĐIỀU HÀNH" title="Tổng quan kinh doanh" description="Các chỉ số quan trọng của MỸ THO LAPTOP trong một màn hình." />
      <div className="metric-grid">
        {cards.map(({ label, value, icon: Icon, tone }) => (
          <article className="metric-card" key={label}>
            <span className={`metric-icon ${tone}`}><Icon size={21} /></span>
            <span>{label}</span>
            <strong>{isLoading ? '—' : value}</strong>
            <small>Dữ liệu trực tiếp từ hệ thống</small>
          </article>
        ))}
      </div>

      <div className="dashboard-grid">
        <section className="panel quick-panel">
          <div className="panel-heading"><div><span className="eyebrow">Thao tác nhanh</span><h2>Bắt đầu công việc</h2></div></div>
          <div className="quick-actions">
            <Link to="/products"><PackageCheck /><span><strong>Quản lý sản phẩm</strong><small>Kiểm tra giá và đồng bộ Woo</small></span><ArrowUpRight /></Link>
            <Link to="/sales"><CircleDollarSign /><span><strong>Tạo hóa đơn bán</strong><small>Ghi nhận giao dịch mới</small></span><ArrowUpRight /></Link>
            <Link to="/inventory"><Boxes /><span><strong>Kiểm tra tồn kho</strong><small>Theo dõi serial và biến động</small></span><ArrowUpRight /></Link>
          </div>
        </section>

        <section className="panel sync-panel">
          <div className="panel-heading"><div><span className="eyebrow">Tích hợp</span><h2>WooCommerce</h2></div><span className="status-badge success">Sẵn sàng</span></div>
          <div className="sync-visual"><span className="sync-logo">MTL</span><span className="sync-line"><i /></span><span className="sync-logo woo">woo</span></div>
          <p>Sản phẩm được lưu tại PostgreSQL và liên kết với website bằng Woo ID. Mọi lần đồng bộ đều có lịch sử.</p>
          <Link className="text-link" to="/settings">Mở cấu hình tích hợp <ArrowUpRight size={16} /></Link>
        </section>
      </div>
    </>
  )
}

