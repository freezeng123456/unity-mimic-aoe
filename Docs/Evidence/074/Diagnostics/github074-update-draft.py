"""Update only the already authorized private completion draft PR, then read back."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--body', type=Path, required=True)
parser.add_argument('--title', required=True)
parser.add_argument('--expected-head', required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
if args.output.exists():
    raise SystemExit('Receipt must be a new file')
credential = subprocess.run(['git', 'credential', 'fill'],
    input='protocol=https\nhost=github.com\n\n', text=True,
    stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
values = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
token = values.get('password')
if not token:
    raise SystemExit('Existing Git credential helper returned no credential')
opener = urllib.request.build_opener(urllib.request.ProxyHandler({'https': 'http://127.0.0.1:12450'}))
base = 'https://api.github.com/repos/freezeng123456/unity-mimic-aoe'


def api(suffix='', data=None):
    request = urllib.request.Request(base + suffix,
        data=None if data is None else json.dumps(data).encode(),
        headers={'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
                 'Content-Type': 'application/json', 'User-Agent': 'Frontier074-delivery'},
        method='GET' if data is None else 'PATCH')
    with opener.open(request, timeout=60) as response:
        return json.load(response)


def summarized(repo, pr, main, head):
    return {'repositoryPrivate': repo['private'], 'visibility': repo['visibility'],
            'repositoryUrl': repo['html_url'], 'prUrl': pr['html_url'],
            'number': pr['number'], 'state': pr['state'], 'draft': pr['draft'],
            'headBranch': pr['head']['ref'], 'headSha': head['object']['sha'],
            'baseBranch': pr['base']['ref'], 'mainSha': main['object']['sha'],
            'title': pr['title']}


repo = api(); before = api('/pulls/1')
main_before = api('/git/ref/heads/main')
head_before = api('/git/ref/heads/codex/land-rts-completion')
assert repo['private'] is True and repo['visibility'] == 'private'
assert before['number'] == 1 and before['state'] == 'open' and before['draft'] is True
assert before['head']['ref'] == 'codex/land-rts-completion' and before['base']['ref'] == 'main'
assert before['head']['sha'] == head_before['object']['sha'] == args.expected_head
assert main_before['object']['sha'] == '0bb044adbe40d0cab952e8c3b44e40eca8a6d3fe'
body = args.body.read_text()
api('/pulls/1', {'title': args.title, 'body': body})
after = api('/pulls/1'); repo_after = api()
main_after = api('/git/ref/heads/main')
head_after = api('/git/ref/heads/codex/land-rts-completion')
assert after['title'] == args.title and after['body'] == body
assert after['draft'] is True and after['state'] == 'open' and after['base']['ref'] == 'main'
assert repo_after['private'] is True and main_after == main_before
assert head_after['object']['sha'] == after['head']['sha'] == args.expected_head
receipt = {'schema': 'frontier074-private-draft-readback-1',
           'status': 'updated_and_read_back_private_open_draft',
           'recordedAtUtc': datetime.now(timezone.utc).isoformat(),
           'before': summarized(repo, before, main_before, head_before),
           'after': summarized(repo_after, after, main_after, head_after),
           'bodyExactAfterReadback': True, 'mainUnchanged': True,
           'scope': 'Title/body of existing private draft PR1 only; no merge, release, deployment or ACL change.'}
with args.output.open('x') as stream:
    json.dump(receipt, stream, indent=2)
    stream.write('\n')
print(json.dumps({'status': receipt['status'], 'url': after['html_url'],
                  'headSha': args.expected_head, 'mainUnchanged': True, 'receipt': str(args.output)}))
