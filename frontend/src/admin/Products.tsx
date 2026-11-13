import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, Loading } from '../components/ui'
import { api, ApiError, money } from '../lib/api'
import type { BuilderOptions, PlushieDesign } from '../lib/types'
import { cents, dollars, uploadImage } from './shared'

type Row = { id: number; name: string; slug: string; active: boolean; brand: string | null; variantCount: number; stock: number; fromPriceCents: number; design: PlushieDesign | null; imageUrl: string | null }
type VariantForm = { id: number; productVariantTypeId: number; value: string; sku: string; priceCents: number; stock: number; weightGrams: number; colorHex: string | null; design: PlushieDesign | null; active: boolean; images: { id: number; url: string }[] }
type ProductForm = { id: number; name: string; slug: string | null; brandId: number | null; currency: string; active: boolean; description: string | null; information: string | null; tags: string[]; hsTariffCode: string | null; originCountry: string | null; categoryIds: number[]; primaryCategoryId: number | null; variants: VariantForm[] }
type Named = { id: number; title?: string; name?: string }

const DEFAULT_DESIGN: PlushieDesign = { species: 'bear', body: '#FF8A3D', belly: '#FFF1D6', eyes: 'button', mouth: 'smile', cheeks: true, accessory: 'none', accessoryColor: '#FF5DA2' }
const blankVariant = (typeId: number): VariantForm => ({ id: 0, productVariantTypeId: typeId, value: '', sku: '', priceCents: 3800, stock: 0, weightGrams: 320, colorHex: '#FF8A3D', design: { ...DEFAULT_DESIGN }, active: true, images: [] })

export function ProductsList() {
  const [q, setQ] = useState('')
  const { data, isLoading, error } = useQuery({ queryKey: ['admin', 'products', q], queryFn: () => api<Row[]>(`/api/admin/products?q=${encodeURIComponent(q)}`) })
  return (
    <div className="stack" style={{ '--gap': '20px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>Products</h1>
          <p className="muted">Ready-made plushies. Each variant has its own price, stock and look.</p>
        </div>
        <Link to="/admin/products/new" className="btn btn--pink">
          New product
        </Link>
      </div>
      <input className="input" style={{ maxWidth: 320 }} type="search" placeholder="Search by name or SKU" aria-label="Search products" value={q} onChange={(e) => setQ(e.target.value)} />
      <ErrorNotice error={error} />
      {isLoading ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Product</th>
                <th>Brand</th>
                <th className="num">Variants</th>
                <th className="num">In stock</th>
                <th className="num">From</th>
                <th>Listed</th>
              </tr>
            </thead>
            <tbody>
              {data?.map((p) => (
                <tr key={p.id}>
                  <td>
                    <Link to={`/admin/products/${p.id}`} className="table__product">
                      <span className="table__art">{p.imageUrl ? <img src={p.imageUrl} alt="" /> : p.design && <Plushie design={p.design} decorative />}</span>
                      <b>{p.name}</b>
                    </Link>
                  </td>
                  <td>{p.brand ?? '—'}</td>
                  <td className="num">{p.variantCount}</td>
                  <td className="num">{p.stock === 0 ? <span className="chip chip--pink">0</span> : p.stock}</td>
                  <td className="num">{money(p.fromPriceCents)}</td>
                  <td>
                    <span className={`chip ${p.active ? 'chip--mint' : 'chip--muted'}`}>{p.active ? 'Live' : 'Hidden'}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

const useLookup = <T,>(path: string) => useQuery({ queryKey: ['admin', path], queryFn: () => api<T[]>(path) })

export function ProductEditor() {
  const { id } = useParams()
  const isNew = id === 'new'
  const navigate = useNavigate()
  const qc = useQueryClient()
  const brands = useLookup<Named>('/api/admin/brands')
  const categories = useLookup<Named>('/api/admin/categories')
  const types = useLookup<Named & { type: string }>('/api/admin/variant-types')
  const builder = useQuery({ queryKey: ['builder'], queryFn: () => api<BuilderOptions>('/api/builder/options'), staleTime: Infinity })
  const existing = useQuery({ queryKey: ['admin', 'product', id], queryFn: () => api<ProductForm>(`/api/admin/products/${id}`), enabled: !isNew })
  const [form, setForm] = useState<ProductForm | null>(null)
  const [open, setOpen] = useState(0)

  useEffect(() => {
    if (form) return
    if (isNew && types.data)
      setForm({ id: 0, name: '', slug: null, brandId: null, currency: 'cad', active: true, description: '', information: '', tags: [], hsTariffCode: null, originCountry: 'CA', categoryIds: [], primaryCategoryId: null, variants: [blankVariant(types.data[0]?.id ?? 0)] })
    else if (existing.data) setForm(structuredClone(existing.data))
  }, [existing.data, types.data, isNew, form])

  const save = useMutation({
    mutationFn: () => api<ProductForm>(isNew ? '/api/admin/products' : `/api/admin/products/${id}`, { method: isNew ? 'POST' : 'PUT', body: form }),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['admin'] })
      qc.invalidateQueries({ queryKey: ['products'] })
      setForm(saved)
      if (isNew) navigate(`/admin/products/${saved.id}`, { replace: true })
    },
  })
  const remove = useMutation({
    mutationFn: () => api<{ retired: boolean }>(`/api/admin/products/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin'] })
      navigate('/admin/products')
    },
  })

  if (existing.error) return <ErrorNotice error={existing.error} />
  if (!form || !builder.data) return <Loading />
  const set = (patch: Partial<ProductForm>) => setForm({ ...form, ...patch })
  const setVariant = (index: number, patch: Partial<VariantForm>) => set({ variants: form.variants.map((v, i) => (i === index ? { ...v, ...patch } : v)) })
  const errors = save.error instanceof ApiError ? save.error.fields : {}
  const o = builder.data

  return (
    <form
      className="stack"
      style={{ '--gap': '22px' } as React.CSSProperties}
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <div className="admin-head">
        <div>
          <Link to="/admin/products" className="label">
            ← Products
          </Link>
          <h1>{isNew ? 'New product' : form.name || 'Product'}</h1>
        </div>
        <div className="row">
          {!isNew && form.slug && form.active && (
            <a className="btn btn--sm" href={`/shop/${form.slug}`} target="_blank" rel="noreferrer">
              View in shop
            </a>
          )}
          <button className="btn btn--pink" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save product'}
          </button>
        </div>
      </div>
      <ErrorNotice error={save.error ?? remove.error} />
      {save.isSuccess && !save.isPending && (
        <p className="notice notice--ok" role="status">
          Saved.
        </p>
      )}

      <div className="admin-cols">
        <section className="card stack">
          <h2>Details</h2>
          <div className="form-grid">
            <div className="field span-2">
              <label htmlFor="p-name">Name</label>
              <input id="p-name" className="input" value={form.name} onChange={(e) => set({ name: e.target.value })} aria-invalid={errors.name ? true : undefined} required />
              {errors.name && <span className="field__error">{errors.name}</span>}
            </div>
            <div className="field span-2">
              <label htmlFor="p-desc">Description</label>
              <textarea id="p-desc" className="textarea" value={form.description ?? ''} onChange={(e) => set({ description: e.target.value })} />
            </div>
            <div className="field span-2">
              <label htmlFor="p-info">Care label (materials, washing, age)</label>
              <textarea id="p-info" className="textarea" style={{ minHeight: 80 }} value={form.information ?? ''} onChange={(e) => set({ information: e.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="p-brand">Brand</label>
              <select id="p-brand" className="select" value={form.brandId ?? ''} onChange={(e) => set({ brandId: e.target.value ? Number(e.target.value) : null })}>
                <option value="">None</option>
                {brands.data?.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="p-tags">Tags</label>
              <input id="p-tags" className="input" value={form.tags.join(', ')} onChange={(e) => set({ tags: e.target.value.split(',').map((t) => t.trim()) })} aria-describedby="p-tags-h" />
              <small id="p-tags-h">Comma separated. “new”, “bestseller” and “gift” are used by the shop.</small>
            </div>
          </div>
        </section>

        <div className="stack">
          <section className="card stack">
            <h2>Listing</h2>
            <label className="check">
              <input type="checkbox" checked={form.active} onChange={(e) => set({ active: e.target.checked })} />
              Visible in the shop
            </label>
            <fieldset className="multi-field">
              <legend>Categories</legend>
              <div className="multi">
                {categories.data?.map((c) => (
                  <label key={c.id} className="check">
                    <input
                      type="checkbox"
                      checked={form.categoryIds.includes(c.id)}
                      onChange={(e) => set({ categoryIds: e.target.checked ? [...form.categoryIds, c.id] : form.categoryIds.filter((x) => x !== c.id) })}
                    />
                    {c.title}
                  </label>
                ))}
              </div>
            </fieldset>
            {form.categoryIds.length > 1 && (
              <div className="field">
                <label htmlFor="p-primary">Primary category</label>
                <select id="p-primary" className="select" value={form.primaryCategoryId ?? ''} onChange={(e) => set({ primaryCategoryId: Number(e.target.value) })}>
                  {categories.data?.filter((c) => form.categoryIds.includes(c.id)).map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.title}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </section>
          <section className="card stack">
            <h2>Customs</h2>
            <p className="muted" style={{ fontSize: '0.875rem' }}>
              Sent to Chit Chats on each line item for U.S. and international parcels.
            </p>
            <div className="form-grid">
              <div className="field">
                <label htmlFor="p-hs">HS tariff code</label>
                <input id="p-hs" className="input" value={form.hsTariffCode ?? ''} onChange={(e) => set({ hsTariffCode: e.target.value || null })} />
              </div>
              <div className="field">
                <label htmlFor="p-origin">Made in (country code)</label>
                <input id="p-origin" className="input" maxLength={2} value={form.originCountry ?? ''} onChange={(e) => set({ originCountry: e.target.value.toUpperCase() || null })} />
              </div>
            </div>
          </section>
        </div>
      </div>

      <section className="card stack">
        <div className="spread">
          <h2>Variants</h2>
          <button
            type="button"
            className="btn btn--sm btn--lemon"
            onClick={() => {
              const last = form.variants[form.variants.length - 1]
              set({ variants: [...form.variants, { ...blankVariant(last?.productVariantTypeId ?? types.data?.[0]?.id ?? 0), priceCents: last?.priceCents ?? 3800, weightGrams: last?.weightGrams ?? 320, design: last?.design ? { ...last.design } : { ...DEFAULT_DESIGN } }] })
              setOpen(form.variants.length)
            }}
          >
            Add variant
          </button>
        </div>
        {errors.variants && <p className="notice">{errors.variants}</p>}
        {form.variants.map((v, i) => {
          const e = (key: string) => errors[`variants[${i}].${key}`]
          const design = v.design
          const setDesign = (patch: Partial<PlushieDesign>) => setVariant(i, { design: { ...(design ?? DEFAULT_DESIGN), ...patch }, ...(patch.body ? { colorHex: patch.body } : {}) })
          return (
            <details key={v.id || `new-${i}`} className="variant-editor" open={open === i} onToggle={(ev) => (ev.currentTarget.open ? setOpen(i) : open === i && setOpen(-1))}>
              <summary>
                <span className="table__art">{v.images[0] ? <img src={v.images[0].url} alt="" /> : design && <Plushie design={design} decorative />}</span>
                <b>{v.value || 'New variant'}</b>
                <code>{v.sku}</code>
                <span className="muted">
                  {money(v.priceCents)} · {v.stock} in stock
                </span>
                {!v.active && <span className="chip chip--muted">Hidden</span>}
              </summary>
              <div className="variant-editor__body">
                <div className="form-grid form-grid--4">
                  <div className="field">
                    <label htmlFor={`v${i}-type`}>Varies by</label>
                    <select id={`v${i}-type`} className="select" value={v.productVariantTypeId} onChange={(ev) => setVariant(i, { productVariantTypeId: Number(ev.target.value) })}>
                      {types.data?.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.title}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <label htmlFor={`v${i}-value`}>Value</label>
                    <input id={`v${i}-value`} className="input" placeholder="e.g. Blueberry" value={v.value} onChange={(ev) => setVariant(i, { value: ev.target.value })} aria-invalid={e('value') ? true : undefined} />
                    {e('value') && <span className="field__error">{e('value')}</span>}
                  </div>
                  <div className="field">
                    <label htmlFor={`v${i}-sku`}>SKU</label>
                    <input id={`v${i}-sku`} className="input" value={v.sku} onChange={(ev) => setVariant(i, { sku: ev.target.value })} aria-invalid={e('sku') ? true : undefined} />
                    {e('sku') && <span className="field__error">{e('sku')}</span>}
                  </div>
                  <div className="field">
                    <label htmlFor={`v${i}-price`}>Price ($)</label>
                    <input id={`v${i}-price`} className="input" type="number" min="0" step="0.01" defaultValue={dollars(v.priceCents)} onChange={(ev) => setVariant(i, { priceCents: cents(ev.target.value) })} />
                  </div>
                  <div className="field">
                    <label htmlFor={`v${i}-stock`}>Stock</label>
                    <input id={`v${i}-stock`} className="input" type="number" min="0" step="1" value={v.stock} onChange={(ev) => setVariant(i, { stock: Number(ev.target.value) })} />
                  </div>
                  <div className="field">
                    <label htmlFor={`v${i}-weight`}>Weight (g)</label>
                    <input id={`v${i}-weight`} className="input" type="number" min="1" step="1" value={v.weightGrams} onChange={(ev) => setVariant(i, { weightGrams: Number(ev.target.value) })} />
                  </div>
                  <label className="check" style={{ alignSelf: 'end', paddingBottom: 12 }}>
                    <input type="checkbox" checked={v.active} onChange={(ev) => setVariant(i, { active: ev.target.checked })} />
                    For sale
                  </label>
                </div>

                <div className="variant-editor__look">
                  <div className="variant-editor__preview">{design ? <Plushie design={design} /> : <span className="muted">No drawing</span>}</div>
                  <div className="stack" style={{ flex: 1 }}>
                    <label className="check">
                      <input type="checkbox" checked={!!design} onChange={(ev) => setVariant(i, { design: ev.target.checked ? { ...DEFAULT_DESIGN } : null })} />
                      Draw this variant (used when there are no photos)
                    </label>
                    {design && (
                      <div className="form-grid form-grid--4">
                        {(
                          [
                            ['species', 'Critter', o.species],
                            ['eyes', 'Eyes', o.eyes],
                            ['mouth', 'Mouth', o.mouths],
                            ['accessory', 'Accessory', o.accessories],
                          ] as const
                        ).map(([key, label, list]) => (
                          <div className="field" key={key}>
                            <label htmlFor={`v${i}-${key}`}>{label}</label>
                            <select id={`v${i}-${key}`} className="select" value={design[key]} onChange={(ev) => setDesign({ [key]: ev.target.value })}>
                              {list.map((x) => (
                                <option key={x.id} value={x.id}>
                                  {x.label}
                                </option>
                              ))}
                            </select>
                          </div>
                        ))}
                        {(
                          [
                            ['body', 'Body'],
                            ['belly', 'Belly'],
                            ['accessoryColor', 'Accent'],
                          ] as const
                        ).map(([key, label]) => (
                          <div className="field" key={key}>
                            <label htmlFor={`v${i}-${key}`}>{label} colour</label>
                            <input id={`v${i}-${key}`} className="color" type="color" value={design[key]} onChange={(ev) => setDesign({ [key]: ev.target.value.toUpperCase() })} />
                          </div>
                        ))}
                        <label className="check" style={{ alignSelf: 'end', paddingBottom: 12 }}>
                          <input type="checkbox" checked={design.cheeks} onChange={(ev) => setDesign({ cheeks: ev.target.checked })} />
                          Rosy cheeks
                        </label>
                      </div>
                    )}
                    <VariantPhotos images={v.images} onChange={(images) => setVariant(i, { images })} />
                  </div>
                </div>
                {form.variants.length > 1 && (
                  <div>
                    <button type="button" className="btn btn--quiet" onClick={() => window.confirm(`Remove the variant “${v.value || 'new'}”?`) && set({ variants: form.variants.filter((_, x) => x !== i) })}>
                      Remove this variant
                    </button>
                  </div>
                )}
              </div>
            </details>
          )
        })}
      </section>

      {!isNew && (
        <div>
          <button
            type="button"
            className="btn btn--quiet"
            disabled={remove.isPending}
            onClick={() => window.confirm(`Delete “${form.name}”? If it has been ordered before it will be hidden instead, so order history stays intact.`) && remove.mutate()}
          >
            Delete product
          </button>
        </div>
      )}
    </form>
  )
}

function VariantPhotos({ images, onChange }: { images: { id: number; url: string }[]; onChange: (images: { id: number; url: string }[]) => void }) {
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  return (
    <div className="stack" style={{ '--gap': '8px' } as React.CSSProperties}>
      <span className="label">Photos</span>
      <div className="photos">
        {images.map((img) => (
          <div key={img.url} className="photos__item">
            <img src={img.url} alt="" />
            <button type="button" aria-label="Remove photo" onClick={() => onChange(images.filter((x) => x.url !== img.url))}>
              ×
            </button>
          </div>
        ))}
        <label className="photos__add">
          {busy ? '…' : '+ Photo'}
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp,image/gif"
            className="sr-only"
            disabled={busy}
            onChange={async (e) => {
              const file = e.target.files?.[0]
              e.target.value = ''
              if (!file) return
              setBusy(true)
              setError(null)
              try {
                onChange([...images, { id: 0, url: await uploadImage(file) }])
              } catch (err) {
                setError(err)
              } finally {
                setBusy(false)
              }
            }}
          />
        </label>
      </div>
      <ErrorNotice error={error} />
    </div>
  )
}
