import { BarChart3, Boxes, ReceiptText, ShoppingCart } from 'lucide-react'
import { PageHeader } from '../components/PageHeader'

const modules = {
  sales: { eyebrow: 'GIAO DỊCH', title: 'Hóa đơn bán hàng', description: 'Bán hàng, thanh toán, trả hàng và lịch sử serial.', icon: ShoppingCart, items: ['Đánh số hóa đơn an toàn', 'Snapshot sản phẩm và giá bán', 'Thanh toán nhiều nguồn', 'Trả hàng và chứng từ đảo'] },
  purchases: { eyebrow: 'MUA HÀNG', title: 'Hóa đơn nhập hàng', description: 'Nhà cung cấp, giá vốn và nhận hàng vào kho.', icon: ReceiptText, items: ['Nhà cung cấp & công nợ', 'Nhận serial theo từng máy', 'Cập nhật giá vốn', 'Thanh toán hóa đơn nhập'] },
  inventory: { eyebrow: 'KHO VẬN', title: 'Kho & serial', description: 'Stock ledger minh bạch cho laptop và linh kiện.', icon: Boxes, items: ['Nhập, xuất, điều chỉnh', 'Theo dõi serial/IMEI', 'Kiểm kê kho', 'Cảnh báo tồn thấp'] },
  reports: { eyebrow: 'PHÂN TÍCH', title: 'Báo cáo', description: 'Doanh thu, lợi nhuận, tồn kho và hiệu suất.', icon: BarChart3, items: ['Doanh thu theo thời gian', 'Lợi nhuận theo sản phẩm', 'Hàng chậm bán', 'Hiệu suất nhân viên'] },
} as const

export function ModulePlaceholderPage({ module: key }: { module: keyof typeof modules }) {
  const module = modules[key]
  const Icon = module.icon
  return <><PageHeader eyebrow={module.eyebrow} title={module.title} description={module.description} /><section className="panel roadmap-panel"><span className="roadmap-icon"><Icon size={28} /></span><div><span className="eyebrow accent">NỀN TẢNG ĐÃ SẴN SÀNG</span><h2>Module được triển khai ở giai đoạn giao dịch</h2><p>Mô hình dữ liệu và permission đã có trong backend. Phần quy trình nghiệp vụ sẽ được hoàn thiện sau khi chốt mẫu hóa đơn và cách tính tồn thực tế.</p><div className="feature-chips">{module.items.map((item) => <span key={item}>{item}</span>)}</div></div></section></>
}

