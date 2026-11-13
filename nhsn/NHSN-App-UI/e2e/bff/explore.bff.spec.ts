import {expect, test} from '@playwright/test';
import {assertBffReachable, seedTestProfile} from '../support/bff';
import {chooseOption} from '../support/fields';

/**
 * TEMPORARY exploration — route B: the webpack dev server (:4300), whose `/api` already proxies
 * to the BFF on :8079, with the test profile seeded directly into localStorage instead of being
 * served from `/shell-config.js`.
 *
 * Credentials are the committed local-development keypair from `docker-compose.yml`
 * (`nhsn-app-ui` env block); the matching public certificate is on `nhsn-app-bff` as
 * `NhsnJwt__PublicCertificatePem`. Not secrets. The facility id is synthetic and local-only.
 */

/**
 * A fresh facility per run. The BFF persists the draft **server-side**, keyed on the token's
 * `facility` claim, so re-using one id makes the second run resume where the first stopped —
 * which is exactly how this spec failed the first time it was re-run. Nothing here cleans up
 * after itself yet, so each run leaves a draft behind in `link-nhsn-app-bff`.
 */
const PROFILE = {
  facilityId: `E2E-EXPLORE-${Date.now()}`,
  facilityName: 'Exploration Facility',
  email: 'e2e.explore@example.org',
  name: 'E2E Explore',
  groups: ['FACADMIN'],
  issuer: 'https://docker-nhsn-app.example.org',
  keyId: '399E34CBAADFE1B9EBF3484198B5B451C6FD3800',
  privateKeyPem:
    '-----BEGIN PRIVATE KEY-----\\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg8bBAWSvX80TrIXvT\\niuwm0MhA2Babh4aomKZtnoszZfihRANCAAQNoNqXozIM+J/ZIq6fa/vhsayZ+GlY\\nE2kQAYPpDGfo3boxro6EDvNAj5D+EbY5650CZZhBQTin3r3Ahk6/nNk2\\n-----END PRIVATE KEY-----'
};

test('explore: what a step change actually sends', async ({page, request}) => {
  await assertBffReachable(request);

  const wire: string[] = [];
  const mark = (text: string) => wire.push(text);

  page.on('request', (r) => {
    if (!r.url().includes('/api/')) return;
    const body = r.postData();
    mark(
      `--> ${r.method()} ${new URL(r.url()).pathname}` +
        (body ? `\n    BODY ${body.slice(0, 600)}` : '')
    );
  });
  page.on('response', async (r) => {
    if (!r.url().includes('/api/')) return;
    const path = new URL(r.url()).pathname;
    const headers = r.headers();
    const interesting = /test-connection|reports|acknowledg/.test(path);
    let body = '';
    if (interesting) {
      try {
        body = `\n    RESP ${(await r.text()).slice(0, 600)}`;
      } catch {
        body = '\n    RESP <unreadable>';
      }
    }
    mark(
      `<-- ${r.status()} ${path}` +
        (headers['etag'] ? `  etag=${headers['etag']}` : '') +
        body
    );
  });

  await seedTestProfile(page, PROFILE);
  await page.goto('/');

  const stepRail = page.locator('.nhsn-link__steps');
  const stepActions = page.locator('.nhsn-link__step-actions');
  const continueButton = page.getByRole('button', {name: 'Continue', exact: true});
  await expect(stepRail).toBeVisible();
  await expect(stepActions).toBeVisible();
  await page.waitForTimeout(500);

  mark('### STEP 1 -> 2 (welcome, nothing edited)');
  await continueButton.click();
  await expect(page).toHaveURL(/reporting-plan$/);
  await page.waitForTimeout(1500);

  mark('### STEP 2 -> 3 (reporting plan, nothing edited)');
  await continueButton.click();
  await expect(page).toHaveURL(/facility-info$/);
  await page.waitForTimeout(1500);

  mark('### STEP 3 -> 4 (facility info, two fields edited)');
  await chooseOption(page, 'facilityEhrVendor', 'Epic');
  await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);
  await continueButton.click();
  await expect(page).toHaveURL(/manual-upload$/);
  await page.waitForTimeout(2000);

  mark('### STEP 4 -> 5 (manual upload, nothing edited)');
  await continueButton.click();
  await expect(page).toHaveURL(/fhir$/);
  await page.waitForTimeout(1500);

  mark('### FHIR CONNECTION TEST (is the real result simulated?)');
  await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill('https://ehr.example.org/fhir/r4');
  await page.getByRole('button', {name: 'Test Connection'}).click();
  await page.waitForTimeout(4000);

  console.log('\n===== WIRE =====');
  for (const line of wire) console.log(line);
  console.log('===== END WIRE =====\n');

  const panelText = await page.locator('.nhsn-link__step-panel').innerText();
  console.log('CONNECTION RESULT ON SCREEN:', panelText.slice(0, 600));

  expect(true).toBeTruthy();
});
