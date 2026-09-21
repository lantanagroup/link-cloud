import { createContext, useCallback, useContext, useEffect, useRef } from 'react';

export interface StepChrome {
  title: string;
  footer: React.ReactNode;
}

/**
 * Wraps a handler (typically one recreated fresh on every render) in a
 * permanently-stable function identity, so it's safe to list in a `useMemo`/
 * `useCallback` dependency array without ever forcing a recompute - calling
 * the returned function always runs whatever `fn` currently is, not whatever
 * it was when this hook first ran.
 */
export function useStableCallback<A extends unknown[], R>(fn: (...args: A) => R): (...args: A) => R {
  const ref = useRef(fn);
  ref.current = fn;
  return useCallback((...args: A) => ref.current(...args), []);
}

export const StepChromeContext = createContext<((chrome: StepChrome | null) => void) | null>(null);

export function useStepChrome(chrome: StepChrome | null) {
  const setChrome = useContext(StepChromeContext);
  if (!setChrome) {
    throw new Error('useStepChrome used outside StepHost');
  }

  useEffect(() => {
    setChrome(chrome);
  }, [setChrome, chrome]);

  useEffect(() => () => setChrome(null), [setChrome]);
}
