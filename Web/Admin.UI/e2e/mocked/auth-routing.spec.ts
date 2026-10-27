import { test, expect } from '../support/test';
import { NavPage } from '../support/pages/nav.page';

test.describe('routing with auth disabled', () => {
  test('root redirects through /login to the dashboard', async ({ page }) => {
    await page.goto('/');
    // '' -> /login (route redirect), then LoginComponent navigates to /dashboard
    // because authRequired is false.
    await page.waitForURL('**/dashboard');
    await new NavPage(page).expectShellVisible();
  });

  test('unknown routes fall through to the dashboard', async ({ page }) => {
    await page.goto('/no-such-route');
    await page.waitForURL('**/dashboard');
    await expect(new NavPage(page).toolbarTitle).toBeVisible();
  });
});

test.describe('routing with auth required', () => {
  test.use({ appConfig: { authRequired: true } });

  test('guard bounces an unauthenticated visit to login, which challenges the BFF', async ({ page, api }) => {
    // No session profile; the guard calls GET /api/user, which rejects.
    api.mock('GET /api/user', { status: 401 });
    // LoginComponent then sends the browser to the BFF challenge endpoint; intercept
    // it so the test does not depend on a running BFF/IdP.
    await page.route('**/api/login', (route) =>
      route.fulfill({ contentType: 'text/html', body: '<html><body>IdP sign-in placeholder</body></html>' }),
    );

    await page.goto('/tenant');
    await page.waitForURL('**/api/login');

    // The guard stored where to return to after login.
    expect(await page.evaluate(() => sessionStorage.getItem('returnUrl'))).toBe('/tenant');
  });
});
