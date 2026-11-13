// One small fetch wrapper for the whole app. It attaches the right credentials (customer token for
// the storefront, staff token for /api/admin), carries the guest cart token, and turns the API's
// problem+json errors into ApiError so screens can show the server's own message.

const CUSTOMER_TOKEN = 'stitchling.token'
const ADMIN_TOKEN = 'stitchling.admin-token'
const CART_TOKEN = 'stitchling.cart'

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null // private mode / storage blocked: behave as signed out
  }
}
function write(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    /* storage unavailable: session lasts until reload */
  }
}

export const tokens = {
  customer: () => read(CUSTOMER_TOKEN),
  setCustomer: (t: string | null) => write(CUSTOMER_TOKEN, t),
  admin: () => read(ADMIN_TOKEN),
  setAdmin: (t: string | null) => write(ADMIN_TOKEN, t),
  cart: () => read(CART_TOKEN),
  setCart: (t: string | null) => write(CART_TOKEN, t),
}

export class ApiError extends Error {
  status: number
  /** Field-level messages keyed by request field, e.g. "address.city". */
  fields: Record<string, string>

  constructor(message: string, status: number, fields: Record<string, string> = {}) {
    super(message)
    this.status = status
    this.fields = fields
  }
}

/** Called when the API says a stored token is no longer valid, so the UI can sign the person out. */
export const authEvents = { onCustomerExpired: () => {}, onAdminExpired: () => {} }

type Options = { method?: string; body?: unknown; form?: FormData; signal?: AbortSignal; raw?: boolean }

export async function api<T = unknown>(path: string, { method = 'GET', body, form, signal, raw }: Options = {}): Promise<T> {
  const isAdmin = path.startsWith('/api/admin')
  const headers: Record<string, string> = {}
  const token = isAdmin ? tokens.admin() : tokens.customer()
  if (token) headers.Authorization = `Bearer ${token}`
  const cart = tokens.cart()
  if (cart && !isAdmin) headers['X-Cart-Token'] = cart
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(path, { method, headers, body: form ?? (body !== undefined ? JSON.stringify(body) : undefined), signal })
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e
    throw new ApiError("We couldn't reach the shop. Check your connection and try again.", 0)
  }

  if (response.status === 401 && token && !path.includes('/auth/')) {
    if (isAdmin) authEvents.onAdminExpired()
    else authEvents.onCustomerExpired()
  }

  if (!response.ok) {
    let message = response.status === 429 ? 'Too many attempts. Wait a minute and try again.' : `Something went wrong (${response.status}).`
    const fields: Record<string, string> = {}
    try {
      const problem = await response.json()
      if (problem.title) message = problem.title
      for (const [key, value] of Object.entries(problem.errors ?? {})) fields[key] = (value as string[])[0]
    } catch {
      /* not JSON: keep the generic message */
    }
    throw new ApiError(message, response.status, fields)
  }

  if (raw) return response as T
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export function money(cents: number, currency = 'cad'): string {
  return new Intl.NumberFormat('en-CA', { style: 'currency', currency: currency.toUpperCase(), currencyDisplay: 'narrowSymbol' }).format(cents / 100)
}

export function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-CA', { year: 'numeric', month: 'short', day: 'numeric' })
}
