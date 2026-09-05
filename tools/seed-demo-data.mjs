/**
 * Fills the catalogue so the register grid, the product search and the barcode lookup have
 * something to work against. Five products is enough to prove the till works and not enough to
 * see whether it holds up.
 *
 *   node tools/seed-demo-data.mjs
 *
 * Environment overrides:
 *
 *   POSFLOW_API_URL   default http://localhost:5100/api/v1
 *   POSFLOW_USERNAME  default admin
 *   POSFLOW_PASSWORD  default Admin@123
 *
 * Goes through the HTTP API rather than SQL, so validation and domain rules apply and this cannot
 * create a product the application itself would reject.
 *
 * Re-running is safe: every product carries a barcode, which is a natural key, so the API rejects
 * a second insert and this counts it rather than treating it as a failure. That is why the seed
 * data below deliberately has barcodes on everything.
 */

const API = process.env.POSFLOW_API_URL ?? 'http://localhost:5100/api/v1';
const USERNAME = process.env.POSFLOW_USERNAME ?? 'admin';
const PASSWORD = process.env.POSFLOW_PASSWORD ?? 'Admin@123';

let token = '';
const stats = { created: 0, existed: 0, failed: [] };

async function call(method, path, body) {
  let res;
  try {
    res = await fetch(API + path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch (cause) {
    throw new Error(`Could not reach the API at ${API}. Is it running?`, { cause });
  }
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch {}
  return { ok: res.ok, status: res.status, json, text };
}

// Arabic first, because the app is Arabic first; NameEn is what the English toggle shows.
// The last item deliberately does not track stock, so that path is represented too.
const PRODUCTS = [
  ['كابتشينو',            'Cappuccino',           '6221031492015', 45,  true,  120],
  ['لاتيه',                'Latte',                '6221031492022', 50,  true,  120],
  ['إسبريسو دوبل',         'Double Espresso',      '6221031492039', 35,  true,  200],
  ['شاي بالنعناع',         'Mint Tea',             '6221031492046', 20,  true,  150],
  ['كرواسون سادة',         'Plain Croissant',      '6221031492053', 30,  true,  40],
  ['كرواسون شوكولاتة',     'Chocolate Croissant',  '6221031492060', 38,  true,  35],
  ['تشيز كيك',             'Cheesecake',           '6221031492077', 65,  true,  18],
  ['براوني',               'Brownie',              '6221031492084', 45,  true,  22],
  ['عصير مانجو',           'Mango Juice',          '6221031492091', 40,  true,  60],
  ['عصير ليمون بالنعناع',  'Lemon Mint',           '6221031492107', 35,  true,  60],
  ['مياه معدنية صغيرة',    'Water 600ml',          '6221031492114', 10,  true,  300],
  ['مشروب غازي',           'Soft Drink',           '6221031492121', 25,  true,  180],
  ['سلطة سيزر',            'Caesar Salad',         '6221031492138', 95,  true,  12],
  ['ساندوتش تونة',         'Tuna Sandwich',        '6221031492145', 70,  true,  16],
  ['ساندوتش حلومي',        'Halloumi Sandwich',    '6221031492152', 80,  true,  14],
  ['طبق فطار',             'Breakfast Plate',      '6221031492169', 120, true,  10],
  ['كيس بن 250 جم',        'Coffee Beans 250g',    '6221031492176', 180, true,  8],
  ['كوب سفري',             'Travel Mug',           '6221031492183', 150, false, 0],
];

async function main() {
  const login = await call('POST', '/auth/login', { username: USERNAME, password: PASSWORD });
  if (!login.ok) {
    throw new Error(`Login failed for ${USERNAME} (${login.status}). ${login.text.slice(0, 180)}`);
  }
  token = login.json.accessToken ?? login.json.token;
  if (!token) throw new Error('Login returned no token: ' + JSON.stringify(login.json).slice(0, 200));

  for (const [nameAr, nameEn, barcode, price, trackStock, stockQuantity] of PRODUCTS) {
    const r = await call('POST', '/products', {
      nameAr, nameEn, barcode, price, categoryId: null, trackStock, stockQuantity,
    });
    if (r.ok) stats.created++;
    // 409 is the barcode uniqueness check rejecting a row that is already there, which is the
    // whole point of giving every product a barcode. Matching on the message text instead was a
    // mistake: the API answers in Arabic, so an English keyword list quietly reported a clean
    // re-run as 18 failures.
    else if (r.status === 409) stats.existed++;
    else stats.failed.push(`${nameEn}: ${r.status} ${r.text.slice(0, 140)}`);
  }

  console.log(`created ${stats.created}, already present ${stats.existed}, failed ${stats.failed.length}`);
  stats.failed.slice(0, 10).forEach(f => console.log('  ! ' + f));
  if (stats.failed.length) process.exitCode = 1;
}

main().catch(err => {
  console.error(err.message);
  if (err.cause) console.error('  cause:', err.cause.message);
  process.exit(1);
});
