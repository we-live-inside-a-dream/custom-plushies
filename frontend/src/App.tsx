import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Loading } from './components/ui'
import { AccountLayout, Addresses, OrderPage, Orders, Profile, WishlistPage } from './pages/Account'
import { Builder } from './pages/Builder'
import { Cart } from './pages/Cart'
import { Checkout } from './pages/Checkout'
import { Home } from './pages/Home'
import { About, AuthPage, Journal, NotFound, PostPage } from './pages/Misc'
import { ProductPage } from './pages/ProductPage'
import { Shop } from './pages/Shop'

// The admin site is its own bundle: shoppers never download it.
const Admin = lazy(() => import('./admin/Admin'))

export function App() {
  return (
    <Routes>
      <Route
        path="/admin/*"
        element={
          <Suspense fallback={<Loading />}>
            <Admin />
          </Suspense>
        }
      />
      <Route element={<Layout />}>
        <Route index element={<Home />} />
        <Route path="shop" element={<Shop />} />
        <Route path="shop/:slug" element={<ProductPage />} />
        <Route path="build" element={<Builder />} />
        <Route path="cart" element={<Cart />} />
        <Route path="checkout" element={<Checkout />} />
        <Route path="login" element={<AuthPage key="login" mode="login" />} />
        <Route path="register" element={<AuthPage key="register" mode="register" />} />
        <Route path="account" element={<AccountLayout />}>
          <Route index element={<Orders />} />
          <Route path="orders/:number" element={<OrderPage />} />
          <Route path="wishlist" element={<WishlistPage />} />
          <Route path="addresses" element={<Addresses />} />
          <Route path="profile" element={<Profile />} />
        </Route>
        <Route path="journal" element={<Journal />} />
        <Route path="journal/:slug" element={<PostPage />} />
        <Route path="about" element={<About />} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}
