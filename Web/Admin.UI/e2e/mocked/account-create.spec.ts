import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { AccountDashboardPage } from '../support/pages/account-dashboard.page';
import { AccountUser, existingUser, pagedUsers, testRoles } from '../fixtures/accounts';

const USERS_GET = '/api/account/users';
const ROLES_GET = '/api/account/role';
const USER_POST = '/api/account/user';

const newUser = {
  firstName: 'Grace',
  lastName: 'Hopper',
  email: 'grace.hopper@example.com',
};
/** The form derives this rather than asking for it — see the payload assertion below. */
const derivedUsername = `${newUser.firstName}.${newUser.lastName}`;

/** The dashboard loads users and roles on init; each test decides what those return. */
function mockDashboard(api: ApiMock, users: AccountUser[] = []): void {
  api.mock(`GET ${USERS_GET}`, pagedUsers(users));
  api.mock(`GET ${ROLES_GET}`, testRoles);
}

/**
 * Create flow store: the list is empty until the POST lands, then serves the user that was
 * posted. The dialog's afterClosed re-fetches, so a static fixture would leave the grid
 * showing "No accounts found." after a successful create.
 *
 * The POST body carries no id — the backend mints one — so this stands in for that, which
 * is what the grid then renders.
 */
function mockUserCreate(api: ApiMock): void {
  const saved: AccountUser[] = [];

  api.mock(`GET ${USERS_GET}`, (route) => route.fulfill({ status: 200, json: pagedUsers(saved) }));
  api.mock(`POST ${USER_POST}`, (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}') as AccountUser;
    saved.push({ ...posted, id: '3f9a7c21-0d64-4f0e-9a52-6c1d8b4e7a30', middleName: '' });
    return route.fulfill({ status: 201, json: { success: true, message: 'User created' } });
  });
}

test('every field is required before an account can be created', async ({ page, api }) => {
  mockDashboard(api);

  const accounts = new AccountDashboardPage(page);
  await accounts.goto();
  await accounts.openCreateDialog();

  // Nothing entered yet, so there is nothing to save.
  await expect(accounts.saveButton).toBeDisabled();

  // The three text fields alone are not enough: Roles is required too, and it is the one
  // with no inline error to explain itself — an empty roles list leaves the save button
  // disabled with nothing on screen saying why.
  await accounts.fillAccount(newUser);
  await expect(accounts.saveButton).toBeDisabled();

  await accounts.chooseRoles(testRoles[0].name);
  await expect(accounts.saveButton).toBeEnabled();

  expect(api.callsTo('POST', USER_POST)).toHaveLength(0);
});

test('an address that is not an email keeps the save disabled', async ({ page, api }) => {
  mockDashboard(api);

  const accounts = new AccountDashboardPage(page);
  await accounts.goto();
  await accounts.openCreateDialog();

  await accounts.fillAccount({ ...newUser, email: 'grace.hopper' });
  await accounts.chooseRoles(testRoles[0].name);

  // Validators.email on the control, so the form never reaches VALID and canSave() stays false.
  await expect(accounts.saveButton).toBeDisabled();

  await accounts.field('Email').fill(newUser.email);
  await expect(accounts.saveButton).toBeEnabled();
});

test('creating an account posts the derived username and the new row appears', async ({ page, api }) => {
  mockDashboard(api);
  mockUserCreate(api);

  const accounts = new AccountDashboardPage(page);
  await accounts.goto();
  await expect(accounts.emptyState).toBeVisible();

  await accounts.openCreateDialog();
  await accounts.fillAccount(newUser);
  await accounts.chooseRoles(testRoles[0].name);
  await accounts.saveButton.click();

  await expect(accounts.dialog).toBeHidden();

  const posts = api.callsTo('POST', USER_POST);
  expect(posts).toHaveLength(1);
  // The dialog never asks for a username: the form composes it from the two name fields
  // (account-config-form.component.ts:134), and defaults the two flags. Roles go as names,
  // not ids. Asserting the whole body is what pins that down — a change to the derivation
  // would otherwise pass unnoticed.
  expect(JSON.parse(posts[0].postData ?? '{}')).toEqual({
    firstName: newUser.firstName,
    lastName: newUser.lastName,
    email: newUser.email,
    roles: [testRoles[0].name],
    username: derivedUsername,
    isActive: true,
    isDeleted: false,
  });

  // Closing the dialog re-fetches, which is what turns the empty state into a row.
  await expect.poll(() => api.callsTo('GET', USERS_GET).length).toBe(2);
  await expect(page.getByText('Account Created')).toBeVisible();

  const row = accounts.rowFor(derivedUsername);
  await expect(row).toHaveCount(1);
  await expect(row).toContainText(newUser.firstName);
  await expect(row).toContainText(newUser.lastName);
  await expect(row).toContainText(newUser.email);
  // Only the selected role is rendered — the second option was left unchosen.
  await expect(accounts.roleBadges(derivedUsername)).toHaveText([testRoles[0].name]);
});

test.describe('duplicate email', () => {
  // The 409 branch logs the response on its way to showing the message
  // (account-config-form.component.ts:170). That is a deliberate diagnostic on a handled
  // path, not a swallowed failure, so this one error is allowed rather than the whole
  // tripwire being switched off for the test.
  test.use({ allowedConsoleErrors: ['Error occurred: HttpErrorResponse'] });

  test('a duplicate email is reported and leaves the dialog open', async ({ page, api }) => {
    mockDashboard(api, [existingUser]);
    api.mock(`POST ${USER_POST}`, {
      status: 409,
      json: { title: 'Conflict', status: 409, detail: 'A user with that email already exists.' },
    });

    const accounts = new AccountDashboardPage(page);
    await accounts.goto();
    await accounts.openCreateDialog();

    await accounts.fillAccount({ ...newUser, email: existingUser.email });
    await accounts.chooseRoles(testRoles[0].name);
    await accounts.saveButton.click();

    // 409 is translated into its own message rather than surfaced raw
    // (account-config-form.component.ts:171). The dialog stays open so the address can be
    // corrected without retyping the rest of the form.
    await expect(page.getByText('Another account with same email exists.')).toBeVisible();
    await expect(accounts.dialog).toBeVisible();
    await expect(accounts.field('First Name')).toHaveValue(newUser.firstName);

    // The failed create must not have refreshed the grid behind the dialog.
    expect(api.callsTo('GET', USERS_GET)).toHaveLength(1);
  });
});
