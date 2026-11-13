import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { ErrorNotice, Icon, Loading, Price, ProductArt, ProductCard, QtyStepper, Stars, variantTint } from '../components/ui'
import { api, ApiError, shortDate } from '../lib/api'
import type { ProductDetail } from '../lib/types'
import { useAuth, useCartActions, useToast, useWishlist } from '../state/session'
import { NotFound } from './Misc'

export function ProductPage() {
  const { slug = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const { data, isLoading, error } = useQuery({ queryKey: ['product', slug], queryFn: () => api<ProductDetail>(`/api/products/${slug}`) })
  const { add } = useCartActions()
  const toast = useToast()
  const wishlist = useWishlist()
  const { customer } = useAuth()
  const [quantity, setQuantity] = useState(1)
  const [photo, setPhoto] = useState(0)

  const product = data?.summary
  const variant =
    product?.variants.find((v) => String(v.id) === params.get('v')) ?? product?.variants.find((v) => v.stock > 0) ?? product?.variants[0]

  useEffect(() => {
    setQuantity(1)
    setPhoto(0)
  }, [variant?.id])
  useEffect(() => {
    if (product) document.title = `${product.name} — Stitchling`
    return () => void (document.title = 'Stitchling — custom plushies, stitched to order')
  }, [product])

  if (isLoading) return <Loading label="Loading plushie" />
  if (error instanceof ApiError && error.status === 404) return <NotFound />
  if (!product || !variant || !data) return <div className="wrap section"><ErrorNotice error={error ?? new Error()} /></div>

  const soldOut = variant.stock === 0
  const wished = wishlist.has(product.id)
  const typeLabel = variant.typeTitle

  return (
    <div className="wrap product">
      <nav className="crumbs label" aria-label="Breadcrumb">
        <Link to="/shop">Shop</Link>
        {product.category && (
          <>
            <span aria-hidden="true">/</span>
            <Link to={`/shop?category=${product.categorySlug}`}>{product.category}</Link>
          </>
        )}
        <span aria-hidden="true">/</span>
        <span aria-current="page">{product.name}</span>
      </nav>

      <div className="product__grid">
        <div className="product__gallery">
          <div className="product__stage" style={{ '--tint': variantTint(variant) } as React.CSSProperties}>
            {product.promoPercent > 0 && variant.salePriceCents < variant.priceCents && <span className="chip chip--pink product__flag">−{product.promoPercent}% today</span>}
            <button type="button" className="squish" aria-label="Squish the plushie">
              <ProductArt key={variant.id} design={variant.design} imageUrl={variant.images[photo]?.url} alt={`${product.name} in ${variant.value}`} animated />
            </button>
          </div>
          {variant.images.length > 1 && (
            <div className="product__thumbs">
              {variant.images.map((img, i) => (
                <button key={img.id} type="button" aria-pressed={i === photo} aria-label={`Photo ${i + 1}`} onClick={() => setPhoto(i)}>
                  <img src={img.thumbnailUrl ?? img.url} alt="" />
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="product__info">
          {product.brand && <span className="label muted">{product.brand}</span>}
          <h1>{product.name}</h1>
          <a href="#reviews" className="product__rating">
            <Stars rating={product.rating} count={product.reviewCount} />
          </a>
          <Price cents={variant.salePriceCents} compareAt={variant.priceCents} currency={product.currency} size="lg" />
          <p className="product__desc">{data.description}</p>

          <fieldset className="variant-picker">
            <legend>
              {typeLabel}: <b>{variant.value}</b>
            </legend>
            <div className="variant-picker__options">
              {product.variants.map((v) => (
                <button
                  key={v.id}
                  type="button"
                  className={`variant ${v.typeKind === 'color' ? 'variant--color' : ''} ${v.stock === 0 ? 'is-out' : ''}`}
                  aria-pressed={v.id === variant.id}
                  onClick={() => setParams({ v: String(v.id) }, { replace: true })}
                  title={v.stock === 0 ? `${v.value} — sold out` : v.value}
                >
                  {v.typeKind === 'color' && <span className="variant__dot" style={{ background: variantTint(v) }} />}
                  <span>{v.value}</span>
                </button>
              ))}
            </div>
          </fieldset>

          <div className="product__buy">
            <QtyStepper value={quantity} max={Math.min(20, Math.max(1, variant.stock))} onChange={setQuantity} label="Quantity" />
            <button
              type="button"
              className="btn btn--pink btn--lg"
              style={{ flex: 1 }}
              disabled={soldOut || add.isPending}
              onClick={() =>
                add.mutate(
                  { productVariantId: variant.id, quantity },
                  { onSuccess: () => toast(`${product.name} (${variant.value}) is in your cart.`, { link: { to: '/cart', label: 'View cart' } }) },
                )
              }
            >
              {soldOut ? 'Sold out' : add.isPending ? 'Adding…' : 'Add to cart'}
            </button>
            <button
              type="button"
              className="icon-btn"
              aria-pressed={wished}
              aria-label={wished ? 'Remove from wishlist' : 'Add to wishlist'}
              onClick={() => (customer ? wishlist.toggle.mutate(product.id) : toast('Sign in to save favourites.', { link: { to: '/login', label: 'Sign in' } }))}
            >
              <Icon.heart filled={wished} />
            </button>
          </div>
          <p className="product__stock label" aria-live="polite">
            {soldOut ? (
              <>This colour is sold out — <Link to="/build">build one like it</Link></>
            ) : variant.stock <= 3 ? (
              <span className="product__low">Only {variant.stock} left in this batch</span>
            ) : (
              'In stock · ready to ship'
            )}
          </p>

          {data.information && (
            <aside className="care-label" aria-label="Care label">
              <span className="care-label__title">Care label</span>
              <p>{data.information}</p>
              <div className="care-label__icons" aria-hidden="true">
                <svg viewBox="0 0 32 32"><path d="M4 10l3 14h18l3-14-4 3-4-3-4 3-4-3-4 3z" /><text x="16" y="22" textAnchor="middle">30</text></svg>
                <svg viewBox="0 0 32 32"><path d="M16 5l12 21H4z" /><path d="M9 27L23 9M23 27L9 9" /></svg>
                <svg viewBox="0 0 32 32"><rect x="5" y="5" width="22" height="22" rx="2" /><path d="M10 12h12M10 16h12M10 20h12" /></svg>
                <svg viewBox="0 0 32 32"><circle cx="16" cy="16" r="11" /><path d="M11 21c3-8 7-8 10 0" /></svg>
              </div>
              <span className="care-label__sku">SKU {variant.sku}</span>
            </aside>
          )}
        </div>
      </div>

      <section id="reviews" className="reviews">
        <div className="section__head">
          <h2>What the humans say</h2>
          <Stars rating={product.rating} count={product.reviewCount} />
        </div>
        <div className="reviews__grid">
          <div className="stack">
            {data.reviews.length === 0 && <p className="muted">No reviews yet. Be the first to tell us how the hugs are.</p>}
            {data.reviews.map((r) => (
              <article key={r.id} className="review card">
                <div className="spread">
                  <Stars rating={r.rating} />
                  <span className="label muted">{shortDate(r.createdAt)}</span>
                </div>
                {r.comment && <p>{r.comment}</p>}
                <span className="review__author">— {r.author}</span>
              </article>
            ))}
          </div>
          <ReviewForm slug={slug} />
        </div>
      </section>

      {data.related.length > 0 && (
        <section className="section" style={{ paddingBottom: 0 }}>
          <div className="section__head">
            <h2>They'd get along with</h2>
          </div>
          <div className="product-grid">
            {data.related.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}

function ReviewForm({ slug }: { slug: string }) {
  const { customer } = useAuth()
  const qc = useQueryClient()
  const toast = useToast()
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const save = useMutation({
    mutationFn: () => api(`/api/products/${slug}/reviews`, { method: 'POST', body: { rating, comment } }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['product', slug] })
      toast('Thanks! Your review is up.')
      setComment('')
    },
  })

  if (!customer)
    return (
      <div className="patch review-form">
        <h3>Own one?</h3>
        <p>Sign in to leave a review and help the next person choose.</p>
        <Link to="/login" className="btn btn--lemon">
          Sign in to review
        </Link>
      </div>
    )

  return (
    <form
      className="patch review-form"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <h3>Leave a review</h3>
      <fieldset className="rate">
        <legend className="sr-only">Your rating</legend>
        {[1, 2, 3, 4, 5].map((n) => (
          <label key={n} className={n <= rating ? 'is-on' : ''}>
            <input type="radio" name="rating" value={n} checked={rating === n} onChange={() => setRating(n)} className="sr-only" />
            <Icon.star />
            <span className="sr-only">
              {n} star{n === 1 ? '' : 's'}
            </span>
          </label>
        ))}
      </fieldset>
      <textarea className="textarea" placeholder="How soft are we talking?" aria-label="Your review" maxLength={2000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <ErrorNotice error={save.error} />
      <button className="btn btn--lemon" disabled={rating === 0 || save.isPending}>
        {save.isPending ? 'Posting…' : rating === 0 ? 'Pick a star rating' : 'Post review'}
      </button>
    </form>
  )
}
