import { money, shortDate } from '../lib/api'
import type { Resource } from './shared'

const yes = (b: boolean) => <span className={`chip ${b ? 'chip--mint' : 'chip--muted'}`}>{b ? 'Yes' : 'No'}</span>
const inDays = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString()

// Everything here is plain configuration for the generic ResourcePage: which endpoint,
// which columns to list, and which fields the form shows.
export const RESOURCES: Record<string, Resource> = {
  promotions: {
    path: '/api/admin/promotions', title: 'Promotions', singular: 'promotion',
    blurb: 'Timed percentage discounts on chosen variants. The best running promotion is applied automatically.',
    columns: [
      { label: 'Title', render: (r) => <b>{r.title}</b> },
      { label: 'Discount', render: (r) => `${r.discountPercent}%` },
      { label: 'Runs', render: (r) => `${shortDate(r.startDate)} → ${shortDate(r.endDate)}` },
      { label: 'Variants', render: (r) => r.variantIds.length },
      { label: 'On home page', render: (r) => yes(r.displayHomePage) },
    ],
    fields: [
      { key: 'title', label: 'Title', wide: true },
      { key: 'description', label: 'Description', type: 'textarea', wide: true },
      { key: 'discountPercent', label: 'Discount %', type: 'number' },
      { key: 'maxDisplayNumber', label: 'Max products shown', type: 'number' },
      { key: 'startDate', label: 'Starts', type: 'datetime' },
      { key: 'endDate', label: 'Ends', type: 'datetime' },
      { key: 'displayHomePage', label: 'Show on the home page', type: 'checkbox', wide: true },
      { key: 'variantIds', label: 'Variants on promotion', type: 'multiselect', wide: true, optionsFrom: { path: '/api/admin/variants', value: 'id', label: 'label' } },
    ],
    blank: () => ({ title: '', description: '', discountPercent: 10, maxDisplayNumber: 8, startDate: inDays(0), endDate: inDays(14), displayHomePage: true, variantIds: [] }),
  },
  coupons: {
    path: '/api/admin/coupons', title: 'Coupons', singular: 'coupon',
    blurb: 'Codes customers type in at the cart.',
    columns: [
      { label: 'Code', render: (r) => <code>{r.code}</code> },
      { label: 'Discount', render: (r) => (r.discountType === 'percent' ? `${r.discountValue}%` : money(r.discountValue)) },
      { label: 'Min. spend', render: (r) => (r.minSubtotalCents ? money(r.minSubtotalCents) : '—') },
      { label: 'Used', render: (r) => `${r.timesRedeemed}${r.maxRedemptions ? ` / ${r.maxRedemptions}` : ''}` },
      { label: 'Active', render: (r) => yes(r.active) },
    ],
    fields: [
      { key: 'code', label: 'Code', hint: 'Letters, numbers and dashes. Stored in capitals.' },
      { key: 'discountType', label: 'Type', type: 'select', options: [{ value: 'percent', label: 'Percent off' }, { value: 'fixed', label: 'Fixed amount off (in cents)' }] },
      { key: 'discountValue', label: 'Value', type: 'number', hint: 'A percentage (10 = 10%) or cents (500 = $5.00).' },
      { key: 'minSubtotalCents', label: 'Minimum spend ($)', type: 'money' },
      { key: 'startsAt', label: 'Starts (optional)', type: 'datetime' },
      { key: 'endsAt', label: 'Ends (optional)', type: 'datetime' },
      { key: 'maxRedemptions', label: 'Max uses (optional)', type: 'number', nullable: true },
      { key: 'description', label: 'Description shown to customers', wide: true, nullable: true },
      { key: 'active', label: 'Active', type: 'checkbox', wide: true },
    ],
    blank: () => ({ code: '', discountType: 'percent', discountValue: 10, minSubtotalCents: 0, startsAt: null, endsAt: null, maxRedemptions: null, description: '', active: true }),
  },
  categories: {
    path: '/api/admin/categories', title: 'Categories', singular: 'category',
    blurb: 'How the shop is organised. A category can sit inside a parent.',
    columns: [
      { label: 'Title', render: (r) => <b>{r.title}</b> },
      { label: 'Slug', render: (r) => <code>{r.slug}</code> },
      { label: 'Description', render: (r) => r.description },
    ],
    fields: [
      { key: 'title', label: 'Title' },
      { key: 'slug', label: 'Slug (optional)', hint: 'Leave empty to make one from the title.', nullable: true },
      { key: 'parentCategoryId', label: 'Parent category', type: 'select', nullable: true, optionsFrom: { path: '/api/admin/categories', value: 'id', label: 'title' } },
      { key: 'variantTypeIds', label: 'Variant types used in this category', type: 'multiselect', optionsFrom: { path: '/api/admin/variant-types', value: 'id', label: 'title' } },
      { key: 'description', label: 'Description', type: 'textarea', wide: true },
    ],
    blank: () => ({ title: '', slug: '', parentCategoryId: null, description: '', variantTypeIds: [] }),
  },
  brands: {
    path: '/api/admin/brands', title: 'Brands', singular: 'brand', blurb: 'Who made it: your own line, or guest makers.',
    columns: [{ label: 'Name', render: (r) => <b>{r.name}</b> }, { label: 'Description', render: (r) => r.description }],
    fields: [{ key: 'name', label: 'Name', wide: true }, { key: 'description', label: 'Description', type: 'textarea', wide: true }],
    blank: () => ({ name: '', description: '' }),
  },
  'variant-types': {
    path: '/api/admin/variant-types', title: 'Variant types', singular: 'variant type',
    blurb: 'What a product can vary by. The type decides how the picker looks on the product page.',
    columns: [{ label: 'Title', render: (r) => <b>{r.title}</b> }, { label: 'Picker', render: (r) => r.type }],
    fields: [
      { key: 'title', label: 'Title' },
      { key: 'type', label: 'Picker style', type: 'select', options: [{ value: 'color', label: 'Colour swatches' }, { value: 'size', label: 'Size pills' }, { value: 'text', label: 'Plain text' }] },
    ],
    blank: () => ({ title: '', type: 'color' }),
  },
  'shipping-methods': {
    path: '/api/admin/shipping-methods', title: 'Shipping methods', singular: 'shipping method',
    blurb: 'What customers are charged at checkout. The real postage is bought per order through Chit Chats.',
    columns: [
      { label: 'Name', render: (r) => <b>{r.name}</b> },
      { label: 'Country', render: (r) => (r.countryCode === '*' ? 'Everywhere else' : r.countryCode) },
      { label: 'Price', render: (r) => money(r.priceCents) },
      { label: 'Free over', render: (r) => (r.freeOverCents ? money(r.freeOverCents) : '—') },
      { label: 'Chit Chats postage', render: (r) => (r.chitChatsPostageType ? <code>{r.chitChatsPostageType}</code> : '—') },
      { label: 'Active', render: (r) => yes(r.active) },
    ],
    fields: [
      { key: 'name', label: 'Name' },
      { key: 'countryCode', label: 'Country code', hint: '2 letters (CA, US…) or * for everywhere else.' },
      { key: 'priceCents', label: 'Price ($)', type: 'money' },
      { key: 'freeOverCents', label: 'Free when the order is over ($)', type: 'money', nullable: true },
      { key: 'estimatedDays', label: 'Delivery estimate', nullable: true },
      { key: 'chitChatsPostageType', label: 'Preferred Chit Chats postage_type', hint: 'e.g. chit_chats_canada_tracked. Pre-selected when buying postage.', nullable: true },
      { key: 'description', label: 'Description', wide: true, nullable: true },
      { key: 'displayOrder', label: 'Display order', type: 'number' },
      { key: 'active', label: 'Active', type: 'checkbox' },
    ],
    blank: () => ({ name: '', countryCode: 'CA', priceCents: 0, freeOverCents: null, estimatedDays: '', chitChatsPostageType: '', description: '', displayOrder: 0, active: true }),
  },
  'tax-rates': {
    path: '/api/admin/tax-rates', title: 'Tax rates', singular: 'tax rate',
    blurb: 'Sales tax by destination. Applied to goods and shipping. Check these with your accountant.',
    columns: [
      { label: 'Country', render: (r) => r.countryCode },
      { label: 'Province', render: (r) => r.provinceCode ?? 'All' },
      { label: 'Label', render: (r) => r.label },
      { label: 'Rate', render: (r) => `${r.ratePercent}%` },
    ],
    fields: [
      { key: 'countryCode', label: 'Country code' },
      { key: 'provinceCode', label: 'Province/state code (optional)', nullable: true },
      { key: 'label', label: 'Label on receipts' },
      { key: 'ratePercent', label: 'Rate %', type: 'number' },
    ],
    blank: () => ({ countryCode: 'CA', provinceCode: '', label: '', ratePercent: 0 }),
  },
  posts: {
    path: '/api/admin/posts', title: 'Journal posts', singular: 'post', blurb: 'Stories and announcements shown in the journal and on the home page.',
    columns: [
      { label: 'Title', render: (r) => <b>{r.title}</b> },
      { label: 'Published', render: (r) => yes(r.published) },
      { label: 'Created', render: (r) => shortDate(r.createdAt) },
    ],
    fields: [
      { key: 'title', label: 'Title', wide: true },
      { key: 'postTypeId', label: 'Type', type: 'select', optionsFrom: { path: '/api/admin/post-types', value: 'id', label: 'title' } },
      { key: 'slug', label: 'Slug (optional)', nullable: true },
      { key: 'excerpt', label: 'Excerpt', type: 'textarea', wide: true },
      { key: 'body', label: 'Body (HTML)', type: 'html', wide: true, hint: 'Headings, paragraphs, lists, links and images. Scripts are stripped when shown.' },
      { key: 'bannerImage', label: 'Banner image', type: 'image', wide: true },
      { key: 'published', label: 'Published', type: 'checkbox', wide: true },
    ],
    blank: () => ({ title: '', postTypeId: 0, slug: '', excerpt: '', body: '', bannerImage: '', published: true }),
  },
  'post-types': {
    path: '/api/admin/post-types', title: 'Post types', singular: 'post type', blurb: 'Labels for journal posts.',
    columns: [{ label: 'Title', render: (r) => <b>{r.title}</b> }],
    fields: [{ key: 'title', label: 'Title', wide: true }],
    blank: () => ({ title: '' }),
  },
  'store-images': {
    path: '/api/admin/store-images', title: 'Store images', singular: 'image', blurb: 'Banners and tiles for storefront pages. Each slot type has a size and a maximum count.',
    columns: [
      { label: 'Image', render: (r) => <img src={r.imageAddress} alt="" className="table__thumb" /> },
      { label: 'Title', render: (r) => r.title },
      { label: 'Link', render: (r) => r.link },
    ],
    fields: [
      { key: 'storePageId', label: 'Page', type: 'select', optionsFrom: { path: '/api/admin/content/pages', value: 'id', label: 'name' } },
      { key: 'storeImageTypeId', label: 'Slot type', type: 'select', optionsFrom: { path: '/api/admin/store-image-types', value: 'id', label: 'name' } },
      { key: 'imageAddress', label: 'Image', type: 'image', wide: true },
      { key: 'title', label: 'Title', nullable: true },
      { key: 'intro', label: 'Intro text', nullable: true },
      { key: 'link', label: 'Link', nullable: true },
      { key: 'linkText', label: 'Link text', nullable: true },
      { key: 'displayOrder', label: 'Display order', type: 'number' },
    ],
    blank: () => ({ storePageId: 0, storeImageTypeId: 0, imageAddress: '', title: '', intro: '', link: '', linkText: '', displayOrder: 0 }),
  },
}
