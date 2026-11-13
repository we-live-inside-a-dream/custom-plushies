import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, PageHead, ProductCard } from '../components/ui'
import { api } from '../lib/api'
import type { Category, Paged, ProductSummary } from '../lib/types'
import { SHOWCASE } from './Home'

const SORTS = [
  ['', 'Newest'],
  ['price-asc', 'Price: low to high'],
  ['price-desc', 'Price: high to low'],
  ['rating', 'Top rated'],
  ['name', 'Name'],
]

export function Shop() {
  const [params, setParams] = useSearchParams()
  const category = params.get('category') ?? ''
  const tag = params.get('tag') ?? ''
  const sort = params.get('sort') ?? ''
  const q = params.get('q') ?? ''
  const [search, setSearch] = useState(q)

  const set = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  // Search as you type, without a request per keystroke.
  useEffect(() => {
    if (search === q) return
    const timer = setTimeout(() => set('q', search.trim()), 300)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])
  useEffect(() => setSearch(q), [q])

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => api<Category[]>('/api/categories'), staleTime: 5 * 60_000 })
  const products = useQuery({
    queryKey: ['products', { category, tag, sort, q }],
    queryFn: ({ signal }) => api<Paged<ProductSummary>>(`/api/products?${new URLSearchParams({ category, tag, sort, q, pageSize: '48' })}`, { signal }),
    placeholderData: keepPreviousData,
  })

  const flat = (categories.data ?? []).flatMap((c) => [c, ...c.children])
  const active = flat.find((c) => c.slug === category)
  const filtered = !!(category || tag || q)

  return (
    <>
      <PageHead kicker={tag ? `#${tag}` : 'The shelf'} title={active?.title ?? (tag === 'gift' ? 'Gifts' : tag === 'bestseller' ? 'Bestsellers' : 'All plushies')}>
        {active?.description ?? 'Ready-made friends, in stock and waiting. Each colour is its own little batch — when it’s gone, it’s gone.'}
      </PageHead>

      <div className="wrap shop">
        <div className="shop__bar">
          <div className="pills" role="group" aria-label="Category">
            <button type="button" className="pill" aria-pressed={!category} onClick={() => set('category', '')}>
              Everything
            </button>
            {flat
              .filter((c) => c.productCount > 0 || c.children.length > 0)
              .map((c) => (
                <button key={c.id} type="button" className="pill" aria-pressed={category === c.slug} onClick={() => set('category', c.slug)}>
                  {c.title}
                </button>
              ))}
          </div>
          <div className="shop__tools">
            <input
              className="input"
              type="search"
              placeholder="Search plushies…"
              aria-label="Search plushies"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <select className="select" aria-label="Sort by" value={sort} onChange={(e) => set('sort', e.target.value)}>
              {SORTS.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        </div>

        <p className="label muted" aria-live="polite">
          {products.data ? `${products.data.total} plushie${products.data.total === 1 ? '' : 's'}` : ' '}
          {filtered && (
            <>
              {' · '}
              <Link to="/shop">Clear filters</Link>
            </>
          )}
        </p>

        <ErrorNotice error={products.error} />
        {products.isLoading ? (
          <div className="product-grid">
            {Array.from({ length: 8 }, (_, i) => (
              <div key={i} className="skeleton" style={{ aspectRatio: '3 / 4' }} />
            ))}
          </div>
        ) : products.data?.items.length === 0 ? (
          <div className="empty">
            <Plushie design={{ ...SHOWCASE[3], eyes: 'sleepy' }} />
            <h2>Nobody here by that name</h2>
            <p>We couldn't find a plushie matching that. Try a different search — or make exactly the one you're picturing.</p>
            <div className="row">
              <Link to="/shop" className="btn">
                Clear filters
              </Link>
              <Link to="/build" className="btn btn--pink">
                Build your own
              </Link>
            </div>
          </div>
        ) : (
          <div className="product-grid" style={{ opacity: products.isPlaceholderData ? 0.6 : 1 }}>
            {products.data?.items.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
        )}
      </div>
    </>
  )
}
