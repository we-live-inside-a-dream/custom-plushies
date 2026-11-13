import { useQuery } from '@tanstack/react-query'
import { useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, Loading } from '../components/ui'
import { api, money } from '../lib/api'
import type { BuilderOptions, PlushieDesign } from '../lib/types'
import { useCartActions, useToast } from '../state/session'

const START: PlushieDesign = { species: 'bear', body: '#FF8A3D', belly: '#FFF1D6', eyes: 'button', mouth: 'smile', cheeks: true, accessory: 'bow', accessoryColor: '#5B2EFF' }
const NAMES = ['Biscuit', 'Pickle', 'Waffle', 'Mango', 'Pudding', 'Sprout', 'Noodle', 'Peanut', 'Tofu', 'Clementine', 'Gus', 'Olive']
const pick = <T,>(list: T[]) => list[Math.floor(Math.random() * list.length)]

/** Mirrors BuilderCatalog.Price on the server. Only for the live preview — the server prices the real order. */
function estimate(options: BuilderOptions, d: PlushieDesign, size: string, embroidery: string) {
  const species = options.species.find((s) => s.id === d.species)
  const s = options.sizes.find((x) => x.id === size)
  const accessory = options.accessories.find((a) => a.id === d.accessory)
  if (!species || !s) return 0
  return Math.round((species.priceCents * s.multiplier) / 100) * 100 + (accessory?.priceCents ?? 0) + (embroidery.trim() ? options.embroideryPriceCents : 0)
}

export function Builder() {
  const { data: options, isLoading, error } = useQuery({ queryKey: ['builder'], queryFn: () => api<BuilderOptions>('/api/builder/options'), staleTime: Infinity })
  const [design, setDesign] = useState(START)
  const [size, setSize] = useState('classic')
  const [name, setName] = useState('')
  const [embroidery, setEmbroidery] = useState('')
  const [notes, setNotes] = useState('')
  const [nameError, setNameError] = useState('')
  const [roll, setRoll] = useState(0)
  const { add } = useCartActions()
  const toast = useToast()
  const navigate = useNavigate()
  const set = (patch: Partial<PlushieDesign>) => setDesign((d) => ({ ...d, ...patch }))

  const price = useMemo(() => (options ? estimate(options, design, size, embroidery) : 0), [options, design, size, embroidery])

  if (isLoading) return <Loading label="Loading the builder" />
  if (!options) return <div className="wrap section"><ErrorNotice error={error ?? new Error()} /></div>

  const fabricName = (hex: string) => options.fabrics.find((f) => f.hex.toLowerCase() === hex.toLowerCase())?.name ?? hex
  const accentLabel = design.species === 'dino' ? 'Spikes & accessory colour' : design.species === 'axolotl' ? 'Gills & accessory colour' : 'Accessory colour'
  const showAccent = design.accessory !== 'none' || design.species === 'dino' || design.species === 'axolotl'

  const surprise = () => {
    const fabrics = options.fabrics.map((f) => f.hex)
    setDesign({
      species: pick(options.species).id,
      body: pick(fabrics),
      belly: pick(['#FFF1D6', '#FFFFFF', '#FFE3EE', '#EAFFF7', '#C9B6FF']),
      eyes: pick(options.eyes).id,
      mouth: pick(options.mouths).id,
      cheeks: Math.random() > 0.25,
      accessory: pick(options.accessories).id,
      accessoryColor: pick(fabrics),
    })
    if (!name) setName(pick(NAMES))
    setRoll((r) => r + 1)
  }

  const submit = () => {
    if (!name.trim()) {
      setNameError('Every plushie needs a name.')
      document.getElementById('plushie-name')?.focus()
      return
    }
    add.mutate(
      { quantity: 1, customDesign: { name: name.trim(), design, size, embroideryText: embroidery.trim() || undefined, notes: notes.trim() || undefined } },
      {
        onSuccess: () => {
          toast(`${name.trim()} is in your cart!`)
          navigate('/cart')
        },
      },
    )
  }

  return (
    <div className="builder" style={{ '--tint': design.body } as React.CSSProperties}>
      <div className="builder__stage">
        <div className="builder__stage-inner">
          <span className="chip chip--ink">Your one-of-one</span>
          <button type="button" className="squish builder__plushie" aria-label="Squish your plushie">
            <Plushie key={`${design.species}${roll}`} design={design} animated className="pop-in" title={`Your custom ${design.species}`} />
          </button>
          <div className="name-tag" aria-hidden="true">
            <span className="name-tag__hello">Hello, my name is</span>
            <span className="name-tag__name">{name.trim() || '…?'}</span>
          </div>
          <button type="button" className="btn btn--lemon" onClick={surprise}>
            Surprise me
          </button>
        </div>
      </div>

      <div className="builder__panel">
        <header className="stack" style={{ '--gap': '10px' } as React.CSSProperties}>
          <h1>Build your plushie</h1>
          <p className="muted">Everything here is stitched to order, just for you. Change anything — the preview keeps up.</p>
        </header>

        <Group n={1} title="Pick a critter">
          <div className="tiles tiles--species">
            {options.species.map((s) => (
              <button key={s.id} type="button" className="tile" aria-pressed={design.species === s.id} onClick={() => set({ species: s.id })}>
                <Plushie design={{ ...design, species: s.id, accessory: 'none' }} decorative />
                <span>{s.label}</span>
                <small>{money(s.priceCents)}</small>
              </button>
            ))}
          </div>
        </Group>

        <Group n={2} title="Choose the fabrics">
          <Swatches label={`Body · ${fabricName(design.body)}`} colors={options.fabrics} value={design.body} onChange={(body) => set({ body })} />
          <Swatches label={`Belly & ears · ${fabricName(design.belly)}`} colors={options.fabrics} value={design.belly} onChange={(belly) => set({ belly })} />
        </Group>

        <Group n={3} title="Give it a face">
          <Choice label="Eyes" options={options.eyes} value={design.eyes} onChange={(eyes) => set({ eyes })} />
          <Choice label="Mouth" options={options.mouths} value={design.mouth} onChange={(mouth) => set({ mouth })} />
          <label className="check">
            <input type="checkbox" checked={design.cheeks} onChange={(e) => set({ cheeks: e.target.checked })} />
            Rosy cheeks
          </label>
        </Group>

        <Group n={4} title="Dress it up">
          <Choice label="Accessory" options={options.accessories} value={design.accessory} onChange={(accessory) => set({ accessory })} showPrice />
          {showAccent && <Swatches label={`${accentLabel} · ${fabricName(design.accessoryColor)}`} colors={options.fabrics} value={design.accessoryColor} onChange={(accessoryColor) => set({ accessoryColor })} />}
        </Group>

        <Group n={5} title="How big?">
          <div className="tiles tiles--sizes">
            {options.sizes.map((s) => (
              <button key={s.id} type="button" className="tile tile--size" aria-pressed={size === s.id} onClick={() => setSize(s.id)}>
                <span className="tile__ruler" style={{ height: `${18 + s.heightCm}px` }} />
                <span>{s.label}</span>
                <small>{s.heightCm} cm tall</small>
              </button>
            ))}
          </div>
        </Group>

        <Group n={6} title="Name & finishing touches">
          <div className="field">
            <label htmlFor="plushie-name">Plushie's name</label>
            <input
              id="plushie-name"
              className="input"
              maxLength={40}
              placeholder="e.g. Sir Chomps"
              value={name}
              aria-invalid={nameError ? true : undefined}
              aria-describedby={nameError ? 'plushie-name-error' : undefined}
              onChange={(e) => {
                setName(e.target.value)
                setNameError('')
              }}
            />
            {nameError && (
              <span id="plushie-name-error" className="field__error">
                {nameError}
              </span>
            )}
          </div>
          <div className="field">
            <label htmlFor="embroidery">
              Embroider a word on the foot <span className="muted">(+{money(options.embroideryPriceCents)})</span>
            </label>
            <input id="embroidery" className="input" maxLength={options.embroideryMaxLength} placeholder="Optional" value={embroidery} onChange={(e) => setEmbroidery(e.target.value)} aria-describedby="embroidery-hint" />
            <small id="embroidery-hint">
              {embroidery.length}/{options.embroideryMaxLength} characters
            </small>
          </div>
          <div className="field">
            <label htmlFor="notes">Anything our stitchers should know?</label>
            <textarea id="notes" className="textarea" maxLength={1000} placeholder="Optional — a gift note, a favourite colour detail…" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        </Group>

        <ErrorNotice error={add.error} />
      </div>

      <div className="builder__bar">
        <div className="wrap builder__bar-inner">
          <div>
            <span className="label">
              {name.trim() || `Custom ${design.species}`} · {options.sizes.find((s) => s.id === size)?.label}
            </span>
            <strong className="builder__price" aria-live="polite">
              {money(price)}
            </strong>
          </div>
          <button type="button" className="btn btn--pink btn--lg" onClick={submit} disabled={add.isPending}>
            {add.isPending ? 'Adding…' : 'Add to cart'}
          </button>
        </div>
      </div>
    </div>
  )
}

function Group({ n, title, children }: { n: number; title: string; children: ReactNode }) {
  return (
    <section className="builder-group">
      <h2>
        <span className="builder-group__n">{n}</span>
        {title}
      </h2>
      {children}
    </section>
  )
}

function Swatches({ label, colors, value, onChange }: { label: string; colors: { name: string; hex: string }[]; value: string; onChange: (hex: string) => void }) {
  return (
    <fieldset className="swatch-picker">
      <legend>{label}</legend>
      <div>
        {colors.map((c) => (
          <button
            key={c.hex}
            type="button"
            className="swatch"
            style={{ background: c.hex }}
            aria-pressed={c.hex.toLowerCase() === value.toLowerCase()}
            aria-label={c.name}
            title={c.name}
            onClick={() => onChange(c.hex)}
          />
        ))}
      </div>
    </fieldset>
  )
}

function Choice({ label, options, value, onChange, showPrice }: { label: string; options: { id: string; label: string; priceCents: number }[]; value: string; onChange: (id: string) => void; showPrice?: boolean }) {
  return (
    <fieldset className="pill-picker">
      <legend>{label}</legend>
      <div className="pills">
        {options.map((o) => (
          <button key={o.id} type="button" className="pill" aria-pressed={value === o.id} onClick={() => onChange(o.id)}>
            {o.label}
            {showPrice && o.priceCents > 0 && <small> +{money(o.priceCents)}</small>}
          </button>
        ))}
      </div>
    </fieldset>
  )
}
