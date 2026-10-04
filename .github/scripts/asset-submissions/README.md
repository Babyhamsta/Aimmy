# Community asset submission automation

`validate-assets.yml` handles non-draft PRs targeting `Aimmy-V2`. It continues to
**automatically squash-merge valid model/config-only submissions**, including
public forks. Code PRs and mixed code/asset PRs are left untouched for normal
review. The old independent empty-PR closer is replaced by this single flow.

## Accepted submissions

- Add or modify regular, non-executable Git blobs under `models/` ending in
  `.onnx`, or under `configs/` ending in `.cfg`. Nested paths remain supported.
- Every changed path must qualify. Deletes, renames, symlinks, submodules,
  executable/mode changes and other maintenance require review.
- Filenames must be Windows-compatible; spaces, brackets and ordinary Unicode
  names are supported. Names never become shell commands or local paths.
- Models retain the 5–50 MiB limit. Exact duplicate models are detected against
  the current target tree and within the submission, without downloading every
  existing model. Configs must be nonempty and at most 1 MiB. A PR is limited to
  256 MiB total and 3,000 changed files. Incomplete API responses fail closed.
- JSON must be an object, with unique keys and finite numbers; nesting and node
  count are bounded. This is format validation, not a complete Aimmy config
  schema or an assurance that every setting is useful.
- ONNX is parsed as binary protobuf in memory and checked statically, without
  inference, shape inference, custom operator loading or external tensor reads.
  External tensors anywhere in the protobuf are rejected, including nested
  graphs, sparse tensors, functions and training information.
- Inputs and outputs must be float32: one input `[B,3,H,W]`, one output
  `[B,>=5,N]`. Resolution is **not** fixed at 640, class count is **not** fixed at
  one, and anchor count is **not** fixed. Symbolic/anonymous dimensions and the
  legacy numeric `-1` unknown-dimension convention are supported. Literal zero
  and integers below -1 are rejected. Static validity is not a guarantee of
  runtime compatibility, model quality or safe execution in an end-user app.

## Trust boundary

1. The `pull_request_target` workflow executes only code from its trusted
   `github.sha`, with checkout credentials disabled. It never checks out PR HEAD,
   builds contributor code, reads PR dependency files or uses contributor URLs.
2. A read-only job resolves the actual `heads/Aimmy-V2` ref. PR `base.sha` metadata
   may lag the branch tip and is not used as a current-tip authority.
3. It resolves the merge base of pinned base/head SHAs, loads complete immutable
   trees, and derives the entire changed-path set itself. Mutable PR file pages
   are fully paginated and cross-checked, but never determine the allowlist on
   their own. This prevents hidden changes beyond page 100 and head-swap races
   between pages. Truncated trees and inconsistent lists stop automation.
4. Admitted blobs are fetched from their exact object SHAs, size/hash checked,
   and saved under generated numeric filenames. The trusted Docker image is
   built before parsing any PR data. Production parsing runs as a non-root user
   with no network, read-only root and input, no capabilities or host credentials,
   and CPU, memory, process, file-size, output and wall-time limits. No Docker
   socket or host directories are exposed to the parser.
5. A fresh job has the write token. It downloads no validation artifacts and
   independently repeats scope/mode/duplicate checks before accepting a small
   strict JSON verdict bound to the exact head and complete immutable manifest.
   Unrelated base movement does not invalidate a byte-identical parser proof:
   the publisher rechecks the new current base instead. Repeated concurrent base
   movement stops with rerun feedback rather than guessing.
6. The merge request supplies GitHub's `sha` guard. A newer head cannot be merged
   under an old verdict. Success is reported only when `data.merged === true`.
   Blocked merges leave an actionable comment and `needs-manual-merge` label.

Only the `github-actions[bot]` comment bearing this workflow's marker is updated.
Contributor text is bounded and escaped; mentions are neutralized. Infrastructure
failures are not mislabeled as invalid assets. Drafts and stale head/ref events
are ignored. Existing automation labels are cleared when a current result changes.

Invalid or empty submissions receive corrective feedback and labels and remain
open. GitHub's close-PR endpoint has no expected-head guard, so automatic closure
could catch a newly updated legitimate PR. The conservative flow avoids that
race; maintainers may close invalid submissions normally.

## Remaining GitHub limitations and deployment checks

These must be reviewed before enabling automatic publication in production:

- GitHub's merge endpoint atomically checks **head SHA only**, not expected base
  ref/SHA. Immediate current-PR and current-ref checks plus cancellation on edits
  reduce, but cannot eliminate, a last-instant base-retarget race. Protect other
  branches/rulesets so the Actions identity cannot merge into unintended targets,
  and review the intended target's policy. No branch settings, bypass permissions,
  tokens or security settings are changed by this patch. Do not treat this as an
  atomic head-and-base transaction.
- Direct branch merges/ref updates are deliberately not substituted: they change
  normal PR semantics and may circumvent required PR protections.
- Squash merges and the necessary Actions contents/PR write permissions must be
  allowed by repository/organization policy. Required checks and reviews remain
  in force; this workflow neither auto-approves reviews nor bypasses protections.
- The automation must be present on the trusted/default workflow branch before
  testing actual asset PR behavior. Changes to `pull_request_target` in a draft
  PR do not activate that changed privileged workflow.
- Check GitHub's current public-repository `pull_request_target` event policy;
  published documentation lists default restriction enforcement from November 2,
  2026. Any exception needs separate owner review and authorization.
- Static parsing needs current trusted dependencies. Actions are SHA-pinned;
  Docker's official Python image uses an explicit patch tag and all Python
  packages have exact version pins. The image tag is not an immutable digest;
  review image/dependency updates and their advisories regularly.
- The GITHUB_TOKEN merge may not trigger downstream push workflows. No asset
  publication/deployment is added here; verify any dependent external process.

Start with the draft PR's read-only `Asset submission policy tests` check. It
builds the Docker image, runs synthetic-data tests, and smoke-tests the exact
production container invocation. After review and deployment, verify an agreed
small submission from a fork. Do not merge a real contribution solely as a test.

## Tests

No repository models or contributor code are executed by these tests. Node uses
its built-in test runner; Python fixtures are generated locally.

```sh
node --test .github/scripts/asset-submissions/test_submissions.cjs
# Use Python 3.12 with the exact packages listed in Dockerfile installed.
python -I -m unittest discover -s .github/scripts/asset-submissions -p test_validator.py -v
docker build --pull --tag aimmy-asset-validator .github/scripts/asset-submissions
```

The separate read-only `pull_request` test workflow intentionally runs proposed
code without write permissions or secrets. Its outputs are never used as a
privileged merge verdict. Tests cover fork object lookup; >100-file pagination;
immutable-diff protection against hidden code; code/mixed PR exclusion; file
modes, deletes and renames; size and duplicate policy; hostile filenames; strict
verdicts; stale head/base/ref state; actual versus cached branch tips; merge
failure/confirmation; numeric download paths; blob integrity; dynamic/non640/
multiclass ONNX; nested external tensors; JSON parsing limits and the parser CLI.

References:
- [GitHub pull request merge API](https://docs.github.com/en/rest/pulls/pulls#merge-a-pull-request)
- [GitHub comparison API limits](https://docs.github.com/en/rest/commits/commits#compare-two-commits)
- [GitHub tree completeness](https://docs.github.com/en/rest/git/trees#get-a-tree)
- [GitHub pull_request_target security](https://docs.github.com/en/actions/reference/security/securely-using-pull_request_target)
- [ONNX shape inference/unknown dimensions](https://onnx.ai/onnx/repo-docs/ShapeInference.html)
- [ONNX security advisories](https://github.com/onnx/onnx/security/advisories)
