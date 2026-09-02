import { createBrowserRouter, Navigate } from 'react-router-dom'
import DashboardPage from './pages/DashboardPage'
import CallbackPage from './pages/CallbackPage'
import SilentRenewPage from './pages/SilentRenewPage'
import SignedOutPage from './pages/SignedOutPage'
import HelpPage from './pages/HelpPage'
import ProtectedRoute from './auth/ProtectedRoute'
import RequireMonthCycle from './auth/RequireMonthCycle'
import AdminRoute from './auth/AdminRoute'
import AppLayout from './layouts/AppLayout'
import MonthCycleSettingsPage from './features/settings/MonthCycleSettingsPage'
import SettingsLayout from './features/settings/SettingsLayout'
import ProfilePage from './features/settings/ProfilePage'
import CategoriesPage from './features/categories/CategoriesPage'
import BudgetSetupPage from './features/budgets/BudgetSetupPage'
import ExpensesPage from './features/expenses/ExpensesPage'
import IncomesPage from './features/incomes/IncomesPage'
import LoansPage from './features/loans/LoansPage'
import LoanDetailPage from './features/loans/LoanDetailPage'
import InvestmentsPage from './features/investments/InvestmentsPage'
import InvestmentDetailPage from './features/investments/InvestmentDetailPage'
import AdminUsersPage from './features/admin/AdminUsersPage'
import AdminFeedbackPage from './features/admin/AdminFeedbackPage'
import FeedbackPage from './features/settings/FeedbackPage'

export const router = createBrowserRouter([
  // Sign-in and registration live on Auth019, not here.
  { path: '/callback', element: <CallbackPage /> },
  { path: '/silent-renew', element: <SilentRenewPage /> },
  { path: '/signed-out', element: <SignedOutPage /> },
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
              { path: '/help', element: <HelpPage /> },
              { path: '/categories', element: <CategoriesPage /> },
              { path: '/budgets', element: <BudgetSetupPage /> },
              { path: '/expenses', element: <ExpensesPage /> },
              { path: '/incomes', element: <IncomesPage /> },
              { path: '/loans', element: <LoansPage /> },
              { path: '/loans/:id', element: <LoanDetailPage /> },
              { path: '/investments', element: <InvestmentsPage /> },
              { path: '/investments/:id', element: <InvestmentDetailPage /> },
              {
                path: '/settings',
                element: <SettingsLayout />,
                children: [
                  { index: true, element: <Navigate to="profile"replace /> },
                  { path: 'profile', element: <ProfilePage /> },
                  // Kept at its original path so existing links still land here.
                  { path: 'month-cycle', element: <MonthCycleSettingsPage /> },
                  { path: 'feedback', element: <FeedbackPage /> },
                ],
              },
              {
                element: <AdminRoute />,
                children: [
                  { path: '/admin/users', element: <AdminUsersPage /> },
                  { path: '/admin/feedback', element: <AdminFeedbackPage /> },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
])
