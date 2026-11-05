import test, { after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, readFileSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { ledgerGenesis } from './Security-Record.mjs';
import { BRANCH, candidateFiles, prepare as realPrepare, merge, report as realReport } from './Security-Pull-Request.mjs';

const originalLedger = process.env.BUN_SECURITY_LEDGER_ISSUE;
process.env.BUN_SECURITY_LEDGER_ISSUE = '8';
after(() => { if (originalLedger === undefined) delete process.env.BUN_SECURITY_LEDGER_ISSUE; else process.env.BUN_SECURITY_LEDGER_ISSUE = originalLedger; });
function report(api, options, extras) {
  if (!api.fixtureNotification) {
    const original = api.response?.bind(api);
    api.response = async (path, ...args) => {
      if (path.startsWith('/issues?') || /^\/issues\/4\/comments\?/.test(path)) return { status: 200, data: await api.request(path), headers: { link: null, remaining: '1000' } };
      return original?.(path, ...args);
    };
    api.fixtureNotification = true;
  }
  return realReport(api, options, extras);
}
function prepare(api, options) {
  if (!api.fixtureLedger) {
    const original = api.request.bind(api), originalResponse = api.response?.bind(api);
    api.request = (path, ...args) => path === '/issues/8' ? Promise.resolve({ number: 8, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, repository_url: `https://api.github.com/repos/${api.repository}`, body: ledgerGenesis(12, 8) }) : original(path, ...args);
    api.response = (path, ...args) => path.startsWith('/issues/8/comments?') ? Promise.resolve({ status: 200, data: [], headers: { link: null, remaining: '1000' } }) : originalResponse?.(path, ...args);
    api.fixtureLedger = true;
  }
  return realPrepare(api, options);
}
const root = process.env.BUN_SECURITY_TEST_ROOT;
assert.ok(root && resolve(root) === root, 'BUN_SECURITY_TEST_ROOT must name an existing absolute task directory');
const fixtures = mkdtempSync(join(root, 'github-tests-'));
// 実際の Actions run の識別子・summary を fixture の API/監査結果へ混ぜない。
const ambient = { GITHUB_RUN_ID: process.env.GITHUB_RUN_ID, GITHUB_STEP_SUMMARY: process.env.GITHUB_STEP_SUMMARY };
delete process.env.GITHUB_RUN_ID;
process.env.GITHUB_STEP_SUMMARY = join(fixtures, 'fixture-step-summary.md');
after(() => {
  for (const [name, value] of Object.entries(ambient)) {
    if (value === undefined) delete process.env[name];
    else process.env[name] = value;
  }
});
test('GitHub Actions ambient run and summary are isolated from offline fixtures', () => {
  assert.equal(process.env.GITHUB_RUN_ID, undefined);
  assert.equal(process.env.GITHUB_STEP_SUMMARY, join(fixtures, 'fixture-step-summary.md'));
});
test('candidate path allowlist rejects workflow/code and symlink-independent traversal', () => {
  assert.throws(() => candidateFiles({ changedFiles: ['../package.json'] }, fixtures), /Disallowed/);
  assert.throws(() => candidateFiles({ changedFiles: ['.github/workflows/ci.yml'] }, fixtures), /Disallowed/);
  assert.throws(() => candidateFiles({ changedFiles: ['package.json', 'package.json'] }, fixtures), /Invalid/);
  writeFileSync(join(fixtures, 'package.json'), '{}');
  assert.equal(candidateFiles({ changedFiles: ['package.json'] }, fixtures)[0].content, 'e30=');
  assert.equal(candidateFiles({ changedFiles: ['package.json'] }, fixtures)[0].path, 'build/scripts/package.json');
  assert.throws(() => candidateFiles({ changedFiles: ['build/scripts/package.json'] }, fixtures), /Disallowed/);
});
test('merge cannot use candidate report as CI success evidence', async () => {
  let calls = 0;
  await assert.rejects(merge({ request() { calls++; } }, { 'validation-success': 'false' }), /Trusted CI/);
  assert.equal(calls, 0);
});
test('report preserves issue body and posts only changed bot comments', async () => {
  const writes = [], comments = [];
  const issue = { number: 4, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, body: '<!-- bun-security-automation -->\nuser edit' };
  const api = { repository: 'owner/repo', async request(path, method = 'GET', payload) {
    if (path === '') return { id: 12, full_name: 'owner/repo', default_branch: 'main', has_issues: true };
    if (path.startsWith('/issues/comments?')) return [];
    if (path.startsWith('/issues?')) return [issue];
    if (path.startsWith('/issues/4/comments?')) return comments;
    if (path === '/issues/4/comments' && method === 'POST') { writes.push([path, method, payload]); const c = { id: 100 + comments.length, user: { id: 41898282, login: 'github-actions[bot]' }, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: payload.body }; comments.push(c); return c; }
    throw Error('Unexpected report request ' + path);
  } };
  await report(api, { status: 'validation-failed', details: 'CI failed' });
  await report(api, { status: 'validation-failed', details: 'CI failed' });
  assert.equal(writes.length, 1); assert.equal(issue.body, '<!-- bun-security-automation -->\nuser edit');
  issue.body += '\nconcurrent user edit';
  await report(api, { status: 'native-merge-unresolved' });
  assert.equal(writes.length, 2); assert.match(issue.body, /concurrent user edit/);
  assert.ok(writes.every(([path, method]) => path === '/issues/4/comments' && method === 'POST'));
});

test('候補ゼロでも未解決のnative intentを無視しない', async () => {
  const head = '2'.repeat(40), base = '1'.repeat(40), branch = BRANCH + '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
  const file = join(fixtures, 'empty-report.json');
  writeFileSync(file, JSON.stringify({ changedFiles: [], candidateRoot: fixtures, remaining: [] }));
  const common = { version: 1, repo: 12, issue: 8, last_closed: null, last_completion: null, last_failure: null };
  const candidate = { package_blob: '6'.repeat(40), lock_blob: '7'.repeat(40) }, proposal = { pr: 7, branch, base, head, ...candidate };
  const nonce = '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
  const record = { version: 1, repo: 12, pr: 7, base, head, merge: '3'.repeat(40), tree: '4'.repeat(40), run: 9, attempt: 1, state: 'intent' };
  const proof = { id: 33, app_id: 123, creator: { id: 99, login: 'guard[bot]' }, nonce, context: 'kagayoi/automation-merge-guard-v1', state: 'success', target_url: 'https://github.com/owner/repo/actions/runs/9/attempts/1', description: `PR 7 H ${head}; run 9 a 1; ${nonce}` };
  const signing = { ...record, state: 'signing', nonce, guard: { app_id: 123, client_id: 'client123', slug: 'guard', creator: proof.creator } };
  const events = [
    { ...common, prev: 0, state: 'preparing', proposal: null, record: null, proof: null, preparing: { pr: null, branch, base, head, ...candidate } },
    { ...common, prev: 100, state: 'idle', proposal, record: null, proof: null, preparing: null },
    { ...common, prev: 101, state: 'signing', proposal, record: signing, proof: null, preparing: null },
    { ...common, prev: 102, state: 'signed', proposal, record: { ...signing, state: 'signed' }, proof, preparing: null },
    { ...common, prev: 103, state: 'intent', proposal, record, proof, preparing: null },
  ].map((event, i) => ({ id: 100 + i, user: { id: 41898282, login: 'github-actions[bot]' }, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: `<!-- bun-security-ledger-event-v1:${JSON.stringify(event)} -->` }));
  const api = { fixtureLedger: true, repository: 'owner/repo', async request(path) {
    if (path === '') return { id: 12, has_issues: true, full_name: 'owner/repo', default_branch: 'main' };
    if (path === '/issues/8') return { number: 8, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, repository_url: 'https://api.github.com/repos/owner/repo', body: ledgerGenesis(12, 8) };
    throw Error(`Unexpected call ${path}`);
  }, async response() { return { status: 200, data: events, headers: { link: null, remaining: '1000' } }; } };
  await assert.rejects(prepare(api, { report: file }), /Native merge remains unresolved/);
});
test('通知は現 main の確定監査と idle ledger・open PR 不在でのみ state-only close する', async () => {
  const base = '1'.repeat(40), head = '2'.repeat(40);
  const reportPath = join(fixtures, 'close-gate.json');
  const clean = { status: 'clean', rawAfter: {}, mitigations: [], remaining: [], needs_followup: false, upstreamFixes: [], result_changed: false, changedFiles: [] };
  const branch = BRANCH + '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
  const pr = { number: 7, state: 'open', merged: false, draft: false, user: { id: 41898282, login: 'github-actions[bot]', type: 'Bot' }, base: { ref: 'main', sha: base, repo: { full_name: 'owner/repo' } }, head: { ref: branch, sha: head, repo: { full_name: 'owner/repo' } }, commits: 1 };
  function apiFixture({ open = false, moved = false, invalid = false } = {}) {
    const f = { writes: [], mainReads: 0, comments: [] };
    f.api = { repository: 'owner/repo', async request(path, method = 'GET', body) {
      if (method !== 'GET') f.writes.push({ path, method, body });
      if (path === '') return { id: 12, full_name: 'owner/repo', default_branch: 'main', has_issues: true };
      if (path.startsWith('/issues?state=open')) return f.noIssue ? [] : [{ number: 4, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, body: '<!-- bun-security-automation -->\noriginal' }];
      if (path === '/issues' && method === 'POST') return { number: 4 };
      if (path === '/issues/8') return { number: 8, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, repository_url: 'https://api.github.com/repos/owner/repo', body: invalid ? 'bad' : ledgerGenesis(12, 8) };
      if (path === `/git/ref/heads/${branch}`) return { object: { sha: head } };
      if (path.startsWith('/pulls?state=open')) return open ? [pr] : [];
      if (path === '/pulls/7') return pr;
      if (path.startsWith('/pulls/7/commits?')) return [{ sha: head }];
      if (path.includes('/statuses?')) return [];
      if (path === '/git/ref/heads/main') return { object: { sha: f.changedAfterClose && f.closed || moved && ++f.mainReads > 1 ? head : base } };
      if (path === '/issues/4' && method === 'PATCH') { f.closed = true; return { number: 4, state: 'closed' }; }
      if (path === '/issues/4') { if (f.issueReadFails) throw Error('issue unavailable'); return { number: 4, state: f.closed ? 'closed' : 'open', user: { id: 41898282, login: 'github-actions[bot]' }, body: '<!-- bun-security-automation -->\noriginal' }; }
      if (path.startsWith('/issues/4/comments?')) return f.comments;
      if (path === '/issues/4/comments' && method === 'POST') { const item = { id: 20, user: { id: 41898282, login: 'github-actions[bot]' }, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: body.body }; f.comments.push(item); return item; }
      throw Error(`Unexpected ${method} ${path}`);
    }, async response(path) {
      if (path.includes('/statuses?per_page=')) return { status: 200, data: await this.request(path), headers: { link: null, remaining: '1000' } };
      if (path.startsWith('/pulls?state=open&per_page=100&page=')) {
        const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
        f.openReads = (f.openReads ?? 0) + 1;
        const data = page === f.openMalformedPage ? null : (f.openPages?.[page - 1] ?? []);
        const probe = f.openProbeLinks && page > 1 && (data?.length === 0 || f.openBadProbe) ? `<https://api.github.com/repositories/12/pulls?state=open&per_page=100&page=${page - 1}>; rel="prev", <https://api.github.com/repositories/12/pulls?state=open&per_page=100&page=${page - 1}>; rel="last", <https://api.github.com/repositories/12/pulls?state=open&per_page=100&page=1>; rel="first"` : null;
        const terminal = f.openProbeLinks && page > 1 && data?.length > 0 ? `<https://api.github.com/repositories/12/pulls?state=open&per_page=100&page=${page - 1}>; rel="prev", <https://api.github.com/repositories/12/pulls?state=open&per_page=100&page=1>; rel="first"` : null;
        return { status: 200, data, headers: { link: probe ?? terminal ?? f.openLinks?.[page - 1] ?? null, remaining: '1000' } };
      }
      if (path.startsWith('/issues/8/comments?')) {
        const common = { version: 1, repo: 12, issue: 8, record: null, proof: null, last_completion: null, last_failure: null, last_closed: null };
        const blobs = { package_blob: '6'.repeat(40), lock_blob: '7'.repeat(40) };
        const events = open ? [
          { ...common, prev: 0, state: 'preparing', proposal: null, preparing: { pr: null, branch, base, head, ...blobs } },
          { ...common, prev: 100, state: 'idle', proposal: { pr: 7, branch, base, head, ...blobs }, preparing: null },
        ].map((event, i) => ({ id: 100 + i, user: pr.user, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: `<!-- bun-security-ledger-event-v1:${JSON.stringify(event)} -->` })) : [];
        return { status: 200, data: events, headers: { link: null, remaining: '1000' } };
      }
      throw Error(`Unexpected response ${path}`);
    } };
    return f;
  }
  const beforeSha = process.env.GITHUB_SHA; process.env.GITHUB_SHA = base;
  try {
    writeFileSync(reportPath, JSON.stringify(clean));
    const refused = apiFixture(); await report(refused.api, { report: reportPath, 'allow-close': 'false' });
    assert.equal(refused.writes.some(item => item.method === 'PATCH'), false);
    const f = apiFixture(); await report(f.api, { report: reportPath, 'allow-close': 'true' });
    assert.deepEqual(f.writes.filter(item => item.method === 'PATCH').map(item => item.body), [{ state: 'closed' }]);
    for (const variant of [
      { ...clean, changedFiles: ['package.json'] }, { ...clean, remaining: [{}] }, { ...clean, needs_followup: true },
      { ...clean, result_changed: true }, { ...clean, upstreamFixes: [{}] }, { ...clean, status: 'error' },
      { ...clean, status: 'mitigated', rawAfter: { x: [{ url: 'https://advisory.invalid' }] }, mitigations: [{ package: 'x', advisory: 'https://advisory.invalid' }] },
    ]) {
      writeFileSync(reportPath, JSON.stringify(variant)); const noClose = apiFixture(); await report(noClose.api, { report: reportPath, 'allow-close': 'true' });
      assert.equal(noClose.writes.some(item => item.method === 'PATCH'), false);
    }
    const verified = [{ package: 'x', advisory: 'https://advisory.invalid' }];
    writeFileSync(reportPath, JSON.stringify({ ...clean, status: 'mitigated', rawAfter: { x: [{ url: 'https://advisory.invalid' }] }, mitigations: verified }));
    const mitigated = apiFixture(); await report(mitigated.api, { report: reportPath, 'allow-close': 'true' }, { verifiedMitigations: () => verified });
    assert.deepEqual(mitigated.writes.filter(item => item.method === 'PATCH').map(item => item.body), [{ state: 'closed' }]);
    writeFileSync(reportPath, JSON.stringify(clean));
    const open = apiFixture({ open: true }); await report(open.api, { report: reportPath, 'allow-close': 'true' }); assert.equal(open.writes.some(item => item.method === 'PATCH'), false);
    for (const kind of ['main', 'issue']) {
      const raced = apiFixture(); if (kind === 'main') raced.changedAfterClose = true; else raced.issueReadFails = true;
      await assert.rejects(report(raced.api, { report: reportPath, 'allow-close': 'true' }));
      assert.deepEqual(raced.writes.filter(item => item.method === 'PATCH').map(item => item.body), [{ state: 'closed' }]);
    }
    const candidateClean = { ...clean, status: 'clean', result_changed: true, changedFiles: ['package.json'], rawBefore: { vulnerable: [{ url: 'https://example.invalid/advisory' }] }, rawAfter: {} };
    writeFileSync(reportPath, JSON.stringify(candidateClean)); const candidate = apiFixture();
    await report(candidate.api, { report: reportPath, 'allow-close': 'true' });
    assert.equal(candidate.writes.some(item => item.method === 'PATCH'), false); assert.equal(candidate.comments.length, 1);
    const summary = readFileSync(process.env.GITHUB_STEP_SUMMARY, 'utf8');
    assert.match(summary, /"mainRawCount": 1/); assert.match(summary, /"candidateRawAuditSucceeded": true/);
    const created = apiFixture(); created.noIssue = true;
    await report(created.api, { report: reportPath, 'allow-close': 'true' });
    assert.equal(created.writes.filter(item => item.path === '/issues' && item.method === 'POST').length, 1);
    writeFileSync(reportPath, JSON.stringify({ ...candidateClean, status: 'error', result_changed: false, changedFiles: [] }));
    const errored = apiFixture(); await report(errored.api, { report: reportPath, 'allow-close': 'true' });
    assert.equal(errored.writes.some(item => item.method === 'PATCH'), false);
    assert.match(readFileSync(process.env.GITHUB_STEP_SUMMARY, 'utf8'), /"mainRawCount": 1/);
    const large = { ...clean, status: 'error', error: 'short diagnostic', rawBefore: { x: [{ url: 'GHSA-x', details: '脆'.repeat(400000) }] },
      rawAfter: {}, remaining: [{ message: 'r'.repeat(7000) }], nativeFix: { exitCode: 1, stderr: 'short stderr' } };
    writeFileSync(process.env.GITHUB_STEP_SUMMARY, 'x'.repeat(800 * 1024)); writeFileSync(reportPath, JSON.stringify(large));
    const oversized = apiFixture(); await report(oversized.api, { report: reportPath, 'allow-close': 'true' });
    assert.ok(statSync(process.env.GITHUB_STEP_SUMMARY).size <= 900 * 1024);
    const boundedSummary = readFileSync(process.env.GITHUB_STEP_SUMMARY, 'utf8');
    assert.match(boundedSummary, /short diagnostic/); assert.match(boundedSummary, /short stderr/); assert.match(boundedSummary, /\[省略\]/);
    const issueText = oversized.comments[0].body.replace(/&#(\d+);/g, (_, code) => String.fromCodePoint(Number(code)));
    assert.match(issueText, /short diagnostic/); assert.match(issueText, /short stderr/); assert.match(issueText, /\[省略\]/);
    writeFileSync(reportPath, JSON.stringify(clean));
    for (const flags of [{ moved: true }, { invalid: true }]) { const blocked = apiFixture(flags); await assert.rejects(report(blocked.api, { report: reportPath, 'allow-close': 'true' })); assert.equal(blocked.writes.some(item => item.method === 'PATCH'), false); }
  } finally { if (beforeSha === undefined) delete process.env.GITHUB_SHA; else process.env.GITHUB_SHA = beforeSha; }
});
