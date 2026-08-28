import {importPKCS8, SignJWT} from 'jose';
import type {ApiClient} from '../../core/api/ApiClient';
import type {TestUserProfile} from './models';

/**
 * Adds a signed test JWT to every call, then delegates.
 *
 * A decorator rather than a third adapter: wrapping keeps `BffApiClient` free
 * of any signing code path, so the embed bundle cannot reach `jose` even by
 * accident. In production the NHSN gateway injects the token and nothing here
 * is built at all.
 *
 * This file — and this folder — is exactly what must never appear in
 * `dist/embed/nhsn-link.js`.
 */
export class TestAuthApiClient implements ApiClient {
  constructor(
    private readonly inner: ApiClient,
    private readonly profile: TestUserProfile
  ) {
    installAuthHeaderInterceptor(profile);
  }

  // Every method delegates; the header is attached by the interceptor below.
  getUserInfo = () => this.inner.getUserInfo();
  getDraft = () => this.inner.getDraft();
  saveDraft: ApiClient['saveDraft'] = draft => this.inner.saveDraft(draft);
  importDraft: ApiClient['importDraft'] = file => this.inner.importDraft(file);
  exportDraft = () => this.inner.exportDraft();
  completeOnboarding = () => this.inner.completeOnboarding();
  getCommitState = () => this.inner.getCommitState();

  getVendorProfiles = () => this.inner.getVendorProfiles();
  getTimezones = () => this.inner.getTimezones();
  getMeasures = () => this.inner.getMeasures();
  getHslocCodes = () => this.inner.getHslocCodes();
  getEncounterCodes: ApiClient['getEncounterCodes'] = q => this.inner.getEncounterCodes(q);
  getDocument: ApiClient['getDocument'] = key => this.inner.getDocument(key);

  testFhirConnection: ApiClient['testFhirConnection'] = c => this.inner.testFhirConnection(c);

  queryPatientList: ApiClient['queryPatientList'] = k => this.inner.queryPatientList(k);
  listSftpFiles = () => this.inner.listSftpFiles();
  testSftpConnection: ApiClient['testSftpConnection'] = c => this.inner.testSftpConnection(c);
  saveSftpCredentials: ApiClient['saveSftpCredentials'] = c => this.inner.saveSftpCredentials(c);
  acknowledgeCensus: ApiClient['acknowledgeCensus'] = a => this.inner.acknowledgeCensus(a);

  getLocationCandidates: ApiClient['getLocationCandidates'] = m =>
    this.inner.getLocationCandidates(m);
  getHslocMappings = () => this.inner.getHslocMappings();
  saveHslocMappings: ApiClient['saveHslocMappings'] = m => this.inner.saveHslocMappings(m);
  getEncounterMappings = () => this.inner.getEncounterMappings();
  saveEncounterMappings: ApiClient['saveEncounterMappings'] = m =>
    this.inner.saveEncounterMappings(m);

  getMrnIntake = () => this.inner.getMrnIntake();
  saveMrnIntake: ApiClient['saveMrnIntake'] = i => this.inner.saveMrnIntake(i);
  getPatientIdentifiers = () => this.inner.getPatientIdentifiers();

  requestReport: ApiClient['requestReport'] = r => this.inner.requestReport(r);
  listReports: ApiClient['listReports'] = p => this.inner.listReports(p);
  getReport: ApiClient['getReport'] = id => this.inner.getReport(id);
  getPatientStatuses: ApiClient['getPatientStatuses'] = id => this.inner.getPatientStatuses(id);
  getQueryPlan: ApiClient['getQueryPlan'] = id => this.inner.getQueryPlan(id);
  getAcquisitionLogs: ApiClient['getAcquisitionLogs'] = id => this.inner.getAcquisitionLogs(id);
  exportReportSummary: ApiClient['exportReportSummary'] = id => this.inner.exportReportSummary(id);
  regenerateReport: ApiClient['regenerateReport'] = id => this.inner.regenerateReport(id);
  acknowledgeReport: ApiClient['acknowledgeReport'] = (id, a) =>
    this.inner.acknowledgeReport(id, a);

  getReportingPlan = () => this.inner.getReportingPlan();

  getFhirServerInfo = () => this.inner.getFhirServerInfo();
  updateFhirServerInfo: ApiClient['updateFhirServerInfo'] = request =>
    this.inner.updateFhirServerInfo(request);
  getJwksInstructionsUrl: ApiClient['getJwksInstructionsUrl'] = vendor =>
    this.inner.getJwksInstructionsUrl(vendor);
}

/**
 * `http.ts` deliberately has no auth seam — an ADR requirement is that the
 * production client cannot attach a token at all. The shell therefore patches
 * `fetch` rather than threading a header through core, which keeps the
 * decision visible in one shell-only place.
 */
let installed = false;
let currentProfile: TestUserProfile | null = null;

function installAuthHeaderInterceptor(profile: TestUserProfile): void {
  currentProfile = profile;
  if (installed) {
    return;
  }
  installed = true;

  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    if (!url.includes('/nhsn-app-bff/') || !currentProfile) {
      return originalFetch(input, init);
    }

    const headers = new Headers(init?.headers);
    headers.set('Authorization', `Bearer ${await createSignedJwt(currentProfile)}`);
    return originalFetch(input, {...init, headers});
  };
}

async function createSignedJwt(profile: TestUserProfile): Promise<string> {
  const pem = normalizePem(profile.privateKeyPem);
  if (!pem) {
    throw new Error('A private key PEM is required for the harness to sign test JWTs.');
  }
  if (!profile.issuer.trim()) {
    throw new Error('An issuer is required for the harness to sign test JWTs.');
  }

  let privateKey;
  try {
    privateKey = await importPKCS8(pem, 'ES256');
  } catch (error) {
    const detail = error instanceof Error && error.message ? ` ${error.message}` : '';
    throw new Error(
      'The JWT private key PEM could not be parsed for ES256 signing. Make sure the key is a ' +
        'valid PKCS#8 private key, includes BEGIN/END PRIVATE KEY lines, and uses real line ' +
        'breaks instead of escaped \\n sequences.' +
        detail
    );
  }

  const header: {alg: 'ES256'; typ: 'JWT'; kid?: string} = {alg: 'ES256', typ: 'JWT'};
  if (profile.keyId.trim()) {
    header.kid = profile.keyId.trim();
  }

  return new SignJWT({
    upn: profile.email,
    userId: profile.email,
    userName: profile.name,
    userLoggedInAs: profile.name,
    groups: profile.groups,
    facility: profile.facilityId,
    facilityName: profile.facilityName
  })
    .setProtectedHeader(header)
    .setIssuer(profile.issuer)
    .setSubject(profile.email)
    .setAudience('nhsn-app-bff')
    .setIssuedAt()
    .setExpirationTime('15m')
    .sign(privateKey);
}

function normalizePem(value: string): string {
  const normalized = value.replace(/\r\n/g, '\n').replace(/\\n/g, '\n').trim();
  const match = normalized.match(
    /-----BEGIN PRIVATE KEY-----([A-Za-z0-9+/=\s]+)-----END PRIVATE KEY-----/s
  );
  if (!match) {
    return normalized;
  }
  const body = match[1].replace(/\s+/g, '');
  const wrapped = body.match(/.{1,64}/g)?.join('\n') ?? body;
  return ['-----BEGIN PRIVATE KEY-----', wrapped, '-----END PRIVATE KEY-----'].join('\n');
}
