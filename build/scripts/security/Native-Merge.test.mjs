import { test, expect } from 'bun:test';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { attest, sign, merge, client, BRANCH, needsSecurityCommit, otherChecks, reconcile, report } from './Security-Pull-Request.mjs';
import { ledgerGenesis, readLedger } from './Security-Record.mjs';
import { STATUS_CONTEXT, OWNER, requiredRule, requestMerge, statusHistory, latestStatuses, statusTarget } from './Native-Merge.mjs';

const APP_ID = 123, on = { BUN_SECURITY_NATIVE_MERGE_ACTIVATED: 'true', DEPENDENCY_GUARD_APP_ID: '123', DEPENDENCY_GUARD_APP_CLIENT_ID: 'client123', DEPENDENCY_GUARD_APP_SLUG: 'guard', DEPENDENCY_GUARD_APP_BOT_ID: '99', DEPENDENCY_GUARD_ACTION_SLUG: 'guard', DEPENDENCY_GUARD_APP_TOKEN: 'fixture-token', DEPENDENCY_GUARD_RULESET_ID: '1', DEPENDENCY_GUARD_RULESET_UPDATED_AT: '2026-11-05T00:00:00Z' };
const hash = value => value.repeat(40);
const branch = BRANCH + '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
const mockStatus = (head, id, context, state = 'success') => ({ id, context, state, url: `https://api.github.com/repos/owner/repo/statuses/${head}`, target_url: null, description: null, creator: { id: 41898282, login: 'github-actions[bot]' } });
const withoutCreator = status => { const { creator, ...combined } = status; return combined; };
function fixture() {
    const f = { base: hash('1'), head: hash('2'), tested: hash('3'), actual: hash('4'), tree: hash('5'), writes: [], puts: 0,
        history: [], events: [], comments: [], ruleReads: 0, result: 'merged', http: 200, deniedStatus: false, intentThrows: false };
    const repository = 'owner/repo';
    f.repo = { id: 12, has_issues: true, full_name: repository, default_branch: 'main', owner: { id: OWNER, type: 'User' } };
    f.pr = { number: 7, state: 'open', merged: false, draft: false, auto_merge: null, user: { id: 41898282, login: 'github-actions[bot]', type: 'Bot' },
        base: { sha: f.base, ref: 'main', repo: { id: 12, full_name: repository } },
        head: { sha: f.head, ref: branch, repo: { id: 12, full_name: repository } } };
    f.current = { id: 90, run_attempt: 2, workflow_id: 91, check_suite_id: 92, event: 'schedule', head_branch: 'main', head_sha: f.base,
        path: '.github/workflows/dependency-security.yml', repository: { full_name: repository }, head_repository: { full_name: repository } };
    f.jobs = ['Validate dependency changes', 'Validate security merge guard', 'Require validation success'].map((name, i) => ({ id: i + 1,
        name: `Validate fixed merge / ${name}`, status: 'completed', conclusion: 'success', run_id: 90, run_attempt: 2, head_sha: f.base,
    }));
    const parameters = { strict_required_status_checks_policy: true, required_status_checks: [{ context: STATUS_CONTEXT, integration_id: APP_ID }] };
    f.effective = [{ type: 'required_status_checks', parameters, ruleset_id: 1, ruleset_source_type: 'Repository', ruleset_source: repository }];
    f.full = { id: 1, updated_at: on.DEPENDENCY_GUARD_RULESET_UPDATED_AT, target: 'branch', source_type: 'Repository', source: repository, enforcement: 'active',
        conditions: { ref_name: { include: ['refs/heads/main'], exclude: [] } }, rules: [{ type: 'required_status_checks', parameters: structuredClone(parameters) }],
        bypass_actors: [{ actor_type: 'User', actor_id: OWNER, bypass_mode: 'always' }] };
    f.validated = [];
    f.ledger = { number: 8, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, repository_url: 'https://api.github.com/repos/owner/repo', body: ledgerGenesis(12, 8) };
    const candidate = { package_blob: hash('6'), lock_blob: hash('7') };
    const common = { version: 1, repo: 12, issue: 8, record: null, proof: null, last_completion: null, last_failure: null, last_closed: null };
    const seed = [
      { ...common, prev: 0, state: 'preparing', proposal: null, preparing: { base: f.base, branch, pr: null, head: f.head, ...candidate } },
      { ...common, prev: 100, state: 'idle', proposal: { pr: 7, branch, base: f.base, head: f.head, ...candidate }, preparing: null },
    ];
    f.comments = seed.map((event, index) => ({ id: 100 + index, user: { id: 41898282, login: 'github-actions[bot]' }, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: `<!-- bun-security-ledger-event-v1:${JSON.stringify(event)} -->` }));
    f.options = () => ({ pr: '7', 'base-sha': f.base, 'head-sha': f.head, 'merge-sha': f.tested, 'validation-success': 'true', ...f.signed });
    f.api = { repository, async request(path, method = 'GET', body) {
        f.events.push(`${method} ${path}`);
        if (method !== 'GET') {
            f.writes.push({ path, method, body });
            if (path === '/issues') {
                f.issue = { number: 8, state: 'open', user: { id: 41898282 }, body: body.body };
                return f.issue;
            }
            if (path === '/issues/8/comments') {
                if (f.denyAbort && body.body.includes('"state":"aborted-before-request"')) throw new Error('timeout');
                if (f.denySigned && body.body.includes('"state":"signed"')) throw new Error('timeout');
                const comment = { id: 100 + f.comments.length, user: { id: 41898282, login: 'github-actions[bot]' }, issue_url: 'https://api.github.com/repos/owner/repo/issues/8', created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z', body: body.body };
                f.comments.push(comment); if (body.body.includes('"state":"signing"')) f.afterSigning?.(); if (body.body.includes('"state":"intent"')) f.afterIntent?.(); return comment;
            }
            if (path === '/issues/9' && method === 'PATCH' && body.state === 'closed') { f.issue.state = 'closed'; return f.issue; }
            if (f.deniedStatus) throw new Error('HTTP 403');
            const status = { ...mockStatus(path.split('/').pop(), 200 + f.writes.length, body.context, body.state), ...body, creator: { id: 99, login: 'guard[bot]' } };
            if (path === `/statuses/${f.tested}`) { f.validated = [status]; return status; }
            if (path === `/statuses/${f.head}`) {
                if (!f.intentThrows || f.intentPersists) f.history.unshift(status);
                if (f.intentThrows) throw new Error('timeout');
                return status;
            }
            throw new Error(`Unexpected write: ${path}`);
        }
        if (path === '') return f.repo;
        if (path.startsWith('/pulls?state=all')) return [f.pr];
        if (path.startsWith('/pulls?state=open')) return f.pr.state === 'open' ? [f.pr] : [];
        if (path.startsWith('/issues?')) return f.issue ? [f.issue] : [];
        if (path.startsWith('/issues/comments?')) {
            const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
            return f.comments.slice((page - 1) * 100, page * 100);
        }
        if (path.startsWith('/issues/8/comments?')) {
            const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
            return f.comments.slice((page - 1) * 100, page * 100);
        }
        if (path === '/issues/8') return f.ledger;
        if (path === '/issues/9') return f.issue;
        if (path === '/pulls/7') return { ...f.pr, commits: 1 };
        if (path.startsWith('/pulls/7/commits?')) return [{ sha: f.pr.head.sha }];
        if (path === '/git/ref/heads/main') return { object: { sha: f.branch ?? f.base } };
        if (path === `/git/ref/heads/${branch}`) return { object: { sha: f.head } };
        if (path === '/git/ref/pull/7/merge') return { object: { sha: f.mergeRef ?? f.tested } };
        if (path === `/contents/build/scripts/package.json?ref=${f.head}`) return { type: 'file', sha: f.badProposalBlob ? hash('9') : hash('6') };
        if (path === `/contents/build/scripts/bun.lock?ref=${f.head}`) return { type: 'file', sha: hash('7') };
        if (path === `/git/commits/${f.head}`) return { sha: f.missingHeadSha ? undefined : f.head, tree: { sha: f.tree }, parents: [{ sha: f.originalBase ?? f.base }], message: f.headMessage ?? `fix(deps): update\n\nBun-Security-Generation: ${branch.slice(BRANCH.length)}\nBun-Security-Automation: v1` };
        if (path === `/git/commits/${f.tested}` || path === `/git/commits/${f.actual}`) return { sha: path.endsWith(f.tested) ? (f.missingTestedSha ? undefined : f.tested) : (f.missingActualSha ? undefined : f.actual), tree: { sha: f.badTree && path.endsWith(f.actual) ? hash('9') : f.tree }, parents: path.endsWith(f.tested) ? [{ sha: f.badMergeParents ? hash('9') : f.base }, { sha: f.head }] : [{ sha: f.base }] };
        if (f.originalBase && path === `/compare/${f.originalBase}...${f.base}`) return { status: 'ahead', base_commit: { sha: f.originalBase }, merge_base_commit: { sha: f.originalBase }, ahead_by: 1, behind_by: 0 };
        if (path === `/compare/${f.actual}...${f.branch}`) return f.comparison ?? {
            status: f.branch === f.actual ? 'identical' : 'ahead', ahead_by: f.branch === f.actual ? 0 : 1,
            behind_by: 0, base_commit: { sha: f.actual }, merge_base_commit: { sha: f.actual },
        };
        if (path.startsWith('/compare/')) return { files: f.diff ?? [{ filename: 'build/scripts/bun.lock', status: 'modified' }] };
        if (path === '/rules/branches/main') { if (f.deniedRules) throw new Error('HTTP 403'); f.ruleReads++; f.beforeRule?.(); return f.effective; }
        if (path === '/rulesets/1') return f.full;
        if (path === '/actions/runs/90') return f.current;
        if (path === '/actions/workflows/dependency-security.yml') return { id: 91, path: f.current.path };
        if (path.startsWith('/actions/runs/90/jobs?')) return { total_count: f.jobs.length, jobs: f.jobs };
        if (path.includes('/check-suites?')) return { total_count: 0, check_suites: [] };
        if (path.includes('/check-runs?')) return { total_count: (f.checks ?? []).length, check_runs: (f.checks ?? []).map((check, i) => ({ id: i + 1, ...check })) };
        if (path === `/commits/${f.tested}/status?per_page=100`) { if (f.replaceValidated) { f.validated = f.validated.map(item => ({ ...item, id: item.id + 1 })); f.replaceValidated = false; } return { sha: f.tested, statuses: f.validated.map(withoutCreator), total_count: f.validated.length }; }
        if (path.startsWith(`/commits/${f.tested}/statuses?`)) return f.validated;
        if (path === `/commits/${f.head}/status?per_page=100`) { const latest = [...new Map([...f.history].reverse().map(s => [s.context, s])).values()]; return { sha: f.head, statuses: latest.map(withoutCreator), total_count: latest.length }; }
        if (path.startsWith(`/commits/${f.head}/statuses?`)) {
            const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
            return f.history.slice((page - 1) * 100, page * 100);
        }
        if (path.includes('/statuses?per_page=')) return [];
        throw new Error(`Unexpected read: ${path}`);
    }, async response(path, method = 'GET', body) {
        f.events.push(`${method} ${path}`);
        if (path.includes('/statuses?per_page=')) return { status: 200, data: await this.request(path), headers: { link: null, remaining: '1000' } };
        if (path.includes('/jobs?filter=latest') || path.includes('/check-suites?') || path.includes('/check-runs?')) {
            const data = await this.request(path), url = new URL(`https://api.github.com/repos/${repository}${path}`);
            const page = Number(url.searchParams.get('page')), per = Number(url.searchParams.get('per_page')), last = Math.max(1, Math.ceil(data.total_count / per));
            let link = null; if (page < last) { url.searchParams.set('page', String(page + 1)); link = `<${url}>; rel="next"`; url.searchParams.set('page', String(last)); link += `, <${url}>; rel="last"`; }
            return { status: 200, data, headers: { link, remaining: '1000' } };
        }
        if (path.startsWith('/issues?state=open') && method === 'GET') return { status: 200, data: f.issue ? [f.issue] : [], headers: { link: null, remaining: '1000' } };
        if (path.startsWith('/issues/8/comments?') && method === 'GET') { const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page')); return { status: 200, data: f.comments.slice((page - 1) * 100, page * 100), headers: { link: null, remaining: '1000' } }; }
        if (path.startsWith('/pulls?state=open&per_page=100&page=')) return { status: 200, data: f.pr.state === 'open' ? [f.pr] : [], headers: { link: null, remaining: '1000' } };
        if (method === 'PUT') {
            f.puts++; f.mergeBody = body;
            if (f.timeout) throw new Error('timeout');
            if (f.http === 200 && f.result === 'merged') { f.pr.state = 'closed'; f.pr.merged = true; f.pr.merged_at = '2026-11-05'; f.pr.merge_commit_sha = f.actual; f.branch = f.mainAfter ?? f.actual; }
            return { status: f.http, data: { merged: f.result === 'merged', sha: f.actual } };
        }
        throw new Error(`Unexpected response: ${path}`);
    }, async global(path) {
        if (path === '/apps/guard') return f.guardApp ?? { id: APP_ID, client_id: 'client123', slug: 'guard' };
        if (path === '/users/guard%5Bbot%5D') return f.guardBot ?? { id: 99, login: 'guard[bot]', type: 'Bot' };
        throw new Error(`Unexpected global API: ${path}`);
    }, async graphql(_query, variables) {
        return { status: 200, data: { data: { repository: { pullRequest: { number: variables.number, timelineItems: { nodes: f.timeline ?? [], pageInfo: { hasPreviousPage: false, startCursor: null } } } } } } };
    } };
    return f;
}
async function inRun(action) {
    const old = process.env.GITHUB_RUN_ID, oldLedger = process.env.BUN_SECURITY_LEDGER_ISSUE; process.env.GITHUB_RUN_ID = '90'; process.env.BUN_SECURITY_LEDGER_ISSUE = '8';
    try { await action(); } finally { if (old === undefined) delete process.env.GITHUB_RUN_ID; else process.env.GITHUB_RUN_ID = old; if (oldLedger === undefined) delete process.env.BUN_SECURITY_LEDGER_ISSUE; else process.env.BUN_SECURITY_LEDGER_ISSUE = oldLedger; }
}
async function signed(f) { expect((await attest(f.api, f.options(), on)).ready).toBe(true); const result = await sign(f.api, f.options(), on, f.api); expect(result.ready).toBe(true); f.signed = { 'status-id': String(result.status_id), nonce: result.nonce }; }

test('署名 intent の中断後は同 nonce を一意に照合し、再 POST せず旧 CI で再開する', async () => inRun(async () => {
    const f = fixture(); f.denySigned = true;
    expect((await sign(f.api, f.options(), on, f.api)).ready).toBe(false);
    expect(f.history).toHaveLength(1); expect(f.puts).toBe(0);
    f.denySigned = false;
    f.api.global = async () => { throw new Error('recovery must not use App token'); };
    const recovered = await reconcile(f.api, on);
    expect(recovered.merged).toBe(true); expect(recovered.main_updated).toBe(true);
    expect(f.history).toHaveLength(1); expect(f.puts).toBe(1);
    const missing = fixture(); missing.denySigned = true;
    await sign(missing.api, missing.options(), on, missing.api); missing.history = [];
    expect((await reconcile(missing.api, on)).blocked).toBe(true); expect(missing.puts).toBe(0);
    const duplicate = fixture(); duplicate.denySigned = true;
    await sign(duplicate.api, duplicate.options(), on, duplicate.api);
    duplicate.history.push({ ...duplicate.history[0], id: 999 });
    expect((await reconcile(duplicate.api, on)).blocked).toBe(true); expect(duplicate.puts).toBe(0);
    const changed = fixture(); changed.denySigned = true;
    await sign(changed.api, changed.options(), on, changed.api);
    expect((await reconcile(changed.api, { ...on, DEPENDENCY_GUARD_APP_BOT_ID: '100' })).blocked).toBe(true);
    expect(changed.history).toHaveLength(1); expect(changed.puts).toBe(0);
}));
test('signed 後の再起動では snapshot・rule を再検証してから PUT する', async () => inRun(async () => {
    const f = fixture(); await signed(f); f.pr.draft = true;
    expect((await reconcile(f.api, on)).blocked).toBe(true); expect(f.puts).toBe(0);
    f.pr.draft = false;
    expect((await reconcile(f.api, on)).merged).toBe(true); expect(f.puts).toBe(1);
}));
test('status 送信前に draft を検出した場合は signing を abort する', async () => inRun(async () => {
    const f = fixture(); f.afterSigning = () => { f.pr.draft = true; };
    const result = await sign(f.api, f.options(), on, f.api);
    expect(result.merge_state).toBe('signing-aborted');
    expect(f.history).toHaveLength(0); expect(f.puts).toBe(0);
    expect(JSON.parse(f.comments.at(-1).body.match(/v1:(.*) -->/)[1]).state).toBe('signing-aborted');
}));
test('App token の自己照会と bot identity が owner pin に一致しなければ status を送らない', async () => inRun(async () => {
    for (const change of [
      f => { f.guardApp = { id: 124, client_id: 'client123', slug: 'guard' }; },
      f => { f.guardApp = { id: APP_ID, client_id: 'different', slug: 'guard' }; },
      f => { f.guardApp = { id: APP_ID, client_id: 'client123', slug: 'other' }; },
      f => { f.guardBot = { id: 100, login: 'guard[bot]', type: 'Bot' }; },
      f => { f.api.global = async () => { throw new Error('HTTP 403'); }; },
    ]) {
      const f = fixture(); change(f);
      expect((await sign(f.api, f.options(), on, f.api)).ready).toBe(false);
      expect(f.writes).toHaveLength(0); expect(f.puts).toBe(0);
    }
}));
test('ドットを含む App client ID を照合し署名と ledger に保存する', async () => inRun(async () => {
    const f = fixture(), clientId = 'Iv1.ab1112223334445c';
    f.guardApp = { id: APP_ID, client_id: clientId, slug: 'guard' };
    expect((await sign(f.api, f.options(), { ...on, DEPENDENCY_GUARD_APP_CLIENT_ID: clientId }, f.api)).ready).toBe(true);
    const ledger = await readLedger(f.api, f.repo);
    expect(ledger.state).toBe('signed'); expect(ledger.record.guard.client_id).toBe(clientId);
}));

test('activation既定OFFではnative merge APIを書かず、検証済み/設定待ちを返す', async () => {
    const api = { request() { throw new Error('must not access API'); } };
    for (const env of [{}, { BUN_SECURITY_NATIVE_MERGE_ACTIVATED: 'false' }, { BUN_SECURITY_NATIVE_MERGE_ACTIVATED: 'TRUE' }]) {
        expect((await merge(api, { 'validation-success': 'true' }, env)).merged).toBe(false);
    }
});

test('strict/main/source/ownerが欠落・変更ならmerge禁止', async () => {
    for (const mutate of [
        f => { f.effective = []; }, f => { f.repo.default_branch = 'other'; }, f => { f.repo.owner.id++; },
        f => { f.deniedRules = true; },
        f => { f.effective[0].parameters.strict_required_status_checks_policy = false; },
        f => { f.effective[0].parameters.required_status_checks[0].integration_id++; },
        f => { f.effective[0].parameters.required_status_checks[0].context = 'unknown'; },
        f => { f.effective[0].parameters.required_status_checks.push({ context: STATUS_CONTEXT, integration_id: 7 }); },
        f => { f.effective[0].ruleset_source = 'attacker/repo'; },
        f => { f.full.enforcement = 'evaluate'; }, f => { f.full.conditions.ref_name.include = ['~ALL']; },
        f => { f.full.rules[0].parameters.strict_required_status_checks_policy = false; },
        f => { f.full.conditions.ref_name.exclude = ['refs/heads/main']; },
    ]) {
        const f = fixture(); mutate(f);
        await expect(requiredRule(f.api, f.repo, on)).rejects.toThrow();
        expect((await merge(f.api, f.options(), on)).merged).toBe(false);
        expect(f.writes).toHaveLength(0); expect(f.puts).toBe(0);
    }
    for (const key of ['DEPENDENCY_GUARD_APP_ID', 'DEPENDENCY_GUARD_RULESET_ID', 'DEPENDENCY_GUARD_RULESET_UPDATED_AT']) {
        const f = fixture(), env = { ...on }; delete env[key];
        await expect(requiredRule(f.api, f.repo, env)).rejects.toThrow();
        expect((await sign(f.api, f.options(), env, f.api)).ready).toBe(false); expect(f.writes).toHaveLength(0);
    }
});

test('bypassは設定監査で担保し、必須ルール確認後に指定SHAで同期squashを要求する', async () => inRun(async () => {
    const f = fixture(); delete f.full.bypass_actors;
    expect(await requiredRule(f.api, f.repo, on)).toBe(1);
    await signed(f); f.result = 'merged';
    expect((await merge(f.api, f.options(), on)).merged).toBe(true);
    expect(f.mergeBody).toEqual({ sha: f.head, merge_method: 'squash' });
}));

test('HEADの共通contextのstatusやcheck、HEADの親偽装は検証しない', async () => inRun(async () => {
    for (const state of ['success', 'pending', 'failure', 'error']) {
      const f = fixture(); f.history.push(mockStatus(f.head, 301, STATUS_CONTEXT, state));
      expect((await sign(f.api, f.options(), on, f.api)).ready).toBe(false); expect(f.puts).toBe(0);
    }
    const check = fixture(); check.checks = [{ name: STATUS_CONTEXT, status: 'completed', conclusion: 'success' }];
    expect((await attest(check.api, check.options())).ready).toBe(false);
    const parents = fixture(); parents.originalBase = hash('9');
    expect((await attest(parents.api, parents.options())).ready).toBe(false);
    const mergeParents = fixture(); mergeParents.badMergeParents = true;
    expect((await attest(mergeParents.api, mergeParents.options())).ready).toBe(false);
    expect(mergeParents.writes.some(write => write.path.startsWith('/statuses/'))).toBe(false);
}));

test('HEAD checkの件数不一致・suite範囲不明や1000件上限ではattestしない', async () => inRun(async () => {
    for (const mode of ['checks-incomplete', 'suites-incomplete', 'suites-limit']) {
      const f = fixture(), request = f.api.request.bind(f.api);
      f.api.request = async path => {
        if (path.includes('/check-runs?filter=all') && mode === 'checks-incomplete') return { total_count: 1, check_runs: [] };
        if (path.includes('/check-suites?per_page=1')) return { total_count: mode === 'suites-limit' ? 1000 : mode === 'suites-incomplete' ? 1 : 0, check_suites: [] };
        return request(path);
      };
      expect((await attest(f.api, f.options())).ready).toBe(false);
      expect(f.writes.some(write => write.path.startsWith('/statuses/'))).toBe(false);
    }
}));

test('check一覧が総数0でnextを返す矛盾は受け付けない', async () => inRun(async () => {
    for (const kind of ['check-suites', 'check-runs']) {
      const f = fixture(), response = f.api.response.bind(f.api);
      f.api.response = async (path, ...args) => {
        if (!path.includes(`/${kind}?`)) return response(path, ...args);
        const url = new URL(`https://api.github.com/repos/owner/repo${path}`); url.searchParams.set('page', '2');
        return { status: 200, data: { total_count: 0, [kind.replace('-', '_')]: [] }, headers: { link: `<${url}>; rel="next"`, remaining: '1000' } };
      };
      expect((await attest(f.api, f.options())).ready).toBe(false);
    }
}));

test('HEAD check複数ページは総数の変化・重複を拒否し完全な一覧だけ受け付ける', async () => inRun(async () => {
    for (const mode of ['ok', 'count-changed', 'duplicate']) {
      const f = fixture(), request = f.api.request.bind(f.api);
      f.api.request = async (path, ...args) => {
        if (!path.includes('/check-runs?filter=all')) return request(path, ...args);
        const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
        const checks = Array.from({ length: 101 }, (_, i) => ({ id: i + 1, name: `check ${i}` }));
        if (mode === 'duplicate') checks[100].id = 1;
        return { total_count: mode === 'count-changed' && page === 2 ? 102 : 101, check_runs: checks.slice((page - 1) * 100, page * 100) };
      };
      expect((await attest(f.api, f.options())).ready).toBe(mode === 'ok');
    }
}));

test('他CIの件数不一致・重複・途中変化を拒否し正常な複数ページを読む', async () => inRun(async () => {
    for (const mode of ['ok', 'suite-incomplete', 'run-incomplete', 'duplicate', 'changed']) {
      const f = fixture(), request = f.api.request.bind(f.api), calls = new Map();
      f.api.request = async path => {
        if (!path.includes('/check-suites?per_page=100') && !path.includes('/check-runs?filter=latest')) return request(path);
        const key = path.includes('check-suites') ? 'check_suites' : 'check_runs';
        const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
        const token = key + page, reads = (calls.get(token) ?? 0) + 1; calls.set(token, reads);
        let items = Array.from({ length: 101 }, (_, i) => ({ id: i + 1, status: 'completed', conclusion: 'success', app: { slug: 'other' } }));
        if (mode === 'duplicate') items[100].id = 1;
        if (mode === 'changed' && page === 1 && reads > 1) items[0].conclusion = 'failure';
        if (mode === 'suite-incomplete' && key === 'check_suites' || mode === 'run-incomplete' && key === 'check_runs') return { total_count: 1, [key]: [] };
        return { total_count: 101, [key]: items.slice((page - 1) * 100, page * 100) };
      };
      const snapshot = { base: f.base, head: f.head, merge: f.tested, pr: '7', defaultBranch: 'main' };
      if (mode === 'ok') expect(await otherChecks(f.api, [f.head], snapshot)).toBeNull();
      else await expect(otherChecks(f.api, [f.head], snapshot)).rejects.toThrow();
    }
}));

test('attestは読取専用、signは設定・全検証後だけApp statusをHEADへ投稿する', async () => inRun(async () => {
    const f = fixture();
    expect((await attest(f.api, f.options())).ready).toBe(true); expect(f.writes).toHaveLength(0);
    expect((await sign(f.api, f.options(), {}, f.api)).ready).toBe(false); expect(f.writes).toHaveLength(0);
    const noSecret = { ...on }; delete noSecret.DEPENDENCY_GUARD_APP_TOKEN;
    expect((await sign(f.api, f.options(), noSecret, f.api)).ready).toBe(false); expect(f.writes).toHaveLength(0);
    await signed(f);
    expect(f.writes.filter(w => w.path.startsWith('/statuses/')).map(w => w.path)).toEqual([`/statuses/${f.head}`]);
    expect(f.history[0].creator.login).toBe('guard[bot]'); expect(f.puts).toBe(0);
    for (const mutate of [
      x => { x.pr.state = 'closed'; }, x => { x.pr.draft = true; }, x => { x.pr.auto_merge = {}; },
      x => { x.pr.user.id++; }, x => { x.pr.head.repo.id++; }, x => { x.pr.base.sha = hash('8'); },
      x => { x.mergeRef = hash('8'); }, x => { x.jobs[0].conclusion = 'failure'; }, x => { x.jobs[0].run_attempt--; },
      x => { x.diff = [{ filename: '.github/workflows/ci.yml' }]; },
    ]) {
      const invalid = fixture(); mutate(invalid);
      expect((await sign(invalid.api, invalid.options(), on, invalid.api)).ready).toBe(false);
      expect(invalid.writes).toHaveLength(0);
    }
}));

test('本人のauto-merge解除はbot操作では上書きせず、本人enable後のみ再評価する', async () => inRun(async () => {
    const f = fixture(), event = (id, type, who, time) => ({ id, __typename: type, createdAt: time, actor: { __typename: who, id: `ID-${who}`, login: who === 'User' ? 'owner' : 'robot' } });
    f.timeline = [event('1', 'AutoMergeDisabledEvent', 'User', '2026-11-05T00:00:01Z'), event('2', 'AutoMergeEnabledEvent', 'Bot', '2026-11-05T00:00:02Z')];
    expect((await sign(f.api, f.options(), on, f.api)).ready).toBe(false); expect(f.writes).toHaveLength(0);
    f.timeline.push(event('3', 'AutoMergeEnabledEvent', 'User', '2026-11-05T00:00:03Z'));
    await signed(f);
    f.timeline.push(event('4', 'AutoMergeDisabledEvent', 'User', '2026-11-05T00:00:04Z'));
    expect((await merge(f.api, f.options(), on)).merged).toBe(false); expect(f.puts).toBe(0);
}));

test('App statusの応答断はnonceによる一意readbackだけで確定し、孤立statusは再投稿しない', async () => inRun(async () => {
    const recovered = fixture(); recovered.intentThrows = true; recovered.intentPersists = true;
    expect((await sign(recovered.api, recovered.options(), on, recovered.api)).ready).toBe(true);
    const missing = fixture(); missing.intentThrows = true;
    expect((await sign(missing.api, missing.options(), on, missing.api)).ready).toBe(false);
    const orphan = fixture(); await signed(orphan); const count = orphan.writes.length;
    expect((await sign(orphan.api, orphan.options(), on, orphan.api)).ready).toBe(false);
    expect(orphan.writes.length).toBe(count); expect(orphan.puts).toBe(0);
}));

test('同期PUTはsquashで一度だけ、200後に親・tree・mainを検証して確定する', async () => inRun(async () => {
    const f = fixture(); await signed(f);
    const result = await merge(f.api, f.options(), on);
    expect(result.merged).toBe(true); expect(f.puts).toBe(1);
    expect(f.mergeBody).toEqual({ sha: f.head, merge_method: 'squash' });
    expect((await readLedger(f.api, f.repo)).lastCompletion.actual).toBe(f.actual);
    const wrong = fixture(); await signed(wrong); wrong.badTree = true;
    expect((await merge(wrong.api, wrong.options(), on)).merged).toBe(false);
    expect((await readLedger(wrong.api, wrong.repo)).state).toBe('intent');
}));

test('PUT前preflight変化はaborted、応答不明はintentで再送しない', async () => inRun(async () => {
    for (const mutate of [
      f => { f.pr.state = 'closed'; }, f => { f.pr.draft = true; }, f => { f.pr.auto_merge = {}; },
      f => { f.branch = hash('8'); }, f => { f.pr.head.sha = hash('8'); }, f => { f.effective = []; },
      f => { f.checks = [{ name: 'unknown', status: 'queued' }]; },
    ]) {
      const f = fixture(); await signed(f); f.afterIntent = () => mutate(f);
      expect((await merge(f.api, f.options(), on)).merge_state).toBe('aborted-before-request');
      expect(f.puts).toBe(0); expect((await readLedger(f.api, f.repo)).state).toBe('aborted-before-request');
    }
    for (const mode of ['timeout', 'server', 'bad200']) {
      const f = fixture(); await signed(f);
      if (mode === 'timeout') f.timeout = true; else if (mode === 'server') f.http = 503; else f.result = 'invalid';
      expect((await merge(f.api, f.options(), on)).merge_state).toBe('unknown');
      expect((await readLedger(f.api, f.repo)).state).toBe('intent');
      expect((await reconcile(f.api)).blocked).toBe(true);
      expect((await merge(f.api, f.options(), on)).merged).toBe(false); expect(f.puts).toBe(1);
    }
}));

test('405/409/422はfailedとして記録し同じCI attemptでは再送しない', async () => inRun(async () => {
    for (const status of [405, 409, 422]) {
      const f = fixture(); await signed(f); f.http = status;
      expect((await merge(f.api, f.options(), on)).merge_state).toBe('failed');
      expect((await readLedger(f.api, f.repo)).record.http_status).toBe(status);
      expect((await reconcile(f.api)).blocked).toBe(false);
      expect((await sign(f.api, f.options(), on, f.api)).ready).toBe(false);
      expect(f.puts).toBe(1);
    }
}));

test('候補実行はwriteなし、署名は別runner、trusted mergeだけintent権限とnative APIを使う', async () => {
    const yaml = name => Bun.YAML.parse(readFileSync(new URL(`../../../.github/workflows/${name}`, import.meta.url), 'utf8'));
    const workflow = yaml('dependency-security.yml'), validation = yaml('dependency-validation.yml');
    expect(validation.permissions.contents).toBe('read');
    for (const job of Object.values(validation.jobs)) expect(Object.values(job.permissions ?? {}).includes('write')).toBe(false);
    expect(workflow.concurrency['cancel-in-progress']).toBe(false);
    expect(workflow.jobs.validate.if).toContain("has_changes == 'true'");
    expect(workflow.jobs.attest.if).toContain("has_changes == 'true'");
    expect(workflow.jobs.attest.permissions.contents).toBe('read'); expect(workflow.jobs.attest.permissions.statuses).toBe('read');
    expect(workflow.jobs.merge.permissions.statuses).toBe('read');
    expect(workflow.jobs.prepare.permissions.actions).toBe('read'); expect(workflow.jobs.prepare.permissions.checks).toBe('read');
    const recovery = workflow.jobs.prepare.steps.find(step => step.id === 'reconcile');
    for (const key of ['DEPENDENCY_GUARD_APP_ID', 'DEPENDENCY_GUARD_APP_SLUG', 'DEPENDENCY_GUARD_APP_BOT_ID', 'DEPENDENCY_GUARD_RULESET_ID', 'DEPENDENCY_GUARD_RULESET_UPDATED_AT']) expect(recovery.env[key]).toContain(`vars.${key}`);
    expect(recovery.env.DEPENDENCY_GUARD_APP_TOKEN).toBeUndefined();
    const token = workflow.jobs.merge.steps.find(step => step.id === 'guard-token');
    expect(token.uses).toBe('actions/create-github-app-token@64ab729a6294000f5aa4a4bc0b73d1f32bc45510'); expect(token.with['permission-statuses']).toBe('write');
    expect(workflow.jobs.merge.if).toContain("needs.attest.outputs.ready == 'true'");
    expect(workflow.jobs.merge.if).toContain("vars.BUN_SECURITY_NATIVE_MERGE_ACTIVATED == 'true'");
    expect(workflow.jobs['report-held'].if).toContain("vars.BUN_SECURITY_NATIVE_MERGE_ACTIVATED != 'true'");
    expect(workflow.jobs['report-success']).toBeUndefined();
    for (const job of [workflow.jobs.attest, workflow.jobs.merge]) {
        const checkout = job.steps.find(step => step.uses?.startsWith('actions/checkout@'));
        expect(checkout.with.ref).toBe('${{ github.sha }}'); expect(checkout.with['persist-credentials']).toBe(false);
        expect(job.steps.some(step => step.run?.includes('bun install'))).toBe(false);
    }
    const source = readFileSync(new URL('./Security-Pull-Request.mjs', import.meta.url), 'utf8');
    expect(source.includes('pushMerge')).toBe(false); expect(source.includes('check-push')).toBe(false);
});

test('workflow通知条件は失敗・中断・保留・候補なし・成功を区別する', () => {
    const workflow = Bun.YAML.parse(readFileSync(new URL('../../../.github/workflows/dependency-security.yml', import.meta.url), 'utf8'));
    const evaluate = expression => new Function('needs', 'vars', `return (${expression.replaceAll('always()', 'true')});`);
    const failure = evaluate(workflow.jobs['report-failure'].if), held = evaluate(workflow.jobs['report-held'].if);
    const base = { prepare: { result: 'success', outputs: { has_changes: 'true' } }, validate: { result: 'success' }, attest: { result: 'success', outputs: { ready: 'true' } }, merge: { result: 'success', outputs: { merged: 'true' } } };
    const vars = { BUN_SECURITY_NATIVE_MERGE_ACTIVATED: 'true' };
    const cases = [
      [{}, false, false],
      [{ prepare: { result: 'success', outputs: { has_changes: 'false' } }, validate: { result: 'skipped' }, attest: { result: 'skipped', outputs: {} }, merge: { result: 'skipped', outputs: {} } }, false, false],
      [{ prepare: { result: 'success', outputs: {} }, validate: { result: 'skipped' }, attest: { result: 'skipped', outputs: {} }, merge: { result: 'skipped', outputs: {} } }, false, false],
      [{ prepare: { result: 'cancelled', outputs: {} } }, false, true],
      [{ validate: { result: 'failure' } }, true, false],
      [{ validate: { result: 'cancelled' }, attest: { result: 'skipped', outputs: {} }, merge: { result: 'skipped', outputs: {} } }, false, true],
      [{ attest: { result: 'failure', outputs: {} } }, true, false],
      [{ attest: { result: 'cancelled', outputs: {} }, merge: { result: 'skipped', outputs: {} } }, false, true],
      [{ merge: { result: 'failure', outputs: {} } }, true, false],
      [{ merge: { result: 'cancelled', outputs: {} } }, false, true],
      [{ merge: { result: 'success', outputs: { merged: 'false' } } }, false, true],
      [{ attest: { result: 'success', outputs: { ready: 'false' } }, merge: { result: 'skipped', outputs: {} } }, false, true],
    ];
    for (const [overrides, expectedFailure, expectedHeld] of cases) {
      const needs = { ...base, ...overrides };
      expect(failure(needs, vars)).toBe(expectedFailure); expect(held(needs, vars)).toBe(expectedHeld);
    }
    expect(held({ ...base, merge: { result: 'skipped', outputs: {} } }, { BUN_SECURITY_NATIVE_MERGE_ACTIVATED: 'false' })).toBe(true);
    const steps = workflow.jobs.prepare.steps;
    const auditIf = steps.find(step => step.id === 'audit').if, prepareIf = steps.find(step => step.id === 'pr').if;
    const stepsExpr = expr => new Function('steps', `return (${expr});`);
    expect(stepsExpr(auditIf)({ reconcile: { outputs: { main_updated: 'true' } } })).toBe(false);
    expect(stepsExpr(prepareIf)({ reconcile: { outputs: { main_updated: 'true', blocked: 'false' } } })).toBe(false);
    expect(stepsExpr(auditIf)({ reconcile: { outputs: { main_updated: '' } } })).toBe(true);
    expect(stepsExpr(prepareIf)({ reconcile: { outputs: { main_updated: '', blocked: 'false' } } })).toBe(true);
    expect(steps.findIndex(step => step.id === 'pr')).toBeLessThan(steps.findIndex(step => step.name === 'Publish audit summary and unresolved notification'));
});

test('native transportは2026 API・timeout・redirect拒否を指定しHTTP状態を保持する', async () => {
    const original = globalThis.fetch;
    globalThis.fetch = async (url, options) => {
        expect(options.headers['X-GitHub-Api-Version']).toBe('2026-03-10'); expect(options.redirect).toBe('error'); expect(options.signal).toBeDefined();
        return { status: 202, headers: new Headers(), json: async () => ({ status: 'pending' }) };
    };
    try { expect((await client('owner/repo', 'fixture').response('/pulls/7/merge', 'PUT', {})).status).toBe(202); }
    finally { globalThis.fetch = original; }
});

test('combined の creator 不在と個別履歴を SHA・最新 ID・発行元で照合し曖昧さは拒否する', async () => {
    const revision = hash('8');
    const history = Array.from({ length: 101 }, (_, i) => mockStatus(revision, 1001 - i, 'check'));
    let combined = withoutCreator(history[0]), combinedSha = revision, omit = false, duplicate = false, flip = false, reads = 0;
    const api = { repository: 'owner/repo', async request(path) {
        if (path.includes('/status?')) {
            reads++;
            return { sha: combinedSha, total_count: 1, statuses: [{ ...combined, id: flip && reads > 1 ? combined.id + 1 : combined.id }] };
        }
        const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
        if (omit) return [];
        const result = history.slice((page - 1) * 100, page * 100);
        if (duplicate && page === 2) return [history[0]];
        return result;
    }, async response(path) { return { status: 200, data: await this.request(path), headers: { link: null, remaining: '1000' } }; } };
    expect((await latestStatuses(api, revision))[0].creator.login).toBe('github-actions[bot]');
    for (const mutate of [
        () => { combinedSha = hash('9'); },
        () => { combined = { ...combined, id: combined.id + 2 }; },
        () => { combined = { ...combined, state: 'failure' }; },
        () => { combined = { ...combined, target_url: 'https://other.invalid/' }; },
        () => { omit = true; },
        () => { duplicate = true; },
        () => { flip = true; reads = 0; },
    ]) {
        combined = withoutCreator(history[0]); combinedSha = revision; omit = false; duplicate = false; flip = false; reads = 0;
        mutate(); await expect(latestStatuses(api, revision)).rejects.toThrow();
    }
});

test('status履歴はLink・件数・重複・正規の空確認ページを検証する', async () => {
    const head = hash('8'), entries = Array.from({ length: 100 }, (_, i) => mockStatus(head, 500 - i, 'check'));
    const url = page => `https://api.github.com/repositories/12/commits/${head}/statuses?per_page=100&page=${page}`;
    for (const mode of ['ok', 'short-next', 'wrong-host', 'wrong-last', 'missing-next', 'bad-prev-first-page', 'bad-prev', 'bad-first', 'duplicate', 'empty-probe', 'overflow']) {
      const api = { repository: 'owner/repo', async request() { return { id: 12, full_name: 'owner/repo' }; }, async response(path) {
        const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
        let data = page === 1 ? entries : page === 2 ? [mockStatus(head, 99, 'check')] : [];
        let link = page === 1 ? `<${url(2)}>; rel="next", <${url(2)}>; rel="last"` : null;
        if (mode === 'short-next' && page === 1) data = [];
        if (mode === 'wrong-host' && page === 1) link = link.replaceAll('api.github.com', 'attacker.invalid');
        if (mode === 'wrong-last' && page === 1) link = `<${url(2)}>; rel="next", <${url(1)}>; rel="last"`;
        if (mode === 'missing-next') link = page === 1 ? `<${url(2)}>; rel="next", <${url(3)}>; rel="last"` : null;
        if (mode === 'bad-prev-first-page' && page === 1) { data = []; link = `<${url(2)}>; rel="prev"`; }
        if (mode === 'bad-prev' && page === 2) link = `<${url(2)}>; rel="prev"`;
        if (mode === 'bad-first' && page === 1) { data = []; link = `<${url(2)}>; rel="first"`; }
        if (mode === 'duplicate' && page === 2) data = [entries[0]];
        if (mode === 'empty-probe') { data = page === 1 ? entries : []; link = page === 2 ? `<${url(1)}>; rel="prev", <${url(1)}>; rel="last", <${url(1)}>; rel="first"` : null; }
        if (mode === 'overflow') { data = entries.map(item => ({ ...item, id: item.id + page * 1000 })); link = null; }
        return { status: 200, data, headers: { link, remaining: '1000' } };
      } };
      if (mode === 'ok') expect((await statusHistory(api, head)).length).toBe(101);
      else if (mode === 'empty-probe') expect((await statusHistory(api, head)).length).toBe(100);
      else await expect(statusHistory(api, head)).rejects.toThrow();
    }
});

test('他CIの失敗statusと偽装guard statusは拒否する', async () => inRun(async () => {
  const f = fixture(); await signed(f);
  const snapshot = { base: f.base, head: f.head, merge: f.tested, pr: '7', defaultBranch: 'main' };
  f.history.unshift(mockStatus(f.head, 500, 'external', 'failure'));
  expect(await otherChecks(f.api, [f.head], snapshot)).toContain('unsuccessful');
  const fake = fixture(); await signed(fake); fake.history[0].creator.id++;
  expect((await merge(fake.api, fake.options(), on)).merged).toBe(false); expect(fake.puts).toBe(0);
  const orphan = fixture(); orphan.history.push(mockStatus(orphan.head, 301, STATUS_CONTEXT.toUpperCase()));
  expect((await sign(orphan.api, orphan.options(), on, orphan.api)).ready).toBe(false);
  const rule = fixture(); rule.effective[0].parameters.required_status_checks.push({ context: STATUS_CONTEXT.toUpperCase(), integration_id: APP_ID });
  await expect(requiredRule(rule.api, rule.repo, on)).rejects.toThrow();
}));

test('一般 context も大小文字を同一視し、古い成功と重複 context を拒否する', async () => {
    const revision = hash('8'), newer = mockStatus(revision, 12, 'BUILD', 'failure'), older = mockStatus(revision, 11, 'build');
    let combined = [withoutCreator(newer)];
    const api = { repository: 'owner/repo', async request(path) {
        return path.includes('/status?') ? { sha: revision, statuses: combined, total_count: combined.length } : [newer, older];
    }, async response(path) { return { status: 200, data: await this.request(path), headers: { link: null, remaining: '1000' } }; } };
    expect((await latestStatuses(api, revision))[0].state).toBe('failure');
    combined = [withoutCreator(older)];
    await expect(latestStatuses(api, revision)).rejects.toThrow();
    combined = [withoutCreator(newer), withoutCreator(older)];
    await expect(latestStatuses(api, revision)).rejects.toThrow();
});

test('応答不明後にPR mergedとcommit検証が揃った場合だけreconcileで完了する', async () => inRun(async () => {
  const f = fixture(); await signed(f); f.timeout = true;
  expect((await merge(f.api, f.options(), on)).merge_state).toBe('unknown');
  expect((await reconcile(f.api)).blocked).toBe(true);
  f.pr.state = 'closed'; f.pr.merged = true; f.pr.merge_commit_sha = f.actual; f.branch = f.actual;
  const result = await reconcile(f.api);
  expect(result.merged).toBe(true); expect(result.main_updated).toBe(true);
  expect((await readLedger(f.api, f.repo)).lastCompletion.actual).toBe(f.actual); expect(f.puts).toBe(1);
}));

test('PR 番号・tested commit・actual commit の SHA 欠落や不一致は拒否する', async () => inRun(async () => {
    const wrongPr = fixture(); wrongPr.pr.number = 8;
    expect((await attest(wrongPr.api, wrongPr.options())).ready).toBe(false);
    const tested = fixture(); tested.missingTestedSha = true;
    expect((await attest(tested.api, tested.options())).ready).toBe(false);
    const actual = fixture(); await signed(actual); actual.result = 'merged'; actual.missingActualSha = true;
    expect((await merge(actual.api, actual.options(), on)).merged).toBe(false);
}));

test('同期merge後の次回clean監査で通知を閉じる', async () => inRun(async () => {
  const root = process.env.BUN_SECURITY_TEST_ROOT, f = fixture(); await signed(f);
  expect((await merge(f.api, f.options(), on)).merged).toBe(true);
  f.issue = { number: 9, state: 'open', user: { id: 41898282, login: 'github-actions[bot]' }, body: '<!-- bun-security-automation -->\noriginal' };
  const audit = join(root, `fixed-ledger-audit-${Date.now()}.json`);
  writeFileSync(audit, JSON.stringify({ status: 'clean', rawAfter: {}, mitigations: [], remaining: [], needs_followup: false, upstreamFixes: [], result_changed: false, changedFiles: [] }));
  const before = process.env.GITHUB_SHA; process.env.GITHUB_SHA = f.actual;
  try { expect((await report(f.api, { report: audit, 'allow-close': 'true' })).issue_reporting).toBe('clear'); expect(f.issue.state).toBe('closed'); }
  finally { if (before === undefined) delete process.env.GITHUB_SHA; else process.env.GITHUB_SHA = before; }
}));

test('native 要求前に proposal の HEAD blob が不一致なら PUT を送らない', async () => inRun(async () => {
    const f = fixture(); await signed(f); const before = f.writes.length; f.badProposalBlob = true;
    expect((await merge(f.api, f.options(), on)).merged).toBe(false);
    expect(f.puts).toBe(0);
    expect(f.writes).toHaveLength(before);
}));
