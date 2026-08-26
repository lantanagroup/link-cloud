/**
 * Shell-only types. These must never be reachable from `core/`.
 *
 * `TestUserProfile` carries a private key: the standalone shell mints real
 * signed JWTs so lower-environment testing hits the same validation path as
 * production. If this reaches the embed bundle we ship a token-forging harness
 * into the NHSN App — which is what the bundle-boundary test exists to catch.
 */
export interface TestUserProfile {
  id: string;
  label: string;
  email: string;
  name: string;
  groups: string[];
  facilityId: string;
  issuer: string;
  keyId: string;
  privateKeyPem: string;
  lastUsedOn: string;
}
