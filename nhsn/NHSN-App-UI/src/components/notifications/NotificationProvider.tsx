import React, {
    createContext,
    PropsWithChildren,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useRef,
    useState
} from 'react';
import {useTranslation} from 'react-i18next';

export type NotificationVariant = 'success' | 'error' | 'info';

export interface NotificationMessage {
  id: string;
  message: string;
  variant: NotificationVariant;
  autoDismiss: boolean;
}

interface NotificationContextValue {
  notifySuccess: (message: string) => void;
  notifyError: (message: string) => void;
  notifyInfo: (message: string) => void;
  closeNotification: (id: string) => void;
}

const NotificationContext = createContext<NotificationContextValue | null>(null);

export function NotificationProvider({ children }: PropsWithChildren) {
  const {t} = useTranslation('common');
  const [notifications, setNotifications] = useState<NotificationMessage[]>([]);
  const timers = useRef<Record<string, number>>({});

  const closeNotification = useCallback((id: string) => {
    if (timers.current[id]) {
      window.clearTimeout(timers.current[id]);
      delete timers.current[id];
    }

    setNotifications(current => current.filter(notification => notification.id !== id));
  }, []);

  useEffect(() => {
    return () => {
      Object.values(timers.current).forEach(timerId => window.clearTimeout(timerId));
      timers.current = {};
    };
  }, []);

  const addNotification = useCallback((message: string, variant: NotificationVariant, autoDismiss: boolean) => {
    const id = crypto.randomUUID();
    const notification: NotificationMessage = { id, message, variant, autoDismiss };
    setNotifications(current => [...current, notification]);

    if (autoDismiss) {
      timers.current[id] = window.setTimeout(() => {
        closeNotification(id);
      }, 5000);
    }
  }, [closeNotification]);

  const contextValue = useMemo<NotificationContextValue>(() => ({
    notifySuccess: (message: string) => addNotification(message, 'success', true),
    notifyError: (message: string) => addNotification(message, 'error', false),
    notifyInfo: (message: string) => addNotification(message, 'info', true),
    closeNotification
  }), [addNotification, closeNotification]);

  return (
    <NotificationContext.Provider value={contextValue}>
      {children}
      <div className="notification-stack" aria-live="polite" aria-atomic="false">
        {notifications.map(notification => (
          <div key={notification.id} className={`notification-stack__item notification-stack__item--${notification.variant}`}>
            <div className="notification-stack__message">{notification.message}</div>
            <button type="button" className="notification-stack__close" onClick={() => closeNotification(notification.id)} aria-label={t('notifications.close')}>
              ×
            </button>
          </div>
        ))}
      </div>
    </NotificationContext.Provider>
  );
}

export function useNotifications(): NotificationContextValue {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationProvider.');
  }

  return context;
}