import '../styles/admin.scss'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Logo } from '../components/Layout'
import { ErrorNotice, Field, Loading, StatusChip } from '../components/ui'
import { api, authEvents, money, shortDate, tokens } from '../lib/api'
import { OrderDetail, OrdersList } from './Orders'
import { ProductEditor, ProductsList } from './Products'
import { RESOURCES } from './resources'
import { ResourcePage } from './shared'

type AdminUser = { id: number; email: string; displayName: string | null; role: string }

const NAV: [string, [string, string][]][] = [
  ['Sell', [['', 'Dashboard'], ['orders', 'Orders'], ['customers', 'Customers']]],
  ['Catalogue', [['products', 'Products'], ['categories', 'Categories'], ['brands', 'Brands'], ['variant-types', 'Variant types']]],
  ['Marketing', [['promotions', 'Promotions'], ['coupons', 'Coupons']]],
  ['Storefront', [['content', 'Page text'], ['store-images', 'Images'], ['posts', 'Journal'], ['post-types', 'Post types']]],
  ['Settings', [['shipping-methods', 'Shipping'], ['tax-rates', 'Tax']]],
]

export default function Admin() {
  const qc = useQueryClient()
  const [hasToken, setHasToken] = useState(() => !!tokens.admin())
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const me = useQuery({ queryKey: ['admin', 'me'], queryFn: () => api<AdminUser>('/api/admin/auth/me'), enabled: hasToken, retry: false })

  const signOut = () => {
    tokens.setAdmin(null)
    setHasToken(false)
    qc.removeQueries({ queryKey: ['admin'] })
  }
  useEffect(() => {
    authEvents.onAdminExpired = signOut
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  useEffect(() => setOpen(false), [pathname])
  useEffect(() => {
    document.title = 'Stitchling admin'
    return () => void (document.title = 'Stitchling — custom plushies, stitched to order')
  }, [])

  if (!hasToken || me.isError) return <AdminLogin onSignedIn={() => setHasToken(true)} />
  if (!me.data) return <Loading />

  return (
    <div className="admin">
      <aside className={`admin__side ${open ? 'is-open' : ''}`}>
        <div className="admin__brand">
          <Logo name="Stitchling" />
          <span className="chip chip--lemon">Admin</span>
          <button type="button" className="btn btn--sm admin__toggle" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
            Menu
          </button>
        </div>
        <nav aria-label="Admin">
          {NAV.map(([group, links]) => (
            <div key={group}>
              <span className="label">{group}</span>
              {links.map(([to, label]) => (
                <NavLink key={to} to={`/admin/${to}`} end={to === ''}>
                  {label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="admin__me">
          <span>{me.data.displayName ?? me.data.email}</span>
          <span className="label">{me.data.role}</span>
          <div className="row">
            <Link to="/" className="btn btn--sm">
              View shop
            </Link>
            <button type="button" className="btn btn--sm" onClick={signOut}>
              Sign out
            </button>
          </div>
        </div>
      </aside>
      <main className="admin__main">
        <Routes>
          <Route index element={<Dashboard />} />
          <Route path="orders" element={<OrdersList />} />
          <Route path="orders/:id" element={<OrderDetail />} />
          <Route path="products" element={<ProductsList />} />
          <Route path="products/:id" element={<ProductEditor key={pathname} />} />
          <Route path="customers" element={<Customers />} />
          <Route path="content" element={<ContentEditor />} />
          {Object.entries(RESOURCES).map(([path, resource]) => (
            <Route key={path} path={path} element={<ResourcePage key={path} resource={resource} />} />
          ))}
          <Route path="*" element={<Navigate to="/admin" replace />} />
        </Routes>
      </main>
    </div>
  )
}

function AdminLogin({ onSignedIn }: { onSignedIn: () => void }) {
  const [form, setForm] = useState({ email: '', password: '' })
  const login = useMutation({
    mutationFn: () => api<{ token: string }>('/api/admin/auth/login', { method: 'POST', body: form }),
    onSuccess: ({ token }) => {
      tokens.setAdmin(token)
      onSignedIn()
    },
  })
  return (
    <div className="admin-login">
      <form
        className="card stack"
        noValidate
        onSubmit={(e) => {
          e.preventDefault()
          login.mutate()
        }}
      >
        <Logo name="Stitchling" />
        <h1>Staff sign-in</h1>
        <Field label="Email">{(p) => <input {...p} className="input" type="email" autoComplete="username" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />}</Field>
        <Field label="Password">{(p) => <input {...p} className="input" type="password" autoComplete="current-password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />}</Field>
        <ErrorNotice error={login.error} />
        <button className="btn btn--pink btn--block" disabled={login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
        <Link to="/" className="btn btn--quiet">
          ← Back to the shop
        </Link>
      </form>
    </div>
  )
}

type OrderRow = { id: number; orderNumber: string; status: string; customer: string; destination: string; totalCents: number; currency: string; createdAt: string }
type DashboardData = {
  revenueCents30d: number; orders30d: number; awaitingShipment: number; inTransit: number; customers: number
  recentOrders: OrderRow[]
  lowStock: { productId: number; product: string; variant: string; sku: string; stock: number }[]
  daily: { day: string; revenueCents: number; orders: number }[]
}

function Dashboard() {
  const { data, isLoading, error } = useQuery({ queryKey: ['admin', 'dashboard'], queryFn: () => api<DashboardData>('/api/admin/dashboard') })
  if (isLoading) return <Loading />
  if (!data) return <ErrorNotice error={error ?? new Error()} />
  const average = data.orders30d ? Math.round(data.revenueCents30d / data.orders30d) : 0

  return (
    <div className="stack" style={{ '--gap': '24px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>Dashboard</h1>
          <p className="muted">The last 30 days at a glance.</p>
        </div>
      </div>

      <div className="tiles-row">
        <Tile label="Revenue, 30 days" value={money(data.revenueCents30d)} note="Paid orders, incl. shipping and tax" />
        <Tile label="Orders, 30 days" value={String(data.orders30d)} note={data.orders30d ? `${money(average)} average order` : 'No orders yet'} />
        <Tile label="Waiting to ship" value={String(data.awaitingShipment)} note="Paid or in production" link={data.awaitingShipment ? '/admin/orders' : undefined} />
        <Tile label="In transit" value={String(data.inTransit)} note="Shipped, not yet delivered" />
      </div>

      <section className="card stack">
        <h2>Revenue per day</h2>
        <RevenueChart days={data.daily} />
      </section>

      <div className="admin-cols">
        <section className="card stack">
          <div className="spread">
            <h2>Latest orders</h2>
            <Link to="/admin/orders" className="btn btn--sm">
              All orders
            </Link>
          </div>
          {data.recentOrders.length === 0 ? (
            <p className="muted">No orders yet. They'll appear here as they come in.</p>
          ) : (
            <table className="table table--flush">
              <tbody>
                {data.recentOrders.map((o) => (
                  <tr key={o.id}>
                    <td>
                      <Link to={`/admin/orders/${o.id}`}>
                        <b>{o.orderNumber}</b>
                      </Link>
                      <br />
                      <span className="muted">{o.customer} · {o.destination}</span>
                    </td>
                    <td>
                      <StatusChip status={o.status} />
                    </td>
                    <td className="num">{money(o.totalCents, o.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
        <section className="card stack">
          <h2>Running low</h2>
          {data.lowStock.length === 0 ? (
            <p className="muted">Everything has more than 3 in stock.</p>
          ) : (
            <table className="table table--flush">
              <tbody>
                {data.lowStock.map((v) => (
                  <tr key={v.sku}>
                    <td>
                      <Link to={`/admin/products/${v.productId}`}>
                        <b>{v.product}</b>
                      </Link>
                      <br />
                      <span className="muted">{v.variant}</span>
                    </td>
                    <td className="num">{v.stock === 0 ? <span className="chip chip--pink">Sold out</span> : `${v.stock} left`}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </div>
  )
}

function Tile({ label, value, note, link }: { label: string; value: string; note: string; link?: string }) {
  const body = (
    <>
      <span className="stat__label">{label}</span>
      <span className="stat__value">{value}</span>
      <span className="stat__note">{note}</span>
    </>
  )
  return link ? (
    <Link to={link} className="stat card">
      {body}
    </Link>
  ) : (
    <div className="stat card">{body}</div>
  )
}

/** One series (daily revenue), so: one colour, no legend, thin columns, a tooltip per column and a table view. */
function RevenueChart({ days }: { days: DashboardData['daily'] }) {
  const [active, setActive] = useState<number | null>(null)
  const max = Math.max(...days.map((d) => d.revenueCents), 1)
  // Round the axis top up to a tidy number of dollars.
  const magnitude = 10 ** Math.floor(Math.log10(max / 100 || 1))
  const top = Math.ceil(max / 100 / magnitude) * magnitude * 100
  const total = days.reduce((n, d) => n + d.revenueCents, 0)
  const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString('en-CA', { month: 'short', day: 'numeric' })
  const shown = active === null ? null : days[active]

  if (total === 0) return <p className="muted">No paid orders in the last 30 days yet.</p>
  return (
    <figure className="chart">
      <div className="chart__plot" onMouseLeave={() => setActive(null)}>
        <div className="chart__axis" aria-hidden="true">
          <span>{money(top)}</span>
          <span>{money(top / 2)}</span>
          <span>$0</span>
        </div>
        <div className="chart__bars" role="img" aria-label={`Revenue per day for the last 30 days, ${money(total)} in total. Highest day ${money(max)}.`}>
          {days.map((d, i) => (
            <button
              key={d.day}
              type="button"
              className={`chart__col ${active === i ? 'is-active' : ''}`}
              onMouseEnter={() => setActive(i)}
              onFocus={() => setActive(i)}
              onBlur={() => setActive(null)}
              aria-label={`${day(d.day)}: ${money(d.revenueCents)} from ${d.orders} order${d.orders === 1 ? '' : 's'}`}
            >
              <span style={{ height: `${(d.revenueCents / top) * 100}%` }} />
            </button>
          ))}
          {shown && active !== null && (
            <div className="chart__tip" style={{ left: `${((active + 0.5) / days.length) * 100}%` }} role="presentation">
              <b>{money(shown.revenueCents)}</b>
              <span>
                {day(shown.day)} · {shown.orders} order{shown.orders === 1 ? '' : 's'}
              </span>
            </div>
          )}
        </div>
      </div>
      <div className="chart__x" aria-hidden="true">
        <span>{day(days[0].day)}</span>
        <span>{day(days[Math.floor(days.length / 2)].day)}</span>
        <span>{day(days[days.length - 1].day)}</span>
      </div>
      <details className="chart__table">
        <summary>View as a table</summary>
        <table className="table table--flush">
          <thead>
            <tr>
              <th>Day</th>
              <th className="num">Orders</th>
              <th className="num">Revenue</th>
            </tr>
          </thead>
          <tbody>
            {days.filter((d) => d.orders > 0).map((d) => (
              <tr key={d.day}>
                <td>{day(d.day)}</td>
                <td className="num">{d.orders}</td>
                <td className="num">{money(d.revenueCents)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </figure>
  )
}

function Customers() {
  const [q, setQ] = useState('')
  const { data, isLoading, error } = useQuery({
    queryKey: ['admin', 'customers', q],
    queryFn: () => api<{ id: number; email: string; name: string; phone: string | null; orderCount: number; spentCents: number; createdAt: string }[]>(`/api/admin/customers?q=${encodeURIComponent(q)}`),
  })
  return (
    <div className="stack" style={{ '--gap': '20px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>Customers</h1>
          <p className="muted">Everyone with an account, newest first.</p>
        </div>
      </div>
      <input className="input" style={{ maxWidth: 320 }} type="search" placeholder="Search by name or email" aria-label="Search customers" value={q} onChange={(e) => setQ(e.target.value)} />
      <ErrorNotice error={error} />
      {isLoading ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Customer</th>
                <th>Joined</th>
                <th className="num">Orders</th>
                <th className="num">Spent</th>
              </tr>
            </thead>
            <tbody>
              {data?.map((c) => (
                <tr key={c.id}>
                  <td>
                    <b>{c.name}</b>
                    <br />
                    <span className="muted">{c.email}{c.phone && ` · ${c.phone}`}</span>
                  </td>
                  <td>{shortDate(c.createdAt)}</td>
                  <td className="num">{c.orderCount}</td>
                  <td className="num">{money(c.spentCents)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

type PageTexts = { id: number; name: string; texts: { code: string; value: string }[] }

function ContentEditor() {
  const qc = useQueryClient()
  const pages = useQuery({ queryKey: ['admin', '/api/admin/content/pages'], queryFn: () => api<PageTexts[]>('/api/admin/content/pages') })
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [savedPage, setSavedPage] = useState<number | null>(null)
  const save = useMutation({
    mutationFn: (page: PageTexts) =>
      api(`/api/admin/content/pages/${page.id}/texts`, { method: 'PUT', body: { texts: Object.fromEntries(page.texts.map((t) => [t.code, drafts[`${page.id}.${t.code}`] ?? t.value])) } }),
    onSuccess: (_, page) => {
      setSavedPage(page.id)
      qc.invalidateQueries({ queryKey: ['admin', '/api/admin/content/pages'] })
      qc.invalidateQueries({ queryKey: ['page', page.name] })
    },
  })
  if (pages.isLoading) return <Loading />
  return (
    <div className="stack" style={{ '--gap': '22px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>Page text</h1>
          <p className="muted">The words on the storefront. Changes show up as soon as you save.</p>
        </div>
      </div>
      <ErrorNotice error={pages.error ?? save.error} />
      {pages.data?.map((page) => (
        <form
          key={page.id}
          className="card stack"
          onSubmit={(e) => {
            e.preventDefault()
            save.mutate(page)
          }}
        >
          <h2 style={{ textTransform: 'capitalize' }}>{page.name}</h2>
          <div className="form-grid">
            {page.texts.map((t) => {
              const key = `${page.id}.${t.code}`
              const value = drafts[key] ?? t.value
              const long = value.length > 70
              return (
                <div key={t.code} className={`field ${long ? 'span-2' : ''}`}>
                  <label htmlFor={key}>
                    <code>{t.code}</code>
                  </label>
                  {long ? (
                    <textarea id={key} className="textarea" style={{ minHeight: 84 }} value={value} onChange={(e) => { setSavedPage(null); setDrafts({ ...drafts, [key]: e.target.value }) }} />
                  ) : (
                    <input id={key} className="input" value={value} onChange={(e) => { setSavedPage(null); setDrafts({ ...drafts, [key]: e.target.value }) }} />
                  )}
                </div>
              )
            })}
          </div>
          <div className="row">
            <button className="btn btn--pink" disabled={save.isPending}>
              Save {page.name} text
            </button>
            {savedPage === page.id && (
              <span className="chip chip--mint" role="status">
                Saved
              </span>
            )}
          </div>
        </form>
      ))}
    </div>
  )
}
