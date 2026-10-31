import './App.css'
import { BrowserRouter, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthProvider'
import { RequireRole } from './auth/RequireRole'
import { Layout } from './components/Layout'
import { AccountPage } from './pages/AccountPage'
import { AdminPage } from './pages/AdminPage'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { DashboardPage } from './pages/DashboardPage'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { NotificationsPage } from './pages/NotificationsPage'
import { PropertiesPage } from './pages/PropertiesPage'
import { PropertyDetailPage } from './pages/PropertyDetailPage'
import { PropertyFormPage } from './pages/PropertyFormPage'
import { MyRequestsPage, ReceivedRequestsPage } from './pages/RequestsPages'

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<HomePage />} />
            <Route path="biens" element={<PropertiesPage />} />
            <Route path="biens/:id" element={<PropertyDetailPage />} />
            <Route path="connexion" element={<LoginPage />} />
            <Route path="inscription" element={<RegisterPage />} />
            <Route
              path="espace-agent"
              element={
                <RequireRole role="Agent">
                  <DashboardPage />
                </RequireRole>
              }
            />
            <Route
              path="espace-agent/biens/nouveau"
              element={
                <RequireRole role="Agent">
                  <PropertyFormPage />
                </RequireRole>
              }
            />
            <Route
              path="espace-agent/biens/:id/modifier"
              element={
                <RequireRole role="Agent">
                  <PropertyFormPage />
                </RequireRole>
              }
            />
            <Route
              path="espace-agent/demandes"
              element={
                <RequireRole role="Agent">
                  <ReceivedRequestsPage />
                </RequireRole>
              }
            />
            <Route
              path="mes-demandes"
              element={
                <RequireRole>
                  <MyRequestsPage />
                </RequireRole>
              }
            />
            <Route
              path="notifications"
              element={
                <RequireRole>
                  <NotificationsPage />
                </RequireRole>
              }
            />
            <Route
              path="compte"
              element={
                <RequireRole>
                  <AccountPage />
                </RequireRole>
              }
            />
            <Route
              path="admin"
              element={
                <RequireRole role="Admin">
                  <AdminPage />
                </RequireRole>
              }
            />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
