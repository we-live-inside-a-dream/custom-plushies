import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { Icon, Marquee, ProductCard, Sticker } from '../components/ui'
import { api, shortDate } from '../lib/api'
import type { Paged, PlushieDesign, PostSummary, ProductSummary, Promotion } from '../lib/types'
import { useCopy } from '../state/session'

const base = { belly: '#FFF1D6', eyes: 'button', mouth: 'smile', cheeks: true, accessory: 'none', accessoryColor: '#FF5DA2' }
export const SHOWCASE: PlushieDesign[] = [
  { ...base, species: 'bear', body: '#FF8A3D', accessory: 'bow', accessoryColor: '#5B2EFF' },
  { ...base, species: 'bunny', body: '#FF5DA2', mouth: 'tongue', accessory: 'flower', accessoryColor: '#FFD84D' },
  { ...base, species: 'frog', body: '#7BC950', belly: '#E9F7C8', accessory: 'crown', accessoryColor: '#FFD84D' },
  { ...base, species: 'axolotl', body: '#FFB3D1', eyes: 'sparkle', accessoryColor: '#FF5DA2' },
  { ...base, species: 'dino', body: '#36E0B0', accessoryColor: '#FF8A3D', accessory: 'scarf' },
  { ...base, species: 'cat', body: '#FFFFFF', belly: '#FFE3EE', mouth: 'kitty', eyes: 'sparkle', accessory: 'glasses' },
]

const FALLBACK = {
  hero_kicker: 'Custom plushies, stitched to order',
  hero_title: 'You dream it. We stitch it.',
  hero_subtitle: 'Pick a critter, choose the fabrics, add a name. We sew your one-of-a-kind plushie by hand.',
  hero_cta_primary: 'Build your plushie',
  hero_cta_secondary: 'Shop ready-made',
  builder_title: 'Make one nobody else has',
  builder_body: 'Six critters, sixteen fabrics and a wardrobe of tiny accessories.',
  steps_title: 'From doodle to doorstep',
}

/** Eyes follow the pointer anywhere on the page. Returns a -1..1 direction from the element's centre. */
function useLook<T extends Element>() {
  const ref = useRef<T>(null)
  const [look, setLook] = useState({ x: 0, y: 0 })
  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    let frame = 0
    const move = (e: PointerEvent) => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(() => {
        const box = ref.current?.getBoundingClientRect()
        if (!box) return
        const dx = e.clientX - (box.left + box.width / 2)
        const dy = e.clientY - (box.top + box.height * 0.4)
        const len = Math.hypot(dx, dy) || 1
        const reach = Math.min(1, len / 240)
        setLook({ x: (dx / len) * reach, y: (dy / len) * reach })
      })
    }
    window.addEventListener('pointermove', move)
    return () => {
      window.removeEventListener('pointermove', move)
      cancelAnimationFrame(frame)
    }
  }, [])
  return [ref, look] as const
}

export function Home() {
  const copy = useCopy('home', FALLBACK)
  const [index, setIndex] = useState(0)
  const [stage, look] = useLook<HTMLDivElement>()
  const featured = useQuery({ queryKey: ['products', 'home'], queryFn: () => api<Paged<ProductSummary>>('/api/products?pageSize=8') })
  const promos = useQuery({ queryKey: ['promotions'], queryFn: () => api<Promotion[]>('/api/promotions/home') })
  const posts = useQuery({ queryKey: ['posts', 3], queryFn: () => api<PostSummary[]>('/api/posts?take=3') })
  const design = SHOWCASE[index % SHOWCASE.length]
  const promo = promos.data?.[0]
  const [first, ...rest] = copy('hero_title').split('. ')

  return (
    <>
      <section className="hero">
        <div className="wrap hero__inner">
          <div className="hero__copy">
            <span className="chip chip--mint">{copy('hero_kicker')}</span>
            <h1 className="hero__title">
              {rest.length ? (
                <>
                  {first}.<br />
                  <em>{rest.join('. ')}</em>
                </>
              ) : (
                first
              )}
            </h1>
            <p className="hero__lede">{copy('hero_subtitle')}</p>
            <div className="row" style={{ '--gap': '16px' } as React.CSSProperties}>
              <Link to="/build" className="btn btn--pink btn--lg">
                {copy('hero_cta_primary')} <Icon.arrow />
              </Link>
              <Link to="/shop" className="btn btn--lg">
                {copy('hero_cta_secondary')}
              </Link>
            </div>
            <ul className="hero__facts">
              <li>
                <b>6</b> critters
              </li>
              <li>
                <b>16</b> fabrics
              </li>
              <li>
                <b>100%</b> hand-stitched
              </li>
            </ul>
          </div>

          <div className="hero__stage" ref={stage}>
            <div className="hero__blob" style={{ background: design.body === '#FFFFFF' ? 'var(--sky)' : `color-mix(in srgb, ${design.body} 55%, white)` }} />
            <span className="hero__spark hero__spark--a" />
            <span className="hero__spark hero__spark--b" />
            <span className="hero__spark hero__spark--c" />
            <button type="button" className="squish hero__plushie" onClick={() => setIndex((i) => i + 1)} aria-label="Squish the plushie to meet another one">
              <Plushie key={index} design={design} look={look} animated className="pop-in" />
            </button>
            <Sticker text="SQUISH ME • TAP TO SWAP • SQUISH ME • TAP TO SWAP • " className="hero__sticker" />
          </div>
        </div>
        <div className="pinked" style={{ '--c': 'var(--lemon)' } as React.CSSProperties} />
      </section>

      <Marquee className="band" items={['Stitched to order', 'Hug tested', 'No two alike', 'Squish guaranteed', 'Made by hand']} />

      {promo && promo.products.length > 0 && (
        <section className="section section--promo">
          <div className="wrap">
            <div className="promo-head">
              <Sticker text="LIMITED TIME • LIMITED TIME • LIMITED TIME • " className="promo-head__sticker">
                <b>−{promo.discountPercent}%</b>
              </Sticker>
              <div>
                <h2>{promo.title}</h2>
                <p>
                  {promo.description} <span className="label">Ends {shortDate(promo.endDate)}</span>
                </p>
              </div>
            </div>
            <div className="product-grid">
              {promo.products.map((p) => (
                <ProductCard key={p.id} product={p} variantId={p.variants.find((v) => v.salePriceCents < v.priceCents)?.id} />
              ))}
            </div>
          </div>
        </section>
      )}

      <section className="section">
        <div className="wrap">
          <div className="section__head">
            <h2>Fresh off the sewing table</h2>
            <Link to="/shop" className="btn btn--sm">
              See them all <Icon.arrow />
            </Link>
          </div>
          <div className="product-grid">
            {featured.isLoading
              ? Array.from({ length: 4 }, (_, i) => <div key={i} className="skeleton" style={{ aspectRatio: '3 / 4' }} />)
              : featured.data?.items.slice(0, 8).map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
          {featured.isError && <p className="notice">We couldn't load the shelf right now. Try refreshing.</p>}
        </div>
      </section>

      <section className="builder-cta">
        <div className="pinked pinked--flip" style={{ '--c': 'var(--paper)' } as React.CSSProperties} />
        <div className="wrap builder-cta__inner">
          <div className="builder-cta__lineup" aria-hidden="true">
            {[1, 2, 4].map((offset, i) => (
              <Plushie key={i} design={SHOWCASE[(index + offset) % SHOWCASE.length]} decorative />
            ))}
          </div>
          <div className="stack" style={{ '--gap': '20px', justifyItems: 'start' } as React.CSSProperties}>
            <span className="chip chip--ink">The plushie builder</span>
            <h2>{copy('builder_title')}</h2>
            <p className="builder-cta__body">{copy('builder_body')}</p>
            <div className="row">
              <Link to="/build" className="btn btn--ink btn--lg">
                Open the builder <Icon.arrow />
              </Link>
              <button type="button" className="btn" onClick={() => setIndex((i) => i + 1)}>
                Shuffle the line-up
              </button>
            </div>
          </div>
        </div>
        <div className="pinked" style={{ '--c': 'var(--paper)' } as React.CSSProperties} />
      </section>

      <section className="section">
        <div className="wrap">
          <div className="section__head">
            <h2>{copy('steps_title')}</h2>
          </div>
          <ol className="steps">
            {[
              ['You design', 'Choose a critter, fabrics, a face and a name in the builder — or adopt a ready-made friend.'],
              ['We cut & stitch', 'Each piece is cut from minky and sewn by hand in our workshop. Faces are embroidered, never glued.'],
              ['The squish test', 'Stuffed a handful at a time until it bounces back in about a second. Not before.'],
              ['Off it goes', 'Wrapped, boxed and sent with tracking. You get a link the moment it leaves.'],
            ].map(([title, body], i) => (
              <li key={title} className="steps__item patch">
                <span className="steps__n">{i + 1}</span>
                <h3>{title}</h3>
                <p>{body}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {posts.data && posts.data.length > 0 && (
        <section className="section section--tint">
          <div className="wrap">
            <div className="section__head">
              <h2>From the workshop</h2>
              <Link to="/journal" className="btn btn--sm">
                Read the journal <Icon.arrow />
              </Link>
            </div>
            <div className="post-grid">
              {posts.data.map((p) => (
                <Link key={p.id} to={`/journal/${p.slug}`} className="post-card card">
                  <span className="chip chip--sky">{p.type}</span>
                  <h3>{p.title}</h3>
                  <p className="muted">{p.excerpt}</p>
                  <span className="label">{shortDate(p.createdAt)}</span>
                </Link>
              ))}
            </div>
          </div>
        </section>
      )}
    </>
  )
}
