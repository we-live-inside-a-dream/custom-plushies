import type { CSSProperties } from 'react'
import type { PlushieDesign } from '../lib/types'

const INK = '#1D1340'
const LIGHT = '#FFF6E5'
const BLUSH = '#FF6B9A'

/** Perceived lightness 0..1, used to keep faces readable on dark fabrics. */
function luminance(hex: string): number {
  const n = parseInt(hex.slice(1), 16)
  const [r, g, b] = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map((c) => c / 255)
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}
const inkOn = (hex: string) => (luminance(hex) < 0.22 ? LIGHT : INK)

type Geometry = { headRx: number; headRy: number; headCy: number; eyeY: number; eyeDx: number; mouthY: number }
const GEOMETRY: Record<string, Geometry> = {
  bear: { headRx: 92, headRy: 80, headCy: 120, eyeY: 118, eyeDx: 40, mouthY: 152 },
  bunny: { headRx: 88, headRy: 78, headCy: 122, eyeY: 120, eyeDx: 38, mouthY: 146 },
  cat: { headRx: 94, headRy: 78, headCy: 122, eyeY: 120, eyeDx: 40, mouthY: 146 },
  frog: { headRx: 100, headRy: 70, headCy: 130, eyeY: 62, eyeDx: 52, mouthY: 140 },
  dino: { headRx: 90, headRy: 80, headCy: 120, eyeY: 116, eyeDx: 40, mouthY: 146 },
  axolotl: { headRx: 100, headRy: 74, headCy: 124, eyeY: 118, eyeDx: 46, mouthY: 138 },
}

type Props = {
  design: PlushieDesign
  /** Where the eyes look, each axis -1..1. */
  look?: { x: number; y: number }
  /** Blink and breathe. Off for dense grids of small plushies. */
  animated?: boolean
  title?: string
  /** Purely ornamental (next to a text label): hidden from assistive tech. */
  decorative?: boolean
  className?: string
  style?: CSSProperties
}

/**
 * Draws a plushie from a design. This is the one renderer for the whole app: product cards,
 * the builder preview, cart lines and order history all go through it, so what a customer
 * designs is exactly what shows up on their order.
 */
export function Plushie({ design, look, animated = false, title, decorative, className, style }: Props) {
  const d = design
  const g = GEOMETRY[d.species] ?? GEOMETRY.bear
  const face = inkOn(d.body)
  const stroke = { stroke: INK, strokeWidth: 5, strokeLinejoin: 'round' as const, strokeLinecap: 'round' as const }
  const body = { fill: d.body, ...stroke }
  const accent = { fill: d.accessoryColor, ...stroke }
  const lx = (look?.x ?? 0) * 3.5
  const ly = (look?.y ?? 0) * 3
  const wide = d.species === 'frog' || d.species === 'axolotl'
  const eyes: [number, number][] = [
    [150 - g.eyeDx, g.eyeY],
    [150 + g.eyeDx, g.eyeY],
  ]

  return (
    <svg
      viewBox="0 -44 300 364"
      className={['plushie', animated && 'plushie--animated', className].filter(Boolean).join(' ')}
      style={style}
      role={decorative ? undefined : 'img'}
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : (title ?? `${d.species} plushie`)}
    >
      <ellipse cx="150" cy="300" rx="92" ry="11" fill={INK} opacity=".14" />

      <g className="plushie__body">
        {/* Things that sit behind the head and body */}
        {d.species === 'dino' && <path d="M210 236 Q266 226 274 258 Q252 274 208 264 Z" {...body} />}
        {d.species === 'bear' && (
          <>
            <circle cx="76" cy="58" r="30" {...body} />
            <circle cx="224" cy="58" r="30" {...body} />
            <circle cx="76" cy="58" r="14" fill={d.belly} />
            <circle cx="224" cy="58" r="14" fill={d.belly} />
          </>
        )}
        {d.species === 'bunny' &&
          [-1, 1].map((s) => (
            <g key={s} transform={`rotate(${s * 11} ${150 + s * 34} 64)`}>
              <ellipse cx={150 + s * 34} cy="14" rx="22" ry="50" {...body} />
              <ellipse cx={150 + s * 34} cy="18" rx="10" ry="34" fill={d.belly} />
            </g>
          ))}
        {d.species === 'cat' &&
          [-1, 1].map((s) => (
            <g key={s} transform={s === 1 ? 'translate(300 0) scale(-1 1)' : undefined}>
              <path d="M64 104 L70 24 L136 56 Z" {...body} />
              <path d="M82 80 L84 46 L112 60 Z" fill={d.belly} />
            </g>
          ))}
        {d.species === 'dino' &&
          [-1, 0, 1].map((i) => (
            <path
              key={i}
              d="M-17 0 L0 -36 L17 0 Z"
              transform={`translate(${150 + i * 40} ${i === 0 ? 46 : 58}) rotate(${i * 22})`}
              {...accent}
            />
          ))}
        {d.species === 'axolotl' &&
          [-1, 1].flatMap((s) =>
            [50, 90, 130].map((a) => (
              <ellipse
                key={`${s}${a}`}
                cx="0"
                cy="-30"
                rx="11"
                ry="28"
                transform={`translate(${150 + s * 86} ${g.headCy - 6}) rotate(${s * a})`}
                {...accent}
              />
            )),
          )}

        {/* Feet, body, belly patch */}
        {[-1, 1].map((s) => (
          <g key={s}>
            <ellipse cx={150 + s * 44} cy="274" rx="31" ry="21" {...body} />
            <ellipse cx={150 + s * 46} cy="278" rx="13" ry="8" fill={d.belly} opacity=".9" />
          </g>
        ))}
        <ellipse cx="150" cy="222" rx="74" ry="66" {...body} />
        <ellipse cx="150" cy="232" rx="44" ry="42" fill={d.belly} />
        <ellipse
          cx="150"
          cy="232"
          rx="37"
          ry="35"
          fill="none"
          stroke={inkOn(d.belly)}
          strokeWidth="2.5"
          strokeDasharray="1 9"
          strokeLinecap="round"
          opacity=".45"
        />
        {/* The little sewn-in tag every proper plushie has */}
        <g transform="translate(218 246) rotate(18)">
          <rect x="0" y="0" width="22" height="15" rx="3" fill="#fff" stroke={INK} strokeWidth="3.5" />
          <path d="M6 8 h10" stroke={BLUSH} strokeWidth="3" strokeLinecap="round" />
        </g>
        {/* Arms */}
        {[-1, 1].map((s) => (
          <ellipse key={s} cx={150 + s * 68} cy="214" rx="19" ry="33" transform={`rotate(${s * 24} ${150 + s * 68} 214)`} {...body} />
        ))}

        {d.accessory === 'scarf' && (
          <>
            <path d="M178 196 L202 190 L212 244 L186 250 Z" {...accent} />
            <path d="M86 176 Q150 214 214 176 L218 198 Q150 236 82 198 Z" {...accent} />
          </>
        )}

        {/* Head */}
        <ellipse cx="150" cy={g.headCy} rx={g.headRx} ry={g.headRy} {...body} />
        {d.species === 'frog' &&
          eyes.map(([x, y]) => (
            <g key={x}>
              <circle cx={x} cy={y} r="31" {...body} />
              <circle cx={x} cy={y} r="19" fill={d.belly} />
            </g>
          ))}
        {/* Soft highlight and a forehead seam */}
        <ellipse cx="100" cy={g.headCy - 44} rx="26" ry="12" fill="#fff" opacity=".22" transform={`rotate(-28 100 ${g.headCy - 44})`} />
        {d.species !== 'dino' && d.species !== 'frog' && (
          <path
            d={`M150 ${g.headCy - g.headRy + 8} v30`}
            stroke={face}
            strokeWidth="2.5"
            strokeDasharray="1 8"
            strokeLinecap="round"
            opacity=".4"
            fill="none"
          />
        )}

        {/* Face */}
        {d.cheeks && (
          <>
            <ellipse cx={150 - g.eyeDx - 22} cy={g.mouthY - 4} rx="13" ry="8" fill={BLUSH} opacity=".6" />
            <ellipse cx={150 + g.eyeDx + 22} cy={g.mouthY - 4} rx="13" ry="8" fill={BLUSH} opacity=".6" />
          </>
        )}
        {d.species === 'bear' && <ellipse cx="150" cy="150" rx="30" ry="22" fill={d.belly} />}
        {d.species === 'cat' &&
          [-1, 1].map((s) => (
            <path
              key={s}
              d={`M${150 + s * 64} ${g.mouthY - 6} h${s * 26} M${150 + s * 64} ${g.mouthY + 6} l${s * 24} 6`}
              stroke={face}
              strokeWidth="3.5"
              strokeLinecap="round"
              opacity=".7"
            />
          ))}
        {d.species === 'dino' && (
          <>
            <circle cx="141" cy={g.mouthY - 12} r="2.6" fill={face} />
            <circle cx="159" cy={g.mouthY - 12} r="2.6" fill={face} />
          </>
        )}
        <Mouth species={d.species} style={d.mouth} y={g.mouthY} wide={wide} color={d.species === 'bear' ? inkOn(d.belly) : face} />

        <g className="plushie__eyes">
          {eyes.map(([x, y]) => (
            <Eye key={x} x={x} y={y} style={d.eyes} color={d.species === 'frog' ? inkOn(d.belly) : face} lx={lx} ly={ly} />
          ))}
        </g>

        {/* Accessories that sit on top */}
        {d.accessory === 'glasses' && (
          <g fill="#fff" fillOpacity=".28" stroke={INK} strokeWidth="5">
            <circle cx={eyes[0][0]} cy={g.eyeY} r="22" />
            <circle cx={eyes[1][0]} cy={g.eyeY} r="22" />
            <path d={`M${eyes[0][0] + 22} ${g.eyeY} Q150 ${g.eyeY - 10} ${eyes[1][0] - 22} ${g.eyeY}`} fill="none" strokeLinecap="round" />
          </g>
        )}
        {d.accessory === 'bow' && (
          <g transform="translate(150 196)" {...accent}>
            <path d="M0 0 L-32 -17 Q-39 0 -32 17 Z" />
            <path d="M0 0 L32 -17 Q39 0 32 17 Z" />
            <circle r="9" />
          </g>
        )}
        {d.accessory === 'crown' && (
          <g transform={`translate(150 ${g.headCy - g.headRy + 12})`}>
            <path d="M-36 4 L-42 -36 L-19 -17 L0 -44 L19 -17 L42 -36 L36 4 Z" {...accent} />
            <circle cx="0" cy="-10" r="5" fill={BLUSH} stroke={INK} strokeWidth="3" />
          </g>
        )}
        {d.accessory === 'flower' && (
          <g transform={`translate(${150 - g.headRx + 30} ${g.headCy - g.headRy + 26})`}>
            {[0, 72, 144, 216, 288].map((a) => (
              <circle key={a} cx="0" cy="-13" r="10" transform={`rotate(${a})`} fill={d.accessoryColor} stroke={INK} strokeWidth="4" />
            ))}
            <circle r="8" fill="#FFD84D" stroke={INK} strokeWidth="4" />
          </g>
        )}
      </g>
    </svg>
  )
}

function Eye({ x, y, style, color, lx, ly }: { x: number; y: number; style: string; color: string; lx: number; ly: number }) {
  const shine = color === INK ? '#fff' : INK
  if (style === 'sleepy')
    return <path d={`M${x - 12} ${y - 2} Q${x} ${y + 12} ${x + 12} ${y - 2}`} fill="none" stroke={color} strokeWidth="5" strokeLinecap="round" />
  if (style === 'stitched')
    return (
      <path
        d={`M${x - 9} ${y - 9} l18 18 M${x + 9} ${y - 9} l-18 18`}
        fill="none"
        stroke={color}
        strokeWidth="5"
        strokeLinecap="round"
      />
    )
  const r = style === 'sparkle' ? 15 : 11
  return (
    <g transform={`translate(${lx} ${ly})`}>
      <circle cx={x} cy={y} r={r} fill={color} />
      <circle cx={x - r * 0.3} cy={y - r * 0.35} r={r * 0.32} fill={shine} />
      {style === 'sparkle' && <circle cx={x + 5} cy={y + 6} r="2.8" fill={shine} />}
    </g>
  )
}

function Mouth({ species, style, y, wide, color }: { species: string; style: string; y: number; wide: boolean; color: string }) {
  const line = { fill: 'none', stroke: color, strokeWidth: 4.5, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const }
  const w = wide ? 30 : 12
  const hasNose = species === 'bear' || species === 'bunny' || species === 'cat'
  return (
    <>
      {hasNose && <ellipse cx="150" cy={y - 12} rx="8" ry="5.5" fill={species === 'bear' ? color : BLUSH} stroke={species === 'bear' ? 'none' : INK} strokeWidth="3" />}
      {style === 'tongue' && (
        <path d={`M143 ${y + 4} v6 a7 7 0 0 0 14 0 v-6`} fill={BLUSH} stroke={INK} strokeWidth="3.5" strokeLinejoin="round" />
      )}
      {style === 'kitty' ? (
        <path d={`M${150 - w - 4} ${y - 2} Q${150 - w / 2 - 2} ${y + 10} 150 ${y} Q${150 + w / 2 + 2} ${y + 10} ${150 + w + 4} ${y - 2}`} {...line} />
      ) : (
        <path d={`M${150 - w} ${y - 2} Q150 ${y + (wide ? 22 : 11)} ${150 + w} ${y - 2}`} {...line} />
      )}
    </>
  )
}
