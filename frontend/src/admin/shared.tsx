import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type ReactNode } from 'react'
import { ErrorNotice, Loading } from '../components/ui'
import { api, ApiError } from '../lib/api'

export const dollars = (cents: number | null | undefined) => (cents == null ? '' : (cents / 100).toFixed(2))
export const cents = (text: string) => Math.round(parseFloat(text || '0') * 100)

/** ISO timestamp <-> the value a datetime-local input wants (local time, no zone). */
export const toLocalInput = (iso: string | null | undefined) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}
export const fromLocalInput = (value: string) => (value ? new Date(value).toISOString() : null)

export async function uploadImage(file: File): Promise<string> {
  const form = new FormData()
  form.append('file', file)
  return (await api<{ url: string }>('/api/admin/uploads', { method: 'POST', form })).url
}

export function ImageInput({ value, onChange }: { value: string; onChange: (url: string) => void }) {
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  return (
    <div className="image-input">
      {value && <img src={value} alt="" />}
      <label className="btn btn--sm">
        {busy ? 'Uploading…' : value ? 'Replace image' : 'Upload image'}
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
              onChange(await uploadImage(file))
            } catch (err) {
              setError(err)
            } finally {
              setBusy(false)
            }
          }}
        />
      </label>
      {value && (
        <button type="button" className="btn btn--quiet" onClick={() => onChange('')}>
          Remove
        </button>
      )}
      <ErrorNotice error={error} />
    </div>
  )
}

// ---- Generic list + form for the simple admin tables ------------------------------------------
type Option = { value: string | number; label: string }
export type FieldDef = {
  key: string
  label: string
  type?: 'text' | 'textarea' | 'html' | 'number' | 'money' | 'checkbox' | 'select' | 'multiselect' | 'datetime' | 'image'
  options?: Option[]
  /** Load options from another admin list endpoint. */
  optionsFrom?: { path: string; value: string; label: string }
  nullable?: boolean
  hint?: string
  wide?: boolean
}
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Row = Record<string, any>
export type Resource = {
  path: string
  title: string
  singular: string
  blurb: string
  columns: { label: string; render: (row: Row) => ReactNode }[]
  fields: FieldDef[]
  blank: () => Row
}

function useOptions(field: FieldDef): Option[] {
  const from = field.optionsFrom
  const { data } = useQuery({ queryKey: ['admin', from?.path], queryFn: () => api<Row[]>(from!.path), enabled: !!from })
  if (!from) return field.options ?? []
  return (data ?? []).map((r) => ({ value: r[from.value], label: String(r[from.label]) }))
}

function FieldInput({ field, value, onChange, error }: { field: FieldDef; value: unknown; onChange: (v: unknown) => void; error?: string }) {
  const id = useId()
  const options = useOptions(field)
  const common = { id, 'aria-invalid': error ? (true as const) : undefined, 'aria-describedby': error ? `${id}-e` : field.hint ? `${id}-h` : undefined }
  let control: ReactNode
  switch (field.type) {
    case 'checkbox':
      return (
        <label className={`check ${field.wide ? 'span-2' : ''}`}>
          <input type="checkbox" checked={!!value} onChange={(e) => onChange(e.target.checked)} />
          {field.label}
        </label>
      )
    case 'textarea':
    case 'html':
      control = <textarea {...common} className="textarea" style={field.type === 'html' ? { minHeight: 220, fontFamily: 'var(--font-mono)', fontSize: 14 } : undefined} value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value)} />
      break
    case 'number':
      control = <input {...common} className="input" type="number" step="any" value={(value as number) ?? ''} onChange={(e) => onChange(e.target.value === '' ? (field.nullable ? null : 0) : Number(e.target.value))} />
      break
    case 'money':
      control = (
        <input
          {...common}
          className="input"
          type="number"
          min="0"
          step="0.01"
          inputMode="decimal"
          placeholder="0.00"
          defaultValue={dollars(value as number | null)}
          onChange={(e) => onChange(e.target.value === '' ? (field.nullable ? null : 0) : cents(e.target.value))}
        />
      )
      break
    case 'select':
      control = (
        <select {...common} className="select" value={value == null ? '' : String(value)} onChange={(e) => onChange(e.target.value === '' ? null : typeof options[0]?.value === 'number' ? Number(e.target.value) : e.target.value)}>
          {(field.nullable || value == null || value === 0) && <option value="">{field.nullable ? 'None' : 'Choose…'}</option>}
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      )
      break
    case 'multiselect': {
      const selected = new Set((value as (string | number)[]) ?? [])
      control = (
        <div className="multi" role="group" aria-labelledby={`${id}-l`}>
          {options.length === 0 && <span className="muted">Nothing to choose from yet.</span>}
          {options.map((o) => (
            <label key={o.value} className="check">
              <input
                type="checkbox"
                checked={selected.has(o.value)}
                onChange={(e) => {
                  const next = new Set(selected)
                  if (e.target.checked) next.add(o.value)
                  else next.delete(o.value)
                  onChange([...next])
                }}
              />
              {o.label}
            </label>
          ))}
        </div>
      )
      break
    }
    case 'datetime':
      control = <input {...common} className="input" type="datetime-local" value={toLocalInput(value as string)} onChange={(e) => onChange(fromLocalInput(e.target.value))} />
      break
    case 'image':
      control = <ImageInput value={(value as string) ?? ''} onChange={onChange} />
      break
    default:
      control = <input {...common} className="input" value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value === '' && field.nullable ? null : e.target.value)} />
  }
  return (
    <div className={`field ${field.wide ? 'span-2' : ''}`}>
      <label htmlFor={id} id={`${id}-l`}>
        {field.label}
      </label>
      {control}
      {error ? (
        <span id={`${id}-e`} className="field__error">
          {error}
        </span>
      ) : (
        field.hint && <small id={`${id}-h`}>{field.hint}</small>
      )}
    </div>
  )
}

export function Drawer({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="drawer" role="dialog" aria-modal="true" aria-label={title} onKeyDown={(e) => e.key === 'Escape' && onClose()}>
      <button type="button" className="drawer__scrim" aria-label="Close" onClick={onClose} />
      <div className="drawer__panel">
        <div className="spread">
          <h2>{title}</h2>
          <button type="button" className="btn btn--sm" onClick={onClose}>
            Close
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function ResourcePage({ resource: r }: { resource: Resource }) {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['admin', r.path], queryFn: () => api<Row[]>(r.path) })
  const [editing, setEditing] = useState<Row | null>(null)
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', r.path] })

  const save = useMutation({
    mutationFn: (row: Row) => api(row.id ? `${r.path}/${row.id}` : r.path, { method: row.id ? 'PUT' : 'POST', body: row }),
    onSuccess: () => {
      refresh()
      setEditing(null)
    },
  })
  const remove = useMutation({ mutationFn: (id: number) => api(`${r.path}/${id}`, { method: 'DELETE' }), onSuccess: refresh })
  const fieldErrors = save.error instanceof ApiError ? save.error.fields : {}

  return (
    <div className="stack" style={{ '--gap': '20px' } as React.CSSProperties}>
      <div className="admin-head">
        <div>
          <h1>{r.title}</h1>
          <p className="muted">{r.blurb}</p>
        </div>
        <button type="button" className="btn btn--pink" onClick={() => { save.reset(); setEditing(r.blank()) }}>
          New {r.singular}
        </button>
      </div>
      <ErrorNotice error={list.error ?? remove.error} />
      {list.isLoading ? (
        <Loading />
      ) : list.data?.length === 0 ? (
        <p className="card muted">No {r.title.toLowerCase()} yet. Create the first one with “New {r.singular}”.</p>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                {r.columns.map((c) => (
                  <th key={c.label}>{c.label}</th>
                ))}
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {list.data?.map((row) => (
                <tr key={row.id}>
                  {r.columns.map((c) => (
                    <td key={c.label}>{c.render(row)}</td>
                  ))}
                  <td className="table__actions">
                    <button type="button" className="btn btn--sm" onClick={() => { save.reset(); setEditing(structuredClone(row)) }}>
                      Edit
                    </button>
                    <button
                      type="button"
                      className="btn btn--quiet"
                      onClick={() => window.confirm(`Delete this ${r.singular}? This can't be undone.`) && remove.mutate(row.id)}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editing && (
        <Drawer title={`${editing.id ? 'Edit' : 'New'} ${r.singular}`} onClose={() => setEditing(null)}>
          <form
            className="stack"
            onSubmit={(e) => {
              e.preventDefault()
              save.mutate(editing)
            }}
          >
            <div className="form-grid">
              {r.fields.map((f) => (
                <FieldInput key={f.key} field={f} value={editing[f.key]} error={fieldErrors[f.key]} onChange={(v) => setEditing((row) => ({ ...row!, [f.key]: v }))} />
              ))}
            </div>
            {/* A field-level message already sits under its input; only show the banner for everything else. */}
            {!r.fields.some((f) => fieldErrors[f.key]) && <ErrorNotice error={save.error} />}
            <div className="row">
              <button className="btn btn--pink" disabled={save.isPending}>
                {save.isPending ? 'Saving…' : `Save ${r.singular}`}
              </button>
              <button type="button" className="btn" onClick={() => setEditing(null)}>
                Cancel
              </button>
            </div>
          </form>
        </Drawer>
      )}
    </div>
  )
}
