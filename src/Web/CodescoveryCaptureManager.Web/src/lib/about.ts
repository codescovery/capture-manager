export const REPOSITORY_URL = 'https://github.com/codescovery/capture-manager';
export const ISSUES_URL = `${REPOSITORY_URL}/issues`;

/** Served by the desktop app next to the page: `{ "name": "...", "version": "1.4.0" }`. */
export const APP_INFO_PATH = './app-info.json';

/** Version from the app-info document, or null when it is missing or malformed. */
export function parseAppInfo(document: unknown): string | null {
  if (typeof document !== 'object' || document === null) return null;
  const { version } = document as Record<string, unknown>;
  return typeof version === 'string' && version.trim().length > 0 ? version.trim() : null;
}

/**
 * Pre-filled "new issue" page. Carries the app version and the browser only: nothing about the windows being
 * shared (same rule as the desktop app).
 */
export function buildIssueUrl(appVersion: string | null, userAgent: string): string {
  const body = [
    '**What happened?**',
    '',
    '',
    '**What did you expect?**',
    '',
    '',
    '**Steps to reproduce**',
    '1. ',
    '',
    '---',
    `Capture Manager ${appVersion ?? 'unknown version'} (web viewer)`,
    `Browser: ${userAgent}`,
  ].join('\n');
  return `${ISSUES_URL}/new?body=${encodeURIComponent(body)}`;
}
