/**
 * The currency amounts are rendered in when the amount itself does not carry one.
 *
 * There is deliberately no "tenant default currency" to read here: the `tenants` table has no
 * currency column, `/api/settings/*` exposes none (its company payload carries only a `vatNumber`
 * identifier), and the only stored default — `/api/platform/settings/tax-finance` — sits behind the
 * `PlatformSettingsAccess` policy and is unreadable by a tenant user. The tenant's configured
 * **country** is the one piece of workspace configuration that determines its currency, so it is
 * what seeds the active currency, exactly as the tenant's time zone seeds `timezone.ts`.
 *
 * Records that carry their own currency (invoices, quotes, credit notes, contracts, preferential
 * rates) must still pass it explicitly — this is the fallback for aggregates and unit costs that
 * have no currency of their own, not a substitute for one that does.
 */

/** Used before a session exists, and when the tenant's country maps to no known currency. */
export const FALLBACK_CURRENCY = 'KES'

/**
 * ISO 4217 codes keyed by the tenant `country` spellings the app can produce. `country` is a
 * free-text field (platform tenant creation and the Company Profile settings form both accept any
 * string), so both country names and ISO 3166 alpha-2/alpha-3 codes are accepted, and anything
 * unrecognised falls back rather than guessing.
 */
const CURRENCY_BY_COUNTRY: Readonly<Record<string, string>> = {
  // ── East Africa ──
  kenya: 'KES', ke: 'KES', ken: 'KES',
  uganda: 'UGX', ug: 'UGX', uga: 'UGX',
  tanzania: 'TZS', tz: 'TZS', tza: 'TZS', unitedrepublicoftanzania: 'TZS',
  rwanda: 'RWF', rw: 'RWF', rwa: 'RWF',
  burundi: 'BIF', bi: 'BIF', bdi: 'BIF',
  ethiopia: 'ETB', et: 'ETB', eth: 'ETB',
  somalia: 'SOS', so: 'SOS', som: 'SOS',
  southsudan: 'SSP', ss: 'SSP', ssd: 'SSP',
  sudan: 'SDG', sd: 'SDG', sdn: 'SDG',
  eritrea: 'ERN', er: 'ERN', eri: 'ERN',
  djibouti: 'DJF', dj: 'DJF', dji: 'DJF',
  // ── Southern Africa ──
  southafrica: 'ZAR', za: 'ZAR', zaf: 'ZAR',
  namibia: 'NAD', na: 'NAD', nam: 'NAD',
  botswana: 'BWP', bw: 'BWP', bwa: 'BWP',
  zambia: 'ZMW', zm: 'ZMW', zmb: 'ZMW',
  zimbabwe: 'ZWG', zw: 'ZWG', zwe: 'ZWG',
  malawi: 'MWK', mw: 'MWK', mwi: 'MWK',
  mozambique: 'MZN', mz: 'MZN', moz: 'MZN',
  angola: 'AOA', ao: 'AOA', ago: 'AOA',
  lesotho: 'LSL', ls: 'LSL', lso: 'LSL',
  eswatini: 'SZL', sz: 'SZL', swz: 'SZL', swaziland: 'SZL',
  mauritius: 'MUR', mu: 'MUR', mus: 'MUR',
  // ── West and Central Africa ──
  nigeria: 'NGN', ng: 'NGN', nga: 'NGN',
  ghana: 'GHS', gh: 'GHS', gha: 'GHS',
  sierraleone: 'SLE', sl: 'SLE', sle: 'SLE',
  liberia: 'LRD', lr: 'LRD', lbr: 'LRD',
  gambia: 'GMD', gm: 'GMD', gmb: 'GMD',
  guinea: 'GNF', gn: 'GNF', gin: 'GNF',
  senegal: 'XOF', sn: 'XOF', sen: 'XOF',
  ivorycoast: 'XOF', cotedivoire: 'XOF', ci: 'XOF', civ: 'XOF',
  mali: 'XOF', ml: 'XOF', mli: 'XOF',
  burkinafaso: 'XOF', bf: 'XOF', bfa: 'XOF',
  benin: 'XOF', bj: 'XOF', ben: 'XOF',
  togo: 'XOF', tg: 'XOF', tgo: 'XOF',
  niger: 'XOF', ne: 'XOF', ner: 'XOF',
  cameroon: 'XAF', cm: 'XAF', cmr: 'XAF',
  gabon: 'XAF', ga: 'XAF', gab: 'XAF',
  chad: 'XAF', td: 'XAF', tcd: 'XAF',
  congo: 'XAF', cg: 'XAF', cog: 'XAF',
  drcongo: 'CDF', democraticrepublicofthecongo: 'CDF', cd: 'CDF', cod: 'CDF',
  // ── North Africa and the Middle East ──
  egypt: 'EGP', eg: 'EGP', egy: 'EGP',
  morocco: 'MAD', ma: 'MAD', mar: 'MAD',
  tunisia: 'TND', tn: 'TND', tun: 'TND',
  algeria: 'DZD', dz: 'DZD', dza: 'DZD',
  libya: 'LYD', ly: 'LYD', lby: 'LYD',
  unitedarabemirates: 'AED', uae: 'AED', ae: 'AED', are: 'AED',
  saudiarabia: 'SAR', sa: 'SAR', sau: 'SAR',
  qatar: 'QAR', qa: 'QAR', qat: 'QAR',
  oman: 'OMR', om: 'OMR', omn: 'OMR',
  kuwait: 'KWD', kw: 'KWD', kwt: 'KWD',
  bahrain: 'BHD', bh: 'BHD', bhr: 'BHD',
  // ── Rest of world ──
  unitedstates: 'USD', unitedstatesofamerica: 'USD', usa: 'USD', us: 'USD',
  unitedkingdom: 'GBP', greatbritain: 'GBP', uk: 'GBP', gb: 'GBP', gbr: 'GBP',
  ireland: 'EUR', ie: 'EUR', irl: 'EUR',
  germany: 'EUR', de: 'EUR', deu: 'EUR',
  france: 'EUR', fr: 'EUR', fra: 'EUR',
  netherlands: 'EUR', nl: 'EUR', nld: 'EUR',
  spain: 'EUR', es: 'EUR', esp: 'EUR',
  italy: 'EUR', it: 'EUR', ita: 'EUR',
  portugal: 'EUR', pt: 'EUR', prt: 'EUR',
  canada: 'CAD', ca: 'CAD', can: 'CAD',
  australia: 'AUD', au: 'AUD', aus: 'AUD',
  newzealand: 'NZD', nz: 'NZD', nzl: 'NZD',
  india: 'INR', in: 'INR', ind: 'INR',
  pakistan: 'PKR', pk: 'PKR', pak: 'PKR',
  china: 'CNY', cn: 'CNY', chn: 'CNY',
  japan: 'JPY', jp: 'JPY', jpn: 'JPY',
  singapore: 'SGD', sg: 'SGD', sgp: 'SGD',
  switzerland: 'CHF', ch: 'CHF', che: 'CHF',
  turkey: 'TRY', tr: 'TRY', tur: 'TRY',
  brazil: 'BRL', br: 'BRL', bra: 'BRL',
}

/** "Côte d'Ivoire", "cote d ivoire" and "COTEDIVOIRE" all collapse to `cotedivoire`. */
function normalizeCountryKey(country: string): string {
  return country
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z]/gi, '')
    .toLowerCase()
}

/** The ISO 4217 code for a tenant country, or null when the country maps to nothing known. */
export function currencyForCountry(country: string | null | undefined): string | null {
  if (!country?.trim()) return null
  return CURRENCY_BY_COUNTRY[normalizeCountryKey(country)] ?? null
}

let activeCurrency = FALLBACK_CURRENCY

/**
 * Sets the workspace currency for the session, alongside `setActiveTimeZone`.
 *
 * `configured` is the tenant's own setting, carried on the session payload so every role receives
 * it — the settings endpoint that owns it is admin-only, so reading it there would make the same
 * invoice render differently depending on who is looking. `country` is the fallback for workspaces
 * that have not set one, and only then does the hardcoded default apply.
 */
export function setActiveCurrency(configured: string | null | undefined, country: string | null | undefined) {
  const normalized = typeof configured === 'string' ? configured.trim().toUpperCase() : ''
  activeCurrency = (normalized.length === 3 ? normalized : null) ?? currencyForCountry(country) ?? FALLBACK_CURRENCY
}

/** The currency to render an amount in when the amount carries none of its own. */
export function getActiveCurrency() {
  return activeCurrency
}
