import type { ReactNode } from 'react'
import { Route, Routes } from 'react-router'
import { RequireAuth } from './auth/RequireAuth'
import { RequireCapability } from './auth/RequireCapability'
import { Layout } from './components/Layout'
import { CAP } from './lib/capabilities'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { MASTER_KINDS } from './pages/manage/constants'
import { MasterDataPage } from './pages/manage/MasterDataPage'
import { UsersPage } from './pages/manage/UsersPage'
import { DeliverPage } from './pages/ops/DeliverPage'
import { ReceivePage } from './pages/ops/ReceivePage'
import { TrackPage } from './pages/ops/TrackPage'
import { ForbiddenPage } from './pages/ForbiddenPage'
import { HistoryPage } from './pages/HistoryPage'
import { ReversePage } from './pages/history/ReversePage'
import { BarcodeReportPage } from './pages/reports/BarcodeReportPage'
import { StockReportPage } from './pages/reports/StockReportPage'
import { SummaryReportPage } from './pages/reports/SummaryReportPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { StubPage } from './pages/StubPage'

/** A route that needs a capability. The guard shows /forbidden when the server did not grant it. */
function guarded(capability: string, element: ReactNode) {
  return <RequireCapability capability={capability}>{element}</RequireCapability>
}

// Every route of the app. Stubs say which day builds them; they already sit behind the real guard.
export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Everything below needs a signed-in user and gets the navbar layout. */}
      <Route element={<RequireAuth><Layout /></RequireAuth>}>
        <Route path="/" element={<HomePage />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />
        <Route path="/change-password" element={guarded(CAP.changePassword, <ChangePasswordPage />)} />

        <Route path="/ops/track" element={guarded(CAP.opsTrack, <TrackPage />)} />
        <Route path="/ops/receive" element={guarded(CAP.opsReceive, <ReceivePage />)} />
        <Route path="/ops/deliver" element={guarded(CAP.opsDeliver, <DeliverPage />)} />

        <Route path="/history" element={guarded(CAP.historyView, <HistoryPage />)} />
        <Route path="/history/:id/reverse" element={guarded(CAP.historyReverse, <ReversePage />)} />

        <Route path="/reports/stock" element={guarded(CAP.reportsView, <StockReportPage />)} />
        <Route path="/reports/summary" element={guarded(CAP.reportsView, <SummaryReportPage />)} />
        <Route path="/reports/barcode" element={guarded(CAP.reportsView, <BarcodeReportPage />)} />

        <Route path="/manage/products" element={guarded(CAP.masterData, <MasterDataPage config={MASTER_KINDS.products} />)} />
        <Route path="/manage/components" element={guarded(CAP.masterData, <MasterDataPage config={MASTER_KINDS.components} />)} />
        <Route path="/manage/points" element={guarded(CAP.masterData, <MasterDataPage config={MASTER_KINDS.points} />)} />
        <Route path="/manage/users" element={guarded(CAP.users, <UsersPage />)} />
        <Route path="/audit" element={guarded(CAP.audit, <StubPage title="Audit log" day={5} />)} />

        <Route path="*" element={<ForbiddenPage />} />
      </Route>
    </Routes>
  )
}
