import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError, authEvents, tokens } from '../lib/api'
import type { Cart, Customer, PlushieDesign, ProductSummary, StorePage } from '../lib/types'

// ---- Toasts -----------------------------------------------------------------------------------
type Toast = { id: number; message: string; kind: 'ok' | 'error'; link?: { to: string; label: string } }
const ToastContext = createContext<(message: string, options?: Partial<Omit<Toast, 'id' | 'message'>>) => void>(() => {})
export const useToast = () => useContext(ToastContext)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)
  const push = useCallback((message: string, options: Partial<Omit<Toast, 'id' | 'message'>> = {}) => {
    const id = nextId.current++
    setToasts((t) => [...t.slice(-2), { id, message, kind: options.kind ?? 'ok', link: options.link }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4200)
  }, [])
  return (
    <ToastContext.Provider value={push}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`toast ${t.kind === 'error' ? 'toast--error' : ''}`}>
            <span>{t.message}</span>
            {t.link && <Link to={t.link.to}>{t.link.label}</Link>}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

// ---- Customer session -------------------------------------------------------------------------
type Auth = {
  customer: Customer | null
  ready: boolean
  signIn: (response: { token: string; customer: Customer }) => void
  signOut: () => void
  setCustomer: (c: Customer) => void
}
const AuthContext = createContext<Auth>({ customer: null, ready: false, signIn: () => {}, signOut: () => {}, setCustomer: () => {} })
export const useAuth = () => useContext(AuthContext)

export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const [customer, setCustomer] = useState<Customer | null>(null)
  const [ready, setReady] = useState(() => !tokens.customer())

  const signOut = useCallback(() => {
    tokens.setCustomer(null)
    setCustomer(null)
    qc.removeQueries({ queryKey: ['wishlist'] })
    qc.removeQueries({ queryKey: ['orders'] })
    qc.invalidateQueries({ queryKey: ['cart'] })
  }, [qc])

  useEffect(() => {
    authEvents.onCustomerExpired = signOut
    if (!tokens.customer()) return
    // Restore the session: the stored token is only trusted once the API accepts it.
    api<Customer>('/api/account/profile')
      .then(setCustomer)
      .catch(() => tokens.setCustomer(null))
      .finally(() => setReady(true))
  }, [signOut])

  const value = useMemo<Auth>(
    () => ({
      customer,
      ready,
      setCustomer,
      signOut,
      signIn: ({ token, customer }) => {
        tokens.setCustomer(token)
        tokens.setCart(null) // the guest cart was merged into the account on the server
        setCustomer(customer)
        qc.invalidateQueries({ queryKey: ['cart'] })
        qc.invalidateQueries({ queryKey: ['wishlist'] })
      },
    }),
    [customer, ready, signOut, qc],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// ---- Cart -------------------------------------------------------------------------------------
export const useCart = () => useQuery({ queryKey: ['cart'], queryFn: () => api<Cart>('/api/cart') })

export type CustomDesignInput = { name: string; design: PlushieDesign; size: string; embroideryText?: string; notes?: string }

export function useCartActions() {
  const qc = useQueryClient()
  const toast = useToast()
  const store = (cart: Cart) => {
    if (cart.token) tokens.setCart(cart.token)
    qc.setQueryData(['cart'], cart)
    return cart
  }
  const fail = (e: unknown) => toast(e instanceof ApiError ? e.message : 'Something went wrong.', { kind: 'error' })

  const add = useMutation({
    mutationFn: (item: { productVariantId?: number; customDesign?: CustomDesignInput; quantity: number }) =>
      api<Cart>('/api/cart/items', { method: 'POST', body: item }).then(store),
    onError: fail,
  })
  const update = useMutation({
    mutationFn: ({ id, quantity }: { id: number; quantity: number }) =>
      api<Cart>(`/api/cart/items/${id}`, { method: 'PATCH', body: { quantity } }).then(store),
    onError: fail,
  })
  const remove = useMutation({
    mutationFn: (id: number) => api<Cart>(`/api/cart/items/${id}`, { method: 'DELETE' }).then(store),
    onError: fail,
  })
  const applyCoupon = useMutation({
    mutationFn: (code: string) => api<Cart>('/api/cart/coupon', { method: 'POST', body: { code } }).then(store),
  })
  const removeCoupon = useMutation({
    mutationFn: () => api<Cart>('/api/cart/coupon', { method: 'DELETE' }).then(store),
    onError: fail,
  })
  return { add, update, remove, applyCoupon, removeCoupon }
}

// ---- Wishlist ---------------------------------------------------------------------------------
export function useWishlist() {
  const { customer } = useAuth()
  const qc = useQueryClient()
  const toast = useToast()
  const query = useQuery({ queryKey: ['wishlist'], queryFn: () => api<ProductSummary[]>('/api/wishlist'), enabled: !!customer })
  const ids = useMemo(() => new Set((query.data ?? []).map((p) => p.id)), [query.data])
  const toggle = useMutation({
    mutationFn: (productId: number) => api(`/api/wishlist/${productId}`, { method: ids.has(productId) ? 'DELETE' : 'PUT' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['wishlist'] }),
    onError: (e) => toast(e instanceof ApiError ? e.message : 'Could not update your wishlist.', { kind: 'error' }),
  })
  return { items: query.data ?? [], isLoading: query.isLoading, has: (id: number) => ids.has(id), toggle }
}

// ---- Editable store copy ----------------------------------------------------------------------
/** Text for a storefront page from store_text_info, with built-in fallbacks while it loads. */
export function useCopy(page: string, fallback: Record<string, string> = {}) {
  const { data } = useQuery({ queryKey: ['page', page], queryFn: () => api<StorePage>(`/api/content/pages/${page}`), staleTime: 5 * 60_000 })
  return (code: string) => data?.texts[code] ?? fallback[code] ?? ''
}
