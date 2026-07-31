import i18n from 'i18next';
import HttpBackend from 'i18next-http-backend';
import LanguageDetector from 'i18next-browser-languagedetector';
import {initReactI18next} from 'react-i18next';

const defaultLocale = 'en-US';
const namespaces = ['common', 'onboarding', 'configuration'];

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
      // Server-side localization endpoint already applies locale fallback
      // (requested -> neutral -> en-US), so we keep client loading to one
      // locale request per namespace and avoid an extra fallback fetch.
      fallbackLng: false,
      load: 'currentOnly',
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