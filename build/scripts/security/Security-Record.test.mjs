import { test, expect } from 'bun:test';
import { createHash } from 'node:crypto';
import { ledgerGenesis, readLedger, saveRecord, saveProposal, findNotification, reportComment } from './Security-Record.mjs';
const sha = char => char.repeat(40), bot = { id: 41898282, login: 'github-actions[bot]' };
const repo = { id: 12 }, repository = 'owner/repo', issueNumber = 8;
const nonce = '630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
const proof = { id: 33, app_id: 123, creator: { id: 99, login: 'guard[bot]' }, nonce, context: 'kagayoi/automation-merge-guard-v1', state: 'success', target_url: 'https://github.com/owner/repo/actions/runs/9/attempts/1', description: `PR 7 H ${sha('2')}; run 9 a 1; ${nonce}` };
const intent = { version: 1, repo: 12, pr: 7, base: sha('1'), head: sha('2'), merge: sha('3'), tree: sha('4'), run: 9, attempt: 1, state: 'intent' };
const branch = 'automation/bun-security-630b9d5e-3f2a-4f7e-8b0c-2d5f9a8c1e42';
function fixture() {
  const f = { comments: [], calls: [], issue: { number: 8, state: 'open', repository_url: 'https://api.github.com/repos/owner/repo', user: bot, body: ledgerGenesis(12, 8) } };
  f.api = { repository, async request(path, method = 'GET', data) {
    f.calls.push([method, path]);
    if (path === '/issues/8' && method === 'GET') return f.issue;
    if (path === '/issues/8/comments' && method === 'POST') {
      f.comments.push({ id: 100 + f.comments.length, body: data.body, user: bot, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z' });
      if (f.lostResponse) throw Error('lost response');
      return f.comments.at(-1);
    }
    throw Error(`unexpected ${method} ${path}`);
  }, async response(path) {
    f.calls.push(['GET', path]);
    const page = Number(new URL('https://example.invalid' + path).searchParams.get('page'));
    const comments = f.pages?.get(page) ?? (page === 1 ? f.comments : []);
    return { status: 200, data: comments, headers: { link: f.links?.get(page) ?? null, remaining: '300' } };
  } };
  return f;
}
async function seed(f) {
  const candidate = { package_blob: sha('6'), lock_blob: sha('7') };
  await saveProposal(f.api, repo, { state: 'preparing', proposal: null, lastClosed: null,
    preparing: { base: intent.base, branch, pr: null, head: intent.head, ...candidate } });
  await saveProposal(f.api, repo, { state: 'idle', proposal: { pr: 7, branch, base: intent.base, head: intent.head, ...candidate }, lastClosed: null, preparing: null });
}
async function saveIntent(api, record, status) {
  const signing = { ...record, state: 'signing', nonce: status.nonce, guard: { app_id: status.app_id, client_id: 'Iv1.ab1112223334445c', slug: status.creator.login.slice(0, -5), creator: status.creator } };
  await saveRecord(api, repo, signing);
  await saveRecord(api, repo, { ...signing, state: 'signed' }, status);
  return saveRecord(api, repo, record, status);
}
async function withLedger(fn) {
  const prior = process.env.BUN_SECURITY_LEDGER_ISSUE; process.env.BUN_SECURITY_LEDGER_ISSUE = '8';
  try { await fn(); } finally { if (prior === undefined) delete process.env.BUN_SECURITY_LEDGER_ISSUE; else process.env.BUN_SECURITY_LEDGER_ISSUE = prior; }
}
test('genesis・未設定・閉鎖・作者・リポジトリを確認する', async () => withLedger(async () => {
  const f = fixture(); expect((await readLedger(f.api, repo)).record).toBeNull();
  expect(f.issue.body).toContain('Bunの脆弱性自動修正の処理状態を保持するIssueです。');
  for (const change of [x => { x.issue.state = 'closed'; }, x => { x.issue.body = ''; }, x => { x.issue.user = null; }, x => { x.issue.repository_url = 'https://api.github.com/repos/other/repo'; }]) {
    const x = fixture(); change(x); await expect(readLedger(x.api, repo)).rejects.toThrow();
  }
  delete process.env.BUN_SECURITY_LEDGER_ISSUE; await expect(readLedger(f.api, repo)).rejects.toThrow();
}));
test('応答不明後に再読で確定し、intent→idle に completion を保持する', async () => withLedger(async () => {
  const f = fixture(); f.lostResponse = true;
  await seed(f); await saveIntent(f.api, intent, proof);
  await saveRecord(f.api, repo, { ...intent, state: 'merged' }, null, sha('5'));
  const state = await readLedger(f.api, repo);
  expect(state.record).toBeNull(); expect(state.lastCompletion.actual).toBe(sha('5'));
  expect(f.comments).toHaveLength(6);
}));
test('数値 repoId の last と next のみを安全に辿る', async () => withLedger(async () => {
  const f = fixture(); await seed(f); await saveIntent(f.api, intent, proof);
  const event = f.comments.slice(-2), first = Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }));
  f.calls = []; f.pages = new Map([[1, first], [500, event]]);
  const link = '<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=500>; rel="last"';
  f.links = new Map([[1, link]]);
  expect((await readLedger(f.api, repo)).record.state).toBe('intent');
  expect(f.calls.filter(item => item[1].includes('/comments?')).length).toBe(2);
  f.pages = new Map([[1, first], [2, event]]); f.links = new Map([[1, '<https://api.github.com/repos/owner/repo/issues/8/comments?per_page=100&page=2>; rel="next"']]);
  expect((await readLedger(f.api, repo)).record.state).toBe('intent');
  f.links.set(1, '<https://attacker.invalid/repositories/12/issues/8/comments?per_page=100&page=2>; rel="next"');
  await expect(readLedger(f.api, repo)).rejects.toThrow();
}));
test('宣言済みlastのコメントページが空ならledgerを空とみなさない', async () => withLedger(async () => {
  const f = fixture(), filler = Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }));
  f.pages = new Map([[1, filler], [2, filler], [3, []]]);
  f.links = new Map([[1, '<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=3>; rel="last"']]);
  await expect(readLedger(f.api, repo)).rejects.toThrow('Declared last comment page is empty');
}));
test('ledgerのprev・firstがページ位置と矛盾すれば空履歴とみなさない', async () => withLedger(async () => {
  const link = (page, rel) => `<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=${page}>; rel="${rel}"`;
  for (const malformed of [link(2, 'prev'), link(2, 'first')]) {
    const f = fixture(); f.pages = new Map([[1, []]]); f.links = new Map([[1, malformed]]);
    await expect(readLedger(f.api, repo)).rejects.toThrow();
  }
  const f = fixture(), filler = Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }));
  f.pages = new Map([[1, filler], [2, [{ id: 101, body: 'ordinary', user: bot }]]]);
  f.links = new Map([[1, `${link(2, 'next')}, ${link(2, 'last')}`], [2, link(2, 'prev')]]);
  await expect(readLedger(f.api, repo)).rejects.toThrow('Invalid ledger previous page');
}));
test('履歴途中ページのnext欠落・last不一致は保留する', async () => withLedger(async () => {
  const f = fixture(); await seed(f); await saveIntent(f.api, intent, proof);
  const filler = (start, length) => Array.from({ length }, (_, i) => ({ id: start + i, body: 'ordinary', user: bot }));
  const first = [...filler(1, 99), { ...f.comments[0], id: 100 }];
  const moved = (index, id, predecessor) => ({ ...f.comments[index], id, body: f.comments[index].body.replace(`"prev":${99 + index}`, `"prev":${predecessor}`) });
  const middle = [...filler(101, 97), moved(1, 198, 100), moved(2, 199, 198), moved(3, 200, 199)];
  const last = [moved(4, 201, 200)];
  const link = (page, rel) => `<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=${page}>; rel="${rel}"`;
  f.pages = new Map([[1, first], [2, middle], [3, last]]);
  for (const middleLink of [null, `${link(3, 'next')}, ${link(4, 'last')}`]) {
    f.links = new Map([[1, `${link(2, 'next')}, ${link(3, 'last')}`], [2, middleLink], [3, `${link(2, 'prev')}, ${link(1, 'first')}`]]);
    await expect(readLedger(f.api, repo, { history: true })).rejects.toThrow();
  }
  f.links.set(2, `${link(3, 'next')}, ${link(3, 'last')}`);
  expect((await readLedger(f.api, repo, { history: true })).record.state).toBe('intent');
}));
test('bot 偽装・編集・空白・不明 actor・不正イベントを拒否する', async () => withLedger(async () => {
  const f = fixture(); await seed(f); await saveIntent(f.api, intent, proof); const original = f.comments.at(-1); const seeds = f.comments.slice(0, -1);
  for (const changed of [{ user: null }, { user: { id: 7, login: 'github-actions[bot]' } }, { body: '' }, { updated_at: '2026-11-05T00:00:01Z' }, { body: original.body.replace(/"prev":\d+/, '"prev":9') }]) {
    f.comments = [...seeds, { ...original, ...changed }]; await expect(readLedger(f.api, repo)).rejects.toThrow();
  }
}));
test('末尾探索は50回・残予算・応答期限を越えず不正な Link で停止する', async () => withLedger(async () => {
  const f = fixture(); let calls = 0;
  f.api.response = async path => {
    const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page')); calls++;
    return { status: 200, data: [], headers: { remaining: '300', link: `<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=${page + 1}>; rel="next"` } };
  };
  await expect(readLedger(f.api, repo)).rejects.toThrow(/limit/); expect(calls).toBe(50);
  f.api.response = async () => ({ status: 200, data: [], headers: { remaining: '199', link: null } });
  await expect(readLedger(f.api, repo)).rejects.toThrow(/budget/);
  f.api.response = (_path, _method, _body, { signal }) => new Promise((_, reject) => signal.addEventListener('abort', () => reject(Error('deadline'))));
  await expect(readLedger(f.api, repo, { signal: AbortSignal.timeout(10) })).rejects.toThrow(/deadline/);
}));

test('矛盾する POST 再読と途中からの状態変更は保留する', async () => withLedger(async () => {
  const f = fixture(); await seed(f);
  const original = f.api.request;
  f.api.request = async (...args) => { const result = await original(...args); if (args[1] === 'POST') f.comments.at(-1).body += 'changed'; return result; };
  await expect(saveRecord(f.api, repo, intent, proof)).rejects.toThrow();
  const other = fixture();
  await expect(saveRecord(other.api, repo, { ...intent, state: 'failed', http_status: 409 }, proof)).rejects.toThrow();
}));

test('末尾を確定し、次の完了へ last_completion を引き継ぐ', async () => withLedger(async () => {
  const f = fixture(); await seed(f); await saveIntent(f.api, intent, proof);
  await saveRecord(f.api, repo, { ...intent, state: 'merged' }, null, sha('5'));
  const previous = (await readLedger(f.api, repo)).lastCompletion;
  const next = { ...intent, pr: 8, head: sha('6'), run: 10 };
  const nextProof = { ...proof, id: 44, target_url: 'https://github.com/owner/repo/actions/runs/10/attempts/1', description: `PR 8 H ${sha('6')}; run 10 a 1; ${nonce}` };
  const candidate = { package_blob: sha('8'), lock_blob: sha('9') };
  await saveProposal(f.api, repo, { state: 'preparing', proposal: null, lastClosed: null, preparing: { base: next.base, branch, pr: null, head: next.head, ...candidate } });
  await saveProposal(f.api, repo, { state: 'idle', proposal: { pr: next.pr, branch, base: next.base, head: next.head, ...candidate }, lastClosed: null, preparing: null });
  await saveIntent(f.api, next, nextProof);
  expect((await readLedger(f.api, repo)).lastCompletion).toEqual(previous);
  const lastTwo = f.comments.slice(-2), first = Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }));
  f.pages = new Map([[1, first], [3, lastTwo]]);
  f.links = new Map([[1, '<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=3>; rel="last"']]);
  expect((await readLedger(f.api, repo)).record.pr).toBe(8);
}));

test('確認できた非 bot の ledger marker 引用は無視し、作者不明は拒否する', async () => withLedger(async () => {
  const f = fixture(); await seed(f); await saveIntent(f.api, intent, proof);
  f.comments.push({ id: 200, body: f.comments.at(-1).body, user: { id: 55, login: 'human' } });
  expect((await readLedger(f.api, repo)).record.state).toBe('intent');
  f.comments.at(-2).user = null; await expect(readLedger(f.api, repo)).rejects.toThrow();
}));

test('100件の bot issue・PR を越え opaque cursor で次ページの通知を読む', async () => {
  const issue = { number: 4, state: 'open', user: bot, body: '<!-- bun-security-automation -->\nuser edit' };
  const calls = [];
  const api = { repository, async response(path) {
    calls.push(path); const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
    if (page === 1) return { status: 200, data: Array.from({ length: 100 }, (_, i) => i % 2 ? { number: i + 100, pull_request: {} } : { number: i + 100, body: 'unrelated', user: bot }), headers: { remaining: '1000', link: '<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&after=opaque%2Bdata%3D&page=2>; rel="next"' } };
    expect(new URL('https://fixture.invalid' + path).searchParams.get('after')).toBe('opaque+data=');
    return { status: 200, data: [issue], headers: { remaining: '999', link: null } };
  } };
  expect(await findNotification(api, repo)).toEqual(issue); expect(calls).toHaveLength(3);
  const original = api.response;
  api.response = async path => { const response = await original(path); if (response.headers.link) response.headers.link = response.headers.link.replace('api.github.com', 'attacker.invalid'); return response; };
  await expect(findNotification(api, repo)).rejects.toThrow();
});

test('満杯の末尾コメントは次ページの空と正しい Link を確認する', async () => withLedger(async () => {
  const f = fixture();
  f.pages = new Map([[1, Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }))], [2, []]]);
  const link = (page, rel) => `<https://api.github.com/repositories/12/issues/8/comments?per_page=100&page=${page}>; rel="${rel}"`;
  f.links = new Map([[2, `${link(1, 'prev')}, ${link(1, 'last')}, ${link(1, 'first')}`]]);
  expect((await readLedger(f.api, repo)).record).toBeNull();
  f.pages.set(2, [{ id: 101, body: 'ordinary', user: bot }]);
  await expect(readLedger(f.api, repo)).rejects.toThrow();
  f.pages.set(2, []); f.links.set(2, link(3, 'last'));
  await expect(readLedger(f.api, repo)).rejects.toThrow();
}));

test('複数ページの通知一覧は先頭ページの変化を保留する', async () => {
  let firstReads = 0;
  const api = { repository, async response(path) {
    const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
    if (page === 1) {
      firstReads++;
      return { status: 200, data: Array.from({ length: 100 }, (_, i) => ({ number: i + (firstReads === 1 ? 1 : 2), body: 'other', user: bot })), headers: { remaining: '1000', link: '<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=2>; rel="next"' } };
    }
    return { status: 200, data: [], headers: { remaining: '1000', link: null } };
  } };
  await expect(findNotification(api, repo)).rejects.toThrow('Notification first page changed');
});

test('通知一覧でlastが先のページを指すのにnextがない場合は保留する', async () => {
  const api = { repository, async response() { return { status: 200, data: [], headers: { remaining: '1000', link: '<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=2>; rel="last"' } }; } };
  await expect(findNotification(api, repo)).rejects.toThrow('Notification page chain is incomplete');
});
test('本文 null は非通知として扱い、本文欠落は保留する', async () => {
  const issue = { number: 1, body: null, user: bot };
  const api = { repository, async response() { return { status: 200, data: [issue], headers: { remaining: '1000', link: null } }; } };
  expect(await findNotification(api, repo)).toBeNull();
  delete issue.body;
  await expect(findNotification(api, repo)).rejects.toThrow('Notification issue body unavailable');
});

test('通知一覧のprev・firstがページ位置と矛盾すれば空一覧とみなさない', async () => {
  const link = (page, rel) => `<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=${page}>; rel="${rel}"`;
  for (const mode of ['first-prev', 'first-first', 'later-prev']) {
    const api = { repository, async response(path) {
      const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
      if (mode !== 'later-prev') return { status: 200, data: [], headers: { remaining: '1000', link: link(2, mode === 'first-prev' ? 'prev' : 'first') } };
      return { status: 200, data: page === 1 ? Array.from({ length: 100 }, (_, i) => ({ number: i + 100, body: 'ordinary', user: bot })) : [],
        headers: { remaining: '1000', link: page === 1 ? `${link(2, 'next')}, ${link(2, 'last')}` : link(2, 'prev') } };
    } };
    await expect(findNotification(api, repo)).rejects.toThrow();
  }
});

test('満ページの次が空でlastが前ページを指す正規応答は受け付ける', async () => {
  const api = { repository, async response(path) {
    const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
    const data = page === 1 ? Array.from({ length: 100 }, (_, i) => ({ number: i + 100, body: 'unrelated', user: bot })) : [];
    const link = page === 2 ? '<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=1>; rel="prev", <https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=1>; rel="last", <https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=1>; rel="first"' : null;
    return { status: 200, data, headers: { remaining: '1000', link } };
  } };
  expect(await findNotification(api, repo)).toBeNull();
});

test('通知一覧の矛盾するLink・重複・ページ上限は保留する', async () => {
  const link = (page, rel) => `<https://api.github.com/repositories/12/issues?state=open&creator=github-actions%5Bbot%5D&per_page=100&page=${page}>; rel="${rel}"`;
  for (const mode of ['last-before-next', 'short-next', 'nonempty-old-last', 'last-changed', 'missing-next', 'duplicate', 'overflow']) {
    const api = { repository, async response(path) {
      const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
      let data = Array.from({ length: 100 }, (_, i) => ({ number: page * 1000 + i, body: 'unrelated', user: bot })), header = null;
      if (mode === 'last-before-next') header = `${link(2, 'next')}, ${link(1, 'last')}`;
      if (mode === 'short-next') { data = data.slice(0, 1); header = link(2, 'next'); }
      if (mode === 'nonempty-old-last' && page === 2) { data = data.slice(0, 1); header = link(1, 'last'); }
      if (mode === 'last-changed') header = page === 1 ? `${link(2, 'next')}, ${link(3, 'last')}` : `${link(3, 'next')}, ${link(4, 'last')}`;
      if (mode === 'missing-next') { if (page === 1) header = `${link(2, 'next')}, ${link(3, 'last')}`; else data = data.slice(0, 1); }
      if (mode === 'duplicate' && page === 2) data = [{ number: 1000, body: 'unrelated', user: bot }];
      return { status: 200, data, headers: { remaining: '1000', link: header } };
    } };
    await expect(findNotification(api, repo)).rejects.toThrow();
  }
});

test('1000件超でも通知末尾から検索し、応答断を再読して重複を避ける', async () => {
  const issue = { number: 4 }, content = 'same report', digest = createHash('sha256').update(content).digest('hex');
  const previous = { id: 1100, user: bot, body: `<!-- bun-security-report-v1:${digest} -->\ntext`, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z' };
  const human = { id: 1101, user: { id: 55, login: 'human' }, body: previous.body };
  const first = Array.from({ length: 100 }, (_, i) => ({ id: i + 1, body: 'ordinary', user: bot }));
  const pages = new Map([[1, first], [11, [previous, human]]]); let reads = 0, posts = 0;
  const api = { repository, async response(path) {
    reads++; const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
    return { status: 200, data: pages.get(page) ?? [], headers: { remaining: '1000', link: page === 1 ? '<https://api.github.com/repositories/12/issues/4/comments?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/12/issues/4/comments?per_page=100&page=11>; rel="last"' : null } };
  }, async request(path, method, body) {
    expect(path).toBe('/issues/4/comments'); expect(method).toBe('POST'); posts++;
    pages.get(11).push({ id: 1102, user: bot, body: body.body, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z' });
    throw Error('lost response');
  } };
  await reportComment(api, repo, issue, content); expect(posts).toBe(0); expect(reads).toBe(2);
  await reportComment(api, repo, issue, 'new report'); expect(posts).toBe(1);
  await reportComment(api, repo, issue, 'new report'); expect(posts).toBe(1);
  pages.get(11).at(-1).updated_at = '2026-11-05T00:00:01Z';
  await expect(reportComment(api, repo, issue, 'new report')).rejects.toThrow();
});

test('通知digestが末尾にあっても先頭ページの矛盾で更新を省略しない', async () => {
  const content = 'same report', digest = createHash('sha256').update(content).digest('hex'); let posts = 0;
  const comment = { id: 301, user: bot, body: `<!-- bun-security-report-v1:${digest} -->\ntext`, created_at: '2026-11-05T00:00:00Z', updated_at: '2026-11-05T00:00:00Z' };
  const api = { repository, async response(path) {
    const page = Number(new URL('https://fixture.invalid' + path).searchParams.get('page'));
    return { status: 200, data: page === 3 ? [comment] : [], headers: { remaining: '1000', link: page === 1 ? '<https://api.github.com/repositories/12/issues/4/comments?per_page=100&page=3>; rel="last"' : null } };
  }, async request() { posts++; } };
  await expect(reportComment(api, repo, { number: 4 }, content)).rejects.toThrow('Comment page chain is incomplete');
  expect(posts).toBe(0);
});
