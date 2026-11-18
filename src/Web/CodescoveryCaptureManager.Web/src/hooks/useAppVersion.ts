import { useEffect, useState } from 'react';
import { APP_INFO_PATH, parseAppInfo } from '../lib/about';

/** Version of the desktop app serving this page; null while loading or when the page is not served by the app (dev server). */
export function useAppVersion(): string | null {
  const [version, setVersion] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch(APP_INFO_PATH, { cache: 'no-store' })
      .then((response) => (response.ok ? response.json() : null))
      .then((document) => !cancelled && setVersion(parseAppInfo(document)))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  return version;
}
