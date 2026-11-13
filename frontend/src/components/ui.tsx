import { useId, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, money } from '../lib/api'
import type { OrderStatus, PlushieDesign, ProductSummary, Variant } from '../lib/types'
import { useAuth, useToast, useWishlist } from '../state/session'
import { Plushie } from './Plushie'

// ---- Icons (inline so they inherit colour and need no icon font) ------------------------------
const stroke = { fill: 'none', stroke: 'currentColor', strokeWidth: 2.4, strokeLinecap: 'round', strokeLinejoin: 'round' } as const
export const Icon = {
  cart: () => (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...stroke}>
      <path d="M3 4h2.5l2.2 11h10.2l2.1-8H7" />
      <circle cx="9.5" cy="19.5" r="1.4" />
      <circle cx="17" cy="19.5" r="1.4" />
    </svg>
  ),
  heart: ({ filled = false }: { filled?: boolean }) => (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...stroke} fill={filled ? 'currentColor' : 'none'}>
      <path d="M12 20s-7.5-4.4-7.5-10A4.3 4.3 0 0 1 12 7.4 4.3 4.3 0 0 1 19.5 10c0 5.6-7.5 10-7.5 10z" />
    </svg>
  ),
  user: () => (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...stroke}>
      <circle cx="12" cy="8.5" r="3.8" />
      <path d="M4.5 20c1.2-3.6 4-5.2 7.5-5.2s6.3 1.6 7.5 5.2" />
    </svg>
  ),
  arrow: () => (
    <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" {...stroke} strokeWidth={2.8}>
      <path d="M5 12h14M13 6l6 6-6 6" />
    </svg>
  ),
  scissors: () => (
    <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" {...stroke}>
      <circle cx="6" cy="6.5" r="2.6" />
      <circle cx="6" cy="17.5" r="2.6" />
      <path d="M8.2 8l11.8 9M8.2 16L20 7" />
    </svg>
  ),
  star: () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 2.5l2.9 6.2 6.6.8-4.9 4.6 1.3 6.6L12 17.4l-5.9 3.3 1.3-6.6L2.5 9.5l6.6-.8z" fill="currentColor" stroke="var(--ink)" strokeWidth="1.8" strokeLinejoin="round" />
    </svg>
  ),
  menu: () => (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...stroke} strokeWidth={2.8}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  ),
  close: () => (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...stroke} strokeWidth={2.8}>
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  ),
}

export function Stars({ rating, count }: { rating: number; count?: number }) {
  const label = count === 0 ? 'No reviews yet' : `Rated ${rating.toFixed(1)} out of 5${count ? ` from ${count} review${count === 1 ? '' : 's'}` : ''}`
  return (
    <span className="stars" role="img" aria-label={label}>
      {[1, 2, 3, 4, 5].map((n) => (
        <span key={n} className={rating >= n - 0.25 ? 'stars__on' : rating >= n - 0.75 ? 'stars__half' : 'stars__off'}>
          <Icon.star />
        </span>
      ))}
      {count !== undefined && <span className="stars__count">{count === 0 ? 'New' : `${rating.toFixed(1)} (${count})`}</span>}
    </span>
  )
}

export function Price({ cents, compareAt, currency, size = 'md' }: { cents: number; compareAt?: number; currency?: string; size?: 'md' | 'lg' }) {
  const onSale = compareAt !== undefined && compareAt > cents
  return (
    <span className={`price price--${size} ${onSale ? 'price--sale' : ''}`}>
      <span className="price__now">{money(cents, currency)}</span>
      {onSale && (
        <s className="price__was">
          <span className="sr-only">was </span>
          {money(compareAt, currency)}
        </s>
      )}
    </span>
  )
}

/** Shows a product's look: an uploaded photo if it has one, otherwise the drawn plushie. */
export function ProductArt({ design, imageUrl, alt, animated, look }: { design: PlushieDesign | null; imageUrl?: string | null; alt: string; animated?: boolean; look?: { x: number; y: number } }) {
  if (imageUrl) return <img className="product-art__photo" src={imageUrl} alt={alt} loading="lazy" />
  if (design) return <Plushie design={design} title={alt} animated={animated} look={look} />
  return <div className="product-art__none" role="img" aria-label={alt}><Icon.scissors /></div>
}

export const variantTint = (v?: { colorHex: string | null; design: PlushieDesign | null } | null) => v?.design?.body ?? v?.colorHex ?? '#FFD84D'

export function ProductCard({ product, variantId }: { product: ProductSummary; variantId?: number }) {
  const variant: Variant | undefined = product.variants.find((v) => v.id === variantId) ?? product.variants.find((v) => v.stock > 0) ?? product.variants[0]
  const wishlist = useWishlist()
  const { customer } = useAuth()
  const toast = useToast()
  const wished = wishlist.has(product.id)
  const isNew = product.tags.includes('new')

  return (
    <article className="product-card" style={{ '--tint': variantTint(variant) } as React.CSSProperties}>
      <Link to={`/shop/${product.slug}${variant ? `?v=${variant.id}` : ''}`} className="product-card__art" aria-label={product.name}>
        <ProductArt design={variant?.design ?? null} imageUrl={variant?.images[0]?.url} alt={`${product.name}, ${variant?.value ?? ''}`} />
      </Link>
      <div className="product-card__badges">
        {!product.inStock ? <span className="chip chip--ink">Sold out</span> : product.promoPercent > 0 ? <span className="chip chip--pink">−{product.promoPercent}%</span> : isNew ? <span className="chip chip--mint">New</span> : null}
      </div>
      <button
        type="button"
        className="icon-btn product-card__wish"
        aria-pressed={wished}
        aria-label={wished ? `Remove ${product.name} from wishlist` : `Add ${product.name} to wishlist`}
        onClick={() => {
          if (!customer) return toast('Sign in to save favourites.', { link: { to: '/login', label: 'Sign in' } })
          wishlist.toggle.mutate(product.id)
        }}
      >
        <Icon.heart filled={wished} />
      </button>
      <div className="product-card__body">
        <div className="spread" style={{ alignItems: 'flex-start', flexWrap: 'nowrap' }}>
          <div>
            <span className="label muted">{product.category ?? 'Plushie'}</span>
            <h3 className="product-card__name">
              <Link to={`/shop/${product.slug}`}>{product.name}</Link>
            </h3>
          </div>
          <Price cents={product.fromSalePriceCents} compareAt={product.fromPriceCents} currency={product.currency} />
        </div>
        <div className="spread">
          <Stars rating={product.rating} count={product.reviewCount} />
          <span className="swatches" aria-label={`${product.variants.length} options`}>
            {product.variants.slice(0, 5).map((v) => (
              <span key={v.id} className="swatches__dot" style={{ background: variantTint(v) }} title={v.value} />
            ))}
          </span>
        </div>
      </div>
    </article>
  )
}

export function QtyStepper({ value, max = 20, onChange, label }: { value: number; max?: number; onChange: (n: number) => void; label: string }) {
  return (
    <div className="qty" role="group" aria-label={label}>
      <button type="button" onClick={() => onChange(value - 1)} disabled={value <= 1} aria-label="Decrease quantity">
        −
      </button>
      <output aria-live="polite">{value}</output>
      <button type="button" onClick={() => onChange(value + 1)} disabled={value >= max} aria-label="Increase quantity">
        +
      </button>
    </div>
  )
}

type FieldProps = { label: string; error?: string; hint?: string; children: (props: { id: string; 'aria-invalid'?: true; 'aria-describedby'?: string }) => ReactNode; className?: string }
export function Field({ label, error, hint, children, className }: FieldProps) {
  const id = useId()
  const describedBy = error ? `${id}-e` : hint ? `${id}-h` : undefined
  return (
    <div className={`field ${className ?? ''}`}>
      <label htmlFor={id}>{label}</label>
      {children({ id, 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy })}
      {error ? (
        <span id={`${id}-e`} className="field__error">
          {error}
        </span>
      ) : hint ? (
        <small id={`${id}-h`}>{hint}</small>
      ) : null}
    </div>
  )
}

export function ErrorNotice({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <div className="notice" role="alert">
      {error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'}
    </div>
  )
}

const STATUS: Record<string, { label: string; tone: string }> = {
  pending_payment: { label: 'Awaiting payment', tone: 'muted' },
  paid: { label: 'Paid', tone: 'lemon' },
  in_production: { label: 'Being stitched', tone: 'sky' },
  shipped: { label: 'On its way', tone: 'pink' },
  delivered: { label: 'Delivered', tone: 'mint' },
  cancelled: { label: 'Cancelled', tone: 'muted' },
  refunded: { label: 'Refunded', tone: 'muted' },
}
export const statusLabel = (s: string) => STATUS[s]?.label ?? s.replace(/_/g, ' ')
export function StatusChip({ status }: { status: OrderStatus | string }) {
  return <span className={`chip chip--${STATUS[status]?.tone ?? 'muted'}`}>{statusLabel(status)}</span>
}

/** A ticker of repeated phrases. The copy is doubled so the loop is seamless; screen readers get it once. */
export function Marquee({ items, className = '' }: { items: string[]; className?: string }) {
  const run = (hidden: boolean) => (
    <div className="marquee__run" aria-hidden={hidden || undefined}>
      {items.map((t, i) => (
        <span key={i}>
          {t}
          <Icon.scissors />
        </span>
      ))}
    </div>
  )
  return (
    <div className={`marquee ${className}`}>
      {run(false)}
      {run(true)}
      {run(true)}
    </div>
  )
}

/** Circular text sticker. */
export function Sticker({ text, className = '', children }: { text: string; className?: string; children?: ReactNode }) {
  const id = useId()
  return (
    <div className={`sticker ${className}`} aria-hidden="true">
      <svg viewBox="0 0 120 120">
        <defs>
          <path id={id} d="M60 60 m-44 0 a44 44 0 1 1 88 0 a44 44 0 1 1 -88 0" />
        </defs>
        <circle cx="60" cy="60" r="57" fill="currentColor" stroke="var(--ink)" strokeWidth="3" />
        <text className="sticker__text">
          <textPath href={`#${id}`}>{text}</textPath>
        </text>
      </svg>
      <div className="sticker__center">{children}</div>
    </div>
  )
}

export function PageHead({ kicker, title, children }: { kicker?: string; title: string; children?: ReactNode }) {
  return (
    <header className="page-head">
      <div className="wrap">
        {kicker && <span className="chip chip--lemon">{kicker}</span>}
        <h1>{title}</h1>
        {children && <p className="page-head__lede">{children}</p>}
      </div>
    </header>
  )
}

export function Loading({ label = 'Loading' }: { label?: string }) {
  return (
    <div className="loading" role="status">
      <span className="loading__dot" />
      <span className="loading__dot" />
      <span className="loading__dot" />
      <span className="sr-only">{label}…</span>
    </div>
  )
}
