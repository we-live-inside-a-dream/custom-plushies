import { useMutation, useQuery } from '@tanstack/react-query'
import DOMPurify from 'dompurify'
import { useMemo, useState } from 'react'
import { Link, Navigate, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { Plushie } from '../components/Plushie'
import { ErrorNotice, Field, Loading, PageHead } from '../components/ui'
import { api, ApiError, shortDate } from '../lib/api'
import type { Customer, Post, PostSummary } from '../lib/types'
import { useAuth, useCopy } from '../state/session'
import { SHOWCASE } from './Home'

export function NotFound() {
  return (
    <div className="wrap empty" style={{ minHeight: '56vh', alignContent: 'center' }}>
      <Plushie design={{ ...SHOWCASE[5], eyes: 'stitched', accessory: 'none' }} />
      <h1>Lost its stuffing</h1>
      <p>That page doesn't exist — it may have been moved, or the link has a typo.</p>
      <div className="row">
        <Link to="/" className="btn">
          Back home
        </Link>
        <Link to="/shop" className="btn btn--pink">
          See the plushies
        </Link>
      </div>
    </div>
  )
}

/** Only follow "next" to a path inside this site, never to another origin. */
const safeNext = (next: string | null) => (next && next.startsWith('/') && !next.startsWith('//') ? next : '/account')

export function AuthPage({ mode }: { mode: 'login' | 'register' }) {
  const { customer, signIn } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const next = safeNext(params.get('next'))
  const [form, setForm] = useState({ email: '', password: '', firstName: '', lastName: '' })
  const submit = useMutation({
    mutationFn: () => api<{ token: string; customer: Customer }>(`/api/auth/${mode}`, { method: 'POST', body: form }),
    onSuccess: (response) => {
      signIn(response)
      navigate(next, { replace: true })
    },
  })
  const errors = submit.error instanceof ApiError ? submit.error.fields : {}
  const isRegister = mode === 'register'
  const other = `${isRegister ? '/login' : '/register'}${params.get('next') ? `?next=${encodeURIComponent(next)}` : ''}`

  if (customer && !submit.isSuccess) return <Navigate to={next} replace />

  return (
    <div className="auth">
      <div className="auth__art" aria-hidden="true">
        <Plushie design={SHOWCASE[isRegister ? 1 : 0]} animated />
      </div>
      <form
        className="auth__form card stack"
        noValidate
        onSubmit={(e) => {
          e.preventDefault()
          submit.mutate()
        }}
      >
        <h1>{isRegister ? 'Join the family' : 'Welcome back'}</h1>
        <p className="muted">{isRegister ? 'Create an account to check out, track orders and save favourites.' : 'Sign in to check out and see your orders.'}</p>
        {isRegister && (
          <div className="form-grid">
            <Field label="First name" error={errors.firstName}>
              {(p) => <input {...p} className="input" autoComplete="given-name" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />}
            </Field>
            <Field label="Last name" error={errors.lastName}>
              {(p) => <input {...p} className="input" autoComplete="family-name" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />}
            </Field>
          </div>
        )}
        <Field label="Email" error={errors.email}>
          {(p) => <input {...p} className="input" type="email" autoComplete="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />}
        </Field>
        <Field label="Password" error={errors.password} hint={isRegister ? 'At least 8 characters.' : undefined}>
          {(p) => (
            <input {...p} className="input" type="password" autoComplete={isRegister ? 'new-password' : 'current-password'} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
          )}
        </Field>
        {Object.keys(errors).length === 0 && <ErrorNotice error={submit.error} />}
        <button className="btn btn--pink btn--lg btn--block" disabled={submit.isPending}>
          {submit.isPending ? 'One moment…' : isRegister ? 'Create account' : 'Sign in'}
        </button>
        <p>
          {isRegister ? 'Already have an account?' : 'New here?'} <Link to={other}>{isRegister ? 'Sign in' : 'Create an account'}</Link>
        </p>
      </form>
    </div>
  )
}

export function Journal() {
  const { data, isLoading, error } = useQuery({ queryKey: ['posts', 'all'], queryFn: () => api<PostSummary[]>('/api/posts?take=50') })
  return (
    <>
      <PageHead title="The journal" kicker="From the workshop">
        Notes on how things get made, who's new on the shelf, and the occasional sewing-machine drama.
      </PageHead>
      <div className="wrap section">
        {isLoading && <Loading />}
        <ErrorNotice error={error} />
        {data?.length === 0 && <p className="muted">Nothing posted yet — check back soon.</p>}
        <div className="post-grid">
          {data?.map((p) => (
            <Link key={p.id} to={`/journal/${p.slug}`} className="post-card card">
              {p.bannerImage && <img src={p.bannerImage} alt="" className="post-card__banner" />}
              <span className="chip chip--sky">{p.type}</span>
              <h3>{p.title}</h3>
              <p className="muted">{p.excerpt}</p>
              <span className="label">{shortDate(p.createdAt)}</span>
            </Link>
          ))}
        </div>
      </div>
    </>
  )
}

export function PostPage() {
  const { slug = '' } = useParams()
  const { data, isLoading, error } = useQuery({ queryKey: ['post', slug], queryFn: () => api<Post>(`/api/posts/${slug}`) })
  // Post bodies are HTML written in the admin site. Sanitise before rendering so a compromised
  // staff account (or a pasted snippet) can't run script in a customer's browser.
  const html = useMemo(() => (data ? DOMPurify.sanitize(data.body, { USE_PROFILES: { html: true } }) : ''), [data])
  if (isLoading) return <Loading />
  if (error instanceof ApiError && error.status === 404) return <NotFound />
  if (!data) return <div className="wrap section"><ErrorNotice error={error ?? new Error()} /></div>
  return (
    <article>
      <PageHead title={data.title} kicker={data.type}>
        {data.excerpt}
      </PageHead>
      <div className="wrap prose-wrap">
        {data.bannerImage && <img src={data.bannerImage} alt="" className="prose__banner" />}
        <p className="label muted">
          {shortDate(data.createdAt)}
          {data.author && ` · ${data.author}`}
        </p>
        <div className="prose" dangerouslySetInnerHTML={{ __html: html }} />
        <Link to="/journal" className="btn">
          ← More from the journal
        </Link>
      </div>
    </article>
  )
}

export function About() {
  const copy = useCopy('about', { title: 'Hello, we make soft things', body: '' })
  return (
    <>
      <PageHead title={copy('title')} kicker="About" />
      <div className="wrap about">
        <div className="about__art" aria-hidden="true">
          <Plushie design={SHOWCASE[0]} />
          <Plushie design={SHOWCASE[2]} />
        </div>
        <div className="stack prose">
          {copy('body')
            .split(/\n{2,}/)
            .map((para, i) => (
              <p key={i}>{para}</p>
            ))}
          <div className="row">
            <Link to="/build" className="btn btn--pink">
              Build your own
            </Link>
            <Link to="/journal" className="btn">
              Read the journal
            </Link>
          </div>
        </div>
      </div>
    </>
  )
}
