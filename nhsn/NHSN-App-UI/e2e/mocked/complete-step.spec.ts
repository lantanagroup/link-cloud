import {expect, test} from '../support/test';
import {
  censusComplete,
  draftAt,
  encounterComplete,
  facilityInfoComplete,
  fhirComplete,
  hslocComplete,
  MOCK_REPORT_ID,
  mrnIntakeComplete,
  reportComplete,
  reportResultsAcknowledged
} from '../fixtures/drafts';

/**
 * Enrollment Complete — step 13, terminal and read-only. It re-reads the draft with `getDraft()`
 * rather than using the provider's store, so the recap states what was persisted rather than
 * whatever local state survived the last save.
 */

function completeDraft() {
  return draftAt('complete', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    hsloc: hslocComplete(),
    encounter: encounterComplete(),
    report: reportComplete(),
    reportResults: reportResultsAcknowledged(),
    mrnIntake: mrnIntakeComplete()
  });
}

test.describe('enrollment complete step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: completeDraft(), path: '/onboarding/complete'});
  });

  test('recaps the facility as it was saved', async ({page, onboarding}) => {
    await expect(onboarding.stepPanel).toContainText(
      "This facility's onboarding has been completed and saved."
    );

    const summary = page.locator('.nhsn-link__summary-list');
    await expect(summary).toContainText('Epic');
    await expect(summary).toContainText('https://ehr.example.org/fhir/r4');
    // The time zone is shown by its display name — the IANA id followed by a UTC offset that
    // moves with DST ("America/New_York — (UTC-05:00) Eastern Time"), so match the id, never the
    // offset.
    await expect(summary).toContainText(/America\/New_York/);
    await expect(summary).toContainText(MOCK_REPORT_ID);
  });

  test('reports "Passed" for a connection test that was only simulated', async ({page}) => {
    // `connectionTested` is a bare boolean on the draft: the `simulated` flag the mock sets on the
    // result never reaches it, and `FhirStep` does not read it either. So a simulated probe is
    // recorded here as an unqualified pass, which is the permanent end of the finding first seen
    // on step 5.
    await expect(page.locator('.nhsn-link__summary-list')).toContainText('Passed');
  });

  test('counts zero reports beside the report it completed from', async ({page}) => {
    // Characterisation of two rows that read from different sources: "Reports Generated" comes
    // from `listReports` (hard-coded empty in the mock) while "Completed From Report Id" comes
    // from the draft. Here they plainly contradict each other, and in a real deployment they can
    // still disagree while the report list catches up.
    const rows = page.locator('.nhsn-link__summary-list li');
    await expect(rows.filter({hasText: 'Reports Generated'})).toContainText('0');
    await expect(rows.filter({hasText: 'Completed From Report Id'})).toContainText(MOCK_REPORT_ID);
  });

  test('is terminal: it offers a way out, and no way on', async ({page, onboarding}) => {
    await expect(page.getByRole('button', {name: /Return to/})).toBeVisible();
    await expect(onboarding.continueButton).toHaveCount(0);
    await expect(onboarding.backButton).toHaveCount(0);
  });
});
