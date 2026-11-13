import {expect, test} from '@playwright/test';
import {assertBffReachable, seedTestProfile} from '../support/bff';
import {chooseOption, typeDate} from '../support/fields';

/**
 * TEMPORARY exploration — can a facility reach step 11 against the REAL BFF, and does the
 * report it just requested appear in `listReports`? That is the premise of LEGLINK-1771 and of
 * the demo walkthrough's deliberate seam, both of which assume it cannot.
 *
 * Deliberately light on assertions: this is discovery. It logs where it got to and what the
 * wire said, and a failure is information rather than a defect.
 */

const PROFILE = {
  facilityId: `E2E-WALK-${Date.now()}`,
  facilityName: 'Walk Facility',
  email: 'e2e.walk@example.org',
  name: 'E2E Walk',
  groups: ['FACADMIN'],
  issuer: 'https://docker-nhsn-app.example.org',
  keyId: '399E34CBAADFE1B9EBF3484198B5B451C6FD3800',
  privateKeyPem:
    '-----BEGIN PRIVATE KEY-----\\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg8bBAWSvX80TrIXvT\\niuwm0MhA2Babh4aomKZtnoszZfihRANCAAQNoNqXozIM+J/ZIq6fa/vhsayZ+GlY\\nE2kQAYPpDGfo3boxro6EDvNAj5D+EbY5650CZZhBQTin3r3Ahk6/nNk2\\n-----END PRIVATE KEY-----'
};

/**
 * The HAPI test server, as the BFF container sees it — verified reachable on /metadata.
 * Override with E2E_FHIR_URL to point at a hosted test EHR instead.
 */
const FHIR_URL = process.env.E2E_FHIR_URL ?? 'http://172.18.0.17:8080/fhir';

/** Six real Epic-style patient-list ids, if the target EHR has any. */
const LIST_IDS = (process.env.E2E_LIST_IDS ?? '').split(',').map((v) => v.trim()).filter(Boolean);

/**
 * Watchable with `--headed`: E2E_SLOW_MO paces every action (try 400), and E2E_KEEP_OPEN=1 holds
 * the window on step 13 instead of tearing it down. Both default off so CI runs at full speed.
 */
const KEEP_OPEN = process.env.E2E_KEEP_OPEN === '1';

test.use({
  launchOptions: {slowMo: Number(process.env.E2E_SLOW_MO ?? 0)},
  video: process.env.E2E_VIDEO === '1' ? 'on' : 'off'
});

/** Where the demo stills land. Anything outside the repo is fine. */
const SHOTS = process.env.E2E_SHOT_DIR;

/** Skips the LEGLINK-1775 workaround, so step 10's empty measure picker can be shown. */
const SKIP_ENROLL_FIX = process.env.E2E_SKIP_ENROLL_FIX === '1';

test('walk: how far does a facility get against the real BFF', async ({page, request}) => {
  test.setTimeout(KEEP_OPEN ? 0 : 300_000);
  await assertBffReachable(request);

  const wire: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('/api/')) wire.push(`--> ${r.method()} ${new URL(r.url()).pathname}`);
  });
  page.on('response', async (r) => {
    if (!r.url().includes('/api/')) return;
    const path = new URL(r.url()).pathname;
    let body = '';
    if (/reports|acknowledg|measures|test-connection|vendors|list-queries/.test(path)) {
      try {
        body = `\n    RESP ${(await r.text()).slice(0, 1600)}`;
      } catch {
        body = '\n    RESP <unreadable>';
      }
    }
    wire.push(`<-- ${r.status()} ${path}${body}`);
  });

  const reached: string[] = [];
  const note = async (label: string) => {
    reached.push(`${label}  @ ${new URL(page.url()).pathname}`);
  };

  await seedTestProfile(page, PROFILE);
  await page.goto('/');

  const continueButton = page.getByRole('button', {name: 'Continue', exact: true});
  const panel = page.locator('.nhsn-link__step-panel');
  await expect(page.locator('.nhsn-link__step-actions')).toBeVisible();
  await page.waitForTimeout(500);

  try {
    await note('1 welcome');
    await continueButton.click();
    await expect(page).toHaveURL(/reporting-plan$/);

    await note('2 reporting-plan');
    await continueButton.click();
    await expect(page).toHaveURL(/facility-info$/);

    await note('3 facility-info');
    // The live BFF offers exactly two vendors: Epic (PatientList) and Cerner (Sftp).
    // Epic's census branch is unreachable against this stack: the ack needs all six Epic `List`
    // queries to have returned, and the HAPI test server holds no List resources. A vendor whose
    // `censusAcquisition` is neither PatientList nor Sftp has `validationLive === false` and so
    // no acknowledgement gate at all.
    await chooseOption(page, 'facilityEhrVendor', process.env.E2E_VENDOR ?? 'Epic');
    await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);
    await continueButton.click();
    await expect(page).toHaveURL(/manual-upload$/);

    await note('4 manual-upload');

    // Measure auto-enrollment runs on the reporting-plan step's save, which for a brand-new
    // facility happens BEFORE facility-info creates it in Tenant -- so DMRP 400s and the BFF
    // swallows it, leaving the facility enrolled in nothing and step 10's measure picker empty.
    // Re-saving step 2 now that the facility exists is the only way to get a measure.
    if (!SKIP_ENROLL_FIX) {
      const backButton = page.getByRole('button', {name: 'Back', exact: true});
      await backButton.click();
      await expect(page).toHaveURL(/facility-info$/);
      await backButton.click();
      await expect(page).toHaveURL(/reporting-plan$/);
      console.log('RE-SAVING reporting-plan now that the facility exists');
      await continueButton.click();
      await expect(page).toHaveURL(/facility-info$/);
      await continueButton.click();
      await expect(page).toHaveURL(/manual-upload$/);
    } else {
      console.log('SKIPPING the LEGLINK-1775 workaround on purpose');
    }

    await continueButton.click();
    await expect(page).toHaveURL(/\/fhir$/);

    await note('5 fhir');
    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill(FHIR_URL);
    await page.locator('#maxConcurrentRequests').fill('4');
    await page.keyboard.press('Tab');
    await page.locator('#lagMinutes').fill('30');
    await page.keyboard.press('Tab');
    await page.getByRole('button', {name: 'Test Connection'}).click();
    await page.waitForTimeout(5000);
    console.log('FHIR PANEL:', (await panel.innerText()).slice(0, 400));
    await continueButton.click();
    await expect(page).toHaveURL(/census$/);

    await note('6 census');
    console.log('CENSUS PANEL:', (await panel.innerText()).slice(0, 900));
    const listInputs = page.locator('[id^="census-list-"]');
    const count = await listInputs.count();
    for (let i = 0; i < count; i += 1) {
      await listInputs.nth(i).fill(LIST_IDS[i] ?? `epic-list-${i + 1}`);
    }
    await page.locator('#census-frequency-hours').fill('1');
    await page.keyboard.press('Tab');

    // Only reachable against the real BFF: `capabilities.patientListWithNames` is ON there and
    // OFF in the mock, so this whole branch — validation, results preview, acknowledgement — is
    // invisible to the mocked tier.
    const validate = page.getByRole('button', {name: 'Validate Census Results'});
    if (await validate.count()) {
      console.log('VALIDATE BUTTON PRESENT; clicking');
      await validate.click();
      await page.waitForTimeout(6000);
      console.log('AFTER VALIDATE:', (await panel.innerText()).slice(-800));
    }
    const censusAck = page.getByRole('checkbox', {
      name: /patients of interest results are accurate/
    });
    if (await censusAck.count()) {
      console.log('CENSUS ACK disabled?', await censusAck.isDisabled());
      await censusAck.check({force: true}).catch((e) => console.log('ACK CHECK FAILED:', e.message.split('\n')[0]));
    }
    await continueButton.click();
    await expect(page).toHaveURL(/location-org$/);

    await note('7 location-org');
    await continueButton.click();
    await expect(page).toHaveURL(/hsloc$/);

    await note('8 hsloc');
    await page.locator('#hsloc-location-value-0').fill('ICU-1');
    // Whatever the real reference list offers — take the first real option, not a known code.
    await page.locator('#hsloc-code-select-0').click();
    const options = page.getByRole('option');
    await expect(options.first()).toBeVisible();
    console.log('HSLOC OPTIONS:', (await options.allTextContents()).slice(0, 6));
    await options.nth(1).click();
    await continueButton.click();
    await expect(page).toHaveURL(/encounter$/);

    await note('9 encounter');
    await page
      .getByRole('textbox', {name: 'Encounter.type Code System', exact: true})
      .fill('http://mysite.org/fhir/encounter-type');
    await continueButton.click();
    await expect(page).toHaveURL(/\/report$/);

    await note('10 report');
    await page.locator('#reportMeasures').click();
    if (SKIP_ENROLL_FIX) {
      // LEGLINK-1775 made visible: the picker is empty, with no error anywhere on the step.
      await page.waitForTimeout(1500);
      if (SHOTS) {
        await page.screenshot({path: `${SHOTS}/leglink-1775-empty-measure-picker.png`});
        console.log('CAPTURED the empty measure picker');
      }
      return;
    }
    const measures = page.getByRole('option');
    await expect(measures.first()).toBeVisible();
    console.log('MEASURES:', (await measures.allTextContents()).slice(0, 8));
    await measures.first().click();
    await typeDate(page, 'reportStartDate', '10-01-2026');
    await typeDate(page, 'reportEndDate', '10-31-2026');
    await page.getByRole('button', {name: '+ Add Patient ID'}).click();
    await page.locator('#reportPatientId-0').fill('patient-001');
    await page.getByRole('button', {name: 'Generate Report'}).click();
    await page.waitForTimeout(6000);

    await note('11 report-results');
    console.log('FACILITY:', PROFILE.facilityId);
    const table = page.locator('.nhsn-link__report-results-table');
    const ack = page.getByRole('checkbox', {
      name: 'I acknowledge that the generated reports are accurate.'
    });
    const refresh = page.getByRole('button', {name: /Refresh/});

    // Report generation is asynchronous (Kafka), so a just-requested report cannot be in
    // listReports yet. Poll via the step's own Refresh to find out whether it ever arrives --
    // F1 assumes this is transient in production and permanent only in mock.
    for (let attempt = 1; attempt <= 10; attempt += 1) {
      const rows = await table.count();
      console.log(`POLL ${attempt}: table=${rows} ackDisabled=${await ack.isDisabled().catch(() => 'n/a')}`);
      if (rows > 0) break;
      if (await refresh.count()) {
        await refresh.click();
      }
      await page.waitForTimeout(12_000);
    }
    console.log('REPORT RESULTS PANEL:', (await panel.innerText()).slice(0, 900));

    await ack.check();
    await continueButton.click();
    await expect(page).toHaveURL(/mrn-intake$/);

    await note('12 mrn-intake');
    // Each question is a tablist named after the whole question; answers are role="tab".
    for (const text of [
      /multiple MRN-like patient identifiers/,
      /vary by facility\/campus\/setting/,
      /change over time for the same patient/
    ]) {
      await page.getByRole('tablist', {name: text}).getByRole('tab', {name: 'No', exact: true}).click();
    }
    // A rule is demanded even when every answer is No -- that is LEGLINK-1767.
    await page.getByRole('button', {name: '+ Add Rule'}).click();
    await page.locator('#mrn-rule-value-0').fill('http://hospital.org/fhir/identifier/mrn');
    await page.getByRole('button', {name: 'Complete Enrollment'}).click();
    await expect(page).toHaveURL(/complete$/, {timeout: 30_000});

    await note('13 complete');
    console.log('COMPLETE PANEL:', (await panel.innerText()).slice(0, 1200));
    if (SHOTS) {
      await page.screenshot({path: `${SHOTS}/walk-13-enrollment-complete.png`});
    }
  } catch (error) {
    console.log('STOPPED WITH:', (error as Error).message.split('\n').slice(0, 6).join('\n'));
    const alerts = await page.locator('main [role="alert"]').allTextContents();
    console.log('ALERTS AT STOP:', JSON.stringify(alerts.map((t) => t.trim()).filter(Boolean)));
    console.log('PANEL AT STOP:', (await panel.innerText()).slice(0, 2500));
  }

  console.log('\n===== REACHED =====');
  for (const line of reached) console.log(line);
  console.log('FINAL URL:', page.url());
  console.log('\n===== WIRE =====');
  for (const line of wire) console.log(line);
  console.log('===== END =====\n');

  if (KEEP_OPEN) {
    console.log('\n  Walk finished — the browser stays open. Close the window to end the run.\n');
    await page.waitForEvent('close', {timeout: 0});
  }

  expect(true).toBeTruthy();
});
