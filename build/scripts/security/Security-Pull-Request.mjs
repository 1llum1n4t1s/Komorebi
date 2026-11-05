// 権限のあるジョブ専用。依存パッケージや候補内のコードを実行しない。
import { appendFileSync, readFileSync, lstatSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createHash, randomUUID } from 'node:crypto';
import { readLedger, reportComment, findNotification, saveRecord, saveProposal, unresolved } from './Security-Record.mjs';
import { hasSecurityMarker } from './Security-Marker.mjs';
import { mitigations, remainingAdvisories } from './Maintain-Security.mjs';
import { STATUS_CONTEXT, sameContext, activated, held, requiredRule, statusTarget, statusDescription, matchesStatus, latestStatuses, statusHistory, requestMerge } from './Native-Merge.mjs';

export const BRANCH = 'automation/bun-security-';
const validBranch = value => typeof value === 'string' && /^automation\/bun-security-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value);
const BOT = 41898282;
const marker = '<!-- bun-security-automation -->';
const legacyCommitMessage = 'fix(deps): Bun の脆弱な依存関係を更新';
const allowed = new Set(['package.json', 'bun.lock']);
function assert(ok, message) { if (!ok) throw new Error(message); }
function sha(value) { assert(/^[0-9a-f]{40}$/.test(value ?? ''), 'Invalid commit SHA'); return value; }
function output(values) {
  for (const [key, value] of Object.entries(values)) {
    const line = `${key}=${String(value).replace(/[\r\n]/g, '')}\n`;
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, line);
    else process.stdout.write(line);
  }
}
function boundedUtf8(value, limit) {
  const bytes = Buffer.from(value, 'utf8');
  if (bytes.length <= limit) return value;
  const suffix = '\n[省略]\n', room = Math.max(0, limit - Buffer.byteLength(suffix));
  let end = room;
  while (end > 0 && (bytes[end] & 0xc0) === 0x80) end--;
  return bytes.subarray(0, end).toString('utf8') + suffix;
}
export function client(repository = process.env.GITHUB_REPOSITORY, token = process.env.GH_TOKEN || process.env.GITHUB_TOKEN) {
  assert(/^[\w.-]+\/[\w.-]+$/.test(repository ?? ''), 'Invalid repository');
  assert(token, 'GitHub token required');
  const fetchApi = async (path, method = 'GET', body, { signal, global = false } = {}) => {
    const response = await fetch(`https://api.github.com${global ? '' : `/repos/${repository}`}${path}`, {
      method, headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2026-03-10', 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body), redirect: 'error', signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(30000)]) : AbortSignal.timeout(30000),
    });
    return { status: response.status, data: response.status === 204 ? null : await response.json(), headers: { link: response.headers.get('link'), remaining: response.headers.get('x-ratelimit-remaining') } };
  };
  const response = (path, method = 'GET', body, options = {}) => fetchApi(path, method, body, options);
  return { repository, response, async global(path) {
    const result = await fetchApi(path, 'GET', undefined, { global: true });
    assert(result.status === 200, `GitHub global API failed (${result.status})`); return result.data;
  }, async graphql(query, variables, { signal } = {}) {
    return fetchApi('/graphql', 'POST', { query, variables }, { global: true, signal });
  }, async request(path, method = 'GET', body, optional = false, options = {}) {
    const result = await response(path, method, body, options);
    if (optional && result.status === 404) return null;
    assert(result.status >= 200 && result.status < 300, `GitHub ${method} ${path.split('?')[0]} failed (${result.status})`);
    return result.data;
  } };
}
function owned(pr, api, base) {
  assert(pr.user?.id === BOT && pr.user?.login === 'github-actions[bot]', 'PR is not owned by GitHub Actions');
  assert(pr.head?.repo?.full_name === api.repository && pr.base?.repo?.full_name === api.repository,
    'PR repository mismatch');
  assert(validBranch(pr.head.ref) && pr.base.ref === base, 'PR branch mismatch');
}
async function metadata(api) {
  const repo = await api.request('');
  assert(repo.full_name === api.repository && repo.default_branch && repo.default_branch !== BRANCH, 'Invalid default branch');
  return repo;
}
async function ref(api, name, optional = false) {
  return (await api.request(`/git/ref/${name}`, 'GET', undefined, optional))?.object?.sha;
}
export function candidateFiles(report, candidateRoot) {
  assert(Array.isArray(report.changedFiles), 'Missing changedFiles');
  assert(report.changedFiles.length <= 2 && new Set(report.changedFiles).size === report.changedFiles.length, 'Invalid changedFiles');
  return report.changedFiles.map(path => {
    assert(allowed.has(path), `Disallowed candidate path: ${path}`);
    const root = resolve(candidateRoot);
    assert(lstatSync(root).isDirectory() && !lstatSync(root).isSymbolicLink(), 'Invalid candidate root');
    const file = join(root, path);
    assert(lstatSync(file).isFile() && !lstatSync(file).isSymbolicLink(), 'Candidate must be a regular file');
    return { path: 'build/scripts/' + path, content: readFileSync(file).toString('base64') };
  });
}
async function allowedDiff(api, base, head) {
  const diff = await api.request(`/compare/${sha(base)}...${sha(head)}`);
  assert(Array.isArray(diff.files) && diff.files.length <= 2 && diff.files.every(file =>
    [...allowed].some(path => file.filename === 'build/scripts/' + path) && !file.previous_filename && file.status !== 'removed'), 'Branch contains non-dependency changes');
}
function checkoutHead(directory) {
  const { GH_TOKEN, GITHUB_TOKEN, ...env } = process.env;
  const result = spawnSync('git', ['rev-parse', 'HEAD'], { cwd: directory, encoding: 'utf8', windowsHide: true, env });
  assert(result.status === 0, 'Cannot inspect trusted checkout HEAD');
  return result.stdout.trim();
}
export const needsSecurityCommit = (previous, tree) => !previous || previous.tree?.sha !== tree || !hasSecurityMarker(previous.message);
async function proposalPr(api, repo, number, { signal } = {}) {
  signal?.throwIfAborted();
  const pr = await api.request(`/pulls/${number}`, 'GET', undefined, false, { signal });
  signal?.throwIfAborted();
  assert(pr?.number === number && ['open', 'closed'].includes(pr.state) && typeof pr.merged === 'boolean', 'Proposal PR identity unavailable');
  owned(pr, api, repo.default_branch);
  return pr;
}
async function verifyHumanControl(api, number) {
  const [owner, repo] = api.repository.split('/'), signal = AbortSignal.timeout(45000), ids = new Set(), cursors = new Set();
  const query = `query($owner:String!,$repo:String!,$number:Int!,$before:String){repository(owner:$owner,name:$repo){pullRequest(number:$number){number timelineItems(last:100,before:$before,itemTypes:[AUTO_MERGE_ENABLED_EVENT,AUTO_MERGE_DISABLED_EVENT]){pageInfo{hasPreviousPage startCursor} nodes{__typename ... on AutoMergeEnabledEvent{id createdAt actor{__typename login ... on User{id} ... on Bot{id}}} ... on AutoMergeDisabledEvent{id createdAt actor{__typename login ... on User{id} ... on Bot{id}}}}}}}}`;
  async function fetchPage(before) {
    signal.throwIfAborted();
    const result = await api.graphql(query, { owner, repo, number, before }, { signal });
    signal.throwIfAborted();
    const pr = result?.data?.data?.repository?.pullRequest;
    assert(result?.status === 200 && !result.data?.errors && pr?.number === number && Array.isArray(pr.timelineItems?.nodes) &&
      pr.timelineItems.nodes.length <= 100 && typeof pr.timelineItems.pageInfo?.hasPreviousPage === 'boolean', 'Human control timeline is unavailable');
    return pr.timelineItems;
  }
  let before = null, first, previous = Infinity, latestHuman = null;
  for (let page = 0; page < 20; page++) {
    const part = await fetchPage(before); if (page === 0) first = JSON.stringify(part);
    for (const event of [...part.nodes].reverse()) {
      const time = Date.parse(event?.createdAt);
      assert(typeof event?.id === 'string' && event.id.length > 0 && !ids.has(event.id) && Number.isFinite(time) && time < previous &&
        ['AutoMergeEnabledEvent', 'AutoMergeDisabledEvent'].includes(event.__typename) && ['User', 'Bot'].includes(event.actor?.__typename) &&
        typeof event.actor.id === 'string' && event.actor.id.length > 0 && typeof event.actor.login === 'string' && event.actor.login.length > 0, 'Human control event identity is unknown');
      ids.add(event.id); previous = time;
      if (latestHuman === null && event.actor.__typename === 'User') latestHuman = event.__typename;
    }
    if (!part.pageInfo.hasPreviousPage) {
      assert(JSON.stringify(await fetchPage(null)) === first, 'Human control timeline changed');
      assert(latestHuman !== 'AutoMergeDisabledEvent', 'Human disabled auto-merge'); return;
    }
    const cursor = part.pageInfo.startCursor;
    assert(part.nodes.length === 100 && typeof cursor === 'string' && cursor.length > 0 && !cursors.has(cursor), 'Human control timeline is incomplete');
    cursors.add(cursor); before = cursor;
  }
  throw new Error('Human control timeline pagination limit exceeded');
}
async function blobsAt(api, commit) {
  const result = {};
  for (const [name, key] of [['package.json', 'package_blob'], ['bun.lock', 'lock_blob']]) {
    const entry = await api.request(`/contents/build/scripts/${name}?ref=${sha(commit)}`);
    assert(entry?.type === 'file', 'Cannot inspect candidate blob'); result[key] = sha(entry.sha);
  }
  return result;
}
function sameValue(a, b) { return JSON.stringify(a) === JSON.stringify(b); }
function exactProposal(pr, proposal) {
  return pr.number === proposal.pr && pr.head?.ref === proposal.branch && pr.head?.sha === proposal.head;
}
async function reconcileProposal(api, repo, ledger) {
  assert(!unresolved(ledger.record), 'Native request remains unresolved');
  if (ledger.state === 'preparing')
    return { blocked: true, reason: 'Proposal operation outcome is unknown; manual review required' };
  if (ledger.state === 'preparing-update') {
    const p = ledger.preparing, branch = await ref(api, `heads/${p.branch}`, true);
    if (branch !== p.head) return { blocked: true, reason: 'Branch update outcome is unknown; manual review required' };
    const pr = await proposalPr(api, repo, p.pr), commit = await api.request(`/git/commits/${p.head}`);
    if (pr.state !== 'open' || pr.draft !== false || pr.merged !== false || pr.auto_merge !== null || pr.head.ref !== p.branch || pr.head.sha !== p.head || pr.base.sha !== p.base || await ref(api, 'heads/main') !== p.base)
      return { blocked: true, reason: 'Updated PR is not safely confirmed' };
    assert(commit.sha === p.head && commit.tree?.sha === p.tree && commit.parents?.length === 2 && commit.parents[0]?.sha === p.old_head && commit.parents[1]?.sha === p.base &&
      hasSecurityMarker(commit.message) && commit.message.includes(`\nBun-Security-Generation: ${p.branch.slice(BRANCH.length)}\n`) &&
      sameValue(await blobsAt(api, p.head), { package_blob: p.package_blob, lock_blob: p.lock_blob }), 'Updated commit identity changed');
    const again = await proposalPr(api, repo, p.pr);
    assert(again.state === 'open' && again.draft === false && again.merged === false && again.auto_merge === null && again.head.ref === p.branch && again.head.sha === p.head && again.base.sha === p.base &&
      await ref(api, `heads/${p.branch}`) === p.head && await ref(api, 'heads/main') === p.base, 'Updated PR changed before confirmation');
    await verifyHumanControl(api, p.pr);
    await saveProposal(api, repo, { state: 'idle', proposal: { pr: p.pr, branch: p.branch, base: p.base, head: p.head, package_blob: p.package_blob, lock_blob: p.lock_blob }, lastClosed: ledger.lastClosed, preparing: null });
    return { blocked: false };
  }
  if (ledger.proposal) {
    const pr = await proposalPr(api, repo, ledger.proposal.pr);
    if (!exactProposal(pr, ledger.proposal)) return { blocked: true, reason: 'Proposal identity changed' };
    if (pr.state === 'closed') {
      if (pr.merged) return { blocked: true, reason: 'Proposal merge has no verified native record' };
      const again = await proposalPr(api, repo, pr.number);
      if (again.state !== 'closed' || again.merged || !exactProposal(again, ledger.proposal)) return { blocked: true, reason: 'Closed proposal changed before confirmation' };
      await saveProposal(api, repo, { state: 'idle', proposal: null, lastClosed: ledger.proposal, preparing: null });
    }
  } else if (ledger.lastClosed) {
    const pr = await proposalPr(api, repo, ledger.lastClosed.pr);
    if (pr.state === 'open') {
      if (!exactProposal(pr, ledger.lastClosed) || pr.merged) return { blocked: true, reason: 'Reopened proposal changed' };
      const again = await proposalPr(api, repo, pr.number);
      if (again.state !== 'open' || !exactProposal(again, ledger.lastClosed)) return { blocked: true, reason: 'Reopened proposal changed before confirmation' };
      await saveProposal(api, repo, { state: 'idle', proposal: ledger.lastClosed, lastClosed: null, preparing: null });
    } else if (pr.merged || !exactProposal(pr, ledger.lastClosed)) return { blocked: true, reason: 'Closed proposal changed' };
  }
  return { blocked: false };
}
export async function prepare(api, options) {
  const repo = await metadata(api);
  let ledger = await readLedger(api, repo);
  assert(!unresolved(ledger.record), 'Native merge remains unresolved; no candidate update permitted');
  const synced = await reconcileProposal(api, repo, ledger);
  assert(!synced.blocked, synced.reason);
  const observed = await observeHistory(api, repo);
  assert(!observed.pending, 'Native merge remains unresolved; no candidate update permitted');
  ledger = observed.ledger;
  const report = JSON.parse(readFileSync(options.report, 'utf8'));
  const remaining_count = Array.isArray(report.remaining) ? report.remaining.length : 'unknown';
  const needs_followup = report.needs_followup === true || (report.upstreamFixes?.length || 0) > 0;
  const files = candidateFiles(report, options['candidate-root'] || report.candidateRoot);
  const base = sha(await ref(api, `heads/${repo.default_branch}`));
  assert(base === sha(process.env.GITHUB_SHA), 'Default branch moved; rerun against its current SHA');
  if (!files.length) {
    const raw = report.rawAfter, validRaw = raw && typeof raw === 'object' && !Array.isArray(raw) && Object.values(raw).every(Array.isArray);
    const verified = mitigations();
    const clean = report.status === 'clean' && validRaw && Object.values(raw).every(items => items.length === 0);
    const mitigated = report.status === 'mitigated' && validRaw && Array.isArray(report.mitigations) && report.mitigations.length > 0 &&
      Array.isArray(verified) && sameValue(report.mitigations, verified) && remainingAdvisories(raw, verified).length === 0;
    if (ledger.proposal && validRaw && (clean || mitigated) && Array.isArray(report.remaining) && report.remaining.length === 0 &&
      report.needs_followup === false && Array.isArray(report.upstreamFixes) && report.upstreamFixes.length === 0 && report.result_changed === false)
      await saveProposal(api, repo, { state: 'idle', proposal: null, lastClosed: ledger.lastClosed, preparing: null });
    return { has_changes: false, remaining_count, needs_followup };
  }
  assert(options.checkout && checkoutHead(options.checkout) === base, 'Checkout is not the trusted workflow base');
  const candidate = {};
  for (const [name, key] of [['package.json', 'package_blob'], ['bun.lock', 'lock_blob']]) {
    const changed = files.find(file => file.path === `build/scripts/${name}`);
    if (changed) { const bytes = Buffer.from(changed.content, 'base64'); candidate[key] = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex'); }
    else { const entry = await api.request(`/contents/build/scripts/${name}?ref=${base}`); assert(entry?.type === 'file', 'Cannot inspect base candidate blob'); candidate[key] = sha(entry.sha); }
  }
  assert(!ledger.lastClosed || candidate.package_blob !== ledger.lastClosed.package_blob || candidate.lock_blob !== ledger.lastClosed.lock_blob, 'Closed candidate is unchanged; automatic recreation prohibited');
  if (!ledger.proposal && ledger.proposals[0]?.package_blob === candidate.package_blob && ledger.proposals[0]?.lock_blob === candidate.lock_blob) {
    const recent = ledger.proposals[0], pr = await proposalPr(api, repo, recent.pr);
    assert(exactProposal(pr, recent), 'Recent proposal identity changed');
    assert(pr.state !== 'closed' || pr.merged === true, 'Closed candidate is unchanged; automatic recreation prohibited');
  }
  let proposal = ledger.proposal, pr;
  if (proposal) {
    pr = await proposalPr(api, repo, proposal.pr);
    assert(exactProposal(pr, proposal) && pr.state === 'open' && pr.draft === false && pr.merged === false && pr.auto_merge === null, 'Proposal is not open and ready');
    await verifyHumanControl(api, proposal.pr);
  }
  async function makeTree() {
    const baseCommit = await api.request(`/git/commits/${base}`);
    assert(baseCommit.sha === base && sha(baseCommit.tree?.sha), 'Base commit identity changed');
    const entries = [];
    for (const file of files) {
      const blob = await api.request('/git/blobs', 'POST', { content: file.content, encoding: 'base64' });
      const expected = file.path.endsWith('/package.json') ? candidate.package_blob : candidate.lock_blob;
      assert(blob.sha === expected, 'Created blob identity changed');
      entries.push({ path: file.path, mode: '100644', type: 'blob', sha: blob.sha });
    }
    const tree = await api.request('/git/trees', 'POST', { base_tree: baseCommit.tree.sha, tree: entries });
    return { tree: sha(tree.sha), baseTree: baseCommit.tree.sha };
  }
  if (proposal && (proposal.base !== base || proposal.package_blob !== candidate.package_blob || proposal.lock_blob !== candidate.lock_blob)) {
    assert(typeof repo.node_id === 'string' && repo.node_id.length > 0 && proposal.head !== base, 'Repository or prior head identity unavailable');
    const { tree, baseTree } = await makeTree();
    if (tree === baseTree) return { has_changes: false, remaining_count, needs_followup };
    const message = `${legacyCommitMessage}\n\nBun-Security-Generation: ${proposal.branch.slice(BRANCH.length)}\nBun-Security-Automation: v1`;
    const created = await api.request('/git/commits', 'POST', { message, tree, parents: [proposal.head, base] });
    const head = sha(created.sha), confirmed = await api.request(`/git/commits/${head}`);
    assert(confirmed?.sha === head && confirmed.tree?.sha === tree && confirmed.parents?.length === 2 &&
      confirmed.parents[0]?.sha === proposal.head && confirmed.parents[1]?.sha === base && confirmed.message === message, 'Updated candidate commit identity changed');
    const preparing = { pr: proposal.pr, branch: proposal.branch, old_head: proposal.head, head, base, tree,
      old_package_blob: proposal.package_blob, old_lock_blob: proposal.lock_blob, ...candidate };
    await saveProposal(api, repo, { state: 'preparing-update', proposal, lastClosed: ledger.lastClosed, preparing });
    const before = await proposalPr(api, repo, proposal.pr), recorded = await readLedger(api, repo);
    assert(exactProposal(before, proposal) && before.state === 'open' && before.draft === false && before.merged === false && before.auto_merge === null &&
      await ref(api, `heads/${proposal.branch}`) === proposal.head && await ref(api, 'heads/main') === base &&
      recorded.state === 'preparing-update' && sameValue(recorded.preparing, preparing), 'Proposal changed before branch update');
    await verifyHumanControl(api, proposal.pr);
    const mutationId = randomUUID();
    try {
      await api.graphql('mutation($input:UpdateRefsInput!){updateRefs(input:$input){clientMutationId}}',
        { input: { repositoryId: repo.node_id, clientMutationId: mutationId, refUpdates: [{ name: `refs/heads/${proposal.branch}`, beforeOid: proposal.head, afterOid: head, force: false }] } });
    } catch { /* refとPRの読取が確定できない場合は再送しない。 */ }
    const refreshed = await readLedger(api, repo), synced = await reconcileProposal(api, repo, refreshed);
    assert(!synced.blocked, synced.reason);
    ledger = await readLedger(api, repo, { history: true }); proposal = ledger.proposal;
    assert(proposal?.head === head && proposal.pr === preparing.pr, 'Updated proposal was not confirmed');
  }
  if (!proposal) {
    const unique = [], seen = new Set();
    for (const item of ledger.proposals) if (!seen.has(item.pr)) { seen.add(item.pr); if (item.package_blob === candidate.package_blob && item.lock_blob === candidate.lock_blob) unique.push(item); }
    const matches = [];
    for (const prior of unique) {
      const old = await proposalPr(api, repo, prior.pr);
      if (old.state !== 'open') continue;
      assert(exactProposal(old, prior) && old.draft === false && old.merged === false && old.auto_merge === null && await ref(api, `heads/${prior.branch}`) === prior.head, 'Historical proposal identity changed');
      await verifyHumanControl(api, prior.pr);
      const commit = await api.request(`/git/commits/${prior.head}`), updates = ledger.updates.filter(item => item.pr === prior.pr && item.head === prior.head);
      assert(updates.length <= 1, 'Historical update history is ambiguous');
      const update = updates[0];
      const parents = update ? [update.old_head, prior.base] : [prior.base];
      assert(commit.sha === prior.head && commit.parents?.length === parents.length && commit.parents.every((item, i) => item.sha === parents[i]) &&
        (!update || update.base === prior.base && update.branch === prior.branch && update.tree === commit.tree?.sha) &&
        hasSecurityMarker(commit.message) && commit.message.includes(`\nBun-Security-Generation: ${prior.branch.slice(BRANCH.length)}\n`) && sameValue(await blobsAt(api, prior.head), candidate), 'Historical proposal commit changed');
      matches.push(prior);
    }
    assert(matches.length <= 1 && await ref(api, 'heads/main') === base, 'Historical proposal is ambiguous or main changed');
    if (matches.length === 1) {
      proposal = matches[0]; await saveProposal(api, repo, { state: 'idle', proposal, lastClosed: null, preparing: null });
      if (proposal.base !== base) return prepare(api, options);
    }
  }
  if (!proposal) {
    const baseCommit = await api.request(`/git/commits/${base}`);
    assert(baseCommit.sha === base && sha(baseCommit.tree?.sha), 'Base commit identity changed');
    const entries = [];
    for (const file of files) {
      const blob = await api.request('/git/blobs', 'POST', { content: file.content, encoding: 'base64' });
      const expected = file.path.endsWith('/package.json') ? candidate.package_blob : candidate.lock_blob;
      assert(blob.sha === expected, 'Created blob identity changed');
      entries.push({ path: file.path, mode: '100644', type: 'blob', sha: blob.sha });
    }
    const tree = await api.request('/git/trees', 'POST', { base_tree: baseCommit.tree.sha, tree: entries });
    sha(tree.sha);
    if (tree.sha === baseCommit.tree.sha) return { has_changes: false, remaining_count, needs_followup };
    const generation = randomUUID(), branch = BRANCH + generation;
    const message = `${legacyCommitMessage}\n\nBun-Security-Generation: ${generation}\nBun-Security-Automation: v1`;
    const commit = await api.request('/git/commits', 'POST', { message, tree: tree.sha, parents: [base] });
    const head = sha(commit.sha), created = await api.request(`/git/commits/${head}`);
    assert(created?.sha === head && created.tree?.sha === tree.sha && created.parents?.length === 1 && created.parents[0]?.sha === base &&
      created.message === message && hasSecurityMarker(created.message), 'Created candidate commit identity changed');
    const preparing = { pr: null, branch, base, head, ...candidate };
    await saveProposal(api, repo, { state: 'preparing', proposal: ledger.proposal, lastClosed: ledger.lastClosed, preparing });
    if (ledger.proposal) {
      const current = await proposalPr(api, repo, ledger.proposal.pr);
      assert(exactProposal(current, ledger.proposal) && current.state === 'open' && current.draft === false && current.merged === false, 'Current proposal changed before creation');
    }
    assert(await ref(api, `heads/${branch}`, true) == null && await ref(api, 'heads/main') === base, 'New branch or base changed');
    await api.request('/git/refs', 'POST', { ref: `refs/heads/${branch}`, sha: head });
    assert(await ref(api, `heads/${branch}`) === head && await ref(api, 'heads/main') === base, 'New branch creation outcome unknown');
    let response;
    try { response = await api.response('/pulls', 'POST', { title: 'fix(deps): Bun の脆弱な依存関係を更新', head: branch, base: repo.default_branch,
      body: `${marker}\nBun audit の修正候補です。固定したマージコミットの CI が成功した場合のみ統合します。` }); }
    catch { throw new Error('PR creation outcome is unknown'); }
    assert(response?.status === 201 && Number.isSafeInteger(response.data?.number) && response.data.number > 0, 'PR creation outcome is unknown');
    pr = await proposalPr(api, repo, response.data.number);
    proposal = { pr: response.data.number, branch, base, head, ...candidate };
    assert(exactProposal(pr, proposal) && pr.state === 'open' && pr.draft === false && pr.merged === false && pr.auto_merge === null && pr.base.sha === base &&
      await ref(api, 'heads/main') === base && await ref(api, `heads/${branch}`) === head, 'Created PR identity changed');
    const createdBlobs = await blobsAt(api, head);
    assert(sameValue(createdBlobs, candidate), 'Candidate blobs changed after creation');
    const confirmed = await proposalPr(api, repo, pr.number);
    assert(exactProposal(confirmed, proposal) && confirmed.state === 'open' && confirmed.draft === false && confirmed.merged === false && confirmed.auto_merge === null &&
      confirmed.base.sha === base && await ref(api, 'heads/main') === base && await ref(api, `heads/${branch}`) === head, 'PR changed before confirmation');
    if (ledger.proposal) {
      const current = await proposalPr(api, repo, ledger.proposal.pr);
      assert(exactProposal(current, ledger.proposal) && current.state === 'open' && current.draft === false && current.merged === false, 'Current proposal changed before switch');
    }
    await saveProposal(api, repo, { state: 'idle', proposal, lastClosed: null, preparing: null });
  }
  const head = proposal.head;
  let merge;
  for (let attempt = 0; attempt < 8; attempt++) {
    if (attempt) await new Promise(done => setTimeout(done, 3000));
    pr = await proposalPr(api, repo, proposal.pr);
    assert(exactProposal(pr, proposal) && pr.state === 'open' && pr.draft === false && pr.merged === false && pr.auto_merge === null && pr.base.sha === base, 'PR changed during preparation');
    const proposed = await ref(api, `pull/${pr.number}/merge`, true);
    if (proposed) {
      const commit = await api.request(`/git/commits/${sha(proposed)}`);
      if (commit.sha === proposed && commit.parents?.length === 2 && commit.parents[0].sha === base && commit.parents[1].sha === head) { merge = proposed; break; }
    }
  }
  assert(merge, 'Merge ref is not ready or PR conflicts; rerun workflow');
  return { has_changes: true, remaining_count, needs_followup, pr_number: pr.number, base_branch: repo.default_branch, base_sha: base, head_sha: head, merge_sha: merge };
}
async function exactMerge(api, merge, base, head) {
  const commit = await api.request(`/git/commits/${sha(merge)}`);
  assert(commit.sha === merge && commit.parents?.length === 2 && commit.parents[0].sha === sha(base) && commit.parents[1].sha === sha(head), 'Test merge parents changed');
}
// この一覧の追加時は、固定mergeの reusable 検証にも同じ検証を追加する。
const coveredWorkflows = new Map([
  ['dependency-validation.yml', ['Validate dependency changes', 'Validate security merge guard', 'Require validation success']],
  ['ci.yml', ['Build & Test']],
  ['format-check.yml', ['format-check']],
  ['localization-check.yml', ['localization-check']],
]);
const trustedValidationJobs = [
  'Validate fixed merge / Validate dependency changes',
  'Validate fixed merge / Validate security merge guard',
  'Validate fixed merge / Require validation success',
];
async function listPage(api, path, page, perPage, key) {
  const expected = new URL(`https://api.github.com/repos/${api.repository}${path}`);
  const response = await api.response(path);
  assert(response?.status === 200 && response.data && Number.isSafeInteger(response.data.total_count) && response.data.total_count >= 0 && Array.isArray(response.data[key]), `Cannot inspect complete ${key}`);
  const remaining = response.headers?.remaining;
  if (remaining !== null && remaining !== undefined) assert(/^\d+$/.test(String(remaining)) && Number(remaining) >= 200, 'GitHub API budget is low');
  const totalPages = Math.max(1, Math.ceil(response.data.total_count / perPage));
  let next = false, last = null; const rels = new Set(), link = response.headers?.link;
  if (link) for (const part of link.split(',')) {
    const match = /^\s*<([^>]+)>;\s*rel="(next|last|first|prev)"\s*$/.exec(part);
    assert(match && !rels.has(match[2]), `Invalid ${key} pagination`); rels.add(match[2]);
    let url; try { url = new URL(match[1]); } catch { throw new Error(`Invalid ${key} pagination URL`); }
    if (url.pathname !== expected.pathname) {
      const numeric = /^\/repositories\/([1-9][0-9]*)(\/.*)$/.exec(url.pathname);
      assert(numeric && expected.pathname === `/repos/${api.repository}${numeric[2]}` && Number.isSafeInteger(Number(numeric[1])), `Invalid ${key} pagination target`);
      const repository = await api.request('');
      assert(repository?.full_name === api.repository && repository.id === Number(numeric[1]), `Invalid ${key} pagination repository`);
    }
    const raw = url.searchParams.get('page'), keys = [...url.searchParams.keys()].sort();
    assert(url.protocol === 'https:' && url.host === 'api.github.com' && !url.username && !url.password && !url.hash &&
      sameValue(keys, [...expected.searchParams.keys()].sort()) && /^[1-9][0-9]*$/.test(raw ?? '') && Number.isSafeInteger(Number(raw)) &&
      [...expected.searchParams.keys()].every(key => key === 'page' || url.searchParams.get(key) === expected.searchParams.get(key)), `Invalid ${key} pagination target`);
    const linkedPage = Number(raw);
    if (match[2] === 'next') { assert(linkedPage === page + 1, `Invalid ${key} next page`); next = true; }
    if (match[2] === 'last') last = linkedPage;
    if (match[2] === 'prev') assert(page > 1 && linkedPage === page - 1, `Invalid ${key} previous page`);
    if (match[2] === 'first') assert(linkedPage === 1, `Invalid ${key} first page`);
  }
  assert(next === (page < totalPages) && (last === null || last === totalPages), `${key} Link and count disagree`);
  return response.data;
}
async function completeList(api, path, key, { max = 1000 } = {}) {
  const items = [], ids = new Set(); let total, first;
  for (let page = 1; page <= 10; page++) {
    const result = await listPage(api, path(page), page, 100, key);
    assert(Number.isSafeInteger(result?.total_count) && result.total_count >= 0 && result.total_count <= max && Array.isArray(result[key]), `Cannot inspect complete ${key}`);
    if (total === undefined) total = result.total_count;
    assert(result.total_count === total && result[key].length === Math.min(100, Math.max(0, total - (page - 1) * 100)), `${key} count changed or is incomplete`);
    for (const item of result[key]) {
      assert(Number.isSafeInteger(item?.id) && item.id > 0 && !ids.has(item.id), `Invalid or duplicate ${key} identity`);
      ids.add(item.id); items.push(item);
    }
    if (page === 1) first = JSON.stringify(result[key]);
    if (items.length === total) {
      if (page > 1) {
        const again = await listPage(api, path(1), 1, 100, key);
        assert(again?.total_count === total && Array.isArray(again[key]) && JSON.stringify(again[key]) === first, `${key} changed while inspecting`);
      }
      return items;
    }
  }
  throw new Error(`Too many ${key} to verify safely`);
}
export async function trustedValidationEvidence(api, current, snapshot) {
  if (!current || !snapshot || current.repository?.full_name !== api.repository ||
      current.head_repository?.full_name !== api.repository || current.path !== '.github/workflows/dependency-security.yml' ||
      !['schedule', 'workflow_dispatch'].includes(current.event) || current.head_sha !== snapshot.base ||
      current.head_branch !== snapshot.defaultBranch || !Number.isSafeInteger(current.id) ||
      !Number.isSafeInteger(current.run_attempt) || current.run_attempt < 1 ||
      ![snapshot.base, snapshot.head, snapshot.merge].every(value => /^[0-9a-f]{40}$/.test(value ?? '')) ||
      !/^[1-9][0-9]*$/.test(String(snapshot.pr ?? ''))) return null;
  const workflow = await api.request('/actions/workflows/dependency-security.yml');
  if (!Number.isSafeInteger(workflow.id) || workflow.id !== current.workflow_id || workflow.path !== current.path) return null;
  let jobs;
  try { jobs = await completeList(api, page => `/actions/runs/${current.id}/jobs?filter=latest&per_page=100&page=${page}`, 'jobs'); }
  catch { return null; }
  for (const name of trustedValidationJobs) {
    const matches = jobs.filter(job => job.name === name);
    if (matches.length !== 1 || matches[0].status !== 'completed' || matches[0].conclusion !== 'success' ||
        matches[0].run_id !== current.id || matches[0].run_attempt !== current.run_attempt || matches[0].head_sha !== snapshot.base) return null;
  }
  return { ...snapshot, repository: api.repository, runId: current.id, attempt: current.run_attempt };
}
export async function duplicateValidationSuite(api, suite, validatedHead, trusted = null) {
  if (!trusted || trusted.repository !== api.repository || trusted.head !== validatedHead ||
      suite.app?.slug !== 'github-actions' || suite.status !== 'completed' || suite.conclusion !== 'action_required' ||
      suite.head_sha !== validatedHead || !Number.isSafeInteger(suite.id))
    return { duplicate: false, description: `suite ${suite.id}` };
  const response = await api.request(`/actions/runs?check_suite_id=${suite.id}&per_page=100`);
  const runs = response.workflow_runs;
  if (response.total_count !== 1 || !Array.isArray(runs) || runs.length !== 1)
    return { duplicate: false, description: `suite ${suite.id}: run identity is ambiguous` };
  const run = runs[0];
  const description = `suite ${suite.id}, workflow ${run.workflow_id}, path ${run.path}`;
  const workflowName = [...coveredWorkflows.keys()].find(name => run.path === `.github/workflows/${name}`);
  if (!workflowName || !Number.isSafeInteger(run.id) || run.check_suite_id !== suite.id || run.event !== 'pull_request' || !validBranch(run.head_branch) ||
      run.head_sha !== validatedHead || run.repository?.full_name !== api.repository ||
      run.head_repository?.full_name !== api.repository ||
      !Array.isArray(run.pull_requests) || run.pull_requests.length !== 1 ||
      String(run.pull_requests[0].number) !== String(trusted.pr) || run.pull_requests[0].base?.sha !== trusted.base ||
      run.pull_requests[0].head?.sha !== trusted.head ||
      run.status !== 'completed' || run.conclusion !== 'action_required') return { duplicate: false, description };
  const workflow = await api.request(`/actions/workflows/${workflowName}`);
  return { duplicate: Number.isSafeInteger(workflow.id) && run.workflow_id === workflow.id &&
    workflow.path === run.path, description, suiteId: suite.id, runId: run.id, head: validatedHead,
    repository: api.repository, checkNames: coveredWorkflows.get(workflowName) };
}
export function duplicateValidationCheck(check, evidence, revision) {
  if (!evidence?.duplicate || check.check_suite?.id !== evidence.suiteId || check.app?.slug !== 'github-actions' ||
      check.status !== 'completed' || check.conclusion !== 'action_required' || check.head_sha !== evidence.head ||
      revision !== evidence.head || !evidence.checkNames.includes(check.name)) return false;
  const prefix = `https://github.com/${evidence.repository}/actions/runs/${evidence.runId}`;
  return check.details_url === prefix || (typeof check.details_url === 'string' &&
    check.details_url.startsWith(`${prefix}/job/`) && /^[0-9]+$/.test(check.details_url.slice(`${prefix}/job/`.length)));
}
export async function otherChecks(api, revisions, snapshot, trustedRun = process.env.GITHUB_RUN_ID) {
  const run = process.env.GITHUB_RUN_ID;
  const ownPrefix = /^\d+$/.test(run || '') ? `https://github.com/${api.repository}/actions/runs/${run}` : null;
  const current = ownPrefix ? await api.request(`/actions/runs/${run}`) : null;
  const trustedId = String(trustedRun ?? '');
  const evidenceRun = trustedId === String(run) ? current : /^[1-9][0-9]*$/.test(trustedId) ? await api.request(`/actions/runs/${trustedId}`) : null;
  const trusted = evidenceRun?.id === Number(trustedId) ? await trustedValidationEvidence(api, evidenceRun, snapshot) : null;
  const prefixes = [ownPrefix, trusted ? `https://github.com/${api.repository}/actions/runs/${trustedId}` : null].filter(Boolean);
  const duplicates = new Map();
  for (const revision of new Set(revisions)) {
    const suites = await completeList(api, page => `/commits/${revision}/check-suites?per_page=100&page=${page}`, 'check_suites', { max: 999 });
    for (const suite of suites) {
        const own = suite.app?.slug === 'github-actions' && (suite.id === current?.check_suite_id || (trusted && suite.id === evidenceRun.check_suite_id));
        if (!own && (suite.status !== 'completed' || !['success', 'neutral', 'skipped'].includes(suite.conclusion))) {
          // allowedDiff と trusted needs の全ジョブ成功により、同じ定義・tree の重複だけを代替する。
          const evidence = await duplicateValidationSuite(api, suite, revisions[0], trusted);
          if (!evidence.duplicate) return `Other CI suite is pending, requires approval, or unsuccessful: ${evidence.description}`;
          duplicates.set(suite.id, evidence);
        }
    }
    const checks = await completeList(api, page => `/commits/${revision}/check-runs?filter=latest&per_page=100&page=${page}`, 'check_runs');
    for (const check of checks) {
        const currentRun = check.app?.slug === 'github-actions' && prefixes.some(prefix =>
          check.details_url === prefix || check.details_url?.startsWith(`${prefix}/`));
        if (!currentRun && (check.status !== 'completed' || !['success', 'neutral', 'skipped'].includes(check.conclusion)) &&
            !duplicateValidationCheck(check, duplicates.get(check.check_suite?.id), revision))
          return 'Other CI check is pending or unsuccessful';
    }
    const statuses = await latestStatuses(api, revision);
    if (statuses.some(status => status.state !== 'success')) return 'Other commit status is pending or unsuccessful';
  }
  return null;
}
async function checkedSnapshot(api, options) {
  assert(options['validation-success'] === 'true', 'Trusted CI validation did not succeed');
  const base = sha(options['base-sha']), head = sha(options['head-sha']), tested = sha(options['merge-sha']);
  assert(/^[1-9][0-9]*$/.test(options.pr ?? ''), 'Invalid PR number');
  const repo = await metadata(api);
  const pr = await api.request(`/pulls/${options.pr}`);
  owned(pr, api, repo.default_branch);
  const ledger = await readLedger(api, repo, { history: true });
  assert(['idle', 'signing', 'signed', 'intent'].includes(ledger.state) && ledger.proposal && ledger.proposal.pr === Number(options.pr) && ledger.proposal.branch === pr.head.ref && ledger.proposal.head === head && ledger.proposal.base === base, 'PR is not the current ledger proposal');
  assert(pr.user.type === 'Bot' && Number.isSafeInteger(repo.id) && pr.head.repo.id === repo.id && pr.base.repo.id === repo.id, 'PR repository/Bot identity changed');
  assert(pr.number === Number(options.pr) && pr.state === 'open' && pr.draft === false && pr.merged === false && pr.auto_merge === null && pr.head.sha === head && pr.base.sha === base, 'PR state or commit changed');
  await verifyHumanControl(api, pr.number);
  assert(await ref(api, `heads/${repo.default_branch}`) === base && await ref(api, `heads/${pr.head.ref}`) === head, 'Branch moved');
  assert(await ref(api, `pull/${pr.number}/merge`) === tested, 'Merge ref changed');
  await exactMerge(api, tested, base, head);
  const [headCommit, mergeCommit] = await Promise.all([api.request(`/git/commits/${head}`), api.request(`/git/commits/${tested}`)]);
  const updates = ledger.updates.filter(item => item.pr === Number(options.pr) && item.head === head);
  assert(updates.length <= 1, 'Candidate update history is ambiguous');
  const parents = updates.length ? [updates[0].old_head, base] : [base];
  assert(headCommit.sha === head && headCommit.parents?.length === parents.length && headCommit.parents.every((parent, i) => parent.sha === parents[i]) &&
    (!updates.length || updates[0].base === base && updates[0].branch === pr.head.ref && updates[0].tree === headCommit.tree?.sha) && mergeCommit.sha === tested &&
    mergeCommit.parents?.length === 2 && mergeCommit.parents[0]?.sha === base && mergeCommit.parents[1]?.sha === head &&
    sha(headCommit.tree?.sha) === sha(mergeCommit.tree?.sha) && hasSecurityMarker(headCommit.message) && headCommit.message.includes(`\nBun-Security-Generation: ${pr.head.ref.slice(BRANCH.length)}\n`), 'Security candidate identity or marker is missing');
  const suites = await listPage(api, `/commits/${head}/check-suites?per_page=1&page=1`, 1, 1, 'check_suites');
  assert(Number.isSafeInteger(suites?.total_count) && suites.total_count >= 0 && suites.total_count < 1000 && Array.isArray(suites.check_suites) && suites.check_suites.length === Math.min(suites.total_count, 1) &&
    suites.check_suites.every(suite => Number.isSafeInteger(suite?.id) && suite.id > 0), 'Candidate check suite range is unavailable');
  const checks = await completeList(api, page => `/commits/${head}/check-runs?filter=all&per_page=100&page=${page}`, 'check_runs');
  assert(checks.every(check => typeof check?.name === 'string' && check.name.length > 0), 'Candidate check identity is unavailable');
  assert(!checks.some(check => sameContext(check.name, STATUS_CONTEXT)), 'Security candidate has a reserved check');
  await allowedDiff(api, base, head);
  return { repo, snapshot: { base, head, merge: tested, pr: options.pr, defaultBranch: repo.default_branch } };
}
async function currentEvidence(api, snapshot, id = process.env.GITHUB_RUN_ID) {
  id = String(id ?? '');
  assert(/^[1-9][0-9]*$/.test(id ?? ''), 'Trusted workflow run identity is missing');
  const current = await api.request(`/actions/runs/${id}`);
  assert(current.id === Number(id), 'Trusted workflow run identity changed');
  const evidence = await trustedValidationEvidence(api, current, snapshot);
  assert(evidence, 'Same-attempt fixed merge validation did not succeed');
  return evidence;
}
async function appIdentity(api, env) {
  const slug = env.DEPENDENCY_GUARD_APP_SLUG, appId = Number(env.DEPENDENCY_GUARD_APP_ID), id = Number(env.DEPENDENCY_GUARD_APP_BOT_ID);
  assert(typeof slug === 'string' && /^[a-z0-9-]+$/.test(slug) && /^[1-9][0-9]*$/.test(env.DEPENDENCY_GUARD_APP_ID ?? '') && Number.isSafeInteger(appId) &&
    /^[1-9][0-9]*$/.test(env.DEPENDENCY_GUARD_APP_BOT_ID ?? '') && Number.isSafeInteger(id), 'Guard App identity is not configured');
  return { appId, id, login: `${slug}[bot]` };
}
async function validatedStatus(api, evidence, identity, expectedId) {
  const matches = (await latestStatuses(api, evidence.head)).filter(status => sameContext(status.context, STATUS_CONTEXT));
  assert(matches.length === 1 && Number.isSafeInteger(expectedId) && matches[0].id === expectedId && matchesStatus(matches[0], api, evidence, identity), 'Guard status identity or success is missing');
  const status = matches[0];
  return { id: status.id, app_id: identity.appId, creator: { id: identity.id, login: identity.login }, nonce: evidence.nonce, context: status.context,
    state: status.state, target_url: status.target_url, description: status.description };
}
async function verifyRecordedGuardStatuses(api, head, ledger, identity) {
  const known = new Map();
  for (const item of [...ledger.aborted, ...ledger.failed, ...[ledger.lastCompletion].filter(Boolean),
    ...(ledger.state === 'signed' ? [{ record: ledger.record, proof: ledger.proof }] : [])]) {
    if (item.record.head !== head) continue;
    const prior = known.get(item.proof.id);
    assert(!prior || sameValue(prior, item.proof), 'Guard proof identity is ambiguous');
    known.set(item.proof.id, item.proof);
  }
  const seen = new Set();
  for (const status of (await statusHistory(api, head)).filter(item => sameContext(item.context, STATUS_CONTEXT))) {
    const proof = known.get(status.id);
    assert(proof && proof.app_id === identity.appId && proof.creator.id === identity.id && proof.creator.login === identity.login &&
      status.context === proof.context && status.state === proof.state && status.creator?.id === proof.creator.id && status.creator?.login === proof.creator.login &&
      status.target_url === proof.target_url && status.description === proof.description && !seen.has(status.id), 'Guard status lacks an authenticated ledger record');
    seen.add(status.id);
  }
  assert(seen.size === known.size, 'Recorded guard status history is incomplete');
}
async function verifyMerged(api, repo, snapshot, tree, actual) {
  actual = sha(actual);
  const [pr, commit] = await Promise.all([api.request(`/pulls/${snapshot.pr}`), api.request(`/git/commits/${actual}`)]);
  assert(pr.user?.id === BOT && pr.user?.login === 'github-actions[bot]' && pr.base?.repo?.full_name === api.repository &&
    pr.base?.ref === repo.default_branch && validBranch(pr.head?.ref) && (!pr.head.repo || pr.head.repo.full_name === api.repository), 'Merged PR identity changed');
  assert(pr.number === Number(snapshot.pr) && commit.sha === actual && pr.merged === true && pr.state === 'closed' && pr.head.sha === snapshot.head && pr.merge_commit_sha === actual &&
    commit.tree?.sha === tree && commit.parents?.length === 1 && commit.parents[0].sha === snapshot.base,
  'Native server result could not be verified against the tested tree/parents');
  const main = sha(await ref(api, 'heads/main'));
  const comparison = await api.request(`/compare/${actual}...${main}`);
  assert(comparison.base_commit?.sha === actual && comparison.merge_base_commit?.sha === actual && comparison.behind_by === 0 &&
    Number.isSafeInteger(comparison.ahead_by) && ((main === actual && comparison.status === 'identical' && comparison.ahead_by === 0) ||
    (main !== actual && comparison.status === 'ahead' && comparison.ahead_by > 0)), 'Native merge commit is not verified as an ancestor of current main');
  return actual;
}
async function observeHistory(api, repo) {
  const ledger = await readLedger(api, repo, { history: true });
  if (unresolved(ledger.record) || ['preparing', 'preparing-update'].includes(ledger.state))
    return { ledger, pulls: [], current: null, pending: true };
  const proposal = ledger.proposal;
  const pulls = [];
  if (proposal) {
    const pr = await proposalPr(api, repo, proposal.pr);
    assert(exactProposal(pr, proposal) && pr.state === 'open' && await ref(api, `heads/${proposal.branch}`) === proposal.head, 'Ledger proposal is not current');
    const detail = await api.request(`/pulls/${proposal.pr}`);
    assert(detail.number === proposal.pr && detail.head?.sha === proposal.head && Number.isSafeInteger(detail.commits) && detail.commits >= 1 && detail.commits <= 250, 'Cannot inspect complete PR commits');
    const commits = [];
    for (let page = 1; page <= 3; page++) {
      const part = await api.request(`/pulls/${proposal.pr}/commits?per_page=100&page=${page}`);
      assert(Array.isArray(part) && part.length <= 100, 'Cannot inspect PR commits');
      commits.push(...part.map(item => sha(item?.sha)));
      if (part.length < 100) break;
    }
    assert(commits.length === detail.commits && new Set(commits).size === commits.length && commits.includes(proposal.head), 'Incomplete PR commits');
    pulls.push(pr);
  }
  return { ledger, pulls, current: proposal?.head ?? null, pending: false };
}
export async function reconcile(api, env = process.env) {
  const repo = await metadata(api);
  let ledger = await readLedger(api, repo);
  if (ledger.state === 'signing-aborted') {
    await saveProposal(api, repo, { state: 'idle', proposal: ledger.proposal, lastClosed: ledger.lastClosed, preparing: null });
    ledger = await readLedger(api, repo);
  }
  if (['signing', 'signed'].includes(ledger.state)) {
    if (!activated(env)) return { blocked: true, reason: 'Guard recovery awaits activation' };
    try {
      const record = ledger.record, recoveryEnv = env;
      assert(recoveryEnv.DEPENDENCY_GUARD_APP_SLUG === record.guard.slug, 'Recorded guard App slug changed');
      assert(recoveryEnv.DEPENDENCY_GUARD_APP_CLIENT_ID === record.guard.client_id, 'Recorded guard App client changed');
      const identity = await appIdentity(api, recoveryEnv);
      assert(identity.appId === record.guard.app_id && identity.id === record.guard.creator.id && identity.login === record.guard.creator.login, 'Recorded guard App changed');
      const options = { pr: String(record.pr), 'base-sha': record.base, 'head-sha': record.head, 'merge-sha': record.merge,
        'validation-success': 'true', 'recovery-run': String(record.run), nonce: record.nonce };
      const { snapshot } = await checkedSnapshot(api, options);
      await requiredRule(api, repo, recoveryEnv);
      const evidence = { ...await currentEvidence(api, snapshot, record.run), nonce: record.nonce };
      assert(evidence.runId === record.run && evidence.attempt === record.attempt, 'Recorded validation attempt changed');
      if (ledger.state === 'signing') {
        const matches = (await statusHistory(api, record.head)).filter(status => matchesStatus(status, api, evidence, identity));
        assert(matches.length === 1, 'Signing status publication is unconfirmed');
        const proof = await validatedStatus(api, evidence, identity, matches[0].id);
        ledger = await saveRecord(api, repo, { ...record, state: 'signed' }, proof);
      }
      options['status-id'] = String(ledger.proof.id);
      const result = await merge(api, options, recoveryEnv);
      return result.merged ? { blocked: false, merged: true, main_updated: true, pr_number: record.pr } : { blocked: true, reason: result.hold_reason ?? result.merge_state };
    } catch (error) { return { blocked: true, reason: error.message }; }
  }
  if (ledger.state === 'aborted-before-request') {
    await saveProposal(api, repo, { state: 'idle', proposal: ledger.proposal, lastClosed: ledger.lastClosed, preparing: null });
    ledger = await readLedger(api, repo);
  }
  if (!unresolved(ledger.record)) {
    const synced = await reconcileProposal(api, repo, ledger);
    if (synced.blocked) return synced;
    const observed = await observeHistory(api, repo);
    assert(!observed.pending, 'Proposal reconciliation remains unresolved');
    return { blocked: false, pr_number: observed.ledger.lastCompletion?.record.pr ?? '' };
  }
  const record = ledger.record;
  const pr = await api.request(`/pulls/${record.pr}`);
  owned(pr, api, repo.default_branch);
  assert(repo.id === record.repo && repo.default_branch === 'main' && pr.user?.type === 'Bot' &&
    pr.head.repo.id === repo.id && pr.base.repo.id === repo.id && ledger.proposal && exactProposal(pr, ledger.proposal), 'Merge PR identity changed');
  if (pr.merged === true) {
    const actual = await verifyMerged(api, repo, record, record.tree, pr.merge_commit_sha);
    const { http_status, ...rest } = record;
    await saveRecord(api, repo, { ...rest, state: 'merged' }, null, actual);
    return { blocked: false, merged: true, main_updated: true, pr_number: record.pr };
  }
  if (record.state !== 'failed') return { blocked: true, reason: 'Merge request outcome is unknown; manual reconciliation required' };
  if (!['open', 'closed'].includes(pr.state) || pr.merged !== false || typeof pr.draft !== 'boolean')
    return { blocked: true, reason: 'Rejected PR state is unknown' };
  const main = sha(await ref(api, 'heads/main')), again = await proposalPr(api, repo, record.pr);
  assert(exactProposal(again, ledger.proposal) && again.state === pr.state && again.merged === false && again.draft === pr.draft &&
    await ref(api, 'heads/main') === main, 'Rejected PR changed before reconciliation');
  await saveProposal(api, repo, { state: 'idle', proposal: pr.state === 'closed' ? null : ledger.proposal,
    lastClosed: pr.state === 'closed' ? ledger.proposal : ledger.lastClosed, preparing: null,
    lastFailure: { record, proof: ledger.proof, observed_main: main, closed: pr.state === 'closed' } });
  return { blocked: false, pr_number: record.pr, failure_message: `HTTP ${record.http_status}` };
}
export async function attest(api, options) {
  assert(options['validation-success'] === 'true', 'Trusted CI validation did not succeed');
  try {
    const { snapshot } = await checkedSnapshot(api, options);
    await currentEvidence(api, snapshot);
    return { ready: true, merged: false, merge_state: 'validated', merge_sha: snapshot.merge };
  } catch (error) { return held(error.message, 'configuration-or-validation-required'); }
}
export async function sign(api, options, env = process.env, statusApi = null) {
  assert(options['validation-success'] === 'true', 'Trusted CI validation did not succeed');
  if (!activated(env)) return held('Fixed merge validated; activation awaits GitHub live verification', 'validated-awaiting-activation');
  if (!env.DEPENDENCY_GUARD_APP_TOKEN) return held('Guard App token is unavailable', 'configuration-or-validation-required');
  try {
    assert(env.DEPENDENCY_GUARD_ACTION_SLUG === env.DEPENDENCY_GUARD_APP_SLUG, 'Guard App token identity changed');
    const { repo, snapshot } = await checkedSnapshot(api, options);
    await requiredRule(api, repo, env);
    const evidence = { ...await currentEvidence(api, snapshot), nonce: randomUUID() }, identity = await appIdentity(api, env);
    assert(/^[A-Za-z0-9_.]+$/.test(env.DEPENDENCY_GUARD_APP_CLIENT_ID ?? ''), 'Guard App client identity is not configured');
    const writer = statusApi ?? client(api.repository, env.DEPENDENCY_GUARD_APP_TOKEN);
    const [app, bot] = await Promise.all([writer.global(`/apps/${env.DEPENDENCY_GUARD_APP_SLUG}`), writer.global(`/users/${env.DEPENDENCY_GUARD_APP_SLUG}%5Bbot%5D`)]);
    assert(app?.id === identity.appId && app.client_id === env.DEPENDENCY_GUARD_APP_CLIENT_ID && app.slug === env.DEPENDENCY_GUARD_APP_SLUG &&
      bot?.id === identity.id && bot.login === identity.login && bot.type === 'Bot', 'Guard App token identity changed');
    const ledger = await readLedger(api, repo, { history: true });
    assert(ledger.state === 'idle' && ledger.proposal?.head === snapshot.head && ledger.proposal.pr === Number(snapshot.pr), 'Merge request or proposal is not verified');
    assert(!ledger.failed.some(({ record }) => record.pr === Number(snapshot.pr) && record.base === snapshot.base && record.head === snapshot.head &&
      record.merge === snapshot.merge && record.run === evidence.runId && record.attempt === evidence.attempt), 'Rejected snapshot and CI attempt cannot be resubmitted');
    await verifyRecordedGuardStatuses(api, snapshot.head, ledger, identity);
    const other = await otherChecks(api, [snapshot.head, snapshot.merge], snapshot);
    if (other) return held(other);
    const tested = await api.request(`/git/commits/${snapshot.merge}`);
    assert(tested.sha === snapshot.merge, 'Tested merge identity changed');
    const record = { version: 1, repo: repo.id, pr: Number(snapshot.pr), base: snapshot.base, head: snapshot.head,
      merge: snapshot.merge, tree: sha(tested.tree?.sha), run: evidence.runId, attempt: evidence.attempt, state: 'signing',
      nonce: evidence.nonce, guard: { app_id: identity.appId, client_id: env.DEPENDENCY_GUARD_APP_CLIENT_ID, slug: env.DEPENDENCY_GUARD_APP_SLUG, creator: { id: identity.id, login: identity.login } } };
    await saveRecord(api, repo, record);
    try {
      await checkedSnapshot(api, options);
      await requiredRule(api, repo, env);
      const current = await currentEvidence(api, snapshot);
      assert(current.runId === evidence.runId && current.attempt === evidence.attempt, 'Validation attempt changed');
      const hold = await otherChecks(api, [snapshot.head, snapshot.merge], snapshot);
      if (hold) throw new Error(hold);
      const saved = await readLedger(api, repo);
      assert(sameValue(saved.record, record) && saved.proof === null, 'Signing intent changed');
    } catch (error) {
      try { await saveRecord(api, repo, { ...record, state: 'signing-aborted' }); }
      catch { return held('Signing preflight failed and abort publication is unconfirmed', 'signing-unknown'); }
      return held(error.message, 'signing-aborted');
    }
    let posted;
    try { posted = await writer.request(`/statuses/${snapshot.head}`, 'POST', {
      context: STATUS_CONTEXT, state: 'success', target_url: statusTarget(api, evidence), description: statusDescription(evidence),
    }); } catch { /* 一意なnonceの完全読取でのみ確定する。 */ }
    const matches = (await statusHistory(api, snapshot.head)).filter(status => matchesStatus(status, api, evidence, identity));
    assert(matches.length === 1 && (!posted || posted.id === matches[0].id && matchesStatus(posted, api, evidence, identity)), 'Guard status publication is unconfirmed');
    const proof = await validatedStatus(api, evidence, identity, matches[0].id);
    await saveRecord(api, repo, { ...record, state: 'signed' }, proof);
    return { ready: true, merged: false, merge_state: 'guard-signed', status_id: matches[0].id, nonce: evidence.nonce };
  } catch (error) { return held(error.message, 'configuration-or-validation-required'); }
}
export async function merge(api, options, env = process.env) {
  assert(options['validation-success'] === 'true', 'Trusted CI validation did not succeed');
  if (!activated(env)) return held('Fixed merge validated; activation awaits GitHub live verification', 'validated-awaiting-activation');
  try {
    assert(/^[1-9][0-9]*$/.test(options['status-id'] ?? '') && Number.isSafeInteger(Number(options['status-id'])), 'Guard status ID is missing');
    assert(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(options.nonce ?? ''), 'Guard status nonce is missing');
    let { repo, snapshot } = await checkedSnapshot(api, options);
    await requiredRule(api, repo, env);
    const evidence = { ...await currentEvidence(api, snapshot, options['recovery-run'] ?? process.env.GITHUB_RUN_ID), nonce: options.nonce }, identity = await appIdentity(api, env);
    const proof = await validatedStatus(api, evidence, identity, Number(options['status-id']));
    const ledgerBefore = await readLedger(api, repo, { history: true });
    assert(ledgerBefore.state === 'signed' && ledgerBefore.proposal?.pr === Number(snapshot.pr) && ledgerBefore.proposal.head === snapshot.head &&
      sameValue(ledgerBefore.proof, proof) && ledgerBefore.record.nonce === evidence.nonce && ledgerBefore.record.run === evidence.runId && ledgerBefore.record.attempt === evidence.attempt &&
      ledgerBefore.record.guard.app_id === identity.appId && ledgerBefore.record.guard.client_id === env.DEPENDENCY_GUARD_APP_CLIENT_ID &&
      sameValue(ledgerBefore.record.guard.creator, proof.creator), 'Merge request or proposal is not verified');
    await verifyRecordedGuardStatuses(api, snapshot.head, ledgerBefore, identity);
    const proposalBlobs = await blobsAt(api, snapshot.head);
    assert(proposalBlobs.package_blob === ledgerBefore.proposal.package_blob && proposalBlobs.lock_blob === ledgerBefore.proposal.lock_blob, 'Native proposal blobs changed');
    assert(!ledgerBefore.failed.some(({ record }) => record.pr === Number(snapshot.pr) && record.base === snapshot.base &&
      record.head === snapshot.head && record.merge === snapshot.merge && record.run === evidence.runId && record.attempt === evidence.attempt), 'Rejected snapshot and CI attempt cannot be resubmitted');
    const checkHold = await otherChecks(api, [snapshot.head, snapshot.merge], snapshot, options['recovery-run']);
    if (checkHold) return held(checkHold);
    const tested = await api.request(`/git/commits/${snapshot.merge}`);
    assert(tested.sha === snapshot.merge && tested.tree?.sha === ledgerBefore.record.tree, 'Tested merge identity changed');
    const record = { version: 1, repo: repo.id, pr: Number(snapshot.pr), base: snapshot.base, head: snapshot.head,
      merge: snapshot.merge, tree: sha(tested.tree?.sha), run: evidence.runId, attempt: evidence.attempt, state: 'intent' };
    await saveRecord(api, repo, record, proof);
    try {
      ({ repo, snapshot } = await checkedSnapshot(api, options));
      await requiredRule(api, repo, env);
      const current = await currentEvidence(api, snapshot, options['recovery-run'] ?? process.env.GITHUB_RUN_ID);
      assert(current.runId === evidence.runId && current.attempt === evidence.attempt, 'Validation attempt changed');
      const confirmedProof = await validatedStatus(api, evidence, identity, proof.id);
      assert(sameValue(confirmedProof, proof), 'Guard status changed');
      const afterIntentHold = await otherChecks(api, [snapshot.head, snapshot.merge], snapshot, options['recovery-run']);
      if (afterIntentHold) throw new Error(afterIntentHold);
      const pendingRecord = await readLedger(api, repo);
      assert(sameValue(pendingRecord.record, record) && sameValue(pendingRecord.proof, proof), 'Merge intent record changed');
    } catch (error) {
      try { await saveRecord(api, repo, { ...record, state: 'aborted-before-request' }); }
      catch { return held('Preflight failed and abort publication is unconfirmed; manual reconciliation required', 'intent-unknown'); }
      return held(error.message, 'aborted-before-request');
    }
    const result = await requestMerge(api, snapshot);
    if (result.merge_state === 'failed') {
      const pr = await proposalPr(api, repo, record.pr), main = sha(await ref(api, 'heads/main'));
      assert(exactProposal(pr, ledgerBefore.proposal) && ['open', 'closed'].includes(pr.state) && typeof pr.merged === 'boolean' && sha(main), 'Rejected merge PR identity is unknown');
      if (pr.merged) {
        const actual = await verifyMerged(api, repo, snapshot, record.tree, pr.merge_commit_sha);
        await saveRecord(api, repo, { ...record, state: 'merged' }, null, actual);
        return { merged: true, ready: true, github_pr_merged: true, merge_state: 'merged', merge_sha: actual };
      }
      await saveRecord(api, repo, { ...record, state: 'failed', http_status: result.http_status });
      return result;
    }
    if (result.merge_state !== 'reported-merged') return result;
    const actual = await verifyMerged(api, repo, snapshot, record.tree, result.sha);
    await saveRecord(api, repo, { ...record, state: 'merged' }, null, actual);
    return { merged: true, ready: true, github_pr_merged: true, merge_state: 'merged', merge_sha: actual };
  } catch (error) { return held(error.message, 'held-or-unknown'); }
}
export async function report(api, options, { verifiedMitigations = mitigations } = {}) {
  const result = options.report ? JSON.parse(readFileSync(options.report, 'utf8')) : { status: options.status };
  assert(typeof result.status === 'string', 'Report status required');
  const count = raw => Object.values(raw || {}).reduce((total, entries) => total + (Array.isArray(entries) ? entries.length : 0), 0);
  const validAudit = raw => raw && typeof raw === 'object' && !Array.isArray(raw) && Object.values(raw).every(Array.isArray);
  const mainRaw = result.result_changed === true || result.status === 'error' ? result.rawBefore : result.rawAfter;
  const candidateRaw = result.result_changed === true ? result.rawAfter : null;
  const mainValid = validAudit(mainRaw), candidateValid = validAudit(candidateRaw);
  const mainRemaining = mainValid ? remainingAdvisories(mainRaw, verifiedMitigations()) : null;
  const audit = {
    status: result.status,
    rawAuditSucceeded: !!mainValid && count(mainRaw) === 0,
    mainRawAuditSucceeded: !!mainValid && count(mainRaw) === 0,
    mainRawCount: mainValid ? count(mainRaw) : null,
    candidateRawAuditSucceeded: candidateValid ? count(candidateRaw) === 0 : null,
    candidateRawCount: candidateValid ? count(candidateRaw) : null,
    mainRemainingCount: Array.isArray(mainRemaining) ? mainRemaining.length : null,
    mitigatedCount: Array.isArray(result.mitigations) ? result.mitigations.length : null,
    remainingCount: Array.isArray(result.remaining) ? result.remaining.length : null,
    rawBefore: result.rawBefore, rawAfter: result.rawAfter,
    mitigations: result.mitigations, remaining: result.remaining, error: result.error,
    needs_followup: result.needs_followup, upstreamFixes: result.upstreamFixes,
  };
  if (mainValid && count(mainRaw) > 0)
    process.stdout.write('::warning::Raw bun audit still reports advisories; verified mitigations are separate.\n');
  if (process.env.GITHUB_STEP_SUMMARY) {
    const path = process.env.GITHUB_STEP_SUMMARY, limit = 900 * 1024;
    let used = 0; try { used = statSync(path).size; } catch (error) { if (error.code !== 'ENOENT') throw error; }
    const priority = { status: result.status, error: boundedUtf8(String(result.error ?? ''), 4000),
      nativeStderr: boundedUtf8(String(result.nativeFix?.stderr ?? ''), 4000), mainRawCount: audit.mainRawCount,
      mainRemainingCount: audit.mainRemainingCount, candidateRawCount: audit.candidateRawCount,
      mainRawAuditSucceeded: audit.mainRawAuditSucceeded, candidateRawAuditSucceeded: audit.candidateRawAuditSucceeded };
    const content = `\n### Bun security audit\n\nKnown mitigations do not turn a failing raw audit into a successful raw audit.\n\n\`\`\`json\n${JSON.stringify(priority, null, 2)}\n\`\`\`\n\n${JSON.stringify(audit, null, 2)}\n`;
    const remaining = Math.max(0, limit - used);
    if (remaining >= 32) appendFileSync(path, boundedUtf8(content, remaining));
    else process.stdout.write('::warning::Bun audit summary omitted because the summary size limit was reached.\n');
  }
  const repo = await metadata(api);
  const diagnostics = JSON.stringify({ status: result.status, error: boundedUtf8(String(result.error ?? ''), 4000),
    nativeFix: result.nativeFix && { exitCode: result.nativeFix.exitCode, stderr: boundedUtf8(String(result.nativeFix.stderr ?? ''), 4000) },
    mainRawCount: audit.mainRawCount, mainRemainingCount: audit.mainRemainingCount, candidateRawCount: audit.candidateRawCount,
    needs_followup: result.needs_followup, remaining: result.remaining, upstreamFixes: result.upstreamFixes }, null, 2);
  const prLink = /^\d+$/.test(options.pr || '') ? `\nPR: https://github.com/${api.repository}/pull/${options.pr}\n` : '';
  if (!repo.has_issues) return { issue_reporting: 'disabled' };
  const notificationSignal = AbortSignal.timeout(45000);
  const existing = await findNotification(api, repo, { signal: notificationSignal });
  if (options.report && options['allow-close'] === 'true' && existing) {
    const raw = result.rawAfter;
    const validRaw = raw && typeof raw === 'object' && !Array.isArray(raw) && Object.values(raw).every(Array.isArray);
    const verified = verifiedMitigations();
    const mitigated = result.status === 'mitigated' && validRaw && Array.isArray(result.mitigations) && result.mitigations.length > 0 &&
      Array.isArray(verified) && JSON.stringify(result.mitigations) === JSON.stringify(verified) && remainingAdvisories(raw, verified).length === 0;
    const clean = result.status === 'clean' && validRaw && count(raw) === 0;
    const complete = validRaw && (clean || mitigated) && Array.isArray(result.remaining) && result.remaining.length === 0 &&
      result.needs_followup === false && Array.isArray(result.upstreamFixes) && result.upstreamFixes.length === 0 &&
      result.result_changed === false && Array.isArray(result.changedFiles) && result.changedFiles.length === 0;
    if (complete) {
      const observed = await observeHistory(api, repo);
      const main = sha(await ref(api, 'heads/main'));
      if (!observed.pending && observed.pulls.length === 0 && main === sha(process.env.GITHUB_SHA)) {
        const current = await readLedger(api, repo);
        assert(!unresolved(current.record) && current.id === observed.ledger.id, 'Ledger changed before notification closure');
        const lastMain = sha(await ref(api, 'heads/main'));
        const finalLedger = await readLedger(api, repo);
        assert(lastMain === main && finalLedger.id === current.id && finalLedger.state === 'idle' && !finalLedger.proposal && !unresolved(finalLedger.record), 'Main or ledger changed before notification closure');
        const closed = await api.request(`/issues/${existing.number}`, 'PATCH', { state: 'closed' });
        assert(closed?.number === existing.number && closed.state === 'closed', 'Notification closure unconfirmed');
        const [verifiedIssue, verifiedMain] = await Promise.all([api.request(`/issues/${existing.number}`), ref(api, 'heads/main')]);
        assert(verifiedIssue?.number === existing.number && verifiedIssue.state === 'closed' && verifiedIssue.user?.id === BOT && verifiedIssue.user?.login === 'github-actions[bot]' &&
          typeof verifiedIssue.body === 'string' && verifiedIssue.body.startsWith(marker) && verifiedMain === main, 'Notification or main changed after closure');
        return { issue_reporting: 'clear' };
      }
    }
  }
  const problem = (options.report && (mainRemaining === null || mainRemaining.length > 0)) || result.needs_followup === true || result.upstreamFixes?.length > 0 || result.remaining?.length || ['unfixable', 'blocked', 'error', 'validation-failed', 'prepare-failed', 'merge-failed', 'validated-awaiting-configuration', 'native-merge-unresolved'].includes(result.status);
  if (!problem && !existing) return { issue_reporting: 'clear' };
  const issue = existing ?? await api.request('/issues', 'POST', { title: 'Bun: 自動修正の確認', body: `${marker}\nBun security automation notification.` });
  assert(Number.isSafeInteger(issue?.number), 'Notification creation unconfirmed');
  await reportComment(api, repo, issue, `Bun security automation: ${result.status}\n${diagnostics}${prLink}\n${String(options.details || '')}`, { signal: notificationSignal });
  return { issue_number: issue.number };
}
async function main() {
  const [command, ...args] = process.argv.slice(2);
  const options = {};
  for (let index = 0; index < args.length; index += 2) {
    assert(/^--[a-z-]+$/.test(args[index]), 'Invalid option');
    options[args[index].slice(2)] = args[index + 1];
  }
  output(await ({ prepare, attest, sign, merge, report, reconcile })[command](client(), options));
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});
