export interface IVendorConfigModel {
  id: string;
  name: string;

  /**
   * Name of the Key Vault secret holding this vendor's PEM signing key.
   *
   * Optional because no existing vendor record has one. This is the secret's id, not the
   * JWKS `kid` header value -- LEGLINK-63's title conflates the two; LEGLINK-567 uses the
   * accurate term. Free text here: validating it against Key Vault is LEGLINK-566.
   */
  secretId?: string;
}
