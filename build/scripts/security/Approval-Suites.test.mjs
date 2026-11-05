import { test, expect } from 'bun:test';
import { trustedValidationEvidence, duplicateValidationSuite, duplicateValidationCheck, otherChecks, BRANCH } from './Security-Pull-Request.mjs';

const repository = 'owner/repo';
const snapshot = { base: '1'.repeat(40), head: '2'.repeat(40), merge: '3'.repeat(40), pr: '7', defaultBranch: 'main' };
const coverage = {
    'dependency-validation.yml': ['Validate dependency changes', 'Validate security merge guard', 'Require validation success'],
    'ci.yml': ['Build & Test'],
    'format-check.yml': ['format-check'],
    'localization-check.yml': ['localization-check'],
};

test('重複扱いのworkflowの検証コマンド・action・check名が固定merge検証で維持される', async () => {
    const load = async name => Bun.YAML.parse(await Bun.file(new URL(`../../../.github/workflows/${name}`, import.meta.url)).text());
    const reusable = await load('dependency-validation.yml');
    const caller = await load('dependency-security.yml');
    const validation = reusable.jobs.validate;
    const steps = validation.steps;
    const commands = steps.map(step => step.run ?? '').join('\n').split('\n').map(line => line.trim());
    expect(caller.jobs.validate.name).toBe('Validate fixed merge');
    expect(caller.jobs.validate.with.merge_sha).toBe('${{ needs.prepare.outputs.merge_sha }}');
    expect(caller.jobs.merge.needs).toContain('validate');
    expect(caller.jobs.merge.if).toContain("needs.validate.result == 'success'");
    const checkout = steps.find(step => step.uses?.startsWith('actions/checkout@'));
    expect(checkout.with.ref).toContain('refs/pull/{0}/merge');
    expect(checkout.with.submodules).toBe(true);
    expect(commands).toContain('test "$(git rev-parse HEAD)" = "$MERGE_SHA"');
    expect(commands).toContain('test "$(git rev-parse HEAD^1)" = "$BASE_SHA"');
    expect(commands).toContain('test "$(git rev-parse HEAD^2)" = "$HEAD_SHA"');
    expect(commands).toContain('test -z "$(git status --porcelain)"');
    for (const [name, checkNames] of Object.entries(coverage)) {
        const workflow = await load(name);
        expect(Object.entries(workflow.jobs).map(([id, job]) => job.name ?? id).sort()).toEqual([...checkNames].sort());
        if (name === 'dependency-validation.yml') continue;
        for (const job of Object.values(workflow.jobs)) {
            for (const step of job.steps) {
                // cacheは検証内容を増やさない。PRの差分判定は上のporcelain確認と同等。
                if (step.uses?.startsWith('actions/cache@') || step.name === 'Verify no changes (PR only)') continue;
                if (step.uses) expect(steps.some(other => other.uses === step.uses)).toBe(true);
                if (step.run) for (const line of step.run.trim().split('\n')) expect(commands).toContain(line.trim());
                if (step['working-directory']) expect(steps.some(other => other.run === step.run && other['working-directory'] === step['working-directory'])).toBe(true);
            }
        }
    }
});
function fixture(workflowName = 'ci.yml') {
    const current = { id: 90, run_attempt: 2, workflow_id: 91, check_suite_id: 92,
        path: '.github/workflows/dependency-security.yml', event: 'schedule', head_branch: 'main', head_sha: snapshot.base,
        repository: { full_name: repository }, head_repository: { full_name: repository } };
    const jobs = ['Validate dependency changes', 'Validate security merge guard', 'Require validation success'].map((name, i) => ({ id: i + 1,
        name: `Validate fixed merge / ${name}`, status: 'completed', conclusion: 'success',
        run_id: current.id, run_attempt: current.run_attempt, head_sha: snapshot.base,
    }));
    const suite = { id: 100, head_sha: snapshot.head, app: { slug: 'github-actions' }, status: 'completed', conclusion: 'action_required' };
    const run = { id: 10, check_suite_id: suite.id, workflow_id: 20, event: 'pull_request', head_branch: BRANCH + '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42',
        head_sha: snapshot.head, repository: { full_name: repository }, head_repository: { full_name: repository },
        pull_requests: [{ number: 7, base: { sha: snapshot.base }, head: { sha: snapshot.head } }],
        path: `.github/workflows/${workflowName}`, status: 'completed', conclusion: 'action_required' };
    const check = { id: 200, name: coverage[workflowName]?.[0], check_suite: { id: suite.id }, app: { slug: 'github-actions' },
        status: 'completed', conclusion: 'action_required', head_sha: snapshot.head,
        details_url: `https://github.com/${repository}/actions/runs/${run.id}/job/15` };
    const f = { current, jobs, suite, run, check, total: 1, workflow: { id: 20, path: run.path },
        checks: [check], statuses: [], suites: [suite] };
    f.api = { repository, async request(path) {
        if (path === '/actions/runs/90') return current;
        if (path === '/actions/workflows/dependency-security.yml') return { id: 91, path: current.path };
        if (path.startsWith('/actions/runs/90/jobs?')) return { total_count: f.jobs.length, jobs: f.jobs };
        if (path.startsWith('/actions/runs?')) return { total_count: f.total, workflow_runs: [run] };
        if (path === `/actions/workflows/${workflowName}`) return f.workflow;
        if (path.includes('/check-suites?')) { const check_suites = path.includes(snapshot.head) ? f.suites : []; return { total_count: check_suites.length, check_suites }; }
        if (path.includes('/check-runs?')) { const check_runs = path.includes(snapshot.head) ? f.checks : []; return { total_count: check_runs.length, check_runs }; }
        if (path.includes('/status?') || path.includes('/statuses?')) {
            const revision = path.split('/')[2];
            const entries = f.statuses.map((entry, index) => ({ id: index + 1, context: 'external', state: entry.state,
                url: `https://api.github.com/repos/${repository}/statuses/${revision}`, target_url: null, description: null, creator: null }));
            if (path.includes('/statuses?')) return entries;
            return { sha: revision, statuses: entries.map(({ creator, ...status }) => status), total_count: entries.length };
        }
        throw new Error(`Unexpected offline request: ${path}`);
    }, async response(path) {
        const data = await this.request(path), url = new URL(`https://api.github.com/repos/${repository}${path}`);
        const page = Number(url.searchParams.get('page')), per = Number(url.searchParams.get('per_page')), last = Math.max(1, Math.ceil((data.total_count ?? 0) / per));
        let link = null; if (page < last) { url.searchParams.set('page', String(page + 1)); link = `<${url}>; rel="next"`; url.searchParams.set('page', String(last)); link += `, <${url}>; rel="last"`; }
        return { status: 200, data, headers: { link, remaining: '1000' } };
    } };
    return f;
}
async function proof(f) { return trustedValidationEvidence(f.api, f.current, snapshot); }

test('固定merge検証の同じattempt・3ジョブ成功と信頼済みworkflowを要求する', async () => {
    expect((await proof(fixture())).merge).toBe(snapshot.merge);
    for (const mutate of [
        f => { f.current.head_sha = snapshot.head; },
        f => { f.current.head_branch = 'untrusted'; },
        f => { f.current.repository.full_name = 'attacker/repo'; },
        f => { f.current.head_repository.full_name = 'attacker/repo'; },
        f => { f.current.path = '.github/workflows/unknown.yml'; },
        f => { f.current.event = 'pull_request'; },
        f => { f.current.workflow_id++; },
        f => { f.jobs[0].conclusion = 'failure'; },
        f => { f.jobs[0].conclusion = 'skipped'; },
        f => { f.jobs[0].status = 'in_progress'; },
        f => { f.jobs[0].run_id++; },
        f => { f.jobs[0].head_sha = snapshot.merge; },
        f => { f.jobs[0].run_attempt--; },
        f => { f.jobs.pop(); },
        f => { f.jobs.push(f.jobs[0]); },
    ]) {
        const f = fixture(); mutate(f);
        expect(await proof(f)).toBeNull();
    }
});

test('job一覧の件数欠落・重複・ページ途中変化や追加同名jobは信頼しない', async () => {
    for (const mode of ['empty', 'duplicate', 'changed', 'same-name']) {
        const f = fixture(), request = f.api.request.bind(f.api); let reads = 0;
        f.api.request = async path => {
            if (!path.startsWith('/actions/runs/90/jobs?')) return request(path);
            const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
            if (mode === 'empty') return { total_count: 1, jobs: [] };
            const extras = Array.from({ length: 98 }, (_, i) => ({ id: 1000 + i, name: `other ${i}` }));
            if (mode === 'same-name') extras[97].name = f.jobs[0].name;
            if (mode === 'duplicate') extras[97].id = f.jobs[0].id;
            if (mode === 'changed' && page === 1 && ++reads > 1) extras[0].name = 'changed';
            const jobs = [...f.jobs, ...extras]; return { total_count: jobs.length, jobs: jobs.slice((page - 1) * 100, page * 100) };
        };
        expect(await proof(f)).toBeNull();
    }
});

test('job総数0とnext Linkの矛盾は信頼しない', async () => {
    const f = fixture(), response = f.api.response.bind(f.api);
    f.api.response = async path => path.startsWith('/actions/runs/90/jobs?') ?
      { status: 200, data: { total_count: 0, jobs: [] }, headers: { remaining: '1000', link: '<https://api.github.com/repos/owner/repo/actions/runs/90/jobs?filter=latest&per_page=100&page=2>; rel="next"' } } : response(path);
    expect(await proof(f)).toBeNull();
});

test('同等検証が済んだ4workflowだけ、同じPR/base/headの承認待ちsuiteとcheckを識別する', async () => {
    for (const workflow of Object.keys(coverage)) {
        const f = fixture(workflow);
        const evidence = await duplicateValidationSuite(f.api, f.suite, snapshot.head, await proof(f));
        expect(evidence.duplicate).toBe(true);
        for (const name of coverage[workflow]) {
            expect(duplicateValidationCheck({ ...f.check, name }, evidence, snapshot.head)).toBe(true);
        }
        expect(duplicateValidationCheck(f.check, evidence, snapshot.merge)).toBe(false);
    }
});

test('悪意ある別workflow・fork・別event・別PR・固定snapshot不一致・曖昧suiteを拒否する', async () => {
    for (const mutate of [
        f => { f.run.path = '.github/workflows/localization-update.yml'; },
        f => { f.run.path = '.github/workflows/release.yml'; },
        f => { f.run.workflow_id++; },
        f => { f.workflow.path = '.github/workflows/unknown.yml'; },
        f => { f.run.event = 'pull_request_target'; },
        f => { f.run.head_branch = 'other'; },
        f => { f.run.head_sha = snapshot.merge; },
        f => { f.suite.head_sha = snapshot.merge; },
        f => { f.run.head_repository.full_name = 'attacker/repo'; },
        f => { f.run.repository.full_name = 'attacker/repo'; },
        f => { f.run.check_suite_id++; },
        f => { f.run.pull_requests[0].base.sha = snapshot.merge; },
        f => { f.run.pull_requests[0].head.sha = snapshot.merge; },
        f => { f.run.pull_requests[0].number++; },
        f => { f.run.pull_requests = []; },
        f => { f.run.status = 'in_progress'; },
        f => { f.run.conclusion = 'failure'; },
        f => { f.suite.status = 'in_progress'; },
        f => { f.suite.conclusion = 'failure'; },
        f => { f.suite.app.slug = 'third-party'; },
        f => { f.total = 2; },
    ]) {
        const f = fixture(); const trusted = await proof(f); mutate(f);
        expect((await duplicateValidationSuite(f.api, f.suite, snapshot.head, trusted)).duplicate).toBe(false);
    }
    const f = fixture();
    expect((await duplicateValidationSuite(f.api, f.suite, snapshot.head)).duplicate).toBe(false);
});

test('承認待ちsuite内でも失敗・未完了・未知check・別suite・別URLを除外しない', async () => {
    const f = fixture();
    const evidence = await duplicateValidationSuite(f.api, f.suite, snapshot.head, await proof(f));
    for (const mutation of [
        { name: 'unreviewed check' }, { conclusion: 'failure' }, { conclusion: 'cancelled' },
        { status: 'in_progress' }, { check_suite: { id: 101 } }, { head_sha: snapshot.merge },
        { app: { slug: 'third-party' } }, { details_url: 'https://attacker.invalid/run/10' },
        { details_url: `https://github.com/${repository}/actions/runs/11/job/15` },
        { details_url: `https://github.com/${repository}/actions/runs/10/job/15/extra` },
    ]) expect(duplicateValidationCheck({ ...f.check, ...mutation }, evidence, snapshot.head)).toBe(false);
});

test('suiteとcheckの統合判定で既知承認待ちだけ通し、失敗・未知・status・検証欠落は保留する', async () => {
    const old = process.env.GITHUB_RUN_ID;
    process.env.GITHUB_RUN_ID = '90';
    try {
        for (const workflow of Object.keys(coverage)) {
            const f = fixture(workflow);
            expect(await otherChecks(f.api, [snapshot.head, snapshot.merge], snapshot)).toBeNull();
        }
        for (const mutate of [
            f => { f.checks.push({ ...f.check, id: 201, name: 'unknown' }); },
            f => { f.check.conclusion = 'failure'; },
            f => { f.check.status = 'queued'; },
            f => { f.suite.conclusion = 'failure'; },
            f => { f.statuses = [{ state: 'pending' }]; },
            f => { f.jobs.pop(); },
            f => { f.current.id++; },
        ]) {
            const f = fixture(); mutate(f);
            expect(await otherChecks(f.api, [snapshot.head, snapshot.merge], snapshot)).toBeString();
        }
    } finally {
        if (old === undefined) delete process.env.GITHUB_RUN_ID;
        else process.env.GITHUB_RUN_ID = old;
    }
});
