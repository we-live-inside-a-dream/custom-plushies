// Shapes returned by the API (see backend/src/Plushies.Api/Features).

export type PlushieDesign = {
  species: string
  body: string
  belly: string
  eyes: string
  mouth: string
  cheeks: boolean
  accessory: string
  accessoryColor: string
}

export type Image = { id: number; url: string; thumbnailUrl: string | null }

export type Variant = {
  id: number
  value: string
  sku: string
  typeTitle: string
  typeKind: 'color' | 'size' | 'text'
  colorHex: string | null
  design: PlushieDesign | null
  priceCents: number
  salePriceCents: number
  stock: number
  images: Image[]
}

export type ProductSummary = {
  id: number
  slug: string
  name: string
  brand: string | null
  currency: string
  tags: string[]
  category: string | null
  categorySlug: string | null
  fromPriceCents: number
  fromSalePriceCents: number
  promoPercent: number
  rating: number
  reviewCount: number
  inStock: boolean
  variants: Variant[]
}

export type Review = { id: number; author: string; rating: number; comment: string | null; createdAt: string }

export type ProductDetail = {
  summary: ProductSummary
  description: string | null
  information: string | null
  reviews: Review[]
  related: ProductSummary[]
}

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number }

export type Category = { id: number; title: string; slug: string; description: string | null; productCount: number; children: Category[] }

export type Promotion = {
  id: number
  title: string
  description: string | null
  discountPercent: number
  endDate: string
  products: ProductSummary[]
}

export type CartLine = {
  id: number
  kind: 'product' | 'custom'
  name: string
  variantName: string | null
  slug: string | null
  design: PlushieDesign | null
  imageUrl: string | null
  embroideryText: string | null
  unitPriceCents: number
  compareAtCents: number
  quantity: number
  lineTotalCents: number
  maxQuantity: number | null
  available: boolean
}

export type Cart = {
  token: string | null
  items: CartLine[]
  itemCount: number
  subtotalCents: number
  discountCents: number
  coupon: { code: string; description: string | null; discountCents: number; problem: string | null } | null
  currency: string
}

export type BuilderOption = { id: string; label: string; priceCents: number }
export type BuilderOptions = {
  species: BuilderOption[]
  sizes: { id: string; label: string; heightCm: number; multiplier: number; weightGrams: number }[]
  fabrics: { name: string; hex: string }[]
  eyes: BuilderOption[]
  mouths: BuilderOption[]
  accessories: BuilderOption[]
  embroideryPriceCents: number
  embroideryMaxLength: number
}

export type Customer = { id: number; email: string; firstName: string; lastName: string; phone: string | null }

export type Address = {
  id: number
  recipientName: string
  address1: string
  address2: string | null
  city: string
  provinceCode: string | null
  postalCode: string | null
  countryCode: string
  phone: string | null
  isDefault: boolean
}

export type ShippingOption = { id: number; name: string; description: string | null; estimatedDays: string | null; priceCents: number; free: boolean }
export type Totals = { subtotalCents: number; discountCents: number; shippingCents: number; taxCents: number; taxLabel: string | null; totalCents: number }

export type OrderItem = {
  id: number
  productName: string
  variantName: string | null
  sku: string | null
  design: PlushieDesign | null
  imageUrl: string | null
  quantity: number
  unitPriceCents: number
  totalCents: number
  isCustom: boolean
}

export type OrderStatus = 'pending_payment' | 'paid' | 'in_production' | 'shipped' | 'delivered' | 'cancelled' | 'refunded'

export type Order = {
  id: number
  orderNumber: string
  status: OrderStatus
  currency: string
  subtotalCents: number
  discountCents: number
  shippingCents: number
  taxCents: number
  taxLabel: string | null
  totalCents: number
  couponCode: string | null
  shippingMethodName: string
  email: string
  shipTo: { name: string; address1: string; address2: string | null; city: string; provinceCode: string | null; postalCode: string | null; countryCode: string; phone: string | null }
  customerNote: string | null
  createdAt: string
  paidAt: string | null
  items: OrderItem[]
  shipments: { id: number; status: string; carrier: string | null; trackingCode: string | null; trackingUrl: string | null; shipDate: string | null }[]
}

export type StorePage = {
  name: string
  texts: Record<string, string>
  images: { id: number; type: string; width: number; height: number; title: string | null; intro: string | null; link: string | null; linkText: string | null; imageAddress: string }[]
}

export type PostSummary = { id: number; slug: string; title: string; type: string; excerpt: string | null; bannerImage: string | null; createdAt: string }
export type Post = PostSummary & { body: string; author: string | null }
