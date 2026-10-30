import './App.css'
import { BrowserRouter, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthProvider'
import { RequireRole } from './auth/RequireRole'
import { Layout } from './components/Layout'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { DashboardPage } from './pages/DashboardPage'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { PropertiesPage } from './pages/PropertiesPage'
import { PropertyDetailPage } from './pages/PropertyDetailPage'
import { PropertyFormPage } from './pages/PropertyFormPage'

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
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
