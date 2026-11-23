import { useMemo } from 'react'
import { Bar } from 'react-chartjs-2'
import { BarElement, CategoryScale, Chart as ChartJS, Legend, LinearScale, Tooltip } from 'chart.js'
import type { SummaryDay } from '../../api/types'

// Chart.js is modular: register only the pieces this chart uses (keeps the bundle smaller).
ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip, Legend)

/** Bootstrap's colours live in CSS variables (--bs-success ...). A canvas cannot use var(), so read the real values once. */
function themeColor(name: string, fallback: string): string {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

/** Grouped bars per day: tracked, received and delivered (reversals are in the cards and the table). */
export function SummaryChart({ days }: { days: SummaryDay[] }) {
  const data = useMemo(() => ({
    labels: days.map((d) => d.day.slice(5)), // "03-27"
    datasets: [
      { label: 'Tracked', data: days.map((d) => d.tracked), backgroundColor: themeColor('--bs-info', '#0dcaf0') },
      { label: 'Received', data: days.map((d) => d.received), backgroundColor: themeColor('--bs-success', '#198754') },
      { label: 'Delivered', data: days.map((d) => d.delivered), backgroundColor: themeColor('--bs-primary', '#0d6efd') },
    ],
  }), [days])

  return (
    <div style={{ height: 320 }} role="img" aria-label="Bar chart of tracked, received and delivered rows per day">
      <Bar data={data} options={{
        responsive: true, maintainAspectRatio: false,
        scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }, // whole numbers only: you cannot receive half a unit
        plugins: { legend: { position: 'bottom' } },
      }} />
    </div>
  )
}
