export const hasSecurityMarker = message => typeof message === 'string' && /(?:^|\n)Bun-Security-Automation: v1[ \t]*(?:\r?\n)?$/.test(message);
