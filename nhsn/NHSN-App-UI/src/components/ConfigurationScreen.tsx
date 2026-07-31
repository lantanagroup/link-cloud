import React from 'react';
import {useTranslation} from 'react-i18next';

export function ConfigurationScreen() {
  const {t} = useTranslation('configuration');

  return (
    <div className="nhsn-link__content">
      <h2>{t('page.title')}</h2>
      <p>{t('page.intro')}</p>
    </div>
  );
}