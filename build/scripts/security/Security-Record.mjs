const BOT = 41898282, OWNER = 16895591;
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const sha = value => typeof value === 'string' && /^[0-9a-f]{40}$/.test(value);
const uuid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value);
const branch = value => typeof value === 'string' && /^automation\/bun-security-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value);
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
export const unresolved = record => !!record && record.state !== 'merged';
const prefix = '<!-- bun-security-ledger-event-v1:';
const genesis = (repo, issue) => `Bunの脆弱性自動修正の処理状態を保持するIssueです。自動処理のため開いたままにしてください。\n\n<!-- bun-security-ledger-v1:${JSON.stringify({ version: 1, repo, issue, state: 'idle' })} -->`;
export const ledgerGenesis = genesis;
const encoded = value => JSON.stringify(value).replaceAll('<', '\\u003c').replaceAll('>', '\\u003e').replaceAll('&', '\\u0026');
function validateRecord(record, repo) {
  const keys = Object.keys(record ?? {}).sort().join(',');
  const signing = ['signing', 'signed', 'signing-aborted'].includes(record?.state);
  assert((signing ? keys === 'attempt,base,guard,head,merge,nonce,pr,repo,run,state,tree,version' :
    ['attempt,base,head,merge,pr,repo,run,state,tree,version', 'attempt,base,head,http_status,merge,pr,repo,run,state,tree,version'].includes(keys)) &&
    record.version === 1 && record.repo === repo && Number.isSafeInteger(record.pr) && record.pr > 0 &&
    Number.isSafeInteger(record.run) && record.run > 0 && Number.isSafeInteger(record.attempt) && record.attempt > 0 &&
    [record.base, record.head, record.merge, record.tree].every(sha) && ['signing', 'signed', 'signing-aborted', 'intent', 'aborted-before-request', 'failed', 'merged'].includes(record.state) &&
    (!signing || (uuid(record.nonce) && Object.keys(record.guard ?? {}).sort().join(',') === 'app_id,client_id,creator,slug' &&
      Number.isSafeInteger(record.guard.app_id) && record.guard.app_id > 0 && /^[a-z0-9-]+$/.test(record.guard.slug ?? '') &&
      /^[A-Za-z0-9_.]+$/.test(record.guard.client_id ?? '') &&
      Object.keys(record.guard.creator ?? {}).sort().join(',') === 'id,login' && Number.isSafeInteger(record.guard.creator?.id) && record.guard.creator.id > 0 && record.guard.creator.login === `${record.guard.slug}[bot]`)) &&
    (record.state === 'failed' ? [405, 409, 422].includes(record.http_status) : record.http_status === undefined), 'Invalid ledger record');
}
function actor(user, owner = false) {
  assert(user && Number.isSafeInteger(user.id) && typeof user.login === 'string' && user.login.length > 0, 'Ledger actor unavailable');
  assert((user.id === BOT) === (user.login === 'github-actions[bot]'), 'Ledger actor identity inconsistent');
  return user.id === BOT || (owner && user.id === OWNER);
}
function date(value) { return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(value) && Number.isFinite(Date.parse(value)) && new Date(value).toISOString() === value.replace('Z', '.000Z'); }
function decode(comment, repo, issue, repository) {
  assert(comment && Number.isSafeInteger(comment.id) && comment.id > 0 && typeof comment.body === 'string' && comment.body.trim() !== '', 'Ledger comment unavailable');
  const marked = comment.body.includes('<!-- bun-security-ledger-');
  const bot = actor(comment.user);
  if (!marked || !bot) return null;
  assert(date(comment.created_at) && date(comment.updated_at) && comment.created_at === comment.updated_at, 'Invalid ledger event author or timestamp');
  const match = /^<!-- bun-security-ledger-event-v1:([\s\S]*?) -->$/.exec(comment.body);
  assert(match, 'Invalid ledger event');
  let event; try { event = JSON.parse(match[1]); } catch { throw new Error('Malformed ledger event'); }
  assert(event && Object.keys(event).sort().join(',') === 'issue,last_closed,last_completion,last_failure,preparing,prev,proof,proposal,record,repo,state,version' &&
    event.version === 1 && event.repo === repo && event.issue === issue && Number.isSafeInteger(event.prev) && event.prev >= 0 && event.prev < comment.id &&
    ['idle', 'preparing', 'preparing-update', 'signing', 'signed', 'signing-aborted', 'intent', 'aborted-before-request', 'failed'].includes(event.state), 'Invalid ledger event identity');
  function validateProof(record, proof) {
    assert(proof && Object.keys(proof).sort().join(',') === 'app_id,context,creator,description,id,nonce,state,target_url' && Number.isSafeInteger(proof.id) && proof.id > 0 && uuid(proof.nonce) &&
      Number.isSafeInteger(proof.app_id) && proof.app_id > 0 && Number.isSafeInteger(proof.creator?.id) && proof.creator.id > 0 && /^[a-z0-9-]+\[bot\]$/.test(proof.creator?.login ?? '') &&
      proof.context === 'kagayoi/automation-merge-guard-v1' && proof.state === 'success' &&
      proof.target_url === `https://github.com/${repository}/actions/runs/${record.run}/attempts/${record.attempt}` &&
      proof.description === `PR ${record.pr} H ${record.head}; run ${record.run} a ${record.attempt}; ${proof.nonce}`, 'Invalid ledger proof');
  }
  const validProposal = item => item && Object.keys(item).sort().join(',') === 'base,branch,head,lock_blob,package_blob,pr' &&
    Number.isSafeInteger(item.pr) && item.pr > 0 && branch(item.branch) && [item.base, item.head, item.package_blob, item.lock_blob].every(sha);
  assert((event.proposal === null || validProposal(event.proposal)) && (event.last_closed === null || validProposal(event.last_closed)), 'Invalid proposal snapshot');
  if (event.last_completion !== null) {
    const completed = event.last_completion;
    assert(completed && Object.keys(completed).sort().join(',') === 'actual,proof,proposal,record' && sha(completed.actual), 'Invalid last completion');
    validateRecord(completed.record, repo);
    assert(completed.record.state === 'merged' && validProposal(completed.proposal) && completed.proposal.pr === completed.record.pr && completed.proposal.head === completed.record.head, 'Invalid completed record');
    validateProof(completed.record, completed.proof);
  }
  if (event.last_failure !== null) {
    const failed = event.last_failure;
    assert(failed && Object.keys(failed).sort().join(',') === 'closed,observed_main,proof,record' && typeof failed.closed === 'boolean' && sha(failed.observed_main), 'Invalid last failure');
    validateRecord(failed.record, repo);
    assert(failed.record.state === 'failed', 'Invalid recovered failure');
    validateProof(failed.record, failed.proof);
  }
  if (event.state === 'preparing') {
    const prep = event.preparing;
    assert(prep && Object.keys(prep).sort().join(',') === 'base,branch,head,lock_blob,package_blob,pr' && prep.pr === null && branch(prep.branch) &&
      [prep.base, prep.head, prep.package_blob, prep.lock_blob].every(sha) &&
      event.record === null && event.proof === null, 'Invalid preparation snapshot');
  } else if (event.state === 'preparing-update') {
    const p = event.preparing;
    assert(p && Object.keys(p).sort().join(',') === 'base,branch,head,lock_blob,old_head,old_lock_blob,old_package_blob,package_blob,pr,tree' &&
      Number.isSafeInteger(p.pr) && p.pr > 0 && branch(p.branch) && [p.base, p.head, p.old_head, p.tree, p.package_blob, p.lock_blob, p.old_package_blob, p.old_lock_blob].every(sha) &&
      event.proposal && p.pr === event.proposal.pr && p.branch === event.proposal.branch && p.old_head === event.proposal.head &&
      p.old_package_blob === event.proposal.package_blob && p.old_lock_blob === event.proposal.lock_blob && event.record === null && event.proof === null, 'Invalid update preparation');
  } else if (event.state === 'idle') {
    assert(event.record === null && event.proof === null && event.preparing === null, 'Invalid idle ledger event');
  } else {
    validateRecord(event.record, repo);
    assert(event.preparing === null && event.record.state === event.state, 'Invalid active ledger event');
    if (['signing', 'signing-aborted'].includes(event.state)) assert(event.proof === null, 'Unexpected signing proof');
    else {
      validateProof(event.record, event.proof);
      if (event.state === 'signed') assert(event.proof.nonce === event.record.nonce && event.proof.app_id === event.record.guard.app_id &&
        same(event.proof.creator, event.record.guard.creator), 'Signing proof identity changed');
    }
  }
  return { id: comment.id, event };
}
async function commentPages(api, repo, issue, signal) {
  const cache = new Map(); let finalizedEnd = null;
  function verify(item, n) {
    if (finalizedEnd === null) return item;
    assert(n <= finalizedEnd && (item.links.last === undefined || item.links.last === finalizedEnd), 'Comment last page changed');
    if (n < finalizedEnd) assert(item.links.next === n + 1 && item.comments.length === 100, 'Comment page chain is incomplete');
    else assert(item.links.next === undefined, 'Comment final page changed');
    return item;
  }
  async function page(n, probe = false) {
    if (cache.has(n)) return verify(cache.get(n), n);
    assert(cache.size < 50 && Number.isSafeInteger(n) && n >= 1, 'Comment pagination limit exceeded');
    signal.throwIfAborted();
    const response = await api.response(`/issues/${issue}/comments?per_page=100&page=${n}`, 'GET', undefined, { signal });
    signal.throwIfAborted();
    assert(response?.status === 200 && Array.isArray(response.data) && response.data.length <= 100, 'Cannot inspect comments');
    checkRemaining(response);
    const links = linkPages(response.headers?.link, api.repository, repo.id, issue, n, probe && response.data.length === 0);
    const result = { comments: response.data, links }; cache.set(n, result); return verify(result, n);
  }
  const first = await page(1);
  let end = first.links.last ?? 1;
  let current = end === 1 ? first : await page(end);
  assert(end === 1 || current.comments.length > 0 || current.links.next !== undefined, 'Declared last comment page is empty');
  while (current.links.next !== undefined) { end = current.links.next; current = await page(end); }
  assert(end === 1 || current.comments.length > 0, 'Declared last comment page is empty');
  if (current.comments.length === 100) {
    const extra = await page(end + 1, true);
    assert(extra.comments.length === 0 && extra.links.next === undefined &&
      (extra.links.last === undefined || extra.links.last === end), 'Comment pagination is incomplete');
  }
  finalizedEnd = end;
  verify(first, 1);
  return { page, end };
}
function checkRemaining(response) {
  const remaining = response.headers?.remaining;
  if (remaining !== null && remaining !== undefined) assert(/^[0-9]+$/.test(String(remaining)) && Number(remaining) >= 200, 'GitHub API budget is low');
}
async function lastReports(api, repo, issue, signal, limit = 1) {
  const pager = await commentPages(api, repo, issue, signal), found = [], ids = new Set();
  for (let n = pager.end; n >= 1 && found.length < limit; n--) {
    const { comments } = await pager.page(n); let priorId = Infinity;
    for (let i = comments.length - 1; i >= 0; i--) {
      const comment = comments[i];
      assert(comment && Number.isSafeInteger(comment.id) && comment.id > 0 && comment.id < priorId && !ids.has(comment.id) &&
        typeof comment.body === 'string' && comment.body.trim() !== '', 'Notification comment unavailable');
      ids.add(comment.id); priorId = comment.id;
      const bot = actor(comment.user);
      if (!bot || !comment.body.includes('<!-- bun-security-report-')) continue;
      assert(date(comment.created_at) && date(comment.updated_at) && comment.created_at === comment.updated_at &&
        /^<!-- bun-security-report-v1:[0-9a-f]{64} -->\n/.test(comment.body), 'Invalid notification report');
      found.push(comment); if (found.length === limit) break;
    }
  }
  return found;
}
export async function reportComment(api, repo, issue, content, { signal = AbortSignal.timeout(45000) } = {}) {
  const { createHash } = await import('node:crypto');
  const digest = createHash('sha256').update(content).digest('hex');
  const tag = `<!-- bun-security-report-v1:${digest} -->`;
  const prior = await lastReports(api, repo, issue.number, signal);
  if (prior[0]?.body.startsWith(tag)) return;
  let end = 5988;
  if (content.length > 6000 && /[\uD800-\uDBFF]/.test(content[end - 1]) && /[\uDC00-\uDFFF]/.test(content[end])) end--;
  const excerpt = content.length > 6000 ? `${content.slice(0, end)}\n[省略]` : content;
  const safe = Array.from(excerpt, c => `&#${c.codePointAt(0)};`).join('');
  const body = `${tag}\n${safe}`;
  signal.throwIfAborted();
  try { await api.request(`/issues/${issue.number}/comments`, 'POST', { body }, false, { signal }); } catch { /* verify below */ }
  const after = await lastReports(api, repo, issue.number, signal, prior.length ? 2 : 1);
  assert(after[0]?.body === body && after[0].id !== prior[0]?.id && (!prior.length || after[1]?.id === prior[0].id), 'Report publication unconfirmed');
}
export async function findNotification(api, repo, { signal = AbortSignal.timeout(45000) } = {}) {
  const matches = [], seen = new Set(); let probingEmpty = false, declaredLast = null;
  let next = '/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=1';
  const firstPath = next; let firstPage = null;
  for (let page = 1; page <= 50; page++) {
    signal.throwIfAborted();
    const response = await api.response(next, 'GET', undefined, { signal });
    signal.throwIfAborted();
    assert(response?.status === 200 && Array.isArray(response.data) && response.data.length <= 100, 'Cannot inspect notification issues');
    checkRemaining(response);
    if (page === 1) firstPage = JSON.stringify({ data: response.data, link: response.headers?.link ?? null });
    const linked = issueLinks(response.headers?.link, api.repository, repo.id, page);
    assert(linked.last === null || declaredLast === null || linked.last === declaredLast, 'Notification last page changed');
    if (linked.last !== null) declaredLast = linked.last;
    assert(declaredLast === null || page <= declaredLast || (probingEmpty && page === declaredLast + 1 && response.data.length === 0), 'Notification last page changed');
    assert(declaredLast === null || page >= declaredLast || linked.next, 'Notification page chain is incomplete');
    assert(linked.last === null || (linked.last < page ? probingEmpty && response.data.length === 0 && linked.last === page - 1 && !linked.next :
      linked.last === page ? !linked.next : !!linked.next), 'Incomplete notification pagination');
    assert(!linked.next || response.data.length === 100, 'Incomplete notification page');
    for (const issue of response.data) {
      assert(Number.isSafeInteger(issue?.number) && issue.number > 0 && !seen.has(issue.number), 'Duplicate or invalid notification issue');
      seen.add(issue.number);
      if (issue.pull_request) continue;
      assert(issue && (issue.body === null || typeof issue.body === 'string'), 'Notification issue body unavailable');
      if (issue.body === null) continue;
      if (!issue.body.startsWith('<!-- bun-security-automation -->')) continue;
      assert(Number.isSafeInteger(issue.number) && issue.number > 0 && issue.state === 'open' && actor(issue.user), 'Invalid notification issue');
      matches.push(issue);
      assert(matches.length <= 1, 'Multiple open notification issues');
    }
    if (linked.next) { next = linked.next; probingEmpty = false; continue; }
    if (response.data.length < 100) {
      if (page > 1) {
        signal.throwIfAborted();
        const first = await api.response(firstPath, 'GET', undefined, { signal });
        signal.throwIfAborted();
        assert(first?.status === 200 && Array.isArray(first.data), 'Cannot recheck notification issues');
        checkRemaining(first);
        assert(JSON.stringify({ data: first.data, link: first.headers?.link ?? null }) === firstPage, 'Notification first page changed');
      }
      return matches[0] ?? null;
    }
    probingEmpty = true;
    next = `/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=${page + 1}`;
  }
  throw new Error('Notification issue pagination limit exceeded');
}
function issueLinks(link, repository, repoId, current) {
  if (link === null || link === undefined || link === '') return { next: null, last: null };
  assert(typeof link === 'string', 'Invalid notification pagination');
  const seen = new Set(); let next = null, last = null;
  for (const part of link.split(',')) {
    const match = /^\s*<([^>]+)>;\s*rel="(next|last|first|prev)"\s*$/.exec(part);
    assert(match && !seen.has(match[2]), 'Invalid notification pagination link');
    seen.add(match[2]);
    let url; try { url = new URL(match[1]); } catch { throw new Error('Invalid notification pagination URL'); }
    const keys = [...url.searchParams.keys()];
    assert(url.protocol === 'https:' && url.host === 'api.github.com' && !url.username && !url.password && !url.hash &&
      [`/repositories/${repoId}/issues`, `/repos/${repository}/issues`].includes(url.pathname) &&
      new Set(keys).size === keys.length && keys.every(key => ['creator', 'page', 'per_page', 'state', 'after', 'before'].includes(key)) &&
      url.searchParams.get('per_page') === '100' && url.searchParams.get('state') === 'open' &&
      url.searchParams.get('creator') === 'github-actions[bot]', 'Invalid notification pagination target');
    for (const key of ['after', 'before']) if (url.searchParams.has(key)) { const value = url.searchParams.get(key); assert(value.length > 0 && value.length <= 2048 && !/[\u0000-\u001f\u007f]/u.test(value), 'Invalid notification cursor'); }
    const raw = url.searchParams.get('page');
    assert(/^[1-9][0-9]*$/.test(raw ?? '') && Number.isSafeInteger(Number(raw)) &&
      (match[2] !== 'next' || Number(raw) === current + 1), 'Invalid notification pagination page');
    if (match[2] === 'next') next = `/issues?${url.searchParams.toString()}`;
    if (match[2] === 'last') last = Number(raw);
    if (match[2] === 'prev') assert(current > 1 && Number(raw) === current - 1, 'Invalid notification previous page');
    if (match[2] === 'first') assert(Number(raw) === 1, 'Invalid notification first page');
  }
  return { next, last };
}
function linkPages(link, repository, repoId, issue, current, emptyProbe = false) {
  if (link === null || link === undefined || link === '') return {};
  assert(typeof link === 'string', 'Invalid ledger pagination');
  const found = {};
  for (const part of link.split(',')) {
    const match = /^\s*<([^>]+)>;\s*rel="(next|last|first|prev)"\s*$/.exec(part);
    assert(match && found[match[2]] === undefined, 'Invalid ledger pagination link');
    let url; try { url = new URL(match[1]); } catch { throw new Error('Invalid ledger pagination URL'); }
    assert(url.protocol === 'https:' && url.host === 'api.github.com' && !url.username && !url.password && !url.hash &&
      [`/repositories/${repoId}/issues/${issue}/comments`, `/repos/${repository}/issues/${issue}/comments`].includes(url.pathname) &&
      [...url.searchParams.keys()].sort().join(',') === 'page,per_page' && url.searchParams.get('per_page') === '100', 'Invalid ledger pagination target');
    const raw = url.searchParams.get('page');
    assert(/^[1-9][0-9]*$/.test(raw ?? '') && Number.isSafeInteger(Number(raw)), 'Invalid ledger pagination page');
    found[match[2]] = Number(raw);
  }
  assert(found.next === undefined || found.next === current + 1, 'Invalid ledger next page');
  assert(found.prev === undefined || (current > 1 && found.prev === current - 1), 'Invalid ledger previous page');
  assert(found.first === undefined || found.first === 1, 'Invalid ledger first page');
  assert(found.last === undefined || (found.last >= current && (found.next === undefined || found.last >= found.next)) ||
    (emptyProbe && found.last === current - 1 && found.next === undefined), 'Invalid ledger last page');
  return found;
}
function transition(previous, current) {
  const a = previous?.event, b = current.event;
  assert(b.prev === (previous?.id ?? 0), 'Ledger predecessor missing or changed');
  const old = a ?? { state: 'idle', proposal: null, last_closed: null, last_completion: null, last_failure: null };
  if (!(old.state === 'failed' && b.state === 'idle' && b.record === null)) assert(same(old.last_failure, b.last_failure), 'Ledger failure changed');
  if (old.state === 'idle') {
    assert(same(old.last_completion, b.last_completion), 'Ledger completion changed');
    if (b.state === 'signing') {
      assert(old.proposal && old.proposal.pr === b.record.pr && old.proposal.head === b.record.head && same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed), 'Invalid intent transition'); return;
    }
    if (b.state === 'preparing') {
      assert(same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed), 'Invalid prepare transition'); return;
    }
    if (b.state === 'preparing-update') {
      assert(old.proposal && same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed), 'Invalid update preparation transition'); return;
    }
    const closed = old.proposal && b.proposal === null && same(b.last_closed, old.proposal);
    const reopened = old.last_closed && same(b.proposal, old.last_closed) && b.last_closed === null;
    const resolved = old.proposal && b.proposal === null && same(b.last_closed, old.last_closed);
    const switched = b.proposal && (!old.proposal || !same(b.proposal, old.proposal)) && b.last_closed === null;
    assert(b.state === 'idle' && (closed || reopened || resolved || switched), 'Invalid idle transition'); return;
  }
  if (old.state === 'preparing') {
    const p = old.preparing;
    assert(b.state === 'idle' && b.proposal && ['branch', 'base', 'head', 'package_blob', 'lock_blob'].every(key => b.proposal[key] === p[key]) &&
      b.last_closed === null && same(b.last_completion, old.last_completion), 'Invalid prepared proposal'); return;
  }
  if (old.state === 'preparing-update') {
    const p = old.preparing;
    assert(b.state === 'idle' && b.proposal && ['pr', 'branch', 'base', 'head', 'package_blob', 'lock_blob'].every(key => b.proposal[key] === p[key]) &&
      same(b.last_closed, old.last_closed) && same(b.last_completion, old.last_completion), 'Invalid updated proposal'); return;
  }
  if (old.state === 'signing-aborted') {
    assert(b.state === 'idle' && same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed) && same(old.last_completion, b.last_completion), 'Invalid signing abort reconciliation'); return;
  }
  if (old.state === 'signing') {
    const { state: _oldState, ...oldSnapshot } = old.record;
    const { state: _newState, ...newSnapshot } = b.record ?? {};
    assert(['signed', 'signing-aborted'].includes(b.state) && same(oldSnapshot, newSnapshot) &&
      same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed) && same(old.last_completion, b.last_completion), 'Invalid signing transition'); return;
  }
  if (old.state === 'signed') {
    const { state: _state, guard: _guard, nonce: _nonce, ...oldSnapshot } = old.record;
    const { state: _newState, ...newSnapshot } = b.record ?? {};
    assert(b.state === 'intent' && same(oldSnapshot, newSnapshot) && same(old.proof, b.proof) &&
      same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed) && same(old.last_completion, b.last_completion), 'Invalid signed transition'); return;
  }
  if (old.state === 'aborted-before-request') {
    assert(b.state === 'idle' && same(old.proposal, b.proposal) && same(old.last_closed, b.last_closed) && same(old.last_completion, b.last_completion), 'Invalid aborted reconciliation'); return;
  }
  if (old.state === 'failed' && b.state === 'idle' && b.record === null) {
    assert(b.last_failure && same(b.last_failure.record, old.record) && same(b.last_failure.proof, old.proof) &&
      (b.last_failure.closed ? b.proposal === null && same(b.last_closed, old.proposal) : same(b.proposal, old.proposal) && same(old.last_closed, b.last_closed)) &&
      same(old.last_completion, b.last_completion), 'Invalid failed reconciliation'); return;
  }
  const completion = b.state === 'idle' ? b.last_completion : null;
  const target = completion?.record ?? b.record, proof = completion?.proof ?? b.proof;
  const fields = ['version', 'repo', 'pr', 'base', 'head', 'merge', 'tree', 'run', 'attempt'];
  assert(target && fields.every(key => old.record[key] === target[key]) && same(old.proof, proof), 'Ledger snapshot changed');
  assert(({ intent: ['aborted-before-request', 'failed', 'idle'], failed: ['idle'] })[old.state]?.includes(b.state), 'Invalid ledger transition');
  assert(same(old.last_closed, b.last_closed) && (b.state === 'idle' ? b.proposal === null && same(completion.proposal, old.proposal) : same(old.proposal, b.proposal)), 'Ledger proposal changed during native request');
  if (b.state !== 'idle') assert(same(old.last_completion, b.last_completion), 'Ledger completion changed during active request');
}
export async function readLedger(api, repo, { signal = AbortSignal.timeout(45000), history = false } = {}) {
  const value = process.env.BUN_SECURITY_LEDGER_ISSUE;
  assert(/^[1-9][0-9]*$/.test(value ?? '') && Number.isSafeInteger(Number(value)) && Number.isSafeInteger(repo.id), 'Ledger issue is not configured');
  const number = Number(value), options = { signal };
  signal.throwIfAborted();
  const issue = await api.request(`/issues/${number}`, 'GET', undefined, false, options);
  signal.throwIfAborted();
  assert(issue?.number === number && issue.state === 'open' && !issue.pull_request && actor(issue.user, true) &&
    issue.repository_url === `https://api.github.com/repos/${api.repository}` && issue.body === genesis(repo.id, number), 'Ledger issue or genesis invalid');
  const pager = await commentPages(api, repo, number, signal);
  const page = pager.page, end = pager.end;
  const found = [], ids = new Set();
  for (let n = end; n >= 1 && (history || found.length < 2) && found.at(-1)?.event.prev !== 0; n--) {
    const item = await page(n); let priorId = Infinity;
    for (let i = item.comments.length - 1; i >= 0; i--) {
      const comment = item.comments[i];
      assert(Number.isSafeInteger(comment?.id) && comment.id > 0 && comment.id < priorId && !ids.has(comment.id), 'Ledger comment order is invalid');
      ids.add(comment.id); priorId = comment.id;
      const event = decode(comment, repo.id, number, api.repository);
      if (event) { found.push(event); if ((!history && found.length === 2) || event.event.prev === 0) break; }
    }
  }
  if (history) {
    for (let i = found.length - 1; i >= 0; i--) {
      transition(found[i + 1] ?? null, found[i]);
      const prior = found[i + 1]?.event, current = found[i].event;
      if (prior?.state === 'idle' && current.state === 'idle' && current.proposal && !same(prior.last_closed, current.proposal))
        assert(found.slice(i + 2).some(item => item.event.state === 'idle' && same(item.event.proposal, current.proposal)), 'Reused proposal lacks ledger history');
    }
  } else if (found[0]) transition(found[1] ?? null, found[0]);
  return { issue, id: found[0]?.id ?? 0, event: found[0]?.event ?? null,
    record: found[0]?.event.record ?? null, proof: found[0]?.event.proof ?? null, lastCompletion: found[0]?.event.last_completion ?? null, lastFailure: found[0]?.event.last_failure ?? null,
    state: found[0]?.event.state ?? 'idle', proposal: found[0]?.event.proposal ?? null, lastClosed: found[0]?.event.last_closed ?? null, preparing: found[0]?.event.preparing ?? null,
    proposals: history ? found.filter(item => item.event.state === 'idle' && item.event.proposal).map(item => item.event.proposal) : undefined,
    updates: history ? found.filter(item => item.event.state === 'preparing-update').map(item => item.event.preparing) : undefined,
    failed: history ? found.filter(item => item.event.state === 'failed').map(item => ({ record: item.event.record, proof: item.event.proof })) : undefined,
    aborted: history ? found.filter(item => item.event.state === 'aborted-before-request').map(item => ({ record: item.event.record, proof: item.event.proof })) : undefined };
}
export async function saveRecord(api, repo, record, intent = null, actual = null) {
  validateRecord(record, repo.id);
  const signal = AbortSignal.timeout(45000);
  const before = await readLedger(api, repo, { signal });
  const proof = intent ?? before.proof;
  const event = { version: 1, repo: repo.id, issue: before.issue.number, prev: before.id,
    state: record.state === 'merged' ? 'idle' : record.state, record: record.state === 'merged' ? null : record,
    proof: record.state === 'merged' ? null : proof,
    last_completion: record.state === 'merged' ? { record, proof, actual, proposal: before.proposal } : before.lastCompletion, last_failure: before.lastFailure,
    proposal: record.state === 'merged' ? null : before.proposal, last_closed: before.lastClosed, preparing: null };
  return appendLedger(api, repo, before, event, signal);
}
export async function saveProposal(api, repo, { state, proposal, lastClosed, preparing, lastFailure }) {
  const signal = AbortSignal.timeout(45000);
  const before = await readLedger(api, repo, { signal });
  const event = { version: 1, repo: repo.id, issue: before.issue.number, prev: before.id, state,
    record: null, proof: null, last_completion: before.lastCompletion, last_failure: lastFailure ?? before.lastFailure, proposal, last_closed: lastClosed, preparing };
  return appendLedger(api, repo, before, event, signal);
}
async function appendLedger(api, repo, before, event, signal) {
  const body = `${prefix}${encoded(event)} -->`;
  const stamp = '2026-01-01T00:00:00Z';
  const candidate = decode({ id: Number.MAX_SAFE_INTEGER, body, user: { id: BOT, login: 'github-actions[bot]' }, created_at: stamp, updated_at: stamp }, repo.id, before.issue.number, api.repository);
  transition(before.event ? { id: before.id, event: before.event } : null, candidate);
  assert(Buffer.byteLength(body, 'utf8') <= 64000, 'Ledger event too large');
  // Re-read immediately before append; a missing or conflicting update is never retried.
  const confirmed = await readLedger(api, repo, { signal });
  assert(confirmed.id === before.id && same(confirmed.event, before.event), 'Ledger changed before append');
  try { await api.request(`/issues/${before.issue.number}/comments`, 'POST', { body }, false, { signal }); } catch { /* read-back decides */ }
  const after = await readLedger(api, repo, { signal });
  assert(after.id !== before.id && same(after.event, event), 'Ledger event publication unconfirmed');
  return after;
}
