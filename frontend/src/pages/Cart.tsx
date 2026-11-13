import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, Loading, PageHead, Price, ProductArt, QtyStepper, variantTint } from '../components/ui'
import { ApiError, money } from '../lib/api'
import type { Cart as CartType, CartLine } from '../lib/types'
import { useCart, useCartActions } from '../state/session'
import { SHOWCASE } from './Home'

export function LineArt({ line }: { line: Pick<CartLine, 'design' | 'imageUrl' | 'name'> }) {
  return (
    <div className="line-art" style={{ '--tint': variantTint({ colorHex: null, design: line.design }) } as React.CSSProperties}>
      <ProductArt design={line.design} imageUrl={line.imageUrl} alt={line.name} />
    </div>
  )
}

export function Cart() {
  const { data: cart, isLoading, error } = useCart()
  const { update, remove } = useCartActions()

  if (isLoading) return <Loading label="Loading your cart" />
  if (error) return <div className="wrap section"><ErrorNotice error={error} /></div>

  if (!cart || cart.items.length === 0)
    return (
      <div className="wrap empty" style={{ minHeight: '56vh', alignContent: 'center' }}>
        <Plushie design={{ ...SHOWCASE[1], eyes: 'sleepy', accessory: 'none' }} />
        <h1>Your cart is empty</h1>
        <p>Nobody's in here yet. Adopt a ready-made friend or stitch up something entirely your own.</p>
        <div className="row">
          <Link to="/shop" className="btn">
            Browse the shelf
          </Link>
          <Link to="/build" className="btn btn--pink">
            Build a plushie
          </Link>
        </div>
      </div>
    )

  const blocked = cart.items.some((l) => !l.available)

  return (
    <>
      <PageHead title="Your cart" kicker={`${cart.itemCount} plushie${cart.itemCount === 1 ? '' : 's'}`} />
      <div className="wrap cart">
        <ul className="cart__lines">
          {cart.items.map((line) => (
            <li key={line.id} className={`cart-line card ${line.available ? '' : 'is-unavailable'}`}>
              <LineArt line={line} />
              <div className="cart-line__main">
                <div>
                  <h2 className="cart-line__name">{line.slug ? <Link to={`/shop/${line.slug}`}>{line.name}</Link> : line.name}</h2>
                  <p className="muted">
                    {line.variantName}
                    {line.embroideryText && <> · embroidered “{line.embroideryText}”</>}
                  </p>
                  {line.kind === 'custom' && <span className="chip chip--mint">Stitched to order</span>}
                  {!line.available && <p className="field__error">Sold out — remove this to check out.</p>}
                </div>
                <div className="cart-line__controls">
                  {line.available && (
                    <QtyStepper
                      value={line.quantity}
                      max={Math.min(20, line.maxQuantity ?? 20)}
                      label={`Quantity of ${line.name}`}
                      onChange={(quantity) => update.mutate({ id: line.id, quantity })}
                    />
                  )}
                  <button type="button" className="btn btn--quiet" onClick={() => remove.mutate(line.id)} aria-label={`Remove ${line.name}`}>
                    Remove
                  </button>
                </div>
              </div>
              <Price cents={line.lineTotalCents} compareAt={line.compareAtCents * line.quantity} currency={cart.currency} />
            </li>
          ))}
        </ul>

        <aside className="cart__summary card">
          <h2>Summary</h2>
          <CouponBox cart={cart} />
          <SummaryRows subtotal={cart.subtotalCents} discount={cart.discountCents} couponCode={cart.coupon?.code} currency={cart.currency} />
          <p className="muted" style={{ fontSize: '0.875rem' }}>
            Shipping and tax are worked out at checkout.
          </p>
          {blocked ? (
            <span className="btn btn--pink btn--lg btn--block" aria-disabled="true">
              Checkout
            </span>
          ) : (
            <Link to="/checkout" className="btn btn--pink btn--lg btn--block">
              Checkout
            </Link>
          )}
          <Link to="/shop" className="btn btn--quiet">
            Keep shopping
          </Link>
        </aside>
      </div>
    </>
  )
}

export function SummaryRows(p: { subtotal: number; discount: number; couponCode?: string | null; shipping?: number | null; tax?: number; taxLabel?: string | null; total?: number; currency: string }) {
  return (
    <dl className="summary">
      <div>
        <dt>Subtotal</dt>
        <dd>{money(p.subtotal, p.currency)}</dd>
      </div>
      {p.discount > 0 && (
        <div className="summary__discount">
          <dt>Discount{p.couponCode ? ` (${p.couponCode})` : ''}</dt>
          <dd>−{money(p.discount, p.currency)}</dd>
        </div>
      )}
      {p.shipping !== undefined && (
        <div>
          <dt>Shipping</dt>
          <dd>{p.shipping === null ? '—' : p.shipping === 0 ? 'Free' : money(p.shipping, p.currency)}</dd>
        </div>
      )}
      {p.tax !== undefined && p.tax > 0 && (
        <div>
          <dt>{p.taxLabel ?? 'Tax'}</dt>
          <dd>{money(p.tax, p.currency)}</dd>
        </div>
      )}
      <div className="summary__total">
        <dt>{p.total === undefined ? 'Total so far' : 'Total'}</dt>
        <dd>{money(p.total ?? p.subtotal - p.discount, p.currency)}</dd>
      </div>
    </dl>
  )
}

function CouponBox({ cart }: { cart: CartType }) {
  const { applyCoupon, removeCoupon } = useCartActions()
  const [code, setCode] = useState('')

  if (cart.coupon)
    return (
      <div className={`notice ${cart.coupon.problem ? '' : 'notice--ok'}`}>
        <div className="spread">
          <span>
            <b>{cart.coupon.code}</b> {cart.coupon.problem ? `— ${cart.coupon.problem}` : cart.coupon.description ? `— ${cart.coupon.description}` : 'applied'}
          </span>
          <button type="button" className="btn btn--quiet" onClick={() => removeCoupon.mutate()}>
            Remove
          </button>
        </div>
      </div>
    )

  return (
    <form
      className="coupon"
      onSubmit={(e) => {
        e.preventDefault()
        if (code.trim()) applyCoupon.mutate(code.trim(), { onSuccess: () => setCode('') })
      }}
    >
      <label htmlFor="coupon" className="sr-only">
        Coupon code
      </label>
      <input
        id="coupon"
        className="input"
        placeholder="Coupon code"
        value={code}
        onChange={(e) => setCode(e.target.value)}
        aria-invalid={applyCoupon.isError ? true : undefined}
        aria-describedby={applyCoupon.isError ? 'coupon-error' : undefined}
        autoCapitalize="characters"
      />
      <button className="btn" disabled={applyCoupon.isPending || !code.trim()}>
        Apply
      </button>
      {applyCoupon.isError && (
        <span id="coupon-error" className="field__error">
          {applyCoupon.error instanceof ApiError ? applyCoupon.error.message : 'Could not apply that code.'}
        </span>
      )}
    </form>
  )
}
