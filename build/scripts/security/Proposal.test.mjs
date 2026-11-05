import { test, expect } from 'bun:test';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { prepare, reconcile, report, BRANCH } from './Security-Pull-Request.mjs';
import { ledgerGenesis, readLedger, saveRecord } from './Security-Record.mjs';

const bot = { id: 41898282, login: 'github-actions[bot]', type: 'Bot' }, sha = c => c.repeat(40);
const nonce = '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
const guardProof = (pr, head) => ({ id: 44, app_id: 123, creator: { id: 99, login: 'guard[bot]' }, nonce, context: 'kagayoi/automation-merge-guard-v1', state: 'success', target_url: 'https://github.com/owner/repo/actions/runs/9/attempts/1', description: `PR ${pr} H ${head}; run 9 a 1; ${nonce}` });
async function saveIntent(api, record, proof) {
  const signing = { ...record, state: 'signing', nonce: proof.nonce, guard: { app_id: proof.app_id, client_id: 'client123', slug: 'guard', creator: proof.creator } };
  await saveRecord(api, { id: 12 }, signing);
  await saveRecord(api, { id: 12 }, { ...signing, state: 'signed' }, proof);
  await saveRecord(api, { id: 12 }, record, proof);
}
function fixture() {
  const root = process.env.BUN_SECURITY_TEST_ROOT;
  if (!root) throw Error('BUN_SECURITY_TEST_ROOT required');
  const dir = mkdtempSync(join(root, 'proposal-')), checkout = join(dir, 'checkout'), candidate = join(dir, 'candidate');
  mkdirSync(checkout); mkdirSync(candidate);
  const git = (args, input) => { const r = spawnSync('git', args, { cwd: checkout, input, encoding: 'utf8', env: { ...process.env, GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.invalid', GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.invalid' } }); if (r.status !== 0) throw Error(r.stderr); return r.stdout.trim(); };
  git(['init', '--quiet']); const tree = git(['mktree'], ''), base = git(['commit-tree', tree], 'base'); git(['update-ref', 'HEAD', base]);
  const report = join(dir, 'report.json'); writeFileSync(report, JSON.stringify({ changedFiles: ['package.json'], candidateRoot: candidate, remaining: [], needs_followup: false }));
  const f = { base, dir, checkout, candidate, report, branch: null, pr: null, refs: new Map(), prs: new Map(), statuses: new Map(), comments: [], calls: [], commits: new Map(), blobs: new Map(), seq: 0, nextPr: 7, created: 0 };
  f.advance = () => { f.base = git(['commit-tree', tree, '-p', f.base], 'advanced'); git(['update-ref', 'HEAD', f.base]); process.env.GITHUB_SHA = f.base; for (const pr of f.prs.values()) pr.base.sha = f.base; };
  f.write = content => writeFileSync(join(candidate, 'package.json'), content);
  f.write('candidate A');
  f.api = { repository: 'owner/repo', async request(path, method = 'GET', body) {
    f.calls.push({ path, method, body });
    if (path === '') return { id: 12, node_id: 'R_12', has_issues: true, full_name: 'owner/repo', default_branch: 'main' };
    if (path === '/issues/8') return { number: 8, state: 'open', user: bot, repository_url: 'https://api.github.com/repos/owner/repo', body: ledgerGenesis(12, 8) };
    if (path === '/issues/9' && method === 'PATCH') { f.notification.state = body.state; return f.notification; }
    if (path === '/issues/9') return f.notification;
    if (path === '/issues/8/comments' && method === 'POST') { const item = { id: 100 + f.comments.length, user: bot, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: body.body }; f.comments.push(item); return item; }
    if (path === '/git/ref/heads/main') return { object: { sha: f.base } };
    if (path.startsWith(`/git/ref/heads/${BRANCH}`)) { const head = f.refs.get(path.slice('/git/ref/heads/'.length)); return head ? { object: { sha: head } } : null; }
    if (path.startsWith('/pulls?state=closed') || path.startsWith('/pulls?state=all')) throw Error('historical PR scan is forbidden');
    if (path.startsWith('/pulls?state=open')) return f.pr?.state === 'open' ? [f.pr] : [];
    if (path.startsWith('/pulls/') && path.includes('/commits?')) return [{ sha: f.prs.get(Number(path.split('/')[2])).head.sha }];
    if (/^\/pulls\/\d+$/.test(path)) { const pr = f.prs.get(Number(path.split('/')[2])); return pr ? { ...pr, commits: 1 } : null; }
    if (path.includes('/statuses?')) return f.statuses.get(path.split('/')[2]) ?? [];
    if (path === `/git/commits/${f.base}`) return { sha: f.base, tree: { sha: sha('b') } };
    if (path === '/git/blobs' && method === 'POST') { const bytes = Buffer.from(body.content, 'base64'); f.nextBlob = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex'); return { sha: f.nextBlob }; }
    if (path === '/git/trees' && method === 'POST') return { sha: f.nextBlob };
    if (path === '/git/commits' && method === 'POST') { const head = (++f.seq).toString(16).repeat(40); f.commits.set(head, { sha: head, tree: { sha: body.tree }, message: body.message, parents: body.parents.map(sha => ({ sha })) }); f.blobs.set(head, { package_blob: f.nextBlob, lock_blob: sha('9') }); return { sha: head }; }
    if (path.startsWith('/git/commits/') && f.commits.has(path.split('/').at(-1))) {
      const value = f.commits.get(path.split('/').at(-1));
      if (f.badCommit === 'sha') return { ...value, sha: sha('f') };
      if (f.badCommit === 'tree') return { ...value, tree: { sha: sha('f') } };
      if (f.badCommit === 'parents') return { ...value, parents: [...value.parents].reverse().length > 1 ? [...value.parents].reverse() : [{ sha: sha('f') }] };
      if (f.badCommit === 'marker') return { ...value, message: 'ordinary commit' };
      if (f.badCommit === 'missing') return { ...value, sha: undefined };
      return value;
    }
    if (path.startsWith('/contents/build/scripts/')) { const url = new URL('https://fixture.invalid' + path), target = url.searchParams.get('ref'); const key = url.pathname.endsWith('package.json') ? 'package_blob' : 'lock_blob'; return { type: 'file', sha: target === f.base ? sha('9') : f.blobs.get(target)?.[key] }; }
    if (path === '/git/refs' && method === 'POST') { f.branch = body.sha; f.refs.set(body.ref.slice('refs/heads/'.length), body.sha); return {}; }
    if (path.startsWith('/compare/')) { const left = path.slice('/compare/'.length).split('...')[0]; return { files: [{ filename: 'build/scripts/package.json', status: 'modified' }], base_commit: { sha: left }, merge_base_commit: { sha: left }, status: 'ahead', ahead_by: 1, behind_by: 0 }; }
    if (path.startsWith('/git/ref/pull/')) { f.mergeHead = f.prs.get(Number(path.split('/')[4])).head.sha; return { object: { sha: sha('e') } }; }
    if (path === `/git/commits/${sha('e')}`) return { sha: sha('e'), parents: [{ sha: f.base }, { sha: f.mergeHead }] };
    throw Error(`Unexpected ${method} ${path}`);
  }, async response(path, method = 'GET', body) {
    f.calls.push({ path, method, body });
    if (path.includes('/statuses?per_page=')) return { status: 200, data: await this.request(path), headers: { link: null, remaining: '1000' } };
    if (path.startsWith('/issues/8/comments?')) return { status: 200, data: f.comments, headers: { link: null, remaining: '1000' } };
    if (path.startsWith('/issues?state=open')) return { status: 200, data: f.notification?.state === 'open' ? [f.notification] : [], headers: { link: null, remaining: '1000' } };
    if (path.startsWith('/pulls?state=open&per_page=100&page=')) return { status: 200, data: [...f.prs.values()].filter(pr => pr.state === 'open'), headers: { link: null, remaining: '1000' } };
    if (path === '/pulls' && method === 'POST') {
      f.created++; if (f.noCreate) throw Error('lost response');
      f.beforeCreate?.();
      f.pr = { number: f.nextPr++, state: 'open', draft: false, merged: false, auto_merge: null, user: bot, base: { sha: f.base, ref: 'main', repo: { id: 12, full_name: 'owner/repo' } }, head: { sha: f.refs.get(body.head), ref: body.head, repo: { id: 12, full_name: 'owner/repo' } } };
      f.prs.set(f.pr.number, f.pr); if (f.lostCreate) throw Error('lost response');
      return { status: f.createStatus ?? 201, data: f.noNumber ? {} : f.pr };
    }
    throw Error(`Unexpected response ${method} ${path}`);
  }, async graphql(query, variables) {
    if (query.startsWith('query')) return { status: 200, data: { data: { repository: { pullRequest: { number: variables.number, timelineItems: { nodes: f.timeline ?? [], pageInfo: { hasPreviousPage: false, startCursor: null } } } } } } };
    const { input } = variables;
    f.calls.push({ path: '/graphql', method: 'POST', body: input });
    const item = input.refUpdates[0], branch = item.name.slice('refs/heads/'.length);
    if (f.beforeCas) f.beforeCas();
    if (f.refs.get(branch) !== item.beforeOid || item.force !== false) return { status: 200, data: { errors: [{ message: 'ref update rejected' }] } };
    f.refs.set(branch, item.afterOid); f.branch = item.afterOid;
    const pr = [...f.prs.values()].find(value => value.head.ref === branch); if (pr) pr.head.sha = item.afterOid;
    if (f.casThrows) throw Error('lost response');
    return { status: 200, data: { data: { updateRefs: { clientMutationId: input.clientMutationId } } } };
  } };
  f.run = () => prepare(f.api, { report: f.report, checkout: f.checkout });
  return f;
}
async function runWithFixture(action) {
  const f = fixture(), ledger = process.env.BUN_SECURITY_LEDGER_ISSUE, oldSha = process.env.GITHUB_SHA;
  process.env.BUN_SECURITY_LEDGER_ISSUE = '8'; process.env.GITHUB_SHA = f.base;
  try { await action(f); } finally { if (ledger === undefined) delete process.env.BUN_SECURITY_LEDGER_ISSUE; else process.env.BUN_SECURITY_LEDGER_ISSUE = ledger; if (oldSha === undefined) delete process.env.GITHUB_SHA; else process.env.GITHUB_SHA = oldSha; }
}

test('closed A→別 B→closed B→A を独立した generation で処理する', async () => runWithFixture(async f => {
  expect((await f.run()).pr_number).toBe(7); const firstHead = f.branch;
  f.pr.state = 'closed'; expect((await reconcile(f.api)).blocked).toBe(false);
  await expect(f.run()).rejects.toThrow('Closed candidate is unchanged');
  f.write('candidate B'); expect((await f.run()).pr_number).toBe(8); expect(f.branch).not.toBe(firstHead);
  f.pr.state = 'closed'; expect((await reconcile(f.api)).blocked).toBe(false);
  f.branch = null; f.write('candidate A'); expect((await f.run()).pr_number).toBe(9);
  expect(f.calls.some(call => call.path.startsWith('/pulls?state=closed') || call.path.startsWith('/pulls?state=all'))).toBe(false);
}));

test('create応答断はPRを推測せず preparing で再POSTしない', async () => runWithFixture(async f => {
  f.lostCreate = true; await expect(f.run()).rejects.toThrow('PR creation outcome is unknown'); expect(f.created).toBe(1);
  expect((await readLedger(f.api, { id: 12 })).state).toBe('preparing'); expect((await reconcile(f.api)).blocked).toBe(true);
  await expect(f.run()).rejects.toThrow('Proposal operation outcome is unknown'); expect(f.created).toBe(1);
  const g = fixture(); const prior = process.env.GITHUB_SHA; process.env.GITHUB_SHA = g.base;
  try { g.noCreate = true; await expect(g.run()).rejects.toThrow('PR creation outcome is unknown'); expect((await readLedger(g.api, { id: 12 })).state).toBe('preparing'); await expect(g.run()).rejects.toThrow('Proposal operation outcome is unknown'); expect((await reconcile(g.api)).blocked).toBe(true); expect(g.created).toBe(1); }
  finally { process.env.GITHUB_SHA = prior; }
}));

test('本人reopen後の候補変更は同じPRにCAS更新する', async () => runWithFixture(async f => {
  await f.run(); const previous = f.branch; f.pr.state = 'closed'; await reconcile(f.api);
  f.pr.state = 'open'; expect((await reconcile(f.api)).blocked).toBe(false); expect((await readLedger(f.api, { id: 12 })).proposal.head).toBe(previous);
  f.write('candidate B'); expect((await f.run()).pr_number).toBe(7);
  expect(f.prs.get(7).state).toBe('open'); expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
}));

test('同じ候補は proposal を再利用し、その branch 変更は保留する', async () => runWithFixture(async f => {
  await f.run(); const before = f.calls.length, head = f.branch;
  expect(f.commits.get(head).message).toContain('Bun-Security-Automation: v1');
  expect((await f.run()).head_sha).toBe(head);
  expect(f.calls.slice(before).some(call => call.method === 'PATCH' || call.path === '/git/commits' && call.method === 'POST')).toBe(false);
  f.refs.set(f.pr.head.ref, sha('f')); await expect(f.run()).rejects.toThrow('Ledger proposal is not current');
}));

test('native intent が未解決なら closed proposal 更新より優先して保留する', async () => runWithFixture(async f => {
  await f.run(); const proposal = (await readLedger(f.api, { id: 12 })).proposal;
  const record = { version: 1, repo: 12, pr: proposal.pr, base: f.base, head: proposal.head, merge: sha('e'), tree: sha('a'), run: 9, attempt: 1, state: 'intent' };
  const proof = guardProof(proposal.pr, proposal.head);
  await saveIntent(f.api, record, proof);
  f.pr.state = 'closed'; const before = f.comments.length;
  expect((await reconcile(f.api)).blocked).toBe(true);
  expect(f.comments).toHaveLength(before);
}));

test('merged 後の自動 branch 削除は新 candidate を current main から作成できる', async () => runWithFixture(async f => {
  await f.run(); const proposal = (await readLedger(f.api, { id: 12 })).proposal;
  const record = { version: 1, repo: 12, pr: proposal.pr, base: f.base, head: proposal.head, merge: sha('e'), tree: sha('a'), run: 9, attempt: 1, state: 'intent' };
  const proof = guardProof(proposal.pr, proposal.head);
  await saveIntent(f.api, record, proof);
  await saveRecord(f.api, { id: 12 }, { ...record, state: 'merged' }, null, sha('d'));
  f.pr.state = 'closed'; f.pr.merged = true; f.branch = null; f.write('candidate B');
  expect((await f.run()).pr_number).toBe(8);
  expect((await readLedger(f.api, { id: 12 })).lastCompletion.proposal).toEqual(proposal);
}));

test('作成直後の commit SHA・tree・親・marker が不正なら ledger と ref を更新しない', async () => {
  for (const bad of ['sha', 'tree', 'parents', 'marker', 'missing']) await runWithFixture(async f => {
    f.badCommit = bad;
    await expect(f.run()).rejects.toThrow('Created candidate commit identity changed');
    expect(f.branch).toBeNull(); expect(f.comments).toHaveLength(0);
    expect(f.calls.some(call => call.path === '/git/refs' || call.path === '/pulls' && call.method === 'POST')).toBe(false);
  });
});

test('新 commit の親が違う場合は新 branch を作成しない', async () => runWithFixture(async f => {
  await f.run(); const beforeHead = f.branch, oldBranch = f.pr.head.ref;
  f.write('candidate B'); f.badCommit = 'parents';
  await expect(f.run()).rejects.toThrow('Updated candidate commit identity changed');
  expect(f.branch).toBe(beforeHead); expect(f.refs.get(oldBranch)).toBe(beforeHead); expect(f.refs.size).toBe(1);
}));

test('候補変更は同じbranch/PRをbeforeOid付き二親CASで更新する', async () => runWithFixture(async f => {
  await f.run(); const old = f.pr, oldBranch = old.head.ref, oldHead = old.head.sha;
  f.write('candidate B'); const result = await f.run(); const current = f.pr;
  expect(result.pr_number).toBe(7); expect(current.head.ref).toBe(oldBranch); expect(current.head.sha).not.toBe(oldHead);
  const update = f.calls.find(call => call.path === '/graphql').body.refUpdates[0];
  expect(update).toEqual({ name: `refs/heads/${oldBranch}`, beforeOid: oldHead, afterOid: current.head.sha, force: false });
  expect(f.commits.get(current.head.sha).parents.map(item => item.sha)).toEqual([oldHead, f.base]);
  expect(f.calls.some(call => call.method === 'PATCH' && call.path.startsWith('/git/refs'))).toBe(false);
}));

test('CASの競合・応答断・close/draft競合を同じPRで保留または確定する', async () => {
  for (const mode of ['conflict', 'lost-response', 'closed', 'draft']) await runWithFixture(async f => {
    await f.run(); const pr = f.pr, branch = pr.head.ref; f.write('candidate B');
    if (mode === 'conflict') f.beforeCas = () => f.refs.set(branch, sha('f'));
    if (mode === 'lost-response') f.casThrows = true;
    if (mode === 'closed') f.beforeCas = () => { pr.state = 'closed'; };
    if (mode === 'draft') f.beforeCas = () => { pr.draft = true; };
    if (mode === 'lost-response') expect((await f.run()).pr_number).toBe(pr.number);
    else {
      await expect(f.run()).rejects.toThrow();
      expect((await readLedger(f.api, { id: 12 })).state).toBe('preparing-update');
    }
    expect(f.created).toBe(1); expect(f.calls.filter(call => call.path === '/graphql')).toHaveLength(1);
    expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
    if (mode === 'conflict') expect(f.refs.get(branch)).toBe(sha('f'));
  });
});

test('CAS後に本人のauto-merge解除を観測したらproposal更新を確定しない', async () => runWithFixture(async f => {
  await f.run(); const pr = f.pr; f.write('candidate B');
  f.beforeCas = () => { f.timeline = [{ id: 'E1', __typename: 'AutoMergeDisabledEvent', createdAt: '2026-11-05T00:00:01Z', actor: { __typename: 'User', id: 'U1', login: 'owner' } }]; };
  await expect(f.run()).rejects.toThrow('Human disabled auto-merge');
  expect((await readLedger(f.api, { id: 12 })).state).toBe('preparing-update');
  expect(pr.state).toBe('open'); expect(f.created).toBe(1);
  expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
}));

test('A→B→A は同じPRで連続する二親commitにする', async () => runWithFixture(async f => {
  await f.run(); const a = f.pr, head = a.head.sha;
  f.write('candidate B'); await f.run(); const b = f.pr;
  f.write('candidate A'); const restored = await f.run();
  expect(restored.pr_number).toBe(a.number); expect(restored.head_sha).not.toBe(head);
  expect(a.state).toBe('open'); expect(b.state).toBe('open'); expect(f.created).toBe(1);
  expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
}));

test('現在PRがdraftなら候補を更新しない', async () => runWithFixture(async f => {
  await f.run(); const a = f.pr; f.write('candidate B'); await f.run();
  a.draft = true; f.write('candidate A');
  await expect(f.run()).rejects.toThrow('Proposal is not open and ready');
  expect(f.created).toBe(1); expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
}));

test('本人がcloseした直近候補を再作成せず、別内容は新規候補にする', async () => runWithFixture(async f => {
  await f.run(); const a = f.pr;
  f.write('candidate B'); await f.run(); const b = f.pr; b.state = 'closed';
  expect((await reconcile(f.api)).blocked).toBe(false);
  expect((await readLedger(f.api, { id: 12 })).proposal).toBeNull();
  await expect(f.run()).rejects.toThrow('Closed candidate is unchanged');
  f.write('candidate A'); const restored = await f.run();
  expect(restored.pr_number).toBe(8); expect(f.created).toBe(2);
  expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
}));

test('CAS直前にPRがdraftへ変わってもcloseやmergeをせず保留する', async () => runWithFixture(async f => {
  await f.run(); const old = f.pr;
  f.write('candidate B'); f.beforeCas = () => { old.draft = true; };
  await expect(f.run()).rejects.toThrow('Updated PR is not safely confirmed');
  expect(old.state).toBe('open'); expect(old.draft).toBe(true); expect(f.calls.filter(call => call.method === 'PATCH')).toHaveLength(0);
  expect((await readLedger(f.api, { id: 12 })).state).toBe('preparing-update');
}));

test('422・番号欠落のPR作成は preparing に残し再POSTしない', async () => {
  for (const mode of ['422', 'missing']) await runWithFixture(async f => {
    if (mode === '422') f.createStatus = 422; else f.noNumber = true;
    await expect(f.run()).rejects.toThrow('PR creation outcome is unknown');
    expect((await readLedger(f.api, { id: 12 })).state).toBe('preparing');
    expect((await reconcile(f.api)).blocked).toBe(true); expect(f.created).toBe(1);
  });
});


test('main前進時は同じPRを二親CAS更新し、draftは保留する', async () => runWithFixture(async f => {
  await f.run(); const old = f.pr, oldHead = old.head.sha;
  f.pr.draft = true; f.advance(); await expect(f.run()).rejects.toThrow('Proposal is not open and ready');
  expect(f.created).toBe(1); expect(old.state).toBe('open');
  f.pr.draft = false; expect((await f.run()).pr_number).toBe(7);
  expect(old.state).toBe('open'); expect(f.pr.head.sha).not.toBe(oldHead); expect(f.pr.base.sha).toBe(f.base);
}));

test('main前進後もPR baseとmerge refが古ければ待機しstatusを出さない', async () => runWithFixture(async f => {
  await f.run(); const previousBase = f.base; f.advance(); f.pr.base.sha = previousBase;
  await expect(f.run()).rejects.toThrow('Updated PR is not safely confirmed');
  expect(f.calls.some(call => call.method === 'POST' && call.path.startsWith('/statuses/'))).toBe(false);
  expect(f.calls.some(call => call.method === 'PATCH')).toBe(false);
}));

test('完全な現main監査の候補ゼロでproposalだけ解除しPRを残す', async () => runWithFixture(async f => {
  await f.run();
  const clean = { changedFiles: [], candidateRoot: f.candidate, status: 'clean', rawAfter: {}, remaining: [], needs_followup: false, upstreamFixes: [], result_changed: false };
  writeFileSync(f.report, JSON.stringify({ ...clean, needs_followup: true }));
  expect((await f.run()).has_changes).toBe(false); expect(f.pr.state).toBe('open');
  writeFileSync(f.report, JSON.stringify(clean)); expect((await f.run()).has_changes).toBe(false);
  expect(f.pr.state).toBe('open'); expect((await readLedger(f.api, { id: 12 })).proposal).toBeNull(); expect(f.created).toBe(1);
}));


test('確定拒否は記録し、同一PR候補を保全する', async () => runWithFixture(async f => {
  await f.run(); const proposal = (await readLedger(f.api, { id: 12 })).proposal, oldHead = proposal.head;
  const record = { version: 1, repo: 12, pr: proposal.pr, base: f.base, head: oldHead, merge: sha('e'), tree: sha('a'), run: 9, attempt: 1, state: 'intent' };
  const proof = guardProof(proposal.pr, oldHead);
  f.statuses.set(oldHead, [{ ...proof, url: `https://api.github.com/repos/owner/repo/statuses/${oldHead}` }]);
  await saveIntent(f.api, record, proof);
  await saveRecord(f.api, { id: 12 }, { ...record, state: 'failed', http_status: 409 });
  expect((await reconcile(f.api)).blocked).toBe(false);
  expect((await readLedger(f.api, { id: 12 })).lastFailure.record.head).toBe(oldHead);
  expect((await f.run()).pr_number).toBe(7); expect(f.pr.head.sha).toBe(oldHead);
}));

test('旧PRがopenでも現mainの監査が解決済みなら通知closeを妨げない', async () => runWithFixture(async f => {
  await f.run(); const old = f.pr;
  f.write('candidate B'); await f.run();
  const clean = { changedFiles: [], candidateRoot: f.candidate, status: 'clean', rawAfter: {}, remaining: [], needs_followup: false, upstreamFixes: [], result_changed: false };
  writeFileSync(f.report, JSON.stringify(clean)); await f.run();
  expect((await readLedger(f.api, { id: 12 })).proposal).toBeNull(); old.state = 'open';
  f.notification = { number: 9, state: 'open', user: bot, body: '<!-- bun-security-automation -->\noriginal' };
  expect((await report(f.api, { report: f.report, 'allow-close': 'true' })).issue_reporting).toBe('clear');
  expect(f.notification.state).toBe('closed');
}));
