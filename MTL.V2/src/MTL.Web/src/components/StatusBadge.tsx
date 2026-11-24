export function StatusBadge({ tone, children }: { tone: 'success' | 'warning' | 'danger' | 'neutral'; children: string }) {
  return <span className={`status-badge ${tone}`}>{children}</span>
}

