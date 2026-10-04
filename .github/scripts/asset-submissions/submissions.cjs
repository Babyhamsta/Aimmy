'use strict';

// All code in this module comes from the trusted workflow commit, never PR HEAD.
const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const TARGET = 'Aimmy-V2';
const MAX_FILES = 3000;
const MAX_TOTAL = 256 * 1024 * 1024;
const SHA = /^[a-f0-9]{40}$/;
const MARKER = '<!-- aimmy-asset-validation-v2 -->';
const digest = value => crypto.createHash('sha256').update(JSON.stringify(value)).digest('hex');
const isAsset = name => name.startsWith('models/') || name.startsWith('configs/');
const kindOf = name => name.startsWith('models/') && name.endsWith('.onnx') ? 'model'
  : name.startsWith('configs/') && name.endsWith('.cfg') ? 'config' : null;
const bound = (kind, size) => Number.isSafeInteger(size) && (kind === 'model'
  ? size >= 5 * 1024 * 1024 && size <= 50 * 1024 * 1024
  : size > 0 && size <= 1024 * 1024);
const sameEntry = (a, b) => a && b && a.sha === b.sha && a.mode === b.mode && a.type === b.type;
const safeName = name => typeof name === 'string' && name.length <= 512 && name.split('/').every(part =>
  part.length > 0 && part.length <= 240 && !['.', '..'].includes(part) &&
  !/[\p{Cc}\p{Cf}\p{Zl}\p{Zp}<>:"\\|?*]/u.test(part) && !/[. ]$/.test(part) &&
  !/^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)/i.test(part));

function repoArgs(fullName) {
  if (typeof fullName !== 'string' || !/^[\w.-]+\/[\w.-]+$/.test(fullName)) throw Error('Invalid repository identity');
  const [owner, repo] = fullName.split('/');
  return { owner, repo };
}

function unchanged(pr, expected, context) {
  return pr.number === expected.number && pr.state === 'open' && !pr.draft &&
    pr.base?.repo?.full_name === `${context.repo.owner}/${context.repo.repo}` &&
    pr.base.ref === TARGET && pr.base.ref === expected.base.ref &&
    pr.head?.sha === expected.head.sha &&
    pr.head?.repo?.full_name === expected.head.repo?.full_name;
}

async function tree(github, repo, sha) {
  if (!SHA.test(sha)) throw Error('Invalid commit identity');
  const { data } = await github.rest.git.getTree({ ...repo, tree_sha: sha, recursive: '1' });
  if (data.truncated !== false || !Array.isArray(data.tree) || data.tree.length > 100000) {
    throw Error('GitHub could not return a complete tree');
  }
  const entries = new Map();
  for (const entry of data.tree) {
    if (typeof entry.path !== 'string' || entries.has(entry.path) || !SHA.test(entry.sha)) {
      throw Error('Malformed or duplicate tree entry');
    }
    entries.set(entry.path, entry);
  }
  return entries;
}

function diffTrees(before, after) {
  const names = [...new Set([...before.keys(), ...after.keys()])].sort();
  return names.filter(name => {
    const a = before.get(name), b = after.get(name);
    if (a?.type === 'tree' && b?.type === 'tree') return false;
    if (!a && b?.type === 'tree' || !b && a?.type === 'tree') return false;
    return !sameEntry(a, b);
  }).map(name => ({ name, before: before.get(name), after: after.get(name) }));
}

async function listFiles(github, repo, number, count) {
  if (!Number.isSafeInteger(count) || count < 0 || count > MAX_FILES) throw Error('PR exceeds the 3,000-file API limit');
  const files = [];
  for (let page = 1; page <= Math.max(1, Math.ceil(count / 100)); page++) {
    const { data } = await github.rest.pulls.listFiles({ ...repo, pull_number: number, per_page: 100, page });
    if (!Array.isArray(data) || data.length > 100) throw Error('Invalid file-list response');
    files.push(...data);
  }
  if (files.length !== count || new Set(files.map(f => f.filename)).size !== count) {
    throw Error('PR file list changed or was incomplete');
  }
  return files;
}

async function baseTip(github, repo) {
  const { data } = await github.rest.git.getRef({ ...repo, ref: `heads/${TARGET}` });
  if (!SHA.test(data.object?.sha) || data.object.type !== 'commit') throw Error('Invalid base branch tip');
  return data.object.sha;
}

async function inspect({ github, context }, retries = 2) {
  const expected = context.payload.pull_request;
  const repo = context.repo;
  if (!expected || !Number.isSafeInteger(expected.number) || expected.base?.ref !== TARGET ||
      !SHA.test(expected.head?.sha) || !SHA.test(expected.base?.sha)) return { decision: 'skip' };
  const getPR = async () => (await github.rest.pulls.get({ ...repo, pull_number: expected.number })).data;
  const pr = await getPR();
  if (!unchanged(pr, expected, context)) return { decision: 'skip' };
  const base = await baseTip(github, repo);
  const files = await listFiles(github, repo, pr.number, pr.changed_files);
  // Never use compare.files (limited to 300) or mutable listFiles as the allowlist.
  const { data: comparison } = await github.rest.repos.compareCommitsWithBasehead({
    ...repo, basehead: `${base}...${pr.head.sha}`, per_page: 1,
  });
  const mergeBase = comparison.merge_base_commit?.sha;
  if (!SHA.test(mergeBase)) throw Error('Missing merge-base identity');
  const headRepo = repoArgs(pr.head.repo.full_name);
  const [before, after, currentBase] = await Promise.all([
    tree(github, repo, mergeBase), tree(github, headRepo, pr.head.sha), tree(github, repo, base),
  ]);
  const changes = diffTrees(before, after);
  if (changes.length > MAX_FILES) throw Error('Submission has too many changed paths');
  const latest = await getPR();
  if (!unchanged(latest, expected, context)) return { decision: 'skip' };
  if (await baseTip(github, repo) !== base) {
    if (retries > 0) return inspect({ github, context }, retries - 1);
    throw Error('Base branch kept changing during inspection; rerun validation');
  }
  // Code PRs, including one valid asset plus code, remain completely manual.
  if (changes.some(change => !isAsset(change.name))) return { decision: 'skip' };
  if (files.some(file => !isAsset(file.filename))) return { decision: 'skip' };
  const manifest = changes.map(({ name, before: a, after: b }) => ({
    name, status: !a ? 'added' : !b ? 'removed' : 'modified',
    oldSha: a?.sha || null, oldMode: a?.mode || null, oldType: a?.type || null,
    sha: b?.sha || null, mode: b?.mode || null, type: b?.type || null, size: b?.size ?? null,
    kind: kindOf(name),
  }));
  const identity = { number: pr.number, repository: `${repo.owner}/${repo.repo}`, head: pr.head.sha,
    headRepository: pr.head.repo.full_name, base, baseRef: TARGET, mergeBase, manifest };
  // Parser proof follows immutable asset bytes, not unrelated movement of the base.
  // Publication recomputes the current merge-base diff and duplicate policy separately.
  const { base: ignoredBase, mergeBase: ignoredMergeBase, ...proof } = identity;
  const snapshot = { ...identity, fingerprint: digest(proof), decision: 'eligible', reasons: [] };
  if (!changes.length && !files.length) return { ...snapshot, decision: 'invalid', reasons: ['No files were changed.'] };
  // Renames/deletions/type changes can be legitimate maintenance; they are never auto-merged.
  if (manifest.some(f => !['added', 'modified'].includes(f.status) || f.type !== 'blob' || f.mode !== '100644' ||
      f.status === 'modified' && (f.oldType !== 'blob' || f.oldMode !== '100644'))) {
    return { ...snapshot, decision: 'manual', reasons: ['Only additions and updates of regular, non-executable asset files can be auto-merged. Deletions, renames, links and mode changes need maintainer review.'] };
  }
  // This cross-check catches races and API inconsistencies; immutable trees remain authoritative.
  if (files.length !== manifest.length || files.some(file => !manifest.some(f =>
      f.name === file.filename && f.status === file.status && f.sha === file.sha))) {
    throw Error('PR file listing did not match the immutable diff');
  }
  let total = 0;
  const existing = new Map([...currentBase.values()].filter(e => e.type === 'blob' && kindOf(e.path) === 'model' &&
    !manifest.some(f => f.name === e.path)).map(e => [e.sha, e.path]));
  for (const file of manifest) {
    if (!safeName(file.name) || !file.kind) snapshot.reasons.push(`${file.name}: use a Windows-compatible .onnx name under models/ or .cfg name under configs/.`);
    else if (!bound(file.kind, file.size)) snapshot.reasons.push(`${file.name}: models must be 5–50 MiB; configs must be nonempty and at most 1 MiB.`);
    total += file.size || 0;
    if (file.kind === 'model') {
      if (existing.has(file.sha)) snapshot.reasons.push(`${file.name}: identical to another model (${existing.get(file.sha)}).`);
      existing.set(file.sha, file.name);
    }
  }
  if (total > MAX_TOTAL) snapshot.reasons.push('Submission exceeds the 256 MiB total limit. Please split it into smaller PRs.');
  if (snapshot.reasons.length) snapshot.decision = 'invalid';
  return snapshot;
}

function report(snapshot, decision = snapshot.decision, reasons = snapshot.reasons) {
  return { version: 1, fingerprint: snapshot.fingerprint, decision, reasons: (reasons || []).slice(0, 20).map(s => String(s).slice(0, 800)) };
}
const statePath = () => path.join(process.env.RUNNER_TEMP || os.tmpdir(), 'aimmy-submission.json');

async function prepare(args) {
  const snapshot = await inspect(args);
  fs.writeFileSync(statePath(), JSON.stringify(snapshot), { mode: 0o600 });
  args.core.setOutput('eligible', snapshot.decision === 'eligible');
  args.core.setOutput('report', JSON.stringify(report(snapshot)));
}

function runParser(input, kind) {
  // No checkout, host credentials, network, writable host mount, GPU, or Docker socket.
  const container = `aimmy-parser-${crypto.randomUUID()}`;
  let result;
  try {
    result = spawnSync('docker', ['run', '--rm', '--name', container, '--network=none', '--read-only',
    '--cap-drop=ALL', '--security-opt=no-new-privileges', '--user=65534:65534',
    '--memory=1536m', '--memory-swap=1536m', '--cpus=1', '--pids-limit=32',
    '--ulimit=cpu=60:60', '--ulimit=fsize=0:0', '--ulimit=nofile=64:64',
    '--mount', `type=bind,src=${input},dst=/input/data,readonly`,
    'aimmy-asset-validator', kind, '/input/data'],
    { encoding: 'utf8', timeout: 90000, maxBuffer: 16384, shell: false });
  } finally {
    // Killing a timed-out Docker client alone need not stop its container.
    spawnSync('docker', ['rm', '--force', container], { timeout: 10000, maxBuffer: 1024, shell: false, stdio: 'ignore' });
  }
  if (result.error || result.signal || ![0, 2].includes(result.status)) throw Error('The isolated parser failed or exceeded a resource limit');
  let value;
  try { value = JSON.parse(result.stdout); } catch { throw Error('The isolated parser returned an invalid response'); }
  if (value?.valid !== (result.status === 0) || typeof value.message !== 'string' || value.message.length > 500) {
    throw Error('The isolated parser returned an invalid verdict');
  }
  return value;
}

async function validate({ github, core }, parser = runParser) {
  const snapshot = JSON.parse(fs.readFileSync(statePath(), 'utf8'));
  if (snapshot.decision !== 'eligible') throw Error('No admitted submission');
  const folder = fs.mkdtempSync(path.join(process.env.RUNNER_TEMP || os.tmpdir(), 'aimmy-assets-'));
  fs.chmodSync(folder, 0o755);
  const reasons = [];
  try {
    for (const [i, file] of snapshot.manifest.entries()) {
      const { data } = await github.rest.git.getBlob({ ...repoArgs(snapshot.headRepository), file_sha: file.sha });
      if (data.encoding !== 'base64' || data.sha !== file.sha || data.size !== file.size ||
          typeof data.content !== 'string' || data.content.length > Math.ceil(file.size * 1.4) + 1024) {
        throw Error('Blob response did not match the admitted asset');
      }
      const bytes = Buffer.from(data.content, 'base64');
      const sha = crypto.createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
      if (bytes.length !== file.size || sha !== file.sha) throw Error('Downloaded bytes did not match the admitted asset');
      // Contributor filenames are never used as filesystem paths or shell/script source.
      const input = path.join(folder, `${i}.bin`);
      fs.writeFileSync(input, bytes, { mode: 0o444 });
      const verdict = parser(input, file.kind);
      if (!verdict.valid) reasons.push(`${file.name}: ${verdict.message}`);
      fs.unlinkSync(input);
    }
  } finally { fs.rmSync(folder, { recursive: true, force: true }); }
  core.setOutput('report', JSON.stringify(report(snapshot, reasons.length ? 'invalid' : 'valid', reasons)));
}

function readReport(raw) {
  if (typeof raw !== 'string' || Buffer.byteLength(raw) > 20000) return null;
  try {
    const value = JSON.parse(raw);
    return value.version === 1 && /^[a-f0-9]{64}$/.test(value.fingerprint) &&
      ['valid', 'invalid', 'manual', 'eligible'].includes(value.decision) && Array.isArray(value.reasons) &&
      value.reasons.length <= 20 && value.reasons.every(s => typeof s === 'string' && s.length <= 800) ? value : null;
  } catch { return null; }
}
const escape = text => String(text).replace(/[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]/gu, ' ').replace(/@/g, '@\u200b').replace(/[\\`*_{}\[\]()#+.!<>|~]/g, '\\$&');

async function comment({ github, context }, body) {
  const args = { ...context.repo, issue_number: context.payload.pull_request.number };
  // Only edit this workflow's own comment, never an arbitrary bot's comment.
  let existing;
  for await (const response of github.paginate.iterator(github.rest.issues.listComments, { ...args, per_page: 100 })) {
    existing = response.data.find(c => c.user?.login === 'github-actions[bot]' && c.body?.startsWith(MARKER));
    if (existing) break;
  }
  if (existing) await github.rest.issues.updateComment({ ...context.repo, comment_id: existing.id, body: `${MARKER}\n${body}` });
  else await github.rest.issues.createComment({ ...args, body: `${MARKER}\n${body}` });
}

async function labels({ github, context, core }, snapshot, decision) {
  const args = { ...context.repo, issue_number: snapshot.number };
  const kinds = new Set(snapshot.manifest.map(f => f.kind).filter(Boolean));
  const desired = decision === 'manual' ? ['needs-manual-merge'] :
    [...kinds].map(kind => `${kind}:${decision === 'valid' ? 'valid' : 'invalid'}`);
  if (decision === 'invalid') desired.push('invalid');
  for (const name of ['invalid', 'model:valid', 'model:invalid', 'config:valid', 'config:invalid', 'needs-manual-merge']) {
    if (!desired.includes(name)) {
      try { await github.rest.issues.removeLabel({ ...args, name }); }
      catch (error) { if (error.status !== 404) core.warning(`Could not remove automation label (${error.status || 'API failure'}).`); }
    }
  }
  if (desired.length) {
    try { await github.rest.issues.addLabels({ ...args, labels: desired }); }
    catch (error) { core.warning(`Could not add automation labels (${error.status || 'API failure'}).`); }
  }
}

async function publish(args, retries = 2) {
  const { github, context, core } = args;
  const snapshot = await inspect(args);
  if (snapshot.decision === 'skip') return;
  const received = readReport(process.env.VALIDATION_REPORT || '');
  let decision = snapshot.decision;
  let reasons = snapshot.reasons;
  if (decision === 'eligible') {
    if (process.env.VALIDATION_JOB_RESULT !== 'success' || !received || received.fingerprint !== snapshot.fingerprint ||
        !['valid', 'invalid'].includes(received.decision)) {
      decision = 'manual';
      reasons = ['Validation did not finish for this exact revision. Rerun the workflow; no automatic merge was attempted.'];
    } else { decision = received.decision; reasons = received.reasons; }
  }
  const fresh = async () => {
    const pr = (await github.rest.pulls.get({ ...context.repo, pull_number: snapshot.number })).data;
    if (!unchanged(pr, context.payload.pull_request, context)) return false;
    if (await baseTip(github, context.repo) !== snapshot.base) {
      if (retries > 0) await publish(args, retries - 1);
      else await comment(args, 'The base branch kept changing during validation. Please rerun this workflow; no automatic merge was attempted.');
      return false;
    }
    return true;
  };
  if (!await fresh()) return;
  await labels(args, snapshot, decision);
  const revision = snapshot.head.slice(0, 12);
  if (decision !== 'valid') {
    if (!await fresh()) return;
    await comment(args, `Asset submission ${decision === 'invalid' ? 'needs changes' : 'needs attention'} (revision ${revision}).\n\n` +
      reasons.slice(0, 20).map(reason => `- ${escape(reason)}`).join('\n') +
      '\n\nThis PR has been left open. Fix the files and push again to revalidate. Code changes and asset maintenance can be reviewed normally.');
    return;
  }
  // The API atomically rejects a changed head. The immediate reread also checks base/ref/state.
  // GitHub has no atomic expected-base parameter; see README.md for that residual race.
  if (!await fresh()) return;
  let merged = false;
  try {
    const { data } = await github.rest.pulls.merge({ ...context.repo, pull_number: snapshot.number, sha: snapshot.head,
      merge_method: 'squash', commit_title: `Add community model/config (#${snapshot.number})` });
    merged = data.merged === true;
  } catch (error) {
    core.warning(`GitHub did not merge the submission (${error.status || 'API failure'}).`);
  }
  if (merged) await comment(args, `Validated and automatically merged revision ${revision}. Thank you for your contribution!`);
  else if (await fresh()) {
    await labels(args, snapshot, 'manual');
    await comment(args, `Asset validation passed for revision ${revision}, but GitHub did not merge it. Check merge conflicts, required checks/reviews, squash-merge availability and repository permissions, then rerun or merge normally.`);
  }
}

module.exports = { TARGET, bound, safeName, kindOf, diffTrees, unchanged, inspect, prepare, validate,
  report, readReport, runParser, publish, escape, listFiles };
