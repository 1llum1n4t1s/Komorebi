// 実サーバーの原子性を検証するまでは activation gate を有効にしない。
export const STATUS_CONTEXT = 'kagayoi/automation-merge-guard-v1';
export const OWNER = 16895591;
export const sameContext = (left, right) => typeof left === 'string' && typeof right === 'string' && left.toLowerCase() === right.toLowerCase();
export const activated = (env = process.env) => env.BUN_SECURITY_NATIVE_MERGE_ACTIVATED === 'true';
export const held = (reason, state = 'held', extra = {}) => ({ merged: false, ready: false, merge_state: state, hold_reason: reason, ...extra });
function requireValue(ok, reason) { if (!ok) throw new Error(reason); }
const canonical = value => Array.isArray(value) ? value.map(canonical) : value && typeof value === 'object'
    ? Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])])) : value;
const same = (left, right) => JSON.stringify(canonical(left)) === JSON.stringify(canonical(right));

export async function requiredRule(api, repository, env = process.env) {
    requireValue(repository.default_branch === 'main' && repository.owner?.id === OWNER && repository.owner?.type === 'User', 'Required main/owner identity is not configured');
    const appId = Number(env.DEPENDENCY_GUARD_APP_ID), ruleId = Number(env.DEPENDENCY_GUARD_RULESET_ID), updatedAt = env.DEPENDENCY_GUARD_RULESET_UPDATED_AT;
    requireValue(/^[1-9][0-9]*$/.test(env.DEPENDENCY_GUARD_APP_ID ?? '') && Number.isSafeInteger(appId) &&
        /^[1-9][0-9]*$/.test(env.DEPENDENCY_GUARD_RULESET_ID ?? '') && Number.isSafeInteger(ruleId) &&
        typeof updatedAt === 'string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(updatedAt) && Number.isFinite(Date.parse(updatedAt)), 'Guard App or ruleset pin is not configured');
    const rules = await api.request('/rules/branches/main');
    requireValue(Array.isArray(rules), 'Effective branch rules are unavailable');
    const covers = rule => {
        if (rule.type !== 'required_status_checks' || rule.parameters?.strict_required_status_checks_policy !== true || !Array.isArray(rule.parameters.required_status_checks)) return false;
        const checks = rule.parameters.required_status_checks.filter(check => sameContext(check.context, STATUS_CONTEXT));
        return checks.length === 1 && checks[0].context === STATUS_CONTEXT && checks[0].integration_id === appId;
    };
    const matches = rules.filter(covers);
    requireValue(matches.length === 1, 'Required status with GitHub Actions source is missing or ambiguous');
    const effective = matches[0];
    requireValue(effective.ruleset_id === ruleId && effective.ruleset_source_type === 'Repository' && effective.ruleset_source === api.repository, 'Required status ruleset source is not this repository');
    const full = await api.request(`/rulesets/${effective.ruleset_id}`);
    requireValue(full.id === effective.ruleset_id && full.updated_at === updatedAt && full.target === 'branch' && full.enforcement === 'active' && full.source_type === 'Repository' && full.source === api.repository, 'Required ruleset identity or enforcement changed');
    requireValue(same(full.conditions?.ref_name?.include, ['refs/heads/main']) && same(full.conditions?.ref_name?.exclude, []), 'Required ruleset target must be exactly main');
    const matchingRules = full.rules?.filter(covers);
    requireValue(matchingRules?.length === 1 && same(matchingRules[0].parameters, effective.parameters), 'Effective and configured required checks disagree');
    return effective.ruleset_id;
}

export function statusTarget(api, evidence) {
    return `https://github.com/${api.repository}/actions/runs/${evidence.runId}/attempts/${evidence.attempt}`;
}
export function statusDescription(evidence) { return `PR ${evidence.pr} H ${evidence.head}; run ${evidence.runId} a ${evidence.attempt}; ${evidence.nonce}`; }
export function matchesStatus(status, api, evidence, identity) {
    return status.context === STATUS_CONTEXT && status.state === 'success' && status.creator?.id === identity.id &&
        status.creator?.login === identity.login && status.target_url === statusTarget(api, evidence) && status.description === statusDescription(evidence);
}
export async function statusHistory(api, head, { signal } = {}) {
    const statuses = [], ids = new Set(); let declaredLast = null, probe = false, repositoryId = null;
    const url = `https://api.github.com/repos/${api.repository}/statuses/${head}`;
    for (let page = 1; page <= 10; page++) {
        signal?.throwIfAborted();
        const response = await api.response(`/commits/${head}/statuses?per_page=100&page=${page}`, 'GET', undefined, { signal });
        signal?.throwIfAborted();
        requireValue(response?.status === 200 && Array.isArray(response.data) && response.data.length <= 100, 'Commit status history is unavailable');
        const remaining = response.headers?.remaining;
        if (remaining !== null && remaining !== undefined) requireValue(/^\d+$/.test(String(remaining)) && Number(remaining) >= 200, 'GitHub API budget is low');
        let next = false, last = null; const rels = new Set(), link = response.headers?.link;
        if (link) for (const part of link.split(',')) {
            const m = /^\s*<([^>]+)>;\s*rel="(next|last|first|prev)"\s*$/.exec(part);
            requireValue(m && !rels.has(m[2]), 'Invalid status pagination'); rels.add(m[2]);
            let target; try { target = new URL(m[1]); } catch { throw new Error('Invalid status pagination URL'); }
            const keys = [...target.searchParams.keys()], raw = target.searchParams.get('page');
            if (target.pathname !== `/repos/${api.repository}/commits/${head}/statuses`) {
                const match = /^\/repositories\/([1-9][0-9]*)\/commits\/([0-9a-f]{40})\/statuses$/.exec(target.pathname);
                requireValue(match && match[2] === head && Number.isSafeInteger(Number(match[1])), 'Invalid status pagination target');
                if (repositoryId === null) {
                    const repository = await api.request('', 'GET', undefined, false, { signal });
                    requireValue(repository?.full_name === api.repository && Number.isSafeInteger(repository.id), 'Repository identity unavailable'); repositoryId = repository.id;
                }
                requireValue(Number(match[1]) === repositoryId, 'Invalid status pagination repository');
            }
            requireValue(target.protocol === 'https:' && target.host === 'api.github.com' && !target.username && !target.password && !target.hash &&
                keys.sort().join(',') === 'page,per_page' &&
                target.searchParams.get('per_page') === '100' && /^[1-9][0-9]*$/.test(raw ?? '') && Number.isSafeInteger(Number(raw)), 'Invalid status pagination target');
            if (m[2] === 'next') { requireValue(Number(raw) === page + 1, 'Invalid status next page'); next = true; }
            if (m[2] === 'last') last = Number(raw);
            if (m[2] === 'prev') requireValue(page > 1 && Number(raw) === page - 1, 'Invalid status previous page');
            if (m[2] === 'first') requireValue(Number(raw) === 1, 'Invalid status first page');
        }
        requireValue(last === null || (last < page ? probe && response.data.length === 0 && last === page - 1 && !next : last === page ? !next : next), 'Incomplete status pagination');
        requireValue(last === null || declaredLast === null || last === declaredLast, 'Status last page changed');
        if (last !== null) declaredLast = last;
        requireValue(declaredLast === null || page <= declaredLast || (probe && page === declaredLast + 1 && response.data.length === 0), 'Status last page changed');
        requireValue(declaredLast === null || page >= declaredLast || next, 'Status page chain is incomplete');
        requireValue(!next || response.data.length === 100, 'Incomplete status page');
        for (const item of response.data) {
            requireValue(Number.isSafeInteger(item?.id) && item.id > 0 && !ids.has(item.id) &&
                typeof item.context === 'string' && item.context.length > 0 &&
                ['success', 'failure', 'error', 'pending'].includes(item.state) && item.url === url &&
                (item.target_url === null || typeof item.target_url === 'string') &&
                (item.description === null || typeof item.description === 'string') &&
                (item.creator === null || (Number.isSafeInteger(item.creator?.id) && typeof item.creator?.login === 'string')), 'Invalid or duplicate commit status history');
            ids.add(item.id); statuses.push(item);
        }
        if (!next && response.data.length < 100) return statuses;
        probe = !next;
    }
    throw new Error('Commit status history is too large to inspect safely');
}
export async function latestStatuses(api, revision, { signal } = {}) {
    async function combined() {
        signal?.throwIfAborted();
        const response = await api.request(`/commits/${revision}/status?per_page=100`, 'GET', undefined, false, { signal });
        signal?.throwIfAborted();
        requireValue(response.sha === revision && Array.isArray(response.statuses) && Number.isSafeInteger(response.total_count) &&
            response.total_count >= 0 && response.total_count === response.statuses.length && response.total_count <= 100,
        'Cannot verify all commit statuses');
        const contexts = new Set();
        for (const item of response.statuses) {
            requireValue(Number.isSafeInteger(item?.id) && typeof item.context === 'string' && !contexts.has(item.context.toLowerCase()) &&
                ['success', 'failure', 'error', 'pending'].includes(item.state), 'Invalid combined commit status');
            contexts.add(item.context.toLowerCase());
        }
        return response.statuses;
    }
    const initial = await combined();
    const history = await statusHistory(api, revision, { signal });
    const latest = new Map();
    for (const item of history) if (!latest.has(item.context.toLowerCase())) latest.set(item.context.toLowerCase(), item);
    requireValue(latest.size === initial.length, 'Combined and individual status contexts disagree');
    const resolved = initial.map(item => {
        const full = latest.get(item.context.toLowerCase());
        requireValue(full && full.id === item.id && full.state === item.state && full.url === item.url &&
            full.target_url === item.target_url && full.description === item.description, 'Combined and individual status identity disagree');
        return full;
    });
    // 投稿や他者の更新と履歴取得の間に最新値が変われば保留する。
    const final = await combined();
    requireValue(same(final, initial), 'Commit status changed while verifying');
    return resolved;
}

// 同期 PUT は1回のみ。通信断・5xx・不正応答は送信済みか不明として保留する。
export async function requestMerge(api, snapshot) {
    let response;
    try { response = await api.response(`/pulls/${snapshot.pr}/merge`, 'PUT', { sha: snapshot.head, merge_method: 'squash' }); }
    catch { return held('Merge request outcome is unknown; do not retry automatically', 'unknown'); }
    if (response?.status === 200 && response.data?.merged === true && /^[0-9a-f]{40}$/.test(response.data.sha ?? ''))
        return { merge_state: 'reported-merged', sha: response.data.sha };
    if ([405, 409, 422].includes(response?.status))
        return held(`Merge request was rejected (HTTP ${response.status})`, 'failed', { http_status: response.status });
    return held('Merge request outcome is unknown; do not retry automatically', 'unknown');
}
