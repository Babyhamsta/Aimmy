'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const policy = require('./submissions.cjs');
const A = 'a'.repeat(40), B = 'b'.repeat(40), H = 'c'.repeat(40), S = 'd'.repeat(40);
const blob = (name, extra = {}) => ({ path: name, sha: S, mode: '100644', type: 'blob', size: name.endsWith('.onnx') ? 6 * 1024 * 1024 : 2, ...extra });
function fixture(after = [blob('configs/test.cfg')], before = [], options = {}) {
  const pr = { number: 42, state: 'open', draft: false,
    base: { ref: 'Aimmy-V2', sha: B, repo: { full_name: 'Babyhamsta/Aimmy' } },
    head: { sha: H, repo: { full_name: 'contributor/Aimmy' } } };
  const differences = policy.diffTrees(new Map(before.map(f => [f.path, f])), new Map(after.map(f => [f.path, f])));
  let files = differences.map(d => ({ filename: d.name, status: !d.before ? 'added' : !d.after ? 'removed' : 'modified', sha: d.after?.sha }));
  if (options.files) files = options.files;
  pr.changed_files = files.length;
  const context = { repo: { owner: 'Babyhamsta', repo: 'Aimmy' }, payload: { pull_request: structuredClone(pr) } };
  const calls = { merges: [], comments: [], labels: [], pages: [], outputs: {}, gets: 0, trees: [] };
  const github = { rest: {
    pulls: {
      get: async () => { calls.gets++; options.onGet?.(pr, calls.gets); return { data: structuredClone(pr) }; },
      listFiles: async args => { calls.pages.push(args.page); return { data: files.slice((args.page - 1) * 100, args.page * 100) }; },
      merge: async args => { calls.merges.push(args); if (options.mergeThrows) throw { status: 409 }; return { data: { merged: options.merged ?? true } }; },
    },
    repos: { compareCommitsWithBasehead: async args => { assert.equal(args.basehead, `${options.baseTip || pr.base.sha}...${H}`); return { data: { merge_base_commit: { sha: A } } }; } },
    git: {
      getRef: async () => ({ data: { object: { type: 'commit', sha: options.baseTip || pr.base.sha } } }),
      getTree: async args => { calls.trees.push(args); return { data: { truncated: options.truncated ?? false,
        tree: args.tree_sha === H ? after : args.tree_sha === A ? before : options.currentBase || before } }; },
      getBlob: async () => ({ data: options.blob }),
    },
    issues: {
      listComments: () => {},
      createComment: async args => { calls.comments.push(args.body); },
      updateComment: async args => { calls.comments.push(args.body); },
      removeLabel: async args => { calls.labels.push(args); options.onLabel?.(pr); },
      addLabels: async args => { calls.labels.push(args); options.onLabel?.(pr); },
    },
  }, paginate: { iterator: async function* () { yield { data: options.comments || [] }; } } };
  const core = { setOutput: (key, val) => { calls.outputs[key] = val; }, warning: () => {} };
  return { github, context, core, calls, pr };
}
async function publish(f, overrides = {}) {
  const snapshot = await policy.inspect(f);
  process.env.VALIDATION_REPORT = JSON.stringify(policy.report(snapshot, 'valid', []));
  process.env.VALIDATION_JOB_RESULT = 'success';
  Object.assign(process.env, overrides);
  await policy.publish(f);
  return snapshot;
}

test('accepts added config from a fork using its immutable objects', async () => {
  const f = fixture();
  assert.equal((await policy.inspect(f)).decision, 'eligible');
  assert.ok(f.calls.trees.some(t => t.owner === 'contributor' && t.tree_sha === H));
});
test('accepts added model and updated config, including nested paths', async () => {
  const f = fixture([blob('models/nested/yolo.onnx'), blob('configs/nested/x.cfg', { sha: H })], [blob('configs/nested/x.cfg')]);
  assert.equal((await policy.inspect(f)).decision, 'eligible');
});
test('gets every file after page 100', async () => {
  const f = fixture(Array.from({ length: 205 }, (_, i) => blob(`configs/${i}.cfg`)));
  assert.equal((await policy.inspect(f)).decision, 'eligible');
  assert.deepEqual(f.calls.pages, [1, 2, 3]);
});
test('code-only PR remains untouched', async () => {
  const f = fixture([blob('Aimmy2/App.cs')]); await publish(f);
  assert.equal(f.calls.comments.length + f.calls.labels.length + f.calls.merges.length, 0);
});
test('mixed asset and code PR remains untouched, even after page 100', async () => {
  const f = fixture([...Array.from({ length: 101 }, (_, i) => blob(`configs/${i}.cfg`)), blob('.github/workflows/pwn.yml')]);
  await publish(f); assert.equal(f.calls.comments.length + f.calls.labels.length + f.calls.merges.length, 0);
});
test('immutable trees catch malicious changes hidden by a mutable A-B-A file list', async () => {
  const f = fixture([blob('configs/ok.cfg'), blob('.github/workflows/pwn.yml')], [],
    { files: [{ filename: 'configs/ok.cfg', status: 'added', sha: S }] });
  assert.equal((await policy.inspect(f)).decision, 'skip');
});
test('empty PR gets feedback but is never closed or merged', async () => {
  const f = fixture([]); await publish(f);
  assert.equal(f.calls.merges.length, 0); assert.match(f.calls.comments[0], /No files were changed/);
});
test('wrong extension gets actionable feedback without merging', async () => {
  const f = fixture([blob('models/readme.txt')]); await publish(f);
  assert.equal(f.calls.merges.length, 0); assert.match(f.calls.comments[0], /needs changes/);
});
for (const [name, after, before] of [
  ['deletion', [], [blob('configs/a.cfg')]],
  ['rename', [blob('configs/b.cfg')], [blob('configs/a.cfg')]],
  ['symlink', [blob('configs/a.cfg', { mode: '120000' })], []],
  ['executable', [blob('configs/a.cfg', { mode: '100755' })], []],
  ['submodule', [blob('configs/a.cfg', { mode: '160000', type: 'commit' })], []],
  ['replacing a symlink', [blob('configs/a.cfg')], [blob('configs/a.cfg', { mode: '120000' })]],
]) test(`${name} needs manual maintenance review`, async () => {
  const f = fixture(after, before); assert.equal((await policy.inspect(f)).decision, 'manual');
  await publish(f); assert.equal(f.calls.merges.length, 0);
});
for (const filename of ['configs/../bad.cfg', 'configs/a\nb.cfg', 'configs/\u202ebad.cfg', 'configs/a\\b.cfg', 'configs/CON.cfg', 'models/file.onnx/']) {
  test(`rejects unsafe filename ${JSON.stringify(filename)}`, async () => {
    assert.equal((await policy.inspect(fixture([blob(filename)]))).decision, 'invalid');
  });
}
test('treats shell/template syntax in a valid filename strictly as data', async () => {
  const filename = 'configs/x$(touch PWNED)`tick`${process.exit(1)}.cfg';
  assert.equal((await policy.inspect(fixture([blob(filename)]))).decision, 'eligible');
});
test('rejects truncated and malformed immutable trees', async () => {
  await assert.rejects(policy.inspect(fixture(undefined, [], { truncated: true })), /complete tree/);
  await assert.rejects(policy.inspect(fixture([blob('configs/a.cfg'), blob('configs/a.cfg')])), /duplicate tree/);
});
test('rejects incomplete mutable file pagination', async () => {
  const f = fixture(); f.pr.changed_files = 2;
  await assert.rejects(policy.inspect(f), /incomplete/);
});
test('rejects files above API cap', async () => {
  const f = fixture(); f.pr.changed_files = 3001;
  await assert.rejects(policy.inspect(f), /3,000/);
});
test('rejects intra-PR duplicates and duplicates already on base', async () => {
  assert.equal((await policy.inspect(fixture([blob('models/a.onnx'), blob('models/b.onnx')]))).decision, 'invalid');
  assert.equal((await policy.inspect(fixture([blob('models/a.onnx')], [], { currentBase: [blob('models/existing.onnx')] }))).decision, 'invalid');
});
test('enforces model/config size and aggregate caps', async () => {
  for (const file of [blob('models/a.onnx', { size: 1 }), blob('models/a.onnx', { size: 51 * 1024 * 1024 }),
    blob('configs/a.cfg', { size: 0 }), blob('configs/a.cfg', { size: 1024 * 1024 + 1 })]) {
    assert.equal((await policy.inspect(fixture([file]))).decision, 'invalid');
  }
  const files = Array.from({ length: 6 }, (_, i) => blob(`models/${i}.onnx`, { size: 50 * 1024 * 1024, sha: `${i}`.repeat(40) }));
  assert.equal((await policy.inspect(fixture(files))).decision, 'invalid');
});
for (const change of [pr => pr.head.sha = S, pr => pr.base.ref = 'other',
  pr => pr.draft = true, pr => pr.state = 'closed', pr => pr.head.repo.full_name = 'other/fork']) {
  test('stale/retargeted/draft/closed PR cannot publish', async () => {
    const f = fixture(); change(f.pr); await publish(f);
    assert.equal(f.calls.merges.length + f.calls.comments.length + f.calls.labels.length, 0);
  });
}
test('rechecks immediately before merge and detects updates during feedback', async () => {
  const f = fixture(undefined, [], { onLabel: pr => { pr.head.sha = S; } });
  await publish(f); assert.equal(f.calls.merges.length, 0);
});
test('merges with exact validated sha and only reports confirmed success', async () => {
  const f = fixture(); await publish(f);
  assert.equal(f.calls.merges.length, 1); assert.equal(f.calls.merges[0].sha, H);
  assert.match(f.calls.comments[0], /automatically merged/);
});
for (const options of [{ merged: false }, { mergeThrows: true }]) test('unconfirmed/blocked merge stays manual', async () => {
  const f = fixture(undefined, [], options); await publish(f);
  assert.ok(f.calls.comments.every(c => !c.includes('automatically merged')));
  assert.match(f.calls.comments[0], /GitHub did not merge/);
});
test('failed validation job never becomes invalid or merges', async () => {
  const f = fixture(); await publish(f, { VALIDATION_JOB_RESULT: 'failure' });
  assert.equal(f.calls.merges.length, 0); assert.match(f.calls.comments[0], /did not finish/);
});
test('forged or stale report never merges', async () => {
  const f = fixture(); await publish(f, { VALIDATION_REPORT: JSON.stringify({ version: 1, decision: 'valid', fingerprint: '0'.repeat(64), reasons: [] }) });
  assert.equal(f.calls.merges.length, 0);
});
test('strict report schema rejects oversized, malformed and inherited-looking data', () => {
  assert.equal(policy.readReport('x'.repeat(20001)), null);
  assert.equal(policy.readReport('{}'), null);
  assert.equal(policy.readReport('{"version":1,"decision":"valid","fingerprint":"' + 'a'.repeat(64) + '","reasons":[{}]}'), null);
});
test('escapes contributor comment text and disables mentions', () => {
  assert.equal(policy.escape('@everyone\n`bad` [link](url)'), '@\u200beveryone \\`bad\\` \\[link\\]\\(url\\)');
});
test('downloads exact admitted blob bytes to numeric paths only', async () => {
  const raw = Buffer.from('{}');
  const sha = crypto.createHash('sha1').update(`blob ${raw.length}\0`).update(raw).digest('hex');
  const f = fixture([blob('configs/$(echo pwn)`x`.cfg', { sha })], [], { blob: { sha, size: 2, encoding: 'base64', content: raw.toString('base64') } });
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'aimmy-test-'));
  const oldTemp = process.env.RUNNER_TEMP; process.env.RUNNER_TEMP = temp;
  try {
    await policy.prepare(f);
    await policy.validate(f, (input, kind) => {
      assert.equal(path.basename(input), '0.bin'); assert.equal(kind, 'config');
      assert.deepEqual(fs.readFileSync(input), raw); return { valid: true, message: 'OK' };
    });
    assert.equal(JSON.parse(f.calls.outputs.report).decision, 'valid');
    assert.deepEqual(fs.readdirSync(temp), ['aimmy-submission.json']);
  } finally { fs.rmSync(temp, { recursive: true }); if (oldTemp === undefined) delete process.env.RUNNER_TEMP; else process.env.RUNNER_TEMP = oldTemp; }
});
test('blob identity mismatch is infrastructure failure, never acceptance', async () => {
  const f = fixture(undefined, [], { blob: { sha: S, size: 2, encoding: 'base64', content: Buffer.from('{}').toString('base64') } });
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'aimmy-test-'));
  const oldTemp = process.env.RUNNER_TEMP; process.env.RUNNER_TEMP = temp;
  try { await policy.prepare(f); await assert.rejects(policy.validate(f, () => { throw Error('must not parse'); }), /did not match/); }
  finally { fs.rmSync(temp, { recursive: true }); if (oldTemp === undefined) delete process.env.RUNNER_TEMP; else process.env.RUNNER_TEMP = oldTemp; }
});

test('unrelated base movement before rerun does not strand a valid PR', async () => {
  const f = fixture(); f.pr.base.sha = S;
  await publish(f); assert.equal(f.calls.merges.length, 1);
});
test('unrelated base movement during publication rechecks and reuses byte proof', async () => {
  let changed = false;
  const f = fixture(undefined, [], { onLabel: pr => { if (!changed) { pr.base.sha = S; changed = true; } } });
  await publish(f); assert.equal(f.calls.merges.length, 1);
});
test('asset proof is unchanged by unrelated base movement', async () => {
  const f = fixture(); const original = await policy.inspect(f);
  f.pr.base.sha = S; const next = await policy.inspect(f);
  assert.equal(original.fingerprint, next.fingerprint); assert.notEqual(original.base, next.base);
});

test('resolves actual branch tip even when PR base metadata is stale', async () => {
  const f = fixture(undefined, [], { baseTip: S });
  const snapshot = await policy.inspect(f);
  assert.equal(snapshot.base, S); assert.equal(f.pr.base.sha, B);
  assert.ok(f.calls.trees.some(args => args.tree_sha === S));
  await publish(f); assert.equal(f.calls.merges.length, 1);
});
