// k6 load test for PosFlow's API. See tests/load/README.md for how to
// run this and what it does/doesn't cover.
//
// Covers the read-heavy paths a real shift hammers hardest (login,
// product catalog) plus a small, low-VU checkout scenario - see the
// README for why checkout can't be scaled to many VUs against the
// stock demo seed data without seeding extra cashier accounts first.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const BASE_URL = __ENV.POSFLOW_BASE_URL || 'https://localhost:5443';
const USERNAME = __ENV.POSFLOW_USERNAME || 'cashier';
const PASSWORD = __ENV.POSFLOW_PASSWORD || 'Cashier@123';

const loginFailures = new Counter('posflow_login_failures');
const checkoutFailures = new Counter('posflow_checkout_failures');

export const options = {
  scenarios: {
    // Simulates a busy shift browsing the catalog - the exact
    // scenario ENTERPRISE-READINESS.md flagged as having no load
    // test and no caching on the products list.
    browse_catalog: {
      executor: 'ramping-vus',
      exec: 'browseCatalog',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '20s', target: 0 }
      ]
    },

    // Deliberately low and constant: the demo seed data has exactly
    // one cashier account, and PosFlow allows only one OPEN shift per
    // (tenant, branch, user) at a time, so this can't be scaled to
    // many concurrent VUs without seeding additional cashier accounts
    // first (see README). Still useful for a sanity check of checkout
    // latency under a small steady load.
    checkout: {
      executor: 'constant-vus',
      exec: 'checkout',
      vus: 1,
      duration: '1m',
      startTime: '5s'
    }
  },

  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{scenario:browse_catalog}': ['p(95)<800']
  }
};

function login() {
  const response = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ username: USERNAME, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  const ok = check(response, {
    'login succeeded': r => r.status === 200,
    'login returned an access token': r =>
      !!r.json('accessToken')
  });

  if (!ok) {
    loginFailures.add(1);
    return null;
  }

  return response.json('accessToken');
}

function authHeaders(token) {
  return { headers: { Authorization: `Bearer ${token}` } };
}

export function browseCatalog() {
  const token = login();

  if (!token) {
    sleep(1);
    return;
  }

  const productsResponse = http.get(
    `${BASE_URL}/api/products?page=1&pageSize=50`,
    authHeaders(token)
  );

  check(productsResponse, {
    'product list succeeded': r => r.status === 200
  });

  sleep(Math.random() * 2 + 1);
}

export function checkout() {
  const token = login();

  if (!token) {
    sleep(1);
    return;
  }

  const headers = authHeaders(token);

  // Best-effort: a shift may already be open from a previous
  // iteration/run, which is fine - "already open" is not a failure
  // for this script's purpose.
  http.post(
    `${BASE_URL}/api/shifts/open`,
    JSON.stringify({ openingCash: 500 }),
    { headers: { ...headers.headers, 'Content-Type': 'application/json' } }
  );

  const productsResponse = http.get(
    `${BASE_URL}/api/products?page=1&pageSize=10`,
    headers
  );

  const products = productsResponse.json('items') || [];

  if (products.length === 0) {
    sleep(1);
    return;
  }

  const product = products[0];

  const checkoutResponse = http.post(
    `${BASE_URL}/api/orders/checkout`,
    JSON.stringify({
      lines: [{ productId: product.id, quantity: 1, discountAmount: 0 }],
      payments: [{ method: 1, amount: product.price, referenceNumber: null }]
    }),
    { headers: { ...headers.headers, 'Content-Type': 'application/json' } }
  );

  const ok = check(checkoutResponse, {
    'checkout succeeded': r => r.status === 201
  });

  if (!ok) {
    checkoutFailures.add(1);
  }

  sleep(Math.random() * 3 + 2);
}
