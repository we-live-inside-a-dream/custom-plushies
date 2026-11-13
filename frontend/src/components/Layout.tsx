import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth, useCart, useCopy } from '../state/session'
import { Icon, Marquee } from './ui'

const NAV = [
  { to: '/shop', label: 'Shop' },
  { to: '/build', label: 'Build yours' },
  { to: '/journal', label: 'Journal' },
  { to: '/about', label: 'About' },
]

export function Logo({ name }: { name: string }) {
  return (
    <Link to="/" className="logo" aria-label={`${name} home`}>
      <svg viewBox="0 0 64 64" aria-hidden="true">
        <circle cx="14" cy="14" r="10" fill="var(--pink)" stroke="var(--ink)" strokeWidth="4" />
        <circle cx="50" cy="14" r="10" fill="var(--pink)" stroke="var(--ink)" strokeWidth="4" />
        <ellipse cx="32" cy="36" rx="27" ry="24" fill="var(--pink)" stroke="var(--ink)" strokeWidth="4" />
        <circle cx="22" cy="34" r="4" fill="var(--ink)" />
        <circle cx="42" cy="34" r="4" fill="var(--ink)" />
        <path d="M26 44q6 6 12 0" fill="none" stroke="var(--ink)" strokeWidth="4" strokeLinecap="round" />
      </svg>
      <span>{name.toLowerCase()}</span>
    </Link>
  )
}

export function Layout() {
  const copy = useCopy('global', { store_name: 'Stitchling' })
  const { customer } = useAuth()
  const cart = useCart()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const count = cart.data?.itemCount ?? 0
  const name = copy('store_name')

  useEffect(() => {
    setOpen(false)
    window.scrollTo(0, 0)
  }, [pathname])

  return (
    <>
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      {copy('announcement') && <Marquee className="announce" items={copy('announcement').split('·').map((s) => s.trim())} />}
      <header className="site-header">
        <div className="wrap site-header__inner">
          <Logo name={name} />
          <nav className={`site-nav ${open ? 'is-open' : ''}`} aria-label="Main">
            {NAV.map((n) => (
              <NavLink key={n.to} to={n.to} className={n.to === '/build' ? 'site-nav__build' : undefined}>
                {n.label}
              </NavLink>
            ))}
          </nav>
          <div className="site-header__actions">
            <Link to={customer ? '/account' : '/login'} className="icon-btn" aria-label={customer ? 'Your account' : 'Sign in'}>
              <Icon.user />
            </Link>
            <Link to="/cart" className="icon-btn cart-btn" aria-label={`Cart, ${count} item${count === 1 ? '' : 's'}`}>
              <Icon.cart />
              {count > 0 && (
                <span className="cart-btn__count" key={count}>
                  {count}
                </span>
              )}
            </Link>
            <button type="button" className="icon-btn site-header__menu" aria-expanded={open} aria-label="Menu" onClick={() => setOpen((o) => !o)}>
              {open ? <Icon.close /> : <Icon.menu />}
            </button>
          </div>
        </div>
      </header>

      <main id="main">
        <Outlet />
      </main>

      <footer className="site-footer">
        <div className="pinked" style={{ '--c': 'var(--ink)' } as React.CSSProperties} />
        <div className="site-footer__body">
          <div className="wrap site-footer__grid">
            <div className="stack">
              <span className="site-footer__mark">{name.toLowerCase()}</span>
              <p>{copy('footer_blurb')}</p>
            </div>
            <nav aria-label="Shop">
              <span className="label">Shop</span>
              <Link to="/shop">All plushies</Link>
              <Link to="/build">Build your own</Link>
              <Link to="/shop?tag=bestseller">Bestsellers</Link>
              <Link to="/shop?tag=gift">Gifts</Link>
            </nav>
            <nav aria-label="Help">
              <span className="label">Help</span>
              <Link to="/account">Track an order</Link>
              <Link to="/about">About us</Link>
              <Link to="/journal">Journal</Link>
              {copy('support_email') && <a href={`mailto:${copy('support_email')}`}>{copy('support_email')}</a>}
            </nav>
          </div>
          <div className="wrap site-footer__legal">
            <span>
              © {new Date().getFullYear()} {name}. Stitched with care.
            </span>
            <Link to="/admin">Staff sign-in</Link>
          </div>
        </div>
      </footer>
    </>
  )
}
