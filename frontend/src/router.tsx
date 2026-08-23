import { createBrowserRouter } from 'react-router-dom'
import DashboardPage from './pages/DashboardPage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import ProtectedRoute from './auth/ProtectedRoute'
import RequireMonthCycle from './auth/RequireMonthCycle'
import AdminRoute from './auth/AdminRoute'
import AppLayout from './layouts/AppLayout'
import MonthCycleSettingsPage from './features/settings/MonthCycleSettingsPage'
import CategoriesPage from './features/categories/CategoriesPage'
import BudgetSetupPage from './features/budgets/BudgetSetupPage'
import ExpensesPage from './features/expenses/ExpensesPage'
import AdminUsersPage from './features/admin/AdminUsersPage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <RequireMonthCycle />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { path: '/', element: <DashboardPage /> },
              { path: '/categories', element: <CategoriesPage /> },
              { path: '/budgets', element: <BudgetSetupPage /> },
              { path: '/expenses', element: <ExpensesPage /> },
              { path: '/settings/month-cycle', element: <MonthCycleSettingsPage /> },
              {
                element: <AdminRoute />,
                children: [{ path: '/admin/users', element: <AdminUsersPage /> }],
              },
            ],
          },
        ],
      },
    ],
  },
])
