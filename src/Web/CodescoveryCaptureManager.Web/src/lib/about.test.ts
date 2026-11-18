import { describe, expect, it } from 'vitest';
import { buildIssueUrl, ISSUES_URL, parseAppInfo } from './about';

describe('parseAppInfo', () => {
  it('reads the version', () => {
    expect(parseAppInfo({ name: 'Capture Manager', version: ' 1.4.0 ' })).toBe('1.4.0');
  });

  it('returns null without a usable version', () => {
    expect(parseAppInfo({ name: 'Capture Manager' })).toBeNull();
    expect(parseAppInfo({ version: '' })).toBeNull();
    expect(parseAppInfo({ version: 14 })).toBeNull();
    expect(parseAppInfo(null)).toBeNull();
    expect(parseAppInfo('1.4.0')).toBeNull();
  });
});

describe('buildIssueUrl', () => {
  it('opens a new issue with version and browser in the body', () => {
    const url = new URL(buildIssueUrl('1.4.0', 'Mozilla/5.0 Chrome/140'));
    const body = url.searchParams.get('body') ?? '';

    expect(`${url.origin}${url.pathname}`).toBe(`${ISSUES_URL}/new`);
    expect(body).toContain('Capture Manager 1.4.0 (web viewer)');
    expect(body).toContain('Browser: Mozilla/5.0 Chrome/140');
  });

  it('says so when the version is not known', () => {
    const body = new URL(buildIssueUrl(null, 'agent')).searchParams.get('body') ?? '';

    expect(body).toContain('Capture Manager unknown version (web viewer)');
  });
});
