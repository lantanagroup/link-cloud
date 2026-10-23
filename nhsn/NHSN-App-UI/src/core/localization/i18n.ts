import i18n from 'i18next';
import HttpBackend from 'i18next-http-backend';
import LanguageDetector from 'i18next-browser-languagedetector';
import {initReactI18next} from 'react-i18next';
import bundledCommon from './bundled/common.json';
import bundledOnboarding from './bundled/onboarding.json';
import bundledConfiguration from './bundled/configuration.json';

const defaultLocale = 'en-US';
const namespaces = ['common', 'onboarding', 'configuration'];

/**
 * A bundled copy of en-US, shipped in the artifact.
 *
 * The BFF is the runtime source of truth and overrides these as soon as it
 * answers. They exist because without them a localization failure renders the
 * component as raw i18n keys — inside someone else's application, with no
 * indication of what went wrong. The ADR requires a bundled default for
 * exactly this reason.
 *
 * Kept in sync by hand with `NHSN-App-BFF/Localization/en-US/`, minus the
 * `shell.*` namespace: those strings belong to the standalone harness, which
 * is never in this bundle, and one of them is a PEM placeholder that reads
 * like a leaked private key when found in the artifact. Drift only degrades
 * the offline fallback, never the served strings.
 */
const bundledResources = {
  [defaultLocale]: {
    common: bundledCommon,
    onboarding: bundledOnboarding,
    configuration: bundledConfiguration
  }
};

let initializedBaseUrl: string | null = null;

type LocalizationOptions = {
  apiBaseUrl?: string;
  locale?: string;
};

export async function ensureI18nInitialized(options: LocalizationOptions = {}): Promise<void> {
  const normalizedApiBase = normalizeApiBaseUrl(options.apiBaseUrl);

  if (!i18n.isInitialized || initializedBaseUrl !== normalizedApiBase) {
    i18n.use(HttpBackend).use(LanguageDetector).use(initReactI18next);

    await i18n.init({
      // The server applies locale fallback itself (requested -> neutral ->
      // en-US), so one request per namespace is enough. `fallbackLng` here is
      // the *offline* fallback: when the BFF is unreachable it resolves
      // against the bundled resources below rather than rendering raw keys.
      fallbackLng: defaultLocale,
      load: 'currentOnly',
      resources: bundledResources,
      // Without this, supplying `resources` stops the backend loading
      // anything else — the served strings would never override the bundle.
      partialBundledLanguages: true,
      ns: namespaces,
      defaultNS: 'common',
      interpolation: {
        escapeValue: true
      },
      detection: {
        order: ['querystring', 'navigator'],
        caches: []
      },
      backend: {
        loadPath: `${normalizedApiBase}/localization/{{lng}}/{{ns}}`
      }
    });

    initializedBaseUrl = normalizedApiBase;
  }

  if (options.locale) {
    await i18n.changeLanguage(options.locale);
  }
}

export async function setAppLocale(locale?: string): Promise<void> {
  if (!locale || !i18n.isInitialized) {
    return;
  }

  await i18n.changeLanguage(locale);
}

export {defaultLocale};

function normalizeApiBaseUrl(value?: string): string {
  const trimmed = value?.trim();
  if (!trimmed) {
    return '/api';
  }

  return trimmed.endsWith('/') ? trimmed.slice(0, -1) : trimmed;
}