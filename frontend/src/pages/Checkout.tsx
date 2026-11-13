import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { ErrorNotice, Field, Loading, PageHead } from '../components/ui'
import { api, ApiError, money } from '../lib/api'
import { COUNTRIES, regionsFor } from '../lib/places'
import type { Address, Order, ShippingOption, Totals } from '../lib/types'
import { useAuth, useCart } from '../state/session'
import { LineArt, SummaryRows } from './Cart'

export type AddressForm = { recipientName: string; address1: string; address2: string; city: string; provinceCode: string; postalCode: string; countryCode: string; phone: string }
export const emptyAddress: AddressForm = { recipientName: '', address1: '', address2: '', city: '', provinceCode: '', postalCode: '', countryCode: 'CA', phone: '' }

/** The address fields, shared by checkout and the address book. `errors` is keyed by field name. */
export function AddressFields({ value, onChange, errors = {} }: { value: AddressForm; onChange: (a: AddressForm) => void; errors?: Record<string, string> }) {
  const set = (patch: Partial<AddressForm>) => onChange({ ...value, ...patch })
  const regions = regionsFor(value.countryCode)
  return (
    <div className="form-grid">
      <Field label="Full name" error={errors.recipientName} className="span-2">
        {(p) => <input {...p} className="input" autoComplete="name" value={value.recipientName} onChange={(e) => set({ recipientName: e.target.value })} required />}
      </Field>
      <Field label="Country" error={errors.countryCode} className="span-2">
        {(p) => (
          <select {...p} className="select" autoComplete="country" value={value.countryCode} onChange={(e) => set({ countryCode: e.target.value, provinceCode: '' })}>
            {COUNTRIES.map(([code, name]) => (
              <option key={code} value={code}>
                {name}
              </option>
            ))}
          </select>
        )}
      </Field>
      <Field label="Address" error={errors.address1} className="span-2">
        {(p) => <input {...p} className="input" autoComplete="address-line1" value={value.address1} onChange={(e) => set({ address1: e.target.value })} required />}
      </Field>
      <Field label="Apartment, unit, etc. (optional)" className="span-2">
        {(p) => <input {...p} className="input" autoComplete="address-line2" value={value.address2} onChange={(e) => set({ address2: e.target.value })} />}
      </Field>
      <Field label="City" error={errors.city}>
        {(p) => <input {...p} className="input" autoComplete="address-level2" value={value.city} onChange={(e) => set({ city: e.target.value })} required />}
      </Field>
      <Field label={value.countryCode === 'US' ? 'State' : value.countryCode === 'CA' ? 'Province' : 'Region (optional)'} error={errors.provinceCode}>
        {(p) =>
          regions ? (
            <select {...p} className="select" autoComplete="address-level1" value={value.provinceCode} onChange={(e) => set({ provinceCode: e.target.value })} required>
              <option value="">Choose…</option>
              {regions.map(([code, name]) => (
                <option key={code} value={code}>
                  {name}
                </option>
              ))}
            </select>
          ) : (
            <input {...p} className="input" autoComplete="address-level1" maxLength={10} value={value.provinceCode} onChange={(e) => set({ provinceCode: e.target.value })} />
          )
        }
      </Field>
      <Field label={value.countryCode === 'US' ? 'ZIP code' : 'Postal code'} error={errors.postalCode}>
        {(p) => <input {...p} className="input" autoComplete="postal-code" value={value.postalCode} onChange={(e) => set({ postalCode: e.target.value })} required={!!regions} />}
      </Field>
      <Field label="Phone (for the courier, optional)" error={errors.phone}>
        {(p) => <input {...p} className="input" type="tel" autoComplete="tel" value={value.phone} onChange={(e) => set({ phone: e.target.value })} />}
      </Field>
    </div>
  )
}

export const toForm = (a: Address): AddressForm => ({
  recipientName: a.recipientName, address1: a.address1, address2: a.address2 ?? '', city: a.city,
  provinceCode: a.provinceCode ?? '', postalCode: a.postalCode ?? '', countryCode: a.countryCode, phone: a.phone ?? '',
})

export function Checkout() {
  const { customer, ready } = useAuth()
  const { data: cart, isLoading } = useCart()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [address, setAddress] = useState<AddressForm>(emptyAddress)
  const [savedId, setSavedId] = useState<number | 'new' | null>(null)
  const [methodId, setMethodId] = useState<number | null>(null)
  const [note, setNote] = useState('')
  const [paymentToken, setPaymentToken] = useState('tok_test_ok')
  const [saveAddress, setSaveAddress] = useState(true)

  const addresses = useQuery({ queryKey: ['addresses'], queryFn: () => api<Address[]>('/api/account/addresses'), enabled: !!customer })

  // Start from the default saved address, or the customer's name on a blank form.
  useEffect(() => {
    if (savedId !== null || !addresses.data || !customer) return
    const preferred = addresses.data[0]
    if (preferred) {
      setSavedId(preferred.id)
      setAddress(toForm(preferred))
    } else {
      setSavedId('new')
      setAddress({ ...emptyAddress, recipientName: `${customer.firstName} ${customer.lastName}` })
    }
  }, [addresses.data, customer, savedId])

  const country = address.countryCode
  const cartKey = `${cart?.subtotalCents}-${cart?.discountCents}`
  const methods = useQuery({
    queryKey: ['shipping-methods', country, cartKey],
    queryFn: () => api<ShippingOption[]>(`/api/checkout/shipping-methods?country=${country}`),
    enabled: !!customer && !!cart?.items.length,
  })
  // Keep a valid method selected as the destination changes.
  useEffect(() => {
    if (!methods.data) return
    if (!methods.data.some((m) => m.id === methodId)) setMethodId(methods.data[0]?.id ?? null)
  }, [methods.data, methodId])

  const validMethod = methods.data?.some((m) => m.id === methodId) ? methodId : null
  const totals = useQuery({
    queryKey: ['totals', country, address.provinceCode, validMethod, cartKey],
    queryFn: () => api<Totals>(`/api/checkout/totals?${new URLSearchParams({ country, province: address.provinceCode, ...(validMethod ? { shippingMethodId: String(validMethod) } : {}) })}`),
    enabled: !!customer && !!cart?.items.length,
  })

  const place = useMutation({
    mutationFn: () =>
      api<Order>('/api/checkout', {
        method: 'POST',
        body: { address, shippingMethodId: validMethod ?? 0, note, paymentToken, saveAddress: savedId === 'new' && saveAddress },
      }),
    onSuccess: (order) => {
      qc.invalidateQueries({ queryKey: ['cart'] })
      qc.invalidateQueries({ queryKey: ['orders'] })
      qc.invalidateQueries({ queryKey: ['addresses'] })
      qc.invalidateQueries({ queryKey: ['products'] })
      navigate(`/account/orders/${order.orderNumber}?placed=1`, { replace: true })
    },
  })

  if (!ready || isLoading) return <Loading label="Loading checkout" />
  if (!customer) return <Navigate to="/login?next=/checkout" replace />
  if (!cart || cart.items.length === 0) return place.isSuccess ? <Loading /> : <Navigate to="/cart" replace />

  const fieldErrors = Object.fromEntries(
    Object.entries(place.error instanceof ApiError ? place.error.fields : {}).map(([k, v]) => [k.replace(/^address\./, ''), v]),
  )
  const t = totals.data

  return (
    <>
      <PageHead title="Checkout" kicker="Nearly there" />
      <form
        className="wrap checkout"
        onSubmit={(e) => {
          e.preventDefault()
          place.mutate()
        }}
      >
        <div className="checkout__steps">
          <section className="card stack">
            <h2>
              <span className="builder-group__n">1</span> Where's it going?
            </h2>
            {addresses.data && addresses.data.length > 0 && (
              <div className="option-list" role="radiogroup" aria-label="Saved addresses">
                {addresses.data.map((a) => (
                  <label key={a.id} className="option">
                    <input type="radio" name="saved" checked={savedId === a.id} onChange={() => { setSavedId(a.id); setAddress(toForm(a)) }} />
                    <span>
                      <b>{a.recipientName}</b>
                      <br />
                      {a.address1}
                      {a.address2 ? `, ${a.address2}` : ''}, {a.city} {a.provinceCode} {a.postalCode}, {a.countryCode}
                    </span>
                  </label>
                ))}
                <label className="option">
                  <input type="radio" name="saved" checked={savedId === 'new'} onChange={() => { setSavedId('new'); setAddress({ ...emptyAddress, recipientName: `${customer.firstName} ${customer.lastName}` }) }} />
                  <span>
                    <b>Somewhere new</b>
                  </span>
                </label>
              </div>
            )}
            {savedId === 'new' && (
              <>
                <AddressFields value={address} onChange={setAddress} errors={fieldErrors} />
                <label className="check">
                  <input type="checkbox" checked={saveAddress} onChange={(e) => setSaveAddress(e.target.checked)} />
                  Save this address for next time
                </label>
              </>
            )}
          </section>

          <section className="card stack">
            <h2>
              <span className="builder-group__n">2</span> How should we send it?
            </h2>
            {methods.isLoading ? (
              <div className="skeleton" style={{ height: 76 }} />
            ) : methods.data?.length ? (
              <div className="option-list" role="radiogroup" aria-label="Shipping method">
                {methods.data.map((m) => (
                  <label key={m.id} className="option">
                    <input type="radio" name="shipping" checked={validMethod === m.id} onChange={() => setMethodId(m.id)} />
                    <span>
                      <b>{m.name}</b>
                      <br />
                      <span className="muted">{[m.description, m.estimatedDays].filter(Boolean).join(' · ')}</span>
                    </span>
                    <strong>{m.free ? 'Free' : money(m.priceCents, cart.currency)}</strong>
                  </label>
                ))}
              </div>
            ) : (
              <p className="notice">We can't ship to that country yet. Email us and we'll sort something out.</p>
            )}
            <Field label="Note for the stitchers (optional)">
              {(p) => <textarea {...p} className="textarea" style={{ minHeight: 70 }} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />}
            </Field>
          </section>

          <section className="card stack">
            <h2>
              <span className="builder-group__n">3</span> Payment
            </h2>
            <p className="notice notice--info">
              Test mode — no card is charged. This shop is using the built-in test payment gateway; pick how the test payment should respond.
            </p>
            <div className="option-list" role="radiogroup" aria-label="Test payment outcome">
              <label className="option">
                <input type="radio" name="pay" checked={paymentToken === 'tok_test_ok'} onChange={() => setPaymentToken('tok_test_ok')} />
                <span>
                  <b>Approved test card</b>
                  <br />
                  <span className="muted">The order is placed and marked paid.</span>
                </span>
              </label>
              <label className="option">
                <input type="radio" name="pay" checked={paymentToken === 'tok_test_declined'} onChange={() => setPaymentToken('tok_test_declined')} />
                <span>
                  <b>Declined test card</b>
                  <br />
                  <span className="muted">See what a customer sees when their bank says no.</span>
                </span>
              </label>
            </div>
          </section>
        </div>

        <aside className="checkout__summary card stack">
          <h2>Your order</h2>
          <ul className="mini-lines">
            {cart.items.map((l) => (
              <li key={l.id}>
                <LineArt line={l} />
                <div>
                  <b>{l.name}</b>
                  <span className="muted">
                    {l.variantName} × {l.quantity}
                  </span>
                </div>
                <span>{money(l.lineTotalCents, cart.currency)}</span>
              </li>
            ))}
          </ul>
          <SummaryRows
            subtotal={cart.subtotalCents}
            discount={cart.discountCents}
            couponCode={cart.coupon?.code}
            shipping={t && validMethod ? t.shippingCents : null}
            tax={t?.taxCents ?? 0}
            taxLabel={t?.taxLabel}
            total={t?.totalCents ?? cart.subtotalCents - cart.discountCents}
            currency={cart.currency}
          />
          <ErrorNotice error={place.error ?? totals.error} />
          <button className="btn btn--pink btn--lg btn--block" disabled={place.isPending || !validMethod}>
            {place.isPending ? 'Placing your order…' : `Place order · ${money(t?.totalCents ?? 0, cart.currency)}`}
          </button>
          <Link to="/cart" className="btn btn--quiet">
            Back to cart
          </Link>
        </aside>
      </form>
    </>
  )
}
