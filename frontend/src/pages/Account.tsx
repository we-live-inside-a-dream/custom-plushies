import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, Navigate, NavLink, Outlet, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, Field, Loading, PageHead, ProductCard, StatusChip } from '../components/ui'
import { api, ApiError, money, shortDate } from '../lib/api'
import type { Address, Customer, Order } from '../lib/types'
import { useAuth, useToast, useWishlist } from '../state/session'
import { LineArt, SummaryRows } from './Cart'
import { AddressFields, emptyAddress, toForm, type AddressForm } from './Checkout'
import { SHOWCASE } from './Home'
import { NotFound } from './Misc'

export function AccountLayout() {
  const { customer, ready, signOut } = useAuth()
  const navigate = useNavigate()
  if (!ready) return <Loading />
  if (!customer) return <Navigate to="/login?next=/account" replace />
  return (
    <>
      <PageHead title={`Hi, ${customer.firstName}`} kicker="Your account" />
      <div className="wrap account">
        <nav className="pills account__nav" aria-label="Account">
          <NavLink to="/account" end className="pill">Orders</NavLink>
          <NavLink to="/account/wishlist" className="pill">Wishlist</NavLink>
          <NavLink to="/account/addresses" className="pill">Addresses</NavLink>
          <NavLink to="/account/profile" className="pill">Profile</NavLink>
          <button
            type="button"
            className="btn btn--quiet"
            onClick={() => {
              signOut()
              navigate('/')
            }}
          >
            Sign out
          </button>
        </nav>
        <Outlet />
      </div>
    </>
  )
}

export function Orders() {
  const { data, isLoading, error } = useQuery({ queryKey: ['orders'], queryFn: () => api<Order[]>('/api/orders') })
  if (isLoading) return <Loading label="Loading your orders" />
  if (error) return <ErrorNotice error={error} />
  if (!data?.length)
    return (
      <div className="empty">
        <Plushie design={SHOWCASE[4]} />
        <h2>No orders yet</h2>
        <p>When you adopt a plushie, it'll show up here with its tracking details.</p>
        <Link to="/shop" className="btn btn--pink">
          Find a friend
        </Link>
      </div>
    )
  return (
    <ul className="stack">
      {data.map((o) => (
        <li key={o.id}>
          <Link to={`/account/orders/${o.orderNumber}`} className="order-row card">
            <div className="order-row__art">
              {o.items.slice(0, 3).map((i) => (
                <LineArt key={i.id} line={{ design: i.design, imageUrl: i.imageUrl, name: i.productName }} />
              ))}
            </div>
            <div>
              <b>{o.orderNumber}</b>
              <span className="muted">
                {shortDate(o.createdAt)} · {o.items.reduce((n, i) => n + i.quantity, 0)} item{o.items.length === 1 && o.items[0].quantity === 1 ? '' : 's'}
              </span>
            </div>
            <StatusChip status={o.status} />
            <strong>{money(o.totalCents, o.currency)}</strong>
          </Link>
        </li>
      ))}
    </ul>
  )
}

const STEPS = ['paid', 'in_production', 'shipped', 'delivered'] as const
const STEP_LABEL = { paid: 'Order placed', in_production: 'Being stitched', shipped: 'On its way', delivered: 'Delivered' }

export function OrderPage() {
  const { number = '' } = useParams()
  const [params] = useSearchParams()
  const { data: o, isLoading, error } = useQuery({ queryKey: ['orders', number], queryFn: () => api<Order>(`/api/orders/${number}`) })
  if (isLoading) return <Loading label="Loading order" />
  if (error instanceof ApiError && error.status === 404) return <NotFound />
  if (!o) return <ErrorNotice error={error ?? new Error()} />

  const stepIndex = STEPS.indexOf(o.status as (typeof STEPS)[number])
  const shipment = o.shipments[o.shipments.length - 1]

  return (
    <div className="stack" style={{ '--gap': '24px' } as React.CSSProperties}>
      {params.get('placed') && (
        <div className="certificate patch">
          <div className="certificate__art">
            {o.items.slice(0, 3).map((i) => i.design && <Plushie key={i.id} design={i.design} />)}
          </div>
          <div className="stack" style={{ '--gap': '8px' } as React.CSSProperties}>
            <span className="label">Certificate of adoption</span>
            <h2>It's official!</h2>
            <p>
              Thank you — order <b>{o.orderNumber}</b> is confirmed. Come back to this page any time to follow it from sewing table to doorstep.
            </p>
          </div>
        </div>
      )}

      <div className="spread">
        <div>
          <Link to="/account" className="label">
            ← All orders
          </Link>
          <h2>Order {o.orderNumber}</h2>
          <span className="muted">Placed {shortDate(o.createdAt)}</span>
        </div>
        <StatusChip status={o.status} />
      </div>

      {stepIndex >= 0 && (
        <ol className="track" aria-label="Order progress">
          {STEPS.map((s, i) => (
            <li key={s} className={i < stepIndex ? 'is-done' : i === stepIndex ? 'is-now' : ''} aria-current={i === stepIndex ? 'step' : undefined}>
              <span className="track__dot" />
              {STEP_LABEL[s]}
            </li>
          ))}
        </ol>
      )}

      {shipment && (
        <div className="notice notice--ok spread">
          <span>
            <b>{shipment.carrier ? shipment.carrier.replace(/_/g, ' ') : 'Shipped'}</b>
            {shipment.trackingCode && <> · tracking <code>{shipment.trackingCode}</code></>}
          </span>
          {shipment.trackingUrl && (
            <a className="btn btn--sm" href={shipment.trackingUrl} target="_blank" rel="noreferrer">
              Track parcel
            </a>
          )}
        </div>
      )}

      <div className="order-detail">
        <ul className="card mini-lines mini-lines--lg">
          {o.items.map((i) => (
            <li key={i.id}>
              <LineArt line={{ design: i.design, imageUrl: i.imageUrl, name: i.productName }} />
              <div>
                <b>{i.productName}</b>
                <span className="muted">
                  {i.variantName} × {i.quantity}
                </span>
                {i.isCustom && <span className="chip chip--mint">Stitched to order</span>}
              </div>
              <span>{money(i.totalCents, o.currency)}</span>
            </li>
          ))}
        </ul>
        <div className="stack">
          <div className="card">
            <SummaryRows subtotal={o.subtotalCents} discount={o.discountCents} couponCode={o.couponCode} shipping={o.shippingCents} tax={o.taxCents} taxLabel={o.taxLabel} total={o.totalCents} currency={o.currency} />
          </div>
          <div className="card stack" style={{ '--gap': '6px' } as React.CSSProperties}>
            <span className="label muted">Shipping to</span>
            <address>
              <b>{o.shipTo.name}</b>
              <br />
              {o.shipTo.address1}
              {o.shipTo.address2 && <>, {o.shipTo.address2}</>}
              <br />
              {o.shipTo.city} {o.shipTo.provinceCode} {o.shipTo.postalCode}
              <br />
              {o.shipTo.countryCode}
            </address>
            <span className="muted">{o.shippingMethodName}</span>
          </div>
        </div>
      </div>
    </div>
  )
}

export function WishlistPage() {
  const { items, isLoading } = useWishlist()
  if (isLoading) return <Loading />
  if (!items.length)
    return (
      <div className="empty">
        <Plushie design={SHOWCASE[2]} />
        <h2>Nothing saved yet</h2>
        <p>Tap the heart on any plushie to keep it here for later.</p>
        <Link to="/shop" className="btn btn--pink">
          Browse the shelf
        </Link>
      </div>
    )
  return (
    <div className="product-grid">
      {items.map((p) => (
        <ProductCard key={p.id} product={p} />
      ))}
    </div>
  )
}

export function Addresses() {
  const qc = useQueryClient()
  const toast = useToast()
  const { data, isLoading } = useQuery({ queryKey: ['addresses'], queryFn: () => api<Address[]>('/api/account/addresses') })
  const [editing, setEditing] = useState<number | 'new' | null>(null)
  const [form, setForm] = useState<AddressForm>(emptyAddress)
  const [isDefault, setIsDefault] = useState(false)
  const done = () => qc.invalidateQueries({ queryKey: ['addresses'] })

  const save = useMutation({
    mutationFn: () => api(editing === 'new' ? '/api/account/addresses' : `/api/account/addresses/${editing}`, { method: editing === 'new' ? 'POST' : 'PUT', body: { ...form, isDefault } }),
    onSuccess: () => {
      done()
      setEditing(null)
      toast('Address saved.')
    },
  })
  const remove = useMutation({ mutationFn: (id: number) => api(`/api/account/addresses/${id}`, { method: 'DELETE' }), onSuccess: done })

  if (isLoading) return <Loading />

  if (editing !== null)
    return (
      <form
        className="card stack"
        style={{ maxWidth: 640 }}
        onSubmit={(e) => {
          e.preventDefault()
          save.mutate()
        }}
      >
        <h2>{editing === 'new' ? 'New address' : 'Edit address'}</h2>
        <AddressFields value={form} onChange={setForm} errors={save.error instanceof ApiError ? save.error.fields : {}} />
        <label className="check">
          <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} />
          Use as my default address
        </label>
        <ErrorNotice error={save.error} />
        <div className="row">
          <button className="btn btn--pink" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save address'}
          </button>
          <button type="button" className="btn" onClick={() => { setEditing(null); save.reset() }}>
            Cancel
          </button>
        </div>
      </form>
    )

  return (
    <div className="stack">
      <div className="address-grid">
        {data?.map((a) => (
          <div key={a.id} className="card stack" style={{ '--gap': '10px' } as React.CSSProperties}>
            {a.isDefault && <span className="chip chip--lemon" style={{ justifySelf: 'start' }}>Default</span>}
            <address>
              <b>{a.recipientName}</b>
              <br />
              {a.address1}
              {a.address2 && <>, {a.address2}</>}
              <br />
              {a.city} {a.provinceCode} {a.postalCode}, {a.countryCode}
            </address>
            <div className="row">
              <button type="button" className="btn btn--sm" onClick={() => { setForm(toForm(a)); setIsDefault(a.isDefault); setEditing(a.id) }}>
                Edit
              </button>
              <button
                type="button"
                className="btn btn--quiet"
                onClick={() => window.confirm(`Delete the address for ${a.recipientName}?`) && remove.mutate(a.id)}
              >
                Delete
              </button>
            </div>
          </div>
        ))}
      </div>
      {data?.length === 0 && <p className="muted">No saved addresses yet.</p>}
      <div>
        <button type="button" className="btn btn--lemon" onClick={() => { setForm(emptyAddress); setIsDefault(!data?.length); setEditing('new') }}>
          Add an address
        </button>
      </div>
    </div>
  )
}

export function Profile() {
  const { customer, setCustomer } = useAuth()
  const toast = useToast()
  const [form, setForm] = useState({ firstName: customer?.firstName ?? '', lastName: customer?.lastName ?? '', phone: customer?.phone ?? '' })
  const save = useMutation({
    mutationFn: () => api<Customer>('/api/account/profile', { method: 'PUT', body: form }),
    onSuccess: (c) => {
      setCustomer(c)
      toast('Profile updated.')
    },
  })
  const errors = save.error instanceof ApiError ? save.error.fields : {}
  return (
    <form
      className="card stack"
      style={{ maxWidth: 560 }}
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <h2>Your details</h2>
      <div className="form-grid">
        <Field label="First name" error={errors.firstName}>
          {(p) => <input {...p} className="input" autoComplete="given-name" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} required />}
        </Field>
        <Field label="Last name" error={errors.lastName}>
          {(p) => <input {...p} className="input" autoComplete="family-name" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} required />}
        </Field>
        <Field label="Email" hint="Your email is your sign-in and can't be changed here." className="span-2">
          {(p) => <input {...p} className="input" value={customer?.email ?? ''} readOnly />}
        </Field>
        <Field label="Phone (optional)" error={errors.phone} className="span-2">
          {(p) => <input {...p} className="input" type="tel" autoComplete="tel" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />}
        </Field>
      </div>
      <ErrorNotice error={save.error} />
      <div>
        <button className="btn btn--pink" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : 'Save changes'}
        </button>
      </div>
    </form>
  )
}
