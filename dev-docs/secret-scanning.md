# Secret scanning

Two checks keep credentials out of this repository. It is public, so a secret that reaches
GitHub has leaked: a pull request ref cannot be deleted without GitHub Support, and the commit
stays reachable by its SHA. Rotate any real credential that gets as far as a push.

| Where | Scans | Blocks |
|---|---|---|
| `.githooks/pre-commit` | the staged files | the commit |
| `.github/workflows/secret-scan.yml` | each commit a pull request adds | the pull request check |

Both run the TruffleHog release pinned, by version and SHA-256, in `.githooks/trufflehog.pins`, and
both report only the lines a commit adds. A secret already in history does not fail every later
change to its file. Neither prints the secret: you get the path, line, finding type and, on the pull
request, the commit.

Both run without verification, so no candidate is sent to its provider. Every match counts,
including ones that might no longer work.

## The pre-commit hook

Enable it once per clone:

```sh
git config core.hooksPath .githooks
```

The first commit downloads TruffleHog from its GitHub release into `.git/tools/` and checks it
against the SHA-256 in the pins file. It does not use any `trufflehog` on `PATH`, which may be the
old Python version 2 under the same name.

The hook fails closed. The commit is blocked when:

- TruffleHog finds a likely secret on a line the commit adds or changes;
- the staged files cannot be listed or copied out for scanning;
- the pins file is missing a value, the download fails, or its hash does not match;
- there is no working `python` to read TruffleHog's results (install Python 3; on Windows, the
  Microsoft Store alias does not count);
- TruffleHog itself fails.

## The pull request check

Job **TruffleHog secret scan** scans the commits between the pull request's merge base and its head.
Commits brought in by merging the base branch are not scanned again. If TruffleHog itself fails, the
job fails rather than passing.

## Upgrading TruffleHog

`.githooks/trufflehog.pins` holds the version and the SHA-256 of each platform's archive. The hook
and the pull request check both read it, so they always run the same release. It lives outside
`.github/workflows/` because the default `GITHUB_TOKEN` cannot push changes to workflow files.

`.github/workflows/trufflehog-bump.yml` keeps it current, checking every day. When a newer release has been out for
at least three days, it opens or updates one pull request, `Bump TruffleHog to <version>`,
from the branch `automation/trufflehog-bump`. Before it does, it:

- checks the release's checksums against TruffleHog's signature, which only TruffleHog's own
  release workflow for that tag can produce, and takes the pinned hashes from them;
- runs the new binary on a sample credential, in both the git mode the pull request check uses and
  the filesystem mode the hook uses, and confirms the results still carry the fields both read.

If either step fails, no pull request is opened and the run fails. It can also be started by hand
from the Actions tab, where `min_release_age_days` can be lowered; `0` takes the newest release.

The bump commit is created through GitHub's API rather than pushed, so GitHub signs it. `dev`
requires signed commits, and a commit made with `git commit` on the runner would be unsigned.

The new binary runs only in a job whose token can read this repository and nothing more. The job
that pushes the branch and opens the pull request never runs it. That job takes only the version
number from the first one, and checks the release's signature again itself before using the hashes.

GitHub does not run other workflows on a pull request that Actions opened, so close and reopen the
bump pull request to run the checks before merging it. After it merges, each clone downloads the new
version on its next commit and deletes the old one.

To upgrade by hand, edit `.githooks/trufflehog.pins`, taking the hashes from that release's
`trufflehog_<version>_checksums.txt`.
