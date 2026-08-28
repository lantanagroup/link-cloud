/**
 * The design-system adapter — the ONLY place `@nhsn/nhsn-react-core`
 * components are imported. (`styles.scss` imports the package's stylesheet;
 * global CSS has no adapter to route through.)
 *
 * Steps import our names, never the package's. Three reasons this indirection
 * earns its keep here beyond the usual one:
 *
 *  1. The package is a Kendo wrapper. Its exports carry Kendo's names and
 *     shapes, and its peer dependencies pin twenty-odd `@progress/*` packages
 *     to exact versions. This folder is where that surface is named in our
 *     terms.
 *  2. Its field renderers are typed for Kendo's `<Form>` but are in fact plain
 *     functions of their props. Driving them directly is what lets the reducer
 *     stay the single writer — see `fieldProps.ts`.
 *  3. Five of its exports require a React Router ancestor — `Alerts`,
 *     `AlertCard`, `IncompleteCard`, `EditAnchorCell2` and `usePageTitle`.
 *     They are deliberately NOT re-exported here. There is no router in this
 *     component and adding one would compete with the host's.
 */

export * from './inputs';
export * from './Select';
export * from './DataTable';
export * from './layout';
export * from './upload';
export type {BaseFieldProps} from './fieldProps';
