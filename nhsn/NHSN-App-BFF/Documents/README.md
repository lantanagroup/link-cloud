# Vendor instruction documents

`GET /api/nhsn-app-bff/documents/{documentKey}` serves PDFs from this directory by key, matching
`VendorProfileCatalog`'s `DocumentKeys`. Drop the actual instruction files in here, named exactly
after their key:

- `epic-census-instructions.pdf`
- `epic-jwks-instructions.pdf`
- `cerner-census-instructions.pdf`
- `cerner-jwks-instructions.pdf`
- `location-org-resolution.pdf`

A key with no matching file returns 404, same as a key not on the allow-list at all.
