import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorNotice, Loading, StatusChip, statusLabel } from '../components/ui'
import { api, money, shortDate } from '../lib/api'
import type { Order, Paged } from '../lib/types'
import { LineArt, SummaryRows } from '../pages/Cart'

type OrderRow = { id: number; orderNumber: string; status: string; customer: string; email: string; destination: string; itemCount: number; totalCents: number; currency: string; createdAt: string; shipmentStatus: string | null; hasCustom: boolean }
type Rate = { postageType: string; description: string | null; carrier: string | null; deliveryTime: string | null; tracking: string | null; isInsured: boolean; paymentAmount: string | null }
type Shipment = { id: number; orderId: number; chitChatsShipmentId: string; status: string; postageType: string | null; carrier: string | null; trackingCode: string | null; trackingUrl: string | null; postageCostCents: number | null; packageType: string; weightGrams: number; sizeXCm: number; sizeYCm: number; sizeZCm: number; hasLabel: boolean; shipDate: string | null; lastSyncedAt: string | null; createdAt: string; rates: Rate[] }
type Detail = { order: Order; customerId: number; customerName: string; payments: { provider: string; reference: string | null; amountCents: number; status: string; createdAt: string }[]; shipments: Shipment[]; nextStatuses: string[] }
type Defaults = { packageType: string; weightGrams: number; sizeXCm: number; sizeYCm: number; sizeZCm: number; preferredPostageType: string | null; packageTypes: string[] }

const STATUSES = ['paid', 'in_production', 'shipped', 'delivered', 'cancelled', 'refunded']
const pretty = (s: string) => s.replace(/_/g, ' ')

export function OrdersList() {
  const [status, setStatus] = useState('')
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const { data, isLoading, error } = useQuery({
    queryKey: ['admin', 'orders', status, q, page],
    queryFn: () => api<Paged<OrderRow>>(`/api/admin/orders?${new URLSearchParams({ status, q, page: String(page) })}`),
    placeholderData: keepPreviousData,
  })
  useEffect(() => setPage(1), [status, q])

  return (
    <div className="stack" style={{ '--gap': '20px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>Orders</h1>
          <p className="muted">Newest first. Open an order to update it or ship it with Chit Chats.</p>
        </div>
      </div>
      <div className="row">
        <div className="pills" role="group" aria-label="Filter by status">
          <button type="button" className="pill" aria-pressed={!status} onClick={() => setStatus('')}>
            All
          </button>
          {STATUSES.map((s) => (
            <button key={s} type="button" className="pill" aria-pressed={status === s} onClick={() => setStatus(s)}>
              {statusLabel(s)}
            </button>
          ))}
        </div>
        <input className="input" style={{ maxWidth: 280, marginLeft: 'auto' }} type="search" placeholder="Order #, name or email" aria-label="Search orders" value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      <ErrorNotice error={error} />
      {isLoading ? (
        <Loading />
      ) : data?.items.length === 0 ? (
        <p className="card muted">No orders match that.</p>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Order</th>
                <th>Customer</th>
                <th>Going to</th>
                <th>Items</th>
                <th className="num">Total</th>
                <th>Status</th>
                <th>Shipment</th>
              </tr>
            </thead>
            <tbody>
              {data?.items.map((o) => (
                <tr key={o.id}>
                  <td>
                    <Link to={`/admin/orders/${o.id}`}>
                      <b>{o.orderNumber}</b>
                    </Link>
                    <br />
                    <span className="muted">{shortDate(o.createdAt)}</span>
                  </td>
                  <td>
                    {o.customer}
                    <br />
                    <span className="muted">{o.email}</span>
                  </td>
                  <td>{o.destination}</td>
                  <td>
                    {o.itemCount} {o.hasCustom && <span className="chip chip--mint">custom</span>}
                  </td>
                  <td className="num">{money(o.totalCents, o.currency)}</td>
                  <td>
                    <StatusChip status={o.status} />
                  </td>
                  <td>{o.shipmentStatus ? <span className="chip">{pretty(o.shipmentStatus)}</span> : <span className="muted">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && data.total > data.pageSize && (
        <div className="row">
          <button type="button" className="btn btn--sm" disabled={page === 1} onClick={() => setPage(page - 1)}>
            ← Newer
          </button>
          <span className="label">
            Page {page} of {Math.ceil(data.total / data.pageSize)}
          </span>
          <button type="button" className="btn btn--sm" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>
            Older →
          </button>
        </div>
      )}
    </div>
  )
}

export function OrderDetail() {
  const { id } = useParams()
  const qc = useQueryClient()
  const key = ['admin', 'order', id]
  const { data, isLoading, error } = useQuery({ queryKey: key, queryFn: () => api<Detail>(`/api/admin/orders/${id}`) })
  const refresh = () => {
    qc.invalidateQueries({ queryKey: key })
    qc.invalidateQueries({ queryKey: ['admin', 'orders'] })
    qc.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
  }
  const setStatus = useMutation({
    mutationFn: (status: string) => api<Detail>(`/api/admin/orders/${id}/status`, { method: 'PATCH', body: { status } }),
    onSuccess: refresh,
  })

  if (isLoading) return <Loading />
  if (!data) return <ErrorNotice error={error ?? new Error()} />
  const o = data.order

  return (
    <div className="stack" style={{ '--gap': '22px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <Link to="/admin/orders" className="label">
            ← Orders
          </Link>
          <h1>{o.orderNumber}</h1>
          <p className="muted">
            Placed {shortDate(o.createdAt)} by {data.customerName} · {o.email}
          </p>
        </div>
        <StatusChip status={o.status} />
      </div>

      <div className="admin-cols">
        <div className="stack">
          <section className="card stack">
            <h2>Items to make &amp; pack</h2>
            <ul className="mini-lines mini-lines--lg">
              {o.items.map((i) => (
                <li key={i.id}>
                  <LineArt line={{ design: i.design, imageUrl: i.imageUrl, name: i.productName }} />
                  <div>
                    <b>
                      {i.quantity} × {i.productName}
                    </b>
                    <span className="muted">{i.variantName}</span>
                    <code>{i.sku}</code>
                    {i.isCustom && i.design && (
                      <span className="label muted">
                        {i.design.species} · body {i.design.body} · belly {i.design.belly} · {i.design.eyes} eyes · {i.design.mouth} mouth · {i.design.accessory}
                        {i.design.accessory !== 'none' && ` ${i.design.accessoryColor}`}
                      </span>
                    )}
                  </div>
                  <span>{money(i.totalCents, o.currency)}</span>
                </li>
              ))}
            </ul>
            {o.customerNote && (
              <p className="notice notice--info">
                <b>Customer note:</b> {o.customerNote}
              </p>
            )}
          </section>

          <ShippingPanel detail={data} onChange={refresh} />
        </div>

        <div className="stack">
          <section className="card stack">
            <h2>Update status</h2>
            {data.nextStatuses.length === 0 ? (
              <p className="muted">This order is {statusLabel(o.status).toLowerCase()} — nothing more to do.</p>
            ) : (
              <div className="row">
                {data.nextStatuses.map((s) => (
                  <button
                    key={s}
                    type="button"
                    className={`btn btn--sm ${s === 'cancelled' || s === 'refunded' ? '' : 'btn--lemon'}`}
                    disabled={setStatus.isPending}
                    onClick={() => {
                      const risky = s === 'cancelled' || s === 'refunded'
                      if (!risky || window.confirm(`Mark ${o.orderNumber} as ${pretty(s)}? ${s === 'refunded' ? 'This only records the refund here — send the money back through your payment provider.' : 'Unshipped items go back into stock.'}`))
                        setStatus.mutate(s)
                    }}
                  >
                    Mark {pretty(s)}
                  </button>
                ))}
              </div>
            )}
            <ErrorNotice error={setStatus.error} />
            <p className="muted" style={{ fontSize: '0.875rem' }}>
              Shipped and delivered are set automatically as Chit Chats reports progress.
            </p>
          </section>

          <section className="card">
            <SummaryRows subtotal={o.subtotalCents} discount={o.discountCents} couponCode={o.couponCode} shipping={o.shippingCents} tax={o.taxCents} taxLabel={o.taxLabel} total={o.totalCents} currency={o.currency} />
            {data.payments.map((p, i) => (
              <p key={i} className="label muted" style={{ marginTop: 12 }}>
                {p.provider} payment · {pretty(p.status)} · {money(p.amountCents, o.currency)}
              </p>
            ))}
          </section>

          <section className="card stack" style={{ '--gap': '6px' } as React.CSSProperties}>
            <span className="label muted">Ship to</span>
            <address style={{ fontStyle: 'normal' }}>
              <b>{o.shipTo.name}</b>
              <br />
              {o.shipTo.address1}
              {o.shipTo.address2 && <>, {o.shipTo.address2}</>}
              <br />
              {o.shipTo.city} {o.shipTo.provinceCode} {o.shipTo.postalCode}
              <br />
              {o.shipTo.countryCode}
              {o.shipTo.phone && (
                <>
                  <br />
                  {o.shipTo.phone}
                </>
              )}
            </address>
            <span className="muted">Customer chose: {o.shippingMethodName}</span>
          </section>
        </div>
      </div>
    </div>
  )
}

/** The Chit Chats workflow for one order: create shipment → pick a rate → buy postage → label → tracking. */
function ShippingPanel({ detail, onChange }: { detail: Detail; onChange: () => void }) {
  const orderId = detail.order.id
  const status = useQuery({ queryKey: ['admin', 'shipping-status'], queryFn: () => api<{ configured: boolean; baseUrl: string; sandbox: boolean }>('/api/admin/shipping/status'), staleTime: 60_000 })
  const defaults = useQuery({ queryKey: ['admin', 'shipment-defaults', orderId], queryFn: () => api<Defaults>(`/api/admin/orders/${orderId}/shipment-defaults`) })
  const open = detail.shipments.find((s) => !['voided', 'delivered', 'exception', 'canceled', 'resolved'].includes(s.status))
  const past = detail.shipments.filter((s) => s !== open)
  const canShip = !['pending_payment', 'cancelled', 'refunded'].includes(detail.order.status)

  return (
    <section className="card stack">
      <div className="spread">
        <h2>Shipping · Chit Chats</h2>
        {status.data?.configured && <span className={`chip ${status.data.sandbox ? 'chip--lemon' : 'chip--mint'}`}>{status.data.sandbox ? 'Sandbox' : 'Live'}</span>}
      </div>

      {status.data && !status.data.configured ? (
        <div className="notice notice--info">
          <b>Chit Chats isn't connected yet.</b> Set <code>ChitChats__ClientId</code> and <code>ChitChats__AccessToken</code> on the API (from Chit Chats → Settings → Developer → API Access Tokens), then restart it. Until then you can still mark orders shipped by hand.
        </div>
      ) : open ? (
        <OpenShipment shipment={open} preferred={defaults.data?.preferredPostageType ?? null} onChange={onChange} />
      ) : canShip && defaults.data ? (
        <CreateShipment orderId={orderId} defaults={defaults.data} onChange={onChange} />
      ) : !canShip ? (
        <p className="muted">An order that is {statusLabel(detail.order.status).toLowerCase()} can't be shipped.</p>
      ) : (
        <Loading />
      )}

      {past.length > 0 && (
        <ul className="stack" style={{ '--gap': '6px' } as React.CSSProperties}>
          {past.map((s) => (
            <li key={s.id} className="label muted">
              Earlier shipment {s.chitChatsShipmentId} · {pretty(s.status)}
              {s.trackingCode && ` · ${s.trackingCode}`}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function CreateShipment({ orderId, defaults, onChange }: { orderId: number; defaults: Defaults; onChange: () => void }) {
  const [form, setForm] = useState({ packageType: defaults.packageType, weightGrams: defaults.weightGrams, sizeXCm: defaults.sizeXCm, sizeYCm: defaults.sizeYCm, sizeZCm: defaults.sizeZCm, insuranceRequested: false, signatureRequested: false, shipDate: 'today' })
  const create = useMutation({ mutationFn: () => api(`/api/admin/orders/${orderId}/shipments`, { method: 'POST', body: form }), onSuccess: onChange })
  const num = (key: 'weightGrams' | 'sizeXCm' | 'sizeYCm' | 'sizeZCm', label: string) => (
    <div className="field">
      <label htmlFor={`ship-${key}`}>{label}</label>
      <input id={`ship-${key}`} className="input" type="number" min="0.1" step="any" value={form[key]} onChange={(e) => setForm({ ...form, [key]: Number(e.target.value) })} required />
    </div>
  )
  return (
    <form
      className="stack"
      onSubmit={(e) => {
        e.preventDefault()
        create.mutate()
      }}
    >
      <p className="muted">Measure the packed parcel. Chit Chats will come back with the postage options and prices for this address.</p>
      <div className="form-grid form-grid--4">
        <div className="field">
          <label htmlFor="ship-type">Package</label>
          <select id="ship-type" className="select" value={form.packageType} onChange={(e) => setForm({ ...form, packageType: e.target.value })}>
            {defaults.packageTypes.map((t) => (
              <option key={t} value={t}>
                {pretty(t)}
              </option>
            ))}
          </select>
        </div>
        {num('weightGrams', 'Weight (g)')}
        <div className="field">
          <label htmlFor="ship-date">Drop-off date</label>
          <input id="ship-date" className="input" type="date" value={form.shipDate === 'today' ? '' : form.shipDate} onChange={(e) => setForm({ ...form, shipDate: e.target.value || 'today' })} aria-describedby="ship-date-hint" />
          <small id="ship-date-hint">Empty = today</small>
        </div>
        {num('sizeXCm', 'Length (cm)')}
        {num('sizeYCm', 'Width (cm)')}
        {num('sizeZCm', 'Height (cm)')}
      </div>
      <div className="row">
        <label className="check">
          <input type="checkbox" checked={form.insuranceRequested} onChange={(e) => setForm({ ...form, insuranceRequested: e.target.checked })} />
          Insurance
        </label>
        <label className="check">
          <input type="checkbox" checked={form.signatureRequested} onChange={(e) => setForm({ ...form, signatureRequested: e.target.checked })} />
          Signature on delivery
        </label>
      </div>
      <ErrorNotice error={create.error} />
      <div>
        <button className="btn btn--pink" disabled={create.isPending}>
          {create.isPending ? 'Asking Chit Chats…' : 'Create shipment & get rates'}
        </button>
      </div>
    </form>
  )
}

function OpenShipment({ shipment: s, preferred, onChange }: { shipment: Shipment; preferred: string | null; onChange: () => void }) {
  const [postageType, setPostageType] = useState(() => s.rates.find((r) => r.postageType === preferred)?.postageType ?? s.rates[0]?.postageType ?? '')
  const [labelError, setLabelError] = useState<unknown>(null)
  const act = (path: string, method = 'POST', body?: unknown) => api(`/api/admin/shipments/${s.id}${path}`, { method, body })
  const buy = useMutation({ mutationFn: () => act('/buy', 'POST', { postageType }), onSuccess: onChange })
  const sync = useMutation({ mutationFn: () => act('/sync'), onSuccess: onChange })
  const refund = useMutation({ mutationFn: () => act('/refund'), onSuccess: onChange })
  const remove = useMutation({ mutationFn: () => act('', 'DELETE'), onSuccess: onChange })
  const needsPostage = s.status === 'pending' || s.status === 'postage_purchase_failed'
  const chosen = s.rates.find((r) => r.postageType === postageType)

  const download = async (format: string) => {
    setLabelError(null)
    try {
      // Fetched with the staff token, then handed to the browser as a file.
      const response = await api<Response>(`/api/admin/shipments/${s.id}/label?format=${format}`, { raw: true })
      const url = URL.createObjectURL(await response.blob())
      const a = Object.assign(document.createElement('a'), { href: url, download: `label-${s.chitChatsShipmentId}.${format}` })
      a.click()
      setTimeout(() => URL.revokeObjectURL(url), 5000)
    } catch (e) {
      setLabelError(e)
    }
  }

  return (
    <div className="stack">
      <dl className="facts">
        <div>
          <dt>Chit Chats ID</dt>
          <dd>
            <code>{s.chitChatsShipmentId}</code>
          </dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>
            <span className="chip chip--sky">{pretty(s.status)}</span>
          </dd>
        </div>
        <div>
          <dt>Parcel</dt>
          <dd>
            {pretty(s.packageType)} · {s.weightGrams} g · {s.sizeXCm}×{s.sizeYCm}×{s.sizeZCm} cm
          </dd>
        </div>
        {s.postageType && (
          <div>
            <dt>Postage</dt>
            <dd>
              {pretty(s.postageType)}
              {s.postageCostCents != null && ` · ${money(s.postageCostCents)} CAD`}
            </dd>
          </div>
        )}
        {s.trackingCode && (
          <div>
            <dt>Tracking</dt>
            <dd>
              {s.trackingUrl ? (
                <a href={s.trackingUrl} target="_blank" rel="noreferrer">
                  {s.trackingCode}
                </a>
              ) : (
                s.trackingCode
              )}
            </dd>
          </div>
        )}
      </dl>

      {needsPostage ? (
        <>
          {s.status === 'postage_purchase_failed' && <p className="notice">The last postage purchase failed. Check your Chit Chats credit balance, then try again.</p>}
          {s.rates.length === 0 ? (
            <p className="notice">Chit Chats returned no rates for this parcel and address. Delete this shipment and check the address and dimensions.</p>
          ) : (
            <div className="option-list" role="radiogroup" aria-label="Postage rate">
              {s.rates.map((r) => (
                <label key={r.postageType} className="option">
                  <input type="radio" name="rate" checked={postageType === r.postageType} onChange={() => setPostageType(r.postageType)} />
                  <span>
                    <b>{r.description ?? pretty(r.postageType)}</b>
                    {r.postageType === preferred && <span className="chip chip--lemon" style={{ marginLeft: 8 }}>customer's choice</span>}
                    <br />
                    <span className="muted">{[r.deliveryTime, r.tracking].filter(Boolean).join(' · ')}</span>
                  </span>
                  <strong>${r.paymentAmount}</strong>
                </label>
              ))}
            </div>
          )}
          <div className="row">
            <button type="button" className="btn btn--pink" disabled={!chosen || buy.isPending} onClick={() => buy.mutate()}>
              {buy.isPending ? 'Buying postage…' : chosen ? `Buy postage · $${chosen.paymentAmount}` : 'Choose a rate'}
            </button>
            <button type="button" className="btn btn--quiet" disabled={remove.isPending} onClick={() => window.confirm('Delete this shipment from Chit Chats?') && remove.mutate()}>
              Delete shipment
            </button>
          </div>
        </>
      ) : (
        <div className="row">
          {s.hasLabel && (
            <>
              <button type="button" className="btn btn--lemon" onClick={() => download('pdf')}>
                Download label (PDF)
              </button>
              <button type="button" className="btn btn--sm" onClick={() => download('png')}>
                PNG
              </button>
              <button type="button" className="btn btn--sm" onClick={() => download('zpl')}>
                ZPL
              </button>
            </>
          )}
          <button type="button" className="btn btn--sm" disabled={sync.isPending} onClick={() => sync.mutate()}>
            {sync.isPending ? 'Syncing…' : 'Sync with Chit Chats'}
          </button>
          {s.status === 'ready' && (
            <button
              type="button"
              className="btn btn--quiet"
              disabled={refund.isPending}
              onClick={() => window.confirm('Refund this postage? The label is voided and can no longer be used.') && refund.mutate()}
            >
              Refund postage
            </button>
          )}
        </div>
      )}
      <ErrorNotice error={buy.error ?? sync.error ?? refund.error ?? remove.error ?? labelError} />
      {s.lastSyncedAt && <span className="label muted">Last synced {new Date(s.lastSyncedAt).toLocaleString('en-CA')}</span>}
    </div>
  )
}
